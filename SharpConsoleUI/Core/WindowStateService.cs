// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Drawing;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Centralized service for managing window system state.
	/// Provides a single source of truth for window collection, active window,
	/// and interaction state (drag/resize operations).
	/// </summary>
	public class WindowStateService : IDisposable
	{
		private readonly object _lock = new();
		private readonly ILogService? _logService;
		private WindowSystemState _currentState = WindowSystemState.Empty;
		private readonly ConcurrentDictionary<string, Window> _windows = new();
		private readonly ConcurrentQueue<WindowSystemState> _stateHistory = new();
		private const int MaxHistorySize = 100;
		private bool _isDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="WindowStateService"/> class.
		/// </summary>
		/// <param name="logService">Optional log service for diagnostic logging.</param>
		public WindowStateService(ILogService? logService = null)
		{
			_logService = logService;
		}

		#region Properties

		/// <summary>
		/// Gets the current window system state.
		/// </summary>
		public WindowSystemState CurrentState
		{
			get
			{
				lock (_lock)
				{
					return _currentState;
				}
			}
		}

		/// <summary>
		/// Gets the currently active window.
		/// </summary>
		public Window? ActiveWindow => CurrentState.ActiveWindow;

		/// <summary>
		/// Gets a value indicating whether a drag operation is in progress.
		/// </summary>
		public bool IsDragging => CurrentState.Interaction.IsDragging;

		/// <summary>
		/// Gets a value indicating whether a resize operation is in progress.
		/// </summary>
		public bool IsResizing => CurrentState.Interaction.IsResizing;

		/// <summary>
		/// Gets the current drag state (if dragging).
		/// </summary>
		public DragState? CurrentDrag => CurrentState.Interaction.Drag;

		/// <summary>
		/// Gets the current resize state (if resizing).
		/// </summary>
		public ResizeState? CurrentResize => CurrentState.Interaction.Resize;

		/// <summary>
		/// Gets all registered windows.
		/// </summary>
		public IReadOnlyDictionary<string, Window> Windows => _windows;

		/// <summary>
		/// Gets the number of registered windows.
		/// </summary>
		public int WindowCount => _windows.Count;

		#endregion

		#region Events

		/// <summary>
		/// Occurs when any aspect of window system state changes.
		/// </summary>
		public event EventHandler<WindowSystemStateChangedEventArgs>? StateChanged;

		/// <summary>
		/// Occurs when a window is created/registered.
		/// </summary>
		public event EventHandler<WindowEventArgs>? WindowCreated;

		/// <summary>
		/// Occurs when a window is closed/unregistered.
		/// </summary>
		public event EventHandler<WindowEventArgs>? WindowClosed;

		/// <summary>
		/// Occurs when the active window changes.
		/// </summary>
		public event EventHandler<WindowActivatedEventArgs>? WindowActivated;

		/// <summary>
		/// Occurs when a window's state (min/max/restore) changes.
		/// </summary>
		public event EventHandler<WindowStateEventArgs>? WindowStateChanged;

		/// <summary>
		/// Occurs when interaction state (drag/resize) changes.
		/// </summary>
		public event EventHandler<InteractionStateChangedEventArgs>? InteractionChanged;

		#endregion

		#region Window Management

		/// <summary>
		/// Registers a new window with the system.
		/// </summary>
		/// <param name="window">The window to register.</param>
		/// <param name="activate">Whether to activate the window after registration.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="window"/> is null.</exception>
		public void RegisterWindow(Window window, bool activate = true)
		{
			if (window == null)
				throw new ArgumentNullException(nameof(window));

			lock (_lock)
			{
				_logService?.LogDebug($"Registering window: {window.Title} (ZIndex: {GetMaxZIndex() + 1})", "WindowState");

				// Calculate Z-index
				window.ZIndex = GetMaxZIndex() + 1;

				// Add to collection
				_windows[window.Guid] = window;

				// Update state
				var newState = CreateStateSnapshot();
				if (activate || _currentState.ActiveWindow == null)
				{
					newState = newState with { ActiveWindow = window };
				}

				UpdateStateInternal(newState);

				// Fire window created event
				FireWindowEvent(window, WindowEventType.Created);

				// Fire activation event if needed
				if (activate || _currentState.ActiveWindow == window)
				{
					FireWindowActivated(null, window);
				}
			}
		}

		/// <summary>
		/// Unregisters a window from the system.
		/// </summary>
		/// <param name="window">The window to unregister.</param>
		public void UnregisterWindow(Window window)
		{
			if (window == null)
				return;

			lock (_lock)
			{
				if (!_windows.TryRemove(window.Guid, out _))
					return;

				_logService?.LogDebug($"Unregistering window: {window.Title}", "WindowState");

				var previousActive = _currentState.ActiveWindow;
				var newState = CreateStateSnapshot();

				// If this was the active window, find a new one
				if (previousActive == window)
				{
					var newActive = FindNextActiveWindow(window);
					newState = newState with { ActiveWindow = newActive };
				}

				UpdateStateInternal(newState);

				// Fire events
				FireWindowEvent(window, WindowEventType.Closed);

				if (previousActive == window)
				{
					FireWindowActivated(window, newState.ActiveWindow);
				}
			}
		}

		/// <summary>
		/// Gets a window by its GUID.
		/// </summary>
		/// <param name="guid">The GUID of the window to find.</param>
		/// <returns>The window if found; otherwise, null.</returns>
		public Window? GetWindow(string guid)
		{
			return _windows.TryGetValue(guid, out var window) ? window : null;
		}

		/// <summary>
		/// Gets all windows ordered by Z-index (back to front).
		/// </summary>
		/// <returns>A read-only list of windows ordered by Z-index.</returns>
		public IReadOnlyList<Window> GetWindowsByZOrder()
		{
			return _windows.Values.OrderBy(w => w.ZIndex).ToList();
		}

		/// <summary>
		/// Gets only visible windows (excludes minimized).
		/// </summary>
		/// <returns>A read-only list of visible windows ordered by Z-index.</returns>
		public IReadOnlyList<Window> GetVisibleWindows()
		{
			return _windows.Values
				.Where(w => w.State != WindowState.Minimized)
				.OrderBy(w => w.ZIndex)
				.ToList();
		}

		/// <summary>
		/// Gets the maximum Z-index among all windows.
		/// </summary>
		/// <returns>The maximum Z-index, or 0 if no windows exist.</returns>
		public int GetMaxZIndex()
		{
			return _windows.Count > 0 ? _windows.Values.Max(w => w.ZIndex) : 0;
		}

		/// <summary>
		/// Finds a window by its Name property.
		/// </summary>
		/// <param name="name">The window name to search for.</param>
		/// <returns>The window if found; otherwise, null.</returns>
		public Window? FindWindowByName(string name)
		{
			if (string.IsNullOrEmpty(name)) return null;
			return _windows.Values.FirstOrDefault(w => w.Name == name);
		}

		/// <summary>
		/// Checks if a window with the given name exists.
		/// </summary>
		/// <param name="name">The window name to check.</param>
		/// <returns>True if a window with the given name exists; otherwise, false.</returns>
		public bool WindowExists(string name)
		{
			return FindWindowByName(name) != null;
		}

		/// <summary>
		/// Checks if any window is dirty (needs re-rendering).
		/// </summary>
		/// <returns>True if any window needs re-rendering; otherwise, false.</returns>
		public bool AnyWindowDirty()
		{
			return _windows.Values.Any(w => w.IsDirty);
		}

		#endregion

		#region Active Window Management

		/// <summary>
		/// Activates a window, making it the active window.
		/// </summary>
		/// <param name="window">The window to activate, or null to deactivate all windows.</param>
		public void ActivateWindow(Window? window)
		{
			lock (_lock)
			{
				// Defensive: ensure window's IsActive flag matches service state
				// This handles the case where RegisterWindow sets ActiveWindow but
				// SetIsActive was never called (e.g., first window added to system)
				if (window != null && !window.GetIsActive())
				{
					window.SetIsActive(true);
				}

				if (window == _currentState.ActiveWindow)
					return;

				var previousActive = _currentState.ActiveWindow;
				_logService?.LogTrace($"Active window changing: {previousActive?.Title ?? "None"} -> {window?.Title ?? "None"}", "WindowState");

				// Update active status on windows
				previousActive?.SetIsActive(false);
				window?.SetIsActive(true);

				// Update Z-index to bring to front
				if (window != null)
				{
					window.ZIndex = GetMaxZIndex() + 1;
				}

				// Update state
				var newState = CreateStateSnapshot() with { ActiveWindow = window };
				UpdateStateInternal(newState);

				// Fire events
				FireWindowActivated(previousActive, window);
			}
		}

		/// <summary>
		/// Activates the next window in Z-order.
		/// </summary>
		public void ActivateNextWindow()
		{
			lock (_lock)
			{
				var windows = _windows.Values.ToList();
				if (windows.Count == 0)
					return;

				var currentIndex = windows.IndexOf(_currentState.ActiveWindow ?? windows.First());
				var nextIndex = (currentIndex + 1) % windows.Count;
				var nextWindow = windows[nextIndex];

				// Restore if minimized
				if (nextWindow.State == WindowState.Minimized)
				{
					nextWindow.State = WindowState.Normal;
				}

				ActivateWindow(nextWindow);
			}
		}

		/// <summary>
		/// Activates the previous window in Z-order.
		/// </summary>
		public void ActivatePreviousWindow()
		{
			lock (_lock)
			{
				var windows = _windows.Values.ToList();
				if (windows.Count == 0)
					return;

				var currentIndex = windows.IndexOf(_currentState.ActiveWindow ?? windows.First());
				var prevIndex = (currentIndex - 1 + windows.Count) % windows.Count;
				var prevWindow = windows[prevIndex];

				// Restore if minimized
				if (prevWindow.State == WindowState.Minimized)
				{
					prevWindow.State = WindowState.Normal;
				}

				ActivateWindow(prevWindow);
			}
		}

		/// <summary>
		/// Activates a window by its index (1-based, for Alt+1-9).
		/// </summary>
		/// <param name="index">The 1-based index of the window to activate.</param>
		public void ActivateWindowByIndex(int index)
		{
			lock (_lock)
			{
				var topLevelWindows = _windows.Values
					.Where(w => w.ParentWindow == null)
					.ToList();

				if (index < 1 || index > topLevelWindows.Count)
					return;

				var window = topLevelWindows[index - 1];

				// Restore if minimized
				if (window.State == WindowState.Minimized)
				{
					window.State = WindowState.Normal;
				}

				ActivateWindow(window);
			}
		}

		#endregion

		#region Window State Operations

		/// <summary>
		/// Minimizes a window.
		/// </summary>
		/// <param name="window">The window to minimize.</param>
		public void MinimizeWindow(Window window)
		{
			if (window == null || !window.IsMinimizable)
				return;

			lock (_lock)
			{
				var previousState = window.State;
				window.Minimize();

				// If this was the active window, activate another
				if (_currentState.ActiveWindow == window)
				{
					var nextWindow = FindNextActiveWindow(window);
					ActivateWindow(nextWindow);
				}

				FireWindowStateChanged(window, previousState, WindowState.Minimized);
			}
		}

		/// <summary>
		/// Maximizes a window.
		/// </summary>
		/// <param name="window">The window to maximize.</param>
		public void MaximizeWindow(Window window)
		{
			if (window == null || !window.IsMaximizable)
				return;

			lock (_lock)
			{
				var previousState = window.State;
				window.Maximize();
				FireWindowStateChanged(window, previousState, WindowState.Maximized);
			}
		}

		/// <summary>
		/// Restores a window to normal state.
		/// </summary>
		/// <param name="window">The window to restore.</param>
		public void RestoreWindow(Window window)
		{
			if (window == null)
				return;

			lock (_lock)
			{
				var previousState = window.State;
				window.Restore();
				FireWindowStateChanged(window, previousState, WindowState.Normal);
			}
		}

		/// <summary>
		/// Closes a window.
		/// </summary>
		/// <param name="window">The window to close.</param>
		public void CloseWindow(Window window)
		{
			if (window == null)
				return;

			UnregisterWindow(window);
		}

		#endregion

		#region Drag Operations

		/// <summary>
		/// Starts a drag operation.
		/// </summary>
		/// <param name="window">The window being dragged.</param>
		/// <param name="mousePos">The initial mouse position.</param>
		public void StartDrag(Window window, Point mousePos)
		{
			if (window == null)
				return;

			lock (_lock)
			{
				var newInteraction = InteractionState.StartDrag(
					window,
					mousePos,
					new Point(window.Left, window.Top)
				);

				var newState = _currentState with
				{
					Interaction = newInteraction,
					UpdateTime = DateTime.UtcNow
				};

				var previousInteraction = _currentState.Interaction;
				UpdateStateInternal(newState);
				FireInteractionChanged(previousInteraction, newInteraction);
			}
		}

		/// <summary>
		/// Ends the current drag operation.
		/// </summary>
		public void EndDrag()
		{
			lock (_lock)
			{
				if (!_currentState.Interaction.IsDragging)
					return;

				var previousInteraction = _currentState.Interaction;
				var newState = _currentState with
				{
					Interaction = InteractionState.None,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(newState);
				FireInteractionChanged(previousInteraction, InteractionState.None);
			}
		}

		#endregion

		#region Resize Operations

		/// <summary>
		/// Starts a resize operation.
		/// </summary>
		/// <param name="window">The window being resized.</param>
		/// <param name="direction">The resize direction.</param>
		/// <param name="mousePos">The initial mouse position.</param>
		public void StartResize(Window window, ResizeDirection direction, Point mousePos)
		{
			if (window == null || !window.IsResizable)
				return;

			lock (_lock)
			{
				var newInteraction = InteractionState.StartResize(
					window,
					direction,
					mousePos,
					new Size(window.Width, window.Height),
					new Point(window.Left, window.Top)
				);

				var newState = _currentState with
				{
					Interaction = newInteraction,
					UpdateTime = DateTime.UtcNow
				};

				var previousInteraction = _currentState.Interaction;
				UpdateStateInternal(newState);
				FireInteractionChanged(previousInteraction, newInteraction);
			}
		}

		/// <summary>
		/// Ends the current resize operation.
		/// </summary>
		public void EndResize()
		{
			lock (_lock)
			{
				if (!_currentState.Interaction.IsResizing)
					return;

				var previousInteraction = _currentState.Interaction;
				var newState = _currentState with
				{
					Interaction = InteractionState.None,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(newState);
				FireInteractionChanged(previousInteraction, InteractionState.None);
			}
		}

		#endregion

		#region Z-Order Management

		/// <summary>
		/// Brings a window to the front (highest Z-index).
		/// </summary>
		/// <param name="window">The window to bring to front.</param>
		public void BringToFront(Window window)
		{
			if (window == null)
				return;

			lock (_lock)
			{
				window.ZIndex = GetMaxZIndex() + 1;
				UpdateStateInternal(CreateStateSnapshot());
				FireWindowEvent(window, WindowEventType.ZOrderChanged);
			}
		}

		/// <summary>
		/// Sends a window to the back (lowest Z-index).
		/// </summary>
		/// <param name="window">The window to send to back.</param>
		public void SendToBack(Window window)
		{
			if (window == null)
				return;

			lock (_lock)
			{
				var minZIndex = _windows.Count > 0 ? _windows.Values.Min(w => w.ZIndex) : 0;
				window.ZIndex = minZIndex - 1;
				UpdateStateInternal(CreateStateSnapshot());
				FireWindowEvent(window, WindowEventType.ZOrderChanged);
			}
		}

		#endregion

		#region Debugging

		/// <summary>
		/// Gets recent state history for debugging.
		/// </summary>
		/// <returns>A read-only list of recent window system states.</returns>
		public IReadOnlyList<WindowSystemState> GetHistory()
		{
			return _stateHistory.ToArray();
		}

		/// <summary>
		/// Clears the state history.
		/// </summary>
		public void ClearHistory()
		{
			while (_stateHistory.TryDequeue(out _)) { }
		}

		/// <summary>
		/// Gets a debug string representation of current state.
		/// </summary>
		/// <returns>A formatted string containing the current state information.</returns>
		public string GetDebugInfo()
		{
			var state = CurrentState;
			return $"WindowState: Windows={state.WindowCount}, " +
			       $"Active={state.ActiveWindow?.Title ?? "none"}, " +
			       $"Dragging={state.Interaction.IsDragging}, " +
			       $"Resizing={state.Interaction.IsResizing}";
		}

		#endregion

		#region Private Helpers

		private WindowSystemState CreateStateSnapshot()
		{
			return new WindowSystemState
			{
				ActiveWindow = _currentState.ActiveWindow,
				Windows = new Dictionary<string, Window>(_windows),
				Interaction = _currentState.Interaction,
				UpdateTime = DateTime.UtcNow
			};
		}

		private void UpdateStateInternal(WindowSystemState newState)
		{
			var previousState = _currentState;

			// Skip if state hasn't meaningfully changed
			if (previousState.ActiveWindow == newState.ActiveWindow &&
			    previousState.Interaction == newState.Interaction &&
			    previousState.WindowCount == newState.WindowCount)
			{
				return;
			}

			_currentState = newState;

			// Add to history
			_stateHistory.Enqueue(newState);
			while (_stateHistory.Count > MaxHistorySize)
			{
				_stateHistory.TryDequeue(out _);
			}

			// Fire state changed event
			var args = new WindowSystemStateChangedEventArgs(previousState, newState);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					StateChanged?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private Window? FindNextActiveWindow(Window? excludeWindow)
		{
			return _windows.Values
				.Where(w => w != excludeWindow && w.State != WindowState.Minimized)
				.OrderByDescending(w => w.ZIndex)
				.FirstOrDefault();
		}

		private void FireWindowEvent(Window window, WindowEventType eventType, object? data = null)
		{
			var args = new WindowEventArgs(window, eventType, data);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					switch (eventType)
					{
						case WindowEventType.Created:
							WindowCreated?.Invoke(this, args);
							break;
						case WindowEventType.Closed:
							WindowClosed?.Invoke(this, args);
							break;
					}
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private void FireWindowActivated(Window? previousWindow, Window? newWindow)
		{
			var args = new WindowActivatedEventArgs(previousWindow, newWindow);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					WindowActivated?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private void FireWindowStateChanged(Window window, WindowState previousState, WindowState newState)
		{
			var args = new WindowStateEventArgs(window, previousState, newState);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					WindowStateChanged?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private void FireInteractionChanged(InteractionState previousState, InteractionState newState)
		{
			var args = new InteractionStateChangedEventArgs(previousState, newState);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					InteractionChanged?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		#endregion

		#region IDisposable

		/// <inheritdoc/>
		public void Dispose()
		{
			if (_isDisposed)
				return;

			_isDisposed = true;
			ClearHistory();
			_windows.Clear();

			// Clear event handlers
			StateChanged = null;
			WindowCreated = null;
			WindowClosed = null;
			WindowActivated = null;
			WindowStateChanged = null;
			InteractionChanged = null;
		}

		#endregion
	}
}

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
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Rendering;

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
		private readonly Func<ConsoleWindowSystem>? _getWindowSystem;
		private readonly ModalStateService? _modalStateService;
		private readonly FocusStateService? _focusStateService;
		private Renderer? _renderer;
		private readonly IConsoleDriver? _consoleDriver;
		private WindowSystemState _currentState = WindowSystemState.Empty;
		private readonly ConcurrentDictionary<string, Window> _windows = new();
		private readonly ConcurrentQueue<WindowSystemState> _stateHistory = new();
		private const int MaxHistorySize = 100;
		private bool _isDisposed;

		// Track windows currently being flashed to prevent concurrent flashes
		private readonly Dictionary<Window, FlashState> _flashingWindows = new();

		/// <summary>
		/// Tracks the state of a window flash animation.
		/// </summary>
		private class FlashState
		{
			public System.Timers.Timer? Timer { get; set; }
			public float Intensity { get; set; }  // 0.0 to 1.0
			public Spectre.Console.Color FlashColor { get; set; }
			public DateTime StartTime { get; set; }
			public int DurationMs { get; set; }
			public float MaxIntensity { get; set; } = 0.3f;  // 30% overlay
			public int CurrentFlashIndex { get; set; }
			public int TotalFlashes { get; set; }
			public Windows.WindowRenderer.BufferPaintDelegate? PaintHandler { get; set; }
			public EventHandler<ClosingEventArgs>? CleanupHandler { get; set; }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WindowStateService"/> class.
		/// </summary>
		/// <param name="logService">Optional log service for diagnostic logging.</param>
		/// <param name="getWindowSystem">Optional function to get window system context (for lifecycle methods).</param>
		/// <param name="modalStateService">Optional modal state service (for lifecycle methods).</param>
		/// <param name="focusStateService">Optional focus state service (for lifecycle methods).</param>
		/// <param name="renderer">Optional renderer (for lifecycle methods).</param>
		/// <param name="consoleDriver">Optional console driver (for lifecycle methods).</param>
		public WindowStateService(
			ILogService? logService = null,
			Func<ConsoleWindowSystem>? getWindowSystem = null,
			ModalStateService? modalStateService = null,
			FocusStateService? focusStateService = null,
			Renderer? renderer = null,
			IConsoleDriver? consoleDriver = null)
		{
			_logService = logService;
			_getWindowSystem = getWindowSystem;
			_modalStateService = modalStateService;
			_focusStateService = focusStateService;
			_renderer = renderer;
			_consoleDriver = consoleDriver;
		}

	/// <summary>
	/// Sets the renderer for this service. Used for screen redraws during window close.
	/// </summary>
	public void SetRenderer(Renderer renderer)
	{
		_renderer = renderer;
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

				EnsureAlwaysOnTopZOrder();

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
		/// Gets all windows ordered by Z-index (front to back).
		/// Higher Z-index means closer to front/top.
		/// </summary>
		/// <returns>A read-only list of windows ordered by Z-index (front to back).</returns>
		public IReadOnlyList<Window> GetWindowsByZOrder()
		{
			return _windows.Values.OrderByDescending(w => w.ZIndex).ToList();
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
		/// Ensures all AlwaysOnTop windows have ZIndex above all normal windows.
		/// Call after any ZIndex change to maintain the invariant.
		/// </summary>
		private void EnsureAlwaysOnTopZOrder()
		{
			int maxNormalZIndex = 0;
			bool hasNormal = false;

			foreach (var w in _windows.Values)
			{
				if (!w.AlwaysOnTop && w.ZIndex > maxNormalZIndex)
				{
					maxNormalZIndex = w.ZIndex;
					hasNormal = true;
				}
			}

			if (!hasNormal) return;

			// Sort by current ZIndex to preserve relative order among AlwaysOnTop windows
			var onTopWindows = _windows.Values
				.Where(w => w.AlwaysOnTop && w.ZIndex <= maxNormalZIndex)
				.OrderBy(w => w.ZIndex)
				.ToList();

			foreach (var w in onTopWindows)
			{
				w.ZIndex = ++maxNormalZIndex;
				w.Invalidate(true);
			}
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
					var oldZIndex = window.ZIndex;
					window.ZIndex = GetMaxZIndex() + 1;

					// Invalidate window when z-order changes because visibleRegions change
					// Window may now have more visible area and needs to repaint with new clipRect
					window.Invalidate(true);

					EnsureAlwaysOnTopZOrder();
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
				var windows = _windows.Values.OrderBy(w => w.ZIndex).ToList();
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
				var windows = _windows.Values.OrderBy(w => w.ZIndex).ToList();
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
		/// Brings a window to the front (highest Z-index) and activates it.
		/// This matches typical windowing system behavior where bringing a window to front makes it active.
		/// </summary>
		/// <param name="window">The window to bring to front.</param>
		public void BringToFront(Window window)
		{
			if (window == null)
				return;

			lock (_lock)
			{
				// Activate window (which also brings it to front with highest Z-index)
				// This ensures the window is both visually on top and receives input
				ActivateWindow(window);

				FireWindowEvent(window, WindowEventType.ZOrderChanged);
			}
		}

		/// <summary>
		/// Sends a window to the back (lowest Z-index).
		/// If the window was active, activates the next available window.
		/// </summary>
		/// <param name="window">The window to send to back.</param>
		public void SendToBack(Window window)
		{
			if (window == null)
				return;

			lock (_lock)
			{
				var wasActive = _currentState.ActiveWindow == window;

				var minZIndex = _windows.Count > 0 ? _windows.Values.Min(w => w.ZIndex) : 0;
				window.ZIndex = minZIndex - 1;
				UpdateStateInternal(CreateStateSnapshot());

				// Invalidate window when z-order changes because visibleRegions change
				// Window may now have less visible area and needs to repaint with new clipRect
				window.Invalidate(true);

				// If this was the active window, activate the next one
				if (wasActive)
				{
					var nextWindow = FindNextActiveWindow(window);
					ActivateWindow(nextWindow);
				}

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

		/// <summary>
		/// Invalidates all windows that overlap with the specified window.
		/// Used after z-order changes to ensure proper rendering of affected windows.
		/// </summary>
		private void InvalidateOverlappingWindows(Window window)
		{
			if (window == null) return;

			var windowRect = new System.Drawing.Rectangle(window.Left, window.Top, window.Width, window.Height);

			foreach (var other in _windows.Values)
			{
				if (other == window) continue;

				// Invalidate any window that overlaps with the moved window
				if (Helpers.GeometryHelpers.DoesRectangleOverlapWindow(windowRect, other))
				{
					other.Invalidate(true);
				}
			}
		}

		#endregion

		#region IDisposable

	#region Window Activation Methods

	#region Window Lifecycle Methods

	/// <summary>
	/// Adds a window to the window system and optionally activates it.
	/// </summary>
	/// <param name="window">The window to add.</param>
	/// <param name="activateWindow">Whether to activate the window after adding. Defaults to true.</param>
	/// <returns>The added window.</returns>
	public Window AddWindow(Window window, bool activateWindow = true)
	{
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var context = _getWindowSystem();

		_logService?.LogDebug($"Adding window: {window.Title} (GUID: {window.Guid})", "Window");

		// Register window (handles ZIndex assignment and adding to collection)
		RegisterWindow(window, activate: false);

		// Register modal windows with the modal state service
		if (window.IsModal && _modalStateService != null)
		{
			_modalStateService.PushModal(window, window.ParentWindow);
			_logService?.LogDebug($"Modal window pushed: {window.Title}", "Modal");
		}

		// Activate the window if needed (through SetActiveWindow for modal logic)
		if (context.ActiveWindow == null || activateWindow)
		{
			context.SetActiveWindow(window);
		}

		window.WindowIsAdded();

		_logService?.LogDebug($"Window added successfully: {window.Title}", "Window");
		return window;
	}

	/// <summary>
	/// Closes a modal window and optionally activates its parent window.
	/// </summary>
	/// <param name="modalWindow">The modal window to close. If null or not a modal window, the method returns without action.</param>
	public void CloseModalWindow(Window? modalWindow)
	{
		if (modalWindow == null || !modalWindow.IsModal)
			return;
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var context = _getWindowSystem();

		_logService?.LogDebug($"Closing modal window: {modalWindow.Title}", "Modal");

		// Store the parent window before closing
		Window? parentWindow = modalWindow.ParentWindow;

		// Close the modal window
		if (CloseWindow(modalWindow))
		{
			// If we have a parent window, ensure it becomes active
			if (parentWindow != null && context.Windows.ContainsKey(parentWindow.Guid))
			{
				context.SetActiveWindow(parentWindow);
			}
		}
	}

	/// <summary>
	/// Closes a window and removes it from the window system.
	/// </summary>
	/// <param name="window">The window to close. If null or not in the system, returns false.</param>
	/// <param name="activateParent">Whether to activate the parent window after closing. Defaults to true.</param>
	/// <param name="force">If true, forces the window to close even if IsClosable is false or OnClosing cancels.</param>
	/// <returns>True if the window was closed successfully; false otherwise.</returns>
	public bool CloseWindow(Window? window, bool activateParent = true, bool force = false)
	{
		if (window == null) return false;
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var context = _getWindowSystem();
		if (!context.Windows.ContainsKey(window.Guid)) return false;

		_logService?.LogDebug($"Closing window: {window.Title} (GUID: {window.Guid}, Force: {force})", "Window");

		// STEP 1: Check if close is allowed BEFORE any state changes
		// This fires OnClosing and respects IsClosable (unless forced)
		if (!window.TryClose(force))
		{
			_logService?.LogDebug($"Window close cancelled by OnClosing handler: {window.Title}", "Window");
			return false;
		}

		// STEP 2: Close is allowed - now safe to remove from system
		Window? parentWindow = window.ParentWindow;
		bool wasActive = (window == context.ActiveWindow);

		// Unregister modal window from modal state service
		if (window.IsModal && _modalStateService != null)
		{
			_modalStateService.PopModal(window);
		}

		// Clear focus state for this window
		_focusStateService?.ClearFocus(window);

		// Remove from window collection via service
		// This prevents race condition where render thread tries to render disposed controls
		UnregisterWindow(window);

		// Activate the next window (UnregisterWindow updates state but doesn't call SetIsActive)
		if (wasActive)
		{
			Window? targetWindow = null;

			if (activateParent && parentWindow != null && context.Windows.ContainsKey(parentWindow.Guid))
			{
				// Let ModalStateService resolve correct target (may redirect to modal child)
				// This prevents the "black hole" bug when closing a modal that has other modals stacked
				targetWindow = _modalStateService?.GetEffectiveActivationTarget(parentWindow);
			}
			else if (context.Windows.Count > 0)
			{
				// Activate window with highest Z-Index
				int maxZIndex = context.Windows.Values.Max(w => w.ZIndex);
				var nextWindow = context.Windows.Values.FirstOrDefault(w => w.ZIndex == maxZIndex);

				if (nextWindow != null && _modalStateService != null)
				{
					// Use GetEffectiveActivationTarget to handle modal redirection
					targetWindow = _modalStateService.GetEffectiveActivationTarget(nextWindow);
				}
			}

			if (targetWindow != null)
			{
				context.SetActiveWindow(targetWindow);
			}
		}

		// STEP 3: Complete the close (fire OnClosed, dispose controls)
		window.CompleteClose();

		// Clear only the closed window's area (not entire screen!)
		if (_renderer != null && _consoleDriver != null)
		{
			var theme = context.Theme;
			// BUG FIX: Only clear the window's rectangle, not entire desktop
			_renderer.FillRect(window.Left, window.Top, window.Width, window.Height,
							  theme.DesktopBackgroundChar, theme.DesktopBackgroundColor, theme.DesktopForegroundColor);

			// Invalidate remaining windows in case they were partially occluded
			foreach (var w in context.Windows.Values)
			{
				w.Invalidate(true);
			}

			// BUG FIX: Removed immediate Flush() - let normal render cycle handle it
			// The next UpdateDisplay() will detect the changes and output clearing
			// This maintains frame-based rendering consistency

			// CRITICAL: Force next render even if no windows remain
			// Without this, Run() loop won't call UpdateDisplay() when AnyWindowDirty() returns false
			context.Render.DesktopNeedsRender = true;
		}

		return true;
	}

	/// <summary>
	/// Flashes a window to draw user attention using a smooth pulse animation effect.
	/// Uses PostBufferPaint to apply a color overlay without modifying window properties.
	/// </summary>
	/// <param name="window">The window to flash. If null, the method returns without action.</param>
	/// <param name="flashCount">The number of times to flash. Defaults to 1.</param>
	/// <param name="flashDuration">The duration of each flash in milliseconds. Defaults to 150.</param>
	/// <param name="flashBackgroundColor">The background color to use for flashing. If null, uses the theme's ModalFlashColor.</param>
	public void FlashWindow(Window? window, int flashCount = 1, int flashDuration = 150, Spectre.Console.Color? flashBackgroundColor = null)
	{
		if (window?.Renderer == null) return;
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var context = _getWindowSystem();

		// Prevent multiple concurrent flashes on the same window
		lock (_flashingWindows)
		{
			if (_flashingWindows.ContainsKey(window)) return;
		}

		var flashColor = flashBackgroundColor ?? context.Theme.ModalFlashColor;
		int totalDuration = flashDuration * 2;  // Duration for fade up + fade down

		var state = new FlashState
		{
			FlashColor = flashColor,
			StartTime = DateTime.Now,
			DurationMs = totalDuration,
			CurrentFlashIndex = 0,
			TotalFlashes = flashCount,
			Timer = new System.Timers.Timer(16)  // ~60 FPS
		};

		// PostBufferPaint handler that applies the flash overlay
		void FlashOverlay(Layout.CharacterBuffer buffer, Layout.LayoutRect dirtyRegion, Layout.LayoutRect clipRect)
		{
			ApplyFlashOverlay(buffer, flashColor, state.Intensity);
		}

		state.PaintHandler = FlashOverlay;

		// Timer tick handler - updates intensity using sine wave
		state.Timer.Elapsed += (sender, e) =>
		{
			var elapsed = (DateTime.Now - state.StartTime).TotalMilliseconds;
			var progress = (float)(elapsed / totalDuration);

			if (progress >= 1.0f)
			{
				// Single flash complete
				state.CurrentFlashIndex++;

				if (state.CurrentFlashIndex < state.TotalFlashes)
				{
					// Start next flash with delay
					state.StartTime = DateTime.Now.AddMilliseconds(flashDuration);
					state.Intensity = 0f;
				}
				else
				{
					// All flashes complete - cleanup
					CleanupFlash(window, state);
				}
			}
			else
			{
				// Update intensity using sine wave for smooth pulse
				state.Intensity = MathF.Sin(progress * MathF.PI) * state.MaxIntensity;
				window.Invalidate(redrawAll: true);
			}
		};

		// Cleanup handler if window closes during flash
		state.CleanupHandler = (s, e) => CleanupFlash(window, state);

		// Subscribe to events
		window.Renderer.PostBufferPaint += FlashOverlay;
		window.OnClosing += state.CleanupHandler;

		lock (_flashingWindows)
		{
			_flashingWindows[window] = state;
		}

		state.Timer.Start();
	}

	/// <summary>
	/// Applies a color overlay to the entire buffer for the flash effect.
	/// </summary>
	/// <param name="buffer">The character buffer to modify.</param>
	/// <param name="flashColor">The color to blend toward.</param>
	/// <param name="intensity">The intensity of the effect (0.0 to 1.0).</param>
	private void ApplyFlashOverlay(Layout.CharacterBuffer buffer, Spectre.Console.Color flashColor, float intensity)
	{
		if (intensity <= 0.001f) return;  // Skip if negligible

		for (int y = 0; y < buffer.Height; y++)
		{
			for (int x = 0; x < buffer.Width; x++)
			{
				var cell = buffer.GetCell(x, y);

				// Blend background toward flash color at full intensity
				var newBg = BlendColor(cell.Background, flashColor, intensity);

				// Blend foreground toward flash color at half intensity for subtlety
				var newFg = BlendColor(cell.Foreground, flashColor, intensity * 0.5f);

				buffer.SetCell(x, y, cell.Character, newFg, newBg);
			}
		}
	}

	/// <summary>
	/// Blends two colors together by the specified amount.
	/// </summary>
	/// <param name="original">The original color.</param>
	/// <param name="target">The target color to blend toward.</param>
	/// <param name="amount">The blend amount (0.0 = original, 1.0 = target).</param>
	/// <returns>The blended color.</returns>
	private Spectre.Console.Color BlendColor(Spectre.Console.Color original, Spectre.Console.Color target, float amount)
	{
		return new Spectre.Console.Color(
			(byte)(original.R + (target.R - original.R) * amount),
			(byte)(original.G + (target.G - original.G) * amount),
			(byte)(original.B + (target.B - original.B) * amount)
		);
	}

	/// <summary>
	/// Cleans up a flash animation by disposing resources and unsubscribing events.
	/// </summary>
	/// <param name="window">The window being flashed.</param>
	/// <param name="state">The flash state to clean up.</param>
	private void CleanupFlash(Window window, FlashState state)
	{
		// Stop and dispose timer
		if (state.Timer != null)
		{
			state.Timer.Stop();
			state.Timer.Dispose();
			state.Timer = null;
		}

		// Unsubscribe from PostBufferPaint
		if (window.Renderer != null && state.PaintHandler != null)
		{
			window.Renderer.PostBufferPaint -= state.PaintHandler;
		}

		// Unsubscribe from OnClosing
		if (state.CleanupHandler != null)
		{
			window.OnClosing -= state.CleanupHandler;
		}

		// Remove from tracking
		lock (_flashingWindows)
		{
			_flashingWindows.Remove(window);
		}

		// Final invalidation to clear any remaining overlay
		window.Invalidate(redrawAll: true);
	}

	/// <summary>
	/// Activates the next non-minimized window after a window is minimized.
	/// </summary>
	/// <param name="minimizedWindow">The window that was just minimized.</param>
	public void ActivateNextNonMinimizedWindow(Window minimizedWindow)
	{
		if (minimizedWindow == null) return;
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var context = _getWindowSystem();

		// Find the next non-minimized window to activate
		var nextWindow = context.Windows.Values
			.Where(w => w != minimizedWindow && w.State != WindowState.Minimized)
			.OrderByDescending(w => w.ZIndex)
			.FirstOrDefault();

		if (nextWindow != null)
		{
			context.SetActiveWindow(nextWindow);
		}
	}

	/// <summary>
	/// Deactivates the current active window (e.g., when clicking on empty desktop).
	/// </summary>
	public void DeactivateCurrentWindow()
	{
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var context = _getWindowSystem();

		if (context.ActiveWindow != null)
		{
			context.ActiveWindow.SetIsActive(false);
			context.ActiveWindow.Invalidate(true);

			// Clear ActiveWindow so clicking the same window again will re-activate it
			var newState = CreateStateSnapshot() with { ActiveWindow = null };
			UpdateStateInternal(newState);
		}
	}

	#endregion

	#endregion

	/// <summary>
	/// Sets the specified window as the active window, handling modal window logic and focus.
	/// </summary>
	/// <param name="window">The window to activate. If null, the method returns without action.</param>
	public void SetActiveWindow(Window window)
	{
		if (window == null)
		{
			return;
		}
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var context = _getWindowSystem();

		// Check if window is registered in the system
		if (!context.Windows.ContainsKey(window.Guid))
		{
			_logService?.LogTrace($"Cannot activate unregistered window: {window.Title}", "WindowState");
			return;
		}

		// Check if activation is blocked by modal service
		if (_modalStateService != null && _modalStateService.IsActivationBlocked(window))
		{
			_logService?.LogTrace($"Window activation blocked by modal: {window.Title}", "Modal");
			var blockingModal = _modalStateService.GetBlockingModal(window);
			if (blockingModal != null && blockingModal != ActiveWindow)
			{
				FlashWindow(blockingModal);
			}
			else if (ActiveWindow != null)
			{
				FlashWindow(ActiveWindow);
			}
			return;
		}

		// Get the effective activation target (handles modal children)
		Window windowToActivate = _modalStateService?.GetEffectiveActivationTarget(window) ?? window;

		// Restore if minimized (typical windowing system behavior)
		if (windowToActivate.State == WindowState.Minimized)
		{
			windowToActivate.State = WindowState.Normal;
		}

		// If a different modal should be activated, flash it
		if (windowToActivate != window && windowToActivate.IsModal)
		{
			FlashWindow(windowToActivate);
		}

		var previousActiveWindow = ActiveWindow;

		// Invalidate previous active window
		previousActiveWindow?.Invalidate(true);

		// Delegate activation to the service
		// The service handles: SetIsActive, ZIndex update, and state tracking
		ActivateWindow(windowToActivate);

		// Update focus state via FocusStateService
		_focusStateService?.SetWindowFocus(windowToActivate);

		// Invalidate new active window
		windowToActivate.Invalidate(true);

		// Unfocus the currently focused control of other windows
		foreach (var w in context.Windows.Values)
		{
			if (w != ActiveWindow)
			{
				w.UnfocusCurrentControl();
				_focusStateService?.ClearFocus(w);
			}
		}

		_logService?.LogTrace($"Window activated: {windowToActivate.Title}", "Window");
	}

	/// <summary>
	/// Activates an existing window by name, or creates it using the factory if not found.
	/// </summary>
	/// <param name="name">The window name to find/create</param>
	/// <param name="factory">Factory function to create the window if it doesn't exist</param>
	/// <returns>The activated or newly created window</returns>
	public Window ActivateOrCreate(string name, Func<Window> factory)
	{
		if (string.IsNullOrEmpty(name))
			throw new ArgumentException("Window name cannot be null or empty", nameof(name));
		if (factory == null)
			throw new ArgumentNullException(nameof(factory));
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var existing = FindWindowByName(name);
		if (existing != null)
		{
			_getWindowSystem().SetActiveWindow(existing);
			return existing;
		}

		var window = factory();
		window.Name = name;
		_getWindowSystem().AddWindow(window);
		return window;
	}

	/// <summary>
	/// Activates an existing window by GUID, or creates it using the factory if not found.
	/// </summary>
	/// <param name="guid">The window GUID to find/create</param>
	/// <param name="factory">Factory function to create the window if it doesn't exist</param>
	/// <returns>The activated or newly created window</returns>
	public Window ActivateOrCreateByGuid(string guid, Func<Window> factory)
	{
		if (string.IsNullOrEmpty(guid))
			throw new ArgumentException("Window GUID cannot be null or empty", nameof(guid));
		if (factory == null)
			throw new ArgumentNullException(nameof(factory));
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var existing = GetWindow(guid);
		if (existing != null)
		{
			_getWindowSystem().SetActiveWindow(existing);
			return existing;
		}

		var window = factory();
		_getWindowSystem().AddWindow(window);
		return window;
	}

	/// <summary>
	/// Activates an existing window by name if found, otherwise returns null.
	/// </summary>
	/// <param name="name">The window name to find</param>
	/// <returns>The activated window, or null if not found</returns>
	public Window? TryActivate(string name)
	{
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var existing = FindWindowByName(name);
		if (existing != null)
		{
			_getWindowSystem().SetActiveWindow(existing);
		}
		return existing;
	}

	/// <summary>
	/// Activates an existing window by GUID if found, otherwise returns null.
	/// </summary>
	/// <param name="guid">The window GUID to find</param>
	/// <returns>The activated window, or null if not found</returns>
	public Window? TryActivateByGuid(string guid)
	{
		if (_getWindowSystem == null)
			throw new InvalidOperationException("WindowSystemContext is not set");

		var existing = GetWindow(guid);
		if (existing != null)
		{
			_getWindowSystem().SetActiveWindow(existing);
		}
		return existing;
	}

	/// <summary>
	/// Cycles to the next active window (Ctrl+T handler).
	/// </summary>
	public void CycleActiveWindow()
	{
		// Delegate to service - it handles window cycling and restoring minimized windows
		ActivateNextWindow();
	}


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

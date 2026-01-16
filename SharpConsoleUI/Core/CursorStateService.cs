// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Centralized service for managing cursor state across the window system.
	/// Provides a single source of truth for cursor visibility, position, and ownership.
	/// </summary>
	public class CursorStateService : IDisposable
	{
		private readonly IConsoleDriver _driver;
		private readonly object _lock = new();
		private CursorState _currentState = CursorState.Hidden;
		private readonly ConcurrentQueue<CursorState> _stateHistory = new();
		private const int MaxHistorySize = 100;
		private bool _isDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="CursorStateService"/> class.
		/// </summary>
		/// <param name="driver">The console driver to use for cursor operations.</param>
		public CursorStateService(IConsoleDriver driver)
		{
			_driver = driver ?? throw new ArgumentNullException(nameof(driver));
		}

		/// <summary>
		/// Gets the current cursor state
		/// </summary>
		public CursorState CurrentState
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
		/// Event fired when any aspect of cursor state changes
		/// </summary>
		public event EventHandler<CursorStateChangedEventArgs>? StateChanged;

		/// <summary>
		/// Event fired specifically when cursor visibility changes
		/// </summary>
		public event EventHandler? VisibilityChanged;

		/// <summary>
		/// Event fired specifically when cursor position changes
		/// </summary>
		public event EventHandler? PositionChanged;

		/// <summary>
		/// Event fired specifically when cursor owner (control/window) changes
		/// </summary>
		public event EventHandler? OwnerChanged;

		/// <summary>
		/// Updates cursor state from the window system.
		/// This is the primary method for updating cursor state based on focused controls.
		/// </summary>
		/// <param name="ownerWindow">The window containing the cursor (if any)</param>
		/// <param name="logicalPosition">The logical position within the owner control</param>
		/// <param name="absolutePosition">The absolute screen position</param>
		/// <param name="ownerControl">The control that owns the cursor (if any)</param>
		/// <param name="shape">The cursor shape to use</param>
		public void UpdateFromWindowSystem(
			Window? ownerWindow,
			Point? logicalPosition,
			Point? absolutePosition,
			IWindowControl? ownerControl,
			CursorShape shape = CursorShape.Block)
		{
			bool isVisible = absolutePosition.HasValue && ownerWindow != null;

			var newState = new CursorState(
				isVisible: isVisible,
				absolutePosition: absolutePosition,
				logicalPosition: logicalPosition,
				ownerControl: ownerControl,
				ownerWindow: ownerWindow,
				shape: shape);

			UpdateState(newState);
		}

		/// <summary>
		/// Sets cursor visibility without changing other properties
		/// </summary>
		public void SetVisible(bool visible)
		{
			lock (_lock)
			{
				if (_currentState.IsVisible == visible)
					return;

				var newState = _currentState with
				{
					IsVisible = visible,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(newState);
			}
		}

		/// <summary>
		/// Sets cursor shape without changing other properties
		/// </summary>
		public void SetShape(CursorShape shape)
		{
			lock (_lock)
			{
				if (_currentState.Shape == shape)
					return;

				var newState = _currentState with
				{
					Shape = shape,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(newState);
			}
		}

		/// <summary>
		/// Hides the cursor and clears ownership
		/// </summary>
		public void HideCursor()
		{
			UpdateState(CursorState.Hidden);
		}

		/// <summary>
		/// Gets recent cursor state history for debugging
		/// </summary>
		public IReadOnlyList<CursorState> GetHistory()
		{
			return _stateHistory.ToArray();
		}

		/// <summary>
		/// Clears the state history
		/// </summary>
		public void ClearHistory()
		{
			while (_stateHistory.TryDequeue(out _)) { }
		}

		private void UpdateState(CursorState newState)
		{
			lock (_lock)
			{
				UpdateStateInternal(newState);
			}
		}

		private void UpdateStateInternal(CursorState newState)
		{
			var previousState = _currentState;

			// Skip if state hasn't changed (using record equality)
			if (previousState == newState)
				return;

			_currentState = newState;

			// Add to history
			_stateHistory.Enqueue(newState);
			while (_stateHistory.Count > MaxHistorySize)
			{
				_stateHistory.TryDequeue(out _);
			}

			// Fire events outside of lock to prevent deadlocks
			var args = new CursorStateChangedEventArgs(previousState, newState);

			// Queue event firing to avoid holding lock during event handlers
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					StateChanged?.Invoke(this, args);

					if (args.VisibilityChanged)
						VisibilityChanged?.Invoke(this, EventArgs.Empty);

					if (args.PositionChanged)
						PositionChanged?.Invoke(this, EventArgs.Empty);

					if (args.OwnerChanged)
						OwnerChanged?.Invoke(this, EventArgs.Empty);
				}
				catch
				{
					// Swallow exceptions from event handlers to prevent crashes
				}
			});
		}

		/// <summary>
		/// Gets a debug string representation of current cursor state
		/// </summary>
		public string GetDebugInfo()
		{
			var state = CurrentState;
			return $"Cursor: Visible={state.IsVisible}, Pos={state.AbsolutePosition}, " +
				   $"Shape={state.Shape}, Owner={state.OwnerControl?.GetType().Name ?? "none"}, " +
				   $"Window={state.OwnerWindow?.Title ?? "none"}";
		}

		/// <summary>
		/// Applies the current cursor state to the console.
		/// This method should be called after updating state to reflect changes in the actual console.
		/// </summary>
		/// <param name="screenWidth">The screen width for bounds checking</param>
		/// <param name="screenHeight">The screen height for bounds checking</param>
		/// <returns>True if cursor was applied successfully</returns>
		public bool ApplyCursorToConsole(int screenWidth, int screenHeight)
		{
			var state = CurrentState;

			if (!state.IsVisible || state.Shape == CursorShape.Hidden)
			{
				_driver.SetCursorVisible(false);
				return true;
			}

			var pos = state.AbsolutePosition;

			// Bounds check
			if (pos.X < 0 || pos.X >= screenWidth || pos.Y < 0 || pos.Y >= screenHeight)
			{
				_driver.SetCursorVisible(false);
				return false;
			}

			// Apply cursor shape, position, and make visible
			_driver.SetCursorShape(state.Shape);
			_driver.SetCursorPosition(pos.X, pos.Y);
			_driver.SetCursorVisible(true);

			return true;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			if (_isDisposed)
				return;

			_isDisposed = true;
			ClearHistory();

			// Clear event handlers
			StateChanged = null;
			VisibilityChanged = null;
			PositionChanged = null;
			OwnerChanged = null;
		}
	}
}

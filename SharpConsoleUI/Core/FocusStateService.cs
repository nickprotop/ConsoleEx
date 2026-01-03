// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Centralized service for managing focus state across the window system.
	/// Provides a single source of truth for which window and control has focus.
	/// </summary>
	public class FocusStateService : IDisposable
	{
		private readonly object _lock = new();
		private readonly ILogService? _logService;
		private FocusState _currentState = FocusState.Empty;
		private readonly ConcurrentQueue<FocusState> _stateHistory = new();
		private readonly Stack<(Window Window, IInteractiveControl? Control)> _focusStack = new();
		private const int MaxHistorySize = 100;
		private bool _isDisposed;

		public FocusStateService(ILogService? logService = null)
		{
			_logService = logService;
		}

		#region Properties

		/// <summary>
		/// Gets the current focus state
		/// </summary>
		public FocusState CurrentState
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
		/// Gets the currently focused window
		/// </summary>
		public Window? FocusedWindow => CurrentState.FocusedWindow;

		/// <summary>
		/// Gets the currently focused control
		/// </summary>
		public IInteractiveControl? FocusedControl => CurrentState.FocusedControl;

		/// <summary>
		/// Gets whether any control has focus
		/// </summary>
		public bool HasFocus => CurrentState.FocusedControl != null;

		#endregion

		#region Events

		/// <summary>
		/// Event fired when any aspect of focus state changes
		/// </summary>
		public event EventHandler<FocusStateChangedEventArgs>? StateChanged;

		/// <summary>
		/// Event fired when a control gains focus
		/// </summary>
		public event EventHandler<ControlFocusEventArgs>? ControlFocused;

		/// <summary>
		/// Event fired when a control loses focus
		/// </summary>
		public event EventHandler<ControlFocusEventArgs>? ControlBlurred;

		/// <summary>
		/// Event fired when the focused window changes
		/// </summary>
		public event EventHandler<FocusStateChangedEventArgs>? WindowFocusChanged;

		#endregion

		#region Focus Management

		/// <summary>
		/// Sets focus to a specific control within a window
		/// </summary>
		/// <param name="window">The window containing the control</param>
		/// <param name="control">The control to focus (null to clear control focus)</param>
		/// <param name="reason">The reason for the focus change</param>
		public void SetFocus(Window window, IInteractiveControl? control, FocusChangeReason reason = FocusChangeReason.Programmatic)
		{
			if (window == null)
				throw new ArgumentNullException(nameof(window));

			lock (_lock)
			{
				var previousState = _currentState;
				var previousControl = previousState.FocusedControl;

				_logService?.LogTrace($"Focus change: {previousControl?.GetType().Name ?? "None"} -> {control?.GetType().Name ?? "None"} ({reason})", "Focus");

				// Update the actual control's HasFocus property
				if (previousControl != null && previousControl != control)
				{
					previousControl.HasFocus = false;
				}

				if (control != null)
				{
					control.HasFocus = true;
				}

				// Create new state
				var newState = new FocusState(
					focusedWindow: window,
					focusedControl: control,
					changeReason: reason);

				UpdateStateInternal(newState);

				// Fire events
				if (previousControl != null && previousControl != control)
				{
					FireControlBlurred(previousControl, previousState.FocusedWindow!, reason);
				}

				if (control != null && control != previousControl)
				{
					FireControlFocused(control, window, reason);
				}

				if (previousState.FocusedWindow != window)
				{
					FireWindowFocusChanged(previousState, newState);
				}
			}
		}

		/// <summary>
		/// Sets focus to a window without specifying a control
		/// </summary>
		/// <param name="window">The window to focus</param>
		/// <param name="reason">The reason for the focus change</param>
		public void SetWindowFocus(Window window, FocusChangeReason reason = FocusChangeReason.Programmatic)
		{
			if (window == null)
				throw new ArgumentNullException(nameof(window));

			lock (_lock)
			{
				var previousState = _currentState;

				// If focusing the same window, keep the current control focus
				if (previousState.FocusedWindow == window)
					return;

				_logService?.LogTrace($"Window focus change: {previousState.FocusedWindow?.Title ?? "None"} -> {window.Title} ({reason})", "Focus");

				// Clear focus from previous control
				if (previousState.FocusedControl != null)
				{
					previousState.FocusedControl.HasFocus = false;
					FireControlBlurred(previousState.FocusedControl, previousState.FocusedWindow!, reason);
				}

				// Create new state (no control focused yet)
				var newState = new FocusState(
					focusedWindow: window,
					focusedControl: null,
					changeReason: reason);

				UpdateStateInternal(newState);
				FireWindowFocusChanged(previousState, newState);
			}
		}

		/// <summary>
		/// Clears all focus (no window or control has focus)
		/// </summary>
		/// <param name="reason">The reason for clearing focus</param>
		public void ClearFocus(FocusChangeReason reason = FocusChangeReason.Programmatic)
		{
			lock (_lock)
			{
				var previousState = _currentState;

				if (previousState.FocusedControl != null)
				{
					previousState.FocusedControl.HasFocus = false;
					FireControlBlurred(previousState.FocusedControl, previousState.FocusedWindow!, reason);
				}

				var newState = FocusState.Empty with { ChangeReason = reason };
				UpdateStateInternal(newState);

				if (previousState.FocusedWindow != null)
				{
					FireWindowFocusChanged(previousState, newState);
				}
			}
		}

		/// <summary>
		/// Clears focus for a specific window (only if that window currently has focus)
		/// </summary>
		/// <param name="window">The window to clear focus for</param>
		/// <param name="reason">The reason for clearing focus</param>
		public void ClearFocus(Window window, FocusChangeReason reason = FocusChangeReason.Programmatic)
		{
			if (window == null)
				return;

			lock (_lock)
			{
				// Only clear if this window has focus
				if (_currentState.FocusedWindow != window)
					return;

				var previousState = _currentState;

				if (previousState.FocusedControl != null)
				{
					previousState.FocusedControl.HasFocus = false;
					FireControlBlurred(previousState.FocusedControl, previousState.FocusedWindow!, reason);
				}

				var newState = FocusState.Empty with { ChangeReason = reason };
				UpdateStateInternal(newState);
				FireWindowFocusChanged(previousState, newState);
			}
		}

		/// <summary>
		/// Clears control focus within the current window but keeps window focus
		/// </summary>
		/// <param name="reason">The reason for clearing control focus</param>
		public void ClearControlFocus(FocusChangeReason reason = FocusChangeReason.Programmatic)
		{
			lock (_lock)
			{
				var previousState = _currentState;

				if (previousState.FocusedControl == null)
					return;

				previousState.FocusedControl.HasFocus = false;
				FireControlBlurred(previousState.FocusedControl, previousState.FocusedWindow!, reason);

				var newState = new FocusState(
					focusedWindow: previousState.FocusedWindow,
					focusedControl: null,
					changeReason: reason);

				UpdateStateInternal(newState);
			}
		}

		/// <summary>
		/// Checks if a specific control currently has focus
		/// </summary>
		public bool HasControlFocus(IInteractiveControl control)
		{
			return CurrentState.FocusedControl == control;
		}

		/// <summary>
		/// Checks if a specific window currently has focus
		/// </summary>
		public bool HasWindowFocus(Window window)
		{
			return CurrentState.FocusedWindow == window;
		}

		#endregion

		#region Focus Stack (for modal dialogs, etc.)

		/// <summary>
		/// Pushes the current focus state onto the stack and sets new focus
		/// </summary>
		public void PushFocus(Window window, IInteractiveControl? control, FocusChangeReason reason = FocusChangeReason.Programmatic)
		{
			lock (_lock)
			{
				// Save current focus
				_focusStack.Push((_currentState.FocusedWindow!, _currentState.FocusedControl));

				// Set new focus
				SetFocus(window, control, reason);
			}
		}

		/// <summary>
		/// Pops focus from the stack and restores it
		/// </summary>
		public void PopFocus(FocusChangeReason reason = FocusChangeReason.Programmatic)
		{
			lock (_lock)
			{
				if (_focusStack.Count == 0)
					return;

				var (window, control) = _focusStack.Pop();

				if (window != null)
				{
					SetFocus(window, control, reason);
				}
				else
				{
					ClearFocus(reason);
				}
			}
		}

		/// <summary>
		/// Gets the depth of the focus stack
		/// </summary>
		public int FocusStackDepth
		{
			get
			{
				lock (_lock)
				{
					return _focusStack.Count;
				}
			}
		}

		#endregion

		#region Debugging

		/// <summary>
		/// Gets recent focus state history for debugging
		/// </summary>
		public IReadOnlyList<FocusState> GetHistory()
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

		/// <summary>
		/// Gets a debug string representation of current focus state
		/// </summary>
		public string GetDebugInfo()
		{
			var state = CurrentState;
			return $"Focus: Window={state.FocusedWindow?.Title ?? "none"}, " +
			       $"Control={state.FocusedControl?.GetType().Name ?? "none"}, " +
			       $"Reason={state.ChangeReason}, " +
			       $"StackDepth={FocusStackDepth}";
		}

		#endregion

		#region Private Helpers

		private void UpdateStateInternal(FocusState newState)
		{
			var previousState = _currentState;

			// Skip if state hasn't changed
			if (!newState.HasChanged(previousState) &&
			    newState.ChangeReason == previousState.ChangeReason)
				return;

			_currentState = newState;

			// Add to history
			_stateHistory.Enqueue(newState);
			while (_stateHistory.Count > MaxHistorySize)
			{
				_stateHistory.TryDequeue(out _);
			}

			// Fire state changed event
			var args = new FocusStateChangedEventArgs(previousState, newState);
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

		private void FireControlFocused(IInteractiveControl control, Window window, FocusChangeReason reason)
		{
			var args = new ControlFocusEventArgs(control, window, reason);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					ControlFocused?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private void FireControlBlurred(IInteractiveControl control, Window window, FocusChangeReason reason)
		{
			var args = new ControlFocusEventArgs(control, window, reason);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					ControlBlurred?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private void FireWindowFocusChanged(FocusState previousState, FocusState newState)
		{
			var args = new FocusStateChangedEventArgs(previousState, newState);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					WindowFocusChanged?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		#endregion

		#region IDisposable

		public void Dispose()
		{
			if (_isDisposed)
				return;

			_isDisposed = true;
			ClearHistory();
			_focusStack.Clear();

			// Clear event handlers
			StateChanged = null;
			ControlFocused = null;
			ControlBlurred = null;
			WindowFocusChanged = null;
		}

		#endregion
	}
}

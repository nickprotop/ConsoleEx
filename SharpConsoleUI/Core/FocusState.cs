// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Reason for a focus change
	/// </summary>
	public enum FocusChangeReason
	{
		/// <summary>Focus changed programmatically</summary>
		Programmatic,
		/// <summary>Focus changed via keyboard (Tab/Shift+Tab)</summary>
		Keyboard,
		/// <summary>Focus changed via mouse click</summary>
		Mouse,
		/// <summary>Focus changed due to window activation</summary>
		WindowActivation,
		/// <summary>Focus changed due to control being removed</summary>
		ControlRemoved
	}

	/// <summary>
	/// Immutable record representing the current focus state.
	/// Provides a single source of truth for focus across the window system.
	/// </summary>
	public record FocusState
	{
		/// <summary>
		/// The currently focused window (receives keyboard input)
		/// </summary>
		public Window? FocusedWindow { get; init; }

		/// <summary>
		/// The currently focused control within the focused window
		/// </summary>
		public IInteractiveControl? FocusedControl { get; init; }

		/// <summary>
		/// The reason for the most recent focus change
		/// </summary>
		public FocusChangeReason ChangeReason { get; init; }

		/// <summary>
		/// Timestamp when this state was created
		/// </summary>
		public DateTime UpdateTime { get; init; }

		/// <summary>
		/// Creates a new focus state
		/// </summary>
		public FocusState(
			Window? focusedWindow = null,
			IInteractiveControl? focusedControl = null,
			FocusChangeReason changeReason = FocusChangeReason.Programmatic)
		{
			FocusedWindow = focusedWindow;
			FocusedControl = focusedControl;
			ChangeReason = changeReason;
			UpdateTime = DateTime.UtcNow;
		}

		/// <summary>
		/// An empty focus state with nothing focused
		/// </summary>
		public static readonly FocusState Empty = new();

		/// <summary>
		/// Returns true if the focused window has changed compared to another state
		/// </summary>
		public bool HasWindowChanged(FocusState other)
		{
			return FocusedWindow != other.FocusedWindow;
		}

		/// <summary>
		/// Returns true if the focused control has changed compared to another state
		/// </summary>
		public bool HasControlChanged(FocusState other)
		{
			return FocusedControl != other.FocusedControl;
		}

		/// <summary>
		/// Returns true if any focus state has changed compared to another state
		/// </summary>
		public bool HasChanged(FocusState other)
		{
			return HasWindowChanged(other) || HasControlChanged(other);
		}
	}

	/// <summary>
	/// Event arguments for focus state changes
	/// </summary>
	public class FocusStateChangedEventArgs : EventArgs
	{
		/// <summary>
		/// The previous focus state
		/// </summary>
		public FocusState PreviousState { get; }

		/// <summary>
		/// The new focus state
		/// </summary>
		public FocusState NewState { get; }

		/// <summary>
		/// Whether the focused window changed
		/// </summary>
		public bool WindowChanged => NewState.HasWindowChanged(PreviousState);

		/// <summary>
		/// Whether the focused control changed
		/// </summary>
		public bool ControlChanged => NewState.HasControlChanged(PreviousState);

		/// <summary>
		/// The control that lost focus (if any)
		/// </summary>
		public IInteractiveControl? LostFocusControl => PreviousState.FocusedControl;

		/// <summary>
		/// The control that gained focus (if any)
		/// </summary>
		public IInteractiveControl? GainedFocusControl => NewState.FocusedControl;

		public FocusStateChangedEventArgs(FocusState previousState, FocusState newState)
		{
			PreviousState = previousState;
			NewState = newState;
		}
	}

	/// <summary>
	/// Event arguments for control focus events
	/// </summary>
	public class ControlFocusEventArgs : EventArgs
	{
		/// <summary>
		/// The control involved in the focus change
		/// </summary>
		public IInteractiveControl Control { get; }

		/// <summary>
		/// The window containing the control
		/// </summary>
		public Window Window { get; }

		/// <summary>
		/// The reason for the focus change
		/// </summary>
		public FocusChangeReason Reason { get; }

		public ControlFocusEventArgs(IInteractiveControl control, Window window, FocusChangeReason reason)
		{
			Control = control;
			Window = window;
			Reason = reason;
		}
	}
}

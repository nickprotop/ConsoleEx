// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Types of window events
	/// </summary>
	public enum WindowEventType
	{
		/// <summary>Window was created and registered</summary>
		Created,
		/// <summary>Window was closed and unregistered</summary>
		Closed,
		/// <summary>Window became the active window</summary>
		Activated,
		/// <summary>Window lost active status</summary>
		Deactivated,
		/// <summary>Window was minimized</summary>
		Minimized,
		/// <summary>Window was maximized</summary>
		Maximized,
		/// <summary>Window was restored from minimized/maximized</summary>
		Restored,
		/// <summary>Window position changed</summary>
		Moved,
		/// <summary>Window size changed</summary>
		Resized,
		/// <summary>Window Z-order changed</summary>
		ZOrderChanged
	}

	/// <summary>
	/// Event arguments for window system state changes.
	/// Provides both previous and new state for comparison.
	/// </summary>
	public class WindowSystemStateChangedEventArgs : EventArgs
	{
		/// <summary>
		/// The state before the change
		/// </summary>
		public WindowSystemState PreviousState { get; }

		/// <summary>
		/// The state after the change
		/// </summary>
		public WindowSystemState NewState { get; }

		/// <summary>
		/// Whether the active window changed
		/// </summary>
		public bool ActiveWindowChanged => NewState.HasActiveWindowChanged(PreviousState);

		/// <summary>
		/// Whether the interaction state (drag/resize) changed
		/// </summary>
		public bool InteractionChanged => NewState.HasInteractionChanged(PreviousState);

		/// <summary>
		/// Whether the window collection changed
		/// </summary>
		public bool WindowsChanged => NewState.HasWindowsChanged(PreviousState);

		public WindowSystemStateChangedEventArgs(WindowSystemState previousState, WindowSystemState newState)
		{
			PreviousState = previousState;
			NewState = newState;
		}
	}

	/// <summary>
	/// Event arguments for individual window events
	/// </summary>
	public class WindowEventArgs : EventArgs
	{
		/// <summary>
		/// The window involved in the event
		/// </summary>
		public Window Window { get; }

		/// <summary>
		/// The type of event that occurred
		/// </summary>
		public WindowEventType EventType { get; }

		/// <summary>
		/// Optional additional data for the event
		/// </summary>
		public object? Data { get; }

		public WindowEventArgs(Window window, WindowEventType eventType, object? data = null)
		{
			Window = window;
			EventType = eventType;
			Data = data;
		}
	}

	/// <summary>
	/// Event arguments for window activation changes
	/// </summary>
	public class WindowActivatedEventArgs : EventArgs
	{
		/// <summary>
		/// The window that was deactivated (may be null)
		/// </summary>
		public Window? PreviousWindow { get; }

		/// <summary>
		/// The window that is now active (may be null if no window is active)
		/// </summary>
		public Window? NewWindow { get; }

		public WindowActivatedEventArgs(Window? previousWindow, Window? newWindow)
		{
			PreviousWindow = previousWindow;
			NewWindow = newWindow;
		}
	}

	/// <summary>
	/// Event arguments for interaction (drag/resize) state changes
	/// </summary>
	public class InteractionStateChangedEventArgs : EventArgs
	{
		/// <summary>
		/// The previous interaction state
		/// </summary>
		public InteractionState PreviousState { get; }

		/// <summary>
		/// The new interaction state
		/// </summary>
		public InteractionState NewState { get; }

		/// <summary>
		/// Whether a drag operation started
		/// </summary>
		public bool DragStarted => !PreviousState.IsDragging && NewState.IsDragging;

		/// <summary>
		/// Whether a drag operation ended
		/// </summary>
		public bool DragEnded => PreviousState.IsDragging && !NewState.IsDragging;

		/// <summary>
		/// Whether a resize operation started
		/// </summary>
		public bool ResizeStarted => !PreviousState.IsResizing && NewState.IsResizing;

		/// <summary>
		/// Whether a resize operation ended
		/// </summary>
		public bool ResizeEnded => PreviousState.IsResizing && !NewState.IsResizing;

		public InteractionStateChangedEventArgs(InteractionState previousState, InteractionState newState)
		{
			PreviousState = previousState;
			NewState = newState;
		}
	}

	/// <summary>
	/// Event arguments for window state (minimize/maximize/restore) changes
	/// </summary>
	public class WindowStateEventArgs : EventArgs
	{
		/// <summary>
		/// The window whose state changed
		/// </summary>
		public Window Window { get; }

		/// <summary>
		/// The previous window state
		/// </summary>
		public WindowState PreviousState { get; }

		/// <summary>
		/// The new window state
		/// </summary>
		public WindowState NewState { get; }

		public WindowStateEventArgs(Window window, WindowState previousState, WindowState newState)
		{
			Window = window;
			PreviousState = previousState;
			NewState = newState;
		}
	}
}

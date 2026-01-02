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
	/// Immutable record representing the current modal state.
	/// Tracks modal window stack and parent-child relationships.
	/// </summary>
	public record ModalState
	{
		/// <summary>
		/// The stack of modal windows, with the topmost modal last
		/// </summary>
		public IReadOnlyList<Window> ModalStack { get; init; } = Array.Empty<Window>();

		/// <summary>
		/// Map of modal windows to their parent windows
		/// </summary>
		public IReadOnlyDictionary<Window, Window> ModalParents { get; init; } = new Dictionary<Window, Window>();

		/// <summary>
		/// Timestamp when this state was created
		/// </summary>
		public DateTime UpdateTime { get; init; }

		/// <summary>
		/// Gets the number of modal windows currently open
		/// </summary>
		public int ModalCount => ModalStack.Count;

		/// <summary>
		/// Gets whether any modal windows are open
		/// </summary>
		public bool HasModals => ModalStack.Count > 0;

		/// <summary>
		/// Gets the topmost modal window (if any)
		/// </summary>
		public Window? TopmostModal => ModalStack.Count > 0 ? ModalStack[^1] : null;

		/// <summary>
		/// Creates a new modal state
		/// </summary>
		public ModalState()
		{
			UpdateTime = DateTime.UtcNow;
		}

		/// <summary>
		/// Empty modal state with no modals
		/// </summary>
		public static readonly ModalState Empty = new();
	}

	/// <summary>
	/// Event arguments for modal state changes
	/// </summary>
	public class ModalStateChangedEventArgs : EventArgs
	{
		/// <summary>
		/// The previous modal state
		/// </summary>
		public ModalState PreviousState { get; }

		/// <summary>
		/// The new modal state
		/// </summary>
		public ModalState NewState { get; }

		/// <summary>
		/// The modal window that was opened (if this is an open event)
		/// </summary>
		public Window? OpenedModal { get; }

		/// <summary>
		/// The modal window that was closed (if this is a close event)
		/// </summary>
		public Window? ClosedModal { get; }

		public ModalStateChangedEventArgs(ModalState previousState, ModalState newState, Window? openedModal = null, Window? closedModal = null)
		{
			PreviousState = previousState;
			NewState = newState;
			OpenedModal = openedModal;
			ClosedModal = closedModal;
		}
	}

	/// <summary>
	/// Event arguments for activation blocked events
	/// </summary>
	public class ActivationBlockedEventArgs : EventArgs
	{
		/// <summary>
		/// The window that tried to activate
		/// </summary>
		public Window TargetWindow { get; }

		/// <summary>
		/// The modal window that blocked the activation
		/// </summary>
		public Window BlockingModal { get; }

		public ActivationBlockedEventArgs(Window targetWindow, Window blockingModal)
		{
			TargetWindow = targetWindow;
			BlockingModal = blockingModal;
		}
	}
}

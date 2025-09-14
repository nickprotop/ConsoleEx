// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Interface for controls that can handle mouse events
	/// </summary>
	public interface IMouseAwareControl : IWindowControl
	{
		/// <summary>
		/// Processes a mouse event for this control
		/// </summary>
		/// <param name="args">Mouse event arguments with control-relative coordinates</param>
		/// <returns>True if the event was handled and should not propagate further</returns>
		bool ProcessMouseEvent(MouseEventArgs args);

		/// <summary>
		/// Whether this control wants to receive mouse events
		/// </summary>
		bool WantsMouseEvents { get; }

		/// <summary>
		/// Whether this control can receive focus via mouse clicks
		/// </summary>
		bool CanFocusWithMouse { get; }

		/// <summary>
		/// Event fired when the control is clicked
		/// </summary>
		event EventHandler<MouseEventArgs>? MouseClick;

		/// <summary>
		/// Event fired when the mouse enters the control area
		/// </summary>
		event EventHandler<MouseEventArgs>? MouseEnter;

		/// <summary>
		/// Event fired when the mouse leaves the control area
		/// </summary>
		event EventHandler<MouseEventArgs>? MouseLeave;

		/// <summary>
		/// Event fired when the mouse moves over the control
		/// </summary>
		event EventHandler<MouseEventArgs>? MouseMove;
	}

	/// <summary>
	/// Interface for controls that can receive focus
	/// </summary>
	public interface IFocusableControl : IWindowControl
	{
		/// <summary>
		/// Whether this control currently has focus
		/// </summary>
		bool HasFocus { get; set; }

		/// <summary>
		/// Whether this control can receive focus
		/// </summary>
		bool CanReceiveFocus { get; }

		/// <summary>
		/// Event fired when the control gains focus
		/// </summary>
		event EventHandler? GotFocus;

		/// <summary>
		/// Event fired when the control loses focus
		/// </summary>
		event EventHandler? LostFocus;

		/// <summary>
		/// Sets focus to this control
		/// </summary>
		/// <param name="focus">Whether to give or remove focus</param>
		/// <param name="reason">The reason for the focus change</param>
		void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic);
	}

	/// <summary>
	/// Reasons for focus changes
	/// </summary>
	public enum FocusReason
	{
		/// <summary>
		/// Focus changed programmatically
		/// </summary>
		Programmatic,

		/// <summary>
		/// Focus changed due to mouse click
		/// </summary>
		Mouse,

		/// <summary>
		/// Focus changed due to keyboard navigation (Tab)
		/// </summary>
		Keyboard,

		/// <summary>
		/// Focus changed due to window activation
		/// </summary>
		WindowActivation
	}
}
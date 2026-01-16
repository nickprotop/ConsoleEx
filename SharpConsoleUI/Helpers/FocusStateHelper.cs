using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;

namespace SharpConsoleUI.Helpers;

/// <summary>
/// Helper class for managing focus state updates across controls.
/// Eliminates 100% identical SetFocus boilerplate from 8+ simple controls.
/// </summary>
public static class FocusStateHelper
{
	/// <summary>
	/// Handles focus state changes for controls with standard focus behavior.
	/// Use this for simple controls (Button, Checkbox, etc.) that don't need custom focus logic.
	/// DO NOT use for complex controls (TreeControl, ListControl) that have custom focus handling.
	/// </summary>
	/// <param name="control">The control receiving the focus change</param>
	/// <param name="hasFocusField">Reference to the control's _hasFocus field</param>
	/// <param name="focus">True to give focus, false to remove focus</param>
	/// <param name="reason">Reason for the focus change</param>
	/// <param name="gotFocusEvent">The control's GotFocus event to invoke</param>
	/// <param name="lostFocusEvent">The control's LostFocus event to invoke</param>
	/// <param name="invalidate">Whether to invalidate the container after focus change</param>
	public static void HandleSetFocus(
		IWindowControl control,
		ref bool hasFocusField,
		bool focus,
		FocusReason reason,
		EventHandler? gotFocusEvent,
		EventHandler? lostFocusEvent,
		bool invalidate = true)
	{
		var hadFocus = hasFocusField;

		hasFocusField = focus;

		if (hadFocus && !focus)
		{
			lostFocusEvent?.Invoke(control, EventArgs.Empty);
		}
		else if (!hadFocus && focus)
		{
			gotFocusEvent?.Invoke(control, EventArgs.Empty);
		}

		if (invalidate)
		{
			control.Container?.Invalidate(true);
		}
	}
}

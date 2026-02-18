// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Extensions;

/// <summary>
/// Extension methods for IWindowControl.
/// </summary>
public static class WindowControlExtensions
{
	/// <summary>
	/// Gets the parent Window by walking up the container hierarchy.
	/// Returns null if the control is not attached to a window.
	/// </summary>
	/// <param name="control">The control to find the parent window for.</param>
	/// <returns>The parent Window, or null if not attached to a window.</returns>
	public static Window? GetParentWindow(this IWindowControl control)
	{
		IContainer? container = control.Container;

		while (container != null)
		{
			// If container is a Window, return it
			if (container is Window window)
				return window;

			// If container is a control (e.g., ColumnContainer, ToolbarControl),
			// walk up to its parent
			if (container is IWindowControl parentControl)
				container = parentControl.Container;
			else
				break;
		}

		return null;
	}

	/// <summary>
	/// Notifies intermediate containers and the parent Window that this control's focus has changed.
	/// Walks up the container hierarchy, notifying each IFocusTrackingContainer along the way,
	/// then notifies the Window with the outermost container as the focus target.
	/// </summary>
	/// <param name="control">The control whose focus changed</param>
	/// <param name="hasFocus">Whether the control now has focus</param>
	public static void NotifyParentWindowOfFocusChange(this IFocusableControl control, bool hasFocus)
	{
		if (control is not IInteractiveControl interactiveControl)
			return;

		IContainer? container = (control as IWindowControl)?.Container;
		IInteractiveControl currentNotifyTarget = interactiveControl;
		IInteractiveControl originalControl = interactiveControl;  // leaf; never overwritten

		while (container != null)
		{
			if (container is IFocusTrackingContainer tracker)
			{
				tracker.NotifyChildFocusChanged(currentNotifyTarget, hasFocus);
				if (container is IInteractiveControl containerAsInteractive)
					currentNotifyTarget = containerAsInteractive;
					// originalControl intentionally NOT updated
			}

			if (container is Window window)
			{
				if (hasFocus)
					window.NotifyControlGainedFocus(currentNotifyTarget, originalControl);
				else
					window.NotifyControlLostFocus(currentNotifyTarget);
				break;
			}

			if (container is IWindowControl parentControl)
				container = parentControl.Container;
			else
				break;
		}
	}
}

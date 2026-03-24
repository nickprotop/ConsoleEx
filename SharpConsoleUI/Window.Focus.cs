// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;

namespace SharpConsoleUI
{
	public partial class Window
	{
		/// <summary>
		/// Sets focus to the specified control in this window.
		/// This is the recommended way to programmatically change focus, as it properly
		/// updates Window's internal focus tracking and unfocuses the previously focused control.
		/// </summary>
		/// <param name="control">The control to focus, or null to clear focus entirely.</param>
		public void FocusControl(IInteractiveControl? control)
		{
			FocusManager.SetFocus(control as Controls.IFocusableControl, Controls.FocusReason.Programmatic);
		}

		/// <summary>
		/// Switches focus to the next or previous interactive control in the window.
		/// </summary>
		/// <param name="backward">True to move focus backward; false to move forward.</param>
		public void SwitchFocus(bool backward = false)
		{
			_eventDispatcher?.SwitchFocus(backward);
		}

		/// <summary>
		/// Removes focus from the currently focused control.
		/// </summary>
		public void UnfocusCurrentControl()
		{
			FocusManager.SetFocus(null, Controls.FocusReason.Programmatic);
		}

		/// <summary>
		/// Returns the top-level controls registered in this window.
		/// Used by WindowRootScope to build the flat focusable control list.
		/// </summary>
		internal IEnumerable<IWindowControl> GetTopLevelControls()
			=> _controls ?? Enumerable.Empty<IWindowControl>();
	}
}

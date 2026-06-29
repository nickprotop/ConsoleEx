// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	public partial class HorizontalGridControl
	{
		/// <inheritdoc/>
		/// <remarks>
		/// OVERRIDE (kept in Task 6): HGC delegates a non-Tab key to the focused column child via its own
		/// column-walking <see cref="GetFocusedChildFromCoordinator"/> (the inherited Grid coordinator
		/// cannot see a focused control nested inside a transparent <see cref="ColumnContainer"/> because
		/// FocusManager collapses the column out of the focus path). Tab/Shift+Tab then advance through
		/// HGC's column-ordered focusable list (the overridden <c>GetNextFocus</c>/<c>GetInitialFocus</c>).
		/// </remarks>
		public override bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!IsEnabled) return false;

			var focusedContent = GetFocusedChildFromCoordinator();

			// Delegate to the focused child first (for non-Tab keys it wants).
			if (focusedContent != null && focusedContent.ProcessKey(key))
				return true;

			// Tab / Shift+Tab: advance across columns using HGC's column-ordered focus traversal.
			if (key.Key == ConsoleKey.Tab)
			{
				bool backward = (key.Modifiers & ConsoleModifiers.Shift) != 0;
				var window = (this as IWindowControl).GetParentWindow();
				var focused = window?.FocusManager.FocusedControl;

				var referenceForNext = (focusedContent as IFocusableControl) ?? focused;
				if (referenceForNext != null)
				{
					var next = GetNextFocus(referenceForNext, backward);
					if (next != null)
					{
						IFocusableControl? target = next;
						if (next is IFocusScope innerScope)
							target = innerScope.GetInitialFocus(backward) ?? next;
						window?.FocusManager.SetFocus(target, FocusReason.Keyboard);
						return true;
					}
					return false; // traversal exhausted — let the caller advance to the next sibling
				}

				var initial = GetInitialFocus(backward);
				if (initial != null)
				{
					window?.FocusManager.SetFocus(initial, FocusReason.Keyboard);
					return true;
				}
			}

			return false;
		}
	}
}

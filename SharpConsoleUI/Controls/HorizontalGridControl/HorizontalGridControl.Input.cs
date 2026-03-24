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
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			var focusedContent = GetFocusedChildFromCoordinator();

			// Delegate to focused child first (for non-Tab keys)
			if (focusedContent != null && focusedContent.ProcessKey(key))
				return true;

			// Handle Tab/Shift+Tab via IFocusScope.GetNextFocus so that navigation
			// within the grid works when ProcessKey is called directly (e.g. from SPC).
			if (key.Key == ConsoleKey.Tab)
			{
				bool backward = (key.Modifiers & ConsoleModifiers.Shift) != 0;
				var window = (this as IWindowControl).GetParentWindow();
				var focused = window?.FocusManager.FocusedControl;

				// Use focusedContent (direct child of grid) as the reference for GetNextFocus
				// when a child container has exited (focusedContent returned false from ProcessKey).
				// This ensures we advance to the next column child rather than re-entering the same one.
				// focusedContent is captured BEFORE the child's ProcessKey may clear FocusedControl to null.
				var referenceForNext = (focusedContent as IFocusableControl) ?? focused;

				if (referenceForNext != null)
				{
					var next = GetNextFocus(referenceForNext, backward);
					if (next != null)
					{
						// When entering a scope (e.g. SPC), respect backward direction so that
						// Shift+Tab enters at the last child rather than the first.
						IFocusableControl? target = next;
						if (next is IFocusScope innerScope)
						{
							target = innerScope.GetInitialFocus(backward) ?? next;
						}
						window?.FocusManager.SetFocus(target, FocusReason.Keyboard);
						return true; // Handled within grid
					}
					// next == null: scope exhausted — let caller advance to next sibling
					return false;
				}
				else
				{
					// No focused control and no prior focusedContent: focus first/last child in grid
					var initial = GetInitialFocus(backward);
					if (initial != null)
					{
						window?.FocusManager.SetFocus(initial, FocusReason.Keyboard);
						return true;
					}
				}
			}

			return false;
		}

	}
}

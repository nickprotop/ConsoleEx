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
	public partial class HtmlControl
	{
		/// <summary>
		/// Whether this control wants to consume Tab key events (for link navigation).
		/// </summary>
		public bool WantsTabKey => true;

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !HasFocus)
				return false;

			// Tab / Shift+Tab — navigate between links.
			// At the boundary (last link forward, first link backward), return false
			// to allow focus to leave the control.
			if (key.Key == ConsoleKey.Tab)
			{
				var links = GetAllLinks();
				if (links.Count == 0)
					return false;

				if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
				{
					if (_focusedLinkIndex <= 0)
						return false; // let focus leave
					FocusedLinkIndex = _focusedLinkIndex - 1;
				}
				else
				{
					if (_focusedLinkIndex >= links.Count - 1)
						return false; // let focus leave
					FocusedLinkIndex = _focusedLinkIndex + 1;
				}
				return true;
			}

			// Enter — activate focused link
			if (key.Key == ConsoleKey.Enter && _focusedLinkIndex >= 0)
			{
				var links = GetAllLinks();
				if (_focusedLinkIndex < links.Count)
				{
					var focused = links[_focusedLinkIndex];
					LinkClicked?.Invoke(this, new LinkClickedEventArgs(focused.link.Url, focused.link.Text));
					return true;
				}
			}

			// Don't consume other modifier keys
			if (key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control))
				return false;

			int viewportHeight = GetViewportHeight();
			int totalHeight = _layoutResult.TotalHeight;
			int maxScroll = Math.Max(0, totalHeight - viewportHeight);

			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					if (_scrollOffset > 0)
					{
						ScrollOffset = _scrollOffset - 1;
						return true;
					}
					return false;

				case ConsoleKey.DownArrow:
					if (_scrollOffset < maxScroll)
					{
						ScrollOffset = _scrollOffset + 1;
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					if (_scrollOffset > 0)
					{
						ScrollOffset = _scrollOffset - viewportHeight;
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					if (_scrollOffset < maxScroll)
					{
						ScrollOffset = _scrollOffset + viewportHeight;
						return true;
					}
					return false;

				case ConsoleKey.Home:
					if (_scrollOffset > 0)
					{
						ScrollOffset = 0;
						return true;
					}
					return false;

				case ConsoleKey.End:
					if (_scrollOffset < maxScroll)
					{
						ScrollOffset = maxScroll;
						return true;
					}
					return false;

				default:
					return false;
			}
		}
	}
}

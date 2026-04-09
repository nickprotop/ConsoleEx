// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	public partial class HtmlControl
	{
		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !HasFocus)
				return false;

			// Don't consume modifier keys
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

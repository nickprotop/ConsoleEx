// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Parsing
{
	/// <summary>
	/// Represents a style state entry on the markup parser's style stack.
	/// Captures foreground, background, and decorations set by a single tag.
	/// </summary>
	internal readonly struct MarkupStyle
	{
		public readonly Color? Foreground;
		public readonly Color? Background;
		public readonly TextDecoration AddedDecorations;

		public MarkupStyle(Color? foreground, Color? background, TextDecoration addedDecorations)
		{
			Foreground = foreground;
			Background = background;
			AddedDecorations = addedDecorations;
		}
	}
}

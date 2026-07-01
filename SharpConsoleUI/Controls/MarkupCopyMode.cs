// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Controls what <see cref="MarkupControl"/> copies to the clipboard when a selection is copied.
	/// </summary>
	public enum MarkupCopyMode
	{
		/// <summary>
		/// Copy the visible rendered text (markup/markdown expanded to plain characters). This is the
		/// default and preserves the historical copy behavior.
		/// </summary>
		Rendered,

		/// <summary>
		/// Copy the original source markup the control was given (the raw <c>[markdown]…[/]</c> /
		/// <c>[yellow]…[/]</c> lines), including embedded newlines. Useful for agent/IDE tools that want the
		/// underlying markdown back rather than the rendered glyphs.
		/// </summary>
		Source
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using TextMateSharp.Grammars;

namespace SharpConsoleUI.Highlighting.TextMate
{
	/// <summary>
	/// Entry point for TextMate-backed highlighting. Resolution is on by default through
	/// <see cref="SyntaxHighlighters.For(string)"/>; these members exist for overrides.
	/// </summary>
	public static class TextMateHighlighting
	{
		/// <summary>Resolves a highlighter for one language directly, bypassing the registry.</summary>
		/// <param name="language">Language name or alias (for example "rust").</param>
		/// <returns>The highlighter, or null when no grammar matches.</returns>
		public static ISyntaxHighlighter? For(string? language)
			=> TextMateEngine.Instance.GetHighlighter(language);

		/// <summary>Recolours all languages from an application palette.</summary>
		/// <param name="palette">The role colours to apply.</param>
		/// <param name="codeBackground">Surface code is drawn on, used for the contrast floor.</param>
		public static void UseTheme(SyntaxPalette palette, Color? codeBackground = null)
			=> TextMateEngine.Instance.SetTheme(palette, codeBackground);

		/// <summary>Switches every language to one of TextMateSharp's bundled editor themes.</summary>
		/// <param name="themeName">The bundled theme (for example <see cref="ThemeName.DarkPlus"/>).</param>
		public static void RegisterAll(ThemeName themeName)
			=> TextMateEngine.Instance.SetBundledTheme(themeName);
	}
}

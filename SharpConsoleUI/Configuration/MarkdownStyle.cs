// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Configuration
{
	/// <summary>
	/// Styling for Markdown rendered via the <c>[markdown]</c> markup tag.
	/// Markdown is structural: emphasis and headings emit colorless tags (inheriting the
	/// surrounding scope), so only "chrome" components (code, quotes, links, table borders)
	/// carry colors here. This is intentionally NOT part of the global theme — the parser is
	/// static and theme-agnostic. Override per-build via <c>WithMarkdownStyle</c> or globally
	/// via <see cref="Default"/>.
	/// </summary>
	public record MarkdownStyle
	{
		/// <summary>Foreground color for inline code and code blocks.</summary>
		public Color CodeForeground { get; init; }

		/// <summary>Background color for inline code and code blocks.</summary>
		public Color CodeBackground { get; init; }

		/// <summary>Color for blockquote text and the quote bar glyph.</summary>
		public Color QuoteColor { get; init; }

		/// <summary>Color for link text.</summary>
		public Color LinkColor { get; init; }

		/// <summary>Color for table border (box-drawing) characters.</summary>
		public Color BorderColor { get; init; }

		/// <summary>
		/// Per-style language→highlighter overrides for fenced code blocks. Checked BEFORE the global
		/// <see cref="SharpConsoleUI.Highlighting.SyntaxHighlighters"/> registry. Empty by default
		/// (falls through to the registry, then to a flat shaded block).
		/// </summary>
		public IReadOnlyDictionary<string, ISyntaxHighlighter> CodeHighlighters { get; init; }
			= new Dictionary<string, ISyntaxHighlighter>();

		/// <summary>Glyph used for bullet list items. Default: <c>•</c>.</summary>
		public string BulletGlyph { get; init; } = "•";

		/// <summary>Spaces of indentation per nested list level. Default: 2.</summary>
		public int ListIndent { get; init; } = 2;

		/// <summary>Glyph used for the blockquote vertical bar. Default: <c>│</c>.</summary>
		public string QuoteGlyph { get; init; } = "│";

		/// <summary>Optional color for H1. <c>null</c> = colorless (structural weight only).</summary>
		public Color? H1Color { get; init; }
		/// <summary>Optional color for H2. <c>null</c> = colorless.</summary>
		public Color? H2Color { get; init; }
		/// <summary>Optional color for H3. <c>null</c> = colorless.</summary>
		public Color? H3Color { get; init; }
		/// <summary>Optional color for H4. <c>null</c> = colorless.</summary>
		public Color? H4Color { get; init; }
		/// <summary>Optional color for H5. <c>null</c> = colorless.</summary>
		public Color? H5Color { get; init; }
		/// <summary>Optional color for H6. <c>null</c> = colorless.</summary>
		public Color? H6Color { get; init; }

		/// <summary>
		/// The process-wide default style used by the <c>[markdown]</c> tag when no per-control
		/// override is supplied. Settable to override globally.
		/// </summary>
		public static MarkdownStyle Default { get; set; } = new()
		{
			// A cohesive, restrained palette: one cool blue-grey family anchored on the link
			// blue. Visible hierarchy without competing hues — headings step down in prominence,
			// chrome recedes. Tasteful on both dark and light terminals.
			CodeForeground = new Color(210, 210, 210),
			CodeBackground = new Color(38, 40, 46),   // softly cooler than pure black
			QuoteColor = new Color(150, 160, 175),    // faint cool-grey
			LinkColor = new Color(102, 168, 224),     // the anchor hue
			BorderColor = new Color(96, 104, 120),    // cool-grey, recedes

			// Headings: H1–H3 tinted in the same blue family (prominence descends);
			// H4–H6 stay colorless (bold/dim weight only) to avoid clutter on deep levels.
			H1Color = new Color(114, 180, 255),       // bright-soft blue, still not neon
			H2Color = new Color(122, 162, 214),       // muted blue
			H3Color = new Color(140, 170, 190),       // blue-grey
		};
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;

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

		private static MarkdownStyle _default = BuiltIn();

		/// <summary>
		/// The process-wide default style used by the <c>[markdown]</c> tag when no per-control
		/// override is supplied AND no active theme is reachable. Assigning this marks it as an
		/// explicit app choice (see <see cref="DefaultExplicitlySet"/>), so it then wins over the
		/// theme-derived default.
		/// </summary>
		public static MarkdownStyle Default
		{
			get => _default;
			set { _default = value; DefaultExplicitlySet = true; }
		}

		/// <summary>
		/// True once an app has explicitly assigned <see cref="Default"/>. When false, the
		/// <c>[markdown]</c> tag derives its style from the active theme (<see cref="FromTheme"/>)
		/// rather than the built-in palette, so Markdown follows the theme by default.
		/// </summary>
		internal static bool DefaultExplicitlySet { get; private set; }

		/// <summary>The library's built-in (theme-agnostic) palette — used only as the last-resort
		/// fallback when no theme is reachable and the app hasn't set <see cref="Default"/>.</summary>
		private static MarkdownStyle BuiltIn() => new()
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

		/// <summary>
		/// Derives a Markdown style from the active theme so the <c>[markdown]</c> tag follows the
		/// theme: headings use the theme accent (descending prominence), code/quote/border derive
		/// from the theme surfaces. Non-color settings (glyphs, indent, highlighters) keep the
		/// built-in defaults. Used as the default when no explicit style is set.
		/// </summary>
		/// <param name="theme">The active theme to derive chrome colors from.</param>
		public static MarkdownStyle FromTheme(Themes.ITheme theme)
		{
			// ITheme has no literal "primary"; ActiveBorderForegroundColor is the accent every theme
			// sets and the palette generator drives from its primary seed.
			Color accent = theme.ActiveBorderForegroundColor ?? theme.WindowForegroundColor;
			Color bg = theme.WindowBackgroundColor;
			Color fg = theme.WindowForegroundColor;

			// Code block: a surface stepped away from the window bg, with a readable fg on it.
			Color codeBg = bg.IsDark() ? bg.Tint(0.10) : bg.Shade(0.10);

			return new MarkdownStyle
			{
				CodeBackground = codeBg,
				CodeForeground = Helpers.PaletteColors.ReadableOn(codeBg),
				QuoteColor = fg.Mix(bg, 0.35),   // muted but readable secondary
				LinkColor = accent,
				BorderColor = fg.Mix(bg, 0.5),   // recedes

				H1Color = accent,
				H2Color = accent.Mix(fg, 0.30),
				H3Color = accent.Mix(fg, 0.50),
			};
		}
	}
}

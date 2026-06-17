// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Configuration;

/// <summary>
/// The [markdown] tag follows the active theme by default (MarkdownStyle.FromTheme), so its chrome
/// (headings/code/quote/border) is readable on both light and dark themes instead of the old
/// dark-tuned static palette. An explicitly-set style remains the user's choice.
/// </summary>
public class MarkdownStyleThemeTests
{
	[Fact]
	public void FromTheme_Light_HeadingsAreReadableOnLightSurface()
	{
		ITheme light = Theme.FromPalette(new Palette
		{
			Primary = Color.FromHex("#2563EB"),
			Background = Color.FromHex("#DFE3E9"),
		});

		var style = MarkdownStyle.FromTheme(light);
		var surface = light.WindowBackgroundColor;

		// H1 uses the theme accent; it must contrast against the light window surface.
		Assert.NotNull(style.H1Color);
		double gap = System.Math.Abs(style.H1Color!.Value.Luminance() - surface.Luminance());
		Assert.True(gap >= 40, $"H1 vs light surface contrast too low: {gap:0}");
	}

	[Fact]
	public void FromTheme_UsesThemeAccentForHeadingsAndLinks()
	{
		ITheme theme = new ModernGrayTheme();
		var style = MarkdownStyle.FromTheme(theme);

		// H1 + links derive from the theme accent (ActiveBorderForegroundColor).
		Assert.Equal(theme.ActiveBorderForegroundColor, style.H1Color);
		Assert.Equal(theme.ActiveBorderForegroundColor, style.LinkColor);
	}

	[Fact]
	public void FromTheme_CodeForeground_IsReadableOnCodeBackground()
	{
		var style = MarkdownStyle.FromTheme(new ModernGrayTheme());
		double gap = System.Math.Abs(style.CodeForeground.Luminance() - style.CodeBackground.Luminance());
		Assert.True(gap >= 60, $"code fg/bg contrast too low: {gap:0}");
	}

	[Fact]
	public void FromTheme_KeepsNonColorSettings()
	{
		var style = MarkdownStyle.FromTheme(new ModernGrayTheme());
		// Glyphs/indent come from the built-in defaults (only colors are theme-derived).
		Assert.Equal("•", style.BulletGlyph);
		Assert.Equal("│", style.QuoteGlyph);
	}

	// NOTE: not testing the global MarkdownStyle.Default override flag here — assigning that static
	// would trip DefaultExplicitlySet for the whole test run and defeat the theme-derived default in
	// other tests. The flag's behavior is exercised end-to-end via the MarkupControl resolution path.

	[Fact]
	public void Parse_NullMarkdownStyle_MatchesDefaultBehavior()
	{
		// The new optional markdownStyle param must be non-breaking: passing null is identical to the
		// old single-arg path (which uses MarkdownStyle.Default).
		const string md = "[markdown]# Title\n\nBody **bold** and `code`.[/]";
		var withNull = SharpConsoleUI.Parsing.MarkupParser.Parse(md, Color.White, Color.Black, (MarkdownStyle?)null);
		var plain = SharpConsoleUI.Parsing.MarkupParser.Parse(md, Color.White, Color.Black);

		Assert.Equal(plain.Count, withNull.Count);
		for (int i = 0; i < plain.Count; i++)
			Assert.Equal(plain[i].Character, withNull[i].Character);
	}
}

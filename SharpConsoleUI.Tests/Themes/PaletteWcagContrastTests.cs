// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

/// <summary>
/// Contrast audit for the palette-generated catalog themes, measured as a real WCAG contrast ratio
/// rather than a raw-channel luminance gap.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists next to <see cref="PaletteContrastAuditTests"/>.</b> That audit compares
/// <see cref="PaletteColors.Luminance"/>, which is a weighted sum of RAW sRGB channels, and asks for
/// a gap of 60 on a 0..255 scale. That is not perceptual: sRGB is gamma-encoded, so a fixed channel
/// gap buys far less real contrast in the dark end of the range than in the light end. Slate passed
/// that audit on every pair while its scrollbar thumb sat at a true ratio of 2.85 against its own
/// track, which is invisible in practice. This audit converts to relative luminance first, which is
/// what WCAG defines and what the eye actually does.
/// </para>
/// <para>
/// <b>Why non-text pairs are included.</b> The older audit only checks foreground text against its
/// surface. Most of what a user reads on a dense TUI screen is not text: it is a scrollbar thumb
/// against its track, a filled progress bar against its unfilled remainder, a slider thumb against
/// its groove, a window border against the desktop. Those were entirely unguarded, and they are
/// where the Slate seed actually failed.
/// </para>
/// <para>
/// <b>The thresholds.</b> Text pairs are held to 4.5:1, the WCAG AA floor for normal text, since a
/// terminal cell is small text. Non-text pairs are held to 3.0:1, the AA floor for user interface
/// components and graphical objects. Both are the published minimums, not invented numbers.
/// </para>
/// </remarks>
public class PaletteWcagContrastTests
{
	/// <summary>WCAG AA for normal-size text.</summary>
	private const double TextMinimum = 4.5;

	/// <summary>WCAG AA for user interface components and graphical objects.</summary>
	private const double NonTextMinimum = 3.0;

	/// <summary>Every palette-generated theme in the seed catalog.</summary>
	public static TheoryData<string> CatalogThemes() =>
		new() { "Ocean", "Amber", "Forest", "Crimson", "Slate", "Daylight" };

	/// <summary>
	/// Text pairs: a foreground that carries glyphs, and the surface it composites onto. A null
	/// surface means the foreground sits directly on the window background.
	/// </summary>
	private static readonly (string Fg, string? Bg)[] TextPairs =
	{
		("WindowForegroundColor", "WindowBackgroundColor"),
		("ButtonForegroundColor", "ButtonBackgroundColor"),
		("ButtonFocusedForegroundColor", "ButtonFocusedBackgroundColor"),
		("ButtonSelectedForegroundColor", "ButtonSelectedBackgroundColor"),
		("ListForegroundColor", "ListBackgroundColor"),
		("ListSelectedForegroundColor", "ListSelectedBackgroundColor"),
		("ListHoverForegroundColor", "ListHoverBackgroundColor"),
		("ListUnfocusedHighlightForegroundColor", "ListUnfocusedHighlightBackgroundColor"),
		("TableForegroundColor", "TableBackgroundColor"),
		("TableHeaderForegroundColor", "TableHeaderBackgroundColor"),
		("TableSelectionForegroundColor", "TableSelectionBackgroundColor"),
		("TabHeaderForegroundColor", "TabHeaderBackgroundColor"),
		("TabHeaderActiveForegroundColor", "TabHeaderActiveBackgroundColor"),
		("DropdownForegroundColor", "DropdownBackgroundColor"),
		("DropdownFocusedForegroundColor", "DropdownFocusedBackgroundColor"),
		("DropdownHighlightForegroundColor", "DropdownHighlightBackgroundColor"),
		// A checkbox and a radio have no resting fill, so this composites onto the window.
		("CheckboxForegroundColor", "CheckboxBackgroundColor"),
		("MenuDropdownForegroundColor", "MenuDropdownBackgroundColor"),
		("MenuDropdownHighlightForegroundColor", "MenuDropdownHighlightBackgroundColor"),
		("PromptInputForegroundColor", "PromptInputBackgroundColor"),
		("PromptInputFocusedForegroundColor", "PromptInputFocusedBackgroundColor"),
		("StatusBarForegroundColor", "StatusBarBackgroundColor"),
		("StatusBarShortcutForegroundColor", "StatusBarBackgroundColor"),
		("TopBarForegroundColor", "TopBarBackgroundColor"),
		("BottomBarForegroundColor", "BottomBarBackgroundColor"),
		("ActiveTitleForegroundColor", "WindowBackgroundColor"),
		("InactiveTitleForegroundColor", "WindowBackgroundColor"),
		("ProgressBarPercentageColor", "WindowBackgroundColor"),
	};

	/// <summary>
	/// Non-text pairs: a graphical element and the surface immediately behind it. These are the
	/// parts of the screen a user reads as shape rather than as letters.
	/// </summary>
	private static readonly (string Fg, string? Bg)[] NonTextPairs =
	{
		("ScrollbarThumbColor", "ScrollbarTrackColor"),
		("ScrollbarThumbUnfocusedColor", "ScrollbarTrackUnfocusedColor"),
		("TableScrollbarThumbColor", "TableScrollbarTrackColor"),
		("ProgressBarFilledColor", "ProgressBarUnfilledColor"),
		("SliderFilledTrackColor", "SliderTrackColor"),
		("SliderThumbColor", "SliderTrackColor"),
		("SliderFocusedThumbColor", "SliderTrackColor"),
		("CheckboxCheckmarkColor", "CheckboxBackgroundColor"),
		("ActiveBorderForegroundColor", "WindowBackgroundColor"),
		("InactiveBorderForegroundColor", "WindowBackgroundColor"),
		("SeparatorForegroundColor", "WindowBackgroundColor"),
		("TableBorderColor", "TableBackgroundColor"),
		("ModalBorderForegroundColor", "ModalBackgroundColor"),
		// The desktop pattern is a graphical texture, not text: it is deliberately the faintest
		// thing on screen and nobody reads it, but it still has to be perceptible as a pattern.
		("DesktopForegroundColor", "DesktopBackgroundColor"),
	};

	/// <summary>
	/// Alpha-damped pairs. Checked separately, and against the lower floor, because what reaches
	/// the screen is the blend of these over their surface rather than the stored color.
	/// </summary>
	private static readonly (string Fg, string? Bg)[] DisabledPairs =
	{
		("ButtonDisabledForegroundColor", "ButtonDisabledBackgroundColor"),
		("CheckboxDisabledForegroundColor", "CheckboxDisabledBackgroundColor"),
		("DropdownDisabledForegroundColor", "DropdownDisabledBackgroundColor"),
		("ListDisabledForegroundColor", "ListDisabledBackgroundColor"),
		("TabHeaderDisabledForegroundColor", "TabHeaderDisabledBackgroundColor"),
		("DatePickerDisabledForegroundColor", "DatePickerDisabledBackgroundColor"),
	};

	/// <summary>One sRGB channel, linearized per the WCAG definition.</summary>
	private static double Linearize(double channel)
	{
		double c = channel / 255.0;
		return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
	}

	/// <summary>Relative luminance per WCAG 2.x, in 0..1.</summary>
	private static double RelativeLuminance(Color c)
		=> (0.2126 * Linearize(c.R)) + (0.7152 * Linearize(c.G)) + (0.0722 * Linearize(c.B));

	/// <summary>The WCAG contrast ratio between two opaque colors, in 1..21.</summary>
	internal static double ContrastRatio(Color a, Color b)
	{
		double la = RelativeLuminance(a);
		double lb = RelativeLuminance(b);
		return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
	}

	private static Color Read(ITheme theme, string member)
	{
		var property = typeof(ITheme).GetProperty(member)
			?? throw new InvalidOperationException($"ITheme has no member '{member}'.");

		// A Color? member with no value boxes to null; treat it as "shows the surface through".
		return property.GetValue(theme) is Color c ? c : Color.Transparent;
	}

	private static IEnumerable<string> Failures(
		ITheme theme, (string Fg, string? Bg)[] pairs, double minimum)
	{
		Color windowBackground = theme.WindowBackgroundColor;

		foreach (var (fgName, bgName) in pairs)
		{
			Color foreground = Read(theme, fgName);
			Color? background = bgName is null ? null : Read(theme, bgName);

			// A transparent or default control background shows the window background through, so
			// that is the surface the color truly composites onto.
			Color surface = PaletteColors.EffectiveSurface(background, windowBackground);

			// A disabled color is deliberately damped with alpha; compositing it is the control's
			// job and its faded look is the intent, so it is not held to the opaque minimum.
			// DisabledStillLegible below holds those to their own, lower bar.
			if (foreground.A != 255)
				continue;

			double ratio = ContrastRatio(foreground, surface);
			if (ratio < minimum)
			{
				yield return
					$"{fgName} on {bgName ?? "WindowBackgroundColor"}: "
					+ $"{ratio:0.00}:1 < {minimum:0.0}:1 (fg {foreground}, surface {surface})";
			}
		}
	}

	[Theory]
	[MemberData(nameof(CatalogThemes))]
	public void CatalogTheme_TextMeetsWcagAa(string themeName)
	{
		var theme = new ThemeRegistryStateService().GetTheme(themeName)!;
		var bad = Failures(theme, TextPairs, TextMinimum).ToList();

		Assert.True(
			bad.Count == 0,
			$"{themeName} has text below WCAG AA ({TextMinimum:0.0}:1):\n  " + string.Join("\n  ", bad));
	}

	[Theory]
	[MemberData(nameof(CatalogThemes))]
	public void CatalogTheme_GraphicalElementsMeetWcagAa(string themeName)
	{
		var theme = new ThemeRegistryStateService().GetTheme(themeName)!;
		var bad = Failures(theme, NonTextPairs, NonTextMinimum).ToList();

		Assert.True(
			bad.Count == 0,
			$"{themeName} has graphical elements below WCAG AA ({NonTextMinimum:0.0}:1):\n  "
			+ string.Join("\n  ", bad));
	}

	/// <summary>
	/// Disabled text is meant to recede, but it still has to be READ: a user has to see what the
	/// control says in order to know what is unavailable to them.
	/// </summary>
	/// <remarks>
	/// Held to the graphical floor rather than the text floor, and composited over its surface
	/// first, because the damping is alpha: the painted result is the blend, not the stored color.
	/// This is the one place the suite checks the disabled pairs, which the opaque audits above
	/// deliberately skip.
	/// </remarks>
	[Theory]
	[MemberData(nameof(CatalogThemes))]
	public void CatalogTheme_DisabledTextStaysLegible(string themeName)
	{
		var theme = new ThemeRegistryStateService().GetTheme(themeName)!;
		Color windowBackground = theme.WindowBackgroundColor;
		var bad = new List<string>();

		foreach (var (fgName, bgName) in DisabledPairs)
		{
			Color foreground = Read(theme, fgName);
			Color? background = bgName is null ? null : Read(theme, bgName);
			Color surface = PaletteColors.EffectiveSurface(background, windowBackground);

			// What the compositor actually puts on screen for a semi-transparent foreground.
			Color painted = foreground.A == 255
				? foreground
				: surface.Mix(foreground, foreground.A / 255.0);

			double ratio = ContrastRatio(painted, surface);
			if (ratio < NonTextMinimum)
				bad.Add($"{fgName} on {bgName ?? "WindowBackgroundColor"}: {ratio:0.00}:1 "
					+ $"< {NonTextMinimum:0.0}:1 (painted {painted}, surface {surface})");
		}

		Assert.True(
			bad.Count == 0,
			$"{themeName} has disabled text that cannot be read:\n  " + string.Join("\n  ", bad));
	}

	/// <summary>
	/// The accent must clear the graphical floor against its own background before the generator
	/// spends it on borders, thumbs, fills and focus marks.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is a floor, not the Slate regression test.</b> Slate shipped a mid-grey #64748B that
	/// measured 3.75:1, which clears this 3.0:1 bar, so this test would not have caught it and does
	/// not fail if the old seed is restored. The generator fixes in this same change are what hold
	/// the derived elements up now, and they rescue even that seed.
	/// </para>
	/// <para>
	/// The seed was still raised to #94A3B8 (6.96:1), because a generator that has to claw every
	/// derived color back to a minimum produces a theme that merely passes, not one that looks
	/// considered: the accent should carry the design, with the floors catching mistakes. Every
	/// sibling dark theme sits between 4.98:1 and 9.07:1, so Slate was the outlier.
	/// </para>
	/// </remarks>
	[Theory]
	[MemberData(nameof(CatalogThemes))]
	public void CatalogTheme_PrimaryIsAVisibleAccent(string themeName)
	{
		var theme = new ThemeRegistryStateService().GetTheme(themeName)!;
		Color primary = Read(theme, nameof(ITheme.PrimaryColor));
		double ratio = ContrastRatio(primary, theme.WindowBackgroundColor);

		Assert.True(
			ratio >= NonTextMinimum,
			$"{themeName} primary {primary} is only {ratio:0.00}:1 against its background "
			+ $"{theme.WindowBackgroundColor}; an accent needs at least {NonTextMinimum:0.0}:1.");
	}
}

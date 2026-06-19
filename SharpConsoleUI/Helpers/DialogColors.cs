// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Helpers;

/// <summary>
/// Theme-derived colors for the built-in dialogs (Settings, etc.). Centralizes the derivation so a
/// dialog's chrome — its background gradient, NavigationView selection/content surface, section
/// accents and rule colors — follows the active theme instead of hardcoded literals that look broken
/// under a non-default theme. All values anchor on the theme's seed/role colors with safe fallbacks.
/// </summary>
internal static class DialogColors
{
	/// <summary>Colors for a NavigationView's selection and content surface, derived from a theme.</summary>
	internal readonly record struct NavColorSet(Color SelectedForeground, Color SelectedBackground, Color ContentBorder, Color ContentBackground);

	/// <summary>The theme accent used as the dialog's primary chrome color.</summary>
	internal static Color Accent(ITheme theme)
		=> theme.PrimaryColor ?? theme.ButtonFocusedBackgroundColor ?? theme.WindowBackgroundColor.Tint(0.30);

	/// <summary>
	/// NavigationView selection + content-surface colors derived from the theme: the selection background
	/// is the accent (with a readable foreground), and the content surface is the window background nudged
	/// toward the accent, with a slightly brighter accent-mixed border.
	/// </summary>
	internal static NavColorSet Nav(ITheme theme)
	{
		Color accent = Accent(theme);
		Color bg = theme.WindowBackgroundColor;
		Color selectedBg = accent;
		Color selectedFg = PaletteColors.ReadableOn(selectedBg);

		// Match the demo launcher's look (DemoApp's DemoTheme): a subtle accent-nudged content surface and
		// a slightly brighter accent-mixed border. The left nav pane shows the window background/gradient;
		// the right content pane is the same background nudged toward the accent so it reads as "themed".
		Color contentBg = bg.Mix(accent, bg.IsDark() ? 0.10 : 0.06);
		Color contentBorder = bg.Mix(accent, 0.35);
		return new NavColorSet(selectedFg, selectedBg, contentBorder, contentBg);
	}

	/// <summary>
	/// A two-stop background gradient derived from the theme's window background: a slightly accent-tinted
	/// top fading to a deeper base. Subtle depth on dark themes, readable on light ones — never a fixed blue.
	/// </summary>
	internal static ColorGradient BackgroundGradient(ITheme theme)
	{
		// Same subtle, depth-not-noise gradient the demo launcher uses (DemoApp's DemoTheme): lighten the
		// top a touch on dark themes, darken it on light ones, fading to a slightly deeper base.
		Color bg = theme.WindowBackgroundColor;
		Color top = bg.IsDark() ? bg.Tint(0.06) : bg.Shade(0.04);
		Color bottom = bg.IsDark() ? bg.Shade(0.08) : bg.Shade(0.10);
		return ColorGradient.FromColors(top, bottom);
	}

	/// <summary>The color for section rules/dividers inside a dialog page — the accent, muted toward the background.</summary>
	internal static Color Rule(ITheme theme)
		=> theme.WindowBackgroundColor.Mix(Accent(theme), 0.55);

	/// <summary>
	/// The accent for a named section/role, mapped to the theme's role seeds (Primary/Info/Warning/Danger/…),
	/// falling back to the primary accent. Use for section headers and page titles so each section keeps a
	/// distinct hue that still tracks the active theme.
	/// </summary>
	internal static Color Section(ITheme theme, DialogSection section)
	{
		Color? seed = section switch
		{
			DialogSection.Appearance => theme.PrimaryColor,
			DialogSection.Performance => theme.WarningColor,
			DialogSection.Logging => theme.DangerColor,
			DialogSection.Info => theme.SecondaryColor ?? theme.TertiaryColor,
			_ => theme.PrimaryColor,
		};
		return seed ?? Accent(theme);
	}
}

/// <summary>Named sections of the built-in Settings dialog, each with its own theme-derived accent.</summary>
internal enum DialogSection
{
	Appearance,
	Performance,
	Logging,
	Info,
}

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;

namespace DemoApp.Helpers;

/// <summary>
/// Shared, theme-aware decoration for demo windows. Instead of hardcoding dark gradients (which
/// look broken under a light theme), demos derive a subtle background gradient from the ACTIVE
/// theme's window background — so it stays rich on dark themes and readable on light ones.
/// </summary>
public static class DemoTheme
{
	/// <summary>
	/// A subtle two-stop gradient derived from the active theme's window background: a slightly raised
	/// top fading to a deeper base. Looks good on both dark and light themes.
	/// </summary>
	public static ColorGradient BackgroundGradient(ConsoleWindowSystem ws)
	{
		var bg = ws.Theme.WindowBackgroundColor;
		// Nudge for depth, not noise: lighten the top a touch on dark themes, darken it on light ones.
		Color top = bg.IsDark() ? bg.Tint(0.06) : bg.Shade(0.04);
		Color bottom = bg.IsDark() ? bg.Shade(0.08) : bg.Shade(0.10);
		return ColorGradient.FromColors(top, bottom);
	}

	/// <summary>
	/// Applies the theme-derived background gradient to <paramref name="window"/> and keeps it in sync:
	/// a gradient is baked at build time and would otherwise go stale on a theme switch, so this also
	/// re-bakes it whenever the active theme changes (unsubscribing when the window closes).
	/// </summary>
	public static void ApplyThemeGradient(Window window, ConsoleWindowSystem ws)
	{
		window.BackgroundGradient = new GradientBackground(BackgroundGradient(ws), GradientDirection.Vertical);

		void OnThemeChanged(object? sender, SharpConsoleUI.Core.ThemeChangedEventArgs e)
		{
			window.BackgroundGradient = new GradientBackground(BackgroundGradient(ws), GradientDirection.Vertical);
			window.Invalidate(true);
		}

		ws.ThemeStateService.ThemeChanged += OnThemeChanged;
		window.OnClosed += (_, _) => ws.ThemeStateService.ThemeChanged -= OnThemeChanged;
	}

	/// <summary>
	/// Colors for a NavigationView's selection and content surface, derived from the active theme.
	/// Anchored on the theme's accent (<see cref="ITheme.PrimaryColor"/>, falling back to the focused-button
	/// color, then the window background) so the nav stays coherent under any theme instead of a fixed purple.
	/// </summary>
	public readonly record struct NavColorSet(Color SelectedForeground, Color SelectedBackground, Color ContentBorder, Color ContentBackground);

	/// <summary>
	/// Derives <see cref="NavColorSet"/> from the active theme. The selection background is the accent;
	/// its foreground is whatever reads on that accent; the content surface is a subtle accent-tinted
	/// shade of the window background, with a slightly brighter accent-mixed border.
	/// </summary>
	public static NavColorSet NavColors(ConsoleWindowSystem ws)
	{
		var theme = ws.Theme;
		Color accent = theme.PrimaryColor ?? theme.ButtonFocusedBackgroundColor ?? theme.WindowBackgroundColor.Tint(0.30);
		Color bg = theme.WindowBackgroundColor;

		Color selectedBg = accent;
		Color selectedFg = PaletteColors.ReadableOn(selectedBg);
		// Content surface: nudge the window background toward the accent so the pane reads as "themed"
		// without fighting the content text. Border is the same idea, a touch brighter.
		Color contentBg = bg.Mix(accent, bg.IsDark() ? 0.10 : 0.06);
		Color contentBorder = bg.Mix(accent, 0.35);
		return new NavColorSet(selectedFg, selectedBg, contentBorder, contentBg);
	}

	/// <summary>
	/// Applies theme-derived <see cref="NavColors"/> to <paramref name="nav"/> and re-applies them whenever
	/// the active theme changes (unsubscribing when <paramref name="window"/> closes), mirroring
	/// <see cref="ApplyThemeGradient"/>. Use instead of hardcoding the nav's selection/content colors.
	/// </summary>
	public static void ApplyNavColors(NavigationView nav, Window window, ConsoleWindowSystem ws)
	{
		void Apply()
		{
			var c = NavColors(ws);
			nav.SelectedItemForeground = c.SelectedForeground;
			nav.SelectedItemBackground = c.SelectedBackground;
			nav.ContentBorderColor = c.ContentBorder;
			nav.ContentBackgroundColor = c.ContentBackground;
		}

		Apply();

		void OnThemeChanged(object? sender, SharpConsoleUI.Core.ThemeChangedEventArgs e)
		{
			Apply();
			window.Invalidate(true);
		}

		ws.ThemeStateService.ThemeChanged += OnThemeChanged;
		window.OnClosed += (_, _) => ws.ThemeStateService.ThemeChanged -= OnThemeChanged;
	}
}

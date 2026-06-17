using SharpConsoleUI;
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
}

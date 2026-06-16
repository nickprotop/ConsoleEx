// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression for the washed-out-text bug: a NavigationView's item foreground used to be a hardcoded
/// White/Grey (and the formatter used a bare [dim] tag), so on a LIGHT theme the menu text rendered
/// light-on-light — invisible. Item foregrounds must follow the active theme so a light theme yields
/// dark, readable text. Verified end-to-end in tmux; this pins the resolution at the API level.
/// </summary>
public class NavigationViewThemeForegroundTests
{
	private static (ConsoleWindowSystem sys, Window window, SharpConsoleUI.Controls.NavigationView nav) Build(string themeName)
	{
		var (sys, window) = ContainerTestHelpers.CreateTestEnvironment();
		sys.ThemeStateService.SwitchTheme(themeName);
		var nav = SharpConsoleUI.Builders.Controls.NavigationView()
			.AddHeader("Section", Color.Cyan1, h => h.AddItem("Alpha", "a").AddItem("Beta", "b"))
			.Build();
		window.AddControl(nav);
		window.RenderAndGetVisibleContent();
		return (sys, window, nav);
	}

	[Fact]
	public void ItemForeground_FollowsTheme_DarkOnLightTheme()
	{
		var (_, _, nav) = Build("Daylight"); // light surface
		var fg = nav.ItemForeground;
		// On a light theme the unselected item text must be DARK, not a stale light grey.
		Assert.True(fg.IsDark(),
			$"NavigationView.ItemForeground should be dark on a light theme, got {fg} (lum={fg.Luminance():0})");
	}

	[Fact]
	public void ItemForeground_FollowsTheme_LightOnDarkTheme()
	{
		var (_, _, nav) = Build("ModernGray"); // dark surface
		var fg = nav.ItemForeground;
		Assert.False(fg.IsDark(),
			$"NavigationView.ItemForeground should be light on a dark theme, got {fg} (lum={fg.Luminance():0})");
	}

	[Fact]
	public void ItemForeground_ExplicitSet_IsPinned()
	{
		var (_, _, nav) = Build("Daylight");
		nav.ItemForeground = Color.Red;
		Assert.Equal(Color.Red, nav.ItemForeground);
	}

	[Fact]
	public void ItemForeground_HasReadableContrastAgainstSurface_OnLightTheme()
	{
		var (sys, _, nav) = Build("Daylight");
		var surface = sys.Theme.WindowBackgroundColor;
		double gap = System.Math.Abs(nav.ItemForeground.Luminance() - surface.Luminance());
		Assert.True(gap >= 60, $"item fg vs surface contrast too low: {gap:0}");
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

/// <summary>
/// Regression: switching the theme must invalidate each window's cached top/bottom border buffers, or
/// the top border keeps rendering the previous theme's colors (the border cache key is width+active
/// state only, so a theme change alone would not rebuild it).
/// </summary>
public class ThemeSwitchBorderTests
{
	[Fact]
	public void SwitchTheme_InvalidatesWindowBorderCache()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new Window(sys) { Title = "t", Width = 20, Height = 8 };
		sys.AddWindow(win);
		sys.SetActiveWindow(win);

		// Build the top-border cache.
		sys.Render.UpdateDisplay();
		Assert.NotNull(win.BorderRenderer!._cachedTopBorder);

		// Switching the theme must clear the cache so it rebuilds with the new colors.
		Assert.True(sys.ThemeStateService.SwitchTheme("Crimson"));
		Assert.Null(win.BorderRenderer!._cachedTopBorder);
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests;

public class TabControlBuilderColorsTests
{
	private static readonly Color Fg = new(10, 20, 30);
	private static readonly Color Bg = new(40, 50, 60);

	[Fact]
	public void WithActiveFocusedColors_SetsBothProps()
	{
		var tc = new TabControlBuilder().WithActiveFocusedColors(Fg, Bg).Build();
		Assert.Equal(Fg, tc.ActiveFocusedForegroundColor);
		Assert.Equal(Bg, tc.ActiveFocusedBackgroundColor);
	}

	[Fact]
	public void WithActiveUnfocusedColors_SetsBothProps()
	{
		var tc = new TabControlBuilder().WithActiveUnfocusedColors(Fg, Bg).Build();
		Assert.Equal(Fg, tc.ActiveUnfocusedForegroundColor);
		Assert.Equal(Bg, tc.ActiveUnfocusedBackgroundColor);
	}

	[Fact]
	public void WithInactiveFocusedColors_SetsBothProps()
	{
		var tc = new TabControlBuilder().WithInactiveFocusedColors(Fg, Bg).Build();
		Assert.Equal(Fg, tc.InactiveFocusedForegroundColor);
		Assert.Equal(Bg, tc.InactiveFocusedBackgroundColor);
	}

	[Fact]
	public void WithInactiveUnfocusedColors_SetsBothProps()
	{
		var tc = new TabControlBuilder().WithInactiveUnfocusedColors(Fg, Bg).Build();
		Assert.Equal(Fg, tc.InactiveUnfocusedForegroundColor);
		Assert.Equal(Bg, tc.InactiveUnfocusedBackgroundColor);
	}

	[Fact]
	public void UnsetStates_RemainNull_ForThemeToDrive()
	{
		// Only Active-Focused set; the other states stay null so the ColorRole/theme drives them.
		var tc = new TabControlBuilder().WithActiveFocusedColors(Fg, Bg).Build();
		Assert.Null(tc.InactiveUnfocusedForegroundColor);
		Assert.Null(tc.ActiveUnfocusedBackgroundColor);
	}
}

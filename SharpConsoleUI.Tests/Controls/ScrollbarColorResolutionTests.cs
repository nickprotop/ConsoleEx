// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Tests that <see cref="ScrollablePanelControl"/> scrollbar color resolution
/// respects the override > theme > fallback priority chain.
/// </summary>
public class ScrollbarColorResolutionTests
{
	private static (ConsoleWindowSystem sys, Window win) Host(IWindowControl control)
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var win = new Window(sys) { Left = 0, Top = 0, Width = 40, Height = 20 };
		win.AddControl(control);
		sys.AddWindow(win);
		return (sys, win);
	}

	[Fact]
	public void SPC_ScrollbarThumb_DefaultsNullWhenNoOverride()
	{
		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel().Build();
		Host(panel);
		Assert.Null(panel.ScrollbarThumbColor);
	}

	[Fact]
	public void SPC_ScrollbarTrack_DefaultsNullWhenNoOverride()
	{
		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel().Build();
		Host(panel);
		Assert.Null(panel.ScrollbarColor);
	}

	[Fact]
	public void SPC_ScrollbarThumb_OverrideWins()
	{
		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithScrollbarColors(trackColor: Color.Magenta1, thumbColor: Color.Lime)
			.Build();
		Assert.Equal(Color.Lime, panel.ScrollbarThumbColor);
		Assert.Equal(Color.Magenta1, panel.ScrollbarColor);
	}

	[Fact]
	public void SPC_ScrollbarColors_OverrideWinsAfterHosting()
	{
		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithScrollbarColors(trackColor: Color.Magenta1, thumbColor: Color.Lime)
			.Build();
		Host(panel);
		Assert.Equal(Color.Lime, panel.ScrollbarThumbColor);
		Assert.Equal(Color.Magenta1, panel.ScrollbarColor);
	}
}

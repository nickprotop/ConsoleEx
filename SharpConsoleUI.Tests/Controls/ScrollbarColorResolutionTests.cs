// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
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

	[Fact]
	public void MLE_ScrollbarThumbColorOverride_AffectsRenderedThumb()
	{
		var content = string.Join("\n", Enumerable.Range(1, 60).Select(i => $"line {i}"));
		var edit = new MultilineEditControl(content) { ViewportHeight = 6 };
		edit.ScrollbarThumbColor = Color.Magenta1; // distinctive override
		var (sys, _) = Host(edit);

		var buffer = new CharacterBuffer(50, 12);
		var bounds = new LayoutRect(0, 0, 48, 10);
		edit.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		bool thumbPainted = false;
		for (int y = 0; y < 12 && !thumbPainted; y++)
			for (int x = 0; x < 50; x++)
				if (buffer.GetCell(x, y).Foreground == Color.Magenta1) { thumbPainted = true; break; }

		Assert.True(thumbPainted, "the ScrollbarThumbColor override must drive the rendered thumb color");
	}
}

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
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Tests for horizontal scrolling of a ScrollablePanelControl: the horizontal scrollbar render
/// path and keyboard scrolling when the sole child is non-focusable (a pure-render canvas).
/// See docs/investigations/scrollpanel-horizontal.md.
/// </summary>
public class ScrollablePanelHorizontalTests
{
	private readonly ITestOutputHelper _out;

	public ScrollablePanelHorizontalTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	/// <summary>
	/// Builds a window (viewport ~100x20) hosting a Fill/Stretch panel (both scroll modes = Scroll)
	/// whose sole child is a non-focusable canvas 240x40 (wider AND taller than the viewport).
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, ScrollablePanelControl panel, CanvasControl canvas)
		Build(int winW = 104, int winH = 22)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(winW + 20, winH + 10);
		var window = new Window(system) { Left = 0, Top = 0, Width = winW, Height = winH };

		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			HorizontalScrollMode = ScrollMode.Scroll,
			VerticalScrollMode = ScrollMode.Scroll,
			ShowScrollbar = true,
			AutoScroll = false,
			BorderStyle = BorderStyle.None
		};

		var canvas = new CanvasControl(240, 40) { AutoSize = false, IsEnabled = false };
		panel.AddControl(canvas);

		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();
		return (system, window, panel, canvas);
	}

	private static ConsoleKeyInfo Key(ConsoleKey key) => new('\0', key, false, false, false);

	// --- Focus: a non-focusable child must not stop the panel from owning focus ---

	[Fact]
	public void PanelWithNonFocusableChild_IsFocusable_AndAutoFocuses()
	{
		var (system, window, panel, canvas) = Build();

		Assert.False(canvas.CanReceiveFocus); // IsEnabled=false canvas
		Assert.True(panel.CanReceiveFocus, "panel must be focusable as a scroll target");
		Assert.True(panel.HasFocus, "panel should hold focus when its only child cannot");
	}

	// --- Bug B: keyboard scrolling reaches the panel and moves the horizontal offset ---

	[Fact]
	public void RightArrow_ScrollsHorizontally()
	{
		var (system, window, panel, canvas) = Build();
		window.FocusManager.SetFocus(panel, FocusReason.Keyboard);

		Assert.Equal(0, panel.HorizontalScrollOffset);
		panel.ProcessKey(Key(ConsoleKey.RightArrow));
		Assert.True(panel.HorizontalScrollOffset > 0, "RightArrow should scroll the panel right");
	}

	[Fact]
	public void End_ScrollsToFarRight_WhenHorizontalIsTheOverflowAxis()
	{
		// Horizontal-only overflow (tall enough to not need vertical scroll).
		var system = TestWindowSystemBuilder.CreateTestSystem(140, 50);
		var window = new Window(system) { Left = 0, Top = 0, Width = 104, Height = 46 };
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			HorizontalScrollMode = ScrollMode.Scroll,
			VerticalScrollMode = ScrollMode.Scroll,
			ShowScrollbar = true,
			BorderStyle = BorderStyle.None
		};
		var canvas = new CanvasControl(240, 10) { AutoSize = false, IsEnabled = false };
		panel.AddControl(canvas);
		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();
		window.FocusManager.SetFocus(panel, FocusReason.Keyboard);

		Assert.False(panel.CanScrollDown, "precondition: no vertical overflow");
		Assert.True(panel.CanScrollRight, "precondition: horizontal overflow");

		panel.ProcessKey(Key(ConsoleKey.End));
		window.RenderAndGetVisibleContent();

		int maxOff = panel.TotalContentWidth - panel.ViewportWidth;
		Assert.Equal(maxOff, panel.HorizontalScrollOffset);
	}

	[Fact]
	public void RightArrow_ClampsAtMaxOffset()
	{
		var (system, window, panel, canvas) = Build();
		window.FocusManager.SetFocus(panel, FocusReason.Keyboard);

		for (int i = 0; i < panel.TotalContentWidth + 10; i++)
		{
			panel.ProcessKey(Key(ConsoleKey.RightArrow));
			window.RenderAndGetVisibleContent();
		}

		int maxOff = panel.TotalContentWidth - panel.ViewportWidth;
		Assert.Equal(maxOff, panel.HorizontalScrollOffset);
		Assert.False(panel.CanScrollRight);
	}

	// --- Bug A: the horizontal scrollbar is actually rendered ---

	[Fact]
	public void HorizontalScrollbar_IsRendered_OnBottomRow()
	{
		var (system, window, panel, canvas) = Build();

		Assert.True(panel.HasHorizontalScrollbar,
			"with horizontal overflow + ShowScrollbar + Scroll mode, an H-scrollbar should be shown");
	}

	[Fact]
	public void HorizontalScrollbar_GlyphsAppearInRenderedBuffer()
	{
		var (system, window, panel, canvas) = Build();
		var lines = window.RenderAndGetVisibleContent();

		// The H-scrollbar draws a track (─), thumb (█), and end arrows (◄ ►) on one row.
		bool found = lines.Any(line =>
			line.Contains('◄') /* ◄ */ && line.Contains('►') /* ► */ &&
			(line.Contains('█') /* █ */ || line.Contains('─') /* ─ */));

		Assert.True(found,
			"a horizontal scrollbar row (◄ … ► with track/thumb glyphs) should be present in the rendered buffer.\n" +
			string.Join("\n", lines));
	}

	[Fact]
	public void NoHorizontalScrollbar_WhenContentFitsWidth()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(140, 50);
		var window = new Window(system) { Left = 0, Top = 0, Width = 120, Height = 40 };
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalScrollMode = ScrollMode.Scroll,
			VerticalScrollMode = ScrollMode.Scroll,
			ShowScrollbar = true,
			BorderStyle = BorderStyle.None
		};
		var canvas = new CanvasControl(40, 10) { AutoSize = false, IsEnabled = false }; // fits
		panel.AddControl(canvas);
		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		Assert.False(panel.HasHorizontalScrollbar);
	}
}

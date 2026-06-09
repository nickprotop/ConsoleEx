// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for a CanvasControl that is larger than the viewport being placed inside a
/// ScrollablePanelControl. The panel must see the canvas's true buffer dimensions so it shows
/// scrollbars and scrolls. See docs/investigations/canvas-in-scrollpanel.md.
/// </summary>
public class CanvasInScrollPanelTests
{
	private readonly ITestOutputHelper _out;

	public CanvasInScrollPanelTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	/// <summary>
	/// A fixed-size (AutoSize=false) canvas larger than the viewport, as the sole child of a
	/// scroller with both scroll modes = Scroll, must drive the panel's content size so both
	/// scrollbars appear and scrolling is possible.
	/// </summary>
	[Fact]
	public void FixedSizeCanvas_LargerThanViewport_DrivesPanelScrollExtent()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 15 };

		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalScrollMode = ScrollMode.Scroll,
			VerticalScrollMode = ScrollMode.Scroll,
			ShowScrollbar = true,
			AutoScroll = false
		};

		var canvas = new CanvasControl(200, 40)
		{
			AutoSize = false
			// Default VerticalAlignment is Top (not Fill) — the canvas should drive its own size.
		};
		panel.AddControl(canvas);

		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		_out.WriteLine($"canvas.GetLogicalContentSize()={canvas.GetLogicalContentSize()}");
		_out.WriteLine($"panel.TotalContentWidth={panel.TotalContentWidth} TotalContentHeight={panel.TotalContentHeight} " +
			$"viewport={panel.ViewportWidth}x{panel.ViewportHeight} " +
			$"canScrollRight={panel.CanScrollRight} canScrollDown={panel.CanScrollDown}");

		Assert.True(panel.TotalContentWidth >= 200,
			$"panel content width should reflect the 200-wide canvas, was {panel.TotalContentWidth}");
		Assert.True(panel.TotalContentHeight >= 40,
			$"panel content height should reflect the 40-tall canvas, was {panel.TotalContentHeight}");
		Assert.True(panel.CanScrollDown, "vertical overflow should allow scrolling down");
		Assert.True(panel.CanScrollRight, "horizontal overflow should allow scrolling right");
	}

	/// <summary>
	/// The canvas must report its true logical size. The base implementation hardcoded
	/// height = 1; the override returns the real buffer height (plus margins).
	/// </summary>
	[Fact]
	public void Canvas_GetLogicalContentSize_ReportsRealBufferDimensions()
	{
		var canvas = new CanvasControl(200, 40) { AutoSize = false };
		var size = canvas.GetLogicalContentSize();
		Assert.Equal(200, size.Width);
		Assert.Equal(40, size.Height); // was 1 before the fix
	}

	/// <summary>
	/// The fix must also flow up through the panel's own logical size: a non-Fill panel reports
	/// its logical height to ITS parent by summing children's logical heights. With the height=1
	/// lie that was 1 (so any layout that respects the panel's logical height gave it a one-row
	/// slot — no viewport, no scroll). It must now reflect the canvas's true height.
	/// </summary>
	[Fact]
	public void Panel_LogicalHeight_ReflectsCanvasHeight_NotOne()
	{
		var panel = new ScrollablePanelControl
		{
			HorizontalScrollMode = ScrollMode.Scroll,
			VerticalScrollMode = ScrollMode.Scroll
		};
		panel.AddControl(new CanvasControl(200, 40) { AutoSize = false });

		Assert.Equal(40, panel.GetLogicalContentSize().Height); // was 1 before the fix
	}

	/// <summary>
	/// Consumer-side requirement: a canvas that is <see cref="VerticalAlignment.Fill"/> is sized
	/// to the viewport by design, so it does NOT drive vertical scroll. To make the canvas drive
	/// scrolling, leave it non-Fill (default Top) with an explicit size and AutoSize=false. This
	/// test pins that contract so the difference is intentional and documented.
	/// </summary>
	[Fact]
	public void FillCanvas_DoesNotDriveVerticalScroll_ByDesign()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 15 };
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalScrollMode = ScrollMode.Scroll,
			VerticalScrollMode = ScrollMode.Scroll,
			AutoScroll = false
		};
		var canvas = new CanvasControl(200, 40)
		{
			AutoSize = false,
			VerticalAlignment = VerticalAlignment.Fill
		};
		panel.AddControl(canvas);
		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		// Fill collapses the height to the viewport — content height == viewport, so no down-scroll.
		Assert.False(panel.CanScrollDown);
	}

	/// <summary>
	/// AutoSize=true must keep working: the buffer tracks the assigned bounds, so the reported
	/// logical size follows the layout slot rather than desyncing. Here a Fill+AutoSize canvas
	/// adopts the viewport size after layout.
	/// </summary>
	[Fact]
	public void AutoSizeCanvas_LogicalSizeFollowsLayoutBounds()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 15 };
		var panel = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill };
		var canvas = new CanvasControl(10, 10)
		{
			AutoSize = true,
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		panel.AddControl(canvas);
		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		// Buffer grew to fill the viewport; logical size reports those real dimensions (no desync).
		var size = canvas.GetLogicalContentSize();
		Assert.Equal(canvas.CanvasWidth, size.Width);
		Assert.Equal(canvas.CanvasHeight, size.Height);
		Assert.True(canvas.CanvasHeight > 10, $"AutoSize canvas should have grown to the viewport, was {canvas.CanvasHeight}");
	}

	/// <summary>
	/// Layout-slot confirmation: a non-Fill fixed-size canvas is laid out at its real height inside
	/// the panel (its overflow can be scrolled), not collapsed to one row or to the viewport.
	/// </summary>
	[Fact]
	public void NonFillCanvas_GetsItsRealHeightSlot_InPanelLayout()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 15 };
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			VerticalScrollMode = ScrollMode.Scroll,
			HorizontalScrollMode = ScrollMode.Scroll
		};
		var canvas = new CanvasControl(200, 40) { AutoSize = false }; // default Top alignment
		panel.AddControl(canvas);
		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		var slots = panel.GetVisibleChildLayout(panel.ViewportWidth);
		var slot = Assert.Single(slots);
		Assert.Equal(40, slot.Height); // real canvas height, not 1 and not viewport-collapsed
	}
}

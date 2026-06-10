// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Contract tests for horizontal scrolling in <see cref="ScrollablePanelControl"/>.
///
/// The panel is the scroll container: a child that reports a logical width larger than the
/// viewport must be scrollable horizontally with NO per-control code. These tests pin that
/// contract and guard the latent bugs found during analysis:
///   A. max horizontal offset must be measured against the vertical-scrollbar-reduced width
///   B. mutual scrollbar reservation (H steals a row, V steals columns) resolves consistently
///   C. hit-testing must add the horizontal scroll offset so clicks route correctly after scroll
///   D. scrollbar thumb position must round-trip with the drag handler
///   + the core gap: paint must shift child cells left by the horizontal offset, and a
///     horizontal scrollbar must be drawn / be draggable.
/// </summary>
public class ScrollablePanelHorizontalTests
{
	private readonly ITestOutputHelper _out;

	public ScrollablePanelHorizontalTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	// A canvas that writes its own column index as a single marker char per column on row 0,
	// so a rendered cell at screen X tells us exactly which canvas column is shown there.
	private static CanvasControl MakeColumnMarkerCanvas(int width, int height)
	{
		var canvas = new CanvasControl(width, height) { AutoSize = false };
		canvas.Paint += (_, e) =>
		{
			// Column c gets the glyph for (c % 10): '0'..'9'. Enough to verify offset by reading
			// the digit at a known screen column.
			for (int x = 0; x < e.CanvasWidth; x++)
				e.Graphics.SetNarrowCell(x, 0, (char)('0' + (x % 10)), Color.White, Color.Black);
		};
		return canvas;
	}

	private static (ScrollablePanelControl panel, Window window) BuildPanelWithWideCanvas(
		int canvasWidth, int canvasHeight, int winWidth = 40, int winHeight = 12,
		bool vertical = true, bool horizontal = true)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 0, Top = 0, Width = winWidth, Height = winHeight };
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalScrollMode = horizontal ? ScrollMode.Scroll : ScrollMode.None,
			VerticalScrollMode = vertical ? ScrollMode.Scroll : ScrollMode.None,
			ShowScrollbar = true,
			AutoScroll = false
		};
		panel.AddControl(MakeColumnMarkerCanvas(canvasWidth, canvasHeight));
		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();
		return (panel, window);
	}

	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var p = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags>(flags), p, p, p);
	}

	// ---- Core gap: paint applies the horizontal offset -------------------------------------

	[Fact]
	public void ScrollHorizontal_ShiftsChildCellsLeft_InPaint()
	{
		// Canvas wider than the viewport; no vertical overflow (height fits) so only H scrolls.
		var (panel, window) = BuildPanelWithWideCanvas(canvasWidth: 200, canvasHeight: 3,
			winHeight: 8, vertical: false, horizontal: true);

		var before = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		// At offset 0, screen column 0 of the content shows canvas column 0 → glyph '0'.
		Assert.Contains("0123456789", before);

		panel.ScrollHorizontalBy(5);
		var after = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());

		// After scrolling right by 5, the content now starts at canvas column 5 → "56789012...".
		Assert.Contains("567890123", after);
		Assert.True(panel.HorizontalScrollOffset == 5,
			$"offset should be 5, was {panel.HorizontalScrollOffset}");
	}

	// ---- Bug A: max offset honours the vertical-scrollbar-reduced width --------------------

	[Fact]
	public void MaxHorizontalOffset_AccountsForVerticalScrollbarWidth()
	{
		// Overflow on BOTH axes so a vertical scrollbar is present (steals 2 columns).
		var (panel, _) = BuildPanelWithWideCanvas(canvasWidth: 200, canvasHeight: 100,
			winWidth: 40, winHeight: 12, vertical: true, horizontal: true);

		Assert.True(panel.HasVerticalScrollbar, "test requires a visible vertical scrollbar");

		// Scroll fully right.
		panel.ScrollHorizontalBy(int.MaxValue / 2);

		// The visible content width is viewport minus the vertical scrollbar columns. The user
		// must be able to bring the LAST canvas column into view, i.e. max offset =
		// contentWidth(total) - visibleContentWidth, NOT total - viewportWidth.
		int visibleContentWidth = panel.ViewportWidth - 2; // 2 cols reserved for the V scrollbar
		int expectedMax = Math.Max(0, panel.TotalContentWidth - visibleContentWidth);

		Assert.Equal(expectedMax, panel.HorizontalScrollOffset);
		Assert.False(panel.CanScrollRight, "at the true max offset there is nothing more to the right");
	}

	// ---- Bug B: both scrollbars appear and reservation is consistent -----------------------

	[Fact]
	public void BothAxesOverflow_ShowsBothScrollbars()
	{
		var (panel, window) = BuildPanelWithWideCanvas(canvasWidth: 200, canvasHeight: 100,
			winWidth: 40, winHeight: 12, vertical: true, horizontal: true);

		Assert.True(panel.HasVerticalScrollbar, "vertical content overflows → vertical scrollbar");
		Assert.True(panel.HasHorizontalScrollbar, "horizontal content overflows → horizontal scrollbar");

		// The horizontal scrollbar steals exactly one content row: the viewport height the panel
		// lays children into is one less than it would be without it.
		var stripped = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("◄", stripped); // left arrow of the horizontal scrollbar
		Assert.Contains("►", stripped); // right arrow
	}

	// ---- Bug C: hit-testing accounts for horizontal scroll --------------------------------

	[Fact]
	public void Click_AfterHorizontalScroll_RoutesToCorrectChildX()
	{
		// One wide canvas that records the canvas-X of the last click.
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 8 };
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalScrollMode = ScrollMode.Scroll,
			VerticalScrollMode = ScrollMode.None,
			ShowScrollbar = true
		};

		int? clickedCanvasX = null;
		var canvas = new CanvasControl(200, 3) { AutoSize = false };
		canvas.CanvasMouseClick += (_, e) => clickedCanvasX = e.CanvasX;
		panel.AddControl(canvas);
		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		panel.ScrollHorizontalBy(10);
		window.RenderAndGetVisibleContent();

		// Click at panel screen X = content origin + 3. With offset 10, that must hit canvas X 13.
		int screenX = panel.ViewportWidth >= 4 ? 3 : 0; // content-relative; origin handled by panel
														// Click via the panel's content area. ContentInset is 0 (no border/padding) so screen X
														// inside the window maps directly. Click a few columns in.
		panel.ProcessMouseEvent(Mouse(screenX, 0, MouseFlags.Button1Clicked));

		Assert.True(clickedCanvasX.HasValue, "canvas should have received the click");
		Assert.Equal(screenX + 10, clickedCanvasX!.Value);
	}

	// ---- Bug D + drag: scrollbar drag moves the offset and round-trips with geometry -------

	[Fact]
	public void HorizontalScrollbar_DragThumb_ScrollsContent()
	{
		var (panel, window) = BuildPanelWithWideCanvas(canvasWidth: 200, canvasHeight: 3,
			winWidth: 40, winHeight: 8, vertical: false, horizontal: true);

		Assert.True(panel.HasHorizontalScrollbar);
		Assert.Equal(0, panel.HorizontalScrollOffset);

		// The horizontal scrollbar lives on the last content row (it steals one row from the
		// viewport). Press on the thumb (near the left, where it sits at offset 0), then drag right.
		int barRow = panel.ViewportHeight - 1; // bottom content row hosts the H scrollbar
											   // Press on the thumb start (just after the left arrow at content X=1).
		panel.ProcessMouseEvent(Mouse(1, barRow, MouseFlags.Button1Pressed));
		// Drag to the far right of the track.
		panel.ProcessMouseEvent(Mouse(panel.ViewportWidth - 2, barRow, MouseFlags.Button1Dragged));
		panel.ProcessMouseEvent(Mouse(panel.ViewportWidth - 2, barRow, MouseFlags.Button1Released));

		Assert.True(panel.HorizontalScrollOffset > 0,
			$"dragging the thumb right should scroll right, offset={panel.HorizontalScrollOffset}");
	}

	[Fact]
	public void HorizontalScrollbar_ClickRightArrow_ScrollsRight()
	{
		var (panel, _) = BuildPanelWithWideCanvas(canvasWidth: 200, canvasHeight: 3,
			winWidth: 40, winHeight: 8, vertical: false, horizontal: true);

		int barRow = panel.ViewportHeight - 1;
		int rightArrowX = panel.ViewportWidth - 1;
		panel.ProcessMouseEvent(Mouse(rightArrowX, barRow, MouseFlags.Button1Clicked));

		Assert.True(panel.HorizontalScrollOffset > 0, "clicking the right arrow scrolls right");
	}

	// ---- Keyboard path already exists; pin that it works without focus-capture hacks -------

	// ---- Clipping: a large canvas scrolled around must never leak outside the content area ----

	[Theory]
	[InlineData(0, 0)]           // top-left
	[InlineData(int.MaxValue, 0)] // top-right
	[InlineData(0, int.MaxValue)] // bottom-left
	[InlineData(int.MaxValue, int.MaxValue)] // bottom-right
	[InlineData(7, 11)]          // an arbitrary interior position
	public void LargeCanvas_ScrolledToExtremes_DoesNotLeakOutsideContentArea(int hTarget, int vTarget)
	{
		// The WINDOW is deliberately larger than the PANEL, with empty window background around it,
		// so the window border is NOT what clips the canvas — the panel must clip itself. Any canvas
		// cell painted in the surrounding background, on the panel's own border, or in its scrollbar
		// column/row is a real self-clipping leak (not masked by the window edge).
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 0, Top = 0, Width = 44, Height = 22 };

		const int panelWidth = 24;
		const int panelHeight = 12;
		var panel = new ScrollablePanelControl
		{
			// Sized smaller than the window client and pinned top-left so background surrounds it.
			Width = panelWidth,
			Height = panelHeight,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			BorderStyle = BorderStyle.Single,
			HorizontalScrollMode = ScrollMode.Scroll,
			VerticalScrollMode = ScrollMode.Scroll,
			ShowScrollbar = true,
			AutoScroll = false
		};

		// Every canvas cell is the marker 'X' — a glyph that appears in NO chrome (border, arrows,
		// thumb, track) and NOT in the empty window background. So any 'X' outside the panel's
		// content area is a paint leak.
		var canvas = new CanvasControl(200, 100) { AutoSize = false };
		canvas.Paint += (_, e) =>
		{
			for (int y = 0; y < e.CanvasHeight; y++)
				for (int x = 0; x < e.CanvasWidth; x++)
					e.Graphics.SetNarrowCell(x, y, 'X', Color.White, Color.Black);
		};
		panel.AddControl(canvas);
		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		Assert.True(panel.HasVerticalScrollbar && panel.HasHorizontalScrollbar,
			"test requires both scrollbars to be present");

		// Scroll to the requested position. ScrollHorizontalBy/ScrollVerticalBy clamp internally,
		// so int.MaxValue lands exactly on the max offset.
		if (hTarget != 0) panel.ScrollHorizontalBy(hTarget);
		if (vTarget != 0) panel.ScrollVerticalBy(vTarget);

		var lines = window.RenderAndGetVisibleContent()
			.Select(l => ContainerTestHelpers.StripAnsiCodes(new[] { l }))
			.ToList();

		// The panel sits at the top-left of the window client (returned grid origin). Its content
		// rectangle = inside its 1-cell border, minus the vertical scrollbar columns on the right
		// and the horizontal scrollbar row at the bottom.
		const int border = 1;
		int contentLeft = border;
		int contentTop = border;
		int contentRight = panelWidth - border - ScrollablePanelControl.VerticalScrollbarColumns; // exclusive
		int contentBottom = panelHeight - border - ScrollablePanelControl.HorizontalScrollbarRows;  // exclusive

		var leaks = new List<string>();
		int insideCount = 0;
		for (int r = 0; r < lines.Count; r++)
		{
			var line = lines[r];
			for (int c = 0; c < line.Length; c++)
			{
				if (line[c] != 'X') continue;
				bool insideContent = c >= contentLeft && c < contentRight && r >= contentTop && r < contentBottom;
				if (insideContent) insideCount++;
				else leaks.Add($"('X' at row={r}, col={c})");
			}
		}

		// Guard against a vacuous pass: the canvas must actually be painting inside the content area.
		Assert.True(insideCount > 0, "expected the canvas to paint marker cells inside the content area");

		Assert.True(leaks.Count == 0,
			$"canvas content leaked outside the panel content area at h={panel.HorizontalScrollOffset} " +
			$"v={panel.VerticalScrollOffset}; content=[{contentLeft}..{contentRight})x[{contentTop}..{contentBottom}); " +
			$"leaks: {string.Join(", ", leaks.Take(12))}");
	}

	[Fact]
	public void RightArrowKey_ScrollsHorizontally_WhenOnlyHorizontalOverflow()
	{
		var (panel, window) = BuildPanelWithWideCanvas(canvasWidth: 200, canvasHeight: 3,
			winHeight: 8, vertical: false, horizontal: true);

		// Panel becomes focusable purely from horizontal overflow (NeedsScrolling covers both axes).
		Assert.True(panel.CanReceiveFocus);

		var before = panel.HorizontalScrollOffset;
		window.FocusManager.SetFocus(panel, FocusReason.Keyboard);
		panel.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false));

		Assert.True(panel.HorizontalScrollOffset > before, "Right arrow scrolls a horizontally-overflowing panel");
	}
}

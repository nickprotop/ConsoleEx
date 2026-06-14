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

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Hardening net for the "offset reset on the next layout pass" class of bug.
///
/// The mouse-wheel bug that escaped the suite was: a scroll offset was set correctly, but the
/// NEXT measure/arrange pass clamped it back to 0. Every pre-existing scroll test asserted the
/// offset IMMEDIATELY after scrolling, with NO re-render in between — so they all passed while
/// the offset silently evaporated on the very next frame the user saw.
///
/// The defining pattern of every test here: scroll → assert immediate → FORCE a re-layout via
/// <c>system.Render.UpdateDisplay()</c> called TWICE (the idiom used throughout the real-dispatch
/// suite; the second pass is what re-runs measure/arrange after the first applied invalidations)
/// → assert the offset SURVIVED. The survive-the-relayout step is the entire point.
/// </summary>
public class ScrollablePanelOffsetSurvivesRelayoutTests
{
	#region Helpers

	/// <summary>
	/// Creates a vertically-overflowing panel, adds it to a window, and renders so layout
	/// dimensions are computed. Returns the panel, system, and window. Mirrors the helper in
	/// <see cref="ScrollablePanelControlTests"/>.
	/// </summary>
	private static (ScrollablePanelControl panel, ConsoleWindowSystem system, Window window)
		CreateRenderedScrollPanel(int childCount = 20, int panelHeight = 10)
	{
		var panel = new ScrollablePanelControl();
		panel.Height = panelHeight;
		for (int i = 0; i < childCount; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		return (panel, system, window);
	}

	/// <summary>
	/// Forces a full measure/arrange re-layout — the step that resets a buggy offset back to 0.
	/// Two passes mirror the real-dispatch suite: the first applies pending invalidations, the
	/// second re-runs layout on the now-dirty tree.
	/// </summary>
	private static void ForceRelayout(ConsoleWindowSystem system)
	{
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
	}

	#endregion

	[Fact]
	public void ScrollVerticalBy_OffsetSurvivesRelayout()
	{
		var (panel, system, _) = CreateRenderedScrollPanel();

		Assert.True(panel.TotalContentHeight > panel.ViewportHeight,
			$"precondition: content must overflow viewport " +
			$"(content={panel.TotalContentHeight} viewport={panel.ViewportHeight})");

		panel.ScrollVerticalBy(5);
		Assert.Equal(5, panel.VerticalScrollOffset);

		ForceRelayout(system);

		Assert.Equal(5, panel.VerticalScrollOffset);
	}

	[Fact]
	public void ScrollToPosition_OffsetSurvivesRelayout()
	{
		var (panel, system, _) = CreateRenderedScrollPanel();

		Assert.True(panel.TotalContentHeight > panel.ViewportHeight,
			$"precondition: content must overflow viewport " +
			$"(content={panel.TotalContentHeight} viewport={panel.ViewportHeight})");

		panel.ScrollToPosition(6);
		Assert.Equal(6, panel.VerticalScrollOffset);

		ForceRelayout(system);

		Assert.Equal(6, panel.VerticalScrollOffset);
	}

	[Fact]
	public void WheelScroll_OffsetSurvivesRelayout_AndAccumulates()
	{
		// Standalone SPC (NOT inside a CollapsiblePanel) so this exercises the direct
		// window → panel path, complementing the CollapsiblePanel-nested relayout test.
		var (panel, system, _) = CreateRenderedScrollPanel(childCount: 30, panelHeight: 10);

		Assert.True(panel.TotalContentHeight > panel.ViewportHeight,
			$"precondition: content must overflow viewport " +
			$"(content={panel.TotalContentHeight} viewport={panel.ViewportHeight})");

		Assert.Equal(0, panel.VerticalScrollOffset);

		// First wheel — aim a couple rows into the panel's painted viewport.
		var wheelDown = ContainerTestHelpers.CreateWheelDown(2, 3);
		bool handled = panel.ProcessMouseEvent(wheelDown);
		Assert.True(handled, "panel should handle the wheel event");
		int afterFirstWheel = panel.VerticalScrollOffset;
		Assert.True(afterFirstWheel > 0,
			$"first wheel must advance the offset (was 0, now {afterFirstWheel})");

		// Re-layout must NOT reset the offset.
		ForceRelayout(system);
		int afterRelayout = panel.VerticalScrollOffset;
		Assert.Equal(afterFirstWheel, afterRelayout);

		// Second wheel must accumulate on top of the survived offset, not restart from 0.
		panel.ProcessMouseEvent(ContainerTestHelpers.CreateWheelDown(2, 3));
		int afterSecondWheel = panel.VerticalScrollOffset;
		Assert.True(afterSecondWheel > afterRelayout,
			$"offset must accumulate across wheels (0 -> {afterFirstWheel} -> {afterSecondWheel}); " +
			$"if it only ever reaches {afterFirstWheel} the offset is being reset between wheels");

		// And survive one more re-layout.
		ForceRelayout(system);
		Assert.Equal(afterSecondWheel, panel.VerticalScrollOffset);
	}

	[Fact]
	public void ProgrammaticScroll_AfterStructuralChange_IsNotResetToZero()
	{
		// Consumer scenario: render the panel (which establishes the REAL on-screen viewport in the
		// instance fields), THEN a structural change (AddControl/ClearContents) drives a MEASURE pass,
		// THEN the consumer scrolls programmatically BEFORE the next paint.
		//
		// A MEASURE pass runs ResolveContentMetrics against an effectively-UNBOUNDED outer box (the panel
		// auto-sizes to its content), so the side-effect instance field _viewportHeight is left equal to
		// the FULL content height — not the real on-screen viewport. We reproduce that measure pass
		// directly (it is the same call the layout engine makes; ForceRebuildLayout defers it, but it
		// runs before the next paint the consumer never reaches). Then:
		//   maxOffset = Math.Max(0, _contentHeight - _viewportHeight)  ->  ~0
		// so without the SyncMetricsFromArrangedBounds() guard at the top of ScrollVerticalBy the scroll
		// is silently clamped to 0. The guard re-derives the viewport from the ARRANGED node bounds, so
		// the clamp uses the true max and the scroll survives.
		var (panel, _, _) = CreateRenderedScrollPanel(childCount: 20, panelHeight: 10);

		int realViewport = panel.ViewportHeight;
		Assert.True(panel.TotalContentHeight > realViewport,
			$"precondition: content must overflow the real viewport " +
			$"(content={panel.TotalContentHeight} viewport={realViewport})");

		// Clobber the side-effect fields exactly as a MEASURE pass would: an unbounded outer box leaves
		// _viewportHeight == content height. This is the staleness the audit identified.
		panel.ResolveContentMetrics(new LayoutRect(0, 0, LayoutConstraints.UnboundedThreshold, LayoutConstraints.UnboundedThreshold));
		Assert.True(panel.ViewportHeight > realViewport,
			$"sanity: the measure pass must inflate the viewport field to ~content size " +
			$"(was {realViewport}, now {panel.ViewportHeight})");

		// Consumer scrolls right after the structural change, no intervening paint.
		panel.ScrollVerticalBy(5);

		Assert.True(panel.VerticalScrollOffset > 0,
			$"programmatic scroll right after a structural change must not be reset to 0 " +
			$"(offset={panel.VerticalScrollOffset}); a content-sized stale _viewportHeight would " +
			$"collapse the clamp to 0");
		Assert.Equal(5, panel.VerticalScrollOffset);
	}

	[Fact]
	public void HorizontalScroll_OffsetSurvivesRelayout()
	{
		// A panel with a child wider than the viewport, horizontal scroll enabled. Mirrors the
		// wide-canvas setup in ScrollablePanelHorizontalTests (CanvasControl wider than viewport).
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalScrollMode = ScrollMode.Scroll,
			VerticalScrollMode = ScrollMode.None,
			ShowScrollbar = true,
			AutoScroll = false
		};
		var canvas = new CanvasControl(200, 3) { AutoSize = false };
		canvas.Paint += (_, e) =>
		{
			for (int x = 0; x < e.CanvasWidth; x++)
				e.Graphics.SetNarrowCell(x, 0, (char)('0' + (x % 10)), Color.White, Color.Black);
		};
		panel.AddControl(canvas);
		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		Assert.True(panel.TotalContentWidth > panel.ViewportWidth,
			$"precondition: content must overflow viewport horizontally " +
			$"(content={panel.TotalContentWidth} viewport={panel.ViewportWidth})");

		panel.ScrollHorizontalBy(5);
		Assert.Equal(5, panel.HorizontalScrollOffset);

		ForceRelayout(system);

		Assert.Equal(5, panel.HorizontalScrollOffset);
	}
}

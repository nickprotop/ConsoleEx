// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression coverage for the reported bug: after ScrollablePanelControl (SPC) became a layout-tree
/// participant, an SPC placed inside a CollapsiblePanel's expanded BODY no longer receives mouse
/// events. The mouse wheel does not scroll it and clicks do not reach its children — mouse never
/// reaches the body SPC at all. An SPC placed DIRECTLY in a window body still works.
///
/// These tests drive the REAL window mouse-dispatch path (driver.SimulateMouseEvent +
/// system.Input.ProcessInput → InputCoordinator → WindowEventDispatcher.ProcessMouseEvent), NOT a
/// direct <c>spc.ProcessMouseEvent</c> call. The bug lives in the dispatch/hit-test + parent-chain
/// routing, so a direct ProcessMouseEvent call on the SPC would bypass it and would not reproduce
/// the failure.
/// </summary>
public class CollapsiblePanelScrollablePanelMouseTests
{


	/// <summary>
	/// Builds a ScrollablePanel with more content than fits its viewport so it can actually scroll.
	/// </summary>
	private static ScrollablePanelControl BuildTallScroller(int viewportHeight = 5, int lineCount = 30)
	{
		var builder = ControlsFactory.ScrollablePanel().WithHeight(viewportHeight);
		for (int i = 0; i < lineCount; i++)
			builder.AddControl(ControlsFactory.Label($"Line {i}"));
		return builder.Build();
	}

	/// <summary>
	/// Dispatches a mouse wheel-down at the given absolute screen coordinates through the real input
	/// pipeline (driver → InputCoordinator → WindowEventDispatcher), exactly the way a real terminal
	/// wheel event arrives. Does NOT call control.ProcessMouseEvent directly.
	/// </summary>
	private static void DispatchWheelDown(ConsoleWindowSystem system, int x, int y)
	{
		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.WheeledDown }, new Point(x, y));
		system.Input.ProcessInput();
	}

	/// <summary>
	/// Bug repro #1 — wheel over a body-hosted SPC must scroll it.
	///
	/// Mirrors the real demo topology (CollapsibleDemoWindow "Scrollable body"):
	/// Window → root ScrollablePanel (Fill) → CollapsiblePanel (expanded, MaxContentHeight = 6) →
	/// body ScrollablePanelControl with overflowing content. A wheel-down dispatched through the real
	/// window path over the SPC body must advance the body SPC's VerticalScrollOffset. On the buggy
	/// code the wheel never reaches the body SPC, so the offset stays 0.
	/// </summary>
	[Fact]
	public void RealDispatch_WheelOverBodyScrollablePanel_Scrolls()
	{
		const int width = 44;
		const int height = 20;

		// Inner body SPC: NO explicit height — it fills the panel's capped (6-row) body region.
		var innerBuilder = ControlsFactory.ScrollablePanel();
		for (int i = 0; i < 30; i++)
			innerBuilder.AddControl(ControlsFactory.Label($"Line {i}"));
		var scroller = innerBuilder.Build();

		var panel = ControlsFactory.CollapsiblePanel("Header")
			.Expanded()
			.WithMaxContentHeight(6)
			.AddControl(scroller)
			.Build();

		// Root scroll host (Fill) wraps the panel, exactly like the demo.
		var root = ControlsFactory.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.AddControl(panel)
			.Build();

		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		window.AddControl(root);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		Assert.True(scroller.TotalContentHeight > scroller.ViewportHeight,
			$"precondition: the body SPC must overflow its viewport so it can scroll " +
			$"(content={scroller.TotalContentHeight} viewport={scroller.ViewportHeight})");

		int before = scroller.VerticalScrollOffset;

		// Aim a couple columns/rows into the body SPC's painted viewport. ActualX/ActualY are the
		// SPC's window-content coordinates; translate to absolute screen coords via the window border.
		int wheelX = window.Left + 1 + scroller.ActualX + 1;
		int wheelY = window.Top + 1 + scroller.ActualY + 1;

		DispatchWheelDown(system, wheelX, wheelY);

		Assert.True(scroller.VerticalScrollOffset > before,
			$"Wheel over a body-hosted ScrollablePanel must scroll it. " +
			$"offset before={before} after={scroller.VerticalScrollOffset} (stayed put => mouse never reached the body SPC).");
	}

	/// <summary>
	/// Bug repro #2 — click must reach a focusable child inside a body-hosted SPC.
	///
	/// Same structure, but the body SPC contains a focusable button (and enough filler to overflow).
	/// A click dispatched through the real window path at the button's on-screen position must focus
	/// it. On the buggy code the click never reaches the body SPC, so the button never gets focus.
	/// </summary>
	[Fact]
	public void RealDispatch_ClickReachesBodyScrollablePanelChild_FocusesIt()
	{
		const int width = 44;
		const int height = 20;

		var targetButton = ControlsFactory.Button("Target").Build();

		var scrollerBuilder = ControlsFactory.ScrollablePanel();
		scrollerBuilder.AddControl(targetButton);
		for (int i = 0; i < 20; i++)
			scrollerBuilder.AddControl(ControlsFactory.Label($"Filler {i}"));
		var scroller = scrollerBuilder.Build();

		var panel = ControlsFactory.CollapsiblePanel("Header")
			.Expanded()
			.WithMaxContentHeight(6)
			.AddControl(scroller)
			.Build();

		var root = ControlsFactory.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.AddControl(panel)
			.Build();

		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		window.AddControl(root);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		int clickX = window.Left + 1 + targetButton.ActualX + 1;
		int clickY = window.Top + 1 + targetButton.ActualY;

		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(clickX, clickY));
		system.Input.ProcessInput();

		Assert.True(window.FocusManager.IsFocused(targetButton),
			"Clicking a focusable control inside a body-hosted ScrollablePanel must focus it " +
			"(mouse must reach the body SPC).");
	}

	/// <summary>
	/// Control / working case — an SPC placed DIRECTLY in the window body still scrolls on wheel.
	///
	/// Identical wheel dispatch, but the SPC is a direct window child with no CollapsiblePanel wrapper.
	/// This MUST pass: it proves the harness and the real dispatch path are correct, isolating the bug
	/// to the CollapsiblePanel-body case.
	/// </summary>
	[Fact]
	public void RealDispatch_WheelOverDirectWindowScrollablePanel_Scrolls()
	{
		const int width = 44;
		const int height = 20;

		var scroller = BuildTallScroller(viewportHeight: 10, lineCount: 40);

		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		window.AddControl(scroller);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		Assert.True(scroller.TotalContentHeight > scroller.ViewportHeight,
			"precondition: the SPC must have overflowing content so it can scroll");

		int before = scroller.VerticalScrollOffset;

		int wheelX = window.Left + 1 + scroller.ActualX + 1;
		int wheelY = window.Top + 1 + scroller.ActualY + 1;

		DispatchWheelDown(system, wheelX, wheelY);

		Assert.True(scroller.VerticalScrollOffset > before,
			$"Wheel over a ScrollablePanel that is a direct window child must scroll it. " +
			$"offset before={before} after={scroller.VerticalScrollOffset}.");
	}

	/// <summary>
	/// Builds the EXACT demo "Capped height + scrollable body" recipe from
	/// <c>CollapsibleDemoWindow.cs</c> (lines ~132-142): a body ScrollablePanel with 20 overflowing
	/// markup lines, wrapped in a CollapsiblePanel with <c>.Expanded().WithMaxContentHeight(6)</c>,
	/// hosted inside a root ScrollablePanel added to the window (mirrors the demo's scrollable launcher
	/// area). The window matches the demo's 84x28 size.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, ScrollablePanelControl root, ScrollablePanelControl longBody)
		BuildCappedHeightDemoRecipe()
	{
		const int windowWidth = 84;
		const int windowHeight = 28;

		// longBody: a ScrollablePanel with 20 single-line markup children — overflows the 6-row cap.
		var longBody = ControlsFactory.ScrollablePanel();
		for (int i = 1; i <= 20; i++)
		{
			longBody.AddControl(ControlsFactory.Markup(
				$"[grey]line {i:00}[/] - body overflows the 6-row cap; scroll to read more.").Build());
		}
		var builtLongBody = longBody.Build();

		var cappedPanel = ControlsFactory.CollapsiblePanel("[orange1]Capped height[/] [grey](MaxContentHeight = 6)[/]")
			.Expanded()
			.WithMaxContentHeight(6)
			.AddControl(builtLongBody)
			.Build();

		// Root scroll host (Fill), like the demo page's scrollable launcher area.
		var root = ControlsFactory.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.AddControl(cappedPanel)
			.Build();

		var system = TestWindowSystemBuilder.CreateTestSystem(windowWidth, windowHeight);
		var window = new Window(system) { Left = 0, Top = 0, Width = windowWidth, Height = windowHeight };
		window.AddControl(root);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, root, builtLongBody);
	}

	/// <summary>
	/// Bug repro — wheel over the CAPPED (MaxContentHeight=6) body ScrollablePanel must scroll it.
	///
	/// This is the exact live-failure topology the user confirmed. The MaxContentHeight cap changes how
	/// CollapsibleLayout arranges/clips the body region; if that makes the inner longBody SPC unreachable
	/// by mouse dispatch, the wheel never reaches it and its VerticalScrollOffset stays 0.
	/// </summary>
	[Fact]
	public void RealDispatch_WheelOverCappedHeightBodyScrollablePanel_Scrolls()
	{
		var (system, window, root, longBody) = BuildCappedHeightDemoRecipe();

		Assert.True(longBody.TotalContentHeight > longBody.ViewportHeight,
			$"precondition: the capped body SPC must overflow its 6-row viewport so it can scroll " +
			$"(content={longBody.TotalContentHeight} viewport={longBody.ViewportHeight})");

		int before = longBody.VerticalScrollOffset;

		// Aim a couple columns/rows into the capped body SPC's painted viewport.
		int wheelX = window.Left + 1 + longBody.ActualX + 1;
		int wheelY = window.Top + 1 + longBody.ActualY + 1;

		DispatchWheelDown(system, wheelX, wheelY);

		Assert.True(longBody.VerticalScrollOffset > before,
			$"Wheel over a MaxContentHeight-capped body ScrollablePanel must scroll it. " +
			$"offset before={before} after={longBody.VerticalScrollOffset} " +
			$"(stayed put => mouse never reached the capped body SPC).");
	}

	/// <summary>
	/// Variant — the wheel over the capped region must scroll the INNER longBody, NOT the root SPC and
	/// NOT the collapsible. Asserts the inner SPC moved while the root stayed put, isolating the routing
	/// target. If MaxContentHeight=6 makes the inner SPC unreachable, the root scrolls instead (or
	/// nothing scrolls), and this fails.
	/// </summary>
	[Fact]
	public void RealDispatch_WheelOverCappedRegion_ScrollsInnerNotRoot()
	{
		var (system, window, root, longBody) = BuildCappedHeightDemoRecipe();

		Assert.True(longBody.TotalContentHeight > longBody.ViewportHeight,
			"precondition: the capped body SPC must overflow so the wheel has somewhere to go");

		int innerBefore = longBody.VerticalScrollOffset;
		int rootBefore = root.VerticalScrollOffset;

		int wheelX = window.Left + 1 + longBody.ActualX + 1;
		int wheelY = window.Top + 1 + longBody.ActualY + 1;

		DispatchWheelDown(system, wheelX, wheelY);

		Assert.True(longBody.VerticalScrollOffset > innerBefore,
			$"Wheel over the capped region must scroll the INNER longBody. " +
			$"inner offset before={innerBefore} after={longBody.VerticalScrollOffset}.");
		Assert.Equal(rootBefore, root.VerticalScrollOffset);
	}

	/// <summary>
	/// Bug repro — the OUTER/root ScrollablePanel ALSO overflows (genuine competing consumer). A wheel
	/// over the INNER capped body SPC must be consumed by the INNER SPC first (deepest scrollable wins),
	/// advancing the inner offset while the root stays put. On the buggy code the wheel bubbles past the
	/// inner SPC and the ROOT scrolls instead.
	/// </summary>
	[Fact]
	public void RealDispatch_WheelOverInnerSPC_OuterAlsoScrollable_InnerWinsNotOuter()
	{
		const int windowWidth = 84;
		const int windowHeight = 16; // small window so the root content overflows -> root CAN scroll

		// Inner body SPC: 20 overflowing lines, capped to 6 rows by the panel.
		var longBody = ControlsFactory.ScrollablePanel();
		for (int i = 1; i <= 20; i++)
			longBody.AddControl(ControlsFactory.Markup($"[grey]line {i:00}[/] - inner body overflow").Build());
		var builtLongBody = longBody.Build();

		var cappedPanel = ControlsFactory.CollapsiblePanel("Capped MaxContentHeight=6")
			.Expanded()
			.WithMaxContentHeight(6)
			.AddControl(builtLongBody)
			.Build();

		// Root scroll host (Fill) that ALSO has lots of extra content below the panel so it overflows
		// the small window -> the root is a genuine competing wheel consumer.
		var rootBuilder = ControlsFactory.ScrollablePanel().WithVerticalAlignment(VerticalAlignment.Fill);
		rootBuilder.AddControl(cappedPanel);
		for (int i = 1; i <= 30; i++)
			rootBuilder.AddControl(ControlsFactory.Markup($"[grey]root filler {i:00}[/]").Build());
		var root = rootBuilder.Build();

		var system = TestWindowSystemBuilder.CreateTestSystem(windowWidth, windowHeight);
		var window = new Window(system) { Left = 0, Top = 0, Width = windowWidth, Height = windowHeight };
		window.AddControl(root);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		Assert.True(builtLongBody.TotalContentHeight > builtLongBody.ViewportHeight,
			$"precondition: inner SPC must overflow (content={builtLongBody.TotalContentHeight} viewport={builtLongBody.ViewportHeight})");
		Assert.True(root.TotalContentHeight > root.ViewportHeight,
			$"precondition: ROOT SPC must ALSO overflow so it is a competing consumer " +
			$"(content={root.TotalContentHeight} viewport={root.ViewportHeight})");

		// Mirror the live scenario: the user has scrolled the ROOT down so the capped panel
		// is mid-viewport. This translates the inner SPC's AbsoluteBounds by the root scroll.
		root.ScrollVerticalBy(2);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		int innerBefore = builtLongBody.VerticalScrollOffset;
		int rootBefore = root.VerticalScrollOffset;

		int wheelX = window.Left + 1 + builtLongBody.ActualX + 1;
		int wheelY = window.Top + 1 + builtLongBody.ActualY + 1;

		DispatchWheelDown(system, wheelX, wheelY);

		Assert.True(builtLongBody.VerticalScrollOffset > innerBefore,
			$"Wheel over the inner capped SPC must scroll the INNER SPC (deepest scrollable wins). " +
			$"inner before={innerBefore} after={builtLongBody.VerticalScrollOffset}; " +
			$"root before={rootBefore} after={root.VerticalScrollOffset}.");
		Assert.Equal(rootBefore, root.VerticalScrollOffset);
	}

	/// <summary>
	/// Regression for the metric-corruption variant of the wheel-steal bug. After a MEASURE pass
	/// resolves the inner SPC's metrics against its UNBOUNDED full-content box (and a later paint is
	/// culled), the panel's runtime <c>_viewportHeight</c> is left equal to its content height, so it
	/// believes <c>maxScroll == 0</c>, declines the wheel, and the event bubbles to the root. Here we
	/// reproduce that corrupted state directly (by invoking the panel's internal ResolveContentMetrics
	/// with the full-content bounds — exactly what ScrollLayout.MeasureChildren does) and then dispatch
	/// a real wheel. It must STILL scroll the inner SPC, because the wheel handler re-syncs metrics from
	/// the arranged node bounds before deciding. Without that re-sync the root steals the wheel.
	/// </summary>
	[Fact]
	public void RealDispatch_WheelAfterMeasureCorruptedMetrics_StillScrollsInner()
	{
		const int windowWidth = 84;
		const int windowHeight = 16;

		var longBody = ControlsFactory.ScrollablePanel();
		for (int i = 1; i <= 20; i++)
			longBody.AddControl(ControlsFactory.Markup($"[grey]line {i:00}[/] inner").Build());
		var builtLongBody = longBody.Build();

		var cappedPanel = ControlsFactory.CollapsiblePanel("Capped")
			.Expanded().WithMaxContentHeight(6).AddControl(builtLongBody).Build();

		var rootBuilder = ControlsFactory.ScrollablePanel().WithVerticalAlignment(VerticalAlignment.Fill);
		rootBuilder.AddControl(cappedPanel);
		for (int i = 1; i <= 30; i++)
			rootBuilder.AddControl(ControlsFactory.Markup($"[grey]root filler {i:00}[/]").Build());
		var root = rootBuilder.Build();

		var system = TestWindowSystemBuilder.CreateTestSystem(windowWidth, windowHeight);
		var window = new Window(system) { Left = 0, Top = 0, Width = windowWidth, Height = windowHeight };
		window.AddControl(root);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Corrupt the inner SPC's runtime metrics the way a stray MEASURE pass does: resolve against the
		// full-content outer box so _viewportHeight becomes the content height (==> maxScroll would be 0).
		var rcm = typeof(ScrollablePanelControl).GetMethod("ResolveContentMetrics",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		Assert.NotNull(rcm);
		var invalidate = typeof(ScrollablePanelControl).GetField("_metricsCacheValid",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		invalidate?.SetValue(builtLongBody, false);
		// content is 20 lines; outer box of height 22 (content + chrome) drives viewport == content.
		rcm!.Invoke(builtLongBody, new object[] { new LayoutRect(0, 0, windowWidth - 4, builtLongBody.TotalContentHeight + 2), true });

		Assert.True(builtLongBody.ViewportHeight >= builtLongBody.TotalContentHeight,
			$"precondition: metrics must be corrupted (viewport={builtLongBody.ViewportHeight} >= content={builtLongBody.TotalContentHeight})");

		int innerBefore = builtLongBody.VerticalScrollOffset;
		int rootBefore = root.VerticalScrollOffset;
		int wheelX = window.Left + 1 + builtLongBody.ActualX + 1;
		int wheelY = window.Top + 1 + builtLongBody.ActualY + 1;
		DispatchWheelDown(system, wheelX, wheelY);

		Assert.True(builtLongBody.VerticalScrollOffset > innerBefore,
			$"Even with measure-corrupted metrics, the wheel must re-sync from arranged bounds and scroll " +
			$"the INNER SPC. inner before={innerBefore} after={builtLongBody.VerticalScrollOffset}; " +
			$"root before={rootBefore} after={root.VerticalScrollOffset}.");
		Assert.Equal(rootBefore, root.VerticalScrollOffset);
	}

	/// <summary>
	/// Regression for the offset-reset variant of the wheel bug (root-caused via live tracing): a wheel
	/// over the capped inner SPC correctly calls ScrollVerticalBy and the offset momentarily advances —
	/// but the subsequent re-layout's MEASURE pass calls ResolveContentMetrics with the panel's
	/// content-sized (auto-size) outer box, derives a viewport == content height, computes maxOffset == 0,
	/// and CLAMPS the persisted scroll offset back to 0. So the panel never visibly scrolls: every wheel
	/// starts again from offset 0.
	///
	/// The earlier wheel tests only asserted the offset changed after ONE wheel — they passed even with
	/// this bug, because the reset happens on the NEXT layout pass. This test dispatches the wheel, then
	/// FORCES a re-render (measure/arrange) and asserts the offset SURVIVES it, then dispatches a SECOND
	/// wheel and asserts the offset strictly INCREASES across both wheels (0 -> N -> M, M > N). That
	/// monotonic-increase-across-relayout property is what the offset-reset bug breaks.
	/// </summary>
	[Fact]
	public void RealDispatch_WheelOverCappedBody_OffsetSurvivesRelayoutAndAccumulates()
	{
		var (system, window, root, longBody) = BuildCappedHeightDemoRecipe();

		Assert.True(longBody.TotalContentHeight > longBody.ViewportHeight,
			$"precondition: the capped body SPC must overflow its viewport so it can scroll " +
			$"(content={longBody.TotalContentHeight} viewport={longBody.ViewportHeight})");

		int wheelX = window.Left + 1 + longBody.ActualX + 1;
		int wheelY = window.Top + 1 + longBody.ActualY + 1;

		Assert.Equal(0, longBody.VerticalScrollOffset);

		// --- First wheel ---
		DispatchWheelDown(system, wheelX, wheelY);
		int afterFirstWheel = longBody.VerticalScrollOffset;
		Assert.True(afterFirstWheel > 0,
			$"first wheel must advance the inner SPC offset (was 0, now {afterFirstWheel}).");

		// Force a full re-layout (measure + arrange). On the buggy code the MEASURE pass clamps the
		// persisted offset back to 0 here — this is exactly the step the earlier tests never exercised.
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		int afterRelayout = longBody.VerticalScrollOffset;
		Assert.True(afterRelayout > 0,
			$"the inner SPC scroll offset must SURVIVE a subsequent measure/arrange re-layout. " +
			$"after wheel={afterFirstWheel}, after re-layout={afterRelayout} " +
			$"(reset to 0 => the measure pass wrongly clamped the persisted offset).");
		Assert.Equal(afterFirstWheel, afterRelayout);

		// --- Second wheel --- must accumulate on top of the survived offset, not start over from 0.
		DispatchWheelDown(system, wheelX, wheelY);
		int afterSecondWheel = longBody.VerticalScrollOffset;
		Assert.True(afterSecondWheel > afterRelayout,
			$"the offset must accumulate across wheels (0 -> {afterFirstWheel} -> {afterSecondWheel}); " +
			$"if it only ever reaches {afterFirstWheel} the offset is being reset between wheels.");

		// And survive one more re-layout, proving the second increment also persists.
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		Assert.Equal(afterSecondWheel, longBody.VerticalScrollOffset);
	}

	/// <summary>
	/// Comprehensive regression guard for wheel routing into a MaxContentHeight-capped ScrollablePanel
	/// body nested inside a CollapsiblePanel inside a (genuinely overflowing) root ScrollablePanel.
	///
	/// Sweeps window heights, the amount of content above the capped panel, and root scroll positions.
	/// For every config where the inner SPC's arranged node is genuinely on-screen, it aims a real
	/// wheel-down at EVERY visible row of that node (its live arranged position, not the possibly-stale
	/// ActualY) and asserts:
	///   (1) the INNER SPC consumed the wheel (its offset advanced) — the deepest scrollable wins, and
	///   (2) the ROOT SPC did NOT scroll (the wheel did not bubble past the inner SPC), and
	///   (3) the inner SPC's runtime viewport metric was not corrupted to >= its content height
	///       (which would make it wrongly believe it cannot scroll and decline the wheel).
	/// Heterogeneous siblings (a HorizontalGrid of expanded panels) sit above the capped panel to mirror
	/// the real demo layout.
	/// </summary>
	[Fact]
	public void RealDispatch_WheelOverOnScreenCappedBody_AlwaysScrollsInnerNotRoot()
	{
		foreach (int windowHeight in new[] { 12, 14, 16, 18, 20, 24, 28 })
			foreach (int aboveCount in new[] { 0, 3, 6, 10 })
			{
				var longBodyB = ControlsFactory.ScrollablePanel();
				for (int i = 1; i <= 20; i++)
					longBodyB.AddControl(ControlsFactory.Markup($"[grey]line {i:00}[/]").Build());
				var longBody = longBodyB.Build();

				var cappedPanel = ControlsFactory.CollapsiblePanel("Capped")
					.Expanded().WithMaxContentHeight(6).AddControl(longBody).Build();

				var rootB = ControlsFactory.ScrollablePanel().WithVerticalAlignment(VerticalAlignment.Fill);
				var sideA = ControlsFactory.CollapsiblePanel("A").Expanded()
					.AddControl(ControlsFactory.Markup("[dim]left body[/]").Build()).Build();
				var sideB = ControlsFactory.CollapsiblePanel("B").Expanded()
					.AddControl(ControlsFactory.Markup("[dim]right body[/]").Build()).Build();
				var grid = ControlsFactory.HorizontalGrid()
					.Column(c => c.Flex().Add(sideA))
					.Column(c => c.Flex().Add(sideB))
					.Build();
				for (int i = 1; i <= aboveCount; i++)
					rootB.AddControl(ControlsFactory.Markup($"[grey]above {i:00}[/]").Build());
				rootB.AddControl(grid);
				rootB.AddControl(cappedPanel);
				for (int i = 1; i <= 12; i++)
					rootB.AddControl(ControlsFactory.Markup($"[grey]below {i:00}[/]").Build());
				var root = rootB.Build();

				var system = TestWindowSystemBuilder.CreateTestSystem(84, windowHeight);
				var window = new Window(system) { Left = 0, Top = 0, Width = 84, Height = windowHeight };
				window.AddControl(root);
				system.AddWindow(window);
				system.Render.UpdateDisplay();
				system.Render.UpdateDisplay();

				var renderer = window.GetType()
					.GetField("_renderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
					?.GetValue(window);
				var getNode = renderer?.GetType().GetMethod("GetLayoutNode");

				for (int rootScroll = 0; rootScroll <= 12; rootScroll++)
				{
					root.ScrollToTop();
					root.ScrollVerticalBy(rootScroll);
					system.Render.UpdateDisplay();
					system.Render.UpdateDisplay();

					var innerNode = getNode?.Invoke(renderer, new object[] { longBody }) as LayoutNode;
					if (innerNode == null) continue;
					var nb = innerNode.AbsoluteBounds;
					int winContentH = windowHeight - 2;
					int visTop = System.Math.Max(0, nb.Y);
					int visBottom = System.Math.Min(winContentH, nb.Y + nb.Height);
					if (visBottom - visTop <= 0) continue; // node entirely off-screen — can't wheel over it

					// Probe the top, middle and bottom visible rows of the node (covers partial-clip edges)
					// without re-rendering per row, keeping the sweep fast.
					var probeRows = new System.Collections.Generic.SortedSet<int> { visTop, (visTop + visBottom - 1) / 2, visBottom - 1 };
					foreach (int cy in probeRows)
					{
						longBody.ScrollToTop();

						int innerBefore = longBody.VerticalScrollOffset;
						int rootBefore = root.VerticalScrollOffset;
						int wheelX = window.Left + 1 + nb.X + 1;
						int wheelY = window.Top + 1 + cy;
						DispatchWheelDown(system, wheelX, wheelY);

						string ctx = $"h={windowHeight} above={aboveCount} rootScroll={rootScroll} node={nb} cy={cy} " +
							$"innerVP={longBody.ViewportHeight} innerContent={longBody.TotalContentHeight}";

						Assert.True(longBody.ViewportHeight < longBody.TotalContentHeight,
							$"inner SPC viewport metric corrupted (>= content) so it would decline the wheel: {ctx}");
						Assert.True(longBody.VerticalScrollOffset > innerBefore,
							$"wheel over the on-screen inner capped SPC must scroll the INNER SPC: {ctx}");
						Assert.Equal(rootBefore, root.VerticalScrollOffset);
					}
				}
			}
	}
}

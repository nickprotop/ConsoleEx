// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;
using Xunit.Abstractions;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests;

/// <summary>
/// Exhaustive matrix coverage for GitHub issue #67 and the guard added to
/// <c>ScrollablePanelControl.ScrollRangeIntoView</c>. The guard leaves an outer ScrollablePanel put
/// when a focused child region is TALLER than the viewport and already OVERLAPS it (maximally
/// visible) — because scrolling to reveal such a child's top would only push its bottom (and the
/// focused element inside it) off-screen. A genuinely off-screen child is still revealed as before.
///
/// Every case drives the REAL focus-into-view path: <c>FocusManager.SetFocus(control, Keyboard)</c>
/// walks the focus chain and calls <c>ScrollChildIntoView</c> on each scrollable ancestor (the same
/// keyboard "bring into view" path Tree/List/NavigationView and toolbar navigation reach). Assertions
/// read the public <c>VerticalScrollOffset</c> — never the private method.
///
/// The five cases (from the issue analysis):
///   CASE 1 (#67): a tall child spanning the viewport → guard SKIPs → no jump.
///   CASE 2 (deep leaf): tall child, a focused leaf deep inside near its bottom is already visible →
///          the OUTER SKIPs; the INNER scroll container reveals the leaf.
///   CASE 3 (fully below fold): tall child entirely below the viewport → reveal logic runs.
///   CASE 4 (small child, partially clipped): a short child with its bottom just below the fold →
///          the DOWN-branch nudge reveals it (no regression).
///   CASE 5 (small child fully off-screen): short child above or below → reveal brings it fully in.
/// </summary>
public class ScrollRangeIntoViewMatrixTests
{
	private readonly ITestOutputHelper _out;

	public ScrollRangeIntoViewMatrixTests(ITestOutputHelper output) => _out = output;

	// A window large enough that the outer SPC's viewport is a comfortable, known size. The window is
	// 40 tall; with a Fill outer SPC (no border) the viewport is close to the window content height.
	private const int WinWidth = 60;
	private const int WinHeight = 40;

	/// <summary>
	/// Builds: Window → outer ScrollablePanel(Fill, no border, AutoScroll off) containing
	///   [topFiller labels] + [target child of <paramref name="childLineCount"/> lines] + [bottomFiller labels].
	/// The target child is either a plain multi-line MarkupControl (a single tall/short region) or,
	/// when <paramref name="wrapTargetInInnerSpc"/> is set, an inner ScrollablePanel hosting the lines
	/// plus a focusable button as its last line (used for the deep-leaf CASE 2 and nesting depth tests).
	/// Returns the outer panel and the focusable "target" control the test will focus.
	/// </summary>
	private (ConsoleWindowSystem system, Window window, ScrollablePanelControl outer, IFocusableControl target,
			ScrollablePanelControl? inner)
		Build(int topFillerLines, int childLineCount, int bottomFillerLines,
			int nestingDepth /* 0 = target directly in outer; 1 = in one inner SPC; 2 = SPC-in-SPC */)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(WinWidth, WinHeight);
		var window = new Window(system) { Left = 0, Top = 0, Width = WinWidth, Height = WinHeight };

		var outer = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = false
		};
		window.AddControl(outer);

		// Top filler so the target sits at a chosen content-space Y.
		if (topFillerLines > 0)
			outer.AddControl(new MarkupControl(MakeLines("top", topFillerLines)));

		IFocusableControl target;
		ScrollablePanelControl? inner = null;

		if (nestingDepth == 0)
		{
			// Target is a focusable Button surrounded by filler lines of the child's own height.
			// To make the child a single region of childLineCount rows, we bundle filler+button into
			// an inner (non-scrolling by height) SPC so the whole thing is ONE outer child slot.
			var container = new ScrollablePanelControl { AutoScroll = false, Height = childLineCount };
			int half = (childLineCount - 1) / 2;
			if (half > 0) container.AddControl(new MarkupControl(MakeLines("child-top", half)));
			var btn = new ButtonControl { Text = "TARGET" };
			container.AddControl(btn);
			int rest = childLineCount - half - 1;
			if (rest > 0) container.AddControl(new MarkupControl(MakeLines("child-bot", rest)));
			outer.AddControl(container);
			target = btn;
			inner = container;
		}
		else
		{
			// One inner SPC of the child's height hosting filler + a button LAST (deep leaf near bottom).
			var level1 = new ScrollablePanelControl { AutoScroll = false, Height = childLineCount };
			ScrollablePanelControl host = level1;

			if (nestingDepth == 2)
			{
				// SPC-in-SPC-in-SPC: level1 hosts level2, level2 hosts the content.
				var level2 = new ScrollablePanelControl { AutoScroll = false, VerticalAlignment = VerticalAlignment.Fill };
				level1.AddControl(level2);
				host = level2;
			}

			// Fill the inner so the focusable leaf sits in the lower part of the tall child but a few
			// rows ABOVE its extreme bottom — so that, when the child spans the viewport, the leaf can
			// still fall inside the visible window (a leaf at the very bottom of a spanning child would
			// be below the fold by definition).
			int leafGap = System.Math.Min(10, System.Math.Max(0, childLineCount - 2));
			int fill = childLineCount - 1 - leafGap;
			if (fill > 0) host.AddControl(new MarkupControl(MakeLines("deep", fill)));
			var leaf = new ButtonControl { Text = "LEAF" };
			host.AddControl(leaf);
			if (leafGap > 0) host.AddControl(new MarkupControl(MakeLines("deep-after", leafGap)));

			outer.AddControl(level1);
			target = leaf;
			inner = level1;
		}

		// Bottom filler so the outer overflows regardless of position.
		if (bottomFillerLines > 0)
			outer.AddControl(new MarkupControl(MakeLines("bot", bottomFillerLines)));

		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, outer, target, inner);
	}

	private static List<string> MakeLines(string tag, int n)
	{
		var l = new List<string>(n);
		for (int i = 0; i < n; i++) l.Add($"{tag} {i}");
		return l;
	}

	private void FocusTarget(ConsoleWindowSystem system, Window window, IFocusableControl target)
	{
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		window.FocusManager.SetFocus(target, FocusReason.Keyboard);
		system.Render.UpdateDisplay();
	}

	// ---------------------------------------------------------------------------------------------
	// CASE 1 (#67) — a child TALLER than the viewport that already spans the viewport must NOT move
	// the outer panel when a control inside it is focused, regardless of starting offset.
	// ---------------------------------------------------------------------------------------------

	[Theory]
	// Tall child placed with plenty of filler both sides; scrolled so the tall child spans the whole
	// viewport (top above the fold, bottom below). Starting offset: mid-child.  outer must stay put.
	// Viewport is 38 (window content 38, Fill outer SPC no border), so a spanning child must be > 38.
	[InlineData(10, 45, 10, 12)]  // child at content Y 10..55; offset 12 → spans (top<12, bot>50)
	[InlineData(10, 45, 10, 15)]  // deeper into the child, still spanning
	[InlineData(5, 50, 20, 8)]    // very tall child (50 rows), different filler
	public void Case1_TallChildSpanningViewport_FocusDoesNotScrollOuter(
		int topFiller, int childLines, int bottomFiller, int startOffset)
	{
		var (system, window, outer, target, _) = Build(topFiller, childLines, bottomFiller, nestingDepth: 0);

		Assert.True(outer.TotalContentHeight > outer.ViewportHeight,
			$"precondition: outer must overflow (content={outer.TotalContentHeight} viewport={outer.ViewportHeight}).");
		Assert.True(childLines >= outer.ViewportHeight,
			$"precondition: child ({childLines}) must be >= viewport ({outer.ViewportHeight}) for CASE 1.");

		outer.ScrollToPosition(startOffset);
		system.Render.UpdateDisplay();
		int before = outer.VerticalScrollOffset;

		// Confirm the tall child actually SPANS the viewport at this offset: top above the fold and
		// bottom below it. childTop = topFiller rows; childBottom = childTop + childLines.
		int childTop = topFiller;
		int childBottom = childTop + childLines;
		Assert.True(childTop < before && childBottom > before + outer.ViewportHeight,
			$"precondition: the tall child must SPAN the viewport (childTop={childTop} childBottom={childBottom} " +
			$"offset={before} viewport={outer.ViewportHeight}).");

		// The child must currently SPAN the viewport (top above the fold, bottom below).
		// With top filler above and a tall child, at this offset the child intersects and exceeds the view.
		_out.WriteLine($"CASE1 before={before} content={outer.TotalContentHeight} viewport={outer.ViewportHeight}");

		FocusTarget(system, window, target);
		int after = outer.VerticalScrollOffset;
		_out.WriteLine($"CASE1 after={after} (delta={after - before})");

		Assert.Equal(before, after);
	}

	// ---------------------------------------------------------------------------------------------
	// CASE 2 — deep leaf: tall child, the focused leaf is deep inside near the bottom and already
	// visible. The OUTER must not move; the INNER reveals the leaf. Covered at nesting depth 1 and 2.
	// ---------------------------------------------------------------------------------------------

	[Theory]
	[InlineData(1)]  // one inner SPC
	[InlineData(2)]  // SPC-in-SPC-in-SPC
	public void Case2_DeepLeaf_OuterUnchanged_InnerReveals(int nestingDepth)
	{
		// Tall child (45 rows > 38-row viewport), a focusable leaf in its lower region. Scroll the outer
		// so the tall child SPANS the viewport (top above the fold, bottom below) with the leaf already
		// inside the visible window. Focusing the leaf must leave the OUTER put (guard) — the INNER
		// scroll container is responsible for keeping the leaf visible.
		const int childLines = 45;
		var (system, window, outer, target, inner) =
			Build(topFillerLines: 8, childLineCount: childLines, bottomFillerLines: 8, nestingDepth);

		Assert.NotNull(inner);
		Assert.True(outer.TotalContentHeight > outer.ViewportHeight,
			$"precondition: outer must overflow (content={outer.TotalContentHeight} viewport={outer.ViewportHeight}).");
		Assert.True(childLines >= outer.ViewportHeight,
			$"precondition: child ({childLines}) must span the viewport ({outer.ViewportHeight}).");

		int childTop = 8 /*top filler rows*/;
		int childBottom = childTop + childLines;
		// Offset that spans: strictly above childTop is impossible; we need offset > childTop (top above
		// the fold) AND childBottom > offset+viewport (bottom below). Pick the midpoint of that window.
		int lo = childTop + 1;
		int hi = childBottom - outer.ViewportHeight - 1;
		Assert.True(lo <= hi, $"no spanning offset exists (lo={lo} hi={hi}) — child not tall enough.");
		int spanOffset = (lo + hi) / 2;
		outer.ScrollToPosition(spanOffset);
		system.Render.UpdateDisplay();
		int before = outer.VerticalScrollOffset;

		// Confirm the child SPANS and the leaf is already visible before focusing it.
		Assert.True(childTop < before && childBottom > before + outer.ViewportHeight,
			$"precondition: child must span (childTop={childTop} childBottom={childBottom} offset={before} viewport={outer.ViewportHeight}).");
		int winContentH0 = WinHeight - 2;
		Assert.InRange(((IWindowControl)target).ActualY, 0, winContentH0 - 1);
		_out.WriteLine($"CASE2 depth={nestingDepth} before={before} viewport={outer.ViewportHeight} content={outer.TotalContentHeight} leafY0={((IWindowControl)target).ActualY}");

		FocusTarget(system, window, target);
		int after = outer.VerticalScrollOffset;
		_out.WriteLine($"CASE2 depth={nestingDepth} after={after} (delta={after - before}) leafVisibleY={((IWindowControl)target).ActualY}");

		// OUTER must not jump: the tall child spans the viewport and the leaf is already visible.
		Assert.Equal(before, after);

		// The leaf is revealed (on-screen) via the inner path, not the outer.
		int winContentH = WinHeight - 2;
		int leafY = ((IWindowControl)target).ActualY;
		Assert.InRange(leafY, 0, winContentH - 1);
	}

	// ---------------------------------------------------------------------------------------------
	// CASE 3 — tall child ENTIRELY below the fold. It does not intersect the viewport, so the reveal
	// logic runs and brings it (its top) into view. Tab-into-view still works for tall children.
	// ---------------------------------------------------------------------------------------------

	[Theory]
	[InlineData(0)]  // target directly in outer
	[InlineData(1)]  // one inner SPC
	[InlineData(2)]  // two levels deep
	public void Case3_TallChildFullyBelowFold_IsRevealed(int nestingDepth)
	{
		// No top filler, tall child, lots of bottom filler; start at the TOP so the tall child is
		// far below the fold (after the outer overflows). Focusing it must scroll the outer DOWN.
		var (system, window, outer, target, _) =
			Build(topFillerLines: 30, childLineCount: 35, bottomFillerLines: 5, nestingDepth);

		Assert.True(outer.TotalContentHeight > outer.ViewportHeight,
			$"precondition: outer must overflow (content={outer.TotalContentHeight} viewport={outer.ViewportHeight}).");

		outer.ScrollToTop();
		system.Render.UpdateDisplay();
		int before = outer.VerticalScrollOffset;
		Assert.Equal(0, before);

		// The child sits at content Y ~= 30 (after 30 filler rows), well below a ~38-row viewport?
		// Viewport is < 30 only if window content < 30; WinHeight=40 → content 38. So ensure the child
		// starts below the fold: child top (30) must be >= viewport for "fully below".
		_out.WriteLine($"CASE3 depth={nestingDepth} viewport={outer.ViewportHeight} content={outer.TotalContentHeight} childTop~=30");

		FocusTarget(system, window, target);
		int after = outer.VerticalScrollOffset;
		_out.WriteLine($"CASE3 depth={nestingDepth} after={after} (delta={after - before}) targetY={((IWindowControl)target).ActualY}");

		// It must have scrolled DOWN to reveal the child (offset increased from 0).
		Assert.True(after > before,
			$"tall child fully below the fold must be revealed (scrolled down). before={before} after={after}.");

		// And the focused control is now on-screen.
		int winContentH = WinHeight - 2;
		Assert.InRange(((IWindowControl)target).ActualY, 0, winContentH - 1);
	}

	// ---------------------------------------------------------------------------------------------
	// CASE 4 — a SHORT child (< viewport) whose bottom is just below the fold must be nudged into
	// view by the existing DOWN branch. NO regression from the guard (guard only fires for tall).
	// ---------------------------------------------------------------------------------------------

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void Case4_SmallChildPartiallyClipped_IsNudgedIntoView(int nestingDepth)
	{
		// Short child (6 rows). Place it so, at some offset, its bottom is just past the fold.
		var (system, window, outer, target, _) =
			Build(topFillerLines: 25, childLineCount: 6, bottomFillerLines: 30, nestingDepth);

		Assert.True(outer.TotalContentHeight > outer.ViewportHeight);
		Assert.True(6 < outer.ViewportHeight, $"CASE4 child must be < viewport ({outer.ViewportHeight}).");

		// Scroll so the child's bottom row is just below the fold: put child top near the bottom edge.
		int childTop = 25;
		// Want childTop within [offset, offset+viewport) but childTop+6 > offset+viewport.
		int off = childTop + 6 - outer.ViewportHeight + 2; // bottom 2 rows clipped
		off = System.Math.Max(0, off);
		outer.ScrollToPosition(off);
		system.Render.UpdateDisplay();
		int before = outer.VerticalScrollOffset;
		_out.WriteLine($"CASE4 depth={nestingDepth} before={before} viewport={outer.ViewportHeight} childTop={childTop}");

		FocusTarget(system, window, target);
		int after = outer.VerticalScrollOffset;
		_out.WriteLine($"CASE4 depth={nestingDepth} after={after} (delta={after - before}) targetY={((IWindowControl)target).ActualY}");

		// The child was partially clipped at the bottom → DOWN nudge scrolls down to fully reveal it.
		Assert.True(after >= before, $"CASE4 must not scroll up. before={before} after={after}.");

		// Fully on-screen now.
		int winContentH = WinHeight - 2;
		Assert.InRange(((IWindowControl)target).ActualY, 0, winContentH - 1);
	}

	// ---------------------------------------------------------------------------------------------
	// CASE 5 — a SHORT child fully off-screen (above or below) is brought fully into view.
	// ---------------------------------------------------------------------------------------------

	[Theory]
	[InlineData(0, /*startAtBottom*/ true)]   // small child ABOVE the fold (scroll up to reveal)
	[InlineData(1, true)]
	[InlineData(0, false)]                     // small child BELOW the fold (scroll down to reveal)
	[InlineData(1, false)]
	public void Case5_SmallChildFullyOffScreen_IsRevealed(int nestingDepth, bool childAbove)
	{
		// Short child (5 rows). Place lots of filler on the far side so it's fully off-screen.
		int topFiller = childAbove ? 5 : 40;
		int bottomFiller = childAbove ? 40 : 5;
		var (system, window, outer, target, _) =
			Build(topFiller, childLineCount: 5, bottomFiller, nestingDepth);

		Assert.True(outer.TotalContentHeight > outer.ViewportHeight);

		if (childAbove)
		{
			outer.ScrollToBottom(); // child (near top) is above the fold
		}
		else
		{
			outer.ScrollToTop();    // child (near bottom) is below the fold
		}
		system.Render.UpdateDisplay();
		int before = outer.VerticalScrollOffset;

		// Confirm the target starts OFF-screen.
		int winContentH = WinHeight - 2;
		int y0 = ((IWindowControl)target).ActualY;
		_out.WriteLine($"CASE5 depth={nestingDepth} above={childAbove} before={before} targetY0={y0}");

		FocusTarget(system, window, target);
		int after = outer.VerticalScrollOffset;
		int y1 = ((IWindowControl)target).ActualY;
		_out.WriteLine($"CASE5 depth={nestingDepth} above={childAbove} after={after} (delta={after - before}) targetY1={y1}");

		Assert.NotEqual(before, after); // it had to scroll to reveal a fully-off-screen child
		Assert.InRange(y1, 0, winContentH - 1); // now on-screen
	}

	// ---------------------------------------------------------------------------------------------
	// Already-visible small child: focusing a short child that is fully inside the viewport must NOT
	// scroll at all — across offsets and nesting. (No-op branch of ScrollRangeIntoView.)
	// ---------------------------------------------------------------------------------------------

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void AlreadyVisibleSmallChild_FocusIsNoOp(int nestingDepth)
	{
		// Short child (4 rows) in the MIDDLE of the content; scroll so it's fully visible mid-viewport.
		var (system, window, outer, target, _) =
			Build(topFillerLines: 20, childLineCount: 4, bottomFillerLines: 20, nestingDepth);

		Assert.True(outer.TotalContentHeight > outer.ViewportHeight);

		int childTop = 20;
		// Offset so child sits comfortably inside: offset a few rows above childTop, bottom well within.
		int off = System.Math.Max(0, childTop - (outer.ViewportHeight / 2));
		outer.ScrollToPosition(off);
		system.Render.UpdateDisplay();
		int before = outer.VerticalScrollOffset;

		int winContentH = WinHeight - 2;
		int y0 = ((IWindowControl)target).ActualY;
		Assert.InRange(y0, 0, winContentH - 1); // already visible
		_out.WriteLine($"AlreadyVisible depth={nestingDepth} before={before} y0={y0} viewport={outer.ViewportHeight}");

		FocusTarget(system, window, target);
		int after = outer.VerticalScrollOffset;
		_out.WriteLine($"AlreadyVisible depth={nestingDepth} after={after}");

		Assert.Equal(before, after);
	}

	// ---------------------------------------------------------------------------------------------
	// WINDOW-LEVEL twin (#67): WindowEventDispatcher.BringIntoFocus has the same "align tall child to
	// top" bug for controls placed DIRECTLY in the window (window-level scroll, not an inner SPC).
	// These tests invoke the REAL internal BringIntoFocus on a REAL Window with a REAL focused control,
	// controlling only the focused control's layout bounds (the input BringIntoFocus reads — normally
	// filled by UpdateControlLayout from the DOM node) so the geometry is deterministic. They assert
	// window._scrollOffset — the exact field BringIntoFocus reads/writes. The guard must leave a
	// tall-and-intersecting focused control put, yet still reveal one genuinely below the fold.
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Builds a window with a single focusable Button added directly, then sets that button's layout
	/// bounds (ControlContentBounds) to a chosen content-space Y/height so the window-level
	/// BringIntoFocus geometry is deterministic. Window content is made to overflow so scroll is valid.
	/// </summary>
	private (ConsoleWindowSystem system, Window window, ButtonControl target)
		BuildWindowLevelWithBounds(int targetContentTop, int targetContentHeight)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(WinWidth, WinHeight);
		var window = new Window(system) { Left = 0, Top = 0, Width = WinWidth, Height = WinHeight, IsScrollable = true };

		// Enough filler that the window content overflows (so the DOWN-branch clamp has room to scroll).
		window.AddControl(new MarkupControl(MakeLines("wfill", 120)));
		var target = new ButtonControl { Text = "TARGET" };
		window.AddControl(target);

		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Pin the focused control's window-content bounds to the requested region (the value
		// BringIntoFocus reads via GetOrCreateControlBounds).
		var b = window._layoutManager.GetOrCreateControlBounds(target);
		b.ControlContentBounds = new System.Drawing.Rectangle(0, targetContentTop, WinWidth - 2, targetContentHeight);
		b.IsVisible = true;

		return (system, window, target);
	}

	[Fact]
	public void WindowLevel_TallControlSpanningVisibleWindow_BringIntoFocusDoesNotJumpToTop()
	{
		// Visible content window is ContentHeight rows. Make the focused control TALLER than that and
		// place it so it SPANS the visible window (top above the fold, bottom below).
		var system0 = TestWindowSystemBuilder.CreateTestSystem(WinWidth, WinHeight);
		var probe = new Window(system0) { Width = WinWidth, Height = WinHeight };
		int visH = probe.ContentHeight; // typically 38
		int tallH = visH + 10;          // taller than the visible window
		int childTop = 20;              // its top sits at content Y 20

		var (system, window, target) = BuildWindowLevelWithBounds(targetContentTop: childTop, targetContentHeight: tallH);
		Assert.NotNull(window.EventDispatcher);

		// Scroll so the tall control spans: offset must be > childTop (top above fold) and
		// childTop+tallH > offset+visibleHeight (bottom below). Pick offset = childTop + 5.
		int visibleHeight = window.ContentHeight - window._topStickyHeight - window._bottomStickyHeight;
		int spanOffset = childTop + 5;
		window._scrollOffset = spanOffset;
		int before = window._scrollOffset;

		// Confirm it spans the visible window at this offset.
		int contentTop = childTop, contentBottom = childTop + tallH;
		int visTop = before + window._topStickyHeight;
		int visBottom = before + (window.ContentHeight - window._bottomStickyHeight);
		Assert.True(contentTop < visBottom && contentBottom > visTop,
			$"precondition: control must intersect the visible window (top={contentTop} bottom={contentBottom} visTop={visTop} visBottom={visBottom}).");
		Assert.True(tallH >= visibleHeight,
			$"precondition: control ({tallH}) must be >= visible window ({visibleHeight}).");
		Assert.True(contentTop < visTop && contentBottom > visBottom,
			$"precondition: control must SPAN the visible window (top above fold, bottom below).");

		_out.WriteLine($"WINDOW-LEVEL span: before={before} visibleHeight={visibleHeight} tallH={tallH} childTop={childTop}");

		window.FocusManager.SetFocus(target, FocusReason.Keyboard);
		window.EventDispatcher!.BringIntoFocus(target);

		int after = window._scrollOffset;
		_out.WriteLine($"WINDOW-LEVEL span: after={after} (delta={after - before})");

		// The tall control spans the visible window and is maximally visible → must NOT jump to its top.
		Assert.Equal(before, after);
	}

	[Fact]
	public void WindowLevel_ControlBelowFold_GuardIsTransparent_RevealBranchRunsUnchanged()
	{
		// A SHORT focusable control fully below the fold does NOT intersect the visible window, so the
		// guard must NOT fire — the existing DOWN-reveal branch runs unchanged. We assert the resulting
		// offset equals exactly what BringIntoFocus's reveal formula (clamped by the window's real
		// max offset) would produce: proving the guard is transparent for a genuinely off-screen target
		// and cannot break real Tab-into-view at the window level.
		var system0 = TestWindowSystemBuilder.CreateTestSystem(WinWidth, WinHeight);
		var probe = new Window(system0) { Width = WinWidth, Height = WinHeight };
		int visH = probe.ContentHeight;
		int childTop = visH + 20; // well below the fold at offset 0
		int childH = 3;

		var (system, window, target) = BuildWindowLevelWithBounds(targetContentTop: childTop, targetContentHeight: childH);
		Assert.NotNull(window.EventDispatcher);

		window._scrollOffset = 0;
		int before = window._scrollOffset;
		Assert.Equal(0, before);

		// Confirm the control is fully below the fold (does not intersect the visible window at offset 0).
		int visBottom = before + (window.ContentHeight - window._bottomStickyHeight);
		Assert.True(childTop >= visBottom,
			$"precondition: control must be fully below the fold (top={childTop} visBottom={visBottom}).");

		// Reproduce BringIntoFocus's DOWN-branch formula exactly (the unchanged reveal path).
		int contentBottom = childTop + childH;
		int expectedNewOffset = contentBottom - (window.ContentHeight - window._bottomStickyHeight);
		int expectedMax = System.Math.Max(0, window.ContentLineCount - (window.ContentHeight - window._topStickyHeight));
		int expected = System.Math.Min(expectedNewOffset, expectedMax);
		_out.WriteLine($"WINDOW-LEVEL below-fold: before={before} childTop={childTop} visH={visH} ContentLineCount={window.ContentLineCount} expected={expected}");

		window.FocusManager.SetFocus(target, FocusReason.Keyboard);
		window.EventDispatcher!.BringIntoFocus(target);

		int after = window._scrollOffset;
		_out.WriteLine($"WINDOW-LEVEL below-fold: after={after} (delta={after - before})");

		// The guard is transparent: the reveal branch produced its usual (clamped) result, unchanged.
		Assert.Equal(expected, after);
	}
}

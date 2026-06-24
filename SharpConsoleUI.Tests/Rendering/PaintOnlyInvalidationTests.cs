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

namespace SharpConsoleUI.Tests.Rendering;

/// <summary>
/// Tests for the paint-only-invalidation refactor (design spec §Testing).
///
/// The contract under test:
///   - <see cref="Window.Invalidate(Invalidation, IWindowControl?)"/> accumulates work via a
///     monotone Max-join (Relayout &gt; Repaint &gt; None) into a lock-free accumulator.
///   - A render consumes that accumulator atomically. When the pending work is
///     <see cref="FrameWork.Repaint"/> the Measure pass is SKIPPED (cached DesiredSize reused);
///     when <see cref="FrameWork.Relayout"/> the Measure pass runs.
///   - <see cref="Window.PendingWork"/> exposes the read-only accumulator state.
///   - <see cref="Window.LastFrameRequests"/> counts requests folded into the last consumed frame.
///   - <see cref="LayoutNode.MeasureInvocationCount"/> (internal static) increments on every Measure().
///
/// Determinism note: <see cref="LayoutNode.MeasureInvocationCount"/> is a process-global counter.
/// Tests read a DELTA tightly around a single action+render so an unrelated render cannot inflate
/// the measured value. The class is also placed in a non-parallel collection so its own tests never
/// race each other's counter reads.
/// </summary>
[Collection("PaintOnlyInvalidation")]
public class PaintOnlyInvalidationTests
{
	#region Helpers

	private static (ConsoleWindowSystem system, Window window) NewWindow(int w = 60, int h = 20)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(w + 4, h + 4);
		var window = new Window(system)
		{
			Title = "PaintOnly",
			Left = 0,
			Top = 0,
			Width = w,
			Height = h
		};
		system.AddWindow(window);
		return (system, window);
	}

	private static MarkupControl AddMarkup(Window window, params string[] lines)
	{
		var control = new MarkupControl(new List<string>(lines));
		window.AddControl(control);
		return control;
	}

	/// <summary>
	/// Render the window once, forcing the buffer rebuild (consumes pending work). A non-empty
	/// visible region is passed so EnsureContentReady treats the frame as on-screen and consumes the
	/// pending work to None — the parameterless overload (null regions) is the off-screen path that
	/// deliberately re-raises a Repaint.
	/// </summary>
	private static List<string> Render(Window window)
	{
		var region = new List<Rectangle> { new Rectangle(0, 0, Math.Max(1, window.Width), Math.Max(1, window.Height)) };
		return window.RenderAndGetVisibleContent(region);
	}

	/// <summary>Measure delta captured tightly around a single action that includes exactly one render.</summary>
	private static long MeasureDelta(Action action)
	{
		long before = System.Threading.Interlocked.Read(ref LayoutNode.MeasureInvocationCount);
		action();
		long after = System.Threading.Interlocked.Read(ref LayoutNode.MeasureInvocationCount);
		return after - before;
	}

	private static MouseEventArgs Mouse(MouseFlags flag, int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags> { flag }, pos, pos, pos);
	}

	#endregion

	// 1. Default still measures.
	[Fact]
	public void Default_FreshWindow_MeasuresOnFirstRender()
	{
		var (system, window) = NewWindow();
		AddMarkup(window, "Hello world");

		// A fresh window starts at Relayout.
		Assert.Equal(FrameWork.Relayout, window.PendingWork);

		long delta = MeasureDelta(() => Render(window));

		Assert.True(delta > 0, $"First render must run Measure (delta was {delta}).");
		Assert.Equal(FrameWork.None, window.PendingWork);
	}

	// 2. Paint-only skips measure.
	[Fact]
	public void PaintOnly_SkipsMeasure_ButStillRebuilds()
	{
		var (system, window) = NewWindow();
		AddMarkup(window, "Hello world");
		Render(window); // initial measured frame
		Assert.Equal(FrameWork.None, window.PendingWork);

		window.Invalidate(Invalidation.Repaint);
		Assert.Equal(FrameWork.Repaint, window.PendingWork);

		long delta = MeasureDelta(() => Render(window));

		Assert.Equal(0, delta); // Measure pass skipped
		Assert.Equal(FrameWork.None, window.PendingWork); // frame was consumed (rebuilt)
	}

	// 3. Paint-only reflects the change.
	[Fact]
	public void PaintOnly_ReflectsContentChange_WithoutMeasure()
	{
		var (system, window) = NewWindow();
		var markup = AddMarkup(window, "PAINTED-CONTENT");
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// A pure Repaint frame: the cached layout is reused (no Measure), but the buffer is rebuilt
		// and the content is reflected in the painted output.
		window.Invalidate(Invalidation.Repaint);
		long delta = MeasureDelta(() => Render(window));
		var lines = Render(window);

		Assert.Equal(0, delta);
		Assert.Contains(lines, l => l.Contains("PAINTED-CONTENT"));
	}

	// 4. DesiredSize persists across paint-only.
	[Fact]
	public void DesiredSize_PersistsAcrossPaintOnly()
	{
		var (system, window) = NewWindow();
		AddMarkup(window, "Stable size line", "Second line");
		var measuredLines = Render(window);
		string measuredJoined = string.Join("\n", measuredLines);

		// Paint-only frame must not re-measure and must reproduce the same stable output.
		window.Invalidate(Invalidation.Repaint);
		long delta = MeasureDelta(() => Render(window));
		var paintOnlyLines = Render(window);

		Assert.Equal(0, delta);
		// NOTE: the root LayoutNode.DesiredSize is not exposed on Window, so size stability is
		// asserted via the stable rendered output (same line content/width) instead.
		Assert.Equal(measuredJoined, string.Join("\n", paintOnlyLines));
	}

	// 5. Layout change re-measures.
	[Fact]
	public void LayoutChange_ReMeasures()
	{
		var (system, window) = NewWindow();
		var markup = AddMarkup(window, "short");
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// SetContent raises Relayout (a genuine size-affecting change).
		long delta = MeasureDelta(() =>
		{
			markup.SetContent(new List<string> { "a much much much longer line of text than before" });
			Assert.Equal(FrameWork.Relayout, window.PendingWork);
			Render(window);
		});

		Assert.True(delta > 0, $"A Relayout frame must run Measure (delta was {delta}).");
	}

	// 6. Off-screen -> visible reuses prior measure.
	[Fact]
	public void OffScreen_ThenVisible_ReusesPriorMeasure()
	{
		var (system, window) = NewWindow();
		AddMarkup(window, "Visible content here");

		// First render with NO visible regions (off-screen): EnsureContentReady still rebuilds +
		// measures once, then re-raises a Repaint so the eventual on-screen frame reuses the measure.
		long firstDelta = MeasureDelta(() => window.RenderAndGetVisibleContent(new List<Rectangle>()));
		Assert.True(firstDelta > 0, $"Off-screen first build must measure (delta was {firstDelta}).");

		// Now render WITH a visible region — should reuse the prior measure (Measure skipped).
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		long secondDelta = MeasureDelta(() => window.RenderAndGetVisibleContent(region));

		// NOTE: the off-screen path re-raised Repaint, so the on-screen frame is a paint-only frame.
		Assert.Equal(0, secondDelta);
		var lines = window.RenderAndGetVisibleContent(region);
		Assert.Contains(lines, l => l.Contains("Visible content here"));
	}

	// 7. Arrange still runs on paint-only.
	[Fact]
	public void Arrange_StillRuns_OnPaintOnly()
	{
		var (system, window) = NewWindow(40, 8);
		// Tall content so the window is scrollable.
		var lines = new List<string>();
		for (int i = 0; i < 40; i++)
			lines.Add($"Line {i:00}");
		AddMarkup(window, lines.ToArray());
		Render(window);

		// Set a scroll offset directly (no invalidation), then drive a PURE Repaint frame.
		int target = 5;
		window.ScrollOffset = target;
		window.Invalidate(Invalidation.Repaint);

		long delta = MeasureDelta(() => Render(window));
		var rendered = Render(window);

		Assert.Equal(0, delta); // paint-only: no measure
								// Arrange (which honours the scroll offset) still ran: the top visible line reflects the scroll.
		Assert.Equal(target, window.ScrollOffset);
		Assert.Contains(rendered, l => l.Contains($"Line {target:00}"));
		// The pre-scroll first line must NOT be visible anymore.
		Assert.DoesNotContain(rendered, l => l.Contains("Line 00"));
	}

	// 8. Nested container forwards the request (Repaint, not Relayout).
	//
	// Regression guard for a bug this test originally caught: HorizontalGridControl.Invalidate(work) and
	// ColumnContainer.InvalidateOnlyColumnContents used to hard-code Relayout, upgrading a child's Repaint
	// to Relayout across the whole grid subtree (defeating paint-only). Now both forward `work` faithfully,
	// so a nested Repaint stays a Repaint at the window. (A genuine layout change still raises Relayout via
	// the width-setter's own Invalidate, folded by the accumulator's Max-join.)
	[Fact]
	public void NestedContainer_ForwardsRepaintRequest()
	{
		var (system, window) = NewWindow();
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);
		grid.AddColumn(col1);
		grid.AddColumn(col2);
		var markup = new MarkupControl(new List<string> { "nested" });
		col1.AddContent(markup);
		window.AddControl(grid);
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// A control deep inside the grid -> column requests Repaint; it must propagate to the window
		// as Repaint (not get upgraded to Relayout along the way).
		markup.Invalidate(Invalidation.Repaint);

		Assert.Equal(FrameWork.Repaint, window.PendingWork);
	}

	// 9. Cross-thread Relayout SET side.
	[Fact]
	public void CrossThread_RelayoutRequest_IsHonoured()
	{
		var (system, window) = NewWindow();
		AddMarkup(window, "cross-thread");
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Invalidate from another thread; the Request path is lock-free.
		System.Threading.Tasks.Task.Run(() => window.Invalidate(Invalidation.Relayout)).GetAwaiter().GetResult();

		Assert.Equal(FrameWork.Relayout, window.PendingWork);

		long delta = MeasureDelta(() => Render(window));
		Assert.True(delta > 0, $"A cross-thread Relayout must result in a Measure (delta was {delta}).");
	}

	// 10. Consume CLEAR side — intent is not erased.
	[Fact]
	public void Consume_ClearSide_RelayoutAfterConsumeStillMeasures()
	{
		var (system, window) = NewWindow();
		AddMarkup(window, "clear-side");
		Render(window); // consume -> None
		Assert.Equal(FrameWork.None, window.PendingWork);

		window.Invalidate(Invalidation.Relayout);
		Assert.Equal(FrameWork.Relayout, window.PendingWork);

		long delta = MeasureDelta(() => Render(window));
		Assert.True(delta > 0, $"Relayout requested after a consume must still measure (delta was {delta}).");
		Assert.Equal(FrameWork.None, window.PendingWork);
	}

	// 11. Lattice join (Max).
	[Fact]
	public void LatticeJoin_MaxOfRequests()
	{
		// Repaint then Relayout then Repaint -> Relayout (Repaint can't downgrade).
		var (system1, window1) = NewWindow();
		AddMarkup(window1, "join");
		Render(window1); // clean
		Assert.Equal(FrameWork.None, window1.PendingWork);

		window1.Invalidate(Invalidation.Repaint);
		window1.Invalidate(Invalidation.Relayout);
		window1.Invalidate(Invalidation.Repaint);
		Assert.Equal(FrameWork.Relayout, window1.PendingWork);

		// Repaint alone -> Repaint.
		var (system2, window2) = NewWindow();
		AddMarkup(window2, "join2");
		Render(window2); // clean
		Assert.Equal(FrameWork.None, window2.PendingWork);

		window2.Invalidate(Invalidation.Repaint);
		Assert.Equal(FrameWork.Repaint, window2.PendingWork);

		// Freshly rendered (clean) window -> None.
		var (system3, window3) = NewWindow();
		AddMarkup(window3, "join3");
		Render(window3);
		Assert.Equal(FrameWork.None, window3.PendingWork);
	}

	// 12. Illegal state unspellable (type-level guarantee).
	[Fact]
	public void IllegalState_Unspellable_EnumShapes()
	{
		var frameValues = Enum.GetValues<FrameWork>();
		Assert.Equal(3, frameValues.Length);
		Assert.Contains(FrameWork.None, frameValues);
		Assert.Contains(FrameWork.Repaint, frameValues);
		Assert.Contains(FrameWork.Relayout, frameValues);

		var invalidationValues = Enum.GetValues<Invalidation>();
		Assert.Equal(2, invalidationValues.Length);
		Assert.Contains(Invalidation.Repaint, invalidationValues);
		Assert.Contains(Invalidation.Relayout, invalidationValues);
		// There is no Invalidation.None — "request nothing" is unspellable.
		Assert.DoesNotContain(invalidationValues, v => (int)v == 0);

		// Values line up so a max-join over ints is well-defined.
		Assert.Equal((int)FrameWork.Repaint, (int)Invalidation.Repaint);
		Assert.Equal((int)FrameWork.Relayout, (int)Invalidation.Relayout);
	}

	// 13. Request-count metric.
	[Fact]
	public void RequestCount_Metric_CoalescesAndReportsCount()
	{
		var (system, window) = NewWindow();
		AddMarkup(window, "count");
		Render(window); // clean
		Assert.Equal(FrameWork.None, window.PendingWork);

		const int n = 5;
		for (int i = 0; i < n; i++)
			window.Invalidate(Invalidation.Repaint);
		Assert.Equal(FrameWork.Repaint, window.PendingWork);

		long delta = MeasureDelta(() => Render(window));

		Assert.Equal(n, window.LastFrameRequests); // all 5 folded into the one consumed frame
		Assert.Equal(FrameWork.None, window.PendingWork); // consumed
		Assert.Equal(0, delta); // the coalesced work was Repaint -> no measure
	}

	// 14. "Real thing" MarkupControl selection (headless).
	[Fact]
	public void RealThing_MarkupSelection_IsRepaintNotRelayout()
	{
		var (system, window) = NewWindow(40, 10);
		var markup = new MarkupControl(new List<string> { "The quick brown fox jumps over the lazy dog" })
		{
			EnableSelection = true
		};
		window.AddControl(markup);
		Render(window); // populates the cached row geometry that hit-testing relies on
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Drive a real selection gesture through the public mouse entry point: press to anchor,
		// then several drag-extends. TryHitTest clamps coordinates against the cached rows, so the
		// gesture lands on the (single) painted row regardless of exact origin.
		markup.ProcessMouseEvent(Mouse(MouseFlags.Button1Pressed, 0, 0));

		long totalDelta = MeasureDelta(() =>
		{
			for (int x = 1; x <= 10; x++)
			{
				markup.ProcessMouseEvent(Mouse(MouseFlags.Button1Dragged, x, 0));
				Render(window);
			}
		});

		// Selection is a Repaint-class change: across all the extend frames, no Measure should run.
		Assert.Equal(0, totalDelta);
		// The selection actually changed.
		Assert.True(markup.HasSelection, "Drag-extend should have produced a non-empty selection.");
		Assert.False(string.IsNullOrEmpty(markup.GetSelectedText()), "Selected text should be non-empty.");
		// Each extend folded into a frame; the coalescing metric reflects per-frame requests.
		Assert.True(window.LastFrameRequests >= 1, "At least one request should fold into the last frame.");
	}

	// 15. "Real thing" HorizontalGrid fan-out (headless).
	[Fact]
	public void RealThing_HorizontalGridFanOut_NoRecursion_Relayout_RendersBothColumns()
	{
		var (system, window) = NewWindow(60, 10);
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);
		grid.AddColumn(col1);
		grid.AddColumn(col2);
		col1.AddContent(new MarkupControl(new List<string> { "LEFT-COLUMN" }));
		col2.AddContent(new MarkupControl(new List<string> { "RIGHT-COLUMN" }));
		window.AddControl(grid);
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// (a) The grid's overridden fan-out Invalidate must not infinite-recurse. Reaching the
		// assertion below at all is the guard (the re-entrancy guard _invalidatingContainers holds).
		grid.Invalidate(Invalidation.Relayout);

		// (b) It propagated to the window as Relayout.
		Assert.Equal(FrameWork.Relayout, window.PendingWork);

		// (c) A subsequent render measures and produces correct output for both columns.
		long delta = MeasureDelta(() => Render(window));
		Assert.True(delta > 0, $"The Relayout frame must measure (delta was {delta}).");

		var lines = Render(window);
		string joined = string.Join("\n", lines);
		Assert.Contains("LEFT-COLUMN", joined);
		Assert.Contains("RIGHT-COLUMN", joined);
	}
}

/// <summary>
/// Serializes <see cref="PaintOnlyInvalidationTests"/> so its global Measure-counter reads never race.
/// </summary>
[Xunit.CollectionDefinition("PaintOnlyInvalidation", DisableParallelization = true)]
public class PaintOnlyInvalidationCollection { }

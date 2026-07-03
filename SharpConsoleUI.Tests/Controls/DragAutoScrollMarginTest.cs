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
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests;

/// <summary>
/// Regression for the #67 follow-on drag-autoscroll ticker bug: a MARGINED, nested MarkupControl
/// (Margin.Top &gt; 0, NOT hosted directly in a ScrollablePanel) mis-computes the clip-relative drag Y,
/// double-counting Margin.Top. An IN-BOUNDS drag (cursor on the label's own visible row) then reports a
/// drag Y == ViewportHeightRows, which DragAutoScroll.ComputeStep treats as "past the bottom edge" and
/// fires a continuous downward autoscroll — marching the selection end to the label's last column every
/// frame (BUG1 jitter) and keeping the drag effectively alive (BUG2 later-click extends).
///
/// The existing DragAutoScrollTargetTests only cover the zero-margin, SPC-direct case, where the
/// double-count term is 0, so this class fills that gap.
/// </summary>
public class DragAutoScrollMarginTest
{
	private readonly ITestOutputHelper _out;

	public DragAutoScrollMarginTest(ITestOutputHelper output) => _out = output;

	private static MouseEventArgs Mouse(int x, int y, MouseFlags flags)
	{
		var p = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags> { flags }, p, p, p);
	}

	/// <summary>
	/// An IN-BOUNDS drag on the label's own visible row must NOT trigger drag-autoscroll. Uses the real
	/// #67 nested topology (BuildDemo17a) so the label has Margin.Top=1 and is not SPC-direct.
	/// </summary>
	[Fact]
	public void InBoundsDrag_OnMarginedNestedLabel_DoesNotAutoScroll()
	{
		const int width = 100;
		const int height = 24;

		var (system, window, outputPanel, btn1, btn2, label) = Issue67RowJitterTest.BuildDemo17a(width, height);

		// Scroll the outer panel down so the trailing label is visible near the bottom.
		outputPanel.ScrollToBottom();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		int winContentH = height - 2;
		Assert.True(label.ActualY >= 0 && label.ActualY < winContentH,
			$"precondition: label must be visible (label.ActualY={label.ActualY}, winContentH={winContentH}).");

		var t = (IDragAutoScrollTarget)label;

		// The label has Margin.Top = 1, so its FIRST visible content row is at content-relative
		// Position.Y == Margin.Top (the mouse Y delivered to the control is measured from the control's
		// bounds top, before the margin — see TryHitTest: row = mouseY - _cachedOriginY, where
		// _cachedOriginY == topInset includes Margin.Top). A real click on the visible label text
		// therefore arrives at Position.Y = Margin.Top, NOT 0. This mirrors the live trace
		// (args.Position.Y = 1, the visible row) that reproduced the bug.
		int visibleRowY = label.Margin.Top; // == 1 for BuildDemo17a's label

		// Press to anchor + register the drag target on the label's first VISIBLE content row.
		((IMouseAwareControl)label).ProcessMouseEvent(Mouse(1, visibleRowY, MouseFlags.Button1Pressed));

		// Drag to a position that is CLEARLY IN-BOUNDS: on the label's own visible content row, a few
		// columns to the right. This is a normal in-bounds selection drag, NOT past any edge.
		((IMouseAwareControl)label).ProcessMouseEvent(Mouse(10, visibleRowY, MouseFlags.Button1Dragged));

		int dragY = t.LastDragRelativeY;
		int vpRows = t.ViewportHeightRows;
		_out.WriteLine($"IN-BOUNDS drag: LastDragRelativeY={dragY}, ViewportHeightRows={vpRows}, " +
			$"IsDragSelecting={t.IsDragSelecting}, HasSelection={label.HasSelection}");

		// Confirm the drag registered (a non-firing drag must not be mistaken for a pass).
		Assert.True(t.IsDragSelecting && label.HasSelection,
			$"the label drag did not register (IsDragSelecting={t.IsDragSelecting}, HasSelection={label.HasSelection}).");

		// The core assertion: an in-bounds drag on the visible row must produce ZERO autoscroll step.
		double carry = 0;
		int step = SharpConsoleUI.Helpers.DragAutoScroll.ComputeStep(dragY, vpRows, elapsedMs: 1000, ref carry);
		_out.WriteLine($"ComputeStep(dragY={dragY}, vpRows={vpRows}, 1000ms) = {step} (expected 0 for in-bounds)");

		Assert.True(dragY >= 0 && dragY <= vpRows - 1,
			$"in-bounds drag Y must be within [0, {vpRows - 1}] but was {dragY} (== ViewportHeightRows means the margin was double-counted).");
		Assert.Equal(0, step);
	}



	/// <summary>
	/// Guard: a genuinely PAST-EDGE drag (cursor far below the label's visible bottom) MUST still trigger
	/// autoscroll after the fix — don't over-correct and kill legitimate autoscroll.
	/// </summary>
	[Fact]
	public void PastEdgeDrag_OnMarginedNestedLabel_StillAutoScrolls()
	{
		const int width = 100;
		const int height = 24;

		var (system, window, outputPanel, btn1, btn2, label) = Issue67RowJitterTest.BuildDemo17a(width, height);

		outputPanel.ScrollToBottom();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var t = (IDragAutoScrollTarget)label;

		((IMouseAwareControl)label).ProcessMouseEvent(Mouse(1, 0, MouseFlags.Button1Pressed));
		// Drag FAR below the label's visible bottom → genuinely past the bottom edge.
		((IMouseAwareControl)label).ProcessMouseEvent(Mouse(5, 500, MouseFlags.Button1Dragged));

		int dragY = t.LastDragRelativeY;
		int vpRows = t.ViewportHeightRows;
		_out.WriteLine($"PAST-EDGE drag: LastDragRelativeY={dragY}, ViewportHeightRows={vpRows}");

		Assert.True(t.IsDragSelecting, "drag must register");

		double carry = 0;
		int step = SharpConsoleUI.Helpers.DragAutoScroll.ComputeStep(dragY, vpRows, elapsedMs: 1000, ref carry);
		_out.WriteLine($"ComputeStep(dragY={dragY}, vpRows={vpRows}, 1000ms) = {step} (expected > 0 for past-edge)");

		Assert.True(step > 0, $"a genuinely past-edge drag must still autoscroll down (step={step}).");
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	// Regression: drag-select down past the viewport did NOT autoscroll on a SLOW drag (worked on a fast drag,
	// worked upward). Root cause: the horizontal scrollbar occupies the row at the viewport bottom
	// (y == Margin.Top + _effectiveViewportHeight). A slow downward drag-select steps ONTO that row; the SGR
	// mouse format re-sends Button1Pressed for each motion event, so the scrollbar-interaction block mistook it
	// for a scrollbar thumb-press and flipped _isHorizontalScrollbarDragging on — hijacking every subsequent
	// event into horizontal scrolling. The drag Y froze at the last in-viewport row so drag-autoscroll never
	// fired. A fast drag jumped OVER the scrollbar row and worked. The fix excludes scrollbar interaction and
	// the scrollbar-area consume while a text drag-selection is in progress (_isDragging).
	public class MultilineEditDragScrollbarRowTests
	{
		private static MouseEventArgs Drag(int x, int y) => new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Pressed, MouseFlags.Button1Dragged, MouseFlags.ReportMousePosition },
			new Point(x, y), new Point(x, y), new Point(x, y));

		[Fact]
		public void SlowDrag_ThroughHorizontalScrollbarRow_KeepsAdvancingDragY()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem();
			var window = new Window(system);

			// Long lines (no wrap) force a horizontal scrollbar; many lines allow vertical scrolling.
			var text = string.Join("\n",
				Enumerable.Range(0, 200).Select(i => $"line {i} " + new string('x', 200)));
			var control = new MultilineEditControl { WrapMode = WrapMode.NoWrap, Content = text };
			window.AddControl(control);
			system.WindowStateService.AddWindow(window);
			system.WindowStateService.SetActiveWindow(window);
			window.FocusManager.SetFocus(control, FocusReason.Programmatic);
			system.Render.UpdateDisplay();

			var target = (IDragAutoScrollTarget)control;
			int vh = target.ViewportHeightRows;
			Assert.True(vh > 2, $"viewport must have real height (was {vh}).");

			// Anchor the drag-selection near the top of the viewport.
			control.ProcessMouseEvent(new MouseEventArgs(
				new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(2, 1), new Point(2, 1), new Point(2, 1)));
			Assert.True(target.IsDragSelecting, "press must start a drag-selection.");

			// Slow drag: step down ONE ROW at a time, THROUGH the horizontal-scrollbar row (at the viewport
			// bottom) and PAST it. Each step must keep advancing LastDragRelativeY — the scrollbar row must not
			// hijack the drag into horizontal scrolling and freeze the drag Y.
			int lastSeen = target.LastDragRelativeY;
			for (int y = 2; y <= vh + 3; y++)
			{
				control.ProcessMouseEvent(Drag(2, y));
				int now = target.LastDragRelativeY;
				Assert.True(now >= lastSeen, $"drag Y regressed at row {y}: {lastSeen} -> {now}.");
				lastSeen = now;
			}

			// After dragging past the viewport bottom, LastDragRelativeY reflects a past-edge row (> vh - 1), so
			// DragAutoScroll.ComputeStep produces a downward step. Before the fix it froze at the scrollbar row.
			Assert.True(target.LastDragRelativeY > vh - 1,
				$"drag Y must exceed the last visible row (vh-1={vh - 1}) after dragging past the bottom, but was {target.LastDragRelativeY}.");

			double carry = 0;
			int step = DragAutoScroll.ComputeStep(target.LastDragRelativeY, vh, elapsedMs: 1000, ref carry);
			Assert.True(step > 0, $"a past-bottom drag must yield a positive (downward) autoscroll step, got {step}.");
		}
	}
}

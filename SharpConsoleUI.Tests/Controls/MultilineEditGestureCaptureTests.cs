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
using System.Reflection;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	// Conversion regression net for routing MultilineEditControl mouse handling through
	// MouseGestureCapture. The bug class: SGR re-sends Button1Pressed for every motion-while-held
	// event, and the old handler re-hit-tested each event, so a gesture that started in one region
	// leaked into another when the pointer crossed it (a text drag freezing on the scrollbar row /
	// toggling a breakpoint in the gutter; a thumb-drag dying when the pointer left the track). The
	// capture model glues a gesture to the region it started in until release.
	public class MultilineEditGestureCaptureTests
	{
		private const BindingFlags Npi = BindingFlags.NonPublic | BindingFlags.Instance;

		private static MouseEventArgs Press(int x, int y) => new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(x, y), new Point(x, y), new Point(x, y));

		private static MouseEventArgs Drag(int x, int y) => new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Pressed, MouseFlags.Button1Dragged, MouseFlags.ReportMousePosition },
			new Point(x, y), new Point(x, y), new Point(x, y));

		private static MouseEventArgs Release(int x, int y) => new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Released }, new Point(x, y), new Point(x, y), new Point(x, y));

		private static (ConsoleWindowSystem system, Window window, MultilineEditControl control) Build()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem();
			var window = new Window(system);

			// Long no-wrap lines force a horizontal scrollbar; many lines force a vertical scrollbar.
			var text = string.Join("\n",
				Enumerable.Range(0, 200).Select(i => $"line {i} " + new string('x', 200)));
			var control = new MultilineEditControl { WrapMode = WrapMode.NoWrap, Content = text };
			control.AddGutterRenderer(new LineNumberGutterRenderer());

			window.AddControl(control);
			system.WindowStateService.AddWindow(window);
			system.WindowStateService.SetActiveWindow(window);
			window.FocusManager.SetFocus(control, FocusReason.Programmatic);
			system.Render.UpdateDisplay();

			return (system, window, control);
		}

		// The V-scrollbar column = Margin.Left + GutterWidth + _effectiveWidth (see IsOnVerticalScrollbar).
		private static int VScrollbarX(MultilineEditControl c) =>
			c.Margin.Left + c.GutterWidth + GetInt(c, "_effectiveWidth");

		// The vertical track height == effective viewport; arrow-down is at relY == trackHeight - 1 (Margin.Top rel).
		private static int VTrackHeight(MultilineEditControl c) => GetInt(c, "_effectiveViewportHeight");

		[Fact]
		public void VScrollThumbDrag_PointerLeavesTrack_KeepsScrolling()
		{
			var (system, _, control) = Build();
			int gutterWidth = control.GutterWidth;
			Assert.True(gutterWidth > 0, "test requires a visible gutter.");

			int sbX = VScrollbarX(control);
			int trackHeight = VTrackHeight(control);
			Assert.True(trackHeight > 3, $"viewport/track must have real height (was {trackHeight}).");

			// Thumb sits at the top of the track (offset 0) — press a row inside the thumb (row 1, since
			// arrow-up is row 0). Then drag DOWN with X moved OFF the scrollbar column, deep into text.
			int thumbRow = control.Margin.Top + 1;

			int caretYBefore = GetInt(control, "_cursorY");
			control.ProcessMouseEvent(Press(sbX, thumbRow));
			int vOffsetAtPress = GetInt(control, "_verticalScrollOffset");

			int textX = control.Margin.Left + gutterWidth + 3;
			for (int y = thumbRow + 1; y <= thumbRow + 6; y++)
				control.ProcessMouseEvent(Drag(textX, y)); // X far off the scrollbar column

			int vOffsetAfter = GetInt(control, "_verticalScrollOffset");
			Assert.True(vOffsetAfter > vOffsetAtPress,
				$"thumb-drag leaving the track must keep scrolling: offset {vOffsetAtPress} -> {vOffsetAfter}.");

			// The caret must NOT have been moved by the drag (it would have if the drag leaked into text).
			Assert.Equal(caretYBefore, GetInt(control, "_cursorY"));
			Assert.False(control.HasSelection, "a scrollbar thumb-drag must not create a text selection.");
		}

		[Fact]
		public void TextDragSelect_OverScrollbarAndGutter_KeepsSelecting()
		{
			var (system, _, control) = Build();
			int gutterWidth = control.GutterWidth;
			Assert.True(gutterWidth > 0, "test requires a visible gutter.");
			int gutterClicks = 0;
			control.GutterClick += (_, _) => gutterClicks++;

			var target = (IDragAutoScrollTarget)control;
			int vh = target.ViewportHeightRows;
			Assert.True(vh > 3, $"viewport must have real height (was {vh}).");

			int textX = control.Margin.Left + gutterWidth + 4;

			// Anchor a text drag-selection near the top.
			control.ProcessMouseEvent(Press(textX, control.Margin.Top + 1));
			Assert.True(target.IsDragSelecting, "press in text must start a drag-selection.");

			int lastY = target.LastDragRelativeY;

			// Drag DOWN through the horizontal-scrollbar row (at the viewport bottom) and past it, swinging
			// LEFT into the gutter column (x = 0) on alternating steps. The selection must keep extending; the
			// drag Y must keep advancing; no GutterClick may fire.
			for (int y = control.Margin.Top + 2; y <= control.Margin.Top + vh + 3; y++)
			{
				int x = (y % 2 == 0) ? 0 : textX;
				control.ProcessMouseEvent(Drag(x, y));
				int now = target.LastDragRelativeY;
				Assert.True(now >= lastY, $"drag Y regressed at row {y}: {lastY} -> {now}.");
				lastY = now;
			}

			Assert.True(target.IsDragSelecting, "crossing the scrollbar row / gutter must not end the drag-selection.");
			Assert.True(control.HasSelection, "the selection must still be active after crossing regions.");
			Assert.Equal(0, gutterClicks);
			Assert.True(target.LastDragRelativeY > control.Margin.Top + vh - 1,
				$"drag Y must exceed the last visible row after dragging past the bottom, was {target.LastDragRelativeY}.");
		}

		[Fact]
		public void FreshClicks_StillWork()
		{
			var (system, _, control) = Build();
			int gutterWidth = control.GutterWidth;
			Assert.True(gutterWidth > 0, "test requires a visible gutter.");
			int gutterClicks = 0;
			control.GutterClick += (_, _) => gutterClicks++;

			var target = (IDragAutoScrollTarget)control;

			// (a) A fresh gutter press fires GutterClick and does NOT start a text drag.
			control.ProcessMouseEvent(Press(0, control.Margin.Top + 3));
			control.ProcessMouseEvent(Release(0, control.Margin.Top + 3));
			Assert.Equal(1, gutterClicks);
			Assert.False(target.IsDragSelecting, "a gutter click must not start a text drag-selection.");

			// (b) A fresh V-scrollbar arrow-down press scrolls (using exact geometry).
			int sbX = VScrollbarX(control);
			int arrowDownY = control.Margin.Top + VTrackHeight(control) - 1;
			int before = GetInt(control, "_verticalScrollOffset");
			control.ProcessMouseEvent(Press(sbX, arrowDownY));
			control.ProcessMouseEvent(Release(sbX, arrowDownY));
			int after = GetInt(control, "_verticalScrollOffset");
			Assert.True(after > before, $"a fresh V-scrollbar arrow-down press must scroll: {before} -> {after}.");
			Assert.False(target.IsDragSelecting, "a scrollbar click must not start a text drag-selection.");

			// (c) A fresh text press places the caret / anchors a selection.
			int textX = control.Margin.Left + gutterWidth + 5;
			control.ProcessMouseEvent(Press(textX, control.Margin.Top + 2));
			Assert.True(target.IsDragSelecting, "a fresh text press must anchor a selection.");
		}

		private static int GetInt(MultilineEditControl c, string field) =>
			(int)typeof(MultilineEditControl).GetField(field, Npi)!.GetValue(c)!;
	}
}

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
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	// Regression: a text drag-selection that crosses into the gutter (e.g. the breakpoint column) fired
	// GutterClick — toggling a breakpoint mid-drag — because SGR re-sends Button1Pressed for every motion event
	// and the gutter code treated the resent press as a fresh gutter click. Same "drag mistaken for a click"
	// class as the scrollbar-row hijack. The fix: fire GutterClick only on a fresh press (_isDragging == false),
	// and let an active drag-selection cross the gutter without being consumed.
	public class MultilineEditDragAcrossGutterTests
	{
		private static MouseEventArgs Press(int x, int y) => new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(x, y), new Point(x, y), new Point(x, y));

		private static MouseEventArgs Drag(int x, int y) => new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Pressed, MouseFlags.Button1Dragged, MouseFlags.ReportMousePosition },
			new Point(x, y), new Point(x, y), new Point(x, y));

		[Fact]
		public void DragSelect_CrossingGutter_DoesNotFireGutterClick_AndKeepsSelecting()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem();
			var window = new Window(system);
			var control = new MultilineEditControl
			{
				WrapMode = WrapMode.NoWrap,
				Content = string.Join("\n", Enumerable.Range(0, 30).Select(i => $"line {i} content here"))
			};
			int gutterClicks = 0;
			control.AddGutterRenderer(new LineNumberGutterRenderer());
			control.GutterClick += (_, _) => gutterClicks++;

			window.AddControl(control);
			system.WindowStateService.AddWindow(window);
			system.WindowStateService.SetActiveWindow(window);
			window.FocusManager.SetFocus(control, FocusReason.Programmatic);
			system.Render.UpdateDisplay();

			int gutterWidth = control.GutterWidth;
			Assert.True(gutterWidth > 0, "test requires a visible gutter.");
			int textX = gutterWidth + 4; // clearly in the text area

			// Start a text drag-selection in the TEXT area, a couple rows down.
			control.ProcessMouseEvent(Press(textX, 2));
			control.ProcessMouseEvent(Drag(textX, 3));
			Assert.True(control.HasSelection, "drag in text must start a selection.");

			// Drag LEFT across the gutter (x = 0, the breakpoint column) while the selection is active.
			control.ProcessMouseEvent(Drag(0, 3));

			// It must NOT fire a gutter click (no breakpoint toggled) mid-drag...
			Assert.Equal(0, gutterClicks);
			// ...and the selection must still be active (the drag was not silently ended).
			Assert.True(control.HasSelection, "crossing the gutter must not end the drag-selection.");
		}

		[Fact]
		public void FreshGutterClick_StillFires()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem();
			var window = new Window(system);
			var control = new MultilineEditControl
			{
				WrapMode = WrapMode.NoWrap,
				Content = string.Join("\n", Enumerable.Range(0, 30).Select(i => $"line {i} content here"))
			};
			int gutterClicks = 0;
			control.AddGutterRenderer(new LineNumberGutterRenderer());
			control.GutterClick += (_, _) => gutterClicks++;

			window.AddControl(control);
			system.WindowStateService.AddWindow(window);
			system.WindowStateService.SetActiveWindow(window);
			window.FocusManager.SetFocus(control, FocusReason.Programmatic);
			system.Render.UpdateDisplay();

			Assert.True(control.GutterWidth > 0, "test requires a visible gutter.");

			// A FRESH press in the gutter (no drag in progress) must still fire GutterClick.
			control.ProcessMouseEvent(Press(0, 2));
			Assert.Equal(1, gutterClicks);
		}
	}
}

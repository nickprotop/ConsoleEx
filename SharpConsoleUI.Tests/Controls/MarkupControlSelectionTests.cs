// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

public class MarkupControlSelectionTests
{
	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var p = new System.Drawing.Point(x, y);
		return new MouseEventArgs(flags.ToList(), p, p, p);
	}

	/// <summary>Paints the control at origin so the selection cache uses test-space coordinates.</summary>
	private static void Paint(MarkupControl control, int width = 40, int height = 10)
	{
		var buffer = new CharacterBuffer(width + 5, height + 5);
		var bounds = new LayoutRect(0, 0, width, height);
		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	/// <summary>
	/// Paints the control at a non-zero buffer offset (as in a real window). Mouse coordinates
	/// delivered to ProcessMouseEvent are control-relative regardless of where the control is
	/// painted, so this exercises the relative-coordinate hit-testing.
	/// </summary>
	private static void PaintAt(MarkupControl control, int boundsX, int boundsY, int width = 40, int height = 10)
	{
		var buffer = new CharacterBuffer(boundsX + width + 5, boundsY + height + 5);
		var bounds = new LayoutRect(boundsX, boundsY, width, height);
		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	[Fact]
	public void Default_SelectionDisabled_DragDoesNotSelect()
	{
		var control = new MarkupControl(new List<string> { "Hello World" });
		Paint(control);

		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(5, 0, MouseFlags.Button1Dragged));

		Assert.False(control.HasSelection);
		Assert.Equal(string.Empty, control.GetSelectedText());
	}

	[Fact]
	public void EnabledSelection_DragSelectsPlainText_StrippingMarkup()
	{
		var control = new MarkupControl(new List<string> { "[red]Hello[/] World" }) { EnableSelection = true };
		Paint(control);

		// "Hello World" = 11 visible columns. Drag from col 0 to end.
		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(11, 0, MouseFlags.Button1Dragged));
		control.ProcessMouseEvent(Mouse(11, 0, MouseFlags.Button1Released));

		Assert.True(control.HasSelection);
		Assert.Equal("Hello World", control.GetSelectedText());
	}

	[Fact]
	public void EnabledSelection_WrappedLogicalLine_CopiesAsSingleLine()
	{
		// One logical line longer than the render width → wraps across rows.
		var control = new MarkupControl(new List<string> { "alpha beta gamma delta epsilon" })
		{
			EnableSelection = true,
			Wrap = true
		};
		Paint(control, width: 12, height: 10);

		// Select everything by spanning many rows/cols.
		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(11, 9, MouseFlags.Button1Dragged));
		control.ProcessMouseEvent(Mouse(11, 9, MouseFlags.Button1Released));

		var text = control.GetSelectedText();
		Assert.True(control.HasSelection);
		// Soft-wrap newlines suppressed: a single logical line yields no embedded newline.
		Assert.DoesNotContain("\n", text);
		Assert.Contains("alpha", text);
		Assert.Contains("epsilon", text);
	}

	[Fact]
	public void EnabledSelection_RegistersWithWindowSelectionManager()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var control = new MarkupControl(new List<string> { "Hello World" }) { EnableSelection = true };
		window.AddControl(control);
		Paint(control);

		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(5, 0, MouseFlags.Button1Dragged));

		Assert.True(control.HasSelection);
		Assert.Same(control, window.SelectionManager.ActiveSelection);
	}

	[Fact]
	public void SecondControlSelection_ClearsFirstControl()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var a = new MarkupControl(new List<string> { "AAAA BBBB" }) { EnableSelection = true };
		var b = new MarkupControl(new List<string> { "CCCC DDDD" }) { EnableSelection = true };
		window.AddControl(a);
		window.AddControl(b);
		Paint(a);
		Paint(b);

		a.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		a.ProcessMouseEvent(Mouse(4, 0, MouseFlags.Button1Dragged));
		Assert.True(a.HasSelection);

		b.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		b.ProcessMouseEvent(Mouse(4, 0, MouseFlags.Button1Dragged));

		Assert.True(b.HasSelection);
		Assert.False(a.HasSelection); // single active selection per window
		Assert.Same(b, window.SelectionManager.ActiveSelection);
	}

	[Fact]
	public void RightClick_SurfacedAndDoesNotChangeSelection()
	{
		var control = new MarkupControl(new List<string> { "Hello World" }) { EnableSelection = true };
		Paint(control);
		bool rightClicked = false;
		control.MouseRightClick += (_, _) => rightClicked = true;

		// Make a selection first.
		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(5, 0, MouseFlags.Button1Dragged));
		Assert.True(control.HasSelection);

		control.ProcessMouseEvent(Mouse(2, 0, MouseFlags.Button3Clicked));

		Assert.True(rightClicked);
		Assert.True(control.HasSelection); // unchanged
	}

	[Fact]
	public void EnabledSelection_HighlightsSelectedCells()
	{
		var control = new MarkupControl(new List<string> { "Hello World" })
		{
			EnableSelection = true,
			SelectionBackgroundColor = new Color(10, 20, 30)
		};
		Paint(control);

		// Select first 5 columns.
		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(5, 0, MouseFlags.Button1Dragged));

		// Re-paint to apply highlight, then inspect the buffer.
		var buffer = new CharacterBuffer(45, 15);
		var bounds = new LayoutRect(0, 0, 40, 10);
		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		var selectedCell = buffer.GetCell(0, 0);
		Assert.Equal(new Color(10, 20, 30), selectedCell.Background);
	}

	[Fact]
	public void DisablingSelection_ClearsExistingSelection()
	{
		var control = new MarkupControl(new List<string> { "Hello World" }) { EnableSelection = true };
		Paint(control);
		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(5, 0, MouseFlags.Button1Dragged));
		Assert.True(control.HasSelection);

		control.EnableSelection = false;

		Assert.False(control.HasSelection);
	}

	[Fact]
	public void MultiLine_DragExtendsAcrossLines()
	{
		var control = new MarkupControl(new List<string> { "line one", "line two", "line three" })
		{
			EnableSelection = true,
			Wrap = false
		};
		Paint(control);

		// Drag from row 0 to the end of row 2 (control-relative coords; content top-left = (0,0)).
		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(20, 2, MouseFlags.Button1Dragged));
		control.ProcessMouseEvent(Mouse(20, 2, MouseFlags.Button1Released));

		Assert.True(control.HasSelection);
		var text = control.GetSelectedText();
		Assert.Contains("line one", text);
		Assert.Contains("line two", text);
		Assert.Contains("line three", text);
		Assert.Equal(2, text.Count(c => c == '\n')); // three logical lines → two newlines
	}

	[Fact]
	public void MultiLine_DragExtendsAcrossLines_WhenPaintedAtOffset()
	{
		// Regression for issue: selection always stayed on the first line when the control
		// was not painted at buffer origin (cache stored absolute origins vs. relative mouse coords).
		var control = new MarkupControl(new List<string> { "alpha", "bravo", "charlie", "delta" })
		{
			EnableSelection = true,
			Wrap = false
		};
		PaintAt(control, boundsX: 7, boundsY: 5);

		// Mouse coordinates are control-relative even though the control is painted at (7,5).
		// Drag from row 0 down to the end of row 3.
		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(20, 3, MouseFlags.Button1Dragged));
		control.ProcessMouseEvent(Mouse(20, 3, MouseFlags.Button1Released));

		Assert.True(control.HasSelection);
		var text = control.GetSelectedText();
		Assert.Contains("alpha", text);
		Assert.Contains("delta", text);
		Assert.Equal(3, text.Count(c => c == '\n'));
	}

	[Fact]
	public void DragToSecondLineOnly_SelectsFromFirstThroughSecond()
	{
		var control = new MarkupControl(new List<string> { "first row", "second row", "third row" })
		{
			EnableSelection = true,
			Wrap = false
		};
		PaintAt(control, boundsX: 3, boundsY: 4);

		// Anchor on row 1 col 0, extend to row 2 col 4 — must span rows, not collapse to row 0.
		control.ProcessMouseEvent(Mouse(0, 1, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(4, 2, MouseFlags.Button1Dragged));

		Assert.True(control.HasSelection);
		var text = control.GetSelectedText();
		var lines = text.Split('\n');
		Assert.Equal(2, lines.Length);                       // spans exactly rows 1 and 2
		Assert.Contains("second row", lines[0]);             // selection begins on the 2nd line (not row 0)
		Assert.Contains("thir", lines[1]);                   // and extends into the 3rd line
	}
}

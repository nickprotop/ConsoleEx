// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests;

/// <summary>
/// Verifies TableControl mouse handling routes Button1 gestures through MouseGestureCapture: a column-resize
/// drag whose pointer wanders off the original border (even past the next column) keeps resizing the ORIGINAL
/// column rather than re-hit-testing into a different border or the cells area, and fresh clicks still work.
/// </summary>
public class TableControlGestureCaptureTests
{
	private const int BufW = 80;
	private const int BufH = 12;

	// Build an interactive, column-resizable in-memory table with three fixed-width columns so the
	// column borders sit at deterministic X positions. Painted once so RenderedX/RenderedWidth are set.
	private static TableControl BuildResizableTable()
	{
		var table = new TableControl
		{
			ColumnResizeEnabled = true,
			ReadOnly = false,
			ShowHeader = true
		};
		table.AddColumn(new TableColumn("A", TextJustification.Left, 10));
		table.AddColumn(new TableColumn("B", TextJustification.Left, 10));
		table.AddColumn(new TableColumn("C", TextJustification.Left, 10));
		table.AddRow("a1", "b1", "c1");
		table.AddRow("a2", "b2", "c2");
		Paint(table);
		return table;
	}

	private static void Paint(TableControl table)
	{
		var buffer = new CharacterBuffer(BufW, BufH);
		var bounds = new LayoutRect(0, 0, BufW, BufH);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var p = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags>(flags), p, p, p);
	}

	// The X of the right border of the given column (where a resize press lands).
	private static int ColumnBorderX(TableControl table, int col)
		=> table.Columns[col].RenderedX + table.Columns[col].RenderedWidth;

	[Fact]
	public void ColumnResizeDrag_PointerPastNextColumn_KeepsResizingOriginalColumn()
	{
		var table = BuildResizableTable();

		int col0Border = ColumnBorderX(table, 0);
		int col1Border = ColumnBorderX(table, 1);
		int col0StartWidth = table.Columns[0].RenderedWidth;
		int col1StartWidth = table.Columns[1].RenderedWidth;

		// Sanity: the two borders are distinct so a wandering pointer really could re-hit-test.
		Assert.True(col1Border > col0Border + 2);

		int headerY = table.HeaderRowYForTest();

		// Down on column 0's right border -> capture ColumnResize on column 0.
		table.ProcessMouseEvent(Mouse(col0Border, headerY, MouseFlags.Button1Pressed));

		// Drag the pointer FAR to the right, PAST column 1's border. Under the old re-hit-testing model
		// this would resolve to column 1's border (or fall into the cells area); under capture it must
		// stay glued to column 0.
		int wanderX = col1Border + 6;
		table.ProcessMouseEvent(Mouse(wanderX, headerY, MouseFlags.Button1Pressed, MouseFlags.Button1Dragged));

		// Column 0 must have grown (drag delta was positive); column 1 must be untouched.
		Assert.True(table.Columns[0].Width.GetValueOrDefault() > col0StartWidth,
			$"Column 0 width should grow from {col0StartWidth}, was {table.Columns[0].Width}");
		Assert.Equal(col1StartWidth, table.Columns[1].Width.GetValueOrDefault());

		// Release ends the gesture.
		table.ProcessMouseEvent(Mouse(wanderX, headerY, MouseFlags.Button1Released));
	}

	[Fact]
	public void ColumnResizeDrag_ThenReleased_NextResizeStartsFresh()
	{
		var table = BuildResizableTable();
		int headerY = table.HeaderRowYForTest();

		int col0Border = ColumnBorderX(table, 0);
		table.ProcessMouseEvent(Mouse(col0Border, headerY, MouseFlags.Button1Pressed));
		table.ProcessMouseEvent(Mouse(col0Border + 5, headerY, MouseFlags.Button1Pressed, MouseFlags.Button1Dragged));
		table.ProcessMouseEvent(Mouse(col0Border + 5, headerY, MouseFlags.Button1Released));

		int col0AfterFirst = table.Columns[0].Width.GetValueOrDefault();

		// Re-paint so borders reflect the new width, then a fresh press on column 1's border resizes col 1,
		// not col 0 (capture was released).
		Paint(table);
		int col1Border = ColumnBorderX(table, 1);
		int col1StartWidth = table.Columns[1].RenderedWidth;

		table.ProcessMouseEvent(Mouse(col1Border, headerY, MouseFlags.Button1Pressed));
		table.ProcessMouseEvent(Mouse(col1Border + 4, headerY, MouseFlags.Button1Pressed, MouseFlags.Button1Dragged));
		table.ProcessMouseEvent(Mouse(col1Border + 4, headerY, MouseFlags.Button1Released));

		Assert.True(table.Columns[1].Width.GetValueOrDefault() > col1StartWidth);
		// Column 0 was not touched by the second gesture.
		Assert.Equal(col0AfterFirst, table.Columns[0].Width.GetValueOrDefault());
	}

	[Fact]
	public void FreshHeaderClick_StillRaisesHeaderClicked()
	{
		var table = BuildResizableTable();
		table.SortingEnabled = true;
		int? clickedCol = null;
		table.HeaderClicked += (_, col) => clickedCol = col;

		// Click near the middle of the first column's header (away from any border).
		int midCol0 = table.Columns[0].RenderedX + table.Columns[0].RenderedWidth / 2;
		table.ProcessMouseEvent(Mouse(midCol0, table.HeaderRowYForTest(), MouseFlags.Button1Clicked));

		Assert.Equal(0, clickedCol);
	}

	[Fact]
	public void FreshCellClick_SelectsRow()
	{
		var table = BuildResizableTable();

		// A press then click on a data row selects it. Row 0 sits just below the header/separator; use a Y
		// resolved via the control's own row hit-testing by clicking a couple of rows down and asserting a
		// valid selection resulted.
		int cellX = table.Columns[0].RenderedX + 1;
		int rowY = table.HeaderRowYForTest() + 2;

		table.ProcessMouseEvent(Mouse(cellX, rowY, MouseFlags.Button1Pressed));
		table.ProcessMouseEvent(Mouse(cellX, rowY, MouseFlags.Button1Clicked));

		Assert.True(table.SelectedRowIndex >= 0);
	}
}

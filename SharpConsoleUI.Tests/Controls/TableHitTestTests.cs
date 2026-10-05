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
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Where on a <see cref="TableControl"/> the pointer is: which column and row a position falls on,
/// with or without a header, scrolled horizontally or not, and which presses start a column resize.
/// </summary>
public class TableHitTestTests
{
	#region Helpers

	private const int BufferWidth = 40;
	private const int BufferHeight = 10;

	/// <summary>
	/// A borderless interactive table with three five-cell columns, so column <c>c</c> starts at
	/// x = 5·c with no separators to account for.
	/// </summary>
	private static TableControl FixedColumnTable(bool showHeader = true)
	{
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ReadOnly = false,
			ShowHeader = showHeader,
			CellNavigationEnabled = true,
		};
		table.AddColumn("A", TextJustification.Left, 5);
		table.AddColumn("B", TextJustification.Left, 5);
		table.AddColumn("C", TextJustification.Left, 5);
		table.AddRow("a1", "b1", "c1");
		table.AddRow("a2", "b2", "c2");
		return table;
	}

	private static void Paint(TableControl table, int width = BufferWidth)
	{
		var buffer = new CharacterBuffer(width, BufferHeight);
		var bounds = new LayoutRect(0, 0, width, BufferHeight);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var point = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags>(flags), point, point, point);
	}

	#endregion

	#region Columns are found without a header

	[Fact]
	public void WithoutAHeader_ClickingACell_SelectsItsColumn()
	{
		var table = FixedColumnTable(showHeader: false);
		Paint(table);

		table.ProcessMouseEvent(Mouse(7, table.RowYForTest(1), MouseFlags.Button1Clicked));

		Assert.Equal(1, table.SelectedRowIndex);
		Assert.Equal(1, table.SelectedColumnIndex);
	}

	[Fact]
	public void WithoutAHeader_TheColumnAtAPosition_IsStillKnown()
	{
		var table = FixedColumnTable(showHeader: false);
		Paint(table);

		Assert.Equal(2, table.GetColumnIndexAt(11));
	}

	[Fact]
	public void WithAHeader_TheColumnAtAPosition_IsKnown()
	{
		var table = FixedColumnTable();
		Paint(table);

		Assert.Equal(0, table.GetColumnIndexAt(0));
		Assert.Equal(1, table.GetColumnIndexAt(5));
		Assert.Equal(-1, table.GetColumnIndexAt(15));
	}

	#endregion

	#region Hit testing follows the horizontal scroll

	/// <summary>
	/// A borderless table of five auto-width columns whose ten-cell floors keep them ten wide,
	/// painted 20 cells wide and scrolled 12 cells to the right: column 1 (logical 10..19) starts on
	/// screen at x = -2, column 2 at x = 8.
	/// </summary>
	/// <remarks>
	/// Floors, not fixed widths, because fixed widths that do not fit are shrunk proportionally and
	/// never scroll; only a floor lets the columns overflow the viewport.
	/// </remarks>
	private static TableControl ScrolledTable(bool checkboxes = false, bool resizable = false)
	{
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ReadOnly = false,
			CellNavigationEnabled = true,
			CheckboxMode = checkboxes,
			ColumnResizeEnabled = resizable,
		};
		for (int c = 0; c < 5; c++)
			table.AddColumn(new TableColumn($"H{c}") { MinWidth = 10 });
		for (int r = 0; r < 2; r++)
			table.AddRow(Enumerable.Range(0, 5).Select(c => $"r{r}c{c}".PadRight(10, '.')).ToArray());
		Paint(table, width: 20);
		table.HorizontalScrollOffset = 12;
		Paint(table, width: 20);
		return table;
	}

	[Fact]
	public void WhileScrolled_TheColumnAtAPosition_IsTheOneDrawnThere()
	{
		var table = ScrolledTable();

		Assert.Equal(1, table.GetColumnIndexAt(0));
		Assert.Equal(1, table.GetColumnIndexAt(7));
		Assert.Equal(2, table.GetColumnIndexAt(8));
	}

	[Fact]
	public void WhileScrolled_ClickingACell_SelectsTheColumnDrawnThere()
	{
		var table = ScrolledTable();

		table.ProcessMouseEvent(Mouse(9, table.RowYForTest(0), MouseFlags.Button1Clicked));

		Assert.Equal(2, table.SelectedColumnIndex);
	}

	[Fact]
	public void WhileScrolled_OnlyTheCheckboxCellsStillOnScreen_ToggleACheckbox()
	{
		// The four checkbox cells are logical 0..3; scrolled by 2, only x = 0..1 still show them,
		// and x = 2 is the first data column.
		var table = ScrolledTable(checkboxes: true);
		table.HorizontalScrollOffset = 2;
		Paint(table, width: 20);
		table.ToggleRowSelection(1);

		table.ProcessMouseEvent(Mouse(2, table.RowYForTest(0), MouseFlags.Button1Clicked));

		Assert.False(table.GetRow(1).IsChecked, "a plain click outside the checkbox clears the other checks");
		Assert.True(table.GetRow(0).IsChecked);
	}

	[Fact]
	public void WhileScrolled_PressingAColumnBorder_ResizesThatColumn()
	{
		var table = ScrolledTable(resizable: true);
		int header = table.HeaderRowYForTest();
		int borderOfColumn1 = 8;                       // logical 20, minus the scroll of 12

		table.ProcessMouseEvent(Mouse(borderOfColumn1, header, MouseFlags.Button1Pressed));
		table.ProcessMouseEvent(Mouse(borderOfColumn1 + 3, header, MouseFlags.Button1Pressed, MouseFlags.Button1Dragged));
		table.ProcessMouseEvent(Mouse(borderOfColumn1 + 3, header, MouseFlags.Button1Released));

		Assert.Equal(13, table.Columns[1].Width);
	}

	#endregion

	#region Columns are resized from the header only

	[Fact]
	public void PressingAColumnBorderOnADataRow_SelectsTheRowInsteadOfResizing()
	{
		var table = FixedColumnTable();
		table.ColumnResizeEnabled = true;
		Paint(table);
		int row = table.RowYForTest(1);

		table.ProcessMouseEvent(Mouse(5, row, MouseFlags.Button1Pressed));
		table.ProcessMouseEvent(Mouse(8, row, MouseFlags.Button1Pressed, MouseFlags.Button1Dragged));
		table.ProcessMouseEvent(Mouse(8, row, MouseFlags.Button1Released));

		Assert.Equal(5, table.Columns[0].Width);
		Assert.Equal(1, table.SelectedRowIndex);
	}

	[Fact]
	public void PressingAColumnBorderOnTheHeader_ResizesTheColumn()
	{
		var table = FixedColumnTable();
		table.ColumnResizeEnabled = true;
		Paint(table);
		int header = table.HeaderRowYForTest();

		table.ProcessMouseEvent(Mouse(5, header, MouseFlags.Button1Pressed));
		table.ProcessMouseEvent(Mouse(8, header, MouseFlags.Button1Pressed, MouseFlags.Button1Dragged));
		table.ProcessMouseEvent(Mouse(8, header, MouseFlags.Button1Released));

		Assert.Equal(8, table.Columns[0].Width);
	}

	#endregion
}

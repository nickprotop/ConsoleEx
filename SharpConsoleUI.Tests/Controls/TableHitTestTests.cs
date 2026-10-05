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

	#region HitTest names every part of the table

	/// <summary>
	/// A bordered, titled, filterable table of two six-cell columns and three rows, painted 14 lines
	/// tall: title 0, top border 1, header 2, header separator 3, rows 4..6, padding 7..10, filter
	/// bar 11..12, bottom border 13. Column 0 is drawn at x = 1..6, column 1 at x = 8..13.
	/// </summary>
	private static TableControl ChromeTable()
	{
		var table = new TableControl
		{
			Title = "T",
			ReadOnly = false,
			FilteringEnabled = true,
			SortingEnabled = true,
			Height = 14,
		};
		table.AddColumn("A", TextJustification.Left, 6);
		table.AddColumn("B", TextJustification.Left, 6);
		for (int r = 0; r < 3; r++)
			table.AddRow($"a{r}", $"b{r}");
		var buffer = new CharacterBuffer(BufferWidth, 14);
		var bounds = new LayoutRect(0, 0, BufferWidth, 14);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return table;
	}

	private static void AssertHit(TableHitTestResult hit, TableHitZone zone, int displayRow = -1, int dataRow = -1, int column = -1, int offset = -1)
	{
		Assert.Equal(zone, hit.Zone);
		Assert.Equal(displayRow, hit.DisplayRowIndex);
		Assert.Equal(dataRow, hit.DataRowIndex);
		Assert.Equal(column, hit.ColumnIndex);
		Assert.Equal(offset, hit.CellOffset);
	}

	[Fact]
	public void BeforeItIsPainted_NothingIsHit()
	{
		var table = FixedColumnTable();

		AssertHit(table.HitTest(1, 1), TableHitZone.None);
	}

	[Fact]
	public void TheTitleAndTheHeader_AreNamed_WithTheHeadersColumn()
	{
		var table = ChromeTable();

		AssertHit(table.HitTest(3, 0), TableHitZone.Title);
		AssertHit(table.HitTest(9, 2), TableHitZone.Header, column: 1, offset: 1);
		AssertHit(table.HitTest(7, 2), TableHitZone.Header);
	}

	[Fact]
	public void ACell_IsNamedWithItsRowColumnAndOffset()
	{
		var table = ChromeTable();

		AssertHit(table.HitTest(3, 4), TableHitZone.Cell, displayRow: 0, dataRow: 0, column: 0, offset: 2);
		AssertHit(table.HitTest(9, 5), TableHitZone.Cell, displayRow: 1, dataRow: 1, column: 1, offset: 1);
	}

	[Fact]
	public void UnderASort_ACellNamesBothItsDisplayAndItsDataRow()
	{
		var table = ChromeTable();
		table.SortByColumn(0);
		table.SortByColumn(0);

		AssertHit(table.HitTest(3, 4), TableHitZone.Cell, displayRow: 0, dataRow: 2, column: 0, offset: 2);
	}

	[Fact]
	public void TheBordersOfADataRow_AreTheRowWithoutAColumn()
	{
		var table = ChromeTable();

		AssertHit(table.HitTest(0, 4), TableHitZone.Row, displayRow: 0, dataRow: 0);
		AssertHit(table.HitTest(7, 4), TableHitZone.Row, displayRow: 0, dataRow: 0);
	}

	[Fact]
	public void BelowTheLastRow_IsTheEmptyDataArea()
	{
		var table = ChromeTable();

		AssertHit(table.HitTest(3, 8), TableHitZone.EmptyDataArea);
	}

	[Fact]
	public void TheFilterBar_AndTheLineAboveIt_AreTheFilterBar()
	{
		var table = ChromeTable();

		AssertHit(table.HitTest(3, 11), TableHitZone.FilterBar);
		AssertHit(table.HitTest(3, 12), TableHitZone.FilterBar);
	}

	[Fact]
	public void TheHorizontalBorders_AreNoPart()
	{
		var table = ChromeTable();

		AssertHit(table.HitTest(3, 1), TableHitZone.None);
		AssertHit(table.HitTest(3, 3), TableHitZone.None);
		AssertHit(table.HitTest(3, 13), TableHitZone.None);
	}

	[Fact]
	public void TheCheckbox_IsNamedWithItsRow_AndTheFirstColumnAfterIt()
	{
		var table = FixedColumnTable();
		table.CheckboxMode = true;
		Paint(table);
		int row = table.RowYForTest(1);

		AssertHit(table.HitTest(1, row), TableHitZone.Checkbox, displayRow: 1, dataRow: 1, offset: 1);
		AssertHit(table.HitTest(4, row), TableHitZone.Cell, displayRow: 1, dataRow: 1, column: 0, offset: 0);
	}

	[Fact]
	public void WhileScrolled_TheOffsetCountsFromTheColumnsOwnEdge()
	{
		var table = ScrolledTable();

		AssertHit(table.HitTest(0, table.RowYForTest(0)), TableHitZone.Cell, displayRow: 0, dataRow: 0, column: 1, offset: 2);
	}

	[Fact]
	public void TheVerticalScrollbar_IsNamedBeforeTheRowItOverlaps()
	{
		var table = new TableControl { ReadOnly = false, BorderStyle = BorderStyle.None };
		table.AddColumn("Id");
		for (int i = 0; i < 30; i++)
			table.AddRow(i.ToString());
		Paint(table);

		AssertHit(table.HitTest(BufferWidth - 1, table.RowYForTest(0)), TableHitZone.VerticalScrollbar);
	}

	[Fact]
	public void TheHorizontalScrollbar_IsNamed()
	{
		var table = ScrolledTable();
		table.HorizontalScrollbarVisibility = ScrollbarVisibility.Always;
		Paint(table, width: 20);

		AssertHit(table.HitTest(3, BufferHeight - 1), TableHitZone.HorizontalScrollbar);
	}

	#endregion
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// What a wrapping table reports as its height, and how much it measures to say so.
/// </summary>
/// <remarks>
/// A table that reports a row count as its height leaves a hosting panel unable to
/// scroll to the end of it, because the panel reserves one line per row and the
/// table draws four. The counting test is the other half: wrapping every row of a
/// ten-thousand-row source on each frame would be worse than the clipping it fixes.
/// </remarks>
public class TableWrapMeasureTests
{
	private const string LongTitle =
		"The quick brown fox jumps over the lazy dog and keeps running onward";

	/// <summary>Counts how often a cell value is read, so measuring can be observed.</summary>
	private sealed class CountingSource : ITableDataSource
	{
		private readonly int _rows;
		internal int CellReads;

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		internal CountingSource(int rows) => _rows = rows;

		public int RowCount => _rows;

		public int ColumnCount => 2;

		public string GetColumnHeader(int c) => c == 0 ? "ID" : "Title";

		public string GetCellValue(int r, int c)
		{
			CellReads++;
			return c == 0 ? r.ToString() : LongTitle;
		}

		public Color? GetRowBackgroundColor(int r) => null;

		public Color? GetRowForegroundColor(int r) => null;

		public bool IsRowEnabled(int r) => true;

		public TextJustification GetColumnAlignment(int c) => TextJustification.Left;

		public int? GetColumnWidth(int c) => c == 0 ? 4 : 20;

		public object? GetRowTag(int r) => null;
	}

	private static TableControl Build(bool wrap, int rows = 3, int? height = 10)
	{
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ShowHeader = false,
		};
		if (height.HasValue) table.Height = height;
		table.AddColumn(new TableColumn("ID", TextJustification.Left, 4));
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = wrap });
		for (int r = 0; r < rows; r++)
			table.AddRow(r.ToString(), r == 0 ? LongTitle : "short");
		return table;
	}

	private static void Paint(TableControl table, int w = 40, int h = 12)
	{
		var buffer = new CharacterBuffer(w, h);
		var bounds = new LayoutRect(0, 0, w, h);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	[Fact]
	public void MeasuredHeight_IsTheSumOfRowHeights()
	{
		// Three rows: one four lines tall, two of one line = 6 data lines, not 3.
		var wrapping = Build(wrap: true, height: null);
		Paint(wrapping);
		var wrapped = wrapping.MeasureDOM(new LayoutConstraints(0, 40, 0, int.MaxValue));

		var plainTable = Build(wrap: false, height: null);
		Paint(plainTable);
		var plain = plainTable.MeasureDOM(new LayoutConstraints(0, 40, 0, int.MaxValue));

		Assert.True(wrapped.Height > plain.Height,
			$"a wrapping table measured {wrapped.Height}, no taller than the plain {plain.Height}: "
			+ "its host will reserve one line per row and clip the wrapped lines");
	}

	[Fact]
	public void MeasuredHeight_OfAWrapOffTable_IsUnchanged()
	{
		var table = Build(wrap: false, height: null);
		Paint(table);

		var size = table.MeasureDOM(new LayoutConstraints(0, 40, 0, int.MaxValue));

		Assert.Equal(3, size.Height);   // three rows, no chrome
	}

	[Fact]
	public void ResizingAColumn_RewrapsAndShrinksTheRow()
	{
		var table = Build(wrap: true);
		Paint(table);
		int tall = table.GetRowHeight(0);

		table.Columns[1].Width = 40;
		Paint(table);
		int shorter = table.GetRowHeight(0);

		Assert.True(shorter < tall,
			$"widening the column from 20 to 40 left the row at {shorter} lines (was {tall})");
	}

	[Fact]
	public void ResizingAColumn_KeepsTheSelectedRow()
	{
		var table = Build(wrap: true, rows: 5);
		Paint(table);
		table.SelectedRowIndex = 3;

		table.Columns[1].Width = 40;
		Paint(table);

		Assert.Equal(3, table.SelectedRowIndex);
	}

	[Fact]
	public void NarrowingAColumn_GrowsTheRow()
	{
		var table = Build(wrap: true);
		Paint(table);
		int before = table.GetRowHeight(0);

		table.Columns[1].Width = 10;
		Paint(table);

		Assert.True(table.GetRowHeight(0) > before,
			$"narrowing the column did not grow the row: still {table.GetRowHeight(0)} lines");
	}

	[Fact]
	public void LargeVirtualSource_ReadsOnlyVisibleRows()
	{
		// A DataSource table cannot wrap (it has no TableColumn objects), so this pins the
		// guard: attaching ten thousand rows must not read them all on a paint.
		var table = new TableControl { BorderStyle = BorderStyle.None, ShowHeader = false, Height = 10 };
		var source = new CountingSource(10_000);
		table.DataSource = source;
		Paint(table);

		source.CellReads = 0;
		Paint(table);

		Assert.True(source.CellReads < 500,
			$"a single paint read {source.CellReads} cells from a 10,000-row source");
	}

	[Fact]
	public void WrappingTable_MeasuresOnlyTheRowsItPaints()
	{
		// Ten thousand in-memory rows, a wrapping column, and a ten-line viewport: the
		// heights measured must be a screenful, not the row count.
		var table = new TableControl { BorderStyle = BorderStyle.None, ShowHeader = false, Height = 10 };
		table.AddColumn(new TableColumn("ID", TextJustification.Left, 4));
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = true });
		for (int r = 0; r < 10_000; r++) table.AddRow(r.ToString(), LongTitle);

		Paint(table);

		Assert.True(table.MeasuredRowHeightCountForTest < 200,
			$"measured {table.MeasuredRowHeightCountForTest} row heights for a ten-line viewport");
	}

	[Fact]
	public void TableInScrollablePanel_ReportsEnoughHeightForItsWrappedRows()
	{
		// The real-thing test: a wrapping table inside a content-sized ScrollablePanel must
		// ask for the lines it will actually draw, or the panel cannot scroll to its end.
		var table = new TableControl { BorderStyle = BorderStyle.None, ShowHeader = false };
		table.AddColumn(new TableColumn("ID", TextJustification.Left, 4));
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = true });
		for (int r = 0; r < 5; r++) table.AddRow(r.ToString(), LongTitle);

		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel().WithScrollbar(true).Build();
		panel.AddControl(table);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Five rows of four lines each is 20 lines. A table measuring by row count would
		// claim 5 and the panel would reserve 5.
		Assert.True(table.ActualHeight >= 10,
			$"the table took {table.ActualHeight} lines inside the panel for five four-line rows");
	}
}

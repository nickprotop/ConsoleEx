// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests;

public class TableControlHeaderEventsTests
{
	// A small in-memory data source with 3 columns so header X maps to a known column.
	private static TableControl BuildTable()
	{
		var table = new TableControl();
		var ds = new StringGridSource(
			columns: new[] { "A", "B", "C" },
			rows: new[,] { { "1", "2", "3" }, { "4", "5", "6" } });
		table.DataSource = ds;
		// Paint once so header geometry (column widths, header row) is established.
		var buffer = new CharacterBuffer(60, 12);
		var bounds = new LayoutRect(0, 0, 60, 12);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return table;
	}

	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var p = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags>(flags), p, p, p);
	}

	// Minimal ITableDataSource for the test — matches the real interface exactly.
	private sealed class StringGridSource : ITableDataSource
	{
		private readonly string[] _cols;
		private readonly string[,] _rows;

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public StringGridSource(string[] columns, string[,] rows)
		{
			_cols = columns;
			_rows = rows;
		}

		public int RowCount => _rows.GetLength(0);
		public int ColumnCount => _cols.Length;
		public string GetColumnHeader(int columnIndex) => _cols[columnIndex];
		public string GetCellValue(int rowIndex, int columnIndex) => _rows[rowIndex, columnIndex];
	}

	[Fact]
	public void IsOnHeader_TrueOnHeaderRow_FalseOnDataRow()
	{
		var table = BuildTable();
		// The header row is at a small Y (Margin.Top, +title/border). Row 0 header is near the top.
		Assert.True(table.IsOnHeader(2, table.HeaderRowYForTest()));
		Assert.False(table.IsOnHeader(2, table.HeaderRowYForTest() + 3));
	}

	[Fact]
	public void GetColumnIndexAt_ReturnsColumn_AndMinusOnePastEnd()
	{
		var table = BuildTable();
		Assert.True(table.GetColumnIndexAt(1) >= 0);       // inside the first column
		Assert.Equal(-1, table.GetColumnIndexAt(10_000));  // far past the last column
	}

	[Fact]
	public void LeftClickOnHeader_RaisesHeaderClicked_WithColumn()
	{
		var table = BuildTable();
		int? got = null;
		table.HeaderClicked += (_, col) => got = col;
		int hy = table.HeaderRowYForTest();
		table.ProcessMouseEvent(Mouse(1, hy, MouseFlags.Button1Clicked)); // x=1 => first column
		Assert.Equal(0, got);
	}

	[Fact]
	public void LeftClickOnHeader_FiresEvenWhenSortingDisabled()
	{
		var table = BuildTable();
		table.SortingEnabled = false;
		int? got = null;
		table.HeaderClicked += (_, col) => got = col;
		table.ProcessMouseEvent(Mouse(1, table.HeaderRowYForTest(), MouseFlags.Button1Clicked));
		Assert.Equal(0, got);
	}

	[Fact]
	public void RightClickOnHeader_RaisesHeaderRightClicked_WithColumn()
	{
		var table = BuildTable();
		int? got = null;
		table.HeaderRightClicked += (_, col) => got = col;
		table.ProcessMouseEvent(Mouse(1, table.HeaderRowYForTest(), MouseFlags.Button3Clicked));
		Assert.Equal(0, got);
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// The members a table derived from <see cref="TableControl"/> builds on: the row mapping, the lock,
/// and the table's own comparison and filter rules, each exercised through a subclass exactly as a
/// third-party table would use them.
/// </summary>
public class TableExtensibilityTests
{
	#region The probe

	/// <summary>Exposes the protected members, and nothing else.</summary>
	private sealed class ProbeTable : TableControl
	{
		public object Lock => SyncRoot;
		public int DataRows => DataRowCount;
		public int DataRowAt(int display) => GetDataRowIndex(display);
		public int DisplayRowOf(int data) => GetDisplayRowIndex(data);
		public bool Matches(int data, string filter) => RowMatchesFilter(data, ParseCompoundFilterExpression(filter)!);
		public int Compare(int a, int b, int column, SortDirection direction) => CompareRows(a, b, column, direction);
		public void Sort(Span<int> rows, int column, SortDirection direction) => SortRowIndices(rows, column, direction);
		public void DropWidths() => InvalidateColumnWidths();
	}

	private static ProbeTable People()
	{
		var table = new ProbeTable { SortingEnabled = true };
		table.AddColumn("Name");
		table.AddColumn("Age");
		table.AddRow("Alice", "34");
		table.AddRow("Bob", "27");
		table.AddRow("Carol", "41");
		table.AddRow("Dave", "27");
		return table;
	}

	#endregion

	#region Mapping between data rows and display positions

	[Fact]
	public void DataRowCount_CountsEveryRow_WhileRowCountCountsTheDisplayedOnes()
	{
		var table = People();

		table.ApplyFilter("a");

		Assert.Equal(4, table.DataRows);
		Assert.Equal(3, table.RowCount);
	}

	[Fact]
	public void GetDataRowIndex_MapsThroughTheSort_AndRefusesPositionsPastTheRows()
	{
		var table = People();
		table.SortByColumn(0);
		table.SortByColumn(0);                          // Dave, Carol, Bob, Alice

		Assert.Equal(3, table.DataRowAt(0));
		Assert.Equal(0, table.DataRowAt(3));
		Assert.Equal(-1, table.DataRowAt(4));
		Assert.Equal(-1, table.DataRowAt(-1));
	}

	[Fact]
	public void GetDisplayRowIndex_IsMinusOneForAHiddenRow_AndForNoRow()
	{
		var table = People();
		table.ApplyFilter("a");                         // Alice, Carol, Dave

		Assert.Equal(-1, table.DisplayRowOf(1));        // Bob
		Assert.Equal(1, table.DisplayRowOf(2));         // Carol
		Assert.Equal(-1, table.DisplayRowOf(4));
		Assert.Equal(-1, table.DisplayRowOf(-1));
	}

	[Fact]
	public void GetDisplayRowIndex_WithoutAMap_IsTheDataRow()
	{
		var table = People();

		Assert.Equal(2, table.DisplayRowOf(2));
	}

	[Fact]
	public void SyncRoot_IsOneLockForTheTablesLifetime()
	{
		var table = People();

		Assert.Same(table.Lock, table.Lock);
	}

	#endregion

	#region The table's own rules

	[Fact]
	public void RowMatchesFilter_AppliesTheTablesRules()
	{
		var table = People();

		Assert.True(table.Matches(0, "name:ali"));
		Assert.False(table.Matches(1, "name:ali"));
		Assert.True(table.Matches(2, "age>40"));
		Assert.True(table.Matches(1, "bob|carol 27"));
	}

	[Fact]
	public void RowMatchesFilter_HonoursFuzzyMatching()
	{
		var table = People();

		Assert.False(table.Matches(0, "alc"));
		table.FuzzyFilterEnabled = true;
		Assert.True(table.Matches(0, "alc"));
	}

	[Fact]
	public void CompareRows_ComparesTheDisplayedText_InTheGivenDirection()
	{
		var table = new ProbeTable();
		table.AddColumn("Fruit");
		table.AddRow("[red]Apple[/]");
		table.AddRow("Banana");

		Assert.True(table.Compare(0, 1, 0, SortDirection.Ascending) < 0);
		Assert.True(table.Compare(0, 1, 0, SortDirection.Descending) > 0);
		Assert.Equal(0, table.Compare(1, 1, 0, SortDirection.Ascending));
	}

	[Fact]
	public void CompareRows_UsesTheColumnsComparers()
	{
		var table = People();
		table.Columns[1].CustomComparer = Comparer<string>.Create((x, y) => int.Parse(y).CompareTo(int.Parse(x)));

		Assert.True(table.Compare(2, 0, 1, SortDirection.Ascending) < 0);  // 41 before 34

		table.Columns[1].CustomRowComparer = (x, y) => string.CompareOrdinal(x.Cells[0], y.Cells[0]);

		Assert.True(table.Compare(0, 2, 1, SortDirection.Ascending) < 0);  // the row comparer wins
	}

	[Fact]
	public void SortRowIndices_SortsASliceStably_InBothDirections()
	{
		var table = People();
		int[] rows = [9, 3, 2, 1, 0, 9];
		var slice = rows.AsSpan(1, 4);

		table.Sort(slice, 1, SortDirection.Ascending);
		Assert.Equal([9, 1, 3, 0, 2, 9], rows);           // 27 (Bob), 27 (Dave), 34, 41

		table.Sort(slice, 1, SortDirection.Descending);
		Assert.Equal([9, 2, 0, 1, 3, 9], rows);           // equal ages keep data order either way
	}

	[Fact]
	public void InvalidateColumnWidths_MakesTheNextLayoutMeasureAgain()
	{
		var table = People();
		var columns = table.Columns.ToList();
		var rows = table.Rows.ToList();
		var first = table.ComputeColumnWidths(40, columns, rows);
		var cached = table.ComputeColumnWidths(40, columns, rows);

		table.DropWidths();
		var measuredAgain = table.ComputeColumnWidths(40, columns, rows);

		Assert.Same(first, cached);
		Assert.NotSame(first, measuredAgain);
	}

	#endregion
}

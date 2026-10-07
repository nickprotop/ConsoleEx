// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A data source that sorts itself is told when the table's sort goes away, so its rows do not
/// keep an order the header no longer shows.
/// </summary>
public class TableDataSourceSortTests
{
	#region Helpers

	/// <summary>Planets with a moon count; sorts itself on request and records what it was asked.</summary>
	private sealed class PlanetSource : ITableDataSource
	{
		private static readonly string[][] Original =
		[
			["Venus", "0"],
			["Mars", "2"],
			["Earth", "1"],
			["Jupiter", "95"],
		];

		private List<string[]> _rows = Original.ToList();

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public int SortCalls { get; private set; }

		public int ClearSortCalls { get; private set; }

		public int RowCount => _rows.Count;

		public int ColumnCount => 2;

		public string GetColumnHeader(int columnIndex) => columnIndex == 0 ? "Planet" : "Moons";

		public string GetCellValue(int rowIndex, int columnIndex) => _rows[rowIndex][columnIndex];

		public bool CanSort(int columnIndex) => true;

		public void Sort(int columnIndex, SortDirection direction)
		{
			SortCalls++;
			var ordered = _rows.OrderBy(row => row[columnIndex], StringComparer.Ordinal);
			_rows = (direction == SortDirection.Descending ? ordered.Reverse() : ordered).ToList();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		public void ClearSort()
		{
			ClearSortCalls++;
			_rows = Original.ToList();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
	}

	/// <summary>A source that sorts itself but has never heard of clearing a sort.</summary>
	private sealed class OldSource : ITableDataSource
	{
		public event NotifyCollectionChangedEventHandler? CollectionChanged { add { } remove { } }

		public int RowCount => 1;

		public int ColumnCount => 1;

		public string GetColumnHeader(int columnIndex) => "Comet";

		public string GetCellValue(int rowIndex, int columnIndex) => "Halley";

		public bool CanSort(int columnIndex) => true;
	}

	private static (TableControl Table, PlanetSource Source) Planets()
	{
		var source = new PlanetSource();
		var table = new TableControl { SortingEnabled = true, FilteringEnabled = true, ReadOnly = false, DataSource = source };
		return (table, source);
	}

	private static List<string> Displayed(TableControl table, ITableDataSource source)
		=> Enumerable.Range(0, table.RowCount).Select(i => source.GetCellValue(table.MapDisplayToData(i), 0)).ToList();

	/// <summary>Filters and sorts itself, and like many sources, forgets its order when its filter is cleared.</summary>
	private sealed class ForgetfulSource : ITableDataSource
	{
		private static readonly string[] Original = ["Venus", "Mars", "Earth", "Jupiter"];

		private List<string> _rows = Original.ToList();

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public int SortCalls { get; private set; }

		public bool CanFilter => true;

		public int RowCount => _rows.Count;

		public int ColumnCount => 1;

		public string GetColumnHeader(int columnIndex) => "Planet";

		public string GetCellValue(int rowIndex, int columnIndex) => _rows[rowIndex];

		public bool CanSort(int columnIndex) => true;

		public void Sort(int columnIndex, SortDirection direction)
		{
			SortCalls++;
			_rows = _rows.OrderBy(row => row, StringComparer.Ordinal).ToList();
			Raise();
		}

		public void ApplyFilter(string filterText, string? columnName, FilterOperator op)
		{
			_rows = _rows.Where(row => row.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList();
			Raise();
		}

		public void ClearFilter()
		{
			_rows = Original.ToList();
			Raise();
		}

		private void Raise() => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	#endregion

	#region Ending a filter keeps the source sorted

	[Fact]
	public void BackspacingAFilterAway_SortsTheSourceAgain_AsClearingItDoes()
	{
		var source = new ForgetfulSource();
		var table = new TableControl { SortingEnabled = true, FilteringEnabled = true, ReadOnly = false, DataSource = source };
		table.SortByColumn(0);
		table.EnterFilterMode();
		table.ProcessFilterKey(new ConsoleKeyInfo('r', ConsoleKey.R, false, false, false));

		table.ProcessFilterKey(new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));

		Assert.Equal(2, source.SortCalls);
		Assert.Equal(["Earth", "Jupiter", "Mars", "Venus"], Displayed(table, source));
		Assert.Equal(SortDirection.Ascending, table.CurrentSortDirection);
	}

	[Fact]
	public void ClearingAFilter_SortsTheSourceAgain()
	{
		var source = new ForgetfulSource();
		var table = new TableControl { SortingEnabled = true, FilteringEnabled = true, DataSource = source };
		table.SortByColumn(0);
		table.ApplyFilter("r");

		table.ClearFilter();

		Assert.Equal(["Earth", "Jupiter", "Mars", "Venus"], Displayed(table, source));
	}

	#endregion

	#region The source hears that the sort went away

	[Fact]
	public void AHeaderClickCyclingToNoSort_TellsTheSourceToDropItsOrder()
	{
		var (table, source) = Planets();

		table.SortByColumn(0);
		table.SortByColumn(0);
		table.SortByColumn(0);

		Assert.Equal(1, source.ClearSortCalls);
		Assert.Equal(["Venus", "Mars", "Earth", "Jupiter"], Displayed(table, source));
	}

	[Fact]
	public void ClearSort_TellsASourceThatSortedItself()
	{
		var (table, source) = Planets();
		table.SortByColumn(0);

		table.ClearSort();

		Assert.Equal(1, source.ClearSortCalls);
		Assert.Equal(SortDirection.None, table.CurrentSortDirection);
	}

	[Fact]
	public void DisablingSorting_TellsTheSource()
	{
		var (table, source) = Planets();
		table.SortByColumn(0);

		table.SortingEnabled = false;

		Assert.Equal(1, source.ClearSortCalls);
	}

	[Fact]
	public void ASourceNeverSorted_IsNotAskedToClearItsSort()
	{
		var (table, source) = Planets();

		table.ClearSort();
		table.SortingEnabled = false;

		Assert.Equal(0, source.ClearSortCalls);
	}

	[Fact]
	public void SortingByAnotherColumn_DoesNotClearInBetween()
	{
		var (table, source) = Planets();

		table.SortByColumn(0);
		table.SortByColumn(1);

		Assert.Equal(2, source.SortCalls);
		Assert.Equal(0, source.ClearSortCalls);
	}

	[Fact]
	public void UnderAClientSideFilter_ClearingTheSort_ShowsTheMatchesInTheSourcesOwnOrder()
	{
		// Sorted by the source before the filter; the table sorts the matches itself after it.
		var (table, source) = Planets();
		table.SortByColumn(0);
		table.ApplyFilter("r");                         // Mars, Earth, Jupiter: client-side

		table.SortByColumn(0);                          // descending, sorted by the table
		table.SortByColumn(0);                          // no sort

		Assert.Equal(1, source.ClearSortCalls);
		Assert.Equal(["Mars", "Earth", "Jupiter"], Displayed(table, source));
	}

	[Fact]
	public void ASourceWithoutClearSort_KeepsWorking()
	{
		var table = new TableControl { SortingEnabled = true, DataSource = new OldSource() };
		table.SortByColumn(0);

		table.ClearSort();

		Assert.Equal(1, table.RowCount);
		Assert.Equal(-1, table.SortColumnIndex);
	}

	[Fact]
	public void ANewDataSource_IsNotAskedToClearTheOldOnesSort()
	{
		var (table, first) = Planets();
		table.SortByColumn(0);
		var second = new PlanetSource();

		table.DataSource = second;
		table.ClearSort();

		Assert.Equal(0, first.ClearSortCalls);
		Assert.Equal(0, second.ClearSortCalls);
	}

	#endregion
}

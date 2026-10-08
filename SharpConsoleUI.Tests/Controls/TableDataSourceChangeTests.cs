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
/// A filter over a data source follows the source's rows as they change: a client-side filter is
/// scanned again, and display rows the source supplied are asked for again.
/// </summary>
public class TableDataSourceChangeTests
{
	#region Helpers

	/// <summary>A submarine crew that changes under the table and says so.</summary>
	private sealed class CrewSource : ITableDataSource
	{
		private readonly List<string> _names = ["Ada Anchor", "Bo Bilge", "Cal Compass", "Dee Depth"];

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public bool CanFilter { get; init; }

		public int ClearFilterCalls { get; private set; }

		public int RowCount => _names.Count;

		public int ColumnCount => 1;

		public string GetColumnHeader(int columnIndex) => "Crew";

		public string GetCellValue(int rowIndex, int columnIndex) => _names[rowIndex];

		public bool CanSort(int columnIndex) => true;

		public void Sort(int columnIndex, SortDirection direction)
		{
			_names.Sort(StringComparer.Ordinal);
			if (direction == SortDirection.Descending)
				_names.Reverse();
			Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		public void ClearFilter()
		{
			ClearFilterCalls++;
			Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		public void Add(string name)
		{
			_names.Add(name);
			Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, name, _names.Count - 1));
		}

		public void RemoveAt(int index)
		{
			string name = _names[index];
			_names.RemoveAt(index);
			Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, name, index));
		}

		public void Rename(int index, string name)
		{
			string old = _names[index];
			_names[index] = name;
			Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, name, old, index));
		}

		public void Touch() => Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		private void Raise(NotifyCollectionChangedEventArgs e) => CollectionChanged?.Invoke(this, e);
	}

	/// <summary>Supplies the display rows for a filter itself, and counts how often it was asked.</summary>
	private sealed class SupplyingTable : TableControl
	{
		public int Asked { get; private set; }

		public Action? WhileAsked { get; set; }

		protected override TableFilterResult TryApplyFilterToDataSource(ITableDataSource dataSource, CompoundFilterExpression compound)
		{
			Asked++;
			WhileAsked?.Invoke();
			var rows = Enumerable.Range(0, dataSource.RowCount)
				.Where(row => dataSource.GetCellValue(row, 0).Contains(compound.RawText, StringComparison.OrdinalIgnoreCase))
				.ToList();
			return TableFilterResult.WithDisplayRows(rows);
		}
	}

	private static List<string> Displayed(TableControl table, ITableDataSource source)
		=> Enumerable.Range(0, table.RowCount).Select(i => source.GetCellValue(table.MapDisplayToData(i), 0)).ToList();

	#endregion

	#region A filter being typed covers the rows the source has now

	private static void Type(TableControl table, string text)
	{
		foreach (char c in text)
			table.ProcessFilterKey(new ConsoleKeyInfo(c, ConsoleKey.A, false, false, false));
	}

	[Fact]
	public void RowsRemovedBeforeTheFirstKeystroke_AreNotReadPastTheEnd()
	{
		var source = new CrewSource();
		var table = new TableControl { FilteringEnabled = true, ReadOnly = false, DataSource = source };
		table.EnterFilterMode();
		source.RemoveAt(3);
		source.RemoveAt(2);

		Type(table, "o");

		Assert.Equal(["Ada Anchor", "Bo Bilge"], Displayed(table, source));
	}

	[Fact]
	public void RowsAddedBeforeTheFirstKeystroke_AreSearched()
	{
		var source = new CrewSource();
		var table = new TableControl { FilteringEnabled = true, ReadOnly = false, DataSource = source };
		table.EnterFilterMode();
		source.Add("Eve Echo");

		Type(table, "echo");

		Assert.Equal(["Eve Echo"], Displayed(table, source));
	}

	#endregion

	#region A client-side filter is scanned again

	[Fact]
	public void ARowAddedThatMatches_IsShown()
	{
		var source = new CrewSource();
		var table = new TableControl { FilteringEnabled = true, DataSource = source };
		table.ApplyFilter("e");                         // Bo Bilge, Dee Depth

		source.Add("Eve Echo");

		Assert.Equal(["Bo Bilge", "Dee Depth", "Eve Echo"], Displayed(table, source));
	}

	[Fact]
	public void ARowRemoved_IsNoLongerShown()
	{
		var source = new CrewSource();
		var table = new TableControl { FilteringEnabled = true, DataSource = source };
		table.ApplyFilter("e");

		source.RemoveAt(1);                             // Bo Bilge

		Assert.Equal(["Dee Depth"], Displayed(table, source));
	}

	[Fact]
	public void ARowThatStopsMatching_IsNoLongerShown()
	{
		var source = new CrewSource();
		var table = new TableControl { FilteringEnabled = true, DataSource = source };
		table.ApplyFilter("e");

		source.Rename(3, "Dot Dinghy");

		Assert.Equal(["Bo Bilge"], Displayed(table, source));
	}

	[Fact]
	public void TheSelection_StaysWithinTheRowsThereAre()
	{
		var source = new CrewSource();
		var table = new TableControl { FilteringEnabled = true, ReadOnly = false, DataSource = source };
		table.ApplyFilter("e");
		table.SelectedRowIndex = 1;                     // Dee Depth

		source.RemoveAt(3);

		Assert.Equal(1, table.RowCount);
		Assert.Equal(0, table.SelectedRowIndex);
	}

	#endregion

	#region Supplied display rows are asked for again

	[Fact]
	public void SuppliedDisplayRows_AreAskedForAgain_WhenTheSourceChanges()
	{
		var source = new CrewSource();
		var table = new SupplyingTable { FilteringEnabled = true, DataSource = source };
		table.ApplyFilter("e");

		source.Add("Eve Echo");

		Assert.Equal(2, table.Asked);
		Assert.Equal(["Bo Bilge", "Dee Depth", "Eve Echo"], Displayed(table, source));
	}

	[Fact]
	public void SuppliedDisplayRows_FollowASortOfTheSource_AskedForOnce()
	{
		var source = new CrewSource();
		var table = new SupplyingTable { FilteringEnabled = true, SortingEnabled = true, DataSource = source };
		table.ApplyFilter("e");

		table.SortByColumn(0);
		table.SortByColumn(0);

		Assert.Equal(3, table.Asked);
		Assert.Equal(["Dee Depth", "Bo Bilge"], Displayed(table, source));
	}

	[Fact]
	public void ASourceAnnouncingAChangeWhileAsked_IsNotAskedAgainFromInside()
	{
		var source = new CrewSource();
		var table = new SupplyingTable { FilteringEnabled = true, DataSource = source };
		table.ApplyFilter("e");
		table.WhileAsked = source.Touch;

		table.ApplyFilter("o");

		Assert.Equal(2, table.Asked);
		Assert.Equal(["Ada Anchor", "Bo Bilge", "Cal Compass"], Displayed(table, source));
	}

	[Fact]
	public void ClearingTheFilter_DoesNotAskForItAgain()
	{
		var source = new CrewSource { CanFilter = true };
		var table = new SupplyingTable { FilteringEnabled = true, DataSource = source };
		table.ApplyFilter("e");

		table.ClearFilter();

		Assert.Equal(1, source.ClearFilterCalls);
		Assert.Equal(1, table.Asked);
		Assert.Equal(4, table.RowCount);
	}

	#endregion
}

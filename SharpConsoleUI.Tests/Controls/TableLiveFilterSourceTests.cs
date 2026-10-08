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
/// While a filter is typed, a data source that narrowed itself for it is told to clear that filter
/// as soon as it stops applying, whatever the text changes to.
/// </summary>
public class TableLiveFilterSourceTests
{
	#region Helpers

	/// <summary>Lighthouses that filter themselves on a single term, as a remote source would.</summary>
	private sealed class LighthouseSource : ITableDataSource
	{
		private static readonly string[] All = ["Alexandria", "Eddystone", "Fastnet", "Bell Rock"];

		private List<string> _shown = All.ToList();

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public int ClearFilterCalls { get; private set; }

		public bool CanFilter => true;

		public int RowCount => _shown.Count;

		public int ColumnCount => 1;

		public string GetColumnHeader(int columnIndex) => "Lighthouse";

		public string GetCellValue(int rowIndex, int columnIndex) => _shown[rowIndex];

		public void ApplyFilter(string filterText, string? columnName, FilterOperator op)
		{
			_shown = All.Where(name => name.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		public void ClearFilter()
		{
			ClearFilterCalls++;
			_shown = All.ToList();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
	}

	private static (TableControl Table, LighthouseSource Source) Typing(string text)
	{
		var source = new LighthouseSource();
		var table = new TableControl { FilteringEnabled = true, ReadOnly = false, DataSource = source };
		table.EnterFilterMode();
		Type(table, text);
		return (table, source);
	}

	private static void Type(TableControl table, string text)
	{
		foreach (char c in text)
			table.ProcessFilterKey(new ConsoleKeyInfo(c, ConsoleKey.A, false, false, false));
	}

	private static void Backspace(TableControl table)
		=> table.ProcessFilterKey(new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));

	private static List<string> Displayed(TableControl table, ITableDataSource source)
		=> Enumerable.Range(0, table.RowCount).Select(i => source.GetCellValue(table.MapDisplayToData(i), 0)).ToList();

	#endregion

	[Fact]
	public void TextChangedToBlanks_ClearsTheSourcesFilter()
	{
		// A leading blank, so taking the letters away leaves blanks without passing through empty text.
		var (table, source) = Typing(" ell");               // Bell Rock, narrowed by the source

		Backspace(table);
		Backspace(table);
		Backspace(table);

		Assert.Equal(1, source.ClearFilterCalls);
		Assert.Equal(4, table.RowCount);
	}

	[Fact]
	public void TextChangedToAFilterTheSourceCannotTake_IsFilteredOverEveryRow()
	{
		// "ex|ast" has two alternatives, so the table filters client-side, which needs every row back.
		var (table, source) = Typing("ex");

		Type(table, "|ast");

		Assert.Equal(1, source.ClearFilterCalls);
		Assert.Equal(["Alexandria", "Fastnet"], Displayed(table, source));
	}

	[Fact]
	public void AFilterAppliedInCode_ThatTheSourceCannotTake_IsFilteredOverEveryRow()
	{
		var source = new LighthouseSource();
		var table = new TableControl { FilteringEnabled = true, DataSource = source };
		table.ApplyFilter("ex");

		table.ApplyFilter("ex|ast");

		Assert.Equal(1, source.ClearFilterCalls);
		Assert.Equal(["Alexandria", "Fastnet"], Displayed(table, source));
	}
}

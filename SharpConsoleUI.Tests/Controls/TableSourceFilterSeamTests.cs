// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Issue #88: <c>TableControl</c> only handed a filter to its <see cref="ITableDataSource"/> when
/// the filter was a single expression, because <c>ITableDataSource.ApplyFilter</c> takes one
/// (text, column, operator) triple and cannot express AND/OR. A source that COULD evaluate the
/// whole expression — a flattened hierarchy, a remote query — had no way to receive it, and
/// <c>TableControl</c> exposed no virtual member to override.
/// </summary>
public class TableSourceFilterSeamTests
{
	#region Sources

	/// <summary>A plain source that narrows itself, standing in for a server-side WHERE.</summary>
	private class NarrowingSource : ITableDataSource
	{
		protected readonly List<string[]> All;
		protected List<string[]> Visible;

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public NarrowingSource(params string[][] rows)
		{
			All = new List<string[]>(rows);
			Visible = new List<string[]>(rows);
		}

		public virtual bool CanFilter => true;
		public virtual int RowCount => Visible.Count;
		public int ColumnCount => 2;
		public string GetColumnHeader(int c) => c == 0 ? "Name" : "City";
		public virtual string GetCellValue(int r, int c) => Visible[r][c];
		public object? GetRowTag(int rowIndex) => null;

		public void ApplyFilter(string filterText, string? columnName, FilterOperator op)
		{
			Visible = All.FindAll(row => row.Any(cell =>
				cell.Contains(filterText, StringComparison.OrdinalIgnoreCase)));
			Raise();
		}

		public void ClearFilter()
		{
			Visible = new List<string[]>(All);
			Raise();
		}

		protected void Raise() => CollectionChanged?.Invoke(this,
			new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	/// <summary>
	/// A source that keeps reporting every row and names which to show — the case
	/// <see cref="TableFilterOutcome.DisplayRowsSupplied"/> exists for.
	/// </summary>
	private sealed class MarkingSource : NarrowingSource
	{
		public MarkingSource(params string[][] rows) : base(rows) { }

		// Never narrows: RowCount stays the full set, the table addresses rows through the map.
		public override int RowCount => All.Count;
		public override string GetCellValue(int r, int c) => All[r][c];

		/// <summary>Rows whose Name or City contains every one of the given needles.</summary>
		public List<int> RowsMatchingAll(IEnumerable<string> needles)
		{
			var result = new List<int>();
			for (int i = 0; i < All.Count; i++)
			{
				if (needles.All(n => All[i].Any(cell =>
						cell.Contains(n, StringComparison.OrdinalIgnoreCase))))
					result.Add(i);
			}
			return result;
		}

		public int Count => All.Count;
	}

	#endregion

	#region Tables

	/// <summary>A table whose source evaluates compound filters and supplies display rows.</summary>
	private sealed class MarkingTable : TableControl
	{
		public int CompoundFiltersSeen { get; private set; }

		protected override TableFilterResult TryApplyFilterToDataSource(
			ITableDataSource dataSource, CompoundFilterExpression compound)
		{
			if (dataSource is not MarkingSource marking) return base.TryApplyFilterToDataSource(dataSource, compound);

			CompoundFiltersSeen++;
			var needles = compound.Terms.Select(t => t.Alternatives[0].Value);
			return TableFilterResult.WithDisplayRows(marking.RowsMatchingAll(needles));
		}
	}

	/// <summary>A table that declines everything, forcing the client-side path.</summary>
	private sealed class DecliningTable : TableControl
	{
		protected override TableFilterResult TryApplyFilterToDataSource(
			ITableDataSource dataSource, CompoundFilterExpression compound)
			=> TableFilterResult.NotHandled;
	}

	#endregion

	/// <summary>
	/// A filter with two terms. The grammar is space-separated AND and <c>|</c> OR — there is no
	/// "AND" keyword, so a literal one would be searched for as text.
	/// </summary>
	/// <remarks>Only "Alan"/"Amsterdam" contains both an 'l' and an 'm'.</remarks>
	private const string CompoundFilter = "l m";

	private static readonly string[][] People =
	[
		["Alice", "Athens"],
		["Bob", "Berlin"],
		["Carol", "Cairo"],
		["Dave", "Dublin"],
		["Alan", "Amsterdam"],
	];

	private static T Filtered<T>(T table, string filter) where T : TableControl
	{
		table.EnterFilterMode();
		table.ApplyFilter(filter);
		return table;
	}

	/// <summary>
	/// The value shown in a DISPLAY row, which is what the user sees.
	/// </summary>
	/// <remarks>
	/// <c>GetCell</c> takes a data index, so reading it directly would bypass the very mapping
	/// these tests are about and pass whether or not the source's rows were honoured.
	/// </remarks>
	private static string DisplayCell(TableControl table, int displayRow, int column)
		=> table.GetCell(table.MapDisplayToData(displayRow), column);

	#region The reported bug — a compound filter reaches the source

	/// <summary>
	/// The heart of #88: before the seam existed, a compound filter was never offered to the
	/// source at all.
	/// </summary>
	[Fact]
	public void ACompoundFilter_IsOfferedToTheSource()
	{
		var table = new MarkingTable { DataSource = new MarkingSource(People) };

		Filtered(table, CompoundFilter);

		Assert.Equal(1, table.CompoundFiltersSeen);
	}

	/// <summary>Only the Alan/Amsterdam row has both an 'l' and an 'm'.</summary>
	[Fact]
	public void TheSourcesAnswer_DecidesWhichRowsShow()
	{
		var table = new MarkingTable { DataSource = new MarkingSource(People) };

		Filtered(table, CompoundFilter);

		Assert.Equal(1, table.RowCount);
		Assert.Equal("Alan", DisplayCell(table, 0, 0));
	}

	/// <summary>
	/// The source keeps reporting every row, so the table must be addressing them through the
	/// supplied map rather than by identity — the distinction the outcome encodes.
	/// </summary>
	[Fact]
	public void TheSourceKeepsReportingEveryRow()
	{
		var source = new MarkingSource(People);
		var table = new MarkingTable { DataSource = source };

		Filtered(table, CompoundFilter);

		Assert.Equal(People.Length, source.RowCount);  // the source did NOT narrow
		Assert.Equal(1, table.RowCount);               // the table shows only the match
	}

	[Fact]
	public void AnEmptyMatchSet_IsHandledNotTreatedAsDeclined()
	{
		var table = new MarkingTable { DataSource = new MarkingSource(People) };

		Filtered(table, "zzz qqq");

		Assert.Equal(0, table.RowCount);
	}

	#endregion

	#region The default is unchanged

	/// <summary>
	/// A table that does not override still delegates single expressions and still keeps compound
	/// ones client-side. This is the behaviour every existing user has.
	/// </summary>
	[Fact]
	public void TheDefault_StillDelegatesASingleExpression()
	{
		var source = new NarrowingSource(People);
		var table = new TableControl { DataSource = source };

		Filtered(table, "Berlin");

		Assert.Equal(1, source.RowCount);   // the source narrowed itself
		Assert.Equal(1, table.RowCount);
	}

	[Fact]
	public void TheDefault_StillKeepsACompoundFilterClientSide()
	{
		var source = new NarrowingSource(People);
		var table = new TableControl { DataSource = source };

		Filtered(table, CompoundFilter);

		// The source was never asked, so it still reports everything.
		Assert.Equal(People.Length, source.RowCount);
		Assert.Equal(1, table.RowCount);  // the table filtered it itself
	}

	/// <summary>An override returning NotHandled gets the client-side path, same as the default.</summary>
	[Fact]
	public void DecliningEverything_FallsBackToClientSideFiltering()
	{
		var source = new NarrowingSource(People);
		var table = new DecliningTable { DataSource = source };

		Filtered(table, "Berlin");

		Assert.Equal(People.Length, source.RowCount);  // never delegated
		Assert.Equal(1, table.RowCount);               // but still filtered correctly
	}

	#endregion

	#region Sorting must not discard the source's map

	/// <summary>
	/// A client-side map is rebuilt on sort by rescanning rows. Doing that to a SOURCE-supplied map
	/// would throw away what the source chose to show — the knowledge it overrode the seam to
	/// supply. The table must ask the source to sort instead.
	/// </summary>
	[Fact]
	public void SortingAFilteredTable_KeepsTheSourcesChoiceOfRows()
	{
		var table = new MarkingTable { DataSource = new MarkingSource(People) };
		Filtered(table, CompoundFilter);
		Assert.Equal(1, table.RowCount);

		table.SortByColumn(0);

		Assert.Equal(1, table.RowCount);
		Assert.Equal("Alan", DisplayCell(table, 0, 0));
	}

	[Fact]
	public void ClearingTheSortAfterwards_StillKeepsTheSourcesRows()
	{
		var table = new MarkingTable { DataSource = new MarkingSource(People) };
		Filtered(table, CompoundFilter);
		table.SortByColumn(0);

		table.ClearSort();

		Assert.Equal(1, table.RowCount);
	}

	/// <summary>The source is not asked again; the filter it already applied still stands.</summary>
	[Fact]
	public void Sorting_DoesNotReAskTheSourceToFilter()
	{
		var table = new MarkingTable { DataSource = new MarkingSource(People) };
		Filtered(table, CompoundFilter);
		Assert.Equal(1, table.CompoundFiltersSeen);

		table.SortByColumn(0);

		Assert.Equal(1, table.CompoundFiltersSeen);
	}

	#endregion

	#region Clearing

	[Fact]
	public void ClearingTheFilter_RestoresEveryRow()
	{
		var table = new MarkingTable { DataSource = new MarkingSource(People) };
		Filtered(table, CompoundFilter);
		Assert.Equal(1, table.RowCount);

		table.ClearFilter();

		Assert.Equal(People.Length, table.RowCount);
	}

	/// <summary>
	/// After clearing, a plain sort takes the ordinary path again — the source-map flag must not
	/// have survived the clear.
	/// </summary>
	[Fact]
	public void AfterClearing_SortingBehavesNormally()
	{
		var table = new MarkingTable { DataSource = new MarkingSource(People) };
		Filtered(table, CompoundFilter);
		table.ClearFilter();

		table.SortByColumn(0);

		Assert.Equal(People.Length, table.RowCount);
	}

	#endregion

	#region The result type

	[Fact]
	public void NotHandled_CarriesNoRows()
	{
		Assert.Equal(TableFilterOutcome.NotHandled, TableFilterResult.NotHandled.Outcome);
		Assert.Null(TableFilterResult.NotHandled.DisplayRows);
	}

	[Fact]
	public void SourceNarrowed_CarriesNoRows()
	{
		Assert.Equal(TableFilterOutcome.SourceNarrowed, TableFilterResult.SourceNarrowed.Outcome);
		Assert.Null(TableFilterResult.SourceNarrowed.DisplayRows);
	}

	[Fact]
	public void WithDisplayRows_CarriesThem()
	{
		var result = TableFilterResult.WithDisplayRows(new[] { 3, 1, 4 });

		Assert.Equal(TableFilterOutcome.DisplayRowsSupplied, result.Outcome);
		Assert.Equal(new[] { 3, 1, 4 }, result.DisplayRows);
	}

	/// <summary>An empty set is "nothing matched", which is a real answer, not a null.</summary>
	[Fact]
	public void WithDisplayRows_AcceptsAnEmptySet()
	{
		var result = TableFilterResult.WithDisplayRows(Array.Empty<int>());

		Assert.Equal(TableFilterOutcome.DisplayRowsSupplied, result.Outcome);
		Assert.Empty(result.DisplayRows!);
	}

	[Fact]
	public void WithDisplayRows_RejectsNull()
		=> Assert.Throws<ArgumentNullException>(() => TableFilterResult.WithDisplayRows(null!));

	/// <summary>
	/// The source's order is honoured, not just its membership: display row 0 is whichever data row
	/// the source listed first.
	/// </summary>
	[Fact]
	public void TheSuppliedOrder_IsTheDisplayOrder()
	{
		var source = new MarkingSource(People);
		var table = new OrderingTable { DataSource = source, Order = [3, 0] };

		Filtered(table, "anything");

		Assert.Equal(2, table.RowCount);
		Assert.Equal("Dave", DisplayCell(table, 0, 0));
		Assert.Equal("Alice", DisplayCell(table, 1, 0));
	}

	private sealed class OrderingTable : TableControl
	{
		public int[] Order { get; init; } = [];

		protected override TableFilterResult TryApplyFilterToDataSource(
			ITableDataSource dataSource, CompoundFilterExpression compound)
			=> TableFilterResult.WithDisplayRows(Order);
	}

	#endregion
}

/// <summary>
/// The use case from issue #88, end to end: a tree flattened into table rows, where filtering has
/// to understand the hierarchy — a parent whose CHILD matches stays visible although the parent
/// itself does not match.
/// </summary>
/// <remarks>
/// The "real thing" test for this change. It builds the real nesting (table in a window in a
/// window system), drives the filter through the REAL key path rather than calling ApplyFilter,
/// and re-renders before asserting, because a map that does not survive a render is no use. The
/// isolated tests above would all pass against a table that dropped the source's map on its next
/// layout pass.
/// </remarks>
public class TableTreeFilterRealUseTests
{
	/// <summary>A story with its tasks, flattened into rows the way a tree table reports them.</summary>
	private sealed class TreeSource : ITableDataSource
	{
		// (text, isChild). Only the tasks mention "login"; the stories never do.
		private readonly (string Text, bool IsChild)[] _rows =
		[
			("Story: Authentication", false),
			("  Task: login form", true),
			("  Task: password reset", true),
			("Story: Reporting", false),
			("  Task: export CSV", true),
		];

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public bool CanFilter => true;
		public int RowCount => _rows.Length;      // never narrows; the map decides what shows
		public int ColumnCount => 1;
		public string GetColumnHeader(int c) => "Item";
		public string GetCellValue(int r, int c) => _rows[r].Text;
		public object? GetRowTag(int rowIndex) => null;
		public void ApplyFilter(string filterText, string? columnName, FilterOperator op) { }
		public void ClearFilter() { }

		/// <summary>
		/// Rows matching every needle, plus the parent of any matching child — the rule the table
		/// cannot infer from flat rows, and the whole reason this source needs the seam.
		/// </summary>
		public List<int> MatchesKeepingParents(IEnumerable<string> needles)
		{
			var keep = new SortedSet<int>();
			for (int i = 0; i < _rows.Length; i++)
			{
				if (!needles.All(n => _rows[i].Text.Contains(n, StringComparison.OrdinalIgnoreCase)))
					continue;

				keep.Add(i);
				if (_rows[i].IsChild)
				{
					for (int p = i - 1; p >= 0; p--)
						if (!_rows[p].IsChild) { keep.Add(p); break; }
				}
			}
			return keep.ToList();
		}
	}

	private sealed class TreeTable : TableControl
	{
		protected override TableFilterResult TryApplyFilterToDataSource(
			ITableDataSource dataSource, CompoundFilterExpression compound)
		{
			if (dataSource is not TreeSource tree) return base.TryApplyFilterToDataSource(dataSource, compound);

			var needles = compound.Terms.Select(t => t.Alternatives[0].Value);
			return TableFilterResult.WithDisplayRows(tree.MatchesKeepingParents(needles));
		}
	}

	private static (TreeTable table, ConsoleWindowSystem system, Window window) Build()
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(100, 30),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));

		// Deliberately narrow and short: an allotted extent that differs from the painted
		// viewport is where layout bugs hide.
		var window = new Window(system) { Left = 0, Top = 0, Width = 44, Height = 10 };
		// Filtering plus interactive, which is what WithFiltering() sets — the '/' key is only
		// read when the table is both.
		var table = new TreeTable
		{
			DataSource = new TreeSource(),
			FilteringEnabled = true,
			ReadOnly = false,
		};

		window.AddControl(table);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.FocusControl(table);
		window.RenderAndGetVisibleContent();

		return (table, system, window);
	}

	/// <summary>Types a filter the way a user does: '/', the text, then Enter.</summary>
	private static void TypeFilter(ConsoleWindowSystem system, string text)
	{
		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		foreach (char c in text)
			Send(system, new ConsoleKeyInfo(c, ConsoleKey.A, false, false, false));
		Send(system, new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
	}

	private static void Send(ConsoleWindowSystem system, ConsoleKeyInfo key)
	{
		system.InputStateService.EnqueueKey(key);
		system.Input.ProcessInput();
	}

	private static List<string> VisibleRows(TableControl table)
	{
		var rows = new List<string>();
		for (int i = 0; i < table.RowCount; i++)
			rows.Add(table.GetCell(table.MapDisplayToData(i), 0));
		return rows;
	}

	/// <summary>
	/// A compound filter matching only a CHILD keeps that child's parent on screen. Before the
	/// seam existed the table filtered the flat rows itself and dropped the parent, so the same
	/// text behaved differently depending on whether it had one term or two.
	/// </summary>
	[Fact]
	public void ACompoundFilterMatchingAChild_KeepsItsParentVisible()
	{
		var (table, system, window) = Build();

		TypeFilter(system, "task login");
		window.RenderAndGetVisibleContent();

		var rows = VisibleRows(table);

		Assert.Contains(rows, r => r.Contains("login form"));
		Assert.Contains(rows, r => r.Contains("Story: Authentication"));
	}

	/// <summary>The unrelated story and its task are gone — the filter still filters.</summary>
	[Fact]
	public void TheUnrelatedBranch_IsHidden()
	{
		var (table, system, window) = Build();

		TypeFilter(system, "task login");
		window.RenderAndGetVisibleContent();

		var rows = VisibleRows(table);

		Assert.DoesNotContain(rows, r => r.Contains("Reporting"));
		Assert.DoesNotContain(rows, r => r.Contains("export CSV"));
	}

	/// <summary>
	/// The state has to SURVIVE a re-render. A map rebuilt or dropped during layout would show the
	/// unfiltered tree again on the next frame, which every isolated test above would miss.
	/// </summary>
	[Fact]
	public void TheFilteredTree_SurvivesARerender()
	{
		var (table, system, window) = Build();
		TypeFilter(system, "task login");

		int afterFilter = table.RowCount;
		for (int i = 0; i < 3; i++) window.RenderAndGetVisibleContent();

		Assert.Equal(afterFilter, table.RowCount);
		Assert.Contains(VisibleRows(table), r => r.Contains("Story: Authentication"));
	}

	/// <summary>Exactly the matching child and its parent: two rows, nothing else swept in.</summary>
	[Fact]
	public void OnlyTheMatchAndItsParentRemain()
	{
		var (table, system, window) = Build();

		TypeFilter(system, "task login");
		window.RenderAndGetVisibleContent();

		Assert.Equal(2, table.RowCount);
	}
}

/// <summary>
/// Backspacing a live filter away has to tell the DATA SOURCE the filter is gone, not just the
/// table. A source that narrowed itself otherwise keeps reporting only the matches while the
/// table believes nothing is filtered — and no further keystroke puts the rows back.
/// </summary>
/// <remarks>
/// Found while adding the issue #88 seam. Driven through the real key path, because the bug is in
/// what the keystroke handler does and calling the internals directly would assume the answer.
/// </remarks>
public class TableLiveFilterClearTests
{
	private sealed class NarrowingSource : ITableDataSource
	{
		private static readonly string[][] AllRows =
		[
			["Alice", "Athens"],
			["Bob", "Berlin"],
			["Carol", "Cairo"],
		];

		private List<string[]> _visible = [.. AllRows];

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public bool CanFilter => true;
		public int RowCount => _visible.Count;
		public int ColumnCount => 2;
		public string GetColumnHeader(int c) => c == 0 ? "Name" : "City";
		public string GetCellValue(int r, int c) => _visible[r][c];
		public object? GetRowTag(int rowIndex) => null;

		public void ApplyFilter(string filterText, string? columnName, FilterOperator op)
		{
			_visible = AllRows.Where(row => row.Any(cell =>
				cell.Contains(filterText, StringComparison.OrdinalIgnoreCase))).ToList();
			Raise();
		}

		public void ClearFilter()
		{
			_visible = [.. AllRows];
			Raise();
		}

		private void Raise() => CollectionChanged?.Invoke(this,
			new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		public static int TotalRows => AllRows.Length;
	}

	private static (TableControl table, ConsoleWindowSystem system, Window window, NarrowingSource source) Build()
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(100, 30),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));

		var window = new Window(system) { Left = 0, Top = 0, Width = 50, Height = 12 };
		var source = new NarrowingSource();
		var table = new TableControl { DataSource = source, FilteringEnabled = true, ReadOnly = false };

		window.AddControl(table);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.FocusControl(table);
		window.RenderAndGetVisibleContent();

		return (table, system, window, source);
	}

	private static void Send(ConsoleWindowSystem system, ConsoleKeyInfo key)
	{
		system.InputStateService.EnqueueKey(key);
		system.Input.ProcessInput();
	}

	private static void Type(ConsoleWindowSystem system, string text)
	{
		foreach (char c in text)
			Send(system, new ConsoleKeyInfo(c, ConsoleKey.A, false, false, false));
	}

	private static void Backspace(ConsoleWindowSystem system, int times)
	{
		for (int i = 0; i < times; i++)
			Send(system, new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
	}

	private static void OpenFilter(ConsoleWindowSystem system)
		=> Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));

	/// <summary>Typing a live filter narrows the source — the premise the rest depends on.</summary>
	[Fact]
	public void TypingALiveFilter_NarrowsTheSource()
	{
		var (table, system, _, source) = Build();

		OpenFilter(system);
		Type(system, "Berlin");

		Assert.Equal(1, source.RowCount);
		Assert.Equal(1, table.RowCount);
	}

	/// <summary>The bug: backspacing to empty left the source still filtered.</summary>
	[Fact]
	public void BackspacingToEmpty_RestoresTheSource()
	{
		var (_, system, _, source) = Build();

		OpenFilter(system);
		Type(system, "Berlin");
		Backspace(system, "Berlin".Length);

		Assert.Equal(NarrowingSource.TotalRows, source.RowCount);
	}

	[Fact]
	public void BackspacingToEmpty_ShowsEveryRowAgain()
	{
		var (table, system, window, _) = Build();

		OpenFilter(system);
		Type(system, "Berlin");
		Backspace(system, "Berlin".Length);
		window.RenderAndGetVisibleContent();

		Assert.Equal(NarrowingSource.TotalRows, table.RowCount);
	}

	/// <summary>Typing again after clearing still filters — the source was not left in a bad state.</summary>
	[Fact]
	public void FilteringAgainAfterClearing_StillWorks()
	{
		var (table, system, _, source) = Build();

		OpenFilter(system);
		Type(system, "Berlin");
		Backspace(system, "Berlin".Length);
		Type(system, "Cairo");

		Assert.Equal(1, source.RowCount);
		Assert.Equal(1, table.RowCount);
	}
}

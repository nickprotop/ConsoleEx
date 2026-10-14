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
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// <c>TableControl</c> moved the selection without saying so. Filtering re-seats the cursor on the
/// first matching row, but only the filter events fired, so a consumer tracking the selection
/// through <see cref="TableControl.SelectedRowChanged"/> went on holding the row it last heard
/// about while the table highlighted a different one. In the report that came in against 2.7.0, an
/// electronic signature was recorded against the wrong item.
/// <para>
/// The second half of the same report: in data-source mode there was no way to ask which row is
/// highlighted. <c>SelectedRowIndex</c> is a DISPLAY index whenever the table does the filtering,
/// <c>MapDisplayToData</c> was internal, <c>GetRowTagAt</c> takes a DATA index, and
/// <c>SelectedRow</c> returns null for a data source. <see cref="TableControl.SelectedDataIndex"/>
/// and <see cref="TableControl.SelectedRowTag"/> close that.
/// </para>
/// </summary>
public class TableSelectionEventTests
{
	#region Sources

	/// <summary>
	/// A source that narrows itself, standing in for a server-side WHERE, and that carries a tag per
	/// row so a test can name the row it means independently of any index.
	/// </summary>
	private sealed class TaggedNarrowingSource : ITableDataSource
	{
		private static readonly string[][] AllRows =
		{
			new[] { "Alice", "Athens" },
			new[] { "Bob", "Berlin" },
			new[] { "Carol", "Cairo" },
			new[] { "Dave", "Dublin" },
			new[] { "Erin", "Edinburgh" },
			new[] { "Frank", "Faro" },
		};

		private List<string[]> _visible = new(AllRows);

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public bool CanFilter { get; init; } = true;
		public int RowCount => _visible.Count;
		public int ColumnCount => 2;
		public string GetColumnHeader(int c) => c == 0 ? "Name" : "City";
		public string GetCellValue(int r, int c) => _visible[r][c];

		/// <summary>The tag is the name, so an assertion reads as the row a user would point at.</summary>
		public object? GetRowTag(int rowIndex) =>
			rowIndex >= 0 && rowIndex < _visible.Count ? _visible[rowIndex][0] : null;

		public void ApplyFilter(string filterText, string? columnName, FilterOperator op)
		{
			if (!CanFilter) return;
			_visible = AllRows.Where(row =>
				row.Any(cell => cell.Contains(filterText, StringComparison.OrdinalIgnoreCase))).ToList();
			Raise();
		}

		public void ClearFilter()
		{
			if (!CanFilter) return;
			_visible = new List<string[]>(AllRows);
			Raise();
		}

		private void Raise() => CollectionChanged?.Invoke(this,
			new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		public static int TotalRows => AllRows.Length;
	}

	#endregion

	#region Harness

	/// <summary>
	/// Builds the real thing: a focused table inside a real window inside a real system, so the '/'
	/// filter key is actually read and the selection is exercised through the paths an app uses.
	/// </summary>
	private static (TableControl table, ConsoleWindowSystem system, Window window) Build(
		ITableDataSource? source, IEnumerable<TableRow>? rows = null)
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(100, 30),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));

		// Deliberately short: a 12-row window over 6 rows would never scroll, and the cursor-moved
		// versus row-changed distinction hides where the viewport is smaller than the data.
		var window = new Window(system) { Left = 0, Top = 0, Width = 50, Height = 10 };
		var table = new TableControl { FilteringEnabled = true, ReadOnly = false };

		if (source != null)
		{
			table.DataSource = source;
		}
		if (rows != null)
		{
			// Client-side filtering walks the COLUMNS the table knows about, not the cells a row
			// happens to carry, so a table with no columns matches nothing at all.
			table.AddColumn("Name");
			table.AddColumn("City");
			table.SetData(rows);
		}

		window.AddControl(table);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.FocusControl(table);
		window.RenderAndGetVisibleContent();

		return (table, system, window);
	}

	private static List<TableRow> ClientRows() => new()
	{
		new TableRow("Alice", "Athens") { Tag = "Alice" },
		new TableRow("Bob", "Berlin") { Tag = "Bob" },
		new TableRow("Carol", "Cairo") { Tag = "Carol" },
		new TableRow("Dave", "Dublin") { Tag = "Dave" },
		new TableRow("Erin", "Edinburgh") { Tag = "Erin" },
		new TableRow("Frank", "Faro") { Tag = "Frank" },
	};

	private static void Send(ConsoleWindowSystem system, ConsoleKeyInfo key)
	{
		system.InputStateService.EnqueueKey(key);
		system.Input.ProcessInput();
	}

	/// <summary>Types a filter the way a user does: '/', the text, then Enter.</summary>
	private static void TypeFilter(ConsoleWindowSystem system, string text)
	{
		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		foreach (char c in text)
			Send(system, new ConsoleKeyInfo(c, ConsoleKey.A, false, false, false));
		Send(system, new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
	}

	/// <summary>
	/// Records what a consumer tracking the selection through the event would believe, which is the
	/// thing that went wrong: the tag is read inside the handler, so the test asserts the table was
	/// already consistent when it fired.
	/// </summary>
	private sealed class SelectionTracker
	{
		private readonly TableControl _table;
		public int Raises { get; private set; }
		public int LastIndex { get; private set; } = -2;
		public object? TagSeenInHandler { get; private set; }

		public SelectionTracker(TableControl table)
		{
			_table = table;
			table.SelectedRowChanged += (_, index) =>
			{
				Raises++;
				LastIndex = index;
				TagSeenInHandler = table.SelectedRowTag;
			};
		}

		public void Reset()
		{
			Raises = 0;
			LastIndex = -2;
			TagSeenInHandler = null;
		}
	}

	#endregion

	#region The reported bug: the delegated filter

	/// <summary>
	/// THE REPORTED REPRO, through the real key path. A source that filters itself, a user on row 5,
	/// then an interactive filter. The selection moves to row 0 and that has to be announced.
	/// </summary>
	[Fact]
	public void InteractiveFilter_OnSelfFilteringSource_RaisesSelectedRowChanged()
	{
		var (table, system, window) = Build(new TaggedNarrowingSource());

		table.SelectedRowIndex = 5;
		Assert.Equal("Frank", table.SelectedRowTag);

		var tracker = new SelectionTracker(table);
		TypeFilter(system, "a");

		Assert.True(tracker.Raises >= 1, "applying a filter must announce that the selection moved");
		Assert.Equal(table.SelectedRowIndex, tracker.LastIndex);
		Assert.Equal(0, table.SelectedRowIndex);

		// The consumer's view and the table's view agree — the whole point of the fix.
		Assert.Equal(table.SelectedRowTag, tracker.TagSeenInHandler);
		Assert.NotEqual("Frank", table.SelectedRowTag);

		// And it survives a re-render.
		window.RenderAndGetVisibleContent();
		Assert.Equal(0, table.SelectedRowIndex);
		Assert.Equal(table.SelectedRowTag, tracker.TagSeenInHandler);
	}

	/// <summary>The same through the programmatic API, which is the other half of the repro.</summary>
	[Fact]
	public void ApplyFilter_Delegated_RaisesSelectedRowChanged()
	{
		var (table, _, _) = Build(new TaggedNarrowingSource());
		table.SelectedRowIndex = 5;

		var tracker = new SelectionTracker(table);
		table.ApplyFilter("a");

		Assert.Equal(1, tracker.Raises);
		Assert.Equal(0, table.SelectedRowIndex);
		Assert.Equal(table.SelectedRowTag, tracker.TagSeenInHandler);
	}

	#endregion

	#region Compound client-side filter — where display and data indices diverge

	/// <summary>
	/// A compound expression cannot be pushed into <c>ITableDataSource.ApplyFilter</c>, so the table
	/// evaluates it and <c>SelectedRowIndex</c> becomes a display index into the matches. This is
	/// the case where reading the selection with the old API silently named the wrong row.
	/// </summary>
	[Fact]
	public void CompoundFilter_RaisesAndSelectedDataIndexResolvesTheHighlightedRow()
	{
		var (table, _, _) = Build(new TaggedNarrowingSource { CanFilter = false });
		table.SelectedRowIndex = 5;

		var tracker = new SelectionTracker(table);
		// Pipe is OR; a space would be AND. Matches Carol and Dave only, so display 0 is data 2.
		table.ApplyFilter("Cairo|Dublin");

		Assert.Equal(1, tracker.Raises);
		Assert.Equal(0, table.SelectedRowIndex);
		Assert.Equal(2, table.RowCount);

		// The display index is NOT the data index here; that divergence is the bug's second half.
		Assert.Equal(2, table.SelectedDataIndex);
		Assert.NotEqual(table.SelectedRowIndex, table.SelectedDataIndex);
		Assert.Equal("Carol", table.SelectedRowTag);
		Assert.Equal("Carol", tracker.TagSeenInHandler);

		// Feeding the DISPLAY index to the data-indexed accessor is what used to go wrong.
		Assert.NotEqual(table.GetRowTagAt(table.SelectedRowIndex), table.SelectedRowTag);
		Assert.Equal(table.GetRowTagAt(table.SelectedDataIndex), table.SelectedRowTag);
	}

	/// <summary>Moving within a compound filter keeps the mapping honest.</summary>
	[Fact]
	public void CompoundFilter_MovingSelection_MapsEachDisplayRowToItsDataRow()
	{
		var (table, _, _) = Build(new TaggedNarrowingSource { CanFilter = false });
		table.SelectedRowIndex = 0;
		table.ApplyFilter("Cairo|Dublin");

		table.SelectedRowIndex = 0;
		Assert.Equal("Carol", table.SelectedRowTag);
		Assert.Equal(2, table.SelectedDataIndex);

		table.SelectedRowIndex = 1;
		Assert.Equal("Dave", table.SelectedRowTag);
		Assert.Equal(3, table.SelectedDataIndex);
	}

	#endregion

	#region Clearing the filter

	/// <summary>
	/// Clearing a filter over a DATA SOURCE used to leave the index where it was. The captured
	/// selection is untracked for a data source, so the restore returned before clamping — a display
	/// index into the matches then indexed every row, naming an unrelated row, silently.
	/// </summary>
	[Fact]
	public void ClearFilter_OnDataSource_RaisesAndLeavesAResolvableSelection()
	{
		var (table, _, _) = Build(new TaggedNarrowingSource());
		table.SelectedRowIndex = 0;
		table.ApplyFilter("a");
		table.SelectedRowIndex = table.RowCount - 1;

		var tracker = new SelectionTracker(table);
		table.ClearFilter();

		Assert.Equal(TaggedNarrowingSource.TotalRows, table.RowCount);
		Assert.True(tracker.Raises >= 1, "clearing a filter moves the selection and must announce it");
		Assert.Equal(table.SelectedRowIndex, tracker.LastIndex);

		// Whatever it points at, it resolves and agrees with what the handler saw.
		Assert.InRange(table.SelectedRowIndex, 0, table.RowCount - 1);
		Assert.Equal(table.SelectedRowTag, tracker.TagSeenInHandler);
		Assert.NotNull(table.SelectedRowTag);
	}

	/// <summary>
	/// For the table's OWN rows, clearing a filter deliberately KEEPS the row the user was on — every
	/// row is shown again, so it is still there. The cursor's index moves but the row does not, and
	/// that is specifically not a selection change. This guards the fix against over-raising.
	/// </summary>
	[Fact]
	public void ClearFilter_OnClientRows_KeepsTheRowAndDoesNotRaise()
	{
		var (table, _, _) = Build(null, ClientRows());
		// Start from a real selection: a view that never selected a row keeps that through a filter.
		table.SelectedRowIndex = 0;
		table.ApplyFilter("Cairo");
		Assert.Equal("Carol", table.SelectedRowTag);

		var tracker = new SelectionTracker(table);
		table.ClearFilter();

		Assert.Equal(6, table.RowCount);
		// Same row, so no SelectedRowChanged — only the index it sits at changed.
		Assert.Equal(0, tracker.Raises);
		Assert.Equal("Carol", table.SelectedRowTag);
		Assert.Equal(2, table.SelectedDataIndex);
	}

	#endregion

	#region Live typing

	/// <summary>
	/// Live typing narrows on every keystroke, each of which can re-seat the cursor. It used to be
	/// the quietest path of all: only <c>FilterTextChanged</c> fired.
	/// </summary>
	[Fact]
	public void LiveTyping_RaisesWhenTheHighlightedRowChanges()
	{
		var (table, system, _) = Build(new TaggedNarrowingSource { CanFilter = false });
		table.SelectedRowIndex = 5;
		Assert.Equal("Frank", table.SelectedRowTag);

		var tracker = new SelectionTracker(table);

		// '/' then a single character, without Enter: the live path, not the confirmed one.
		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		Send(system, new ConsoleKeyInfo('C', ConsoleKey.C, false, false, false));

		Assert.True(tracker.Raises >= 1, "live typing moved the cursor off Frank without saying so");
		Assert.Equal(table.SelectedRowIndex, tracker.LastIndex);
		Assert.Equal(table.SelectedRowTag, tracker.TagSeenInHandler);
		Assert.NotEqual("Frank", table.SelectedRowTag);
	}

	/// <summary>Each keystroke that re-seats the cursor announces it exactly once.</summary>
	[Fact]
	public void LiveTyping_RaisesOncePerKeystrokeThatMoves()
	{
		var (table, system, _) = Build(new TaggedNarrowingSource { CanFilter = false });
		table.SelectedRowIndex = 4;

		var tracker = new SelectionTracker(table);

		// '/' opens the filter; the first character narrows to Carol and moves the cursor there.
		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		Send(system, new ConsoleKeyInfo('C', ConsoleKey.C, false, false, false));

		// One move, announced once — not once per internal reset.
		Assert.Equal(1, tracker.Raises);
		Assert.Equal(0, table.SelectedRowIndex);
		Assert.Equal(table.SelectedRowTag, tracker.TagSeenInHandler);
	}

	#endregion

	#region Sort and Reset

	/// <summary>
	/// Sorting reorders rows under the cursor. For the table's own rows the selection follows its
	/// row, so the row does not change and nothing is raised — but the data index must track it.
	/// </summary>
	[Fact]
	public void Sort_OnClientRows_FollowsTheRowAndKeepsTagStable()
	{
		var (table, _, _) = Build(null, ClientRows());
		table.SelectedRowIndex = 0;
		Assert.Equal("Alice", table.SelectedRowTag);

		var tracker = new SelectionTracker(table);
		table.SortByColumn(0);

		// Whatever the order, the selection follows its own row, so the row does not change.
		Assert.Equal(0, tracker.Raises);
		Assert.Equal("Alice", table.SelectedRowTag);
		Assert.Equal(table.GetRowTagAt(table.SelectedDataIndex), table.SelectedRowTag);
	}

	/// <summary>A source Reset replaces every row, so the selected row is not the row it was.</summary>
	[Fact]
	public void SourceReset_RaisesSelectedRowChanged()
	{
		var source = new TaggedNarrowingSource();
		var (table, _, _) = Build(source);
		table.SelectedRowIndex = 5;

		var tracker = new SelectionTracker(table);
		// ApplyFilter on the source raises Reset without the table asking for it.
		source.ApplyFilter("Cairo", null, FilterOperator.Contains);

		Assert.True(tracker.Raises >= 1, "a source Reset replaces the rows and must announce it");
		Assert.Equal(table.SelectedRowIndex, tracker.LastIndex);
		Assert.Equal(table.SelectedRowTag, tracker.TagSeenInHandler);
	}

	/// <summary>
	/// A view that never selected a row must not acquire one because its rows changed. A
	/// follow-the-tail log pane is exactly this: it leaves the selection at -1 and rebuilds on every
	/// append once its buffer is full. Moving that cursor to row 0 invented a selection, and
	/// <c>LogViewerControl</c> opens its detail pane on <see cref="TableControl.SelectedRowChanged"/>,
	/// so the invented selection shrank the table under the user.
	/// </summary>
	[Fact]
	public void NoSelection_SurvivesASourceReset()
	{
		var source = new TaggedNarrowingSource();
		var (table, _, _) = Build(source);
		table.SelectedRowIndex = -1;

		var tracker = new SelectionTracker(table);

		// A source Reset is how an evicting buffer rebuilds, so it must not seat a cursor.
		source.ApplyFilter("Cairo", null, FilterOperator.Contains);
		Assert.Equal(-1, table.SelectedRowIndex);
		Assert.Equal(0, tracker.Raises);
		Assert.Null(table.SelectedRowTag);

		source.ClearFilter();
		Assert.Equal(-1, table.SelectedRowIndex);
		Assert.Equal(0, tracker.Raises);
	}

	#endregion

	#region The new API in isolation

	/// <summary>With nothing filtered the two indices agree, which is why the bug stayed hidden.</summary>
	[Fact]
	public void Unfiltered_DisplayAndDataIndicesAgree()
	{
		var (table, _, _) = Build(new TaggedNarrowingSource());

		for (int i = 0; i < table.RowCount; i++)
		{
			table.SelectedRowIndex = i;
			Assert.Equal(i, table.SelectedDataIndex);
			Assert.Equal(table.GetRowTagAt(i), table.SelectedRowTag);
		}
	}

	/// <summary>
	/// A delegated filter leaves the indices equal too — the source reports only matches, so its own
	/// indices already are the displayed ones.
	/// </summary>
	[Fact]
	public void DelegatedFilter_IndicesAgreeBecauseTheSourceReportsOnlyMatches()
	{
		var (table, _, _) = Build(new TaggedNarrowingSource());
		table.SelectedRowIndex = 5;
		table.ApplyFilter("a");

		Assert.Equal(table.SelectedRowIndex, table.SelectedDataIndex);
		Assert.Equal(table.GetRowTagAt(table.SelectedDataIndex), table.SelectedRowTag);
	}

	[Fact]
	public void NoSelection_SelectedDataIndexIsMinusOneAndTagIsNull()
	{
		var (table, _, _) = Build(new TaggedNarrowingSource());
		table.SelectedRowIndex = -1;

		Assert.Equal(-1, table.SelectedDataIndex);
		Assert.Null(table.SelectedRowTag);
	}

	/// <summary>
	/// <see cref="TableControl.SelectedRowTag"/> works in data-source mode, where
	/// <see cref="TableControl.SelectedRow"/> is null by design — the gap the report named.
	/// </summary>
	[Fact]
	public void DataSourceMode_SelectedRowIsNullButTagResolves()
	{
		var (table, _, _) = Build(new TaggedNarrowingSource());
		table.SelectedRowIndex = 3;

		Assert.Null(table.SelectedRow);
		Assert.Equal("Dave", table.SelectedRowTag);
		Assert.Equal(3, table.SelectedDataIndex);
	}

	/// <summary>And it agrees with <see cref="TableControl.SelectedRow"/> for the table's own rows.</summary>
	[Fact]
	public void ClientMode_TagAgreesWithSelectedRow()
	{
		var (table, _, _) = Build(null, ClientRows());
		table.SelectedRowIndex = 0;
		table.ApplyFilter("Cairo|Dublin");
		table.SelectedRowIndex = 1;

		Assert.Equal("Dave", table.SelectedRowTag);
		Assert.Same(table.SelectedRow, table.GetRow(table.SelectedDataIndex));
		Assert.Equal(table.SelectedRow?.Tag, table.SelectedRowTag);
	}

	#endregion
}

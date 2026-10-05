// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
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

	#region Deciding which rows are displayed

	/// <summary>
	/// Hides rows whose name starts with "-" unless <see cref="ShowHidden"/> is set, on top of the
	/// table's own filter and sort, and records what it was asked.
	/// </summary>
	private sealed class HidingTable : TableControl
	{
		public bool ShowHidden { get; set; }
		public List<TableDisplayQuery> Queries { get; } = new();
		public Func<int, int> StandIn { get; set; } = _ => -1;
		public Action? DuringCompute { get; set; }
		public Action? DuringResolve { get; set; }

		public void Refresh() => RefreshDisplayRows();

		protected override int[]? ComputeDisplayRows(TableDisplayQuery query)
		{
			Queries.Add(query);
			DuringCompute?.Invoke();

			int[]? computed = base.ComputeDisplayRows(query);
			if (DataSource != null || ShowHidden) return computed;

			int[] rows = computed ?? Enumerable.Range(0, DataRowCount).ToArray();
			return rows.Where(row => !GetRow(row).Cells[0].StartsWith('-')).ToArray();
		}

		protected override int ResolveHiddenSelectedRow(int dataIndex)
		{
			DuringResolve?.Invoke();
			return StandIn(dataIndex);
		}
	}

	private static HidingTable Hiding()
	{
		var table = new HidingTable { SortingEnabled = true };
		table.AddColumn("Name");
		table.AddRow("Alice");
		table.AddRow("-Bob");
		table.AddRow("Carol");
		return table;
	}

	private static List<string> Displayed(TableControl table)
		=> Enumerable.Range(0, table.RowCount).Select(i => table.GetRow(table.MapDisplayToData(i)).Cells[0]).ToList();

	[Fact]
	public void AnOverride_DecidesWhichRowsAreDisplayed()
	{
		var table = Hiding();

		Assert.Equal(2, table.RowCount);
		Assert.Equal(["Alice", "Carol"], Displayed(table));
	}

	[Fact]
	public void TheOverride_IsAskedEvenWithNothingSortedOrFiltered()
	{
		var table = Hiding();

		var last = table.Queries[^1];
		Assert.False(last.IsFiltered);
		Assert.False(last.IsSorted);
	}

	[Fact]
	public void TheOverride_IsGivenTheTablesFilterAndSort()
	{
		var table = Hiding();
		table.SortByColumn(0);
		table.SortByColumn(0);

		table.ApplyFilter("a");

		var last = table.Queries[^1];
		Assert.Equal("a", last.Filter?.RawText);
		Assert.Equal(FilterMode.Confirmed, last.FilterMode);
		Assert.Equal(0, last.SortColumnIndex);
		Assert.Equal(SortDirection.Descending, last.SortDirection);
		Assert.True(last.IsSorted);
		Assert.Equal(["Carol", "Alice"], Displayed(table));
	}

	[Fact]
	public void RefreshDisplayRows_ShowsTheOverridesNewAnswer_AndKeepsTheCursorOnItsRow()
	{
		var table = Hiding();
		table.SelectedRowIndex = 1;                     // Carol

		table.ShowHidden = true;
		table.Refresh();

		Assert.Equal(["Alice", "-Bob", "Carol"], Displayed(table));
		Assert.Equal("Carol", table.SelectedRow?.Cells[0]);
		Assert.Equal(2, table.SelectedRowIndex);
	}

	[Fact]
	public void RefreshDisplayRows_SendsAHiddenCursorToTheRowTheTableNames()
	{
		var table = Hiding();
		table.ShowHidden = true;
		table.Refresh();
		table.SelectedRowIndex = 1;                     // -Bob
		table.StandIn = dataIndex => dataIndex - 1;     // the row above it
		var changed = new List<string?>();
		table.SelectedRowItemChanged += (_, row) => changed.Add(row?.Cells[0]);

		table.ShowHidden = false;
		table.Refresh();

		Assert.Equal("Alice", table.SelectedRow?.Cells[0]);
		Assert.Equal(["Alice"], changed);
	}

	[Fact]
	public void WithoutAStandIn_AHiddenCursorLandsOnTheRowNowAtItsPosition()
	{
		var table = Hiding();
		table.ShowHidden = true;
		table.Refresh();
		table.SelectedRowIndex = 1;                     // -Bob

		table.ShowHidden = false;
		table.Refresh();

		Assert.Equal("Carol", table.SelectedRow?.Cells[0]);
	}

	[Fact]
	public void ChangingTheRowsFromComputeDisplayRows_Throws_AndLeavesTheTableUsable()
	{
		var table = Hiding();
		table.DuringCompute = () => table.AddRow("Intruder");

		Assert.Throws<InvalidOperationException>(() => table.AddRow("Dave"));

		table.DuringCompute = null;
		table.AddRow("Eve");
		Assert.Contains("Eve", Displayed(table));
		Assert.DoesNotContain("Intruder", Displayed(table));
	}

	[Fact]
	public void RefreshingFromResolveHiddenSelectedRow_Throws()
	{
		var table = Hiding();
		table.ShowHidden = true;
		table.Refresh();
		table.SelectedRowIndex = 1;
		table.DuringResolve = () => table.Refresh();

		table.ShowHidden = false;

		Assert.Throws<InvalidOperationException>(() => table.Refresh());
	}

	[Fact]
	public void WithADataSource_TheOverrideIsAskedOnlyWhileFilteringClientSide()
	{
		var table = new HidingTable { SortingEnabled = true, DataSource = new LetterSource() };
		int before = table.Queries.Count;

		table.SortByColumn(0);
		Assert.Equal(before, table.Queries.Count);

		table.ApplyFilter("b");
		Assert.Equal(before + 1, table.Queries.Count);
	}

	#endregion

	#region A prefix in front of a cell's value

	/// <summary>Draws <see cref="Prefix"/> in front of cells, and records which cells it was asked about.</summary>
	private sealed class PrefixTable : TableControl
	{
		public Func<int, int, string?> Prefix { get; set; } = (_, _) => null;
		public HashSet<int> AskedColumns { get; } = new();

		protected override string? GetCellPrefixMarkup(int dataRowIndex, int columnIndex)
		{
			AskedColumns.Add(columnIndex);
			return Prefix(dataRowIndex, columnIndex);
		}
	}

	/// <summary>A borderless table with one column of names, header on line 0, rows from line 1.</summary>
	private static PrefixTable Prefixed(string prefix, int? width = null,
		TextJustification alignment = TextJustification.Left, params string[] names)
	{
		var table = new PrefixTable { BorderStyle = BorderStyle.None, Prefix = (_, _) => prefix };
		table.AddColumn("Name", alignment, width);
		foreach (var name in names.Length > 0 ? names : ["Alice", "Bob"])
			table.AddRow(name);
		return table;
	}

	private static CharacterBuffer PaintTable(TableControl table)
	{
		var buffer = new CharacterBuffer(30, 6);
		var bounds = new LayoutRect(0, 0, 30, 6);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return buffer;
	}

	private static string Line(CharacterBuffer buffer, int y, int width = 30)
		=> string.Concat(Enumerable.Range(0, width).Select(x => buffer.GetCell(x, y).Character.ToString())).TrimEnd();

	[Fact]
	public void APrefix_IsDrawnInFrontOfTheValue()
	{
		var buffer = PaintTable(Prefixed(">> "));

		Assert.Equal(">> Alice", Line(buffer, 1));
	}

	[Fact]
	public void TheHeader_GetsNoPrefix()
	{
		var buffer = PaintTable(Prefixed(">> "));

		Assert.StartsWith("Name", Line(buffer, 0));
	}

	[Fact]
	public void AnAlignedValue_IsAlignedInWhatThePrefixLeaves()
	{
		var buffer = PaintTable(Prefixed("> ", width: 10, alignment: TextJustification.Right));

		Assert.Equal(">    Alice", Line(buffer, 1, width: 10));
	}

	[Fact]
	public void ALongValue_IsCutWhileThePrefixIsKept()
	{
		var buffer = PaintTable(Prefixed("> ", width: 6, names: "Alexandra"));

		Assert.Equal("> Alex", Line(buffer, 1, width: 6));
	}

	[Fact]
	public void AnAutoWidthColumn_MakesRoomForThePrefix()
	{
		var buffer = PaintTable(Prefixed("--> "));

		Assert.Equal("--> Alice", Line(buffer, 1));
	}

	[Fact]
	public void AWideGlyphCutAtTheCellsEdge_IsBlankedNotHalved()
	{
		var buffer = PaintTable(Prefixed("ab漢", width: 3));

		Assert.Equal("ab ", Line(buffer, 1, width: 3).PadRight(3));
	}

	[Fact]
	public void AFilterHighlight_FallsOnTheValueOnly()
	{
		// Applying a filter selects its first match, and a selected row is not highlighted, so the
		// second match is the one to look at.
		var table = Prefixed("li ", names: ["Alice", "Elias"]);
		table.ApplyFilter("li");

		var buffer = PaintTable(table);

		Assert.Equal("li Elias", Line(buffer, 2));
		Assert.NotEqual(Color.DarkYellow, buffer.GetCell(0, 2).Background);
		Assert.NotEqual(Color.DarkYellow, buffer.GetCell(1, 2).Background);
		Assert.Equal(Color.DarkYellow, buffer.GetCell(4, 2).Background);
		Assert.Equal(Color.DarkYellow, buffer.GetCell(5, 2).Background);
	}

	[Fact]
	public void EditingACell_EditsTheValueAfterThePrefix()
	{
		var table = Prefixed(">> ");
		table.ReadOnly = false;
		table.InlineEditingEnabled = true;
		table.SelectedRowIndex = 0;
		table.SelectedColumnIndex = 0;
		string? oldValue = null;
		table.CellEditCompleted += (_, edit) => oldValue = edit.OldValue;

		table.BeginCellEdit();
		var buffer = PaintTable(table);
		table.CommitEdit();

		Assert.Equal(">> Alice", Line(buffer, 1));
		Assert.Equal("Alice", oldValue);
		Assert.Equal("Alice", table.GetCell(0, 0));
	}

	[Fact]
	public void ASelectedRow_DrawsThePrefixInTheRowsColours()
	{
		var table = Prefixed("[red]>>[/] ");
		table.ReadOnly = false;
		table.SelectedRowIndex = 0;

		var buffer = PaintTable(table);

		Assert.Equal(buffer.GetCell(3, 1).Foreground, buffer.GetCell(0, 1).Foreground);
		Assert.Equal(buffer.GetCell(3, 1).Background, buffer.GetCell(0, 1).Background);
	}

	[Fact]
	public void AnUnselectedRow_KeepsThePrefixsOwnColours()
	{
		var buffer = PaintTable(Prefixed("[red]>>[/] "));

		Assert.Equal(Color.Red, buffer.GetCell(0, 1).Foreground);
	}

	[Fact]
	public void InCheckboxMode_ThePrefixIsAskedForDataColumnsAndDrawnAfterTheCheckbox()
	{
		var table = Prefixed(">");
		table.AddColumn("Other");
		table.CheckboxMode = true;

		var buffer = PaintTable(table);

		Assert.Equal([0, 1], table.AskedColumns.Order());
		Assert.StartsWith("[ ] >Alice", Line(buffer, 1));
	}

	#endregion

	#region Hearing that a row's cells changed

	/// <summary>Records content changes, and can re-sort when one arrives.</summary>
	private sealed class ContentWatchingTable : TableControl
	{
		public List<string> Changed { get; } = new();
		public List<bool> WasEditing { get; } = new();
		public List<bool> HeldTheLock { get; } = new();
		public bool RefreshOnChange { get; set; }

		protected override void OnRowContentChanged(TableRow row)
		{
			Changed.Add(row.Cells[0]);
			WasEditing.Add(IsEditing);
			HeldTheLock.Add(Monitor.IsEntered(SyncRoot));
			if (RefreshOnChange)
				RefreshDisplayRows();
		}
	}

	private static ContentWatchingTable Watching()
	{
		var table = new ContentWatchingTable { SortingEnabled = true, ReadOnly = false };
		table.AddColumn("Name");
		table.AddRow("Alice");
		table.AddRow("Bob");
		table.AddRow("Carol");
		return table;
	}

	[Fact]
	public void UpdatingACell_IsReported_OutsideTheLock()
	{
		var table = Watching();

		table.UpdateCell(1, 0, "Bea");

		Assert.Equal(["Bea"], table.Changed);
		Assert.Equal([false], table.HeldTheLock);
	}

	[Fact]
	public void ChangingACellThroughItsRow_IsReported()
	{
		var table = Watching();

		table.GetRow(2).Cells[0] = "Cleo";

		Assert.Equal(["Cleo"], table.Changed);
	}

	[Fact]
	public void CommittingAnEdit_IsReported_AfterTheEditEnded()
	{
		var table = Watching();
		table.InlineEditingEnabled = true;
		table.SelectedRowIndex = 0;
		table.SelectedColumnIndex = 0;
		table.BeginCellEdit();

		table.CommitEdit();

		Assert.Equal(["Alice"], table.Changed);
		Assert.Equal([false], table.WasEditing);
		Assert.Equal([false], table.HeldTheLock);
	}

	[Fact]
	public void ChangingARowsColours_IsNotAContentChange()
	{
		var table = Watching();

		table.GetRow(0).ForegroundColor = Color.Red;
		table.GetRow(0).IsEnabled = false;

		Assert.Empty(table.Changed);
	}

	[Fact]
	public void ATableThatRefreshesOnAChange_ReSortsAndKeepsTheSelectionOnItsRow()
	{
		var table = Watching();
		table.SortByColumn(0);
		table.SelectedRowIndex = 1;                     // Bob
		table.RefreshOnChange = true;

		table.UpdateCell(1, 0, "Zed");

		Assert.Equal(["Alice", "Carol", "Zed"], Displayed(table));
		Assert.Equal("Zed", table.SelectedRow?.Cells[0]);
	}

	#endregion

	#region Handling keys before the table does

	/// <summary>Offers every key to <see cref="Handler"/>, and records what it was offered.</summary>
	private sealed class KeyTable : TableControl
	{
		public Func<ConsoleKeyInfo, bool> Handler { get; set; } = _ => false;
		public List<ConsoleKey> Offered { get; } = new();

		protected override bool TryHandleKey(ConsoleKeyInfo key)
		{
			Offered.Add(key.Key);
			return Handler(key);
		}
	}

	private static ConsoleKeyInfo Press(ConsoleKey key, char ch = '\0') => new(ch, key, false, false, false);

	/// <summary>An interactive table hosted in a window and given the focus, so keys reach it.</summary>
	private static KeyTable FocusedKeyTable(bool withRows = true)
	{
		var table = new KeyTable { ReadOnly = false, FilteringEnabled = true, InlineEditingEnabled = true };
		table.AddColumn("Name");
		if (withRows)
		{
			table.AddRow("Alice");
			table.AddRow("Bob");
		}

		var system = Infrastructure.TestWindowSystemBuilder.CreateTestSystem(60, 20);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
		window.AddControl(table);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.FocusControl(table);
		return table;
	}

	[Fact]
	public void AHandledKey_IsNotActedOnByTheTable()
	{
		var table = FocusedKeyTable();
		table.SelectedRowIndex = 0;
		table.Handler = key => key.Key == ConsoleKey.DownArrow;

		bool handled = table.ProcessKey(Press(ConsoleKey.DownArrow));

		Assert.True(handled);
		Assert.Equal(0, table.SelectedRowIndex);
	}

	[Fact]
	public void AnUnhandledKey_IsActedOnAsBefore()
	{
		var table = FocusedKeyTable();
		table.SelectedRowIndex = 0;

		table.ProcessKey(Press(ConsoleKey.DownArrow));

		Assert.Equal([ConsoleKey.DownArrow], table.Offered);
		Assert.Equal(1, table.SelectedRowIndex);
	}

	[Fact]
	public void KeysTypedIntoAFilter_AreNotOffered()
	{
		var table = FocusedKeyTable();
		table.ProcessKey(Press(ConsoleKey.Oem2, '/'));
		table.Offered.Clear();

		table.ProcessKey(Press(ConsoleKey.DownArrow));
		table.ProcessKey(Press(ConsoleKey.B, 'b'));

		Assert.Empty(table.Offered);
		Assert.Equal("b", table._filterBuffer);
	}

	[Fact]
	public void KeysTypedIntoAnEdit_AreNotOffered()
	{
		var table = FocusedKeyTable();
		table.SelectedRowIndex = 0;
		table.SelectedColumnIndex = 0;
		table.BeginCellEdit();

		table.ProcessKey(Press(ConsoleKey.LeftArrow));

		Assert.Empty(table.Offered);
		Assert.True(table.IsEditing);
	}

	[Fact]
	public void KeysAreOffered_EvenToATableWithNoRows()
	{
		var table = FocusedKeyTable(withRows: false);
		table.Handler = _ => true;

		Assert.True(table.ProcessKey(Press(ConsoleKey.Insert)));
		Assert.Equal([ConsoleKey.Insert], table.Offered);
	}

	#endregion

	#region Handling clicks before the table does

	/// <summary>Offers every click to <see cref="Handler"/>, and records what it was offered.</summary>
	private sealed class ClickTable : TableControl
	{
		public Func<TableHitTestResult, int, bool> Handler { get; set; } = (_, _) => false;
		public List<(TableHitTestResult Hit, int Count)> Offered { get; } = new();

		protected override bool TryHandleClick(TableHitTestResult hit, int clickCount, SharpConsoleUI.Events.MouseEventArgs args)
		{
			Offered.Add((hit, clickCount));
			return Handler(hit, clickCount);
		}
	}

	/// <summary>A borderless interactive table with a header on line 0 and rows from line 1, painted.</summary>
	private static ClickTable PaintedClickTable()
	{
		var table = new ClickTable { BorderStyle = BorderStyle.None, ReadOnly = false, SortingEnabled = true };
		table.AddColumn("Name");
		table.AddRow("Alice");
		table.AddRow("Bob");
		PaintTable(table);
		return table;
	}

	private static void ClickAt(TableControl table, int y, SharpConsoleUI.Drivers.MouseFlags flag = SharpConsoleUI.Drivers.MouseFlags.Button1Clicked)
	{
		var point = new System.Drawing.Point(1, y);
		table.ProcessMouseEvent(new SharpConsoleUI.Events.MouseEventArgs(new List<SharpConsoleUI.Drivers.MouseFlags> { flag }, point, point, point));
	}

	[Fact]
	public void TheHook_IsToldWhatTheClickLandedOn()
	{
		var table = PaintedClickTable();

		ClickAt(table, 2);

		var (hit, count) = Assert.Single(table.Offered);
		Assert.Equal(TableHitZone.Cell, hit.Zone);
		Assert.Equal(1, hit.DisplayRowIndex);
		Assert.Equal(0, hit.ColumnIndex);
		Assert.Equal(1, count);
	}

	[Fact]
	public void AHandledHeaderClick_DoesNotSort_ButIsStillReportedAsAClick()
	{
		var table = PaintedClickTable();
		table.Handler = (hit, _) => hit.Zone == TableHitZone.Header;
		int headerClicks = 0, clicks = 0;
		table.HeaderClicked += (_, _) => headerClicks++;
		table.MouseClick += (_, _) => clicks++;

		ClickAt(table, 0);

		Assert.Equal(-1, table.SortColumnIndex);
		Assert.Equal(0, headerClicks);
		Assert.Equal(1, clicks);
	}

	[Fact]
	public void ASecondQuickClickOnTheSameRow_CountsAsTwo()
	{
		var table = PaintedClickTable();
		var activated = new List<int>();
		table.RowActivated += (_, row) => activated.Add(row);

		ClickAt(table, 2);
		ClickAt(table, 2);

		Assert.Equal([1, 2], table.Offered.Select(o => o.Count));
		Assert.Equal([1], activated);
	}

	[Fact]
	public void AHandledSecondClick_DoesNotActivateTheRow()
	{
		var table = PaintedClickTable();
		table.Handler = (_, count) => count == 2;
		var activated = new List<int>();
		table.RowActivated += (_, row) => activated.Add(row);

		ClickAt(table, 2);
		ClickAt(table, 2);

		Assert.Empty(activated);
	}

	[Fact]
	public void AHandledClick_IsNotPairedWithTheNextOne()
	{
		var table = PaintedClickTable();
		bool first = true;
		table.Handler = (_, _) => { bool take = first; first = false; return take; };
		var activated = new List<int>();
		table.RowActivated += (_, row) => activated.Add(row);

		ClickAt(table, 2);
		ClickAt(table, 2);

		Assert.Equal([1, 1], table.Offered.Select(o => o.Count));
		Assert.Empty(activated);
	}

	[Fact]
	public void ADoubleClickTheTerminalReports_IsOfferedAsTwo_AndStillReported()
	{
		var table = PaintedClickTable();
		table.Handler = (_, _) => true;
		var activated = new List<int>();
		int doubleClicks = 0;
		table.RowActivated += (_, row) => activated.Add(row);
		table.MouseDoubleClick += (_, _) => doubleClicks++;

		ClickAt(table, 1, SharpConsoleUI.Drivers.MouseFlags.Button1DoubleClicked);

		Assert.Equal(2, Assert.Single(table.Offered).Count);
		Assert.Empty(activated);
		Assert.Equal(1, doubleClicks);
	}

	#endregion

	#region Data sources

	/// <summary>Three rows of one letter each; sorts itself, never filters.</summary>
	private sealed class LetterSource : ITableDataSource
	{
		private readonly string[] _letters = ["c", "a", "b"];

		public event System.Collections.Specialized.NotifyCollectionChangedEventHandler? CollectionChanged { add { } remove { } }

		public int RowCount => _letters.Length;
		public int ColumnCount => 1;
		public string GetColumnHeader(int columnIndex) => "Letter";
		public string GetCellValue(int rowIndex, int columnIndex) => _letters[rowIndex];
		public bool CanSort(int columnIndex) => true;
	}

	#endregion
}

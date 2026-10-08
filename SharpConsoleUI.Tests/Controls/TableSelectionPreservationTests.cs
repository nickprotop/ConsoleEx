// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// The selection belongs to rows, not to display positions: when rows are added, removed or
/// replaced, the cursor, the multi-selection and the range anchor stay on the rows they were on,
/// and the selection events fire only when the selected row actually changes.
/// </summary>
/// <remarks>
/// Every mutator used to adjust the selection by index arithmetic in data order, when the
/// selection is kept in display order, or not at all: removing a row left the multi-selection and
/// the anchor where they were, so they slid onto the next rows, and under a sort the cursor's own
/// arithmetic compared a display index with a data index.
/// </remarks>
public class TableSelectionPreservationTests
{
	#region Helpers

	private static readonly string[] People = ["Alice", "Bob", "Carol", "Dave", "Eve"];

	private static TableControl PeopleTable(bool multiSelect = false)
	{
		var table = new TableControl
		{
			MultiSelectEnabled = multiSelect,
			RightClickExtendsSelection = multiSelect,
			SortingEnabled = true,
		};
		table.AddColumn("Name");
		foreach (var name in People)
			table.AddRow(name);
		return table;
	}

	private static string? SelectedName(TableControl table) => table.SelectedRow?.Cells[0];

	private static List<string> SelectedNames(TableControl table)
		=> table.GetSelectedRows().Select(row => row.Cells[0]).ToList();

	/// <summary>Records the selection events a table raises.</summary>
	private sealed class SelectionEvents
	{
		public readonly List<int> RowChanged = new();
		public readonly List<string?> ItemChanged = new();
		public readonly List<string?> PropertyChanged = new();
		public readonly List<int> MultiSelectionChanged = new();

		public SelectionEvents(TableControl table)
		{
			table.SelectedRowChanged += (_, index) => RowChanged.Add(index);
			table.SelectedRowItemChanged += (_, row) => ItemChanged.Add(row?.Cells[0]);
			table.MultiSelectionChanged += (_, count) => MultiSelectionChanged.Add(count);
			((INotifyPropertyChanged)table).PropertyChanged += (_, e) => PropertyChanged.Add(e.PropertyName);
		}
	}

	#endregion

	#region The cursor stays on its row

	[Fact]
	public void RemovingARowAboveTheCursor_KeepsTheCursorOnItsRow()
	{
		var table = PeopleTable();
		table.SelectedRowIndex = 3;

		table.RemoveRow(1);

		Assert.Equal("Dave", SelectedName(table));
		Assert.Equal(2, table.SelectedRowIndex);
	}

	[Fact]
	public void RemovingARowUnderASort_KeepsTheCursorOnItsRow()
	{
		var table = PeopleTable();
		table.SortByColumn(0);
		table.SortByColumn(0);                         // Eve, Dave, Carol, Bob, Alice
		table.SelectedRowIndex = 1;                    // Dave

		table.RemoveRow(4);                            // Eve, by data index

		Assert.Equal("Dave", SelectedName(table));
	}

	[Fact]
	public void InsertingRowsUnderASort_KeepsTheCursorOnItsRow()
	{
		var table = PeopleTable();
		table.SortByColumn(0);
		table.SortByColumn(0);                         // Eve, Dave, Carol, Bob, Alice
		table.SelectedRowIndex = 3;                    // Bob

		table.InsertRow(0, "Zed");

		Assert.Equal("Bob", SelectedName(table));
	}

	[Fact]
	public void AppendingARowUnderASort_KeepsTheCursorOnItsRow()
	{
		var table = PeopleTable();
		table.SortByColumn(0);
		table.SortByColumn(0);
		table.SelectedRowIndex = 0;                    // Eve

		table.AddRow("Frank");

		Assert.Equal("Eve", SelectedName(table));
	}

	[Fact]
	public void ReplacingTheRows_KeepsTheCursorOnARowThatStays()
	{
		var table = PeopleTable();
		var dave = table.GetRow(3);
		table.SelectedRowIndex = 3;

		table.SetData([new TableRow("Zed"), dave]);

		Assert.Same(dave, table.SelectedRow);
		Assert.Equal(1, table.SelectedRowIndex);
	}

	#endregion

	#region Events fire when the selected row changes, not when it only moves

	[Fact]
	public void RemovingTheSelectedRow_SelectsTheRowNowInItsPlace_AndSaysSo()
	{
		var table = PeopleTable();
		table.SelectedRowIndex = 1;
		var events = new SelectionEvents(table);

		table.RemoveRow(1);

		Assert.Equal("Carol", SelectedName(table));
		Assert.Equal([1], events.RowChanged);
		Assert.Equal(["Carol"], events.ItemChanged);
	}

	[Fact]
	public void RemovingTheLastRowWhileSelected_SelectsTheNewLastRow()
	{
		var table = PeopleTable();
		table.SelectedRowIndex = 4;

		table.RemoveRow(4);

		Assert.Equal("Dave", SelectedName(table));
		Assert.Equal(3, table.SelectedRowIndex);
	}

	[Fact]
	public void RemovingTheOnlyRowWhileSelected_ClearsTheSelection()
	{
		var table = new TableControl();
		table.AddColumn("Name");
		table.AddRow("Solo");
		table.SelectedRowIndex = 0;
		var events = new SelectionEvents(table);

		table.RemoveRow(0);

		Assert.Equal(-1, table.SelectedRowIndex);
		Assert.Equal([-1], events.RowChanged);
		Assert.Equal([null], events.ItemChanged);
	}

	[Fact]
	public void ACursorThatOnlyMoves_ReportsItsNewIndexButNotANewRow()
	{
		var table = PeopleTable();
		table.SelectedRowIndex = 3;
		var events = new SelectionEvents(table);

		table.RemoveRow(0);

		Assert.Empty(events.RowChanged);
		Assert.Empty(events.ItemChanged);
		Assert.Contains(nameof(TableControl.SelectedRowIndex), events.PropertyChanged);
	}

	[Fact]
	public void ARowChangeBelowTheCursor_RaisesNothing()
	{
		var table = PeopleTable();
		table.SelectedRowIndex = 1;
		var events = new SelectionEvents(table);

		table.RemoveRow(3);

		Assert.Empty(events.RowChanged);
		Assert.DoesNotContain(nameof(TableControl.SelectedRowIndex), events.PropertyChanged);
	}

	[Fact]
	public void RemovingTheRowBeingEdited_CancelsTheEditFirst()
	{
		var table = PeopleTable();
		table.ReadOnly = false;
		table.InlineEditingEnabled = true;
		table.SelectedRowIndex = 2;
		table.SelectedColumnIndex = 0;
		table.BeginCellEdit();
		(int Row, int Column)? cancelled = null;
		table.CellEditCancelled += (_, cell) => cancelled = cell;

		table.RemoveRow(2);

		Assert.False(table.IsEditing);
		Assert.Equal((2, 0), cancelled);
	}

	[Fact]
	public void RemovingAnotherRow_KeepsEditingTheSameRow()
	{
		var table = PeopleTable();
		table.ReadOnly = false;
		table.InlineEditingEnabled = true;
		table.SelectedRowIndex = 2;
		table.SelectedColumnIndex = 0;
		table.BeginCellEdit();

		table.RemoveRow(0);

		Assert.True(table.IsEditing);
		Assert.Equal("Carol", SelectedName(table));
	}

	#endregion

	#region The multi-selection and the anchor stay on their rows

	[Fact]
	public void RemovingARowAboveTheMultiSelection_KeepsItOnItsRows()
	{
		var table = PeopleTable(multiSelect: true);
		table.ToggleRowSelection(2);
		table.ToggleRowSelection(3);

		table.RemoveRow(0);

		Assert.Equal(["Carol", "Dave"], SelectedNames(table));
	}

	[Fact]
	public void RemovingASelectedRow_DropsItFromTheMultiSelection_AndSaysSo()
	{
		var table = PeopleTable(multiSelect: true);
		table.ToggleRowSelection(2);
		table.ToggleRowSelection(3);
		var events = new SelectionEvents(table);

		table.RemoveRow(2);

		Assert.Equal(["Dave"], SelectedNames(table));
		Assert.Equal([1], events.MultiSelectionChanged);
	}

	[Fact]
	public void InsertingARowUnderASort_KeepsTheMultiSelectionOnItsRows()
	{
		var table = PeopleTable(multiSelect: true);
		table.SortByColumn(0);
		table.SortByColumn(0);                         // Eve, Dave, Carol, Bob, Alice
		table.ToggleRowSelection(0);                   // Eve
		table.ToggleRowSelection(1);                   // Dave

		table.InsertRow(0, "Zed");

		Assert.Equal(["Dave", "Eve"], SelectedNames(table).Order().ToList());
	}

	[Fact]
	public void RemovingARow_KeepsTheRangeAnchorOnItsRow()
	{
		var table = PeopleTable(multiSelect: true);
		table.SelectedRowIndex = 2;                    // anchor: Carol
		table.RightClickRowForTest(4);                 // Carol..Eve

		table.RemoveRow(0);
		table.RightClickRowForTest(2);                 // from the anchor to Dave

		Assert.Equal(["Carol", "Dave"], SelectedNames(table));
	}

	#endregion

	#region Sorting keeps the selection on its rows

	[Fact]
	public void Sorting_KeepsTheCursorOnARowWithoutATag()
	{
		var table = PeopleTable();
		table.SelectedRowIndex = 1;                    // Bob

		table.SortByColumn(0);
		table.SortByColumn(0);                         // descending: Bob moves to 3

		Assert.Equal("Bob", SelectedName(table));
		Assert.Equal(3, table.SelectedRowIndex);
	}

	[Fact]
	public void Sorting_ReportsTheNewIndexButNotANewRow()
	{
		var table = PeopleTable();
		table.SortByColumn(0);
		table.SelectedRowIndex = 1;
		var events = new SelectionEvents(table);

		table.SortByColumn(0);

		Assert.Empty(events.RowChanged);
		Assert.Empty(events.ItemChanged);
		Assert.Contains(nameof(TableControl.SelectedRowIndex), events.PropertyChanged);
	}

	[Fact]
	public void Sorting_KeepsTheMultiSelectionOnItsRows()
	{
		var table = PeopleTable(multiSelect: true);
		table.ToggleRowSelection(0);                   // Alice
		table.ToggleRowSelection(1);                   // Bob

		table.SortByColumn(0);
		table.SortByColumn(0);

		Assert.Equal(["Alice", "Bob"], SelectedNames(table).Order().ToList());
		Assert.Equal([3, 4], table.GetSelectedIndices());
	}

	[Fact]
	public void SortingInCheckboxMode_KeepsTheChecksAndTheSelectionTogether()
	{
		var table = PeopleTable(multiSelect: true);
		table.CheckboxMode = true;
		table.ToggleRowSelection(0);                   // Alice

		table.SortByColumn(0);
		table.SortByColumn(0);

		Assert.Equal(["Alice"], table.GetCheckedRows().Select(row => row.Cells[0]));
		Assert.Equal(["Alice"], SelectedNames(table));
	}

	[Fact]
	public void Sorting_KeepsTheRangeAnchorOnItsRow()
	{
		var table = PeopleTable(multiSelect: true);
		table.SelectedRowIndex = 1;                    // anchor: Bob
		table.RightClickRowForTest(2);                 // Bob..Carol

		table.SortByColumn(0);
		table.SortByColumn(0);                         // Eve, Dave, Carol, Bob, Alice
		table.RightClickRowForTest(1);                 // from the anchor (Bob, now 3) to Dave

		Assert.Equal(["Bob", "Carol", "Dave"], SelectedNames(table).Order().ToList());
	}

	[Fact]
	public void Sorting_KeepsAVisibleCursorOnScreen()
	{
		var table = new TableControl { SortingEnabled = true, ReadOnly = false, Height = 8 };
		table.AddColumn("Id");
		for (int i = 0; i < 40; i++)
			table.AddRow(i.ToString("D2"));
		var buffer = new SharpConsoleUI.Layout.CharacterBuffer(30, 8);
		var bounds = new SharpConsoleUI.Layout.LayoutRect(0, 0, 30, 8);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		table.SelectedRowIndex = 1;

		table.SortByColumn(0);
		table.SortByColumn(0);                         // row 01 moves to position 38

		int visible = table.GetVisibleRowCount();
		Assert.Equal(38, table.SelectedRowIndex);
		Assert.InRange(table.SelectedRowIndex, table.ScrollOffset, table.ScrollOffset + visible - 1);
	}

	#endregion

	#region Clearing the sort or the filter keeps the selected row

	[Fact]
	public void ClearingTheSort_KeepsTheCursorOnItsRow()
	{
		var table = PeopleTable();
		table.SortByColumn(0);
		table.SortByColumn(0);
		table.SelectedRowIndex = 0;                    // Eve
		var events = new SelectionEvents(table);

		table.ClearSort();

		Assert.Equal("Eve", SelectedName(table));
		Assert.Equal(4, table.SelectedRowIndex);
		Assert.Empty(events.RowChanged);
	}

	[Fact]
	public void ClearingTheFilter_KeepsTheCursorOnItsRow()
	{
		var table = PeopleTable();
		table.ApplyFilter("a");                        // Alice, Carol, Dave
		table.SelectedRowIndex = 1;                    // Carol
		var events = new SelectionEvents(table);

		table.ClearFilter();

		Assert.Equal("Carol", SelectedName(table));
		Assert.Equal(2, table.SelectedRowIndex);
		Assert.Empty(events.RowChanged);
	}

	[Fact]
	public void ClearingTheFilter_KeepsTheMultiSelectionOnItsRows()
	{
		var table = PeopleTable(multiSelect: true);
		table.ApplyFilter("a");
		table.ToggleRowSelection(1);                   // Carol
		table.ToggleRowSelection(2);                   // Dave

		table.ClearFilter();

		Assert.Equal(["Carol", "Dave"], SelectedNames(table));
	}

	[Fact]
	public void BackspacingTheFilterToEmpty_KeepsTheCursorOnItsRow()
	{
		var table = PeopleTable();
		table.ReadOnly = false;
		table.FilteringEnabled = true;
		table.EnterFilterMode();
		table.ProcessFilterKey(new ConsoleKeyInfo('v', ConsoleKey.V, false, false, false));   // Dave, Eve
		table.SelectedRowIndex = 1;                                                       // Eve

		table.ProcessFilterKey(new ConsoleKeyInfo('\0', ConsoleKey.Backspace, false, false, false));

		Assert.Equal("Eve", SelectedName(table));
		Assert.Equal(4, table.SelectedRowIndex);
	}

	[Fact]
	public void ApplyingAFilter_StillStartsAtTheFirstMatch()
	{
		var table = PeopleTable();
		table.SelectedRowIndex = 3;                    // Dave

		table.ApplyFilter("a");

		Assert.Equal(0, table.SelectedRowIndex);
		Assert.Equal("Alice", SelectedName(table));
	}

	#endregion
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Two input bugs on the table's filter bar, both reported against 2.7.1.
/// <para>
/// CLICKING THE FILTER BAR MOVED THE SELECTION. <c>GetRowIndexAtY</c> walks down from the top of the
/// control through the title, border and header, but knows nothing of what is painted BELOW the rows.
/// A click on the filter bar produced a row offset past the last row, and the only guard was a
/// RowCount check that passes whenever the list is scrolled far enough for the offset to land on a
/// real row. The hit test already had a <c>FilterBar</c> zone; nothing read it.
/// </para>
/// <para>
/// BACKSPACE ON AN EMPTY FILTER LEFT FILTER MODE. Deleting the last character of a filter and
/// pressing backspace once more — how one retypes a filter from scratch — called <c>ClearFilter</c>
/// and closed the filter bar. Esc is already the key for leaving.
/// </para>
/// </summary>
public class TableFilterBarInputTests
{
	#region Harness

	/// <summary>
	/// Builds the real nesting on a HeadlessConsoleDriver so clicks go through the ACTUAL window
	/// dispatcher rather than the control's own method, and with more rows than fit so the list is
	/// genuinely scrollable — which is the condition that let the stale RowCount check pass.
	/// </summary>
	private static (TableControl table, ConsoleWindowSystem system, Window window) Build(int rows = 25)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16, Title = "Rows" };

		var table = new TableControl
		{
			FilteringEnabled = true,
			ReadOnly = false,
			ShowHeader = true,
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		table.AddColumn("Name");
		table.AddColumn("City");
		for (int i = 0; i < rows; i++)
			table.AddRow($"Name{i:00}", i % 2 == 0 ? "Athens" : "Berlin");

		window.AddControl(table);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		system.Render.UpdateDisplay();
		window.FocusControl(table);
		system.Render.UpdateDisplay();

		return (table, system, window);
	}

	/// <summary>Delivers a left click through the REAL dispatcher, in window coordinates.</summary>
	private static void RealClick(ConsoleWindowSystem system, int x, int y)
	{
		var driver = (HeadlessConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(x, y));
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();
	}

	private static void Send(ConsoleWindowSystem system, ConsoleKeyInfo key)
	{
		system.InputStateService.EnqueueKey(key);
		system.Input.ProcessInput();
	}

	private static ConsoleKeyInfo Ch(char c) => new(c, ConsoleKey.A, false, false, false);

	/// <summary>
	/// Finds the filter bar's row in window coordinates by looking for the status text it paints,
	/// rather than hard-coding a line number that border and padding choices would invalidate.
	/// </summary>
	private static int FilterBarRow(Window window)
	{
		var lines = window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });

		for (int i = 0; i < lines.Count; i++)
		{
			// The bar renders the row counter and the '/' hint; "Row " is enough to name it and
			// appears nowhere else in the table body.
			if (lines[i].Contains("Row ") && lines[i].Contains("/"))
				return i;
		}
		return -1;
	}

	#endregion

	#region Clicking the filter bar

	/// <summary>
	/// THE REPORTED BUG, through the real dispatcher: a click on the filter bar must not move the
	/// selection. The list is scrolled first, because that is what made the old RowCount guard pass.
	/// </summary>
	[Fact]
	public void ClickingTheFilterBar_DoesNotMoveTheSelection()
	{
		var (table, system, window) = Build();
		table.SelectedRowIndex = 1;

		int barRow = FilterBarRow(window);
		Assert.True(barRow > 0, "the filter bar should be painted and findable");

		int before = table.SelectedRowIndex;
		int raises = 0;
		table.SelectedRowChanged += (_, _) => raises++;

		RealClick(system, 10, barRow);

		Assert.Equal(before, table.SelectedRowIndex);
		Assert.Equal(0, raises);

		// And it survives the re-render.
		window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });
		Assert.Equal(before, table.SelectedRowIndex);
	}

	/// <summary>
	/// The same with the list scrolled, which is the case the stale guard could not catch: the row
	/// offset the filter bar produced still mapped onto a real row.
	/// </summary>
	[Fact]
	public void ClickingTheFilterBar_WhenScrolled_DoesNotMoveTheSelection()
	{
		var (table, system, window) = Build();
		table.SelectedRowIndex = 1;
		table.ScrollOffset = 12;
		system.Render.UpdateDisplay();

		int barRow = FilterBarRow(window);
		Assert.True(barRow > 0);

		int before = table.SelectedRowIndex;
		RealClick(system, 10, barRow);

		Assert.Equal(before, table.SelectedRowIndex);
	}

	/// <summary>
	/// The hit test agrees: both lines of the bar — the separator and the status row — report the
	/// FilterBar zone and no row, in CONTROL coordinates. This pins the contract the click path now
	/// relies on, without the window-border offset that window-relative clicking involves.
	/// </summary>
	[Fact]
	public void BothLinesOfTheFilterBar_HitTestAsFilterBarWithNoRow()
	{
		var (table, _, window) = Build();

		// Find the bar in control coordinates by asking the hit test, which is the thing under test.
		var barLines = new List<int>();
		for (int y = 0; y < window.Height; y++)
		{
			if (table.HitTest(10, y).Zone == TableHitZone.FilterBar)
				barLines.Add(y);
		}

		Assert.Equal(2, barLines.Count);  // separator + status row
		foreach (int y in barLines)
			Assert.Equal(-1, table.HitTest(10, y).DisplayRowIndex);

		// The line directly above the bar IS a data row, so the guard is not simply swallowing
		// everything near the bottom.
		Assert.Equal(TableHitZone.Cell, table.HitTest(10, barLines[0] - 1).Zone);
	}

	/// <summary>
	/// THE GUARD AGAINST OVER-FIXING: a click on a real data row must still select it. Rejecting the
	/// filter bar by returning -1 for everything outside the data band would pass the tests above
	/// while breaking the control.
	/// </summary>
	[Fact]
	public void ClickingADataRow_StillSelectsIt()
	{
		var (table, system, window) = Build();
		table.SelectedRowIndex = 0;

		int barRow = FilterBarRow(window);
		// A line comfortably inside the data area: below the header, above the bar.
		int dataRow = barRow - 3;
		Assert.True(dataRow > 2);

		RealClick(system, 10, dataRow);

		Assert.True(table.SelectedRowIndex >= 0, "a click on a data row must still select a row");
		Assert.InRange(table.SelectedRowIndex, 0, table.RowCount - 1);
	}

	/// <summary>
	/// THE DEMO'S OWN CONFIGURATION — a title plus row separators, which is what the Employee
	/// Directory uses and where the bug was reported. Separators double the line pitch, so this is
	/// the layout most likely to expose an off-by-one in a band guard: every second line is a
	/// separator that must not select, every other one must.
	/// </summary>
	[Fact]
	public void WithTitleAndRowSeparators_DataRowsSelectAndTheFilterBarDoesNot()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Left = 0, Top = 0, Width = 80, Height = 24, Title = "W" };
		var table = new TableControl
		{
			FilteringEnabled = true,
			ReadOnly = false,
			ShowHeader = true,
			Title = "Employee Directory",
			ShowRowSeparators = true,
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		table.AddColumn("ID");
		table.AddColumn("Name");
		for (int i = 0; i < 25; i++)
			table.AddRow($"{i}", $"Name{i:00}");
		window.AddControl(table);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		system.Render.UpdateDisplay();
		window.FocusControl(table);
		system.Render.UpdateDisplay();

		var cells = new List<int>();
		var barLines = new List<int>();
		for (int y = 0; y < window.Height; y++)
		{
			var hit = table.HitTest(10, y);
			if (hit.Zone == TableHitZone.Cell) cells.Add(y);
			if (hit.Zone == TableHitZone.FilterBar) barLines.Add(y);
		}

		// Data rows still resolve, so the guard did not swallow the body.
		Assert.True(cells.Count >= 5, $"expected several selectable rows, got {cells.Count}");
		foreach (int y in cells)
			Assert.True(table.HitTest(10, y).DisplayRowIndex >= 0);

		// And the bar's two lines still resolve to no row.
		Assert.Equal(2, barLines.Count);
		foreach (int y in barLines)
			Assert.Equal(-1, table.HitTest(10, y).DisplayRowIndex);

		// The bar is below every data row, which is the ordering the bug depended on.
		Assert.True(barLines[0] > cells[^1]);
	}

	#endregion

	#region Backspace in the filter

	/// <summary>
	/// THE REPORTED UX BUG: backspace is an editing key. On an empty filter it must do nothing, not
	/// leave filter mode.
	/// </summary>
	[Fact]
	public void BackspaceOnAnEmptyFilter_StaysInFilterMode()
	{
		var (table, system, _) = Build();

		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);  // '/' opens the filter bar

		Send(system, Ch('B'));
		Send(system, new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
		// Deleting the only character leaves the bar open.
		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);

		// The press that used to close the bar.
		Send(system, new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
		// The press that used to close the bar must not leave filter mode.
		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);

		// Still usable: typing again filters.
		Send(system, Ch('B'));
		Send(system, Ch('e'));
		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);
		Assert.True(table.RowCount < 25, "typing after backspacing to empty should filter again");
	}

	/// <summary>Several backspaces on an empty filter are still harmless.</summary>
	[Fact]
	public void RepeatedBackspaceOnAnEmptyFilter_IsHarmless()
	{
		var (table, system, _) = Build();

		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		for (int i = 0; i < 5; i++)
			Send(system, new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));

		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);
		Assert.Equal(25, table.RowCount);
	}

	/// <summary>
	/// Backspacing a filter away still restores every row — the behaviour the existing suite
	/// relies on — it just does not close the bar as well.
	/// </summary>
	[Fact]
	public void BackspacingAFilterAway_ShowsEveryRowAndKeepsTheBarOpen()
	{
		var (table, system, _) = Build();

		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		foreach (char c in "Berlin")
			Send(system, Ch(c));
		Assert.True(table.RowCount < 25);

		for (int i = 0; i < "Berlin".Length; i++)
			Send(system, new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));

		Assert.Equal(25, table.RowCount);
		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);
	}

	/// <summary>Esc remains the way out, and still clears the filter.</summary>
	[Fact]
	public void EscapeStillLeavesFilterMode()
	{
		var (table, system, _) = Build();

		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		foreach (char c in "Berlin")
			Send(system, Ch(c));

		Send(system, new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false));

		// Esc is the key that leaves the filter.
		Assert.NotEqual(FilterMode.Typing, table.CurrentFilterMode);
		Assert.Equal(25, table.RowCount);
	}

	#endregion

	#region Row navigation from inside the filter editor

	/// <summary>
	/// Up, down and paging used to fall to the filter editor's default branch and be SWALLOWED, so
	/// pressing Down while typing a filter did nothing. They now move through the matches, leaving
	/// the filter text alone, so one can narrow a list and walk it without confirming and reopening
	/// the filter.
	/// </summary>
	[Fact]
	public void ArrowsInTheFilterEditor_MoveThroughTheMatches()
	{
		var (table, system, _) = Build();

		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		Send(system, Ch('N'));  // matches every row, so there is room to move

		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);
		int start = table.SelectedRowIndex;

		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false));
		Assert.Equal(start + 1, table.SelectedRowIndex);

		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false));
		Assert.Equal(start + 2, table.SelectedRowIndex);

		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false));
		Assert.Equal(start + 1, table.SelectedRowIndex);

		// Still typing, and the text was not touched by the navigation.
		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);
	}

	/// <summary>Paging works the same way, by a viewport at a time.</summary>
	[Fact]
	public void PagingInTheFilterEditor_MovesByAPage()
	{
		var (table, system, _) = Build();

		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		Send(system, Ch('N'));
		table.SelectedRowIndex = 0;

		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.PageDown, false, false, false));
		int afterPageDown = table.SelectedRowIndex;
		Assert.True(afterPageDown > 1, $"PageDown should move more than one row, moved to {afterPageDown}");

		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.PageUp, false, false, false));
		Assert.Equal(0, table.SelectedRowIndex);
		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);
	}

	/// <summary>
	/// HOME AND END STAY WITH THE TEXT. They are the only navigation keys the single-line editor
	/// genuinely uses, so they must not reach the rows — otherwise there is no way to get to the
	/// start of a filter you are editing.
	/// </summary>
	[Fact]
	public void HomeAndEndInTheFilterEditor_MoveTheTextCursorNotTheSelection()
	{
		var (table, system, _) = Build();

		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		foreach (char c in "Name0")
			Send(system, Ch(c));

		table.SelectedRowIndex = 2;
		int before = table.SelectedRowIndex;

		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false));
		Assert.Equal(before, table.SelectedRowIndex);

		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false));
		Assert.Equal(before, table.SelectedRowIndex);

		// Home really moved the text cursor: typing at it prepends rather than appends.
		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false));
		Send(system, Ch('Z'));
		Assert.Equal(0, table.RowCount);  // "ZName0" matches nothing
	}

	/// <summary>Left and Right stay with the text too, where a cursor exists.</summary>
	[Fact]
	public void LeftAndRightInTheFilterEditor_StayWithTheText()
	{
		var (table, system, _) = Build();

		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		Send(system, Ch('N'));
		table.SelectedRowIndex = 2;
		int before = table.SelectedRowIndex;

		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false));
		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false));

		Assert.Equal(before, table.SelectedRowIndex);
	}

	/// <summary>Navigating from the editor still announces the move, as any selection change must.</summary>
	[Fact]
	public void NavigatingFromTheFilterEditor_RaisesSelectedRowChanged()
	{
		var (table, system, _) = Build();

		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		Send(system, Ch('N'));

		int raises = 0;
		table.SelectedRowChanged += (_, _) => raises++;

		Send(system, new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false));

		Assert.Equal(1, raises);
	}

	#endregion

	#region TreeTableControl inherits both fixes

	/// <summary>
	/// <see cref="TreeTableControl"/> derives from <see cref="TableControl"/> and overrides
	/// <c>TryHandleClick</c> and <c>TryHandleKey</c>, but neither the row-resolution path nor the
	/// filter-key path, so both fixes reach it. These pin that, because an override added later
	/// could silently take the behaviour back.
	/// </summary>
	private static (TreeTableControl table, ConsoleWindowSystem system, Window window) BuildTree()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16, Title = "Tree" };

		var table = new TreeTableControl
		{
			FilteringEnabled = true,
			ReadOnly = false,
			ShowHeader = true,
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		table.AddColumn("Name");
		// Enough rows, expanded, to overflow the viewport — the scrollable case the old guard missed.
		for (int i = 0; i < 6; i++)
		{
			var root = table.AddRootRow($"Root{i}");
			root.AddChild($"Child{i}a");
			root.AddChild($"Child{i}b");
		}
		table.ExpandAll();

		window.AddControl(table);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		system.Render.UpdateDisplay();
		window.FocusControl(table);
		system.Render.UpdateDisplay();

		return (table, system, window);
	}

	[Fact]
	public void TreeTable_ClickingTheFilterBar_DoesNotMoveTheSelection()
	{
		var (table, system, window) = BuildTree();
		table.SelectedRowIndex = 1;

		int barRow = FilterBarRow(window);
		Assert.True(barRow > 0, "the filter bar should be painted and findable");

		int before = table.SelectedRowIndex;
		int raises = 0;
		table.SelectedRowChanged += (_, _) => raises++;

		RealClick(system, 10, barRow);

		Assert.Equal(before, table.SelectedRowIndex);
		Assert.Equal(0, raises);
	}

	[Fact]
	public void TreeTable_BothLinesOfTheFilterBar_HitTestAsFilterBarWithNoRow()
	{
		var (table, _, window) = BuildTree();

		var barLines = new List<int>();
		for (int y = 0; y < window.Height; y++)
		{
			if (table.HitTest(10, y).Zone == TableHitZone.FilterBar)
				barLines.Add(y);
		}

		Assert.Equal(2, barLines.Count);
		foreach (int y in barLines)
			Assert.Equal(-1, table.HitTest(10, y).DisplayRowIndex);

		// Still selectable above the bar, so the guard did not swallow the tree body.
		Assert.Equal(TableHitZone.Cell, table.HitTest(10, barLines[0] - 1).Zone);
	}

	[Fact]
	public void TreeTable_BackspaceOnAnEmptyFilter_StaysInFilterMode()
	{
		var (table, system, _) = BuildTree();

		Send(system, new ConsoleKeyInfo('/', ConsoleKey.Divide, false, false, false));
		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);

		Send(system, Ch('R'));
		Send(system, new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
		Send(system, new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));

		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);
	}

	#endregion
}

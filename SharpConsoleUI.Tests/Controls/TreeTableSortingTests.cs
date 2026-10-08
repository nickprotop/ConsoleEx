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
/// Sorting a <see cref="TreeTableControl"/>: every row's children are ordered among themselves, by
/// the table's own rules, and no row leaves its parent.
/// </summary>
public class TreeTableSortingTests
{
	#region Helpers

	/// <summary>
	/// Two features, each with stories added out of order; points in the second column, with ties.
	/// </summary>
	private static TreeTableControl Backlog()
	{
		var table = new TreeTableControl { SortingEnabled = true, ReadOnly = false, BorderStyle = BorderStyle.None };
		table.AddColumn("Title");
		table.AddColumn("Pts", TextJustification.Right, 3);
		var zeta = table.AddRootRow("Zeta feature", "5");
		zeta.AddChild("Story c", "3");
		zeta.AddChild("Story a", "1");
		zeta.AddChild("Story b", "3");
		var alpha = table.AddRootRow("Alpha feature", "8");
		alpha.AddChild("Story y", "2").AddChild("Task 2", "1");
		alpha.AddChild("Story x", "2");
		return table;
	}

	private static List<string> Displayed(TableControl table)
		=> Enumerable.Range(0, table.RowCount).Select(i => table.GetRow(table.MapDisplayToData(i)).Cells[0]).ToList();

	private static TreeTableRow Row(TreeTableControl table, string title)
		=> (TreeTableRow)table.Rows.Single(r => r.Cells[0] == title);

	#endregion

	#region Siblings are ordered among themselves

	[Fact]
	public void Sorting_OrdersEachRowsChildren_AndKeepsThemUnderIt()
	{
		var table = Backlog();

		table.SortByColumn(0);

		Assert.Equal(
			["Alpha feature", "Story x", "Story y", "Task 2", "Zeta feature", "Story a", "Story b", "Story c"],
			Displayed(table));
	}

	[Fact]
	public void SortingDescending_ReversesEachLevel()
	{
		var table = Backlog();

		table.SortByColumn(0);
		table.SortByColumn(0);

		Assert.Equal(
			["Zeta feature", "Story c", "Story b", "Story a", "Alpha feature", "Story y", "Task 2", "Story x"],
			Displayed(table));
	}

	[Fact]
	public void SiblingsWithEqualKeys_KeepTheOrderTheyWereAddedIn_EitherWay()
	{
		var table = Backlog();

		table.SortByColumn(1);
		var ascending = Displayed(table);
		table.SortByColumn(1);
		var descending = Displayed(table);

		// Zeta (5) before Alpha (8) ascending, after it descending.
		Assert.Equal(["Story a", "Story c", "Story b"], ascending.Skip(1).Take(3));     // 1, 3, 3
		Assert.Equal(["Story y", "Task 2", "Story x"], descending.Skip(1).Take(3));    // 2, 2: as added
		Assert.Equal(["Story c", "Story b", "Story a"], descending.Skip(5).Take(3));    // 3, 3, 1
	}

	[Fact]
	public void TheColumnsComparer_DecidesTheOrder()
	{
		// By length: the shorter feature title first; the stories all tie, and keep their order.
		var table = Backlog();
		table.Columns[0].CustomComparer = Comparer<string>.Create((x, y) => x.Length.CompareTo(y.Length));

		table.SortByColumn(0);

		Assert.Equal(
			["Zeta feature", "Story c", "Story a", "Story b", "Alpha feature", "Story y", "Task 2", "Story x"],
			Displayed(table));
	}

	[Fact]
	public void ClearingTheSort_RestoresTheOrderRowsWereAddedIn()
	{
		var table = Backlog();
		table.SortByColumn(0);

		table.ClearSort();

		Assert.Equal(
			["Zeta feature", "Story c", "Story a", "Story b", "Alpha feature", "Story y", "Task 2", "Story x"],
			Displayed(table));
	}

	[Fact]
	public void ARowAddedWhileSorted_TakesItsSortedPlaceAmongItsSiblings()
	{
		var table = Backlog();
		table.SortByColumn(0);

		Row(table, "Zeta feature").AddChild("Story aa", "9");

		Assert.Equal(["Zeta feature", "Story a", "Story aa", "Story b", "Story c"], Displayed(table).Skip(4));
	}

	[Fact]
	public void TheSelection_StaysOnItsRow_WhenTheSortMovesIt()
	{
		var table = Backlog();
		table.SelectRow(Row(table, "Story c"));

		table.SortByColumn(0);

		Assert.Equal("Story c", table.SelectedRow?.Cells[0]);
		Assert.Equal(7, table.SelectedRowIndex);
	}

	[Fact]
	public void TheGuides_FollowTheSortedOrder()
	{
		var table = Backlog();
		table.SortByColumn(0);

		var buffer = new CharacterBuffer(30, 10);
		var bounds = new LayoutRect(0, 0, 30, 10);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		string Line(int y) => string.Concat(Enumerable.Range(0, 30).Select(x => buffer.GetCell(x, y).Character.ToString())).TrimEnd();

		Assert.StartsWith("├─     Story x", Line(2));
		Assert.StartsWith("└─ [-] Story y", Line(3));
		Assert.StartsWith("   └─     Task 2", Line(4));
		Assert.StartsWith("└─     Story c", Line(8));
	}

	#endregion
}

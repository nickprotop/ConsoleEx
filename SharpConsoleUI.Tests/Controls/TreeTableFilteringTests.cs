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
/// Filtering a <see cref="TreeTableControl"/>: every row is searched, collapsed or not, and each
/// match is shown with the rows above it, while the hierarchy's own state is left as it was.
/// </summary>
public class TreeTableFilteringTests
{
	#region Helpers

	/// <summary>
	/// Feature Login > (Story Form > (Task layout, Task validation), Story Reset);
	/// Feature Report > (Story Export > (Task csv)). Owner in the second column.
	/// </summary>
	private static TreeTableControl Backlog()
	{
		var table = new TreeTableControl { ReadOnly = false, FilteringEnabled = true, SortingEnabled = true, BorderStyle = BorderStyle.None };
		table.AddColumn("Title");
		table.AddColumn("Owner");
		var login = table.AddRootRow("Feature Login", "Ann");
		var form = login.AddChild("Story Form", "Bob");
		form.AddChild("Task layout", "Bob");
		form.AddChild("Task validation", "Cid");
		login.AddChild("Story Reset", "Ann");
		var report = table.AddRootRow("Feature Report", "Dee");
		report.AddChild("Story Export", "Dee").AddChild("Task csv", "Eli");
		return table;
	}

	private static List<string> Displayed(TableControl table)
		=> Enumerable.Range(0, table.RowCount).Select(i => table.GetRow(table.MapDisplayToData(i)).Cells[0]).ToList();

	private static TreeTableRow Row(TreeTableControl table, string title)
		=> (TreeTableRow)table.Rows.Single(r => r.Cells[0] == title);

	private static List<string> Lines(TableControl table)
	{
		var buffer = new CharacterBuffer(40, 10);
		var bounds = new LayoutRect(0, 0, 40, 10);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return Enumerable.Range(0, 10)
			.Select(y => string.Concat(Enumerable.Range(0, 40).Select(x => buffer.GetCell(x, y).Character.ToString())).TrimEnd())
			.Where(line => line.Length > 0)
			.ToList();
	}

	#endregion

	#region Matches are shown in context

	[Fact]
	public void AMatch_IsShownWithTheRowsAboveIt()
	{
		var table = Backlog();

		table.ApplyFilter("validation");

		Assert.Equal(["Feature Login", "Story Form", "Task validation"], Displayed(table));
	}

	[Fact]
	public void TheRowsAboveAMatch_AreDrawnOpen()
	{
		var table = Backlog();

		table.ApplyFilter("csv");

		var lines = Lines(table);
		Assert.Equal(4, lines.Count);
		Assert.StartsWith("[-] Feature Report", lines[1]);
		Assert.StartsWith("└─ [-] Story Export", lines[2]);
		Assert.StartsWith("   └─     Task csv", lines[3]);
	}

	[Fact]
	public void RowsInsideCollapsedParents_AreSearched_AndTheirStateKept()
	{
		var table = Backlog();
		table.CollapseAll();

		table.ApplyFilter("csv");

		Assert.Equal(["Feature Report", "Story Export", "Task csv"], Displayed(table));
		Assert.False(Row(table, "Feature Report").IsExpanded);
	}

	[Fact]
	public void AMatchsOtherChildren_StayHidden()
	{
		var table = Backlog();

		table.ApplyFilter("Title:form");

		Assert.Equal(["Feature Login", "Story Form"], Displayed(table));
	}

	[Fact]
	public void IncludingDescendants_ShowsEverythingUnderAMatch()
	{
		var table = Backlog();
		table.FilterIncludesDescendants = true;

		table.ApplyFilter("Title:form");

		Assert.Equal(["Feature Login", "Story Form", "Task layout", "Task validation"], Displayed(table));
	}

	[Fact]
	public void ARowWhoseChildrenAreAllFilteredOut_ShowsNoExpander()
	{
		var table = Backlog();

		table.ApplyFilter("Title:form");

		// Feature Login still shows its expander, so Story Form leaves the expander's width blank.
		Assert.StartsWith("└─     Story Form", Lines(table)[2]);
	}

	[Fact]
	public void EveryFilterForm_WorksOnTheHierarchy()
	{
		var table = Backlog();

		table.ApplyFilter("Owner:bob|eli task");

		Assert.Equal(["Feature Login", "Story Form", "Task layout", "Feature Report", "Story Export", "Task csv"], Displayed(table));
	}

	[Fact]
	public void SortingAFilteredTable_SortsTheShownSiblings()
	{
		var table = Backlog();
		table.ApplyFilter("task");

		table.SortByColumn(0);
		table.SortByColumn(0);

		Assert.Equal(["Feature Report", "Story Export", "Task csv", "Feature Login", "Story Form", "Task validation", "Task layout"], Displayed(table));
	}

	[Fact]
	public void TypingAFilter_NarrowsAsYouType()
	{
		var table = Backlog();
		table.EnterFilterMode();

		foreach (char c in "csv")
			table.ProcessFilterKey(new ConsoleKeyInfo(c, ConsoleKey.A, false, false, false));

		Assert.Equal(["Feature Report", "Story Export", "Task csv"], Displayed(table));
	}

	#endregion

	#region Opening and closing while filtered changes only the filtered view

	[Fact]
	public void CollapsingWhileFiltered_ChangesOnlyTheFilteredView()
	{
		var table = Backlog();
		table.ApplyFilter("task");

		Assert.True(table.Collapse(Row(table, "Story Form")));

		Assert.False(table.IsRowExpanded(Row(table, "Story Form")));
		Assert.True(Row(table, "Story Form").IsExpanded);
		Assert.DoesNotContain("Task layout", Displayed(table));
	}

	[Fact]
	public void ClearingTheFilter_BringsTheHierarchyBackAsItWas()
	{
		var table = Backlog();
		table.Collapse(Row(table, "Feature Report"));
		table.ApplyFilter("task");
		table.Collapse(Row(table, "Story Form"));

		table.ClearFilter();

		Assert.Equal(["Feature Login", "Story Form", "Task layout", "Task validation", "Story Reset", "Feature Report"], Displayed(table));
	}

	[Fact]
	public void ChangingTheFilter_ForgetsWhatWasToggled()
	{
		var table = Backlog();
		table.ApplyFilter("task");
		table.Collapse(Row(table, "Story Form"));

		table.ApplyFilter("Task");

		Assert.Contains("Task layout", Displayed(table));
	}

	[Fact]
	public void SettingIsExpandedWhileFiltered_ShowsWhereTheRowWasNotToggled()
	{
		var table = Backlog();
		table.FilterIncludesDescendants = true;
		table.ApplyFilter("Title:story");

		Row(table, "Story Form").IsExpanded = false;

		Assert.DoesNotContain("Task layout", Displayed(table));
		Assert.False(Row(table, "Story Form").IsExpanded);
	}

	[Fact]
	public void ASelectionClearingTheFilterHides_MovesToTheRowAboveIt()
	{
		var table = Backlog();
		table.Collapse(Row(table, "Story Form"));
		table.ApplyFilter("validation");
		table.SelectRow(Row(table, "Task validation"));

		table.ClearFilter();

		Assert.Equal("Story Form", table.SelectedRow?.Cells[0]);
	}

	#endregion

	#region Highlighting

	[Fact]
	public void TheMatchHighlight_FallsOnTheValue_AfterTheGuides()
	{
		var table = Backlog();
		table.ApplyFilter("csv");
		table.SelectedRowIndex = 0;     // keeps the cursor off the row inspected
		var buffer = new CharacterBuffer(40, 10);
		var bounds = new LayoutRect(0, 0, 40, 10);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		// "   └─     Task csv": guide and blank expander in cells 0..9, "Task " 10..14, "csv" 15..17.
		Assert.NotEqual(Color.DarkYellow, buffer.GetCell(4, 3).Background);
		Assert.NotEqual(Color.DarkYellow, buffer.GetCell(14, 3).Background);
		Assert.Equal(Color.DarkYellow, buffer.GetCell(15, 3).Background);
		Assert.Equal(Color.DarkYellow, buffer.GetCell(17, 3).Background);
	}

	#endregion
}

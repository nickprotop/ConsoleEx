// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Opening and closing rows of a <see cref="TreeTableControl"/>: what is displayed, what is
/// reported, and where the selection goes.
/// </summary>
public class TreeTableExpansionTests
{
	#region Helpers

	/// <summary>Feature A > (Story A1 > (Task A1a, Task A1b), Story A2); Feature B > (Story B1).</summary>
	private static TreeTableControl Backlog()
	{
		var table = new TreeTableControl { ReadOnly = false };
		table.AddColumn("Title");
		var featureA = table.AddRootRow("Feature A");
		var storyA1 = featureA.AddChild("Story A1");
		storyA1.AddChild("Task A1a");
		storyA1.AddChild("Task A1b");
		featureA.AddChild("Story A2");
		table.AddRootRow("Feature B").AddChild("Story B1");
		return table;
	}

	private static TreeTableRow Row(TreeTableControl table, string title)
		=> (TreeTableRow)table.Rows.Single(r => r.Cells[0] == title);

	private static List<string> Displayed(TableControl table)
		=> Enumerable.Range(0, table.RowCount).Select(i => table.GetRow(table.MapDisplayToData(i)).Cells[0]).ToList();

	/// <summary>Counts how often the display is recomputed.</summary>
	private sealed class CountingTreeTable : TreeTableControl
	{
		public int Computes { get; private set; }

		protected override int[]? ComputeDisplayRows(TableDisplayQuery query)
		{
			Computes++;
			return base.ComputeDisplayRows(query);
		}
	}

	#endregion

	#region What is displayed

	[Fact]
	public void CollapsingARow_HidesItsDescendants_ButKeepsThemInRows()
	{
		var table = Backlog();

		Assert.True(table.Collapse(Row(table, "Feature A")));

		Assert.Equal(["Feature A", "Feature B", "Story B1"], Displayed(table));
		Assert.Equal(3, table.RowCount);
		Assert.Equal(7, table.Rows.Count);
	}

	[Fact]
	public void ACollapsedAncestor_HidesRowsEvenWhenTheirParentIsOpen()
	{
		var table = Backlog();

		table.Collapse(Row(table, "Feature A"));

		Assert.True(table.IsRowExpanded(Row(table, "Story A1")));
		Assert.DoesNotContain("Task A1a", Displayed(table));
	}

	[Fact]
	public void ExpandCollapseAndToggle_SayWhetherTheyChangedAnything()
	{
		var table = Backlog();
		var story = Row(table, "Story A1");

		Assert.False(table.Expand(story));
		Assert.True(table.Collapse(story));
		Assert.False(table.Collapse(story));
		Assert.True(table.Toggle(story));
		Assert.True(table.IsRowExpanded(story));
	}

	[Fact]
	public void SettingIsExpanded_OnARowInTheTable_CollapsesItThere()
	{
		var table = Backlog();

		Row(table, "Story A1").IsExpanded = false;

		Assert.Equal(["Feature A", "Story A1", "Story A2", "Feature B", "Story B1"], Displayed(table));
	}

	[Fact]
	public void CollapseAllAndExpandAll_ActOnEveryRow()
	{
		var table = Backlog();

		table.CollapseAll();
		Assert.Equal(["Feature A", "Feature B"], Displayed(table));

		table.ExpandAll();
		Assert.Equal(7, table.RowCount);
	}

	[Fact]
	public void ExpandSubtree_OpensTheRowAndEverythingUnderIt()
	{
		var table = Backlog();
		table.CollapseAll();

		Assert.True(table.ExpandSubtree(Row(table, "Feature A")));

		Assert.Equal(["Feature A", "Story A1", "Task A1a", "Task A1b", "Story A2", "Feature B"], Displayed(table));
	}

	[Fact]
	public void ExpandAll_RecomputesTheDisplayOnce()
	{
		var table = new CountingTreeTable();
		table.AddColumn("Title");
		for (int i = 0; i < 20; i++)
			table.AddRootRow($"Parent {i}").AddChild("Child");
		table.CollapseAll();
		int before = table.Computes;

		table.ExpandAll();

		Assert.Equal(before + 1, table.Computes);
	}

	[Fact]
	public void ARowNotInTheTable_IsRefused()
	{
		var table = Backlog();

		Assert.Throws<InvalidOperationException>(() => table.Expand(new TreeTableRow("stranger")));
	}

	#endregion

	#region Where the selection goes

	[Fact]
	public void CollapsingTheSelectedRowsParent_SelectsTheParent()
	{
		var table = Backlog();
		table.SelectRow(Row(table, "Task A1b"));
		var changed = new List<string?>();
		table.SelectedRowItemChanged += (_, row) => changed.Add(row?.Cells[0]);

		table.Collapse(Row(table, "Feature A"));

		Assert.Equal("Feature A", table.SelectedRow?.Cells[0]);
		Assert.Equal(["Feature A"], changed);
	}

	[Fact]
	public void CollapsingAnotherRow_KeepsTheSelectionOnItsRow()
	{
		var table = Backlog();
		table.SelectRow(Row(table, "Story B1"));
		var changed = new List<string?>();
		table.SelectedRowItemChanged += (_, row) => changed.Add(row?.Cells[0]);

		table.Collapse(Row(table, "Feature A"));

		Assert.Equal("Story B1", table.SelectedRow?.Cells[0]);
		Assert.Equal(2, table.SelectedRowIndex);
		Assert.Empty(changed);
	}

	[Fact]
	public void SelectRow_OpensTheRowsAbove_AndSelectsIt()
	{
		var table = Backlog();
		table.CollapseAll();

		Assert.True(table.SelectRow(Row(table, "Task A1a")));

		Assert.Equal("Task A1a", table.SelectedRow?.Cells[0]);
		Assert.True(table.IsRowExpanded(Row(table, "Feature A")));
		Assert.True(table.IsRowExpanded(Row(table, "Story A1")));
		Assert.False(table.SelectRow(new TreeTableRow("stranger")));
	}

	[Fact]
	public void EnsureRowVisible_ScrollsTheRowIntoView()
	{
		var table = new TreeTableControl { Height = 6 };
		table.AddColumn("Title");
		var parent = table.AddRootRow("Parent");
		for (int i = 0; i < 30; i++)
			parent.AddChild($"Child {i:D2}");
		table.Collapse(parent);
		var buffer = new SharpConsoleUI.Layout.CharacterBuffer(30, 6);
		var bounds = new SharpConsoleUI.Layout.LayoutRect(0, 0, 30, 6);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		Assert.True(table.EnsureRowVisible(parent.Children[25]));

		int position = 26;
		Assert.InRange(position, table.ScrollOffset, table.ScrollOffset + table.GetVisibleRowCount() - 1);
	}

	#endregion
}

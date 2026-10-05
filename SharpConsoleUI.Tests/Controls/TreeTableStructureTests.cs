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
/// The hierarchy of a <see cref="TreeTableControl"/>: how rows nest, what may nest where, and how
/// the table's inherited row members read in terms of it.
/// </summary>
public class TreeTableStructureTests
{
	#region Helpers

	/// <summary>
	/// Two features with stories, the first story with tasks:
	/// Feature A > (Story A1 > (Task A1a, Task A1b), Story A2); Feature B > (Story B1).
	/// </summary>
	private static TreeTableControl Backlog()
	{
		var table = new TreeTableControl();
		table.AddColumn("Title");
		var featureA = table.AddRootRow("Feature A");
		var storyA1 = featureA.AddChild("Story A1");
		storyA1.AddChild("Task A1a");
		storyA1.AddChild("Task A1b");
		featureA.AddChild("Story A2");
		table.AddRootRow("Feature B").AddChild("Story B1");
		return table;
	}

	private static List<string> Titles(IEnumerable<TableRow> rows) => rows.Select(r => r.Cells[0]).ToList();

	private static TreeTableRow Row(TreeTableControl table, string title)
		=> (TreeTableRow)table.Rows.Single(r => r.Cells[0] == title);

	#endregion

	#region Rows nest

	[Fact]
	public void Rows_ListEveryRowDepthFirstInSiblingOrder()
	{
		var table = Backlog();

		Assert.Equal(
			["Feature A", "Story A1", "Task A1a", "Task A1b", "Story A2", "Feature B", "Story B1"],
			Titles(table.Rows));
	}

	[Fact]
	public void AChild_KnowsItsParentAndDepth()
	{
		var table = Backlog();
		var task = Row(table, "Task A1b");

		Assert.Equal("Story A1", task.Parent?.Cells[0]);
		Assert.Equal(2, task.Depth);
		Assert.Same(task.Parent, table.GetParentRow(task));
		Assert.Equal(2, table.GetDepth(task));
		Assert.Equal(0, table.GetDepth(Row(table, "Feature B")));
	}

	[Fact]
	public void Children_IsALiveView()
	{
		var table = Backlog();
		var feature = Row(table, "Feature B");
		var children = feature.Children;

		feature.AddChild("Story B2");

		Assert.Equal(["Story B1", "Story B2"], Titles(children));
	}

	[Fact]
	public void ASubtreeBuiltDetached_IsAddedInOneStep()
	{
		var table = new TreeTableControl();
		table.AddColumn("Title");
		var epic = new TreeTableRow("Epic");
		epic.AddChild("Feature").AddChild("Story");

		table.InsertRootRow(0, epic);

		Assert.Equal(["Epic", "Feature", "Story"], Titles(table.Rows));
		Assert.Same(epic, Assert.Single(table.RootRows));
	}

	[Fact]
	public void AddRootRow_AddsASubtreeAsTheLastRoot()
	{
		var table = Backlog();
		var epic = new TreeTableRow("Epic");
		epic.AddChild("Feature");

		var added = table.AddRootRow(epic);

		Assert.Same(epic, added);
		Assert.Same(epic, table.RootRows[^1]);
		Assert.Equal(["Feature B", "Story B1", "Epic", "Feature"], Titles(table.Rows).Skip(5));
	}

	[Fact]
	public void AddRootRow_RefusesARowThatBelongsSomewhere()
	{
		var table = Backlog();

		Assert.Throws<InvalidOperationException>(() => table.AddRootRow(Row(table, "Story A2")));
	}

	[Fact]
	public void ChildrenAddedThroughAnAttachedRow_AppearAtOnce()
	{
		var table = Backlog();

		Row(table, "Story A1").InsertChild(1, new TreeTableRow("Task A1-between"));

		Assert.Equal(["Feature A", "Story A1", "Task A1a", "Task A1-between", "Task A1b", "Story A2", "Feature B", "Story B1"],
			Titles(table.Rows));
	}

	[Fact]
	public void RemovingAChild_RemovesItsSubtree()
	{
		var table = Backlog();
		var story = Row(table, "Story A1");

		Assert.True(story.Parent!.RemoveChild(story));

		Assert.Equal(["Feature A", "Story A2", "Feature B", "Story B1"], Titles(table.Rows));
		Assert.Null(story.Parent);
		Assert.Equal(2, story.Children.Count);
	}

	[Fact]
	public void ClearingChildren_RemovesThemAll()
	{
		var table = Backlog();

		Row(table, "Feature A").ClearChildren();

		Assert.Equal(["Feature A", "Feature B", "Story B1"], Titles(table.Rows));
	}

	[Fact]
	public void RemoveRowByRow_RemovesAtAnyDepth()
	{
		var table = Backlog();

		Assert.True(table.RemoveRow(Row(table, "Task A1a")));
		Assert.False(table.RemoveRow(new TreeTableRow("stranger")));

		Assert.Equal(["Feature A", "Story A1", "Task A1b", "Story A2", "Feature B", "Story B1"], Titles(table.Rows));
	}

	[Fact]
	public void MoveRow_KeepsTheRowAndItsSubtree()
	{
		var table = Backlog();
		var story = Row(table, "Story A1");

		table.MoveRow(story, Row(table, "Feature B"), 0);

		Assert.Same(story, Row(table, "Story A1"));
		Assert.Equal(["Feature A", "Story A2", "Feature B", "Story A1", "Task A1a", "Task A1b", "Story B1"], Titles(table.Rows));
		Assert.Equal("Feature B", story.Parent?.Cells[0]);
	}

	[Fact]
	public void MoveRow_KeepsTheSelectionOnTheRow()
	{
		var table = Backlog();
		var story = Row(table, "Story A2");
		table.SelectedRowIndex = table.Rows.ToList().IndexOf(story);

		table.MoveRow(story, null, 0);

		Assert.Same(story, table.SelectedRow);
	}

	[Fact]
	public void FindRowByTag_SearchesEveryRow()
	{
		var table = Backlog();
		Row(table, "Task A1b").Tag = 42;

		Assert.Equal("Task A1b", table.FindRowByTag(42)?.Cells[0]);
		Assert.Null(table.FindRowByTag(7));
	}

	#endregion

	#region A row is in one place at a time

	[Fact]
	public void AddingARowThatAlreadyHasAParent_Throws()
	{
		var table = Backlog();
		var task = Row(table, "Task A1a");

		Assert.Throws<InvalidOperationException>(() => Row(table, "Story A2").AddChild(task));
		Assert.Throws<InvalidOperationException>(() => table.InsertRootRow(0, task));
		Assert.Throws<InvalidOperationException>(() => table.AddRow(task));
	}

	[Fact]
	public void AddingARowTwice_Throws()
	{
		var table = new TreeTableControl();
		table.AddColumn("Title");
		var plain = new TableRow("plain");
		table.AddRow(plain);

		Assert.Throws<InvalidOperationException>(() => table.AddRow(plain));
		Assert.Throws<InvalidOperationException>(() => table.AddRows([new TreeTableRow("x"), plain]));
	}

	[Fact]
	public void NestingARowUnderItself_Throws()
	{
		var parent = new TreeTableRow("parent");
		var child = parent.AddChild("child");

		Assert.Throws<InvalidOperationException>(() => child.AddChild(parent));
		Assert.Throws<InvalidOperationException>(() => parent.AddChild(parent));
	}

	[Fact]
	public void MovingARowUnderItsOwnDescendant_Throws()
	{
		var table = Backlog();

		Assert.Throws<InvalidOperationException>(() => table.MoveRow(Row(table, "Feature A"), Row(table, "Task A1a"), 0));
	}

	[Fact]
	public void APlainRow_CanOnlyBeARoot()
	{
		var table = Backlog();
		var plain = new TableRow("plain");
		table.AddRow(plain);

		Assert.Throws<InvalidOperationException>(() => table.MoveRow(plain, Row(table, "Feature A"), 0));
		Assert.Equal(0, table.GetDepth(plain));
		Assert.Null(table.GetParentRow(plain));
	}

	#endregion

	#region The inherited row API reads in terms of the hierarchy

	[Fact]
	public void AddRowWithText_AppendsARootTreeRow()
	{
		var table = Backlog();

		table.AddRow("Feature C");

		var added = Assert.IsType<TreeTableRow>(table.RootRows[^1]);
		Assert.Equal("Feature C", added.Cells[0]);
	}

	[Fact]
	public void AddRowWithATreeRow_BringsItsSubtree()
	{
		var table = Backlog();
		var feature = new TreeTableRow("Feature C");
		feature.AddChild("Story C1");

		table.AddRow(feature);

		Assert.Equal(["Feature C", "Story C1"], Titles(table.Rows.Skip(7)));
	}

	[Fact]
	public void APlainTableRow_IsARootWithoutChildren()
	{
		var table = Backlog();

		table.AddRow(new TableRow("Plain"));

		Assert.Equal("Plain", table.RootRows[^1].Cells[0]);
		Assert.Equal("Plain", table.Rows[^1].Cells[0]);
	}

	[Fact]
	public void InsertRow_LandsAtTheFirstRootAtOrAfterTheIndex()
	{
		var table = Backlog();

		table.InsertRow(2, "Feature X");                 // index 2 is inside Feature A's subtree

		Assert.Equal(["Feature A", "Feature X", "Feature B"], Titles(table.RootRows));
	}

	[Fact]
	public void InsertRow_AtARootBoundary_InsertsThere()
	{
		var table = Backlog();

		table.InsertRow(5, "Feature X");                 // index 5 is Feature B

		Assert.Equal(["Feature A", "Feature X", "Feature B"], Titles(table.RootRows));
	}

	[Fact]
	public void RemoveRowByIndex_RemovesThatRowWithItsDescendants()
	{
		var table = Backlog();

		table.RemoveRow(1);                              // Story A1

		Assert.Equal(["Feature A", "Story A2", "Feature B", "Story B1"], Titles(table.Rows));
	}

	[Fact]
	public void ClearRows_ClearsTheHierarchy()
	{
		var table = Backlog();
		var feature = (TreeTableRow)table.RootRows[0];

		table.ClearRows();

		Assert.Empty(table.Rows);
		Assert.Empty(table.RootRows);
		feature.AddChild("Still usable");                // detached rows can be reused, subtree and all
		table.AddRow(feature);
		Assert.Equal(["Feature A", "Story A1", "Task A1a", "Task A1b", "Story A2", "Still usable"], Titles(table.Rows));
	}

	[Fact]
	public void SetData_ReplacesTheRoots()
	{
		var table = Backlog();
		var kept = (TreeTableRow)table.RootRows[1];

		table.SetData([new TreeTableRow("Feature Z"), kept]);

		Assert.Equal(["Feature Z", "Feature B", "Story B1"], Titles(table.Rows));
	}

	[Fact]
	public void UpdateCell_AddressesRowsDepthFirst()
	{
		var table = Backlog();

		table.UpdateCell(2, 0, "Task renamed");

		Assert.Equal("Task renamed", Row(table, "Task renamed").Cells[0]);
		Assert.Equal("Story A1", Row(table, "Task renamed").Parent?.Cells[0]);
	}

	[Fact]
	public void WithADataSource_TheHierarchyIsRefused()
	{
		var table = new TreeTableControl { DataSource = new OneRowSource() };

		Assert.Throws<InvalidOperationException>(() => table.AddRootRow("x"));
		Assert.Throws<InvalidOperationException>(() => table.AddRow("x"));
	}

	private sealed class OneRowSource : ITableDataSource
	{
		public event System.Collections.Specialized.NotifyCollectionChangedEventHandler? CollectionChanged { add { } remove { } }

		public int RowCount => 1;
		public int ColumnCount => 1;
		public string GetColumnHeader(int columnIndex) => "Only";
		public string GetCellValue(int rowIndex, int columnIndex) => "a";
	}

	#endregion

	#region Batches

	[Fact]
	public void ABatch_AppliesItsChangesAtTheEnd()
	{
		var table = new TreeTableControl();
		table.AddColumn("Title");
		int countInside = -1;

		table.BatchUpdate(() =>
		{
			for (int i = 0; i < 100; i++)
				table.AddRootRow($"Row {i}");
			countInside = table.Rows.Count;
		});

		Assert.Equal(0, countInside);
		Assert.Equal(100, table.Rows.Count);
	}

	[Fact]
	public void ABatchThatThrows_StillAppliesWhatItChanged()
	{
		var table = new TreeTableControl();
		table.AddColumn("Title");

		Assert.Throws<InvalidOperationException>(() => table.BatchUpdate(() =>
		{
			table.AddRootRow("kept");
			throw new InvalidOperationException("boom");
		}));

		Assert.Equal(["kept"], Titles(table.Rows));
	}

	[Fact]
	public void NestedBatches_ApplyWhenTheOutermostEnds()
	{
		var table = new TreeTableControl();
		table.AddColumn("Title");
		int countAfterInner = -1;

		table.BatchUpdate(() =>
		{
			table.BatchUpdate(() => table.AddRootRow("inner"));
			countAfterInner = table.Rows.Count;
		});

		Assert.Equal(0, countAfterInner);
		Assert.Single(table.Rows);
	}

	#endregion
}

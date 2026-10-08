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
/// Where the cursor of a <see cref="TreeTableControl"/> goes when its row is removed: to the next
/// sibling, else the previous one, else the parent, rather than to whatever row followed the removed
/// subtree.
/// </summary>
public class TreeTableSelectionTests
{
	#region Helpers

	/// <summary>
	/// Galaxy Gate > (Moon Base > (Dome, Greenhouse, Rover bay), Launch pad);
	/// Orbit Ring > (Dock > (Airlock)); Comet Watch.
	/// </summary>
	private static TreeTableControl Colony()
	{
		var table = new TreeTableControl { ReadOnly = false, SortingEnabled = true, FilteringEnabled = true };
		table.AddColumn("Name");
		var gate = table.AddRootRow("Galaxy Gate");
		var moonBase = gate.AddChild("Moon Base");
		moonBase.AddChild("Dome");
		moonBase.AddChild("Greenhouse");
		moonBase.AddChild("Rover bay");
		gate.AddChild("Launch pad");
		table.AddRootRow("Orbit Ring").AddChild("Dock").AddChild("Airlock");
		table.AddRootRow("Comet Watch");
		return table;
	}

	private static TreeTableRow Row(TreeTableControl table, string name)
		=> (TreeTableRow)table.Rows.Single(r => r.Cells[0] == name);

	private static string? Selected(TreeTableControl table) => table.SelectedRow?.Cells[0];

	#endregion

	#region Removing the selected row

	[Fact]
	public void RemovingTheSelectedRow_SelectsItsNextSibling()
	{
		var table = Colony();
		table.SelectRow(Row(table, "Greenhouse"));

		table.RemoveRow(Row(table, "Greenhouse"));

		Assert.Equal("Rover bay", Selected(table));
	}

	[Fact]
	public void RemovingTheLastSibling_SelectsThePreviousOne()
	{
		// The row now in its place would be Orbit Ring, under another parent.
		var table = Colony();
		table.SelectRow(Row(table, "Launch pad"));

		table.RemoveRow(Row(table, "Launch pad"));

		Assert.Equal("Moon Base", Selected(table));
	}

	[Fact]
	public void RemovingAnOnlyChild_SelectsTheParent()
	{
		// The row now in its place would be Comet Watch.
		var table = Colony();
		table.SelectRow(Row(table, "Dock"));

		table.RemoveRow(Row(table, "Dock"));

		Assert.Equal("Orbit Ring", Selected(table));
	}

	[Fact]
	public void RemovingTheRowAboveTheSelection_SelectsThatRowsNextSibling()
	{
		var table = Colony();
		table.SelectRow(Row(table, "Greenhouse"));

		table.RemoveRow(Row(table, "Moon Base"));

		Assert.Equal("Launch pad", Selected(table));
	}

	[Fact]
	public void TheNextSibling_IsTheOneDisplayedBelow_InASortedView()
	{
		// Sorted descending: Rover bay, Greenhouse, Dome. Greenhouse's next sibling as added is Rover bay.
		var table = Colony();
		table.SortByColumn(0);
		table.SortByColumn(0);
		table.SelectRow(Row(table, "Greenhouse"));

		table.RemoveRow(Row(table, "Greenhouse"));

		Assert.Equal("Dome", Selected(table));
	}

	[Fact]
	public void ASiblingTheFilterHides_IsPassedOver()
	{
		// "e" leaves out Launch pad; the row now in its place would be Comet Watch.
		var table = Colony();
		table.ApplyFilter("Name:e");
		table.SelectRow(Row(table, "Moon Base"));

		table.RemoveRow(Row(table, "Moon Base"));

		Assert.Equal("Galaxy Gate", Selected(table));
	}

	[Fact]
	public void RemovingEveryChildInABatch_SelectsTheParent()
	{
		var table = Colony();
		table.SelectRow(Row(table, "Dome"));
		var moonBase = Row(table, "Moon Base");

		table.BatchUpdate(() =>
		{
			foreach (var child in moonBase.Children.ToList())
				table.RemoveRow(child);
		});

		Assert.Equal("Moon Base", Selected(table));
	}

	[Fact]
	public void ClearingTheChildren_SelectsTheParent()
	{
		var table = Colony();
		table.SelectRow(Row(table, "Rover bay"));

		Row(table, "Moon Base").ClearChildren();

		Assert.Equal("Moon Base", Selected(table));
	}

	[Fact]
	public void TheInheritedRemoveRow_FollowsTheSameRule()
	{
		var table = Colony();
		table.SelectRow(Row(table, "Launch pad"));

		table.RemoveRow(table.Rows.ToList().IndexOf(Row(table, "Launch pad")));

		Assert.Equal("Moon Base", Selected(table));
	}

	[Fact]
	public void TheSelectionEvents_NameTheNewRowOnce()
	{
		var table = Colony();
		table.SelectRow(Row(table, "Launch pad"));
		var changed = new List<string?>();
		table.SelectedRowItemChanged += (_, row) => changed.Add(row?.Cells[0]);

		table.RemoveRow(Row(table, "Launch pad"));

		Assert.Equal(["Moon Base"], changed);
	}

	[Fact]
	public void RemovingTheLastRoot_SelectsThePreviousRoot()
	{
		var table = Colony();
		table.SelectRow(Row(table, "Comet Watch"));

		table.RemoveRow(Row(table, "Comet Watch"));

		Assert.Equal("Orbit Ring", Selected(table));
	}

	[Fact]
	public void RemovingAnotherRow_LeavesTheSelectionOnItsRow()
	{
		var table = Colony();
		table.SelectRow(Row(table, "Airlock"));

		table.RemoveRow(Row(table, "Moon Base"));

		Assert.Equal("Airlock", Selected(table));
	}

	#endregion
}

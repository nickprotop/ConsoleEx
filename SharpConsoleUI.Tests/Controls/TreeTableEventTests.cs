// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// The expansion events of a <see cref="TreeTableControl"/>, raised the same way whatever expands or
/// collapses a row, and the overridable filter and sibling order.
/// </summary>
public class TreeTableEventTests
{
	#region Helpers

	/// <summary>Counts how often the displayed rows are computed.</summary>
	private class CountingTable : TreeTableControl
	{
		public int Computes { get; private set; }

		protected override int[]? ComputeDisplayRows(TableDisplayQuery query)
		{
			Computes++;
			return base.ComputeDisplayRows(query);
		}
	}

	/// <summary>Kitchen > (Pantry > (Flour, Sugar), Fridge > (Milk)); Garage.</summary>
	private static CountingTable House()
	{
		var table = new CountingTable { ReadOnly = false, FilteringEnabled = true, BorderStyle = BorderStyle.None };
		table.AddColumn("Room");
		var kitchen = table.AddRootRow("Kitchen");
		var pantry = kitchen.AddChild("Pantry");
		pantry.AddChild("Flour");
		pantry.AddChild("Sugar");
		kitchen.AddChild("Fridge").AddChild("Milk");
		table.AddRootRow("Garage");
		return table;
	}

	private static TreeTableRow Row(TreeTableControl table, string room)
		=> (TreeTableRow)table.Rows.Single(r => r.Cells[0] == room);

	private static List<string> Displayed(TableControl table)
		=> Enumerable.Range(0, table.RowCount).Select(i => table.GetRow(table.MapDisplayToData(i)).Cells[0]).ToList();

	/// <summary>Records every expansion event as "Changing Pantry -" or "Changed Pantry +".</summary>
	private static List<string> Record(TreeTableControl table)
	{
		var log = new List<string>();
		table.RowExpansionChanging += (_, e) => log.Add($"Changing {e.Row.Cells[0]} {(e.IsExpanded ? '+' : '-')}");
		table.RowExpansionChanged += (_, e) => log.Add($"Changed {e.Row.Cells[0]} {(e.IsExpanded ? '+' : '-')}");
		return log;
	}

	#endregion

	#region Changing, then Changed

	[Fact]
	public void Collapsing_RaisesChanging_ThenChanged()
	{
		var table = House();
		var log = Record(table);

		Assert.True(table.Collapse(Row(table, "Pantry")));

		Assert.Equal(["Changing Pantry -", "Changed Pantry -"], log);
	}

	[Fact]
	public void TheArguments_NameTheRowAndWhatItBecomes()
	{
		var table = House();
		TreeTableRowExpansionEventArgs? changed = null;
		table.RowExpansionChanged += (_, e) => changed = e;

		table.Collapse(Row(table, "Fridge"));

		Assert.Same(Row(table, "Fridge"), changed?.Row);
		Assert.False(changed?.IsExpanded);
		Assert.False(changed?.IsFilteredView);
	}

	[Fact]
	public void SettingIsExpanded_RaisesTheSameEvents()
	{
		var table = House();
		var log = Record(table);

		Row(table, "Pantry").IsExpanded = false;

		Assert.Equal(["Changing Pantry -", "Changed Pantry -"], log);
	}

	[Fact]
	public void ClickingTheExpander_RaisesTheSameEvents()
	{
		var table = House();
		var buffer = new CharacterBuffer(30, 8);
		var bounds = new LayoutRect(0, 0, 30, 8);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		var log = Record(table);

		var point = new System.Drawing.Point(4, 2);    // Pantry's [-]
		table.ProcessMouseEvent(new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Clicked }, point, point, point));

		Assert.Equal(["Changing Pantry -", "Changed Pantry -"], log);
	}

	[Fact]
	public void NothingToChange_RaisesNothing()
	{
		var table = House();
		var log = Record(table);

		Assert.False(table.Expand(Row(table, "Pantry")));
		Row(table, "Pantry").IsExpanded = true;

		Assert.Empty(log);
	}

	[Fact]
	public void TheAsyncTwin_IsRaisedToo()
	{
		var table = House();
		TreeTableRow? changed = null;
		table.RowExpansionChangedAsync += (_, e) =>
		{
			changed = e.Row;
			return Task.CompletedTask;
		};

		table.Collapse(Row(table, "Pantry"));

		Assert.Same(Row(table, "Pantry"), changed);
	}

	[Fact]
	public void Changed_IsRaisedOnceTheRowsDisplayedShowIt()
	{
		var table = House();
		List<string>? displayedThen = null;
		table.RowExpansionChanged += (_, _) => displayedThen = Displayed(table);

		table.Collapse(Row(table, "Pantry"));

		Assert.Equal(["Kitchen", "Pantry", "Fridge", "Milk", "Garage"], displayedThen);
	}

	[Fact]
	public void Changed_FollowsTheSelectionEventsTheChangeCaused()
	{
		var table = House();
		table.SelectRow(Row(table, "Sugar"));
		var log = new List<string>();
		table.SelectedRowItemChanged += (_, row) => log.Add($"Selected {row?.Cells[0]}");
		table.RowExpansionChanged += (_, e) => log.Add($"Changed {e.Row.Cells[0]}");

		table.Collapse(Row(table, "Pantry"));

		Assert.Equal(["Selected Pantry", "Changed Pantry"], log);
	}

	#endregion

	#region Cancelling

	[Fact]
	public void CancellingChanging_KeepsTheRowAsItIs()
	{
		var table = House();
		table.RowExpansionChanging += (_, e) => e.Cancel = true;
		int changed = 0;
		table.RowExpansionChanged += (_, _) => changed++;

		Assert.False(table.Collapse(Row(table, "Pantry")));
		Row(table, "Fridge").IsExpanded = false;

		Assert.True(Row(table, "Pantry").IsExpanded);
		Assert.True(Row(table, "Fridge").IsExpanded);
		Assert.Contains("Flour", Displayed(table));
		Assert.Equal(0, changed);
	}

	[Fact]
	public void ACancelledRow_IsLeftOutOfABulkChange_WhileTheOthersChange()
	{
		var table = House();
		table.RowExpansionChanging += (_, e) => e.Cancel = e.Row.Cells[0] == "Fridge";
		var log = Record(table);

		table.CollapseAll();

		Assert.False(Row(table, "Kitchen").IsExpanded);
		Assert.False(Row(table, "Pantry").IsExpanded);
		Assert.True(Row(table, "Fridge").IsExpanded);
		Assert.Equal(["Changed Kitchen -", "Changed Pantry -"], log.Where(l => l.StartsWith("Changed")));
	}

	#endregion

	#region Bulk, batched and filtered changes

	[Fact]
	public void ABulkChange_RaisesForEachRow_AndRecomputesOnce()
	{
		var table = House();
		table.CollapseAll();
		var log = Record(table);
		int before = table.Computes;

		table.ExpandAll();

		Assert.Equal(1, table.Computes - before);
		Assert.Equal(3, log.Count(l => l.StartsWith("Changing")));
		Assert.Equal(3, log.Count(l => l.StartsWith("Changed")));
	}

	[Fact]
	public void InABatch_ChangedWaitsForTheBatchToEnd()
	{
		var table = House();
		var log = Record(table);

		table.BatchUpdate(() =>
		{
			table.Collapse(Row(table, "Pantry"));
			log.Add("batch body done");
		});

		Assert.Equal(["Changing Pantry -", "batch body done", "Changed Pantry -"], log);
	}

	[Fact]
	public void WhileFiltered_TheEventsSayTheChangeIsToTheFilteredView()
	{
		var table = House();
		table.ApplyFilter("Milk");
		TreeTableRowExpansionEventArgs? changed = null;
		table.RowExpansionChanged += (_, e) => changed = e;

		table.Collapse(Row(table, "Fridge"));

		Assert.True(changed?.IsFilteredView);
		Assert.True(Row(table, "Fridge").IsExpanded);
	}

	[Fact]
	public void SettingIsExpandedWhileFiltered_IsAChangeToTheRowsOwnState()
	{
		var table = House();
		table.ApplyFilter("Milk");
		TreeTableRowExpansionEventArgs? changed = null;
		table.RowExpansionChanged += (_, e) => changed = e;

		Row(table, "Pantry").IsExpanded = false;

		Assert.False(changed?.IsFilteredView);
		Assert.False(Row(table, "Pantry").IsExpanded);
	}

	#endregion

	#region Loading children on demand

	[Fact]
	public void ChildrenAddedFromChanging_AreDisplayedWithTheExpansion_InOneRecompute()
	{
		var table = House();
		var garage = Row(table, "Garage");
		garage.IsExpanded = false;
		garage.HasUnrealizedChildren = true;
		table.RowExpansionChanging += (_, e) =>
		{
			if (!e.IsExpanded || !e.Row.HasUnrealizedChildren) return;

			e.Row.AddChild("Bicycle");
			e.Row.AddChild("Toolbox");
			e.Row.HasUnrealizedChildren = false;
		};
		int before = table.Computes;

		table.Expand(garage);

		Assert.Equal(1, table.Computes - before);
		Assert.Equal(["Garage", "Bicycle", "Toolbox"], Displayed(table).Skip(6));
		Assert.False(garage.HasUnrealizedChildren);
	}

	#endregion

	#region Overriding the filter and the sibling order

	/// <summary>Matches rows on their tag too, and sorts rows with children before rows without.</summary>
	private sealed class CupboardTable : TreeTableControl
	{
		protected override bool IsFilterMatch(TableRow row, CompoundFilterExpression filter)
			=> row.Tag is string label && label.Contains(filter.RawText, StringComparison.OrdinalIgnoreCase)
				|| base.IsFilterMatch(row, filter);

		protected override int CompareSiblings(TableRow x, TableRow y, int columnIndex, SortDirection direction)
		{
			bool xNests = x is TreeTableRow { Children.Count: > 0 };
			bool yNests = y is TreeTableRow { Children.Count: > 0 };
			return xNests != yNests ? (xNests ? -1 : 1) : base.CompareSiblings(x, y, columnIndex, direction);
		}
	}

	private static CupboardTable Cupboard()
	{
		var table = new CupboardTable { SortingEnabled = true, FilteringEnabled = true, ReadOnly = false };
		table.AddColumn("Item");
		table.AddRootRow("Apron");
		var drawer = table.AddRootRow("Drawer");
		drawer.AddChild("Spoon").Tag = "cutlery";
		drawer.AddChild("Napkin");
		table.AddRootRow("Bowl");
		return table;
	}

	[Fact]
	public void AnOverriddenFilterMatch_DecidesWhatMatches()
	{
		var table = Cupboard();

		table.ApplyFilter("cutlery");

		Assert.Equal(["Drawer", "Spoon"], Displayed(table));
	}

	[Fact]
	public void AnOverriddenSiblingOrder_DecidesTheSort()
	{
		var table = Cupboard();

		table.SortByColumn(0);

		Assert.Equal(["Drawer", "Napkin", "Spoon", "Apron", "Bowl"], Displayed(table));
	}

	#endregion
}

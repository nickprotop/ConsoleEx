// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Which rows a <see cref="TableControl"/> shows, and in what order, once sorting, filtering and
/// row changes combine. Each region pins one bug in that pipeline, asserted on the composited
/// screen where the bug was visible, or on the order the table reports.
/// </summary>
public class TableDisplayPipelineTests
{
	#region Helpers

	private static readonly string[] People = ["Alice", "Bob", "Carol", "Dave", "Eve"];

	/// <summary>A borderless one-column table of <see cref="People"/>.</summary>
	private static TableControl PeopleTable(int? height = null)
	{
		var table = new TableControl { BorderStyle = BorderStyle.None, Height = height };
		table.AddColumn("Name");
		foreach (var name in People)
			table.AddRow(name);
		return table;
	}

	/// <summary>Hosts the table in a frameless maximized window on a headless screen.</summary>
	private static ConsoleWindowSystem Host(TableControl table, int width = 30, int height = 14)
	{
		var system = ChromeGeometry.CreateSystem(width, height);
		var window = new WindowBuilder(system).Frameless().Maximized().Build();
		window.AddControl(table);
		system.WindowStateService.AddWindow(window);
		return system;
	}

	/// <summary>Renders the screen and returns its rows, trimmed.</summary>
	private static List<string> Screen(ConsoleWindowSystem system)
	{
		var snap = ChromeGeometry.Render(system);
		return Enumerable.Range(0, snap.Height)
			.Select(y => ChromeGeometry.Row(snap, y).TrimEnd('\0', ' '))
			.ToList();
	}

	/// <summary>The data rows on screen: every rendered line that names one of the people.</summary>
	private static List<string> NamesOnScreen(ConsoleWindowSystem system)
		=> Screen(system).Select(line => line.Trim()).Where(line => People.Contains(line)).ToList();

	/// <summary>The first cell of every displayed row, in display order.</summary>
	private static List<string> Displayed(TableControl table)
		=> Enumerable.Range(0, table.RowCount)
			.Select(i => table.GetRow(table.MapDisplayToData(i)).Cells[0])
			.ToList();

	/// <summary>
	/// Three rows whose <see cref="TableRow.Tag"/> orders them against the alphabet, sorted through
	/// a <see cref="TableColumn.CustomRowComparer"/> on that tag. Every name contains "Al".
	/// </summary>
	private static TableControl TaggedTable()
	{
		var table = new TableControl { SortingEnabled = true };
		table.AddColumn(new TableColumn("Name")
		{
			CustomRowComparer = (x, y) => ((int)x.Tag!).CompareTo((int)y.Tag!)
		});
		table.AddRow(new TableRow("Albert") { Tag = 3 });
		table.AddRow(new TableRow("Alan") { Tag = 1 });
		table.AddRow(new TableRow("Alice") { Tag = 2 });
		return table;
	}

	#endregion

	#region A filtered table paints its matches only

	[Fact]
	public void AFilteredTableTallerThanItsMatches_PaintsOnlyTheMatches()
	{
		// Height leaves room for every row, so the space below the three matches is where the
		// unmatched rows used to be painted, read through an identity fallback past the map.
		var table = PeopleTable(height: 8);
		var system = Host(table);

		table.ApplyFilter("a");

		Assert.Equal(["Alice", "Carol", "Dave"], NamesOnScreen(system));
	}

	[Fact]
	public void AFilteredTable_StillPaintsOnlyTheMatches_AfterARerender()
	{
		var table = PeopleTable(height: 8);
		var system = Host(table);
		table.ApplyFilter("a");

		Screen(system);
		Screen(system);

		Assert.Equal(["Alice", "Carol", "Dave"], NamesOnScreen(system));
	}

	#endregion

	#region A filtered sort orders rows the way an unfiltered one does

	[Fact]
	public void SortingUnderAFilter_UsesTheColumnsRowComparer()
	{
		var table = TaggedTable();
		table.ApplyFilter("Al");

		table.SortByColumn(0);

		Assert.Equal(["Alan", "Alice", "Albert"], Displayed(table));
	}

	[Fact]
	public void FilteringASortedTable_KeepsTheRowComparersOrder()
	{
		var table = TaggedTable();
		table.SortByColumn(0);
		var unfiltered = Displayed(table);

		table.ApplyFilter("Al");

		Assert.Equal(["Alan", "Alice", "Albert"], unfiltered);
		Assert.Equal(unfiltered, Displayed(table));
	}

	#endregion

	#region Rows with equal keys keep their data order

	/// <summary>
	/// 64 rows whose "Group" column holds only "x" or "y", alternating, and whose "Id" column is
	/// the data order. Enough rows that the sort cannot fall back to an insertion sort, which
	/// would hide an unstable one.
	/// </summary>
	private static TableControl GroupedTable()
	{
		var table = new TableControl { SortingEnabled = true };
		table.AddColumn("Id");
		table.AddColumn("Group");
		for (int i = 0; i < 64; i++)
			table.AddRow(i.ToString("D2"), i % 2 == 0 ? "x" : "y");
		return table;
	}

	/// <summary>The ids of the displayed rows in one group, in display order.</summary>
	private static List<string> IdsIn(TableControl table, string group)
		=> Enumerable.Range(0, table.RowCount)
			.Select(i => table.GetRow(table.MapDisplayToData(i)))
			.Where(row => row.Cells[1] == group)
			.Select(row => row.Cells[0])
			.ToList();

	/// <summary>The ids of one group in data order.</summary>
	private static List<string> DataOrderOf(TableControl table, string group)
		=> table.Rows.Where(row => row.Cells[1] == group).Select(row => row.Cells[0]).ToList();

	[Fact]
	public void SortingAscending_KeepsEqualKeysInDataOrder()
	{
		var table = GroupedTable();

		table.SortByColumn(1);

		Assert.Equal(DataOrderOf(table, "x"), IdsIn(table, "x"));
		Assert.Equal(DataOrderOf(table, "y"), IdsIn(table, "y"));
	}

	[Fact]
	public void SortingDescending_KeepsEqualKeysInDataOrder()
	{
		var table = GroupedTable();

		table.SortByColumn(1);
		table.SortByColumn(1);

		Assert.Equal(SortDirection.Descending, table.CurrentSortDirection);
		Assert.Equal("y", table.GetRow(table.MapDisplayToData(0)).Cells[1]);
		Assert.Equal(DataOrderOf(table, "x"), IdsIn(table, "x"));
		Assert.Equal(DataOrderOf(table, "y"), IdsIn(table, "y"));
	}

	[Fact]
	public void SortingUnderAFilter_KeepsEqualKeysInDataOrder()
	{
		var table = GroupedTable();
		table.ApplyFilter("Group:x|y");

		table.SortByColumn(1);

		Assert.Equal(64, table.RowCount);
		Assert.Equal(DataOrderOf(table, "x"), IdsIn(table, "x"));
		Assert.Equal(DataOrderOf(table, "y"), IdsIn(table, "y"));
	}

	#endregion

	#region The default sort reads the text, not the markup

	/// <summary>Fruit whose markup, read as text, would sort them differently from their names.</summary>
	private static TableControl MarkedUpTable()
	{
		var table = new TableControl { SortingEnabled = true };
		table.AddColumn("Fruit");
		table.AddRow("[red]Apple[/]");
		table.AddRow("Banana");
		table.AddRow("[blue]Cherry[/]");
		return table;
	}

	[Fact]
	public void TheDefaultSort_OrdersByTheTextTheMarkupShows()
	{
		var table = MarkedUpTable();

		table.SortByColumn(0);

		Assert.Equal(["[red]Apple[/]", "Banana", "[blue]Cherry[/]"], Displayed(table));
	}

	[Fact]
	public void TheDefaultSort_UnderAFilter_OrdersByTheTextTheMarkupShows()
	{
		var table = MarkedUpTable();
		table.ApplyFilter("a");

		table.SortByColumn(0);

		Assert.Equal(["[red]Apple[/]", "Banana"], Displayed(table));
	}

	[Fact]
	public void ACustomComparer_StillReceivesTheRawCellText()
	{
		var seen = new List<string>();
		var table = new TableControl { SortingEnabled = true };
		table.AddColumn(new TableColumn("Fruit")
		{
			CustomComparer = Comparer<string>.Create((x, y) =>
			{
				seen.Add(x);
				seen.Add(y);
				return string.CompareOrdinal(x, y);
			})
		});
		table.AddRow("[red]Apple[/]");
		table.AddRow("Banana");

		table.SortByColumn(0);

		Assert.Contains("[red]Apple[/]", seen);
	}

	#endregion
}

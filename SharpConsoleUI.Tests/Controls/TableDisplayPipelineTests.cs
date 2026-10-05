// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
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

	#region One display map, so the order on screen is the order on the header

	/// <summary>Three people whose first and last names sort in opposite orders.</summary>
	private static TableControl FirstLastTable()
	{
		var table = new TableControl { SortingEnabled = true, FilteringEnabled = true, ReadOnly = false };
		table.AddColumn("First");
		table.AddColumn("Last");
		table.AddRow("Ann", "Zed");
		table.AddRow("Bob", "Young");
		table.AddRow("Cid", "Xu");
		return table;
	}

	private static ConsoleKeyInfo Key(ConsoleKey key, char ch = '\0') => new(ch, key, false, false, false);

	[Fact]
	public void BackspacingAFilterToEmpty_ShowsTheSortTheHeaderShows()
	{
		var table = FirstLastTable();
		table.SortByColumn(0);
		table.EnterFilterMode();
		table.ProcessFilterKey(Key(ConsoleKey.U, 'u'));
		table.SortByColumn(1);

		table.ProcessFilterKey(Key(ConsoleKey.Backspace));

		Assert.Equal(1, table.SortColumnIndex);
		Assert.Equal(["Cid", "Bob", "Ann"], Displayed(table));
	}

	[Fact]
	public void ClearingAFilter_ShowsTheSortChangedWhileItWasActive()
	{
		var table = FirstLastTable();
		table.SortByColumn(0);
		table.ApplyFilter("n|b|d");
		table.SortByColumn(1);

		table.ClearFilter();

		Assert.Equal(["Cid", "Bob", "Ann"], Displayed(table));
	}

	[Fact]
	public void ASortedTable_CountsAllItsRowsWhenFilteringStarts()
	{
		var table = FirstLastTable();
		table.SortByColumn(0);

		table.EnterFilterMode();

		Assert.Equal(3, table._unfilteredRowCount);
	}

	#endregion

	#region A hidden row has no display position

	[Fact]
	public void ARowTheFilterHides_MapsToNoDisplayPosition()
	{
		var table = PeopleTable();

		table.ApplyFilter("a");

		Assert.Equal(-1, table.MapDataToDisplay(1));   // Bob
		Assert.Equal(-1, table.MapDataToDisplay(4));   // Eve
	}

	[Fact]
	public void ADisplayedRow_MapsToItsPositionUnderSortAndFilter()
	{
		var table = PeopleTable();
		table.SortingEnabled = true;
		table.ApplyFilter("a");

		table.SortByColumn(0);
		table.SortByColumn(0);

		Assert.Equal(["Dave", "Carol", "Alice"], Displayed(table));
		Assert.Equal(0, table.MapDataToDisplay(3));    // Dave
		Assert.Equal(1, table.MapDataToDisplay(2));    // Carol
		Assert.Equal(2, table.MapDataToDisplay(0));    // Alice
	}

	[Fact]
	public void WithoutAMap_EveryRowIsItsOwnDisplayPosition()
	{
		var table = PeopleTable();

		Assert.Equal(3, table.MapDataToDisplay(3));
	}

	#endregion

	#region Changing the rows keeps the sort and the filter

	[Fact]
	public void AddingARowToASortedTable_PutsItInSortedPlace()
	{
		var table = PeopleTable();
		table.SortingEnabled = true;
		table.SortByColumn(0);
		table.SortByColumn(0);

		table.AddRow("Bert");

		Assert.Equal(["Eve", "Dave", "Carol", "Bob", "Bert", "Alice"], Displayed(table));
	}

	[Fact]
	public void InsertingAndRemovingRows_KeepTheSort()
	{
		var table = PeopleTable();
		table.SortingEnabled = true;
		table.SortByColumn(0);

		table.InsertRows(0, [new TableRow("Zoe"), new TableRow("Aaron")]);
		table.RemoveRow(table.Rows.ToList().FindIndex(r => r.Cells[0] == "Carol"));

		Assert.Equal(["Aaron", "Alice", "Bob", "Dave", "Eve", "Zoe"], Displayed(table));
	}

	[Fact]
	public void AddingRowsToAFilteredTable_ShowsOnlyTheNewMatches()
	{
		var table = PeopleTable();
		table.ApplyFilter("a");

		table.AddRow("Frank");
		table.AddRow("Gus");

		Assert.Equal(["Alice", "Carol", "Dave", "Frank"], Displayed(table));
	}

	[Fact]
	public void ReplacingTheRows_FiltersAndSortsTheNewOnes()
	{
		var table = PeopleTable();
		table.SortingEnabled = true;
		table.SortByColumn(0);
		table.ApplyFilter("a");

		table.SetData([new TableRow("Zara"), new TableRow("Bob"), new TableRow("Anna")]);

		Assert.Equal(["Anna", "Zara"], Displayed(table));
	}

	[Fact]
	public void RefillingASortedTable_RowByRow_KeepsTheSort()
	{
		var table = PeopleTable();
		table.SortingEnabled = true;
		table.SortByColumn(0);

		table.ClearRows();
		foreach (var name in new[] { "Mia", "Leo", "Ava" })
			table.AddRow(name);

		Assert.Equal(0, table.SortColumnIndex);
		Assert.Equal(["Ava", "Leo", "Mia"], Displayed(table));
	}

	[Fact]
	public void AddingRowsUnderAFilter_PaintsTheMatchesOnly()
	{
		var table = PeopleTable(height: 9);
		var system = Host(table);
		table.ApplyFilter("e");

		table.AddRow("Bert");

		Assert.Equal(["Alice", "Dave", "Eve", "Bert"], Screen(system).Select(l => l.Trim()).Where(l => l.Length > 0 && l != "Name").ToList());
	}

	[Fact]
	public void AddingRowsToASortedTable_ComparesEachNewRowOnlyAgainstASortedFew()
	{
		// A sort kept across mutations must not re-sort the whole table per row: refilling a sorted
		// table row by row would then cost O(n² log n) comparisons.
		int comparisons = 0;
		var table = new TableControl { SortingEnabled = true };
		table.AddColumn(new TableColumn("Key")
		{
			CustomComparer = Comparer<string>.Create((x, y) =>
			{
				comparisons++;
				return string.CompareOrdinal(x, y);
			})
		});
		table.SortByColumn(0);
		var random = new Random(7);
		const int n = 2000;

		for (int i = 0; i < n; i++)
			table.AddRow(random.Next(100000).ToString("D6"));

		Assert.True(comparisons <= n * (Math.Log2(n) + 2), $"{comparisons} comparisons for {n} rows");
		var keys = Displayed(table);
		Assert.Equal(keys.Order(StringComparer.Ordinal).ToList(), keys);
	}

	[Fact]
	public void RandomRowChanges_ShowWhatAFullRecomputeShows()
	{
		var random = new Random(42);
		var table = new TableControl { SortingEnabled = true };
		table.AddColumn("Name");
		table.AddColumn("Group");
		string RandomName() => $"{(char)('a' + random.Next(6))}{random.Next(10)}";
		for (int i = 0; i < 40; i++)
			table.AddRow(RandomName(), random.Next(3).ToString());
		table.SortByColumn(1);
		table.ApplyFilter("Group:0|1");

		for (int step = 0; step < 300; step++)
		{
			int count = table.Rows.Count;
			switch (random.Next(4))
			{
				case 0:
					table.AddRow(RandomName(), random.Next(3).ToString());
					break;
				case 1:
					table.InsertRow(random.Next(count + 1), RandomName(), random.Next(3).ToString());
					break;
				case 2 when count > 0:
					table.RemoveRow(random.Next(count));
					break;
				default:
					table.InsertRows(random.Next(count + 1),
						Enumerable.Range(0, random.Next(1, 4)).Select(_ => new TableRow(RandomName(), random.Next(3).ToString())));
					break;
			}

			var expected = table.Rows
				.Select((row, index) => (row, index))
				.Where(x => x.row.Cells[1] != "2")
				.OrderBy(x => x.row.Cells[1], StringComparer.OrdinalIgnoreCase)
				.ThenBy(x => x.index)
				.Select(x => x.index)
				.ToList();
			var actual = Enumerable.Range(0, table.RowCount).Select(table.MapDisplayToData).ToList();
			Assert.True(expected.SequenceEqual(actual), $"display rows diverged at step {step}");
		}
	}

	#endregion

	#region Auto-width columns are sized from the rows on screen

	private const string LongName = "zebra-crossing-attendant";

	/// <summary>
	/// 60 short names and one long one at data row 55, past the 50 rows a column is sampled over
	/// from the top of the data. Sorted descending, the long name is the first row on screen.
	/// </summary>
	private static TableControl LongNameLastTable()
	{
		var table = new TableControl { BorderStyle = BorderStyle.None, SortingEnabled = true, Height = 12 };
		table.AddColumn("Name");
		table.AddColumn("Id", TextJustification.Left, 4);
		for (int i = 0; i < 60; i++)
			table.AddRow(i == 55 ? LongName : $"a{i:D2}", i.ToString());
		table.SortByColumn(0);
		table.SortByColumn(0);
		return table;
	}

	[Fact]
	public void ASortedTable_SizesItsColumnsFromTheRowsOnScreen()
	{
		var table = LongNameLastTable();
		var system = Host(table, width: 60);

		var screen = Screen(system);

		Assert.Equal(LongName, Displayed(table)[0]);
		Assert.Contains(screen, line => line.Contains(LongName));
	}

	[Fact]
	public void AFilteredTable_SizesItsColumnsFromTheRowsOnScreen()
	{
		var table = LongNameLastTable();
		table.ClearSort();
		var system = Host(table, width: 60);

		table.ApplyFilter("e");

		Assert.Contains(Screen(system), line => line.Contains(LongName));
	}

	#endregion
}

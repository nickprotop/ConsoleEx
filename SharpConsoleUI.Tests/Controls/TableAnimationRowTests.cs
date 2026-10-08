// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A row animation stays on the row it was started on: the row shown at the position given, followed
/// through sorts, filters, collapses and changes to the rows, and the only row an animated removal
/// removes.
/// </summary>
public class TableAnimationRowTests
{
	#region Helpers

	private const int Width = 40;
	private const int Height = 10;

	/// <summary>A borderless table, header on line 0, with an animation manager of its own.</summary>
	private static (TableControl Table, AnimationManager Manager) Fruit(params string[] names)
	{
		var table = new TableControl { BorderStyle = BorderStyle.None, SortingEnabled = true, FilteringEnabled = true };
		table.AddColumn("Fruit");
		foreach (var name in names)
			table.AddRow(name);
		var manager = new AnimationManager();
		table.SetAnimationManagerForTesting(manager);
		return (table, manager);
	}

	private static CharacterBuffer Paint(TableControl table)
	{
		var buffer = new CharacterBuffer(Width, Height);
		var bounds = new LayoutRect(0, 0, Width, Height);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return buffer;
	}

	/// <summary>The cells an overlay changed: painted at intensity 0, then at the flash's peak.</summary>
	private static List<(int X, int Y)> Overlaid(TableControl table, AnimationManager manager, double peakMs = 150)
	{
		var before = Paint(table);
		Advance(manager, peakMs);
		var after = Paint(table);

		var cells = new List<(int, int)>();
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				if (before.GetCell(x, y).Background != after.GetCell(x, y).Background)
					cells.Add((x, y));
			}
		}
		return cells;
	}

	private static List<int> OverlaidLines(TableControl table, AnimationManager manager)
		=> Overlaid(table, manager).Select(cell => cell.Y).Distinct().ToList();

	private static List<string> Names(TableControl table) => table.Rows.Select(row => row.Cells[0]).ToList();

	private static void Advance(AnimationManager manager, double totalMs)
	{
		double remaining = totalMs;
		while (remaining > 0)
		{
			double tick = Math.Min(remaining, AnimationDefaults.MaxFrameDeltaMs);
			manager.Update(TimeSpan.FromMilliseconds(tick));
			remaining -= tick;
		}
	}

	private static readonly TimeSpan Flash = TimeSpan.FromMilliseconds(300);

	#endregion

	#region The row that fades is the row removed

	[Fact]
	public void OnASortedTable_TheRowShownAtThePosition_IsRemoved()
	{
		var (table, manager) = Fruit("Cherry", "Apple", "Banana");
		table.SortByColumn(0);                          // Apple, Banana, Cherry

		table.AnimateRowRemoval(0, Flash);
		Advance(manager, 400);

		Assert.Equal(["Cherry", "Banana"], Names(table));
	}

	[Fact]
	public void OnAFilteredTable_TheRowShownAtThePosition_IsRemoved()
	{
		var (table, manager) = Fruit("Cherry", "Apple", "Banana");
		table.ApplyFilter("an");                        // Banana

		table.AnimateRowsRemoval([0], Flash);
		Advance(manager, 400);

		Assert.Equal(["Cherry", "Apple"], Names(table));
	}

	[Fact]
	public void ARowInsertedAboveDuringTheFade_DoesNotSendTheRemovalToAnotherRow()
	{
		// None of the rows has a Tag, so a check by tag cannot tell them apart.
		var (table, manager) = Fruit("Apple", "Banana", "Cherry");

		table.AnimateRowRemoval(1, Flash);              // Banana
		table.InsertRow(0, "Date");
		Advance(manager, 400);

		Assert.Equal(["Date", "Apple", "Cherry"], Names(table));
	}

	[Fact]
	public void ARowAlreadyRemovedDuringTheFade_IsNotReplacedByAnother()
	{
		var (table, manager) = Fruit("Apple", "Banana", "Cherry");

		table.AnimateRowRemoval(1, Flash);
		table.RemoveRow(1);
		Advance(manager, 400);

		Assert.Equal(["Apple", "Cherry"], Names(table));
	}

	[Fact]
	public void WithADataSource_NothingIsRemoved()
	{
		var table = new TableControl { DataSource = new BerrySource() };
		table.SetAnimationManagerForTesting(new AnimationManager());

		Assert.Null(table.AnimateRowRemoval(0, Flash));
		Assert.Null(table.AnimateRowsRemoval([0, 1], Flash));
	}

	#endregion

	#region The overlay stays on its row

	[Fact]
	public void AFlash_FollowsItsRow_WhenTheTableIsSorted()
	{
		var (table, manager) = Fruit("Cherry", "Apple", "Banana");

		table.FlashRow(0, Color.Red, Flash);            // Cherry
		table.SortByColumn(0);                          // Cherry moves to the third line

		Assert.Equal([3], OverlaidLines(table, manager));
	}

	[Fact]
	public void AFlash_FollowsItsRow_WhenARowIsInsertedAbove()
	{
		var (table, manager) = Fruit("Apple", "Banana");

		table.HighlightRow(1, Color.Green, Flash);      // Banana
		table.InsertRow(0, "Cherry");

		var lines = Overlaid(table, manager, peakMs: 1).Select(cell => cell.Y).Distinct().ToList();
		Assert.Equal([3], lines);
	}

	[Fact]
	public void AFlashOnARowTheFilterHides_IsNotDrawn()
	{
		var (table, manager) = Fruit("Apple", "Banana", "Cherry");

		table.FlashRow(2, Color.Red, Flash);            // Cherry
		table.ApplyFilter("an");

		Assert.Empty(OverlaidLines(table, manager));
	}

	[Fact]
	public void AFlashOnARowCollapsedAway_IsNotDrawn()
	{
		var table = new TreeTableControl { BorderStyle = BorderStyle.None };
		table.AddColumn("Orchard");
		table.AddRootRow("Pear tree").AddChild("Pear");
		var manager = new AnimationManager();
		table.SetAnimationManagerForTesting(manager);

		table.FlashRow(1, Color.Red, Flash);            // Pear
		table.Collapse((TreeTableRow)table.Rows[0]);

		Assert.Empty(OverlaidLines(table, manager));
	}

	[Fact]
	public void ARemovalFinishingAboveAFlash_DoesNotLeaveTheFlashBehind()
	{
		var (table, manager) = Fruit("Apple", "Banana", "Cherry", "Date");

		table.FlashRow(3, Color.Red, Flash);
		table.AnimateRowRemoval(0, TimeSpan.FromMilliseconds(100));
		Advance(manager, 400);

		Assert.False(table.HasActiveRowAnimations);
	}

	[Fact]
	public void ACellFlash_LightsTheCellWhereItsColumnIsDrawn_PastTheCheckboxes()
	{
		var table = new TableControl { BorderStyle = BorderStyle.None, CheckboxMode = true };
		table.AddColumn("Fruit", TextJustification.Left, 6);
		table.AddColumn("Colour", TextJustification.Left, 6);
		table.AddRow("Apple", "Red");
		var manager = new AnimationManager();
		table.SetAnimationManagerForTesting(manager);
		Paint(table);
		var columnCells = Enumerable.Range(0, Width).Where(x => table.HitTest(x, 1).ColumnIndex == 1).ToList();

		table.FlashCell(0, 1, Color.Red, Flash);

		Assert.Equal(columnCells, Overlaid(table, manager).Select(cell => cell.X).ToList());
	}

	#endregion

	#region A data source's rows

	/// <summary>Berries, added and removed under the table with the changes announced.</summary>
	private sealed class BerrySource : ITableDataSource
	{
		private readonly List<string> _berries = ["Blackberry", "Blueberry", "Cranberry"];

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public int RowCount => _berries.Count;

		public int ColumnCount => 1;

		public string GetColumnHeader(int columnIndex) => "Berry";

		public string GetCellValue(int rowIndex, int columnIndex) => _berries[rowIndex];

		public void Insert(int index, string berry)
		{
			_berries.Insert(index, berry);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, berry, index));
		}

		public void RemoveAt(int index)
		{
			string berry = _berries[index];
			_berries.RemoveAt(index);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, berry, index));
		}
	}

	[Fact]
	public void AFlashOnASourceRow_FollowsIt_WhenARowIsAddedAbove()
	{
		var source = new BerrySource();
		var table = new TableControl { BorderStyle = BorderStyle.None, DataSource = source };
		var manager = new AnimationManager();
		table.SetAnimationManagerForTesting(manager);

		table.FlashRow(1, Color.Red, Flash);            // Blueberry
		source.Insert(0, "Gooseberry");

		Assert.Equal([3], OverlaidLines(table, manager));
	}

	[Fact]
	public void AFlashOnASourceRowRemoved_IsNotDrawnOnTheRowNowInItsPlace()
	{
		var source = new BerrySource();
		var table = new TableControl { BorderStyle = BorderStyle.None, DataSource = source };
		var manager = new AnimationManager();
		table.SetAnimationManagerForTesting(manager);

		table.FlashRow(1, Color.Red, Flash);
		source.RemoveAt(1);

		Assert.Empty(OverlaidLines(table, manager));
	}

	#endregion
}

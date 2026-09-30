// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// When an <see cref="ITableDataSource"/> filters itself, the control leaves
/// <c>_filterIndexMap</c> null by design — the source has already narrowed, so there is no display
/// map to build. The filter footer counted that null map and always reported "No matches", even
/// though the matching rows were on screen right above it.
/// </summary>
public class TableDelegatedFilterFooterTests
{
	/// <summary>A source that narrows itself, standing in for a server-side WHERE.</summary>
	private sealed class SelfFilteringSource : ITableDataSource
	{
		private readonly List<string[]> _all;
		private List<string[]> _visible;

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public SelfFilteringSource(params string[][] rows)
		{
			_all = new List<string[]>(rows);
			_visible = new List<string[]>(rows);
		}

		public bool CanFilter => true;
		public int RowCount => _visible.Count;
		public int ColumnCount => 2;
		public string GetColumnHeader(int c) => c == 0 ? "Name" : "City";
		public string GetCellValue(int r, int c) => _visible[r][c];
		public object? GetRowTag(int rowIndex) => null;

		public void ApplyFilter(string filterText, string? columnName, FilterOperator op)
		{
			_visible = _all.FindAll(row =>
			{
				foreach (var cell in row)
					if (cell.Contains(filterText, StringComparison.OrdinalIgnoreCase)) return true;
				return false;
			});
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		public void ClearFilter()
		{
			_visible = new List<string[]>(_all);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
	}

	private static TableControl FilteredTable(string filter, out SelfFilteringSource source)
	{
		source = new SelfFilteringSource(
			new[] { "Alice", "Athens" },
			new[] { "Bob", "Berlin" },
			new[] { "Carol", "Cairo" },
			new[] { "Dave", "Dublin" });

		var table = new TableControl();
		table.DataSource = source;

		// Entering filter mode is what captures the unfiltered total, exactly as pressing '/' does.
		table.EnterFilterMode();
		table.ApplyFilter(filter);
		return table;
	}

	/// <summary>Renders the footer and returns its text.</summary>
	private static string Footer(TableControl table, int width = 70)
	{
		var buffer = new CharacterBuffer(width, 3);
		var clip = new LayoutRect(0, 0, width, 3);
		table.DrawFilterStatusBar(buffer, 0, 0, width, clip,
			Color.White, Color.Black, BoxChars.Single, Color.White, hasBorder: false);

		var sb = new System.Text.StringBuilder();
		for (int x = 0; x < width; x++)
			sb.Append(buffer.GetCell(x, 0).Character.ToString());
		return sb.ToString();
	}

	#region The reported bug

	/// <summary>
	/// One row matches "Berlin", and the footer has to say so rather than "No matches".
	/// </summary>
	[Fact]
	public void ADelegatedFilterWithMatches_DoesNotSayNoMatches()
	{
		var table = FilteredTable("Berlin", out var source);

		Assert.Equal(1, source.RowCount);   // the source really did narrow
		Assert.DoesNotContain("No matches", Footer(table));
	}

	[Fact]
	public void ADelegatedFilterReportsTheMatchedCount()
	{
		var table = FilteredTable("Berlin", out _);

		Assert.Contains("1", Footer(table));
	}

	/// <summary>The denominator stays the pre-filter total, so the footer reads "1/4 rows".</summary>
	[Fact]
	public void ADelegatedFilterReportsTheUnfilteredTotal()
	{
		var table = FilteredTable("Berlin", out _);

		Assert.Contains("/4 rows", Footer(table));
	}

	/// <summary>A filter matching several rows counts them all, not just the first.</summary>
	[Fact]
	public void ADelegatedFilterCountsEveryMatch()
	{
		// "a" appears in every name or city here.
		var table = FilteredTable("a", out var source);

		string footer = Footer(table);

		Assert.DoesNotContain("No matches", footer);
		Assert.Contains($"{source.RowCount}", footer);
	}

	#endregion

	#region Still correct when nothing matches

	[Fact]
	public void ADelegatedFilterWithNoMatches_StillSaysNoMatches()
	{
		var table = FilteredTable("Reykjavik", out var source);

		Assert.Equal(0, source.RowCount);
		Assert.Contains("No matches", Footer(table));
	}

	#endregion

	/// <summary>
	/// Clearing and refiltering must not drift the total. ClearFilter zeroes the captured count, so
	/// the next filter recaptures it from the restored source rather than reusing a stale number —
	/// the reason the reset belongs there and not in the delegation path.
	/// </summary>
	[Fact]
	public void ClearingAndRefiltering_KeepsTheTotalCorrect()
	{
		var table = FilteredTable("Berlin", out var source);
		Assert.Contains("/4 rows", Footer(table));

		table.ClearFilter();
		Assert.Equal(4, source.RowCount);

		table.EnterFilterMode();
		table.ApplyFilter("Cairo");

		string footer = Footer(table);
		Assert.Contains("1", footer);
		Assert.Contains("/4 rows", footer);
	}

	/// <summary>
	/// Filtering twice without clearing in between still reports the original total, not the
	/// already-narrowed one.
	/// </summary>
	[Fact]
	public void RefilteringWithoutClearing_DoesNotShrinkTheTotal()
	{
		var table = FilteredTable("a", out _);

		table.ApplyFilter("Cairo");

		Assert.Contains("/4 rows", Footer(table));
	}

	#region The in-memory path is unaffected

	/// <summary>
	/// A table filtering its own rows builds a display map as before, and the footer must keep
	/// reading the same numbers from it.
	/// </summary>
	[Fact]
	public void AnInMemoryFilter_IsUnchanged()
	{
		var table = new TableControl();
		table.AddColumn("Name");
		table.AddColumn("City");
		table.AddRow("Alice", "Athens");
		table.AddRow("Bob", "Berlin");
		table.AddRow("Carol", "Cairo");

		table.EnterFilterMode();
		table.ApplyFilter("Berlin");

		string footer = Footer(table);

		Assert.DoesNotContain("No matches", footer);
		Assert.Contains("1", footer);
		Assert.Contains("/3 rows", footer);
	}

	[Fact]
	public void AnInMemoryFilterWithNoMatches_SaysNoMatches()
	{
		var table = new TableControl();
		table.AddColumn("Name");
		table.AddRow("Alice");

		table.EnterFilterMode();
		table.ApplyFilter("Reykjavik");

		Assert.Contains("No matches", Footer(table));
	}

	#endregion
}

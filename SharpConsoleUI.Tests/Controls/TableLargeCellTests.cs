// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using System.Diagnostics;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A cell holding far more than a column can show.
/// </summary>
/// <remarks>
/// A database column can hold a whole file. The table only ever draws a column's
/// width of it, so the work that matters is the work done per FRAME on the part
/// nobody sees: parsing a megabyte into cells and keeping a handful of them.
/// </remarks>
public class TableLargeCellTests
{
	/// <summary>One row, one column, with a value as long as the test wants.</summary>
	private sealed class OneBigCell : ITableDataSource
	{
		private readonly string _value;
		private readonly int _width;

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		internal OneBigCell(string value, int width)
		{
			_value = value;
			_width = width;
		}

		/// <summary>Counts reads, so a test can see how often the value is fetched.</summary>
		internal int Reads { get; private set; }

		public int RowCount => 1;
		public int ColumnCount => 1;
		public string GetColumnHeader(int c) => "Blob";

		public string GetCellValue(int r, int c)
		{
			Reads++;
			return _value;
		}

		// The width the column is given. In DataSource mode this is where a
		// fixed width comes from, so the test drives the real path.
		public int? GetColumnWidth(int c) => _width;

		public object? GetRowTag(int r) => null;
	}

	private static List<string> Paint(TableControl table, int w = 40, int h = 8)
	{
		var buffer = new CharacterBuffer(w, h);
		var bounds = new LayoutRect(0, 0, w, h);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		return Enumerable.Range(0, h)
			.Select(y => string.Concat(Enumerable.Range(0, w)
				.Select(x => buffer.GetCell(x, y).Character.ToString())).TrimEnd())
			.ToList();
	}

	private static TableControl WithValue(string value, int width = 20)
	{
		var table = new TableControl { BorderStyle = BorderStyle.None, Height = 6 };
		table.DataSource = new OneBigCell(value, width);
		return table;
	}

	[Fact]
	public void A_huge_cell_costs_about_what_a_small_one_costs()
	{
		// The whole point. Painting draws a column's width either way, so the
		// time should follow the COLUMN, not the value. Before the fix the full
		// value was parsed into one Cell per character and all but colW thrown
		// away, so this grew with the value and the UI stalled on every repaint.
		var small = WithValue(new string('x', 1_000));
		var huge = WithValue(new string('x', 1_000_000));

		Paint(small);
		Paint(huge);   // warm both paths

		var smallTime = Time(() => Paint(small));
		var hugeTime = Time(() => Paint(huge));

		// Generous: a thousandfold more data must not cost ten times more to
		// draw. A tight bound would make this a flaky timing test; this one
		// fails by orders of magnitude when the value is parsed in full.
		Assert.True(hugeTime < smallTime * 10 + TimeSpan.FromMilliseconds(50),
			$"huge cell took {hugeTime.TotalMilliseconds:F1}ms against "
			+ $"{smallTime.TotalMilliseconds:F1}ms for a small one");
	}

	private static TimeSpan Time(Action act)
	{
		var sw = Stopwatch.StartNew();
		for (int i = 0; i < 5; i++) act();
		sw.Stop();
		return sw.Elapsed;
	}

	[Fact]
	public void A_wider_column_shows_more_of_the_same_cell()
	{
		// Truncation has to follow the frame's width rather than being cached
		// against the value, or resizing a column would stop changing what it
		// shows.
		string value = new('a', 500);

		var narrow = WithValue(value, width: 10);
		var wide = WithValue(value, width: 30);

		int narrowRun = Paint(narrow).Max(l => Run(l, 'a'));
		int wideRun = Paint(wide).Max(l => Run(l, 'a'));

		Assert.True(wideRun > narrowRun,
			$"a 30-wide column showed {wideRun} characters, a 10-wide one {narrowRun}");
	}

	/// <summary>The longest unbroken run of one character in a line.</summary>
	private static int Run(string line, char ch)
	{
		int best = 0, run = 0;

		foreach (char c in line)
		{
			run = c == ch ? run + 1 : 0;
			if (run > best) best = run;
		}

		return best;
	}

	[Fact]
	public void Markup_cut_mid_span_does_not_leak_into_the_next_column()
	{
		// Truncating markup means cutting between an opening tag and its close.
		// The colour has to stop at the column edge whatever the cut did to the
		// tags, or the row after it wears the colour of the row before.
		var table = new TableControl { BorderStyle = BorderStyle.None, Height = 6 };
		table.AddColumn("A");
		table.AddColumn("B");
		table.AddRow("[red]" + new string('r', 400) + "[/]", "plain");
		table.SetColumnWidth(0, 8);
		table.SetColumnWidth(1, 8);

		var buffer = new CharacterBuffer(40, 8);
		var bounds = new LayoutRect(0, 0, 40, 8);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		// The row that holds the value: find the line carrying the r's.
		int row = -1;
		for (int y = 0; y < 8 && row < 0; y++)
		{
			for (int x = 0; x < 40; x++)
				if (buffer.GetCell(x, y).Character.Value == 'r') { row = y; break; }
		}

		Assert.True(row >= 0, "the painted value was not found");

		// Somewhere to the right of the first column the red must have stopped.
		var red = buffer.GetCell(0, row).Foreground;
		bool endsSomewhere = Enumerable.Range(0, 40)
			.Any(x => buffer.GetCell(x, row).Foreground != red);

		Assert.True(endsSomewhere, "the colour ran to the end of the line");
	}
}

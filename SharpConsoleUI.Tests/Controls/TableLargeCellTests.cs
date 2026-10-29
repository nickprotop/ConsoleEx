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
using SharpConsoleUI.Builders;
using SharpConsoleUI.Tests.Infrastructure;
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

	[Fact]
	public void A_filter_match_highlights_the_word_it_matched()
	{
		// The painter indexes the drawn cells by the offsets the filter reports.
		// Those agree only because the filter searches the value with its markup
		// already removed - converting them a second time, as if they counted
		// raw characters, moves the highlight left by the length of every tag
		// before the match.
		var table = new TableControl { BorderStyle = BorderStyle.None, Height = 6 };
		table.AddColumn("A");
		table.AddRow("[green]xx[/]MATCH");
		table.SetColumnWidth(0, 20);
		table.ApplyFilter("MATCH");

		var matches = table.FindMatchPositions(0, table._activeFilter!);

		// Offsets into the drawn text: "xxMATCH", so the match starts at 2.
		var one = Assert.Single(matches);

		Assert.Equal(0, one.Column);
		Assert.Equal(2, one.Start);
		Assert.Equal(5, one.Length);
	}

	[Fact]
	public void A_match_after_a_wide_character_still_points_at_the_word()
	{
		// A wide glyph is one character of the value and two drawn cells. The
		// offsets are character counts, so this is the case that would need
		// converting if anything ever did - it is pinned here so a later change
		// to either side has to notice.
		var table = new TableControl { BorderStyle = BorderStyle.None, Height = 6 };
		table.AddColumn("A");
		table.AddRow("\u4f60\u597dMATCH");
		table.SetColumnWidth(0, 20);
		table.ApplyFilter("MATCH");

		var one = Assert.Single(table.FindMatchPositions(0, table._activeFilter!));

		Assert.Equal(2, one.Start);
	}

	[Fact]
	public void A_painted_filter_match_sits_under_the_word_it_matched()
	{
		// The one that exercises the painter rather than the filter. The match
		// offsets already count drawn cells, because the filter searches the
		// value with its markup removed; converting them again puts the
		// highlight on the characters before the word.
		var table = new TableControl { BorderStyle = BorderStyle.None, Height = 6 };
		table.AddColumn("A");
		table.AddRow("[green]xx[/]MATCH");
		table.SetColumnWidth(0, 20);
		table.ApplyFilter("MATCH");

		// Left selected on purpose: that is the row a filter is most often read
		// on, and the one that used to show no highlight at all.

		var buffer = new CharacterBuffer(30, 6);
		var bounds = new LayoutRect(0, 0, 30, 6);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		int row = -1, at = -1;
		for (int y = 0; y < 6 && row < 0; y++)
		{
			for (int x = 0; x + 6 < 30; x++)
			{
				string seven = string.Concat(Enumerable.Range(0, 7)
					.Select(i => buffer.GetCell(x + i, y).Character.ToString()));

				if (seven == "xxMATCH") { row = y; at = x; break; }
			}
		}

		Assert.True(row >= 0, "the value was not painted");

		// The five characters of MATCH carry the highlight; the two x before
		// them do not.
		var lit = buffer.GetCell(at + 2, row).Background;

		// The highlight must actually differ from the plain background, or every
		// comparison below passes by saying nothing.
		Assert.True(lit != buffer.GetCell(at, row).Background,
			$"no highlight was drawn at all: cell0={buffer.GetCell(at, row).Background} lit={lit}");

		for (int i = 2; i < 7; i++)
		{
			Assert.True(buffer.GetCell(at + i, row).Background == lit,
				$"cell {i} of \"xxMATCH\" was not highlighted with the rest of the word");
		}

		for (int i = 0; i < 2; i++)
		{
			Assert.True(buffer.GetCell(at + i, row).Background != lit,
				$"cell {i} is before the match and must not be highlighted");
		}
	}

	[Fact]
	public void A_filter_highlights_while_it_is_being_typed()
	{
		// The rows are already narrowed on every keystroke - ApplyFilterLive
		// does that and sets the active filter - so the matches are known before
		// Enter. Waiting for Enter to colour them meant the table showed which
		// rows a filter kept without showing why, until after the question had
		// been answered.
		var table = new TableControl { BorderStyle = BorderStyle.None, Height = 6, FilteringEnabled = true, ReadOnly = false };
		table.AddColumn("A");
		table.AddRow("xxMATCH");
		table.SetColumnWidth(0, 20);

		// ProcessKey ignores a control without focus, and focus is computed from
		// the container, so the table needs a real window to sit in.
		var system = ChromeGeometry.CreateSystem(40, 12);
		var window = new WindowBuilder(system).Frameless().Maximized().Build();
		window.AddControl(table);
		system.WindowStateService.AddWindow(window);
		window.FocusManager.SetFocus(table, FocusReason.Programmatic);

		// The real path: open the filter bar and type, without pressing Enter.
		table.EnterFilterMode();
		foreach (char ch in "MATCH")
			table.ProcessKey(new ConsoleKeyInfo(ch, ConsoleKey.A, false, false, false));

		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);

		var buffer = new CharacterBuffer(30, 6);
		var bounds = new LayoutRect(0, 0, 30, 6);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		int row = -1, at = -1;
		for (int y = 0; y < 6 && row < 0; y++)
		{
			for (int x = 0; x + 6 < 30; x++)
			{
				string seven = string.Concat(Enumerable.Range(0, 7)
					.Select(i => buffer.GetCell(x + i, y).Character.ToString()));

				if (seven == "xxMATCH") { row = y; at = x; break; }
			}
		}

		Assert.True(row >= 0, "the value was not painted while typing");

		var lit = buffer.GetCell(at + 2, row).Background;

		Assert.True(lit != buffer.GetCell(at, row).Background,
			"nothing was highlighted while the filter was being typed");
	}
}

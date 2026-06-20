// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Render coverage for the two-pass measure in <see cref="GridLayout"/>: a <see cref="MarkupControl"/>
/// with <c>Wrap=true</c> placed in a narrow column must reflow to the column's width (multiple lines)
/// rather than measuring against the full grid width and being clipped to a single line. Pass 1 sizes
/// the tracks loosely; pass 2 re-measures each cell against its real cell extent so wrapping controls
/// reflow, and Auto rows grow to fit the wrapped height.
/// </summary>
public class GridWrapTests
{
	// A sentence with spaces, long enough to overflow a ~20-column half of a 40-wide grid so it can wrap.
	private const string LongSentence =
		"alpha bravo charlie delta echo foxtrot golf hotel india juliet";

	private const string FirstWord = "alpha";
	private const string LastWord = "juliet";

	private static List<string> RenderGridLines(GridControl grid)
	{
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		var content = window.RenderAndGetVisibleContent();
		return ContainerTestHelpers.StripAnsiCodes(content).Split('\n').ToList();
	}

	/// <summary>Index of the first line containing <paramref name="word"/>, or -1 when absent.</summary>
	private static int LineOf(IReadOnlyList<string> lines, string word)
	{
		for (int i = 0; i < lines.Count; i++)
		{
			if (lines[i].Contains(word))
			{
				return i;
			}
		}
		return -1;
	}

	[Fact]
	public void WrappingContent_ReflowsToColumnWidth()
	{
		// Two ~20-wide columns; one Star row. The sentence (~62 cols) placed in cell (0,0) must wrap to
		// the ~20-col column rather than being clipped to a single line.
		var grid = new GridControl { Width = 40, Height = 10 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { LongSentence }) { Wrap = true }, 0, 0);

		var lines = RenderGridLines(grid);

		int firstLine = LineOf(lines, FirstWord);
		int lastLine = LineOf(lines, LastWord);

		// If single-pass (broken), the loose measure reports a one-line sentence that is clipped to the
		// ~20-col column, so the last word never renders. Its presence proves the cell wrapped.
		Assert.NotEqual(-1, firstLine);
		Assert.NotEqual(-1, lastLine);

		// The full sentence must not appear on one line; first and last words land on different rows.
		Assert.DoesNotContain(lines, l => l.Contains(LongSentence));
		Assert.True(lastLine > firstLine, $"Expected wrapped content: '{LastWord}' (line {lastLine}) below '{FirstWord}' (line {firstLine}).");
	}

	[Fact]
	public void ColSpanCell_WrapsToCombinedWidth()
	{
		// Same sentence in a single ~20-col column vs. a colSpan=2 cell spanning ~40 cols. The wider span
		// wraps into fewer lines.
		var single = new GridControl { Width = 40, Height = 10 };
		single.ColumnDefinitions.Add(GridLength.Star(1));
		single.ColumnDefinitions.Add(GridLength.Star(1));
		single.RowDefinitions.Add(GridLength.Star(1));
		single.Place(new MarkupControl(new List<string> { LongSentence }) { Wrap = true }, 0, 0);

		var spanned = new GridControl { Width = 40, Height = 10 };
		spanned.ColumnDefinitions.Add(GridLength.Star(1));
		spanned.ColumnDefinitions.Add(GridLength.Star(1));
		spanned.RowDefinitions.Add(GridLength.Star(1));
		spanned.Place(new MarkupControl(new List<string> { LongSentence }) { Wrap = true }, 0, 0, rowSpan: 1, colSpan: 2);

		int LinesUsed(GridControl g)
		{
			var lines = RenderGridLines(g);
			// Count distinct rendered rows that contain any word of the sentence.
			var words = LongSentence.Split(' ');
			return lines.Count(l => words.Any(w => l.Contains(w)));
		}

		int singleLines = LinesUsed(single);
		int spannedLines = LinesUsed(spanned);

		Assert.True(singleLines > 1, "Single ~20-col column should wrap to multiple lines.");
		Assert.True(spannedLines < singleLines, $"colSpan=2 (~40 cols) should wrap into fewer lines ({spannedLines}) than 1 column ({singleLines}).");
	}

	[Fact]
	public void AutoRow_GrowsToWrappedHeight()
	{
		// The wrapping sentence sits in the NARROW left column (~20 cols) of a 40-wide grid, in an Auto
		// row. With the two-pass fix the cell reflows to its ~20-col column (more lines than the full-grid
		// loose measure), so the Auto row grows taller and pushes BELOWCELL further down than the same
		// sentence measured loosely against the full grid width would. Comparing against a short top row
		// isolates the row's growth.
		GridControl Build(string topText)
		{
			var g = new GridControl { Width = 40, Height = 14 };
			g.ColumnDefinitions.Add(GridLength.Star(1));
			g.ColumnDefinitions.Add(GridLength.Star(1));
			g.RowDefinitions.Add(GridLength.Auto());
			g.RowDefinitions.Add(GridLength.Auto());
			g.Place(new MarkupControl(new List<string> { topText }) { Wrap = true }, 0, 0);
			g.Place(new MarkupControl(new List<string> { "BELOWCELL" }) { Wrap = false }, 1, 0);
			return g;
		}

		// Top row holds the wrapping sentence in a narrow column vs. a single short line.
		var wrappedLines = RenderGridLines(Build(LongSentence));
		var shortLines = RenderGridLines(Build("short"));

		int wrappedBelow = LineOf(wrappedLines, "BELOWCELL");
		int shortBelow = LineOf(shortLines, "BELOWCELL");

		Assert.NotEqual(-1, wrappedBelow);
		Assert.NotEqual(-1, shortBelow);

		// The Auto row wrapped to the ~20-col column grew, pushing BELOWCELL lower than the single-line row.
		Assert.True(wrappedBelow > shortBelow + 1, $"Auto row should grow to wrapped height: BELOWCELL at line {wrappedBelow} (wrapped) vs {shortBelow} (short).");

		// The wrapped sentence occupies multiple rows in its narrow column (first and last words differ).
		Assert.True(LineOf(wrappedLines, LastWord) > LineOf(wrappedLines, FirstWord), "Sentence should occupy multiple rows in the narrow Auto-row column.");
	}

	[Fact]
	public void NonWrappingShortContent_Unaffected()
	{
		// Sanity: a short label in a wide cell renders identically — the two-pass fix is a no-op when no
		// reflow is needed.
		var grid = new GridControl { Width = 40, Height = 6 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "HELLO" }), 0, 0);
		grid.Place(new MarkupControl(new List<string> { "WORLD" }), 0, 1);

		var lines = RenderGridLines(grid);

		Assert.Contains(lines, l => l.Contains("HELLO"));
		Assert.Contains(lines, l => l.Contains("WORLD"));
		// Both short labels remain on the same row (no spurious wrapping).
		Assert.Equal(LineOf(lines, "HELLO"), LineOf(lines, "WORLD"));
	}
}

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
/// Coverage for the <see cref="GridCell"/> value-type accessor and per-cell styling (background,
/// border, padding). Render-based tests prove the chrome actually paints and that border/padding/margin
/// insets compose correctly; the structural tests prove the accessor reads/writes the same cell store as
/// the rest of the grid. Mirrors the <see cref="GridCrudTests"/>/<see cref="GridRenderTests"/> setup.
/// </summary>
/// <remarks>
/// <see cref="GridCell"/> is a value-type handle whose setters mutate the grid it points at. C# forbids
/// writing a property directly on the temporary returned by an indexer/method (CS1612), so the tests
/// take the handle into a local first (<c>var cell = grid[r, c]; cell.Background = ...;</c>) — the write
/// still reaches the grid because the handle holds the grid reference.
/// </remarks>
public class GridCellTests
{
	private static GridControl NewGrid(int cols = 1, int rows = 1)
	{
		var grid = new GridControl { Width = 40, Height = 10 };
		for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(GridLength.Star(1));
		for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(GridLength.Star(1));
		return grid;
	}

	private static MarkupControl Label(string text) => new(new List<string> { text });

	private static List<string> RenderRaw(GridControl grid)
	{
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		return window.RenderAndGetVisibleContent();
	}

	private static string RenderStripped(GridControl grid) =>
		ContainerTestHelpers.StripAnsiCodes(RenderRaw(grid));

	// Finds the (line, col) of the first char of needle on the line containing it, in stripped output.
	// Returns (-1, -1) when not found.
	private static (int line, int col) Locate(string stripped, string needle)
	{
		var lines = stripped.Split('\n');
		for (int i = 0; i < lines.Length; i++)
		{
			int c = lines[i].IndexOf(needle, System.StringComparison.Ordinal);
			if (c >= 0) return (i, c);
		}
		return (-1, -1);
	}

	#region Accessor structure

	[Fact]
	public void Indexer_AndCell_AddressSameCell()
	{
		var grid = NewGrid();
		var label = Label("HELLO");
		var cell = grid[0, 0];
		cell.Content = label;

		Assert.Same(label, grid.Cell(0, 0).Content);
		Assert.Same(label, grid[0, 0].Content);
	}

	[Fact]
	public void Content_Get_ReturnsPlacedControl()
	{
		var grid = NewGrid();
		var label = Label("X");
		grid.Place(label, 0, 0);

		Assert.Same(label, grid[0, 0].Content);
	}

	[Fact]
	public void Content_Set_ReplacesControl()
	{
		var grid = NewGrid();
		grid.Place(Label("OLD"), 0, 0);
		var fresh = Label("NEW");
		var cell = grid[0, 0];
		cell.Content = fresh;

		Assert.Same(fresh, grid[0, 0].Content);
		Assert.Single(grid.GetChildren());
	}

	[Fact]
	public void Content_SetOnEmpty_Places()
	{
		var grid = NewGrid();
		Assert.True(grid[0, 0].IsEmpty);

		var label = Label("FILL");
		var cell = grid[0, 0];
		cell.Content = label;

		Assert.Same(label, grid[0, 0].Content);
		Assert.Contains("FILL", RenderStripped(grid));
	}

	[Fact]
	public void Content_SetNull_Clears()
	{
		var grid = NewGrid();
		grid.Place(Label("BYE"), 0, 0);

		var cell = grid[0, 0];
		cell.Content = null;

		Assert.Null(grid[0, 0].Content);
		Assert.DoesNotContain("BYE", RenderStripped(grid));
	}

	[Fact]
	public void IsEmpty_ReflectsContent()
	{
		var grid = NewGrid();
		Assert.True(grid[0, 0].IsEmpty);

		var cell = grid[0, 0];
		cell.Content = Label("X");
		Assert.False(grid[0, 0].IsEmpty);
	}

	[Fact]
	public void Clear_RemovesControl()
	{
		var grid = NewGrid();
		grid.Place(Label("Z"), 0, 0);

		grid[0, 0].Clear();

		Assert.True(grid[0, 0].IsEmpty);
		Assert.Empty(grid.GetChildren());
	}

	#endregion

	#region Styling render

	[Fact]
	public void CellBackground_RendersFillColor()
	{
		var grid = NewGrid();
		// A distinctive RGB so its truecolor sequence is unambiguous in the raw ANSI.
		var bg = new Color(11, 222, 33);
		var cell = grid[0, 0];
		cell.Content = Label("A");
		cell.Background = bg;

		var raw = string.Join("\n", RenderRaw(grid));
		Assert.Contains($"48;2;{bg.R};{bg.G};{bg.B}", raw);
	}

	[Fact]
	public void CellBorder_RendersBorderChars()
	{
		var grid = NewGrid();
		var cell = grid[0, 0];
		cell.Content = Label("A");
		cell.Border = BorderStyle.Single;

		var stripped = RenderStripped(grid);
		bool hasBorderGlyph = stripped.Any(ch => "┌┐└┘─│".Contains(ch));
		Assert.True(hasBorderGlyph, "expected single-line border glyphs in the rendered output");
	}

	[Fact]
	public void CellPadding_InsetsContent()
	{
		// Baseline: content sits at the cell's top-left.
		var baseline = NewGrid();
		baseline.Place(Label("PAD"), 0, 0);
		var baseLoc = Locate(RenderStripped(baseline), "PAD");

		var padded = NewGrid();
		padded.Place(Label("PAD"), 0, 0);
		var pcell = padded[0, 0];
		pcell.Padding = new Padding(2);
		var padLoc = Locate(RenderStripped(padded), "PAD");

		Assert.True(baseLoc.line >= 0 && padLoc.line >= 0, "content should render in both cases");
		// Padding(2) shifts the content down and right by 2 vs the no-padding baseline.
		Assert.True(padLoc.col > baseLoc.col, $"padded col {padLoc.col} should exceed baseline {baseLoc.col}");
		Assert.True(padLoc.line > baseLoc.line, $"padded line {padLoc.line} should exceed baseline {baseLoc.line}");
	}

	[Fact]
	public void CellBorder_InsetsContentByOne()
	{
		// Two side-by-side cells: the right one is bordered. A one-cell border insets the content of its
		// cell on every side, so a border glyph must sit immediately to the LEFT of and immediately ABOVE
		// the bordered cell's content. (The left, unbordered cell provides the contrast — its content has
		// no border glyph beside it.)
		var grid = NewGrid(cols: 2, rows: 1);
		grid.Place(Label("LL"), 0, 0);
		grid.Place(Label("RR"), 0, 1);
		var rcell = grid[0, 1];
		rcell.Border = BorderStyle.Single;

		var lines = RenderStripped(grid).Split('\n');
		var right = Locate(string.Join("\n", lines), "RR");
		Assert.True(right.line >= 1 && right.col >= 1, "bordered content should render inset from the cell edges");

		const string borderGlyphs = "┌┐└┘─│";
		char leftOf = lines[right.line][right.col - 1];
		char above = lines[right.line - 1][right.col];
		Assert.Contains(leftOf, borderGlyphs);
		Assert.Contains(above, borderGlyphs);

		// Contrast: the unbordered cell's content has no border glyph immediately to its left.
		var left = Locate(string.Join("\n", lines), "LL");
		if (left.col >= 1)
			Assert.DoesNotContain(lines[left.line][left.col - 1], borderGlyphs);
	}

	[Fact]
	public void ChildMargin_ComposesWithCellPadding()
	{
		// Measure each inset's effect independently, then assert the combined inset is their sum — so the
		// test is robust to whatever per-control baseline a MarkupControl margin contributes.
		var baseline = NewGrid();
		baseline.Place(Label("MG"), 0, 0);
		var baseLoc = Locate(RenderStripped(baseline), "MG");

		var padOnly = NewGrid();
		padOnly.Place(Label("MG"), 0, 0);
		var pcell = padOnly[0, 0];
		pcell.Padding = new Padding(2);
		var padLoc = Locate(RenderStripped(padOnly), "MG");

		var marginOnly = NewGrid();
		var mchild = Label("MG");
		mchild.Margin = new Margin(1, 1, 0, 0);
		marginOnly.Place(mchild, 0, 0);
		var marginLoc = Locate(RenderStripped(marginOnly), "MG");

		var composed = NewGrid();
		var cchild = Label("MG");
		cchild.Margin = new Margin(1, 1, 0, 0);
		composed.Place(cchild, 0, 0);
		var ccell = composed[0, 0];
		ccell.Padding = new Padding(2);
		var loc = Locate(RenderStripped(composed), "MG");

		Assert.True(baseLoc.line >= 0 && loc.line >= 0, "content should render");

		int padDeltaCol = padLoc.col - baseLoc.col;
		int padDeltaLine = padLoc.line - baseLoc.line;
		int marginDeltaCol = marginLoc.col - baseLoc.col;
		int marginDeltaLine = marginLoc.line - baseLoc.line;

		// Both insets must each have an effect, and the composed shift is their sum (they compose, not override).
		Assert.True(padDeltaCol > 0 && marginDeltaCol > 0, "padding and margin should each inset horizontally");
		Assert.Equal(baseLoc.col + padDeltaCol + marginDeltaCol, loc.col);
		Assert.Equal(baseLoc.line + padDeltaLine + marginDeltaLine, loc.line);
	}

	[Fact]
	public void StyledEmptyCell_RendersChrome_NoCrash()
	{
		var grid = NewGrid(cols: 2, rows: 1);
		var button = ContainerTestHelpers.CreateButton("GO");
		grid.Place(button, 0, 1);

		// Style an EMPTY cell (no content placed in 0,0).
		var empty = grid[0, 0];
		empty.Background = new Color(5, 6, 7);
		empty.Border = BorderStyle.Single;

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);

		// Should render without throwing, and the styled empty cell must NOT have become a tree/focus child.
		var stripped = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("GO", stripped);

		// Only the button is a real child; the styled empty cell is content-less.
		Assert.Single(grid.GetChildren());

		// The focusable control elsewhere still focuses.
		window.FocusManager.SetFocus(button, FocusReason.Programmatic);
		Assert.True(button.HasFocus);
	}

	[Fact]
	public void CellStyle_SurvivesReRender()
	{
		var grid = NewGrid();
		var bg = new Color(99, 88, 77);
		var cell = grid[0, 0];
		cell.Content = Label("S");
		cell.Background = bg;
		cell.Border = BorderStyle.Single;

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);

		_ = window.RenderAndGetVisibleContent(); // first render
		var raw = string.Join("\n", window.RenderAndGetVisibleContent()); // re-render
		var stripped = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());

		Assert.Contains($"48;2;{bg.R};{bg.G};{bg.B}", raw);
		Assert.True(stripped.Any(ch => "┌┐└┘─│".Contains(ch)), "border should survive re-render");
	}

	[Fact]
	public void ResetStyle_ClearsBackgroundBorderPadding_KeepsContent()
	{
		var grid = NewGrid();
		var bg = new Color(123, 45, 67);
		var content = Label("KEEP");
		var cell = grid[0, 0];
		cell.Content = content;
		cell.Background = bg;
		cell.Border = BorderStyle.Single;
		cell.Padding = new Padding(1);

		// Styling is present in the first render.
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		var styledRaw = string.Join("\n", window.RenderAndGetVisibleContent());
		var styledStripped = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains($"48;2;{bg.R};{bg.G};{bg.B}", styledRaw);
		Assert.True(styledStripped.Any(ch => "┌┐└┘─│".Contains(ch)), "border should render before reset");

		// Reset styling — content stays.
		var resetHandle = grid[0, 0];
		resetHandle.ResetStyle();

		Assert.Same(content, grid[0, 0].Content);
		Assert.Null(grid[0, 0].Background);
		Assert.Equal(BorderStyle.None, grid[0, 0].Border);
		Assert.Equal(new Padding(0), grid[0, 0].Padding);

		var clearedRaw = string.Join("\n", window.RenderAndGetVisibleContent());
		var clearedStripped = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.DoesNotContain($"48;2;{bg.R};{bg.G};{bg.B}", clearedRaw);
		Assert.False(clearedStripped.Any(ch => "┌┐└┘─│".Contains(ch)), "cell border chars should be gone after reset");
		Assert.Contains("KEEP", clearedStripped);
	}

	[Fact]
	public void ResetStyle_OnContentlessStyledCell_RemovesIt()
	{
		var grid = NewGrid();
		var empty = grid[0, 0];
		empty.Background = new Color(10, 20, 30); // creates a content-less styled entry

		Assert.NotNull(grid[0, 0].Placement);

		var handle = grid[0, 0];
		handle.ResetStyle();

		Assert.True(grid[0, 0].IsEmpty);
		Assert.Null(grid[0, 0].Placement);
	}

	#endregion
}

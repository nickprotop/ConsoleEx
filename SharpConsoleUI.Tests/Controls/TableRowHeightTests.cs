// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// How tall a row needs to be, and how often that question gets answered.
/// </summary>
/// <remarks>
/// A wrapping column turns row height into a measurement rather than a constant,
/// and a measurement on the paint path has to be cached or it is paid per frame
/// per row. These tests pin both halves: the height is the tallest wrapped cell,
/// and asking twice measures once.
/// </remarks>
public class TableRowHeightTests
{
	private const string FourLinesAtTwenty =
		"The quick brown fox jumps over the lazy dog and keeps running onward";

	/// <summary>Two columns, the second optionally wrapping.</summary>
	private static (TableRowHeightCache cache, List<TableColumn> cols) Build(
		bool wrap, int maxLines = 0)
	{
		var cols = new List<TableColumn>
		{
			new TableColumn("ID"),
			new TableColumn("Title") { Wrap = wrap, MaxLines = maxLines },
		};
		return (new TableRowHeightCache(), cols);
	}

	private static Func<int, IList<string>> Rows(params string[][] rows) =>
		r => rows[r];

	[Fact]
	public void RowHeight_IsOneWhenNoColumnWraps()
	{
		var (cache, cols) = Build(wrap: false);

		int height = cache.GetHeight(0, new[] { 4, 20 },
			Rows(new[] { "7", FourLinesAtTwenty }), cols);

		Assert.Equal(1, height);
	}

	[Fact]
	public void RowHeight_IsTheTallestWrappedCell()
	{
		var (cache, cols) = Build(wrap: true);

		int height = cache.GetHeight(0, new[] { 4, 20 },
			Rows(new[] { "7", FourLinesAtTwenty }), cols);

		Assert.Equal(4, height);
	}

	[Fact]
	public void RowHeight_IsCappedByMaxLines()
	{
		var (cache, cols) = Build(wrap: true, maxLines: 2);

		int height = cache.GetHeight(0, new[] { 4, 20 },
			Rows(new[] { "7", FourLinesAtTwenty }), cols);

		Assert.Equal(2, height);
	}

	[Fact]
	public void RowHeight_OfAWhitespaceOnlyCell_IsOne()
	{
		var (cache, cols) = Build(wrap: true);

		int height = cache.GetHeight(0, new[] { 4, 20 }, Rows(new[] { "7", "   " }), cols);

		Assert.Equal(1, height);
	}

	[Fact]
	public void RowHeight_OfAnEmptyCell_IsOne()
	{
		var (cache, cols) = Build(wrap: true);

		int height = cache.GetHeight(0, new[] { 4, 20 }, Rows(new[] { "7", "" }), cols);

		Assert.Equal(1, height);
	}

	[Fact]
	public void RowHeight_WithMaxLinesOne_MatchesNoWrapHeight()
	{
		var (wrapped, wrapCols) = Build(wrap: true, maxLines: 1);
		var (plain, plainCols) = Build(wrap: false);

		var cells = Rows(new[] { "7", FourLinesAtTwenty });

		Assert.Equal(
			plain.GetHeight(0, new[] { 4, 20 }, cells, plainCols),
			wrapped.GetHeight(0, new[] { 4, 20 }, cells, wrapCols));
	}

	[Fact]
	public void RowHeight_IsCachedAcrossRepeatedQueries()
	{
		var (cache, cols) = Build(wrap: true);
		var cells = Rows(new[] { "7", FourLinesAtTwenty });

		cache.GetHeight(0, new[] { 4, 20 }, cells, cols);
		cache.GetHeight(0, new[] { 4, 20 }, cells, cols);

		Assert.Equal(1, cache.MeasureCount);
	}

	[Fact]
	public void RowHeight_RemeasuresWhenColumnWidthChanges()
	{
		var (cache, cols) = Build(wrap: true);
		var cells = Rows(new[] { "7", FourLinesAtTwenty });

		int narrow = cache.GetHeight(0, new[] { 4, 20 }, cells, cols);
		int wide = cache.GetHeight(0, new[] { 4, 40 }, cells, cols);

		Assert.Equal(2, cache.MeasureCount);
		Assert.True(wide < narrow, $"a wider column should need fewer lines, got {wide} vs {narrow}");
	}

	[Fact]
	public void SeparateRows_AreCachedSeparately()
	{
		var (cache, cols) = Build(wrap: true);
		var cells = Rows(
			new[] { "1", FourLinesAtTwenty },
			new[] { "2", "short" });

		cache.GetHeight(0, new[] { 4, 20 }, cells, cols);
		cache.GetHeight(1, new[] { 4, 20 }, cells, cols);
		cache.GetHeight(0, new[] { 4, 20 }, cells, cols);
		cache.GetHeight(1, new[] { 4, 20 }, cells, cols);

		Assert.Equal(2, cache.MeasureCount);
		Assert.Equal(4, cache.GetHeight(0, new[] { 4, 20 }, cells, cols));
		Assert.Equal(1, cache.GetHeight(1, new[] { 4, 20 }, cells, cols));
	}

	[Fact]
	public void InvalidateAll_DropsEverything()
	{
		var (cache, cols) = Build(wrap: true);
		var cells = Rows(new[] { "1", FourLinesAtTwenty });

		cache.GetHeight(0, new[] { 4, 20 }, cells, cols);
		cache.InvalidateAll();
		cache.GetHeight(0, new[] { 4, 20 }, cells, cols);

		Assert.Equal(2, cache.MeasureCount);
	}

	[Fact]
	public void RowHeight_NeverExceedsTableMaxWrapLines()
	{
		var (cache, cols) = Build(wrap: true);
		// One cell wide, so the value wraps to one line per character: far past the ceiling.
		string long_ = new string('x', ControlDefaults.TableMaxWrapLines * 4);

		int height = cache.GetHeight(0, new[] { 4, 1 }, Rows(new[] { "7", long_ }), cols);

		Assert.True(height <= ControlDefaults.TableMaxWrapLines,
			$"height {height} is past the {ControlDefaults.TableMaxWrapLines}-line ceiling");
	}

	[Fact]
	public void RowHeight_IgnoresAColumnWithNoWidthEntry()
	{
		// More columns than widths happens mid-resize; it must not throw.
		var (cache, cols) = Build(wrap: true);

		int height = cache.GetHeight(0, new[] { 4 }, Rows(new[] { "7", FourLinesAtTwenty }), cols);

		Assert.Equal(1, height);
	}

	[Fact]
	public void RowHeight_WithNullColumns_IsOne()
	{
		// DataSource mode has no TableColumn list to consult.
		var cache = new TableRowHeightCache();

		int height = cache.GetHeight(0, new[] { 4, 20 },
			Rows(new[] { "7", FourLinesAtTwenty }), null);

		Assert.Equal(1, height);
	}

	// --- the control's own view of it, not just the cache's ---

	[Fact]
	public void Control_HasWrappingColumns_FollowsTheColumns()
	{
		var table = new TableControl();
		table.AddColumn(new TableColumn("ID"));
		var title = new TableColumn("Title");
		table.AddColumn(title);

		Assert.False(table.HasWrappingColumns);

		title.Wrap = true;
		Assert.True(table.HasWrappingColumns);
	}

	[Fact]
	public void Control_GetRowHeight_IsOneWithoutWrapping()
	{
		var table = new TableControl();
		table.AddColumn(new TableColumn("Title"));
		table.AddRow(FourLinesAtTwenty);

		Assert.Equal(1, table.GetRowHeight(0));
	}

	[Fact]
	public void Control_GetRowHeight_IsOneBeforeTheFirstPaint()
	{
		// RenderedWidth is 0 until a paint sets it, and a 0-wide column wraps to nothing.
		// A table nobody has drawn has no layout to measure against.
		var table = new TableControl();
		table.AddColumn(new TableColumn("Title") { Wrap = true });
		table.AddRow(FourLinesAtTwenty);

		Assert.Equal(1, table.GetRowHeight(0));
	}

	[Fact]
	public void Control_GetRowHeight_SurvivesAFilterRemappingTheRows()
	{
		// The height cache is keyed by DATA index for exactly this: a filter changes which row
		// each display position holds without any cell changing, and a cache keyed by display
		// index answered with the height of whatever used to sit there. A filtered table then
		// measured its rows short and clipped the wrapped lines.
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ShowHeader = false,
			Height = 12,
			FilteringEnabled = true,
		};
		table.AddColumn(new TableColumn("ID", TextJustification.Left, 4));
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = true });
		table.AddRow("1", "keep short");
		table.AddRow("2", "drop me");
		table.AddRow("3", "keep " + FourLinesAtTwenty);

		Paint(table);
		Assert.Equal(1, table.GetRowHeight(1));   // "drop me"

		table.ApplyFilter("keep");
		Paint(table);

		// Display row 1 is now the long row that was display row 2.
		Assert.True(table.GetRowHeight(1) > 1,
			$"after filtering, display row 1 is the long row but reports {table.GetRowHeight(1)}");
	}

	private static void Paint(TableControl table, int w = 40, int h = 12)
	{
		var buffer = new CharacterBuffer(w, h);
		var bounds = new LayoutRect(0, 0, w, h);
		table.PaintDOM(buffer, bounds, bounds, SharpConsoleUI.Color.White, SharpConsoleUI.Color.Black);
	}

	[Fact]
	public void Control_GetRowHeight_OutOfRangeRow_IsOne()
	{
		var table = new TableControl();
		table.AddColumn(new TableColumn("Title") { Wrap = true });
		table.AddRow("only row");

		Assert.Equal(1, table.GetRowHeight(99));
		Assert.Equal(1, table.GetRowHeight(-1));
	}
}

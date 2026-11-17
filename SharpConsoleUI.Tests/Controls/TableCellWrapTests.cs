// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Wrapping one cell's markup to a column width.
/// </summary>
/// <remarks>
/// The wrapping itself is MarkupParser.ParseLines, which already handles words,
/// display width and carrying a style across a break. What is tested here is the
/// part this library adds on top: the line cap, the ellipsis that says content
/// was dropped, and that measuring agrees with drawing.
/// </remarks>
public class TableCellWrapTests
{
	private const string LongTitle =
		"The quick brown fox jumps over the lazy dog and keeps running onward";

	[Fact]
	public void WrapCell_LongTitleInTwentyWide_WrapsToFourLines()
	{
		var lines = TableCellWrapper.WrapCell(LongTitle, 20, 0, Color.White, Color.Black);

		Assert.Equal(4, lines.Count);
		Assert.All(lines, line => Assert.True(line.Count <= 20,
			$"a wrapped line was {line.Count} cells wide, over the 20 the column allows"));
	}

	[Fact]
	public void WrapCell_MaxLinesTwo_ClipsAndEndsInEllipsis()
	{
		var lines = TableCellWrapper.WrapCell(LongTitle, 20, 2, Color.White, Color.Black);

		Assert.Equal(2, lines.Count);
		Assert.Equal(ControlDefaults.TableWrapEllipsis, lines[1][^1].Character.Value);
	}

	[Fact]
	public void WrapCell_MaxLinesThatFits_AddsNoEllipsis()
	{
		// Four lines of content with a cap of four: nothing was dropped, so nothing
		// should claim it was. A cap is a ceiling, not an instruction to truncate.
		var lines = TableCellWrapper.WrapCell(LongTitle, 20, 4, Color.White, Color.Black);

		Assert.Equal(4, lines.Count);
		Assert.NotEqual(ControlDefaults.TableWrapEllipsis, lines[3][^1].Character.Value);
	}

	[Fact]
	public void WrapCell_MarkupAcrossBoundary_KeepsColourOnBothLines()
	{
		var lines = TableCellWrapper.WrapCell(
			"[red]aaaa bbbb cccc dddd[/]", 10, 0, Color.White, Color.Black);

		Assert.True(lines.Count >= 2, "the value should not have fitted on one 10-wide line");
		Assert.Equal(Color.Red, lines[0][0].Foreground);
		Assert.Equal(Color.Red, lines[1][0].Foreground);
	}

	[Fact]
	public void WrapCell_EmptyValue_ReturnsOneEmptyLine()
	{
		var lines = TableCellWrapper.WrapCell(string.Empty, 20, 0, Color.White, Color.Black);

		Assert.Single(lines);
	}

	[Fact]
	public void WrapCell_WhitespaceOnlyValue_ReturnsOneLine()
	{
		var lines = TableCellWrapper.WrapCell("   ", 20, 0, Color.White, Color.Black);

		Assert.Single(lines);
	}

	[Fact]
	public void WrapCell_WideGlyphWiderThanColumn_DoesNotHang()
	{
		// A 2-wide glyph in a 1-wide column cannot be placed. The requirement is only
		// that this terminates and yields something: a hang here would freeze the
		// render loop, which is far worse than a dropped glyph.
		var lines = TableCellWrapper.WrapCell("中文", 1, 0, Color.White, Color.Black);

		Assert.True(lines.Count >= 1);
	}

	[Fact]
	public void WrapCell_TruncatingOnAWideGlyph_DoesNotLeaveHalfOfIt()
	{
		// The ellipsis replaces the line's last cell. When that cell is the second half of a
		// wide glyph, replacing only it leaves a 2-wide character followed by a 1-wide ellipsis
		// in a column with room for one of them: the half-drawn glyph the rendering rules forbid.
		var lines = TableCellWrapper.WrapCell(
			"\u4e2d\u6587\u6d4b\u8bd5\u4e2d\u6587\u6d4b\u8bd5\u4e2d\u6587\u6d4b\u8bd5",
			10, 2, Color.White, Color.Black);

		Assert.Equal(2, lines.Count);

		var last = lines[1];
		Assert.Equal(ControlDefaults.TableWrapEllipsis, last[^1].Character.Value);

		// Every cell must account for exactly one column: no lead glyph without its continuation.
		for (int i = 0; i < last.Count; i++)
		{
			if (last[i].IsWideContinuation) continue;
			bool isWide = SharpConsoleUI.Helpers.UnicodeWidth.IsWideRune(last[i].Character);
			bool hasContinuation = i + 1 < last.Count && last[i + 1].IsWideContinuation;
			Assert.False(isWide && !hasContinuation,
				$"cell {i} ('{last[i].Character}') is wide but has no continuation cell after it");
		}
	}

	[Fact]
	public void WrapCell_ZeroWidth_ReturnsOneEmptyLine()
	{
		var lines = TableCellWrapper.WrapCell(LongTitle, 0, 0, Color.White, Color.Black);

		Assert.Single(lines);
	}

	[Theory]
	[InlineData(LongTitle, 20, 0)]
	[InlineData(LongTitle, 20, 2)]
	[InlineData(LongTitle, 40, 0)]
	[InlineData("", 20, 0)]
	[InlineData("[red]aaaa bbbb cccc dddd[/]", 10, 0)]
	public void MeasureLines_AgreesWithWrapCell(string markup, int width, int maxLines)
	{
		int measured = TableCellWrapper.MeasureLines(markup, width, maxLines);
		int drawn = TableCellWrapper.WrapCell(markup, width, maxLines, Color.White, Color.Black).Count;

		Assert.Equal(drawn, measured);
	}

	[Fact]
	public void NoWrap_IsTheInverseOfWrap()
	{
		var col = new TableColumn("Title");

		// The default: nothing wraps, and NoWrap now says so. It reported false before
		// this release while still never wrapping.
		Assert.False(col.Wrap);
		Assert.True(col.NoWrap);

		col.Wrap = true;
		Assert.False(col.NoWrap);

		col.NoWrap = true;
		Assert.False(col.Wrap);
	}

	[Fact]
	public void MaxLines_DefaultsToNoLimit()
	{
		Assert.Equal(0, new TableColumn("Title").MaxLines);
	}

	[Fact]
	public void SettingWrap_RequestsRelayout()
	{
		var (window, col) = BuildWindowWithColumn();

		Assert.Equal(FrameWork.None, window.PendingWork);
		col.Wrap = true;
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	[Fact]
	public void SettingMaxLines_RequestsRelayout()
	{
		var (window, col) = BuildWindowWithColumn();

		col.MaxLines = 2;
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	private static (Window window, TableColumn col) BuildWindowWithColumn()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 24);
		var window = new Window(system) { Title = "T", Left = 0, Top = 0, Width = 80, Height = 20 };
		var table = new TableControl();
		var col = new TableColumn("Title");
		table.AddColumn(col);
		table.AddRow(LongTitle);
		window.AddControl(table);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, 80, 20) });
		return (window, col);
	}
}

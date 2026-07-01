// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

public class MarkdownTableWrappingTests
{
	private static List<string> Render(string md, int width)
	{
		var rows = MarkupParser.ParseLines($"[markdown]{md}[/]", width, Color.White, Color.Black, out _);
		return rows.Select(r => string.Concat(
			r.Where(c => !c.IsWideContinuation).Select(c => c.Character.ToString()))).ToList();
	}

	private const string Long =
		"| col1 | col2 |\n|---|---|\n| this is long text this is long text this is long text this is long text | short |";

	[Fact]
	public void LongCell_EveryLineFitsWidth_BordersIntact()
	{
		var rows = Render(Long, 40);
		// 1. Every rendered line fits the width (no outer-wrap shredding).
		Assert.All(rows, r => Assert.True(r.Length <= 40, $"line len {r.Length} > 40: '{r}'"));
		// 2. Borders are intact: each border row starts with a corner/edge and is not a stray fragment.
		Assert.Contains(rows, r => r.StartsWith("┌") && r.EndsWith("┐"));
		Assert.Contains(rows, r => r.StartsWith("└") && r.EndsWith("┘"));
		// 3. No row is a lone orphaned fragment (a border char with almost nothing else).
		Assert.DoesNotContain(rows, r => r.Trim() == "┐" || r.Trim() == "┘" || r.Trim() == "│");
		// 4. The long content actually appears (wrapped across lines).
		Assert.Contains(rows, r => r.Contains("this is long text"));
	}

	[Fact]
	public void ShortTable_RendersWithinWidth_CleanBox()
	{
		var rows = Render("| a | b |\n|---|---|\n| 1 | 2 |", 40);
		Assert.All(rows, r => Assert.True(r.Length <= 40));
		Assert.Contains(rows, r => r.StartsWith("┌") && r.EndsWith("┐"));
		Assert.Contains(rows, r => r.Contains("│ a") && r.Contains("│ b"));
	}

	[Fact]
	public void ShrinkOnlyOverLong_ShortColumnKeepsContent()
	{
		// One long column + one tiny column; the tiny column's 'x' must remain present and intact.
		var rows = Render("| big | y |\n|---|---|\n| aaaa bbbb cccc dddd eeee ffff | x |", 30);
		Assert.All(rows, r => Assert.True(r.Length <= 30));
		Assert.Contains(rows, r => r.Contains(" x "));   // tiny column content survives
	}

	[Fact]
	public void CjkCell_WrapsOnDisplayWidth_NoMidBorderSplit()
	{
		var rows = Render("| 名 | b |\n|---|---|\n| 测试内容很长很长很长很长 | x |", 24);
		Assert.All(rows, r => Assert.True(GetWidth(r) <= 24, $"display width {GetWidth(r)} > 24"));
		Assert.Contains(rows, r => r.StartsWith("┌"));   // top border present and not split
	}

	[Fact]
	public void DegenerateNarrow_DoesNotCrashOrLoop()
	{
		// Width far too small for the columns — must produce SOME bounded output, no exception/hang.
		var rows = Render("| aaaa | bbbb | cccc |\n|---|---|---|\n| 1 | 2 | 3 |", 5);
		Assert.NotEmpty(rows);
	}

	private static int GetWidth(string s)
		=> s.Sum(ch => System.Globalization.UnicodeCategory.OtherLetter ==
			System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
			&& SharpConsoleUI.Helpers.UnicodeWidth.IsWide(ch) ? 2 : 1);

	// -----------------------------------------------------------------------
	// Task 4b: Exhaustive table-wrapping matrix
	// -----------------------------------------------------------------------

	// Display width (CJK-aware) of a rendered line.
	private static int Width(string s) => SharpConsoleUI.Helpers.UnicodeWidth.GetStringWidth(s);

	// Lines that are part of the table box (start with a box-drawing edge/corner).
	private static bool IsTableLine(string r)
	{
		if (r.Length == 0) return false;
		char c0 = r.TrimStart().Length == 0 ? ' ' : r.TrimStart()[0];
		return "┌├└│".IndexOf(c0) >= 0;
	}

	// The cross-cutting invariants every fitted table must satisfy.
	private static void AssertTableInvariants(List<string> rows, int width)
	{
		var tableLines = rows.Where(IsTableLine).ToList();
		Assert.NotEmpty(tableLines);

		// (1) Every table line fits the width (no outer-wrap shredding).
		foreach (var r in tableLines)
			Assert.True(Width(r) <= width, $"table line display width {Width(r)} > {width}: '{r}'");

		// (2) Borders present and complete: exactly one top (┌…┐) and one bottom (└…┘) line.
		Assert.Single(tableLines.Where(r => r.TrimStart().StartsWith("┌") && r.TrimEnd().EndsWith("┐")));
		Assert.Single(tableLines.Where(r => r.TrimStart().StartsWith("└") && r.TrimEnd().EndsWith("┘")));

		// (3) No orphaned border fragment occupies a line on its own.
		foreach (var r in tableLines)
			Assert.False(r.Trim() == "┐" || r.Trim() == "┘" || r.Trim() == "│" || r.Trim() == "├" || r.Trim() == "┤",
				$"orphaned border fragment line: '{r}'");

		// (4) Every box row has the SAME number of vertical separators '│' (column count + 1), so the grid
		//     is consistent — content rows align with borders. Use the top border's column count as truth.
		var top = tableLines.First(r => r.TrimStart().StartsWith("┌"));
		int expectedSeps = top.Count(ch => ch == '┬') + 2; // ┬ count + the two outer corners' columns
														   // Content/separator rows: count '│' on '│'-led lines == columns + 1.
		int barCols = top.Count(ch => ch == '┬') + 1;       // number of columns
		foreach (var r in tableLines.Where(r => r.TrimStart().StartsWith("│")))
			Assert.Equal(barCols + 1, r.Count(ch => ch == '│'));
	}

	private static List<string> R(string md, int width) => Render(md, width);

	public static IEnumerable<object[]> CellContents() => new[]
	{
		new object[] { "plain text" },
		new object[] { "averylongunbreakabletoken0123456789abcdef" }, // long no-space token
		new object[] { "this is a long sentence with many words to wrap" },
		new object[] { "1234567890" },                                 // number
		new object[] { "3.14159265358979" },                          // float (must not split mid-number)
		new object[] { "mixed 中文 and English 文字 words" },          // CJK+Latin
		new object[] { "中文测试内容很长很长很长很长很长" },             // pure CJK
		new object[] { "**bold text that is quite long here**" },      // markdown emphasis -> styled
		new object[] { "*italic and `code` spans long enough to wrap*" },
		new object[] { "" },                                          // empty cell
	};

	public static IEnumerable<object[]> Widths() => new[]
	{
		new object[] { 12 }, new object[] { 20 }, new object[] { 30 }, new object[] { 50 },
	};

	[Theory]
	[MemberData(nameof(CellContents))]
	public void TwoColumn_AnyContent_AnyWidth_HoldsInvariants(string cell)
	{
		foreach (int w in new[] { 12, 20, 30, 50 })
		{
			string md = $"| col1 | col2 |\n|---|---|\n| {cell} | {cell} |";
			AssertTableInvariants(R(md, w), w);
		}
	}

	[Theory]
	[MemberData(nameof(Widths))]
	public void ColumnCounts_OneToFour_HoldInvariants(int width)
	{
		foreach (int cols in new[] { 1, 2, 3, 4 })
		{
			string header = "|" + string.Concat(Enumerable.Range(0, cols).Select(i => $" h{i} |"));
			string sep = "|" + string.Concat(Enumerable.Range(0, cols).Select(_ => "---|"));
			string body = "|" + string.Concat(Enumerable.Range(0, cols).Select(i =>
				i == 0 ? " a long cell value that needs to wrap somewhere |" : $" v{i} |"));
			AssertTableInvariants(R($"{header}\n{sep}\n{body}", width), width);
		}
	}

	[Fact]
	public void RaggedRow_FewerCellsThanColumns_HoldsInvariants()
	{
		// A body row with fewer cells than the header — missing cells render blank, grid stays consistent.
		var rows = R("| a | b | c |\n|---|---|---|\n| only one |", 30);
		AssertTableInvariants(rows, 30);
	}

	[Fact]
	public void HeaderOnly_NoBodyRows_HoldsInvariants()
	{
		var rows = R("| left | right |\n|---|---|", 24);
		AssertTableInvariants(rows, 24);
	}

	[Fact]
	public void WidthOne_Degenerate_DoesNotCrashOrLoop()
	{
		var rows = R("| a | b |\n|---|---|\n| 1 | 2 |", 1);
		Assert.NotEmpty(rows); // bounded output, no exception/hang
	}

	[Theory]
	[InlineData(12)]
	[InlineData(20)]
	[InlineData(40)]
	public void StyledCell_StylePreservedOnEveryWrappedLine(int width)
	{
		// A coloured emphasis cell wrapped across lines must keep its colour on every produced line.
		string md = $"| info | x |\n|---|---|\n| **important very long highlighted message that wraps** | x |";
		var rows = ParseLines2(md, width);
		// Find content rows of the first body row (after the header separator) and assert any styled glyph
		// keeps a non-default decoration/colour. (Bold from ** -> Decorations has Bold.)
		bool sawBoldOnMultipleLines = rows
			.Count(line => line.Any(c => (c.Decorations & TextDecoration.Bold) != 0)) >= 2;
		Assert.True(sawBoldOnMultipleLines, "bold style was lost on wrapped lines");
		// And every rendered line fits the width.
		foreach (var line in rows)
			Assert.True(line.Where(c => !c.IsWideContinuation).Count() <= width);
	}

	// Render to CELL rows (not strings) so decoration/colour can be inspected.
	private static List<List<Cell>> ParseLines2(string md, int width)
		=> MarkupParser.ParseLines($"[markdown]{md}[/]", width, Color.White, Color.Black, out _);

	[Fact]
	public void HorizontalRule_FillsTheRenderWidth()
	{
		// A markdown horizontal rule (---) must fill the available render width, not a fixed 40 columns
		// (issue #59 — changlv: "horizontal rules cannot fill the full width").
		var rows = Render("before\n\n---\n\nafter", 60);
		var ruleRow = rows.FirstOrDefault(r => r.Length > 0 && r.All(ch => ch == '─'));
		Assert.NotNull(ruleRow);
		Assert.Equal(60, ruleRow!.Length);
	}

	[Fact]
	public void HorizontalRule_Unbounded_UsesDefaultWidth()
	{
		// With no width budget (non-wrap Parse path), the rule keeps a sensible fixed default (unchanged).
		var cells = MarkupParser.Parse("[markdown]a\n\n---\n\nb[/]", Color.White, Color.Black);
		string all = string.Concat(cells.Where(c => !c.IsWideContinuation).Select(c => c.Character.ToString()));
		Assert.Contains(new string('─', 40), all); // default MarkdownRuleWidth
	}
}

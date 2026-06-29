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
using SharpConsoleUI.Parsing;
using Xunit;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Exhaustive regression tests for issue #59: assigning a MULTI-LINE <c>[markdown]…[/]</c> region to
/// <see cref="MarkupControl.Text"/> rendered the Markdown LITERALLY (raw "#", raw "| col |", no box
/// table) because the Text setter line-split the value on "\n", tearing the region across content
/// entries — line 0 = "[markdown]" (unclosed), the table on bare lines, "[/]" orphaned — so the
/// per-line parse never saw a complete region. The fix keeps each balanced <c>[markdown]…[/]</c>
/// region (embedded newlines included) as ONE content entry, exactly like SetMarkdown stores it.
///
/// All assertions are at the parser/control level (no Window harness — a windowed render of this
/// content can hang). The render oracle is <see cref="MarkupParser.ParseLines"/> — the path the
/// control actually uses (EnsureParsed → ParseLines), which expands [markdown]…[/] via
/// PreProcessMarkdownTags BEFORE splitting on "\n", so an INTACT region yields multiple rows with no
/// U+FFFD. (The raw Parse(wholeString) overload is NOT used as the oracle: it does not split on "\n",
/// so it sanitizes embedded newlines to U+FFFD and would mislead.)
/// </summary>
public class MarkupControlMultilineMarkdownTests
{
	private static readonly System.Text.Rune Replacement = new('�');

	private const string TableMarkdown =
		"[markdown]\n# header\n\n| col1 | col2 |\n|---|---|\n| a | b |\n[/]";

	// ---- Render-path helpers (the REAL path: ParseLines expands [markdown] then splits on "\n"). ----

	/// <summary>Render a control's current content through the real path (one ParseLines per entry).</summary>
	private static List<List<Cell>> RenderRows(MarkupControl c, int width = 40)
	{
		var rows = new List<List<Cell>>();
		foreach (var entry in c.GetContentLinesForTest())
			rows.AddRange(MarkupParser.ParseLines(entry, width, Color.White, Color.Black, out _, null));
		return rows;
	}

	private static bool HasReplacementGlyph(List<List<Cell>> rows)
	{
		foreach (var row in rows)
			foreach (var cell in row)
				if (cell.Character == Replacement) return true;
		return false;
	}

	private static bool HasRune(List<List<Cell>> rows, char c)
	{
		var rune = new System.Text.Rune(c);
		foreach (var row in rows)
			foreach (var cell in row)
				if (cell.Character == rune) return true;
		return false;
	}

	private static bool HasBoxChars(List<List<Cell>> rows)
		=> HasRune(rows, '┌') || HasRune(rows, '│') || HasRune(rows, '└')
		|| HasRune(rows, '┐') || HasRune(rows, '┘') || HasRune(rows, '─');

	private static string RowText(List<Cell> row)
	{
		var sb = new System.Text.StringBuilder();
		foreach (var cell in row)
			if (cell.Character.Value != 0) sb.Append(cell.Character.ToString());
		return sb.ToString();
	}

	/// <summary>Joined plain text of all rendered rows (whitespace-trimmed per row).</summary>
	private static string RenderedText(List<List<Cell>> rows)
		=> string.Join("\n", rows.ConvertAll(r => RowText(r)));

	// =====================================================================================
	// The reported bug: Text = multi-line [markdown] table.
	// =====================================================================================

	[Fact]
	public void TextSetter_MultilineTable_IsSingleAtomicEntry()
	{
		var c = new MarkupControl(new List<string> { "placeholder" });
		c.Text = TableMarkdown;

		var lines = c.GetContentLinesForTest();
		Assert.Single(lines);
		Assert.StartsWith("[markdown]", lines[0]);
		Assert.EndsWith("[/]", lines[0]);
		Assert.Contains("\n", lines[0]); // embedded newlines preserved INSIDE the one entry
	}

	[Fact]
	public void TextSetter_MultilineTable_RendersBoxRows_NoReplacementGlyph()
	{
		var c = new MarkupControl(new List<string> { "placeholder" });
		c.Text = TableMarkdown;
		var rows = RenderRows(c);

		Assert.True(rows.Count > 1, "A multi-line markdown table must render as multiple rows.");
		Assert.True(HasBoxChars(rows), "The table must box-draw (it was parsed, not literal).");
		Assert.False(HasReplacementGlyph(rows), "No U+FFFD cells (issue #45/#59).");
		Assert.DoesNotContain("| col1 |", RenderedText(rows)); // literal pipe syntax must be gone
	}

	[Fact]
	public void TornRegionFragment_RegressionWitness_DoesNotBoxDraw()
	{
		// Witness the BUG the fix prevents: a torn "[markdown]" fragment (the old per-line split's first
		// entry) renders with NO box chars — proving the atomic-entry fix is load-bearing.
		var rows = MarkupParser.ParseLines("[markdown]", 40, Color.White, Color.Black, out _, null);
		Assert.False(rows.Exists(r => RowText(r).Contains('┌')),
			"A torn '[markdown]' fragment alone must not box-draw.");
	}

	// =====================================================================================
	// Markdown content shapes (each through Text= and asserted via the render path).
	// =====================================================================================

	[Theory]
	[InlineData("# h1\n## h2\n### h3")]                                    // headers
	[InlineData("- a\n- b\n- c")]                                          // bullet list
	[InlineData("1. one\n2. two\n3. three")]                              // ordered list
	[InlineData("above\n\n---\n\nbelow")]                                 // horizontal rule
	[InlineData("| col1 | col2 |\n|---|---|\n| a | b |")]                 // table
	[InlineData("**bold** and *italic* and ~~strike~~")]                  // nested emphasis
	[InlineData("`inline code` here")]                                    // inline code
	[InlineData("```\ncode block\nline two\n```")]                        // fenced code block
	[InlineData("> a quote\n> second line")]                              // blockquote
	[InlineData("[a link](https://example.com)")]                         // link
	public void TextSetter_MarkdownShape_StaysAtomic_AndRendersWithoutReplacementGlyph(string md)
	{
		var c = new MarkupControl(new List<string>());
		c.Text = $"[markdown]\n{md}\n[/]";

		Assert.Single(c.GetContentLinesForTest());
		var rows = RenderRows(c, 50);
		Assert.False(HasReplacementGlyph(rows), $"No U+FFFD for shape: {md}");
		Assert.True(rows.Count >= 1);
	}

	[Fact]
	public void TextSetter_SingleLineMarkdown_Works()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "[markdown]# header[/]";
		Assert.Single(c.GetContentLinesForTest());
		var rows = RenderRows(c);
		Assert.False(HasReplacementGlyph(rows));
		Assert.DoesNotContain("# header", RenderedText(rows)); // '#' consumed by markdown
	}

	// =====================================================================================
	// Plain (non-markdown) markup — documented scope: NOT kept atomic across lines.
	// A multi-line non-markdown tag is NOT made atomic, because even an atomic _content entry would
	// still be torn at render time: MarkupParser.ParseLines splits each logical line on "\n" and parses
	// every sub-line independently, so an open tag's style does not carry to the next line. [markdown]
	// is the exception (PreProcessMarkdownTags expands it to per-line native markup BEFORE that split).
	// Verified empirically: ParseLines("[yellow]A\nB[/]") colors 'A' yellow but 'B' white. A real fix
	// requires threading the open-style stack across newlines inside the (frozen) ParseLines path — a
	// separate maintainer-gated change. See the fix report.
	// =====================================================================================

	[Fact]
	public void TextSetter_SingleLinePlainTag_Unchanged()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "[yellow]hello[/]";
		Assert.Equal(new[] { "[yellow]hello[/]" }, c.GetContentLinesForTest());
		Assert.False(HasReplacementGlyph(RenderRows(c)));
	}

	[Fact]
	public void TextSetter_MultilinePlainTag_StaysAtomic_AndStyleCarriesAcrossLines()
	{
		// A multi-line NON-markdown tag ([yellow]…[/] spanning a newline) is now kept atomic in one
		// content entry (general balanced-tag split), so the render path carries the [yellow] style
		// across the newline — both lines render yellow, not just line 1.
		var c = new MarkupControl(new List<string>());
		c.Text = "[yellow]line A\nline B[/]";
		Assert.Equal(new[] { "[yellow]line A\nline B[/]" }, c.GetContentLinesForTest()); // one atomic entry
		var rows = RenderRows(c);
		Assert.False(HasReplacementGlyph(rows));
		// Both rows carry the yellow foreground.
		Assert.True(rows.Count >= 2);
		Assert.Contains(rows[0], cell => cell.Foreground == Color.Yellow);
		Assert.Contains(rows[1], cell => cell.Foreground == Color.Yellow);
	}

	// =====================================================================================
	// Mixed content: text around regions, multiple regions, regions + plain lines.
	// =====================================================================================

	[Fact]
	public void TextSetter_TextBeforeAndAfterRegion_SplitsAroundAtomicRegion()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "intro\n[markdown]\n# h\n[/]\noutro";
		var lines = c.GetContentLinesForTest();
		Assert.Equal(3, lines.Count);
		Assert.Equal("intro", lines[0]);
		Assert.StartsWith("[markdown]", lines[1]);
		Assert.EndsWith("[/]", lines[1]);
		Assert.Contains("# h", lines[1]);
		Assert.Equal("outro", lines[2]);
	}

	[Fact]
	public void TextSetter_MultipleMarkdownRegions_EachAtomic()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "[markdown]\n# one\n[/]\nmiddle\n[markdown]\n# two\n[/]";
		var lines = c.GetContentLinesForTest();
		Assert.Equal(3, lines.Count);
		Assert.Contains("# one", lines[0]);
		Assert.StartsWith("[markdown]", lines[0]);
		Assert.Equal("middle", lines[1]);
		Assert.Contains("# two", lines[2]);
		Assert.StartsWith("[markdown]", lines[2]);
		Assert.False(HasReplacementGlyph(RenderRows(c)));
	}

	[Fact]
	public void TextSetter_RegionFollowedByPlainLines()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "[markdown]\n# h\n[/]\nplain1\nplain2";
		var lines = c.GetContentLinesForTest();
		Assert.Equal(3, lines.Count);
		Assert.StartsWith("[markdown]", lines[0]);
		Assert.Equal("plain1", lines[1]);
		Assert.Equal("plain2", lines[2]);
	}

	[Fact]
	public void TextSetter_TwoAdjacentRegions_NoTextBetween()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "[markdown]# a[/][markdown]# b[/]";
		var lines = c.GetContentLinesForTest();
		// Both regions fall on the same source line (no newline between) → one content entry.
		Assert.Single(lines);
		Assert.Equal("[markdown]# a[/][markdown]# b[/]", lines[0]);
	}

	// =====================================================================================
	// Round-trip: Text getter (string.Join("\n", _content)) reverses the setter's split.
	// =====================================================================================

	[Theory]
	[InlineData(TableMarkdown)]
	[InlineData("before\n[markdown]\n# h\n[/]\nafter")]
	[InlineData("[markdown]x[/]")]
	[InlineData("[markdown]\n# one\n[/]\nmiddle\n[markdown]\n# two\n[/]")]
	[InlineData("a\nb\nc")]
	[InlineData("[red]plain[/]")]
	[InlineData("[yellow]line A\nline B[/]")] // non-markdown multi-line tag: torn per line, still round-trips
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\n")]
	[InlineData("\nleading")]
	[InlineData("trailing\n")]
	[InlineData("double\n\nblank")]
	[InlineData("[markdown]\nunclosed and trailing")]
	[InlineData("[[markdown]]\nsecond")]
	[InlineData("orphan [/] close")]
	[InlineData("see the [markdown] keyword inline")]
	public void TextSetter_RoundTrips_OnLfInputs(string value)
	{
		var c = new MarkupControl(new List<string>());
		c.Text = value;
		// Getter normalizes \r\n / \r to \n; these inputs use \n only, so they round-trip exactly.
		Assert.Equal(value, c.Text);
	}

	// =====================================================================================
	// Edge cases.
	// =====================================================================================

	[Fact]
	public void TextSetter_EmptyString_SingleEmptyEntry()
	{
		var c = new MarkupControl(new List<string> { "x" });
		c.Text = "";
		Assert.Equal(new[] { "" }, c.GetContentLinesForTest());
		Assert.Equal("", c.Text);
	}

	[Fact]
	public void TextSetter_WhitespaceOnly_Preserved()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "   ";
		Assert.Equal(new[] { "   " }, c.GetContentLinesForTest());
		Assert.Equal("   ", c.Text);
	}

	[Fact]
	public void TextSetter_LoneNewline_TwoEmptyEntries()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "\n";
		Assert.Equal(new[] { "", "" }, c.GetContentLinesForTest());
		Assert.Equal("\n", c.Text);
	}

	[Fact]
	public void TextSetter_LeadingNewlineBeforeMarkdown_StaysAtomic()
	{
		// The user's exact text starts with "\n" right after "[markdown]"; also test a leading bare \n.
		var c = new MarkupControl(new List<string>());
		c.Text = "\n[markdown]\n# h\n[/]";
		var lines = c.GetContentLinesForTest();
		Assert.Equal(2, lines.Count);
		Assert.Equal("", lines[0]);
		Assert.StartsWith("[markdown]", lines[1]);
		Assert.EndsWith("[/]", lines[1]);
	}

	[Fact]
	public void TextSetter_ConsecutiveBlankLines_Preserved()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "a\n\n\nb";
		Assert.Equal(new[] { "a", "", "", "b" }, c.GetContentLinesForTest());
		Assert.Equal("a\n\n\nb", c.Text);
	}

	[Fact]
	public void TextSetter_Crlf_InsideMarkdownRegion_StaysAtomic_AndRendersRows()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "[markdown]\r\n# h\r\n\r\n| a | b |\r\n|---|---|\r\n| 1 | 2 |\r\n[/]";
		Assert.Single(c.GetContentLinesForTest());
		var rows = RenderRows(c);
		Assert.True(rows.Count > 1);
		Assert.False(HasReplacementGlyph(rows));
		Assert.True(HasBoxChars(rows));
		// The CRLF lives INSIDE the atomic region, so it is preserved verbatim in the entry (the region
		// is kept byte-for-byte). ParseLines normalizes \r\n/\r at render time, which is what matters —
		// the rendered rows above are clean (no U+FFFD, box chars present).
	}

	[Fact]
	public void TextSetter_BareCr_InsideMarkdownRegion_StaysAtomic()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "[markdown]\r# h\r[/]";
		Assert.Single(c.GetContentLinesForTest());
		Assert.False(HasReplacementGlyph(RenderRows(c)));
	}

	[Fact]
	public void TextSetter_CrlfOutsideRegion_SplitsLikeBefore()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "a\r\nb\r\nc";
		Assert.Equal(new[] { "a", "b", "c" }, c.GetContentLinesForTest());
	}

	[Fact]
	public void TextSetter_EscapedMarkdown_IsNotTreatedAsRegion()
	{
		// "[[markdown]]" is an escaped literal, NOT a tag — it must NOT swallow following newlines.
		var c = new MarkupControl(new List<string>());
		c.Text = "[[markdown]]\nsecond";
		Assert.Equal(new[] { "[[markdown]]", "second" }, c.GetContentLinesForTest());
	}

	[Fact]
	public void TextSetter_UnclosedMarkdown_KeepsRemainderAsOneEntry_AndRenders()
	{
		// No "[/]" — mirror PreProcessMarkdownTags: everything after the tag is one markdown region.
		var c = new MarkupControl(new List<string>());
		c.Text = "before\n[markdown]\n# h\nstill markdown";
		var lines = c.GetContentLinesForTest();
		Assert.Equal(2, lines.Count);
		Assert.Equal("before", lines[0]);
		Assert.StartsWith("[markdown]", lines[1]);
		Assert.Contains("still markdown", lines[1]);
		Assert.False(HasReplacementGlyph(RenderRows(c)));
	}

	[Fact]
	public void TextSetter_OrphanClose_NoOpen_SplitsPerLine()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "first\n[/]\nthird";
		// No "[markdown]" anywhere → fast path, split per line.
		Assert.Equal(new[] { "first", "[/]", "third" }, c.GetContentLinesForTest());
		Assert.False(HasReplacementGlyph(RenderRows(c)));
	}

	[Fact]
	public void TextSetter_LiteralMarkdownWordMidLine_TreatedAsTag_MirrorsPreProcess()
	{
		// A bare "[markdown]" in the middle of normal text IS the tag (no [/] → unclosed region to end).
		// This intentionally mirrors PreProcessMarkdownTags so the setter and parser agree.
		var c = new MarkupControl(new List<string>());
		c.Text = "see the [markdown] keyword inline";
		var lines = c.GetContentLinesForTest();
		Assert.Single(lines);
		Assert.Equal("see the [markdown] keyword inline", lines[0]);
		Assert.Equal("see the [markdown] keyword inline", c.Text); // round-trips
	}

	[Fact]
	public void TextSetter_NestedTagInside_DepthAware_StaysAtomic_NoCrash()
	{
		// The general splitter is depth-aware (reuses MarkupParser.FindMatchingCloseTag): an inner
		// "[markdown]" counts as a nested open, so a single "[/]" closes the INNER scope and the outer
		// region runs to end-of-string (unclosed → kept as one entry, not torn). Pathological nesting;
		// the contract here is "does not crash, round-trips, no replacement glyph".
		var c = new MarkupControl(new List<string>());
		c.Text = "[markdown]\n# outer [markdown] inner\n[/]\ntail";
		var lines = c.GetContentLinesForTest();
		Assert.Single(lines);                                  // unclosed outer → one atomic entry
		Assert.Equal("[markdown]\n# outer [markdown] inner\n[/]\ntail", c.Text); // round-trips
		Assert.False(HasReplacementGlyph(RenderRows(c)));
	}

	[Fact]
	public void TextSetter_VeryLongSingleLine_OneEntry()
	{
		var c = new MarkupControl(new List<string>());
		string longLine = new string('x', 5000);
		c.Text = longLine;
		Assert.Equal(new[] { longLine }, c.GetContentLinesForTest());
		Assert.Equal(longLine, c.Text);
	}

	[Fact]
	public void TextSetter_ReAssigned_InvalidatesCache_NoStaleContent()
	{
		var c = new MarkupControl(new List<string>());
		c.Text = "[markdown]\n# first\n[/]";
		int v1 = c.GetContentVersionForTest();
		var rows1 = RenderRows(c);
		Assert.Contains("first", RenderedText(rows1));

		c.Text = "[markdown]\n# second\n[/]";
		int v2 = c.GetContentVersionForTest();
		Assert.NotEqual(v1, v2); // BumpContentVersion fired → cache key changes

		var rows2 = RenderRows(c);
		Assert.Contains("second", RenderedText(rows2));
		Assert.DoesNotContain("first", RenderedText(rows2)); // no stale content served
	}

	// =====================================================================================
	// Part B: Markdown() alias + other entry points keep multi-line markdown intact.
	// =====================================================================================

	[Fact]
	public void Markdown_ProducesIdenticalContentToSetMarkdown()
	{
		const string md = "# header\n\n| col1 | col2 |\n|---|---|\n| a | b |";

		var viaMethod = new MarkupControl(new List<string>());
		viaMethod.Markdown(md);

		var viaSet = new MarkupControl(new List<string>());
		viaSet.SetMarkdown(md);

		Assert.Equal(viaSet.GetContentLinesForTest(), viaMethod.GetContentLinesForTest());
	}

	[Fact]
	public void Markdown_StoresAsSingleWrappedRegion_AndBoxDraws()
	{
		var c = new MarkupControl(new List<string>());
		c.Markdown("| col1 | col2 |\n|---|---|\n| a | b |");
		var lines = c.GetContentLinesForTest();
		Assert.Single(lines);
		Assert.StartsWith("[markdown]", lines[0]);
		Assert.EndsWith("[/]", lines[0]);

		var rows = RenderRows(c);
		Assert.True(HasBoxChars(rows));
		Assert.False(HasReplacementGlyph(rows));
	}

	[Fact]
	public void Markdown_NullArgument_DoesNotThrow()
	{
		var c = new MarkupControl(new List<string>());
		c.Markdown(null!);
		var lines = c.GetContentLinesForTest();
		Assert.Single(lines);
		Assert.Equal("[markdown][/]", lines[0]);
	}

	[Fact]
	public void SetContent_MultilineMarkdownEntry_RendersWithoutReplacementGlyph()
	{
		// SetContent stores the list verbatim — a caller that passes one "[markdown]…[/]" entry must
		// render correctly (it does not line-split, so the region is already atomic).
		var c = new MarkupControl(new List<string>());
		c.SetContent(new List<string> { TableMarkdown });
		var rows = RenderRows(c);
		Assert.True(HasBoxChars(rows));
		Assert.False(HasReplacementGlyph(rows));
	}

	[Fact]
	public void Builder_InitialLine_MultilineMarkdown_StaysAtomic()
	{
		// Controls.Markup(initialLine) → AddLine(verbatim): a multi-line region passed as the initial
		// line is one entry and renders correctly.
		var c = SharpConsoleUI.Builders.Controls.Markup(TableMarkdown).Build();
		Assert.Single(c.GetContentLinesForTest());
		var rows = RenderRows(c);
		Assert.True(HasBoxChars(rows));
		Assert.False(HasReplacementGlyph(rows));
	}

	[Fact]
	public void Builder_AddMarkdown_And_WithMarkdown_StoreSameAtomicRegion()
	{
		const string md = "# h\n\n| a | b |\n|---|---|\n| 1 | 2 |";
		var a = SharpConsoleUI.Builders.Controls.Markup().AddMarkdown(md).Build();
		var b = SharpConsoleUI.Builders.Controls.Markup().WithMarkdown(md).Build();

		Assert.Equal(a.GetContentLinesForTest(), b.GetContentLinesForTest());
		Assert.Single(a.GetContentLinesForTest());

		var rows = RenderRows(a);
		Assert.True(HasBoxChars(rows));
		Assert.False(HasReplacementGlyph(rows));
	}

	[Fact]
	public void Builder_AddMarkdown_EqualsControlSetMarkdown()
	{
		const string md = "# h\ntext";
		var built = SharpConsoleUI.Builders.Controls.Markup().AddMarkdown(md).Build();
		var viaSet = new MarkupControl(new List<string>());
		viaSet.SetMarkdown(md);
		Assert.Equal(viaSet.GetContentLinesForTest(), built.GetContentLinesForTest());
	}

	[Fact]
	public void AppendLine_MultilineMarkdownEntry_StaysAtomic()
	{
		// AppendLine stores its argument verbatim (one entry) — a multi-line region is preserved.
		var c = new MarkupControl(new List<string>());
		c.AppendLine(TableMarkdown);
		Assert.Single(c.GetContentLinesForTest());
		var rows = RenderRows(c);
		Assert.True(HasBoxChars(rows));
		Assert.False(HasReplacementGlyph(rows));
	}

	[Fact]
	public void AppendLines_MultilineMarkdownEntries_EachAtomic()
	{
		var c = new MarkupControl(new List<string>());
		c.AppendLines(new[] { TableMarkdown, TableMarkdown });
		Assert.Equal(2, c.GetContentLinesForTest().Count);
		var rows = RenderRows(c);
		Assert.True(HasBoxChars(rows));
		Assert.False(HasReplacementGlyph(rows));
	}

	[Fact]
	public void AppendText_MultilineMarkdown_SplitsPerLine_DocumentedLimitation()
	{
		// SCOPE: AppendText/Append split on "\n" by design (Console.Write semantics), so a multi-line
		// [markdown] passed there IS torn — use AppendLine / SetMarkdown / Markdown for atomic regions.
		var c = new MarkupControl(new List<string>());
		c.AppendText(TableMarkdown);
		Assert.True(c.GetContentLinesForTest().Count > 1, "AppendText splits on newline (documented).");
	}
}

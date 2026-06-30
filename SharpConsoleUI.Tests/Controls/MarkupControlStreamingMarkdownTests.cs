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

public class MarkupControlStreamingMarkdownTests
{
	private const int Width = 50;

	private static string RowText(List<Cell> row)
		=> string.Concat(row.Where(c => c.Character.Value != 0).Select(c => c.Character.ToString()));

	private static bool AnyRowHas(List<List<Cell>> rows, char ch)
		=> rows.Any(r => RowText(r).IndexOf(ch) >= 0);

	private static bool AnyRowHasReplacement(List<List<Cell>> rows)
		=> rows.Any(r => r.Any(c => c.Character.Value == 0xFFFD));

	// Streaming via AppendLine: one [markdown] block built across three calls renders as markdown.
	[Fact]
	public void AppendLine_StreamingMarkdown_RendersAsOneBlock()
	{
		var m = new MarkupControl(new List<string>());
		m.AppendLine("[markdown]# Title");
		m.AppendLine("- item 1");
		m.AppendLine("- item 2");

		var parsed = m.EnsureParsedForTest(Width);
		// Markdown applied: a bullet glyph appears, the literal "# " heading marker does NOT.
		Assert.True(AnyRowHas(parsed.Rows, '•'), "expected a bullet (•) — markdown should be applied");
		Assert.False(parsed.Rows.Any(r => RowText(r).StartsWith("# ")), "heading marker '# ' should be consumed by markdown");
		Assert.False(AnyRowHasReplacement(parsed.Rows), "no U+FFFD");
	}

	// AppendText with embedded newlines: the whole [markdown] block renders.
	[Fact]
	public void AppendText_MultilineMarkdown_RendersAsOneBlock()
	{
		var m = new MarkupControl(new List<string>());
		m.AppendText("[markdown]# Title\n- item 1\n- item 2[/]");

		var parsed = m.EnsureParsedForTest(Width);
		Assert.True(AnyRowHas(parsed.Rows, '•'));
		Assert.False(AnyRowHasReplacement(parsed.Rows));
	}

	// All rows of a streamed [markdown] block are attributed to the FIRST entry (so copy treats it as
	// one logical block — matching Text= behavior).
	[Fact]
	public void StreamedMarkdown_RowsAttributedToFirstEntry()
	{
		var m = new MarkupControl(new List<string>());
		m.AppendLine("[markdown]# Title");
		m.AppendLine("- item 1");
		m.AppendLine("- item 2");

		var parsed = m.EnsureParsedForTest(Width);
		Assert.All(parsed.RowSourceLine, src => Assert.Equal(0, src));
	}

	// Progressive: rendering after each append shows the markdown of the accumulated block so far.
	[Fact]
	public void Streaming_RendersProgressively()
	{
		var m = new MarkupControl(new List<string>());
		m.AppendLine("[markdown]# Title");
		Assert.False(AnyRowHasReplacement(m.EnsureParsedForTest(Width).Rows));
		m.AppendLine("- item 1");
		var p2 = m.EnsureParsedForTest(Width);
		Assert.True(AnyRowHas(p2.Rows, '•'), "bullet appears once the list item streams in");
	}

	// Regression: plain multi-entry content (no [markdown]) parses per entry — each entry its own
	// source index, so copy inserts newlines between them.
	[Fact]
	public void PlainMultiEntry_KeepsPerEntrySourceAttribution()
	{
		var m = new MarkupControl(new List<string>());
		m.AppendLine("line A");
		m.AppendLine("line B");
		m.AppendLine("line C");

		var parsed = m.EnsureParsedForTest(Width);
		Assert.Equal(3, parsed.Rows.Count);
		Assert.Equal(new[] { 0, 1, 2 }, parsed.RowSourceLine.ToArray());
	}

	// Unclosed at end (stream still in flight) renders progressively, no replacement glyph.
	[Fact]
	public void UnclosedMarkdownAtEnd_RendersToEnd()
	{
		var m = new MarkupControl(new List<string>());
		m.AppendLine("[markdown]# Heading");
		m.AppendLine("- a");
		var parsed = m.EnsureParsedForTest(Width);
		Assert.True(AnyRowHas(parsed.Rows, '•'));
		Assert.False(AnyRowHasReplacement(parsed.Rows));
	}
}

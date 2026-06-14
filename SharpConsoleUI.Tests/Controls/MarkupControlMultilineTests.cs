// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for issue #45: a MarkupControl logical line containing an embedded "\n" rendered
/// the newline as a U+FFFD (◆) replacement glyph, because the non-wrap paint path handed the raw
/// "\n" to MarkupParser.Parse (which sanitizes control chars to U+FFFD). The fix splits on "\n" in
/// the paint path so embedded newlines become separate rows instead of replacement glyphs.
/// </summary>
public class MarkupControlMultilineTests
{
	private static readonly Rune Replacement = new('�');

	private static CharacterBuffer Paint(MarkupControl control, int w = 40, int h = 12)
	{
		var buffer = new CharacterBuffer(w + 5, h + 3);
		var bounds = new LayoutRect(0, 0, w, h);
		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return buffer;
	}

	private static bool BufferContains(CharacterBuffer buf, Rune rune, int w, int h)
	{
		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				if (buf.GetCell(x, y).Character == rune)
					return true;
		return false;
	}

	private static string RowText(CharacterBuffer buf, int y, int w)
	{
		var sb = new StringBuilder();
		for (int x = 0; x < w; x++)
		{
			var ch = buf.GetCell(x, y).Character;
			if (ch.Value != 0) sb.Append(ch.ToString());
		}
		return sb.ToString().TrimEnd();
	}

	[Fact]
	public void EmbeddedNewline_DoesNotRenderReplacementGlyph()
	{
		// The exact #45 shape: one logical line carrying embedded newlines (e.g. a multi-line
		// message from a MultilineEdit submitted as a single AddLine).
		var c = new MarkupControl(new List<string> { "a\nb\nc\nd" });
		var buf = Paint(c);
		Assert.False(BufferContains(buf, Replacement, 45, 15),
			"Embedded '\\n' must not render as the U+FFFD replacement glyph (issue #45).");
	}

	[Fact]
	public void EmbeddedNewline_RendersOnSeparateRows()
	{
		var c = new MarkupControl(new List<string> { "a\nb\nc\nd" });
		var buf = Paint(c);
		Assert.Equal("a", RowText(buf, 0, 40));
		Assert.Equal("b", RowText(buf, 1, 40));
		Assert.Equal("c", RowText(buf, 2, 40));
		Assert.Equal("d", RowText(buf, 3, 40));
	}

	[Fact]
	public void EmbeddedNewline_DigitsCase_NoTrailingDiamond()
	{
		// Mirrors the reporter's first screenshot: digits each followed by a stray ◆ before the fix.
		var c = new MarkupControl(new List<string> { "1\n3\n3\n4\n4\n5" });
		var buf = Paint(c);
		Assert.False(BufferContains(buf, Replacement, 45, 15));
		Assert.Equal("1", RowText(buf, 0, 40));
		Assert.Equal("5", RowText(buf, 5, 40));
	}

	[Fact]
	public void Markup_StillParsesPerSubline()
	{
		// Markup tags must still work on each split sub-line (the split is on '\n', tags intact).
		var c = new MarkupControl(new List<string> { "[red]one[/]\n[green]two[/]" });
		var buf = Paint(c);
		Assert.False(BufferContains(buf, Replacement, 45, 15));
		Assert.Equal("one", RowText(buf, 0, 40));
		Assert.Equal("two", RowText(buf, 1, 40));
	}

	[Fact]
	public void Builder_AddLineWithEmbeddedNewline_RendersCleanly()
	{
		// The reporter's actual entry point: Controls.Markup().AddLine(multiLineText).
		var c = SharpConsoleUI.Builders.Controls.Markup()
			.AddLine("x\ny\nz")
			.Build();
		var buf = Paint(c);
		Assert.False(BufferContains(buf, Replacement, 45, 15));
		Assert.Equal("x", RowText(buf, 0, 40));
		Assert.Equal("y", RowText(buf, 1, 40));
		Assert.Equal("z", RowText(buf, 2, 40));
	}

	[Fact]
	public void SingleLine_Unaffected()
	{
		var c = new MarkupControl(new List<string> { "hello world" });
		var buf = Paint(c);
		Assert.Equal("hello world", RowText(buf, 0, 40));
	}

	// --- Every MarkupControl text entry point flows through the same fixed paint path. These
	//     assert each one renders an embedded "\n" cleanly (no U+FFFD), not just the constructor.

	[Fact]
	public void AppendLine_WithEmbeddedNewline_RendersCleanly()
	{
		var c = new MarkupControl(new List<string> { "top" });
		c.AppendLine("a\nb");
		var buf = Paint(c);
		Assert.False(BufferContains(buf, Replacement, 45, 15));
		Assert.Equal("top", RowText(buf, 0, 40));
		Assert.Equal("a", RowText(buf, 1, 40));
		Assert.Equal("b", RowText(buf, 2, 40));
	}

	[Fact]
	public void AppendLines_WithEmbeddedNewline_RendersCleanly()
	{
		var c = new MarkupControl(new List<string>());
		c.AppendLines(new[] { "a\nb", "c" });
		var buf = Paint(c);
		Assert.False(BufferContains(buf, Replacement, 45, 15));
		Assert.Equal("a", RowText(buf, 0, 40));
		Assert.Equal("b", RowText(buf, 1, 40));
		Assert.Equal("c", RowText(buf, 2, 40));
	}

	[Fact]
	public void Builder_AddLines_WithEmbeddedNewline_RendersCleanly()
	{
		var c = SharpConsoleUI.Builders.Controls.Markup()
			.AddLines("a\nb", "c")
			.Build();
		var buf = Paint(c);
		Assert.False(BufferContains(buf, Replacement, 45, 15));
		Assert.Equal("a", RowText(buf, 0, 40));
		Assert.Equal("b", RowText(buf, 1, 40));
		Assert.Equal("c", RowText(buf, 2, 40));
	}

	[Fact]
	public void TextSetter_WithEmbeddedNewline_RendersCleanly()
	{
		var c = new MarkupControl(new List<string> { "placeholder" });
		c.Text = "a\nb\nc";
		var buf = Paint(c);
		Assert.False(BufferContains(buf, Replacement, 45, 15));
		Assert.Equal("a", RowText(buf, 0, 40));
		Assert.Equal("b", RowText(buf, 1, 40));
		Assert.Equal("c", RowText(buf, 2, 40));
	}
}

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

public class ParseLinesSoftWrapFlagTests
{
	private static readonly Color Fg = Color.White;
	private static readonly Color Bg = Color.Black;

	[Fact]
	public void HardNewlines_AreNotSoftWrapContinuations()
	{
		// Three hard lines, each short enough not to wrap → every row starts a new hard line.
		var rows = MarkupParser.ParseLines("aaa\nbbb\nccc", 40, Fg, Bg, out _, out var soft);
		Assert.Equal(rows.Count, soft.Count);
		Assert.Equal(3, rows.Count);
		Assert.All(soft, s => Assert.False(s)); // no soft-wrap; all hard-line starts
	}

	[Fact]
	public void WordWrappedLine_MarksContinuationRows()
	{
		// One logical line that must wrap to >1 row: first row false, the rest true.
		var rows = MarkupParser.ParseLines("alpha beta gamma delta epsilon", 8, Fg, Bg, out _, out var soft);
		Assert.Equal(rows.Count, soft.Count);
		Assert.True(rows.Count >= 2);
		Assert.False(soft[0]);                       // first row starts the (only) hard line
		Assert.All(soft.Skip(1), s => Assert.True(s)); // every later row is a soft-wrap continuation
	}

	[Fact]
	public void Mixed_HardThenWrap_FlagsAreCorrect()
	{
		// "x" (hard) then a long line that wraps. Row 0 = false; the long line's first row = false,
		// its continuations = true.
		var rows = MarkupParser.ParseLines("x\nalpha beta gamma delta", 8, Fg, Bg, out _, out var soft);
		Assert.Equal(rows.Count, soft.Count);
		Assert.False(soft[0]);   // "x"
		Assert.False(soft[1]);   // first row of the second logical line
		Assert.Contains(true, soft); // at least one continuation from the wrap
	}

	[Fact]
	public void CountInvariant_Holds_ForMarkdownBlock()
	{
		var rows = MarkupParser.ParseLines("[markdown]# Title\n\n- one\n- two[/]", 40, Fg, Bg, out _, out var soft);
		Assert.Equal(rows.Count, soft.Count);
	}
}

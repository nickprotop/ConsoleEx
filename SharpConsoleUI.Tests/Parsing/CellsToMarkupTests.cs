// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

public class CellsToMarkupTests
{
	private static readonly Color Fg = Color.White;
	private static readonly Color Bg = Color.Black;

	private static string Visible(System.Collections.Generic.List<Cell> cells)
		=> string.Concat(cells.Where(c => !c.IsWideContinuation).Select(c => c.Character.ToString()));

	[Fact]
	public void PlainText_RoundTrips_NoTags()
	{
		var cells = MarkupParser.Parse("hello world", Fg, Bg);
		string markup = MarkupParser.CellsToMarkup(cells, Fg, Bg);
		Assert.Equal("hello world", markup);                 // plain run -> no tags
		Assert.Equal(cells, MarkupParser.Parse(markup, Fg, Bg)); // round-trips
	}

	[Fact]
	public void StyledText_RoundTrips()
	{
		var cells = MarkupParser.Parse("[#FF0000 bold]red[/] plain", Fg, Bg);
		string markup = MarkupParser.CellsToMarkup(cells, Fg, Bg);
		var reparsed = MarkupParser.Parse(markup, Fg, Bg);
		Assert.Equal(cells, reparsed);                       // colors + decorations preserved
		Assert.Equal("red plain", Visible(reparsed));
	}

	[Fact]
	public void BackgroundColor_RoundTrips()
	{
		var cells = MarkupParser.Parse("[#FFFFFF on #112233]x[/]", Fg, Bg);
		string markup = MarkupParser.CellsToMarkup(cells, Fg, Bg);
		Assert.Equal(cells, MarkupParser.Parse(markup, Fg, Bg));
	}

	[Fact]
	public void WideChar_RoundTrips()
	{
		var cells = MarkupParser.Parse("中a", Fg, Bg);       // wide + narrow
		string markup = MarkupParser.CellsToMarkup(cells, Fg, Bg);
		Assert.Equal(cells, MarkupParser.Parse(markup, Fg, Bg));
	}

	[Fact]
	public void CombiningMark_RoundTrips()
	{
		// "e" + U+0301 COMBINING ACUTE ACCENT: one cell whose Combiners tail carries the mark.
		var cells = MarkupParser.Parse("e\u0301x", Fg, Bg);
		Assert.Equal("\u0301", cells[0].Combiners);          // guard: the mark is there to lose

		string markup = MarkupParser.CellsToMarkup(cells, Fg, Bg);
		Assert.Equal(cells, MarkupParser.Parse(markup, Fg, Bg));
	}

	[Fact]
	public void ZwjCluster_RoundTrips()
	{
		// A ZWJ family emoji is one grapheme spanning a base + wide-continuation cell, with
		// the rest of the cluster held in the base cell's combiner tail.
		var cells = MarkupParser.Parse("\U0001F468\u200D\U0001F469\u200D\U0001F467a", Fg, Bg);
		Assert.False(string.IsNullOrEmpty(cells[0].Combiners));

		string markup = MarkupParser.CellsToMarkup(cells, Fg, Bg);
		Assert.Equal(cells, MarkupParser.Parse(markup, Fg, Bg));
	}

	[Fact]
	public void CombiningMark_SurvivesStyledRewrap()
	{
		// The real path: a styled line that gets soft-wrapped is re-emitted through
		// CellsToMarkup, which is where markdown table cells and radio labels lose the mark.
		// Built by codepoint, not as a literal: an editor or tool that NFC-normalises the
		// source would fold "e" + U+0301 into the precomposed "é" and the test would pass
		// without ever exercising a combiner.
		string acute = "\u0301";
		var lines = MarkupParser.WrapMarkupLines("[#FF0000]e" + acute + "xy abcd[/]", 4);

		// Ordinal, deliberately. Assert.Contains and string.IndexOf are culture-sensitive,
		// and under ICU a lone combining mark is a zero-weight element that a culture-aware
		// search never finds - so the culture-sensitive form reports "missing" for a mark
		// that is demonstrably present.
		Assert.True(string.Concat(lines).Contains(acute, StringComparison.Ordinal));
	}
}

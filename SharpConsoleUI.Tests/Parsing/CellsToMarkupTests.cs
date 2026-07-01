// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

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
}

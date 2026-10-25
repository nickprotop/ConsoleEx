// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

/// <summary>
/// A decoration that trails the background colour used to be silently dropped: the whole
/// remainder after "on" was parsed as a single colour name, so "blue bold" matched nothing.
/// </summary>
public class MarkupParserDecorationAfterBackgroundTests
{
	private static readonly Color Fg = Color.White;
	private static readonly Color Bg = Color.Black;

	[Theory]
	[InlineData("[red on blue bold]x[/]")]
	[InlineData("[#FF0000 on #0000FF bold]x[/]")]
	[InlineData("[on blue bold]x[/]")]
	public void DecorationAfterBackground_IsApplied(string markup)
	{
		var cells = MarkupParser.Parse(markup, Fg, Bg);
		Assert.Equal(TextDecoration.Bold, cells[0].Decorations);
	}

	[Fact]
	public void BackgroundColour_StillParses_WhenADecorationTrailsIt()
	{
		var cells = MarkupParser.Parse("[red on blue bold]x[/]", Fg, Bg);
		Assert.Equal(new Color(0, 0, 255), cells[0].Background);
		Assert.Equal(new Color(255, 0, 0), cells[0].Foreground);
	}

	[Fact]
	public void MultipleDecorations_AcrossBothSides_Combine()
	{
		var cells = MarkupParser.Parse("[italic red on blue bold underline]x[/]", Fg, Bg);
		var dec = cells[0].Decorations;
		Assert.True(dec.HasFlag(TextDecoration.Italic));
		Assert.True(dec.HasFlag(TextDecoration.Bold));
		Assert.True(dec.HasFlag(TextDecoration.Underline));
	}
}

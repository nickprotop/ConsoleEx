// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

public class MarkupParserUnclosedMarkdownTests
{
	[Fact]
	public void OpenNoClose_IsUnclosed()
		=> Assert.True(MarkupParser.HasUnclosedMarkdownRegion("[markdown]# Title"));

	[Fact]
	public void OpenWithClose_IsClosed()
		=> Assert.False(MarkupParser.HasUnclosedMarkdownRegion("[markdown]# Title[/]"));

	[Fact]
	public void NoMarkdownTag_IsClosed()
		=> Assert.False(MarkupParser.HasUnclosedMarkdownRegion("plain [yellow]text[/]"));

	[Fact]
	public void EmptyString_IsClosed()
		=> Assert.False(MarkupParser.HasUnclosedMarkdownRegion(""));

	[Fact]
	public void EscapedDoubleBracket_IsNotARealTag()
		=> Assert.False(MarkupParser.HasUnclosedMarkdownRegion("see [[markdown]] literally"));

	[Fact]
	public void SecondOpenAfterClosedFirst_IsUnclosed()
		=> Assert.True(MarkupParser.HasUnclosedMarkdownRegion("[markdown]a[/] then [markdown]b"));

	[Fact]
	public void ClosedAfterOpen_LastIsClosed()
		=> Assert.False(MarkupParser.HasUnclosedMarkdownRegion("[markdown]a[/] then [markdown]b[/]"));
}

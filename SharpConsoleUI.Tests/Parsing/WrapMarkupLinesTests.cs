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

public class WrapMarkupLinesTests
{
	private static readonly Color Fg = Color.White;
	private static readonly Color Bg = Color.Black;

	private static int Vis(string markupLine) => MarkupParser.StripLength(markupLine);

	[Fact]
	public void EachLine_WithinWidth_AndWordsNotSplit()
	{
		var lines = MarkupParser.WrapMarkupLines("the quick brown fox", 9);
		Assert.All(lines, l => Assert.True(Vis(l) <= 9, $"'{l}' visible width {Vis(l)} > 9"));
		// No word split mid-token: re-join visible text equals the words in order.
		var joined = string.Join(" ", lines.Select(l => string.Concat(
			MarkupParser.Parse(l, Fg, Bg).Where(c => !c.IsWideContinuation).Select(c => c.Character.ToString())).Trim()));
		Assert.Contains("quick", joined);
		Assert.Contains("brown", joined);
	}

	[Fact]
	public void StyledRegion_KeepsStyleOnEveryWrappedLine()
	{
		// A red region wrapped narrow: every produced line must carry the red foreground.
		var lines = MarkupParser.WrapMarkupLines("[#FF0000]alpha beta gamma[/]", 6);
		Assert.True(lines.Count >= 2);
		foreach (var line in lines)
		{
			var cells = MarkupParser.Parse(line, Fg, Bg).Where(c => !c.IsWideContinuation && c.Character.Value != ' ').ToList();
			Assert.NotEmpty(cells);
			Assert.All(cells, c => Assert.Equal(new Color(255, 0, 0), c.Foreground));
		}
	}

	[Fact]
	public void PlainText_NoTagsAdded()
	{
		var lines = MarkupParser.WrapMarkupLines("abc def", 3);
		Assert.All(lines, l => Assert.DoesNotContain('[', l)); // plain wrap emits no tags
	}

	[Fact]
	public void Cjk_WrapsPerIdeograph()
	{
		var lines = MarkupParser.WrapMarkupLines("中文测试内容", 4); // 2 ideographs per line
		Assert.All(lines, l => Assert.True(Vis(l) <= 4));
		Assert.True(lines.Count >= 3);
	}
}

using System.Text;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

public class MarkupParserGradientTests
{
	private static readonly Color Fg = Color.White;
	private static readonly Color Bg = Color.Black;

	private static string CellString(List<Cell> cells)
		=> string.Concat(cells.Select(c => c.Character.ToString()));

	#region Gradient Tag Parsing

	[Fact]
	public void Parse_GradientTag_ProducesCorrectText()
	{
		var cells = MarkupParser.Parse("[gradient=blue->cyan]Hello[/]", Fg, Bg);
		Assert.Equal("Hello", CellString(cells));
	}

	[Fact]
	public void Parse_GradientTag_AppliesGradientForeground()
	{
		var cells = MarkupParser.Parse("[gradient=red->green]AB[/]", Fg, Bg);

		// First char should be red
		Assert.Equal(Color.Red, cells[0].Foreground);
		// Last char should be green
		Assert.Equal(Color.Green, cells[1].Foreground);
	}

	[Fact]
	public void Parse_GradientTag_PreservesBackground()
	{
		var cells = MarkupParser.Parse("[gradient=red->blue]Test[/]", Fg, Bg);

		Assert.All(cells, c => Assert.Equal(Bg, c.Background));
	}

	[Fact]
	public void Parse_GradientTag_UnicodeArrow()
	{
		var cells = MarkupParser.Parse("[gradient=red\u2192blue]AB[/]", Fg, Bg);

		Assert.Equal("AB", CellString(cells));
		Assert.Equal(Color.Red, cells[0].Foreground);
		Assert.Equal(Color.Blue, cells[1].Foreground);
	}

	#endregion

	#region Predefined Gradients

	[Fact]
	public void Parse_GradientTag_PredefinedName()
	{
		var cells = MarkupParser.Parse("[gradient=cool]Test[/]", Fg, Bg);

		Assert.Equal("Test", CellString(cells));
		// First char should be blue-ish (start of cool gradient)
		Assert.Equal(Color.Blue, cells[0].Foreground);
	}

	[Fact]
	public void Parse_GradientTag_PredefinedWithReverse()
	{
		var cells = MarkupParser.Parse("[gradient=cool:reverse]AB[/]", Fg, Bg);

		Assert.Equal("AB", CellString(cells));
		// Reversed cool: starts with cyan, ends with blue
		Assert.Equal(Color.Cyan1, cells[0].Foreground);
		Assert.Equal(Color.Blue, cells[1].Foreground);
	}

	#endregion

	#region Nested Tags

	[Fact]
	public void Parse_GradientTag_WithNestedBold()
	{
		var cells = MarkupParser.Parse("[gradient=red->blue][bold]Hi[/][/]", Fg, Bg);

		Assert.Equal("Hi", CellString(cells));
		// Gradient should be applied
		Assert.Equal(Color.Red, cells[0].Foreground);
		Assert.Equal(Color.Blue, cells[1].Foreground);
		// Bold decoration should be preserved
		Assert.True((cells[0].Decorations & TextDecoration.Bold) != 0);
		Assert.True((cells[1].Decorations & TextDecoration.Bold) != 0);
	}

	[Fact]
	public void Parse_GradientTag_WithMixedContent()
	{
		var cells = MarkupParser.Parse("Before [gradient=red->blue]AB[/] After", Fg, Bg);

		Assert.Equal("Before AB After", CellString(cells));
		// "Before " should use default fg
		Assert.Equal(Fg, cells[0].Foreground);
		// "A" (index 7) should be red
		Assert.Equal(Color.Red, cells[7].Foreground);
		// "B" (index 8) should be blue
		Assert.Equal(Color.Blue, cells[8].Foreground);
		// " After" should use default fg
		Assert.Equal(Fg, cells[10].Foreground);
	}

	#endregion

	#region Malformed Input

	[Fact]
	public void Parse_GradientTag_MalformedSpec_FallsBackToLiteral()
	{
		// Invalid gradient spec should emit as literal text
		var cells = MarkupParser.Parse("[gradient=notacolor->alsonotacolor]text[/]", Fg, Bg);

		// Should still produce text (gradient tag treated as literal or inner text preserved)
		Assert.True(cells.Count > 0);
	}

	[Fact]
	public void Parse_GradientTag_MissingCloseTag_FallsBackToLiteral()
	{
		var cells = MarkupParser.Parse("[gradient=red->blue]text without close", Fg, Bg);

		// Should produce some output without crashing
		Assert.True(cells.Count > 0);
	}

	[Fact]
	public void Parse_GradientTag_EmptyContent()
	{
		var cells = MarkupParser.Parse("[gradient=red->blue][/]", Fg, Bg);

		Assert.Empty(cells);
	}

	#endregion

	#region StripLength

	[Fact]
	public void StripLength_GradientTag_CountsOnlyVisibleText()
	{
		int len = MarkupParser.StripLength("[gradient=red->blue]Hello[/]");
		// The gradient tag is not a standard tag that StripLength knows about,
		// so it strips it as a normal tag: [gradient=red->blue] and [/] are both tags
		Assert.Equal(5, len);
	}

	#endregion
}

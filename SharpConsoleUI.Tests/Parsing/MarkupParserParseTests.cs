using Xunit;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using System.Text;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkupParserParseTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;

		private static string CellString(List<Cell> cells)
			=> string.Concat(cells.Select(c => c.Character.ToString()));

		#region Plain Text & Empty Input

		[Fact]
		public void Parse_NullInput_ReturnsEmptyList()
		{
			var result = MarkupParser.Parse(null!, Fg, Bg);
			Assert.Empty(result);
		}

		[Fact]
		public void Parse_EmptyString_ReturnsEmptyList()
		{
			var result = MarkupParser.Parse("", Fg, Bg);
			Assert.Empty(result);
		}

		[Fact]
		public void Parse_Whitespace_ReturnsCellsWithSpaces()
		{
			var result = MarkupParser.Parse("   ", Fg, Bg);
			Assert.Equal(3, result.Count);
			Assert.All(result, c =>
			{
				Assert.Equal(new Rune(' '), c.Character);
				Assert.Equal(Fg, c.Foreground);
				Assert.Equal(Bg, c.Background);
			});
		}

		[Fact]
		public void Parse_PlainText_CellsMatchCharactersWithDefaultColors()
		{
			var result = MarkupParser.Parse("hello", Fg, Bg);
			Assert.Equal("hello", CellString(result));
			Assert.All(result, c =>
			{
				Assert.Equal(Fg, c.Foreground);
				Assert.Equal(Bg, c.Background);
				Assert.Equal(TextDecoration.None, c.Decorations);
			});
		}

		[Fact]
		public void Parse_SingleCharacter_ReturnsOneCell()
		{
			var result = MarkupParser.Parse("x", Fg, Bg);
			Assert.Single(result);
			Assert.Equal(new Rune('x'), result[0].Character);
		}

		#endregion

		#region Escaped Brackets

		[Fact]
		public void Parse_DoubleOpenBracket_EmitsSingleOpenBracket()
		{
			var result = MarkupParser.Parse("[[", Fg, Bg);
			Assert.Single(result);
			Assert.Equal(new Rune('['), result[0].Character);
		}

		[Fact]
		public void Parse_DoubleCloseBracket_EmitsSingleCloseBracket()
		{
			var result = MarkupParser.Parse("]]", Fg, Bg);
			Assert.Single(result);
			Assert.Equal(new Rune(']'), result[0].Character);
		}

		[Fact]
		public void Parse_EscapedBracketsAroundText_LiteralBrackets()
		{
			var result = MarkupParser.Parse("[[text]]", Fg, Bg);
			Assert.Equal("[text]", CellString(result));
		}

		[Fact]
		public void Parse_EscapedEmptyBrackets_LiteralEmptyBrackets()
		{
			var result = MarkupParser.Parse("[[]]", Fg, Bg);
			Assert.Equal("[]", CellString(result));
		}

		[Fact]
		public void Parse_MultipleEscapedSequences_AllLiteral()
		{
			var result = MarkupParser.Parse("[[a]] [[b]]", Fg, Bg);
			Assert.Equal("[a] [b]", CellString(result));
		}

		[Fact]
		public void Parse_EscapedOpenThenBoldTag_LiteralBracketThenBoldText()
		{
			var result = MarkupParser.Parse("[[[bold]text[/]", Fg, Bg);
			Assert.Equal("[text", CellString(result));
			Assert.Equal(Fg, result[0].Foreground); // literal [
			Assert.Equal(TextDecoration.Bold, result[1].Decorations); // 't'
		}

		[Fact]
		public void Parse_BoldTagThenEscapedClose_BoldTextThenLiteralBracket()
		{
			var result = MarkupParser.Parse("[bold]text[/]]]", Fg, Bg);
			Assert.Equal("text]", CellString(result));
			Assert.Equal(TextDecoration.Bold, result[0].Decorations);
			Assert.Equal(TextDecoration.None, result[4].Decorations); // literal ]
		}

		#endregion

		#region Basic Tags

		[Fact]
		public void Parse_RedTag_SetsForegroundToRed()
		{
			var result = MarkupParser.Parse("[red]text[/]", Fg, Bg);
			Assert.Equal("text", CellString(result));
			Assert.All(result, c => Assert.Equal(Color.Red, c.Foreground));
			Assert.All(result, c => Assert.Equal(Bg, c.Background));
		}

		[Fact]
		public void Parse_BoldTag_SetsBoldDecoration()
		{
			var result = MarkupParser.Parse("[bold]text[/]", Fg, Bg);
			Assert.Equal("text", CellString(result));
			Assert.All(result, c => Assert.Equal(TextDecoration.Bold, c.Decorations));
		}

		[Fact]
		public void Parse_RedOnBlue_SetsFgAndBg()
		{
			var result = MarkupParser.Parse("[red on blue]text[/]", Fg, Bg);
			Assert.Equal("text", CellString(result));
			Assert.All(result, c =>
			{
				Assert.Equal(Color.Red, c.Foreground);
				Assert.Equal(Color.Blue, c.Background);
			});
		}

		[Fact]
		public void Parse_BoldRed_SetsBothDecorationAndColor()
		{
			var result = MarkupParser.Parse("[bold red]text[/]", Fg, Bg);
			Assert.Equal("text", CellString(result));
			Assert.All(result, c =>
			{
				Assert.Equal(Color.Red, c.Foreground);
				Assert.Equal(TextDecoration.Bold, c.Decorations);
			});
		}

		[Fact]
		public void Parse_BoldItalicUnderline_CombinedDecorations()
		{
			var result = MarkupParser.Parse("[bold italic underline]text[/]", Fg, Bg);
			var expected = TextDecoration.Bold | TextDecoration.Italic | TextDecoration.Underline;
			Assert.All(result, c => Assert.Equal(expected, c.Decorations));
		}

		[Fact]
		public void Parse_TagThenPlain_MixedStyles()
		{
			var result = MarkupParser.Parse("[red]R[/]plain", Fg, Bg);
			Assert.Equal("Rplain", CellString(result));
			Assert.Equal(Color.Red, result[0].Foreground);
			for (int i = 1; i < result.Count; i++)
				Assert.Equal(Fg, result[i].Foreground);
		}

		#endregion

		#region Nesting

		[Fact]
		public void Parse_NestedBoldRed_CombinesStyles()
		{
			var result = MarkupParser.Parse("[bold][red]text[/][/]", Fg, Bg);
			Assert.Equal("text", CellString(result));
			Assert.All(result, c =>
			{
				Assert.Equal(Color.Red, c.Foreground);
				Assert.Equal(TextDecoration.Bold, c.Decorations);
			});
		}

		[Fact]
		public void Parse_NestedColors_InnerOverridesOuterThenRestores()
		{
			var result = MarkupParser.Parse("[red]a[blue]b[/]c[/]", Fg, Bg);
			Assert.Equal("abc", CellString(result));
			Assert.Equal(Color.Red, result[0].Foreground);  // a
			Assert.Equal(Color.Blue, result[1].Foreground);  // b
			Assert.Equal(Color.Red, result[2].Foreground);   // c (restored)
		}

		[Fact]
		public void Parse_DeepNesting_AllProperlyResolved()
		{
			var result = MarkupParser.Parse("[bold][italic][underline][dim][red]x[/][/][/][/][/]", Fg, Bg);
			Assert.Single(result);
			var expected = TextDecoration.Bold | TextDecoration.Italic |
						   TextDecoration.Underline | TextDecoration.Dim;
			Assert.True(result[0].Decorations.HasFlag(expected));
			Assert.Equal(Color.Red, result[0].Foreground);
		}

		[Fact]
		public void Parse_CloseTagOnEmptyStack_SafeNoOp()
		{
			var result = MarkupParser.Parse("[/]text", Fg, Bg);
			Assert.Equal("text", CellString(result));
			Assert.All(result, c => Assert.Equal(Fg, c.Foreground));
		}

		#endregion

		#region Invalid/Malformed

		[Fact]
		public void Parse_OpenBracketAtEnd_EmitsLiteral()
		{
			var result = MarkupParser.Parse("text[", Fg, Bg);
			Assert.Equal("text[", CellString(result));
		}

		[Fact]
		public void Parse_EmptyTag_EmitsLiteralBrackets()
		{
			var result = MarkupParser.Parse("[]", Fg, Bg);
			Assert.Equal("[]", CellString(result));
		}

		[Fact]
		public void Parse_UnknownTag_EmittedAsLiteral()
		{
			var result = MarkupParser.Parse("[unknownthing]text[/]", Fg, Bg);
			// Unknown tag emits as literal: [unknownthing]
			// Then "text" as plain, then [/] closes nothing (empty stack)
			Assert.Contains('[', CellString(result));
			Assert.Contains("unknownthing", CellString(result));
		}

		[Fact]
		public void Parse_CloseWithNothingOpen_SilentlyIgnored()
		{
			var result = MarkupParser.Parse("a[/]b", Fg, Bg);
			Assert.Equal("ab", CellString(result));
		}

		#endregion

		#region Color Formats in Tags

		[Fact]
		public void Parse_NamedColor_Red()
		{
			var result = MarkupParser.Parse("[red]x[/]", Fg, Bg);
			Assert.Equal(Color.Red, result[0].Foreground);
		}

		[Fact]
		public void Parse_NamedColor_SteelBlue()
		{
			var result = MarkupParser.Parse("[steelblue]x[/]", Fg, Bg);
			Assert.Equal(Color.SteelBlue, result[0].Foreground);
		}

		[Fact]
		public void Parse_NamedColor_Grey50()
		{
			var result = MarkupParser.Parse("[grey50]x[/]", Fg, Bg);
			Assert.Equal(Color.Grey50, result[0].Foreground);
		}

		[Fact]
		public void Parse_HexColor_6Digit()
		{
			var result = MarkupParser.Parse("[#FF0000]x[/]", Fg, Bg);
			Assert.Equal(new Color(255, 0, 0), result[0].Foreground);
		}

		[Fact]
		public void Parse_HexColor_3Digit()
		{
			var result = MarkupParser.Parse("[#F00]x[/]", Fg, Bg);
			Assert.Equal(new Color(255, 0, 0), result[0].Foreground);
		}

		[Fact]
		public void Parse_HexColor_Lowercase()
		{
			var result = MarkupParser.Parse("[#ff00ff]x[/]", Fg, Bg);
			Assert.Equal(new Color(255, 0, 255), result[0].Foreground);
		}

		[Fact]
		public void Parse_RgbColor()
		{
			var result = MarkupParser.Parse("[rgb(255,0,0)]x[/]", Fg, Bg);
			Assert.Equal(new Color(255, 0, 0), result[0].Foreground);
		}

		[Fact]
		public void Parse_OnKeyword_SetsBackgroundOnly()
		{
			var result = MarkupParser.Parse("[on blue]x[/]", Fg, Bg);
			Assert.Equal(Fg, result[0].Foreground);
			Assert.Equal(Color.Blue, result[0].Background);
		}

		[Fact]
		public void Parse_InvalidRgb_EmittedAsLiteral()
		{
			var result = MarkupParser.Parse("[rgb(999,0,0)]x[/]", Fg, Bg);
			// byte.TryParse fails for 999, so tag is invalid → literal
			Assert.Contains("rgb", CellString(result));
		}

		[Fact]
		public void Parse_InvalidHex_EmittedAsLiteral()
		{
			var result = MarkupParser.Parse("[#GGG]x[/]", Fg, Bg);
			Assert.Contains("#GGG", CellString(result));
		}

		#endregion

		#region Decorations

		[Theory]
		[InlineData("bold", TextDecoration.Bold)]
		[InlineData("italic", TextDecoration.Italic)]
		[InlineData("underline", TextDecoration.Underline)]
		[InlineData("dim", TextDecoration.Dim)]
		[InlineData("strikethrough", TextDecoration.Strikethrough)]
		[InlineData("strike", TextDecoration.Strikethrough)]
		[InlineData("invert", TextDecoration.Invert)]
		[InlineData("reverse", TextDecoration.Invert)]
		[InlineData("blink", TextDecoration.Blink)]
		[InlineData("slowblink", TextDecoration.Blink)]
		[InlineData("rapidblink", TextDecoration.Blink)]
		public void Parse_Decoration_MapsCorrectly(string tag, TextDecoration expected)
		{
			var result = MarkupParser.Parse($"[{tag}]x[/]", Fg, Bg);
			Assert.Equal(expected, result[0].Decorations);
		}

		[Fact]
		public void Parse_CombinedDecorations_BoldItalic()
		{
			var result = MarkupParser.Parse("[bold italic]x[/]", Fg, Bg);
			Assert.Equal(TextDecoration.Bold | TextDecoration.Italic, result[0].Decorations);
		}

		[Theory]
		[InlineData("BOLD")]
		[InlineData("Bold")]
		[InlineData("bOlD")]
		public void Parse_CaseInsensitiveDecorations(string tag)
		{
			var result = MarkupParser.Parse($"[{tag}]x[/]", Fg, Bg);
			Assert.Equal(TextDecoration.Bold, result[0].Decorations);
		}

		[Fact]
		public void Parse_DecorationWithColor_OrderInvariant()
		{
			var result1 = MarkupParser.Parse("[bold red]x[/]", Fg, Bg);
			var result2 = MarkupParser.Parse("[red bold]x[/]", Fg, Bg);
			Assert.Equal(result1[0].Foreground, result2[0].Foreground);
			Assert.Equal(result1[0].Decorations, result2[0].Decorations);
		}

		#endregion

		#region Style Stack

		[Fact]
		public void Parse_CloseTag_RestoresPreviousStyle()
		{
			var result = MarkupParser.Parse("[red]a[blue]b[/]c[/]d", Fg, Bg);
			Assert.Equal("abcd", CellString(result));
			Assert.Equal(Color.Red, result[0].Foreground);   // a
			Assert.Equal(Color.Blue, result[1].Foreground);  // b
			Assert.Equal(Color.Red, result[2].Foreground);   // c (restored to red)
			Assert.Equal(Fg, result[3].Foreground);          // d (restored to default)
		}

		[Fact]
		public void Parse_DecorationsAccumulateAcrossNesting()
		{
			var result = MarkupParser.Parse("[bold][italic]x[/][/]", Fg, Bg);
			var expected = TextDecoration.Bold | TextDecoration.Italic;
			Assert.Equal(expected, result[0].Decorations);
		}

		[Fact]
		public void Parse_BoldDimNested_BoldStillActiveAfterDimCloses()
		{
			var result = MarkupParser.Parse("[bold]a[dim]b[/]c[/]", Fg, Bg);
			Assert.Equal("abc", CellString(result));
			Assert.Equal(TextDecoration.Bold, result[0].Decorations);
			Assert.Equal(TextDecoration.Bold | TextDecoration.Dim, result[1].Decorations);
			Assert.Equal(TextDecoration.Bold, result[2].Decorations);
		}

		#endregion

		#region Unicode

		[Fact]
		public void Parse_PlainTextWithEmoji_CellsContainEmoji()
		{
			var result = MarkupParser.Parse("hi!", Fg, Bg);
			Assert.Equal(3, result.Count);
		}

		[Fact]
		public void Parse_TagsAroundUnicode_StyleApplied()
		{
			var result = MarkupParser.Parse("[red]abc[/]", Fg, Bg);
			Assert.Equal("abc", CellString(result));
			Assert.All(result, c => Assert.Equal(Color.Red, c.Foreground));
		}

		#endregion
	}
}

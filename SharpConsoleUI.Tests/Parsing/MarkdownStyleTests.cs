using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Parsing
{
	public class MarkdownStyleTests
	{
		[Fact]
		public void Default_HasExpectedDefaults()
		{
			var s = MarkdownStyle.Default;
			Assert.Equal("•", s.BulletGlyph);
			Assert.Equal(2, s.ListIndent);
			Assert.Equal("│", s.QuoteGlyph);
			// H1–H3 are tinted by default; H4–H6 stay colorless (weight only).
			Assert.NotNull(s.H1Color);
			Assert.NotNull(s.H2Color);
			Assert.NotNull(s.H3Color);
			Assert.Null(s.H4Color);
			Assert.Null(s.H5Color);
			Assert.Null(s.H6Color);
		}

		[Fact]
		public void With_OverridesOnlyTargetedField()
		{
			var s = MarkdownStyle.Default with { LinkColor = new Color(1, 2, 3) };
			Assert.Equal(new Color(1, 2, 3), s.LinkColor);
			Assert.Equal(MarkdownStyle.Default.CodeBackground, s.CodeBackground);
		}

		[Fact]
		public void CodeHighlighters_DefaultsToEmpty()
		{
			Assert.NotNull(MarkdownStyle.Default.CodeHighlighters);
			Assert.Empty(MarkdownStyle.Default.CodeHighlighters);
		}

		[Fact]
		public void CodeHighlighters_CanBeSetViaWith()
		{
			var hl = new SharpConsoleUI.Highlighting.CSharpSyntaxHighlighter();
			var s = MarkdownStyle.Default with
			{
				CodeHighlighters = new Dictionary<string, SharpConsoleUI.Controls.ISyntaxHighlighter> { ["csharp"] = hl }
			};
			Assert.True(s.CodeHighlighters.ContainsKey("csharp"));
		}
	}
}

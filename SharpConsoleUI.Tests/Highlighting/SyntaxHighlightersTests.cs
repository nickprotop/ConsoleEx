using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Highlighting;
using Xunit;

namespace SharpConsoleUI.Tests.Highlighting
{
	public class SyntaxHighlightersTests
	{
		[Fact]
		public void For_CSharp_And_Alias_ResolveSameType_CaseInsensitive()
		{
			var a = SyntaxHighlighters.For("csharp");
			var b = SyntaxHighlighters.For("cs");
			var c = SyntaxHighlighters.For("CSHARP");
			Assert.NotNull(a);
			Assert.NotNull(b);
			Assert.NotNull(c);
			Assert.IsType<CSharpSyntaxHighlighter>(a);
			Assert.IsType<CSharpSyntaxHighlighter>(b);
		}

		[Fact]
		public void For_Unknown_ReturnsNull()
		{
			Assert.Null(SyntaxHighlighters.For("rust"));
			Assert.Null(SyntaxHighlighters.For(null));
			Assert.Null(SyntaxHighlighters.For(""));
		}

		[Fact]
		public void Register_AddsCustomLanguage_BuiltInsRemain()
		{
			var custom = new CSharpSyntaxHighlighter();
			SyntaxHighlighters.Register("toml-test", custom);
			Assert.Same(custom, SyntaxHighlighters.For("toml-test"));
			Assert.NotNull(SyntaxHighlighters.For("csharp"));
			Assert.True(SyntaxHighlighters.Has("toml-test"));
		}
	}
}

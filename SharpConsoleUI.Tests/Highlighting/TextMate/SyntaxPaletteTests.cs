using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Highlighting.TextMate
{
	public class SyntaxPaletteTests
	{
		[Fact]
		public void DeriveFrom_PopulatesEveryRole()
		{
			var palette = SyntaxPalette.DeriveFrom(new ModernGrayTheme());

			Assert.NotNull(palette.Default);
			Assert.NotNull(palette.Keyword);
			Assert.NotNull(palette.Operator);
			Assert.NotNull(palette.String);
			Assert.NotNull(palette.Number);
			Assert.NotNull(palette.Comment);
			Assert.NotNull(palette.Type);
			Assert.NotNull(palette.Function);
			Assert.NotNull(palette.Variable);
			Assert.NotNull(palette.Constant);
			Assert.NotNull(palette.Tag);
			Assert.NotNull(palette.Attribute);
			Assert.NotNull(palette.Punctuation);
			Assert.NotNull(palette.Invalid);
		}

		[Fact]
		public void DeriveFrom_RolesAReaderMustDistinguish_AreDistinct()
		{
			var palette = SyntaxPalette.DeriveFrom(new ModernGrayTheme());

			Assert.NotEqual(palette.Keyword, palette.String);
			Assert.NotEqual(palette.Keyword, palette.Comment);
			Assert.NotEqual(palette.String, palette.Comment);
			Assert.NotEqual(palette.Number, palette.Comment);
		}

		[Fact]
		public void ITheme_SyntaxColors_DefaultsToNull()
		{
			ITheme theme = new ModernGrayTheme();
			Assert.Null(theme.SyntaxColors);
		}

		[Fact]
		public void DeriveFrom_ColoursAreReadableAgainstTheThemeBackground()
		{
			var theme = new ModernGrayTheme();
			var palette = SyntaxPalette.DeriveFrom(theme);
			Color bg = theme.WindowBackgroundColor;

			foreach (Color? role in new[] { palette.Keyword, palette.String, palette.Comment, palette.Number })
			{
				Assert.NotNull(role);
				double gap = System.Math.Abs(role!.Value.Luminance() - bg.Luminance());
				Assert.True(gap > 0, "derived colour collapsed into the background");
			}
		}
	}
}

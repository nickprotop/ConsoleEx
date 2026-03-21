using System.Drawing;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers
{
	public class ColorResolverTests
	{
		// --- Coalesce helper ---

		[Fact]
		public void Coalesce_Null_ReturnsNull()
		{
			Assert.Null(ColorResolver.Coalesce(null));
		}

		[Fact]
		public void Coalesce_Default_ReturnsNull()
		{
			Assert.Null(ColorResolver.Coalesce(Color.Default));
		}

		[Fact]
		public void Coalesce_Transparent_ReturnsTransparent()
		{
			Assert.Equal(Color.Transparent, ColorResolver.Coalesce(Color.Transparent));
		}

		[Fact]
		public void Coalesce_OpaqueColor_ReturnsThatColor()
		{
			var red = new Color(255, 0, 0);
			Assert.Equal(red, ColorResolver.Coalesce(red));
		}

		// --- ResolveBackground (no theme slot, no container fallback) ---

		[Fact]
		public void ResolveBackground_NoExplicit_NoContainer_ReturnsTransparent()
		{
			var result = ColorResolver.ResolveBackground(null, null);
			Assert.Equal(Color.Transparent, result);
		}

		[Fact]
		public void ResolveBackground_ExplicitDefault_NoContainer_ReturnsTransparent()
		{
			var result = ColorResolver.ResolveBackground(Color.Default, null);
			Assert.Equal(Color.Transparent, result);
		}

		[Fact]
		public void ResolveBackground_ExplicitColor_ReturnsExplicit()
		{
			var blue = new Color(0, 0, 255);
			var result = ColorResolver.ResolveBackground(blue, null);
			Assert.Equal(blue, result);
		}

		// --- ResolveButtonBackground — no container.BackgroundColor fallback ---

		[Fact]
		public void ResolveButtonBackground_NoExplicit_NoContainer_ReturnsTransparent()
		{
			var result = ColorResolver.ResolveButtonBackground(null, null);
			Assert.Equal(Color.Transparent, result);
		}

		[Fact]
		public void ResolveButtonBackground_ExplicitDefault_NoContainer_ReturnsTransparent()
		{
			var result = ColorResolver.ResolveButtonBackground(Color.Default, null);
			Assert.Equal(Color.Transparent, result);
		}

		[Fact]
		public void ResolveButtonBackground_ExplicitColor_ReturnsExplicit()
		{
			var red = new Color(200, 0, 0);
			var result = ColorResolver.ResolveButtonBackground(red, null);
			Assert.Equal(red, result);
		}

		// --- New methods: ResolveCheckboxBackground (×3) ---

		[Fact]
		public void ResolveCheckboxBackground_NoExplicit_NoContainer_ReturnsTransparent()
		{
			Assert.Equal(Color.Transparent, ColorResolver.ResolveCheckboxBackground(null, null));
		}

		[Fact]
		public void ResolveCheckboxFocusedBackground_NoExplicit_NoContainer_ReturnsTransparent()
		{
			Assert.Equal(Color.Transparent, ColorResolver.ResolveCheckboxFocusedBackground(null, null));
		}

		[Fact]
		public void ResolveCheckboxDisabledBackground_NoExplicit_NoContainer_ReturnsTransparent()
		{
			Assert.Equal(Color.Transparent, ColorResolver.ResolveCheckboxDisabledBackground(null, null));
		}

		// --- ResolveListBackground ---

		[Fact]
		public void ResolveListBackground_NoExplicit_NoContainer_ReturnsTransparent()
		{
			Assert.Equal(Color.Transparent, ColorResolver.ResolveListBackground(null, null));
		}

		// --- ResolveTreeBackground (×3) ---

		[Fact]
		public void ResolveTreeBackground_NoExplicit_NoContainer_ReturnsTransparent()
		{
			Assert.Equal(Color.Transparent, ColorResolver.ResolveTreeBackground(null, null));
		}

		[Fact]
		public void ResolveTreeSelectionBackground_NoExplicit_NoContainer_ReturnsTransparent()
		{
			Assert.Equal(Color.Transparent, ColorResolver.ResolveTreeSelectionBackground(null, null));
		}

		[Fact]
		public void ResolveTreeUnfocusedSelectionBackground_NoExplicit_NoContainer_ReturnsTransparent()
		{
			Assert.Equal(Color.Transparent, ColorResolver.ResolveTreeUnfocusedSelectionBackground(null, null));
		}
	}
}

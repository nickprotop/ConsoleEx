// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using SharpConsoleUI.Html;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	public class HtmlStyleResolverTests
	{
		private static IElement ParseAndQuery(string html, string selector)
		{
			var config = AngleSharp.Configuration.Default.WithCss();
			var context = BrowsingContext.New(config);
			var parser = context.GetService<IHtmlParser>()!;
			var doc = parser.ParseDocument(html);
			return doc.QuerySelector(selector)!;
		}

		[Fact]
		public void BoldElement_ReturnsBoldDecoration()
		{
			var el = ParseAndQuery("<html><body><b id='t'>bold</b></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.True(style.Decorations.HasFlag(TextDecoration.Bold));
		}

		[Fact]
		public void ItalicElement_ReturnsItalicDecoration()
		{
			var el = ParseAndQuery("<html><body><em id='t'>italic</em></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.True(style.Decorations.HasFlag(TextDecoration.Italic));
		}

		[Fact]
		public void UnderlineElement_ReturnsUnderlineDecoration()
		{
			var el = ParseAndQuery("<html><body><u id='t'>underline</u></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.True(style.Decorations.HasFlag(TextDecoration.Underline));
		}

		[Fact]
		public void StrikethroughElement_ReturnsStrikethroughDecoration()
		{
			var el = ParseAndQuery("<html><body><del id='t'>deleted</del></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.True(style.Decorations.HasFlag(TextDecoration.Strikethrough));
		}

		[Fact]
		public void InlineColor_SetsForeground()
		{
			var el = ParseAndQuery("<html><body><span id='t' style='color: rgb(255, 0, 0)'>red</span></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.Equal(new Color(255, 0, 0), style.Foreground);
		}

		[Fact]
		public void InlineBackgroundColor_SetsBackground()
		{
			var el = ParseAndQuery("<html><body><span id='t' style='background-color: rgb(0, 128, 0)'>green bg</span></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.Equal(new Color(0, 128, 0), style.Background);
		}

		[Fact]
		public void CssClassBold_ReturnsBoldDecoration()
		{
			var html = "<html><head><style>.bold { font-weight: bold; }</style></head><body><span id='t' class='bold'>bold</span></body></html>";
			var el = ParseAndQuery(html, "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.True(style.Decorations.HasFlag(TextDecoration.Bold));
		}

		[Fact]
		public void DisplayNone_SetsIsHidden()
		{
			var el = ParseAndQuery("<html><body><span id='t' style='display: none'>hidden</span></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.True(style.IsHidden);
		}

		[Fact]
		public void TextAlignCenter_SetsCenterAlignment()
		{
			var el = ParseAndQuery("<html><body><div id='t' style='text-align: center'>centered</div></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.Equal(TextAlignment.Center, style.Alignment);
		}

		[Fact]
		public void WhiteSpacePre_SetsPreserveWhitespace()
		{
			var el = ParseAndQuery("<html><body><pre id='t' style='white-space: pre'>code</pre></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.True(style.PreserveWhitespace);
		}

		[Fact]
		public void NestedBoldItalic_StacksDecorations()
		{
			var el = ParseAndQuery("<html><body><b><em id='t'>bold italic</em></b></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.True(style.Decorations.HasFlag(TextDecoration.Bold));
			Assert.True(style.Decorations.HasFlag(TextDecoration.Italic));
		}

		[Fact]
		public void MarginTopBottom_SetsMarginValues()
		{
			var el = ParseAndQuery("<html><body><div id='t' style='margin-top: 32px; margin-bottom: 16px'>spaced</div></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.Equal(2, style.MarginTop);   // 32/16 = 2
			Assert.Equal(1, style.MarginBottom); // 16/16 = 1
		}

		[Fact]
		public void Padding_SetsPaddingValues()
		{
			var el = ParseAndQuery("<html><body><div id='t' style='padding-left: 16px; padding-right: 24px'>padded</div></body></html>", "#t");
			var style = HtmlStyleResolver.Resolve(el, Color.White, Color.Black);
			Assert.Equal(2, style.PaddingLeft);  // 16/8 = 2
			Assert.Equal(3, style.PaddingRight); // 24/8 = 3
		}
	}
}

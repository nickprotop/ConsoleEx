// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Html;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	public class HtmlEdgeCaseTests
	{
		// ── Helpers ──────────────────────────────────────────────────────

		private static LayoutLine[] Flow(string bodyContent, int maxWidth = 80)
		{
			var doc = HtmlTestHelpers.ParseHtml($"<html><body>{bodyContent}</body></html>");
			return HtmlBlockFlow.FlowBlocks(
				doc.Body!,
				maxWidth,
				Color.White,
				Color.Black);
		}

		private static LayoutResult EngineLayout(string html, int maxWidth = 80,
			string? baseUrl = null, bool showImages = false)
		{
			var engine = new HtmlLayoutEngine();
			return engine.Layout(html, maxWidth, Color.White, Color.Black,
				blockSpacing: 1, baseUrl: baseUrl, showImages: showImages);
		}

		// ── Unicode & Surrogate handling ────────────────────────────────

		[Fact]
		public void CJK_Characters_RenderCorrectly()
		{
			var lines = Flow("<p>你好世界</p>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("你好世界", text);
		}

		[Fact]
		public void CJK_Characters_MeasuredAsDoubleWidth()
		{
			// 80 columns should fit ~40 CJK chars (each is 2 cols wide)
			// 50 CJK chars = 100 columns, should wrap within 80
			var cjk = new string('中', 50);
			var lines = Flow($"<p>{cjk}</p>", 80);
			Assert.True(lines.Length > 1, "50 CJK chars should wrap in 80 columns");
			foreach (var line in lines)
			{
				// Count visual width (cells including wide continuations)
				Assert.True(line.Cells.Length <= 80, $"Line visual width {line.Cells.Length} exceeded 80");
			}
		}

		[Fact]
		public void Emoji_DoNotCrash()
		{
			var lines = Flow("<p>Hello 🎉🌍💻 World</p>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Hello", text);
			Assert.Contains("World", text);
		}

		[Fact]
		public void MixedLatinAndCJK_WrapsCorrectly()
		{
			var lines = Flow("<p>Hello 你好世界 World 这是测试</p>", 20);
			Assert.True(lines.Length >= 1, "Should produce at least one line");
			foreach (var line in lines)
				Assert.True(line.Cells.Length <= 20, $"Line exceeded maxWidth: {line.Cells.Length}");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Hello", text);
		}

		[Fact]
		public void SurrogatePairs_InPreBlock()
		{
			// Emoji are surrogate pairs in UTF-16
			var lines = Flow("<pre>Code: 🎉 done</pre>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Code:", text);
			Assert.Contains("done", text);
		}

		// ── CSS edge cases ──────────────────────────────────────────────

		[Fact]
		public void ComplexMediaQueries_DoNotCrash()
		{
			var html = @"<html><head><style>
				@media screen and (max-width: 768px) { .nav { display: none; } }
				@media (prefers-color-scheme: dark) { body { color: white; } }
				.content { font-size: 16px; }
			</style></head><body><p class='content'>Visible text</p></body></html>";
			var result = EngineLayout(html);
			var text = HtmlTestHelpers.LinesToText(result.Lines);
			Assert.Contains("Visible text", text);
		}

		[Fact]
		public void MalformedCSS_DoNotCrash()
		{
			var lines = Flow("<span style=\"color: \">text</span>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("text", text);
		}

		[Fact]
		public void DeeplyCssNested_DoNotCrash()
		{
			// Build deeply nested divs with CSS classes
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < 30; i++)
				sb.Append($"<div class='level{i}' style='margin-left: {i}px'>");
			sb.Append("Deep content");
			for (int i = 0; i < 30; i++)
				sb.Append("</div>");
			var lines = Flow(sb.ToString());
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Deep content", text);
		}

		[Fact]
		public void InlineStyleEmpty_DoNotCrash()
		{
			var lines = Flow("<div style=\"\">text</div>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("text", text);
		}

		[Fact]
		public void CSSWithCalc_DoNotCrash()
		{
			var lines = Flow("<div style=\"width: calc(100% - 20px)\">text</div>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("text", text);
		}

		// ── Style leaking prevention ────────────────────────────────────

		[Fact]
		public void StyleTag_InBody_ContentNotRendered()
		{
			var lines = Flow("<style>.x { color:red }</style><p>text</p>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.DoesNotContain("color:red", text);
			Assert.DoesNotContain(".x", text);
			Assert.Contains("text", text);
		}

		[Fact]
		public void ScriptTag_ContentNotRendered()
		{
			var lines = Flow("<script>var x=1;</script><p>text</p>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.DoesNotContain("var x", text);
			Assert.DoesNotContain("x=1", text);
			Assert.Contains("text", text);
		}

		[Fact]
		public void StyleTag_InsideTable_ContentNotRendered()
		{
			var lines = Flow("<table><tr><td><style>.x{}</style>data</td></tr></table>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.DoesNotContain(".x{}", text);
			Assert.Contains("data", text);
		}

		[Fact]
		public void NoscriptTag_ContentNotRendered()
		{
			var lines = Flow("<noscript>Enable JS</noscript><p>text</p>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.DoesNotContain("Enable JS", text);
			Assert.Contains("text", text);
		}

		// ── Protocol-relative URLs ──────────────────────────────────────

		[Fact]
		public void ProtocolRelativeUrl_ImageLoads()
		{
			// Protocol-relative URLs (//example.com/...) should be normalized to https://
			var html = "<html><body><img src=\"//example.com/img.png\" alt=\"photo\"></body></html>";
			var result = EngineLayout(html, showImages: true);
			var text = HtmlTestHelpers.LinesToText(result.Lines);
			// Should show alt text (can't actually load from example.com) but should not crash
			Assert.Contains("photo", text);
		}

		[Fact]
		public void ProtocolRelativeUrl_LinkResolved()
		{
			var html = "<html><body><a href=\"//example.com/page\">link</a></body></html>";
			var result = EngineLayout(html, baseUrl: "https://base.com");
			var links = HtmlTestHelpers.GetAllLinks(result.Lines);
			// The protocol-relative URL starts with // which should be resolved relative to baseUrl
			Assert.True(links.Count > 0, "Should find at least one link");
			// The href starts with // which Uri.TryCreate with base will resolve
			var url = links[0].Url;
			Assert.Contains("example.com", url);
		}

		// ── Image handling ──────────────────────────────────────────────

		[Fact]
		public void InlineDataUri_Base64PNG()
		{
			var dataUri = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
			var html = $"<html><body><img src=\"{dataUri}\" alt=\"pixel\"></body></html>";
			var result = EngineLayout(html, showImages: true);
			Assert.True(result.Lines.Length > 0, "Should produce output lines");
			// Check that at least one cell contains the half-block char or image content
			var text = HtmlTestHelpers.LinesToText(result.Lines);
			// A 1x1 image should render at minimum 1 row; it won't contain "pixel" since image loaded successfully
			Assert.True(result.Lines.Length >= 1);
		}

		[Fact]
		public void InlineDataUri_InvalidBase64()
		{
			var html = "<html><body><img src=\"data:image/png;base64,INVALID!!!\" alt=\"broken\"></body></html>";
			var result = EngineLayout(html, showImages: true);
			var text = HtmlTestHelpers.LinesToText(result.Lines);
			// Should fall back to alt text with error
			Assert.Contains("broken", text);
		}

		[Fact]
		public void ImageInsideLink_Detected()
		{
			var dataUri = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
			var html = $"<html><body><a href=\"https://example.com\"><img src=\"{dataUri}\" alt=\"pic\"></a></body></html>";
			var result = EngineLayout(html, showImages: true);
			Assert.True(result.Lines.Length > 0, "Should produce output");
			// The image should be rendered (not just alt text "[pic]")
			// Since the image is valid, we should get actual image cells
		}

		[Fact]
		public void ImageAlt_ShowsErrorOnFailure()
		{
			// SVG data URI will fail in ImageSharp decoder
			var html = "<html><body><img src=\"data:image/svg+xml,<svg></svg>\" alt=\"icon\"></body></html>";
			var result = EngineLayout(html, showImages: true);
			var text = HtmlTestHelpers.LinesToText(result.Lines);
			// Should show alt text; SVG can't be decoded by ImageSharp
			Assert.Contains("icon", text);
		}

		[Fact]
		public void SVGImage_FallsBackToAltText()
		{
			var html = "<html><body><img src=\"data:image/svg+xml,<svg></svg>\" alt=\"vector\"></body></html>";
			var result = EngineLayout(html, showImages: true);
			var text = HtmlTestHelpers.LinesToText(result.Lines);
			Assert.Contains("vector", text);
		}

		// ── Table edge cases ────────────────────────────────────────────

		[Fact]
		public void TableWithStyleBlock_NoLeak()
		{
			var html = @"<table><tr><td><style>.wiki { color: blue; }</style>Cell data</td></tr></table>";
			var lines = Flow(html);
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.DoesNotContain(".wiki", text);
			Assert.DoesNotContain("color: blue", text);
			Assert.Contains("Cell data", text);
		}

		[Fact]
		public void EmptyTableCells_RenderWithoutCrash()
		{
			var lines = Flow("<table><tr><td></td><td></td></tr></table>");
			Assert.NotNull(lines);
			// Should not throw
		}

		[Fact]
		public void TableWithNestedTags_ExtractsTextOnly()
		{
			var lines = Flow("<table><tr><td><b>bold</b> text</td></tr></table>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("bold", text);
			Assert.Contains("text", text);
		}

		[Fact]
		public void SingleColumnTable_Renders()
		{
			var lines = Flow("<table><tr><td>only column</td></tr></table>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("only column", text);
		}

		// ── Real-world HTML patterns ────────────────────────────────────

		[Fact]
		public void WikipediaStyleHTML_DoesNotCrash()
		{
			var html = @"
				<html><head><style>
					.infobox { border: 1px solid #aaa; }
					.navbox { background: #fdfdfd; }
					@media print { .noprint { display: none; } }
				</style></head><body>
				<nav class='noprint'><ul><li><a href='/wiki/Main_Page'>Main page</a></li></ul></nav>
				<article>
					<h1>Test Article</h1>
					<div class='infobox'>
						<table><tr><td>Born</td><td>1990</td></tr></table>
					</div>
					<p>This is the <b>first paragraph</b> with <a href='/wiki/Link'>a link</a>.</p>
					<figure>
						<img alt='Photo' src='photo.jpg'>
						<figcaption>A caption</figcaption>
					</figure>
					<h2>References</h2>
					<ol><li>Ref 1</li><li>Ref 2</li></ol>
					<div class='navbox'><p>Navigation</p></div>
				</article>
				</body></html>";
			var result = EngineLayout(html);
			var text = HtmlTestHelpers.LinesToText(result.Lines);
			Assert.Contains("Test Article", text);
			Assert.Contains("first paragraph", text);
		}

		[Fact]
		public void HTMLWithManyNestedDivs_DoesNotCrash()
		{
			var sb = new System.Text.StringBuilder();
			sb.Append("<html><body>");
			for (int i = 0; i < 50; i++)
				sb.Append("<div>");
			sb.Append("Deep nested content");
			for (int i = 0; i < 50; i++)
				sb.Append("</div>");
			sb.Append("</body></html>");
			var result = EngineLayout(sb.ToString());
			var text = HtmlTestHelpers.LinesToText(result.Lines);
			Assert.Contains("Deep nested content", text);
		}

		[Fact]
		public void HTMLWithHiddenElements_SkipsThem()
		{
			var lines = Flow(
				"<p>Visible</p>" +
				"<p style='display:none'>Hidden by display</p>" +
				"<p>Also visible</p>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("Visible", text);
			Assert.Contains("Also visible", text);
			Assert.DoesNotContain("Hidden by display", text);
		}

		[Fact]
		public void HTMLWithBrTags_CreatesLineBreaks()
		{
			var lines = Flow("line1<br>line2<br><br>line4");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("line1", text);
			Assert.Contains("line2", text);
			Assert.Contains("line4", text);
			// Should have multiple lines
			Assert.True(lines.Length > 1, "br tags should create line breaks");
		}

		[Fact]
		public void HTMLWithEntities_DecodesCorrectly()
		{
			var lines = Flow("<p>&amp; &lt; &gt; &quot; &nbsp;</p>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("&", text);
			Assert.Contains("<", text);
			Assert.Contains(">", text);
			Assert.Contains("\"", text);
		}

		// ── Layout engine edge cases ────────────────────────────────────

		[Fact]
		public void ZeroWidthLayout_DoesNotCrash()
		{
			var result = EngineLayout("<p>Hello</p>", maxWidth: 0);
			// Should not throw; inline flow clamps to 1
			Assert.NotNull(result.Lines);
		}

		[Fact]
		public void VeryNarrowLayout_StillRenders()
		{
			var result = EngineLayout("<p>Hello</p>", maxWidth: 1);
			Assert.True(result.Lines.Length > 0, "Should render something even at width 1");
			var text = HtmlTestHelpers.LinesToText(result.Lines);
			Assert.Contains("H", text);
		}

		[Fact]
		public void EmptyHTML_ReturnsEmptyResult()
		{
			var result = EngineLayout("");
			Assert.True(result.Lines.Length == 0 || result.TotalHeight == 0,
				"Empty HTML should produce empty or zero-height result");
		}

		[Fact]
		public void HTMLWithOnlyWhitespace_ReturnsEmptyResult()
		{
			var result = EngineLayout("   \n\t  ");
			// Whitespace-only body should produce no meaningful content
			if (result.Lines.Length > 0)
			{
				var text = HtmlTestHelpers.LinesToText(result.Lines).Trim();
				Assert.True(string.IsNullOrWhiteSpace(text),
					$"Whitespace-only HTML should not produce visible text, got: '{text}'");
			}
		}

		[Fact]
		public void VeryLongSingleWord_BreaksCorrectly()
		{
			var longWord = new string('A', 200);
			var result = EngineLayout($"<p>{longWord}</p>", maxWidth: 40);
			Assert.True(result.Lines.Length >= 5, "200-char word in 40-col width should produce at least 5 lines");
			foreach (var line in result.Lines)
			{
				// Each line should not exceed maxWidth
				Assert.True(line.Cells.Length <= 40,
					$"Line has {line.Cells.Length} cells, expected <= 40");
			}
		}

		// ── Inline flow edge cases ──────────────────────────────────────

		[Fact]
		public void ConsecutiveSpaces_CollapsedToOne()
		{
			var doc = HtmlTestHelpers.ParseHtml("<html><body>hello    world</body></html>");
			var lines = HtmlInlineFlow.FlowInline(
				doc.Body!.ChildNodes, 80, Color.White, Color.Black);
			var text = HtmlTestHelpers.CellsToText(lines[0].Cells);
			Assert.Equal("hello world", text);
		}

		[Fact]
		public void LeadingTrailingSpaces_Trimmed()
		{
			var doc = HtmlTestHelpers.ParseHtml("<html><body>  hello  </body></html>");
			var lines = HtmlInlineFlow.FlowInline(
				doc.Body!.ChildNodes, 80, Color.White, Color.Black);
			var text = HtmlTestHelpers.CellsToText(lines[0].Cells);
			// Leading space should be trimmed (at start of line), trailing space may or may not be
			Assert.StartsWith("hello", text.TrimEnd());
		}

		[Fact]
		public void NestedLinksIgnored()
		{
			// HTML spec doesn't allow nested links; AngleSharp will fix them up
			var lines = Flow("<a href=\"a\"><a href=\"b\">text</a></a>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("text", text);
		}

		[Fact]
		public void EmptyLink_DoesNotCrash()
		{
			var lines = Flow("<a href=\"url\"></a><p>after</p>");
			var text = HtmlTestHelpers.LinesToText(lines);
			Assert.Contains("after", text);
		}

		[Fact]
		public void LinkWithOnlyImage()
		{
			// With showImages=false, inline flow should render alt text
			var doc = HtmlTestHelpers.ParseHtml("<html><body><a href=\"url\"><img alt=\"pic\"></a></body></html>");
			var lines = HtmlInlineFlow.FlowInline(
				doc.Body!.ChildNodes, 80, Color.White, Color.Black);
			if (lines.Count > 0)
			{
				var text = HtmlTestHelpers.CellsToText(lines[0].Cells);
				Assert.Contains("pic", text);
			}
		}
	}
}

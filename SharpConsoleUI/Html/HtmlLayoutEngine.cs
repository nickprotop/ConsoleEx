// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Spectre.Console;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Result of laying out HTML content.
	/// </summary>
	public readonly struct LayoutResult
	{
		public readonly LayoutLine[] Lines;
		public readonly int TotalHeight;

		public LayoutResult(LayoutLine[] lines, int totalHeight)
		{
			Lines = lines;
			TotalHeight = totalHeight;
		}
	}

	/// <summary>
	/// Entry point for parsing HTML and coordinating layout.
	/// </summary>
	public class HtmlLayoutEngine
	{
		private readonly IBrowsingContext _context;

		// Cached parsed DOM to avoid re-parsing on width-only changes (e.g., during resize)
		private string? _cachedHtml;
		private string? _cachedBaseUrl;
		private IDocument? _cachedDocument;

		public HtmlLayoutEngine()
		{
			var config = AngleSharp.Configuration.Default.WithCss();
			_context = BrowsingContext.New(config);
		}

		/// <summary>
		/// Parses the given HTML string and lays it out within the specified width.
		/// Caches the parsed DOM — only re-parses when the HTML content or base URL changes.
		/// </summary>
		public LayoutResult Layout(
			string html,
			int maxWidth,
			Color defaultFg,
			Color defaultBg,
			int blockSpacing = 1,
			Color? linkColor = null,
			Color? visitedLinkColor = null,
			string? baseUrl = null,
			bool showImages = false,
			Dictionary<string, Imaging.PixelBuffer?>? imageCache = null)
		{
			// Only re-parse if HTML content or base URL changed
			if (html != _cachedHtml || baseUrl != _cachedBaseUrl || _cachedDocument == null)
			{
				var parser = _context.GetService<IHtmlParser>()!;
				_cachedDocument = parser.ParseDocument(html);
				_cachedHtml = html;
				_cachedBaseUrl = baseUrl;

				if (baseUrl != null)
				{
					ResolveRelativeUrls(_cachedDocument, baseUrl);
				}
			}

			return LayoutDocument(_cachedDocument, maxWidth, defaultFg, defaultBg, blockSpacing, linkColor, visitedLinkColor, showImages, imageCache);
		}

		/// <summary>
		/// Lays out an already-parsed document within the specified width.
		/// </summary>
		public LayoutResult LayoutDocument(
			IDocument document,
			int maxWidth,
			Color defaultFg,
			Color defaultBg,
			int blockSpacing = 1,
			Color? linkColor = null,
			Color? visitedLinkColor = null,
			bool showImages = false,
			Dictionary<string, Imaging.PixelBuffer?>? imageCache = null)
		{
			var body = document.Body;
			if (body == null)
			{
				return new LayoutResult(Array.Empty<LayoutLine>(), 0);
			}

			var lines = HtmlBlockFlow.FlowBlocks(
				body,
				maxWidth,
				defaultFg,
				defaultBg,
				blockSpacing,
				linkColor,
				visitedLinkColor,
				showImages,
				imageCache);

			// Reassign Y positions sequentially
			int y = 0;
			for (int i = 0; i < lines.Length; i++)
			{
				lines[i].Y = y;
				y++;
			}

			int totalHeight = lines.Length > 0 ? y : 0;
			return new LayoutResult(lines, totalHeight);
		}

		/// <summary>
		/// Extracts all image URLs from an HTML string (after resolving relative URLs).
		/// </summary>
		public List<string> GetImageUrls(string html, string? baseUrl = null)
		{
			var parser = _context.GetService<IHtmlParser>()!;
			var document = parser.ParseDocument(html);
			if (baseUrl != null) ResolveRelativeUrls(document, baseUrl);

			var urls = new List<string>();
			foreach (var img in document.QuerySelectorAll("img[src]"))
			{
				var src = img.GetAttribute("src");
				if (!string.IsNullOrEmpty(src))
				{
					if (src.StartsWith("//")) src = "https:" + src;
					urls.Add(src);
				}
			}
			return urls;
		}

		private static void ResolveRelativeUrls(IDocument document, string baseUrl)
		{
			if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
			{
				return;
			}

			// Resolve <a href="...">
			foreach (var anchor in document.QuerySelectorAll("a[href]"))
			{
				var href = anchor.GetAttribute("href");
				if (href != null && ShouldResolve(href))
				{
					if (Uri.TryCreate(baseUri, href, out var resolved))
					{
						anchor.SetAttribute("href", resolved.ToString());
					}
				}
			}

			// Resolve <img src="...">
			foreach (var img in document.QuerySelectorAll("img[src]"))
			{
				var src = img.GetAttribute("src");
				if (src != null && ShouldResolve(src))
				{
					if (Uri.TryCreate(baseUri, src, out var resolved))
					{
						img.SetAttribute("src", resolved.ToString());
					}
				}
			}
		}

		private static bool ShouldResolve(string url)
		{
			return !url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
				&& !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
				&& !url.StartsWith("#", StringComparison.Ordinal)
				&& !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
		}
	}
}

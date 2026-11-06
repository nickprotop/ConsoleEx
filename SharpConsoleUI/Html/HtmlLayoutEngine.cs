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

// -----------------------------------------------------------------------------
// Scoped NativeAOT trim-warning suppression for AngleSharp.Css's calc() pipeline.
//
// SharpConsoleUI reaches AngleSharp.Css's CSS value pipeline transitively from the
// HtmlLayoutEngine constructor (Configuration.Default.WithCss() + BrowsingContext.New)
// and LoadCssAsync (_cssContext.OpenAsync). AngleSharp.Css's four
// CssCalc*Expression.Compute methods each call Activator.CreateInstance(value.GetType(),
// ...) where the runtime Type lacks DynamicallyAccessedMemberTypes.PublicConstructors,
// which NativeAOT trim analysis flags as IL2072. ILC attributes the warning to those
// AngleSharp methods themselves (they contain the reflection call), NOT to SharpConsoleUI's
// call sites — so a C# [UnconditionalSuppressMessage] on HtmlLayoutEngine cannot clear it,
// and ILC honors neither cross-assembly Scope/Target suppression attributes nor an embedded
// ILLink.LinkAttributes.xml that targets another assembly. The genuinely-scoped suppression
// is therefore applied where the AOT publish happens, via the 'singlewarnassembly' ILC
// option scoped to AngleSharp.Css ALONE (see SharpConsoleUI.Tests/aot.test/AotSmoke.csproj).
// SharpConsoleUI's own code still gets full IL2072-as-error analysis — this is NOT a
// project-wide NoWarn of IL2072.
//
// RUNTIME-VERIFIED SAFE: a NativeAOT binary rendering HTML with CSS calc() runs correctly
// (exit 0, content rendered) — the concrete expression types survive trimming. The IL2072
// is a trim-analysis warning, not a runtime crash. HtmlControl is otherwise AOT-compatible.
// -----------------------------------------------------------------------------

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
		private readonly IBrowsingContext _cssContext;

		// Cached parsed DOM to avoid re-parsing on width-only changes (e.g., during resize)
		private string? _cachedHtml;
		private string? _cachedBaseUrl;
		private IDocument? _cachedDocument;

		// CSS-aware document loaded in background — used ONLY for selective CSS lookups
		// (e.g., image width). Never used for full layout (too slow with 20+ stylesheets).
		private IDocument? _cssDocument;

		/// <summary>
		/// Fired when external CSS stylesheets finish loading in the background.
		/// Listeners should invalidate and re-layout to pick up CSS-derived widths.
		/// </summary>
		public event Action? CssLoaded;

		public HtmlLayoutEngine()
		{
			// Sync context: fast parsing, no resource loading — used by Layout()
			var config = AngleSharp.Configuration.Default.WithCss();
			_context = BrowsingContext.New(config);

			// CSS context: loads external stylesheets only — used by LoadCssAsync()
			var cssConfig = AngleSharp.Configuration.Default
				.WithDefaultLoader(new AngleSharp.Io.LoaderOptions
				{
					IsResourceLoadingEnabled = true,
					Filter = request =>
					{
						// Block images, scripts, fonts — only allow CSS and unknown types
						var path = request.Address?.Path ?? "";
						return !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) &&
							   !path.EndsWith(".eot", StringComparison.OrdinalIgnoreCase);
					}
				})
				// What the page is TOLD about its viewport: the frame its media
				// queries and viewport-relative units resolve against. Not the
				// frame an image is measured in - that is the narrower content
				// column, because a browser window also holds furniture a terminal
				// does not render. See HtmlConstants.CssContentWidthPx.
				.WithRenderDevice(new AngleSharp.Css.DefaultRenderDevice
				{
					// Device* is the screen; ViewPort* is the window on it, and the
					// two are separate properties. Media queries and relative units
					// read the VIEWPORT, so setting only the device left it at zero:
					// every (min-width: N) was false and every (max-width: N) true,
					// whatever N said. A page got its phone layout and its desktop
					// layout at once, and a vw length threw outright. Both pairs are
					// set, to the same nominal page, so the answers agree.
					DeviceWidth = (int)HtmlConstants.CssViewportWidthPx,
					DeviceHeight = (int)HtmlConstants.CssViewportHeightPx,
					ViewPortWidth = (int)HtmlConstants.CssViewportWidthPx,
					ViewPortHeight = (int)HtmlConstants.CssViewportHeightPx,
					FontSize = (int)HtmlConstants.CssRootFontSizePx
				})
				.WithCss();
			_cssContext = BrowsingContext.New(cssConfig);
		}

		/// <summary>
		/// Loads external CSS stylesheets in the background. When done, replaces the
		/// cached document with a CSS-aware version and fires CssLoaded.
		/// Only call for pages loaded from HTTP URLs.
		/// </summary>
		public async Task LoadCssAsync(string html, string baseUrl)
		{
			try
			{
				var doc = await _cssContext.OpenAsync(req => req.Content(html).Address(baseUrl));

				// Only update if the HTML hasn't changed while we were loading
				if (html == _cachedHtml && baseUrl == _cachedBaseUrl)
				{
					ResolveRelativeUrls(doc, baseUrl);
					_cssDocument = doc; // Store separately — NOT in _cachedDocument
					CssLoaded?.Invoke();
				}
			}
			catch
			{
				// CSS loading failed — keep using the non-CSS document
			}
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
			Dictionary<string, Imaging.PixelBuffer?>? imageCache = null,
			Drivers.IGraphicsProtocol? graphicsProtocol = null,
			bool usePageBackground = false)
		{
			// Only re-parse if HTML content or base URL changed
			if (html != _cachedHtml || baseUrl != _cachedBaseUrl || _cachedDocument == null)
			{
				var parser = _context.GetService<IHtmlParser>()!;
				_cachedDocument = parser.ParseDocument(html);
				_cachedHtml = html;
				_cachedBaseUrl = baseUrl;
				_cssDocument = null; // invalidate — new content, CSS needs reloading

				if (baseUrl != null)
				{
					ResolveRelativeUrls(_cachedDocument, baseUrl);
				}
			}

			// The page's own surface, when asked for and when it states one. Text
			// colours are resolved against whatever background wins, so adopting
			// the surface and keeping the page's text stay in step: a page that
			// sets cream and black gets cream and black, and one that sets only a
			// colour still gets it checked against the terminal's surface.
			var surface = usePageBackground
				? PageBackground(_cachedDocument) ?? defaultBg
				: defaultBg;

			return LayoutDocument(_cachedDocument, maxWidth, defaultFg, surface, blockSpacing, linkColor, visitedLinkColor, showImages, imageCache, graphicsProtocol);
		}

		/// <summary>
		/// The background colour a document asks for, or null when it does not ask.
		/// </summary>
		/// <remarks>
		/// <c>bgcolor</c> as well as CSS, because that attribute is how the pages
		/// that still set a page colour actually set it: Hacker News's cream and
		/// textfiles.com's black are both <c>bgcolor</c> on an element, not a
		/// stylesheet rule. Reading only CSS kept their text colours while losing
		/// the surface those colours were chosen for.
		/// </remarks>
		/// <summary>
		/// How far down a single-child wrapper chain a page background is looked for.
		/// </summary>
		/// <remarks>
		/// Deep enough for the body > center > table shape a classic page uses,
		/// shallow enough that a deeply nested content element cannot be mistaken
		/// for the page itself.
		/// </remarks>
		private const int MaxPageBackgroundDepth = 4;

		/// <summary>Elements that draw nothing, so they do not make a wrapper ambiguous.</summary>
		private static readonly HashSet<string> NonRendering =
			new(StringComparer.OrdinalIgnoreCase) { "script", "style", "noscript", "template", "link", "meta" };

		private static Color? PageBackground(IDocument document)
		{
			// Body first, then the element a classic layout actually colours: a
			// full-width table standing in for the page.
			foreach (var element in Candidates(document))
			{
				if (element == null) continue;

				var attribute = element.GetAttribute("bgcolor");
				if (!string.IsNullOrWhiteSpace(attribute))
				{
					var parsed = HtmlStyleResolver.ParseCssColor(attribute);
					if (parsed.HasValue) return parsed;
				}

				var style = element.GetAttribute("style");
				if (!string.IsNullOrWhiteSpace(style))
				{
					var match = System.Text.RegularExpressions.Regex.Match(
						style, @"background(?:-color)?\s*:\s*([^;]+)",
						System.Text.RegularExpressions.RegexOptions.IgnoreCase);
					if (match.Success)
					{
						var parsed = HtmlStyleResolver.ParseCssColor(match.Groups[1].Value.Trim());
						if (parsed.HasValue) return parsed;
					}
				}
			}

			return null;

			static IEnumerable<IElement?> Candidates(IDocument doc)
			{
				yield return doc.Body;
				if (doc.Body == null) yield break;

				// Then down the WRAPPER CHAIN: while an element is the only one at
				// its level, it is still standing in for the page rather than being
				// content within it. Hacker News reaches its colour as
				// body > center > table, and stopping a level short found nothing
				// at all.
				//
				// The walk ends at the first element with siblings, which is where
				// the page stops being a single container and starts being layout.
				IElement current = doc.Body;
				for (int depth = 0; depth < MaxPageBackgroundDepth; depth++)
				{
					// Counting RENDERED children, not all of them: Hacker News
					// closes its body with a <script>, so by a raw count the body
					// has two children and the walk stopped one step short of the
					// colour it was looking for.
					IElement? only = null;
					foreach (var child in current.Children)
					{
						if (NonRendering.Contains(child.LocalName)) continue;
						if (only != null) { only = null; break; }
						only = child;
					}

					if (only == null) yield break;

					current = only;
					yield return current;
				}
			}
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
			Dictionary<string, Imaging.PixelBuffer?>? imageCache = null,
			Drivers.IGraphicsProtocol? graphicsProtocol = null)
		{
			var body = document.Body;
			if (body == null)
			{
				return new LayoutResult(Array.Empty<LayoutLine>(), 0);
			}

			HtmlInlineFlow.ResetLinkIds();
			var lines = HtmlBlockFlow.FlowBlocks(
				body,
				maxWidth,
				defaultFg,
				defaultBg,
				blockSpacing,
				linkColor,
				visitedLinkColor,
				showImages,
				imageCache,
				_cssDocument,
				graphicsProtocol,
				_cachedBaseUrl);

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
				&& !url.StartsWith('#')
				&& !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
		}
	}
}

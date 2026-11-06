// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.IO;
using AngleSharp;
using AngleSharp.Dom;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Laying an <c>&lt;img&gt;</c> out: resolving it against the cache, decoding an inline
	/// <c>data:</c> URI, turning pixels into cell rows, and falling back to alt text.
	/// </summary>
	/// <remarks>
	/// Kept apart from the block flow because it is the one element whose layout depends on
	/// something other than the document — a cache, a decoder and, for inline data, real work on
	/// the UI thread. The rest of the flow is pure tree walking.
	/// </remarks>
	public static partial class HtmlBlockFlow
	{
		private static void ProcessImage(
			IElement element,
			BlockContext ctx,
			int indent)
		{
			var src = element.GetAttribute("src");
			if (string.IsNullOrEmpty(src))
				return;

			// Same-origin rule for local files, as a browser applies it: a page
			// fetched over http(s) may not pull in file:// subresources. Without
			// it, any site visited here could name a path on this machine and
			// read the answer back off the screen.
			//
			// Enforced where the read is about to happen, and decided from the
			// element's own document, so a caller cannot forget to pass the
			// origin and quietly reopen the hole.
			if (HtmlImageLoader.IsLocalFileUrl(src) && !MayReadLocalFiles(ctx.DocumentUrl))
			{
				// No suffix: the alt text is what the page author wrote for a
				// reader who cannot see the image, and that is exactly the
				// situation. Why it was blocked concerns the developer, not the
				// reader, and a parenthetical here would appear mid-paragraph.
				AddAltText(element, ctx, indent, suffix: null);
				return;
			}

			var maxAvailableWidth = ctx.MaxWidth - indent;
			if (maxAvailableWidth <= 0) maxAvailableWidth = 1;

			// What the page says this image is, in CSS pixels. Measured in one unit
			// and converted in ONE place, which is the point: a stylesheet width and
			// an attribute width used to be converted separately with different
			// ratios, so the same declared size rendered eight times larger through
			// one path than the other.
			//
			// A stylesheet width is layout intent and is taken as final. A width
			// ATTRIBUTE is not reliably either: on a real encyclopedia article some
			// carry the original file's resolution (width="3640" on a thumbnail
			// served as 250 real pixels) and others a display size left over from a
			// narrower layout (width="135" on that same 250-pixel file). Believed
			// literally, one fills the page and the other is a smudge - in the same
			// article, in adjacent paragraphs.
			//
			// So an attribute is reconciled against the pixels actually served,
			// which is the one number the page cannot be wrong about. See
			// ReconcileWithServedPixels.
			double? cssWidth = CssDeclaredWidthPx(element, src, ctx);
			double? attributeWidth = cssWidth == null ? AttributeWidthPx(element) : null;
			double? declaredPx = cssWidth ?? attributeWidth;

			int effectiveWidth = declaredPx.HasValue
				? ScaleToColumns(declaredPx.Value, maxAvailableWidth)
				: maxAvailableWidth;

			// One place that turns a decoded image into rows, so the reconciliation
			// below cannot be applied on one path and forgotten on another.
			Cell[][]? Render(Imaging.PixelBuffer buffer)
			{
				// When only a width ATTRIBUTE declared the size, the pixels actually
				// served decide instead. They are the one number a page cannot be
				// wrong about, and a real encyclopedia article gets the attribute
				// wrong in both directions at once: the original file's resolution
				// on some images (3640 for a file served at 250 pixels) and a stale
				// display size on others (135 for that same file). Believed
				// literally those two neighbours render as a full-page photograph
				// and a smudge; believed as served, both are the thumbnail the
				// server prepared.
				//
				// A stylesheet width is left alone - showing a 1600px photograph as
				// a 200px inset is real layout intent, not stale metadata.
				int width = attributeWidth.HasValue && buffer.Width > 0
					? ScaleToColumns(buffer.Width, maxAvailableWidth)
					: effectiveWidth;

				return HtmlImageLoader.RenderFromBuffer(buffer, width, ctx.DefaultBg,
					ctx.GraphicsProtocol, declaredPx.HasValue);
			}

			Cell[][]? imageRows;
			string? imageError = null;
			try
			{
				// Check cache first
				if (ctx.ImageCache != null)
				{
					var normalizedSrc = src.StartsWith("//") ? "https:" + src : src;
					if (ctx.ImageCache.TryGetValue(normalizedSrc, out var cachedBuffer))
					{
						imageRows = cachedBuffer != null
							? Render(cachedBuffer)
							: null;
					}
					else if (normalizedSrc.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
					{
						// Inline data — nothing to download, decode it right here (once: the cache
						// keeps the buffer for later layouts)
						var decoded = HtmlImageLoader.DecodeDataUri(normalizedSrc);
						ctx.ImageCache[normalizedSrc] = decoded;
						imageRows = decoded != null
							? Render(decoded)
							: null;
					}
					else if (HtmlImageLoader.IsLocalFileUrl(normalizedSrc))
					{
						// A local file needs no download, so there is nothing to wait
						// for and nothing to show alt text in place of: read it here,
						// the way an inline data image is read, and cache the result
						// so a failure is not retried on every relayout.
						var local = HtmlImageLoader.DecodeLocalFile(normalizedSrc);
						ctx.ImageCache[normalizedSrc] = local;
						imageRows = local != null
							? Render(local)
							: null;
					}
					else
					{
						// Not in cache — show alt text (will be loaded progressively)
						imageRows = null;
					}
				}
				else
				{
					imageRows = HtmlImageLoader.LoadAndRender(src, effectiveWidth, ctx.DefaultBg,
						ctx.GraphicsProtocol, declaredPx.HasValue);
				}
			}
			catch (Exception ex)
			{
				imageRows = null;
				imageError = ex.Message;
			}

			if (imageRows != null && imageRows.Length > 0)
			{
				// A picture inside an <a> is a link, and the inline flow that
				// builds links never sees it: an image is block rows, written
				// here. One region per row, all sharing one id, so the whole
				// picture is a single Tab stop rather than a dozen - the same
				// thing the inline flow does for a link wrapped across lines.
				string? href = EnclosingLinkUrl(element);
				int linkId = href != null ? HtmlInlineFlow.NextLinkId() : 0;

				foreach (var rowCells in imageRows)
				{
					LinkRegion[]? links = href != null && rowCells.Length > 0
						? new[] { new LinkRegion(0, rowCells.Length, href, string.Empty, linkId) }
						: null;

					var line = new LayoutLine(0, indent, rowCells.Length, rowCells,
						TextAlignment.Left, links, isImage: true);
					ctx.AddLine(line, indent);
				}

				AddSpacing(ctx, indent, ctx.BlockSpacing);
			}
			else
			{
				string? suffix = null;
				if (imageError != null)
				{
					// Truncate long decoder error messages rather than dumping
					// a decoder list into the page.
					var shortError = imageError.Split('\n')[0];
					if (shortError.Length > 60) shortError = shortError.Substring(0, 57) + "...";
					suffix = $" ({shortError})";
				}

				AddAltText(element, ctx, indent, suffix);
			}
		}

		/// <summary>
		/// Writes an image's alt text in place of the image.
		/// </summary>
		/// <remarks>
		/// What a reader gets whenever the picture does not arrive, for any
		/// reason: not found, not decodable, or not permitted. One path, so a
		/// newly blocked case cannot render as a blank gap.
		/// </remarks>
		private static void AddAltText(IElement element, BlockContext ctx, int indent, string? suffix)
		{
			var alt = element.GetAttribute("alt");
			string? href = EnclosingLinkUrl(element);

			// A picture with nothing to say says nothing. Alt text is what the
			// author wrote for a reader who cannot see the image, and an absent
			// or empty one means there was nothing worth describing - which is
			// exactly how a page marks a logo, an icon or a spacer. Inventing the
			// word "image" turned those into a column of placeholders, each
			// taking a line and saying less than the blank line it replaced. On
			// one real news site, 63 of 134 images carried no alt attribute.
			//
			// Unless it is a link, where a placeholder earns its line: with
			// nothing to land on the link cannot be followed at all, so the URL
			// stands in - the page's own words rather than ours.
			if (string.IsNullOrWhiteSpace(alt) && suffix == null)
			{
				if (href == null)
					return;

				alt = href;
			}

			var text = HtmlConstants.ImageAltPrefix + (alt ?? string.Empty) + (suffix ?? "") + HtmlConstants.ImageAltSuffix;

			var width = ctx.MaxWidth - indent;
			var cells = TextToCells(text, width > 0 ? width : 80,
				ctx.DefaultFg, ctx.DefaultBg, TextDecoration.Dim | TextDecoration.Italic);

			// What this text stands in for may be a link, and then so is it.
			// Text that reads as a link and does nothing is worse than either
			// alternative, and this is the state a page is in for every image it
			// has not finished loading.
			LinkRegion[]? links = href != null && cells.Length > 0
				? new[] { new LinkRegion(0, cells.Length, href, text, HtmlInlineFlow.NextLinkId()) }
				: null;

			ctx.AddLine(new LayoutLine(0, indent, cells.Length, cells, TextAlignment.Left, links), indent);
		}

		/// <summary>
		/// The URL of the nearest enclosing <c>&lt;a href&gt;</c>, or null.
		/// </summary>
		/// <remarks>
		/// Walks ancestors rather than checking the parent, because a thumbnail
		/// is rarely a direct child: an encyclopedia wraps it in a figure and a
		/// span or two before the anchor. An anchor with no href is a named
		/// target, not a link, and is skipped.
		/// </remarks>
		private static string? EnclosingLinkUrl(IElement element)
		{
			for (var e = element.ParentElement; e != null; e = e.ParentElement)
			{
				if (!string.Equals(e.TagName, "A", StringComparison.OrdinalIgnoreCase))
					continue;

				var href = e.GetAttribute("href");
				return string.IsNullOrWhiteSpace(href) ? null : href;
			}

			return null;
		}

		/// <summary>
		/// Whether a document loaded from this URL is allowed to read local files.
		/// </summary>
		/// <remarks>
		/// <para>
		/// True for a document that came from a <c>file:</c> URL, and for one with
		/// no origin at all - content handed to the control by the application
		/// itself, which is not a remote page and has nothing to cross. False for
		/// anything fetched over http(s).
		/// </para>
		/// <para>
		/// The URL is the one the control loaded, not the parsed document's own
		/// address: relative sources are resolved by rewriting attributes, so the
		/// document itself always reports <c>about:blank</c> and cannot be asked.
		/// </para>
		/// </remarks>
		private static bool MayReadLocalFiles(string? documentUrl)
		{
			if (string.IsNullOrEmpty(documentUrl)
				|| documentUrl.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
				return true;

			return HtmlImageLoader.IsLocalFileUrl(documentUrl);
		}

		/// <summary>
		/// Turns a width declared in CSS pixels into a number of terminal columns.
		/// </summary>
		/// <remarks>
		/// The declared width is a fraction of the content column the page was
		/// written for (<see cref="HtmlConstants.CssContentWidthPx"/>), and that
		/// fraction is spent on the columns available here. The column rather than
		/// the whole viewport, because that is the frame an author sizes a picture
		/// in, and in a terminal - which renders no sidebar - the column is the
		/// line. See that constant for why, and
		/// <see cref="HtmlConstants.CssViewportWidthPx"/> for why a fixed
		/// pixels-per-column ratio cannot work.
		/// </remarks>
		internal static int ScaleToColumns(double declaredPx, int maxAvailableWidth)
		{
			if (declaredPx <= 0 || maxAvailableWidth <= 0)
				return maxAvailableWidth > 0 ? maxAvailableWidth : 1;

			double fraction = Math.Min(1.0, declaredPx / HtmlConstants.CssContentWidthPx);
			int columns = (int)Math.Round(maxAvailableWidth * fraction);

			// No minimum beyond one column. A floor was tried and removed: because
			// the result is a pure fraction of the page, "big enough to deserve the
			// floor" and "already at least the floor" are the same condition, so
			// the branch could never run. An image that rounds to one column is one
			// the page asked to be tiny, and one column is what tiny looks like
			// here.
			if (columns < 1) columns = 1;
			if (columns > maxAvailableWidth) columns = maxAvailableWidth;
			return columns;
		}

		/// <summary>
		/// The width a stylesheet gives this image, in CSS pixels, if any.
		/// </summary>
		/// <remarks>
		/// Only this one element is queried against the CSS document, never the
		/// whole tree: the CSS document is a second parse loaded in the background
		/// and using it for general layout would mean laying the page out twice.
		/// </remarks>
		private static double? CssDeclaredWidthPx(IElement element, string src, BlockContext ctx)
		{
			// An inline style is on the element in the document being laid out, so
			// it is readable whether or not external CSS ever arrives. Checked
			// first for that reason: it needs no second document and cannot go
			// stale against one.
			var styleAttribute = element.GetAttribute("style");
			if (!string.IsNullOrEmpty(styleAttribute))
			{
				var inlineWidth = HtmlStyleResolver.ParseCssPixels(
					InlineDeclaration(styleAttribute, "width"));
				if (inlineWidth is > 0)
					return inlineWidth;
			}

			if (ctx.CssDocument == null)
				return null;

			try
			{
				var cssImg = ctx.CssDocument.QuerySelectorAll("img")
					.FirstOrDefault(img => img.GetAttribute("src") == src);
				if (cssImg == null)
					return null;

				var style = HtmlStyleResolver.Resolve(cssImg, ctx.DefaultFg, ctx.DefaultBg);

				// ExplicitWidth is already in columns, divided by the generic
				// px-per-column ratio on the way through. Multiply back out to the
				// pixels the stylesheet actually declared, so the one conversion
				// above is the only one that decides a width.
				if (style.ExplicitWidth is > 0)
					return style.ExplicitWidth.Value * HtmlConstants.PxToCharRatio;
			}
			catch
			{
				// AngleSharp.Css throws on some malformed real-world CSS; an
				// unknown width is better than no image.
			}

			return null;
		}

		/// <summary>Pulls one declaration out of an inline <c>style</c> attribute.</summary>
		private static string? InlineDeclaration(string styleAttribute, string property)
		{
			foreach (var part in styleAttribute.Split(';'))
			{
				int colon = part.IndexOf(':');
				if (colon <= 0) continue;
				if (part.AsSpan(0, colon).Trim().Equals(property, StringComparison.OrdinalIgnoreCase))
					return part.Substring(colon + 1).Trim();
			}
			return null;
		}

		/// <summary>The width the HTML <c>width</c> attribute gives, in CSS pixels, if any.</summary>
		private static double? AttributeWidthPx(IElement element)
		{
			var widthAttr = element.GetAttribute("width");
			if (widthAttr != null && int.TryParse(widthAttr, out int pxWidth) && pxWidth > 0)
				return pxWidth;
			return null;
		}
	}
}

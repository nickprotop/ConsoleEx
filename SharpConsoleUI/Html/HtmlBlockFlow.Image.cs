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

			var maxAvailableWidth = ctx.MaxWidth - indent;
			if (maxAvailableWidth <= 0) maxAvailableWidth = 1;

			// Determine image width: CSS (from background-loaded doc) → HTML attr → natural size
			int effectiveWidth = maxAvailableWidth;

			// 1. Try CSS computed width from the CSS document (loaded in background)
			//    Only query this specific img element — do NOT use for full layout.
			if (ctx.CssDocument != null)
			{
				try
				{
					// Find matching img in CSS document by src attribute
					var cssImg = ctx.CssDocument.QuerySelectorAll("img")
						.FirstOrDefault(img => img.GetAttribute("src") == src);
					if (cssImg != null)
					{
						var style = HtmlStyleResolver.Resolve(cssImg, ctx.DefaultFg, ctx.DefaultBg);
						if (style.ExplicitWidth.HasValue && style.ExplicitWidth.Value > 0)
							effectiveWidth = Math.Min(style.ExplicitWidth.Value, maxAvailableWidth);
					}
				}
				catch { /* CSS resolution may fail — fall through */ }
			}

			// 2. If CSS didn't set a width, try HTML width attribute
			if (effectiveWidth == maxAvailableWidth)
			{
				var widthAttr = element.GetAttribute("width");
				if (widthAttr != null && int.TryParse(widthAttr, out int pxWidth) && pxWidth > 0)
				{
					effectiveWidth = Math.Min((int)Math.Ceiling(pxWidth / HtmlConstants.ImagePxToCharRatio), maxAvailableWidth);
				}
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
							? HtmlImageLoader.RenderFromBuffer(cachedBuffer, effectiveWidth, ctx.DefaultBg, ctx.GraphicsProtocol)
							: null;
					}
					else if (normalizedSrc.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
					{
						// Inline data — nothing to download, decode it right here (once: the cache
						// keeps the buffer for later layouts)
						var decoded = HtmlImageLoader.DecodeDataUri(normalizedSrc);
						ctx.ImageCache[normalizedSrc] = decoded;
						imageRows = decoded != null
							? HtmlImageLoader.RenderFromBuffer(decoded, effectiveWidth, ctx.DefaultBg, ctx.GraphicsProtocol)
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
					imageRows = HtmlImageLoader.LoadAndRender(src, effectiveWidth, ctx.DefaultBg, ctx.GraphicsProtocol);
				}
			}
			catch (Exception ex)
			{
				imageRows = null;
				imageError = ex.Message;
			}

			if (imageRows != null && imageRows.Length > 0)
			{
				foreach (var rowCells in imageRows)
				{
					var line = new LayoutLine(0, indent, rowCells.Length, rowCells, TextAlignment.Left);
					ctx.AddLine(line, indent);
				}

				AddSpacing(ctx, indent, ctx.BlockSpacing);
			}
			else
			{
				// Fallback to alt text — truncate error to avoid dumping decoder lists
				var alt = element.GetAttribute("alt") ?? "image";
				var errorSuffix = "";
				if (imageError != null)
				{
					// Truncate long ImageSharp error messages (e.g., "Image cannot be loaded. Available decoders:...")
					var shortError = imageError.Split('\n')[0];
					if (shortError.Length > 60) shortError = shortError.Substring(0, 57) + "...";
					errorSuffix = $" ({shortError})";
				}
				var imgText = HtmlConstants.ImageAltPrefix + alt + errorSuffix + HtmlConstants.ImageAltSuffix;
				var effectiveAltWidth = ctx.MaxWidth - indent;
				var cells = TextToCells(imgText, effectiveAltWidth > 0 ? effectiveAltWidth : 80,
					ctx.DefaultFg, ctx.DefaultBg, TextDecoration.Dim | TextDecoration.Italic);

				var line = new LayoutLine(0, indent, cells.Length, cells, TextAlignment.Left);
				ctx.AddLine(line, indent);
			}
		}

	}
}

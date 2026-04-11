// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using System.IO;
using AngleSharp;
using AngleSharp.Dom;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Block-level layout engine for HTML rendering.
	/// Handles paragraphs, headings, lists, blockquotes, pre, hr, div, and
	/// delegates inline content to HtmlInlineFlow.
	/// </summary>
	public static class HtmlBlockFlow
	{
		private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
		{
			"p", "div", "h1", "h2", "h3", "h4", "h5", "h6",
			"hr", "br", "ul", "ol", "li", "blockquote", "pre", "table",
			"section", "article", "header", "footer", "nav", "main", "aside",
			"figure", "figcaption", "details", "summary", "dl", "dt", "dd"
		};

		private static readonly HashSet<string> SkipTags = new(StringComparer.OrdinalIgnoreCase)
		{
			"script", "style", "head", "meta", "link", "noscript"
		};

		/// <summary>
		/// Flows block-level HTML elements into an array of LayoutLines.
		/// </summary>
		public static LayoutLine[] FlowBlocks(
			IElement root,
			int maxWidth,
			Color defaultFg,
			Color defaultBg,
			int blockSpacing = 1,
			Color? linkColor = null,
			Color? visitedLinkColor = null,
			bool showImages = false,
			Dictionary<string, Imaging.PixelBuffer?>? imageCache = null)
		{
			var ctx = new BlockContext
			{
				MaxWidth = maxWidth,
				DefaultFg = defaultFg,
				DefaultBg = defaultBg,
				BlockSpacing = blockSpacing,
				LinkColor = linkColor,
				VisitedLinkColor = visitedLinkColor,
				ShowImages = showImages,
				ImageCache = imageCache,
			};

			ProcessChildren(root, ctx, indent: 0, listDepth: 0);

			// Flush any remaining inline content
			FlushInline(ctx, indent: 0);

			return ctx.Lines.ToArray();
		}

		private static void ProcessChildren(
			IElement parent,
			BlockContext ctx,
			int indent,
			int listDepth)
		{
			foreach (var node in parent.ChildNodes)
			{
				if (node is IText textNode)
				{
					ctx.InlineBuffer.Add(node);
				}
				else if (node is IElement element)
				{
					var tag = element.LocalName.ToLowerInvariant();

					if (SkipTags.Contains(tag))
						continue;

					// Check display:none
					var style = HtmlStyleResolver.Resolve(element, ctx.DefaultFg, ctx.DefaultBg);
					if (style.IsHidden)
						continue;

					if (BlockTags.Contains(tag))
					{
						// Flush pending inline content
						FlushInline(ctx, indent);

						ProcessBlockElement(element, tag, style, ctx, indent, listDepth);
					}
					else if (tag == "img" && ctx.ShowImages && !string.IsNullOrEmpty(element.GetAttribute("src")))
					{
						// Render image as a block-level element
						FlushInline(ctx, indent);
						ProcessImage(element, ctx, indent);
					}
					else if (ctx.ShowImages && element.QuerySelector("img[src]") != null)
					{
						// Inline element contains an image — flush inline, render image(s) as blocks
						FlushInline(ctx, indent);
						foreach (var img in element.QuerySelectorAll("img[src]"))
						{
							if (!string.IsNullOrEmpty(img.GetAttribute("src")))
								ProcessImage(img, ctx, indent);
						}
					}
					else
					{
						// Inline element — collect it
						ctx.InlineBuffer.Add(node);
					}
				}
			}
		}

		private static void ProcessBlockElement(
			IElement element,
			string tag,
			ResolvedStyle style,
			BlockContext ctx,
			int indent,
			int listDepth)
		{
			switch (tag)
			{
				case "p":
					ProcessParagraph(element, style, ctx, indent);
					break;

				case "h1":
					ProcessHeading(element, style, ctx, indent, TextDecoration.Bold | TextDecoration.Underline);
					break;

				case "h2":
				case "h3":
				case "h4":
				case "h5":
				case "h6":
					ProcessHeading(element, style, ctx, indent, TextDecoration.Bold);
					break;

				case "hr":
					ProcessHr(ctx, indent);
					break;

				case "br":
					ctx.AddEmptyLine(indent);
					break;

				case "ul":
					ProcessUnorderedList(element, ctx, indent, listDepth);
					break;

				case "ol":
					ProcessOrderedList(element, ctx, indent, listDepth);
					break;

				case "li":
					// Standalone li outside list — just render inline content
					ProcessParagraph(element, style, ctx, indent);
					break;

				case "blockquote":
					ProcessBlockquote(element, ctx, indent, listDepth);
					break;

				case "pre":
					ProcessPre(element, ctx, indent);
					break;

				case "table":
					ProcessTable(element, ctx, indent);
					break;

				default:
					// Generic block: div, section, article, header, footer, nav, main, aside,
					// figure, figcaption, details, summary, dl, dt, dd
					ProcessGenericBlock(element, style, ctx, indent, listDepth);
					break;
			}
		}

		private static void ProcessParagraph(
			IElement element,
			ResolvedStyle style,
			BlockContext ctx,
			int indent)
		{
			var effectiveWidth = ctx.MaxWidth - indent;
			if (effectiveWidth <= 0) effectiveWidth = 1;

			var lines = HtmlInlineFlow.FlowInline(
				element.ChildNodes,
				effectiveWidth,
				style.Foreground,
				style.Background,
				alignment: style.Alignment,
				linkColor: ctx.LinkColor,
				visitedLinkColor: ctx.VisitedLinkColor);

			foreach (var line in lines)
				ctx.AddLine(line, indent);

			AddSpacing(ctx, indent, ctx.BlockSpacing);
		}

		private static void ProcessHeading(
			IElement element,
			ResolvedStyle style,
			BlockContext ctx,
			int indent,
			TextDecoration decorations)
		{
			// Empty line before heading
			AddSpacing(ctx, indent, 1);

			var effectiveWidth = ctx.MaxWidth - indent;
			if (effectiveWidth <= 0) effectiveWidth = 1;

			var headingDecorations = decorations | style.Decorations;

			var lines = HtmlInlineFlow.FlowInline(
				element.ChildNodes,
				effectiveWidth,
				style.Foreground,
				style.Background,
				inheritedDecorations: headingDecorations,
				alignment: style.Alignment,
				linkColor: ctx.LinkColor,
				visitedLinkColor: ctx.VisitedLinkColor);

			foreach (var line in lines)
				ctx.AddLine(line, indent);

			// Empty line after heading
			AddSpacing(ctx, indent, 1);
		}

		private static void ProcessHr(BlockContext ctx, int indent)
		{
			var width = ctx.MaxWidth - indent;
			if (width <= 0) width = 1;

			var cells = new Cell[width];
			for (int i = 0; i < width; i++)
				cells[i] = new Cell(HtmlConstants.HorizontalRuleChar, ctx.DefaultFg, ctx.DefaultBg);

			var line = new LayoutLine(0, indent, width, cells, TextAlignment.Left);
			ctx.AddLine(line, indent);

			AddSpacing(ctx, indent, 1);
		}

		private static void ProcessUnorderedList(
			IElement element,
			BlockContext ctx,
			int indent,
			int listDepth)
		{
			var bulletChar = HtmlConstants.BulletChars[listDepth % HtmlConstants.BulletChars.Length];

			foreach (var child in element.Children)
			{
				var childTag = child.LocalName.ToLowerInvariant();
				if (childTag == "li")
				{
					ProcessListItem(child, ctx, indent, listDepth, $"{bulletChar} ");
				}
			}
		}

		private static void ProcessOrderedList(
			IElement element,
			BlockContext ctx,
			int indent,
			int listDepth)
		{
			int counter = 1;
			foreach (var child in element.Children)
			{
				var childTag = child.LocalName.ToLowerInvariant();
				if (childTag == "li")
				{
					ProcessListItem(child, ctx, indent, listDepth, $"{counter}. ");
					counter++;
				}
			}
		}

		private static void ProcessListItem(
			IElement element,
			BlockContext ctx,
			int indent,
			int listDepth,
			string prefix)
		{
			// Use actual prefix width (not fixed ListIndent) to avoid overflow on "10. ", "100. " etc.
			int prefixWidth = prefix.EnumerateRunes().Sum(r => Helpers.UnicodeWidth.GetRuneWidth(r));
			int actualIndent = Math.Max(HtmlConstants.ListIndent, prefixWidth);
			var itemIndent = indent + actualIndent;

			// Render inline content of the li (excluding nested lists)
			var inlineNodes = new List<INode>();
			foreach (var child in element.ChildNodes)
			{
				if (child is IElement el)
				{
					var childTag = el.LocalName.ToLowerInvariant();
					if (childTag == "ul" || childTag == "ol")
						continue;
				}
				inlineNodes.Add(child);
			}

			if (inlineNodes.Count > 0)
			{
				var effectiveWidth = ctx.MaxWidth - itemIndent;
				if (effectiveWidth <= 0) effectiveWidth = 1;

				var wrapper = new NodeListWrapper(inlineNodes);
				var lines = HtmlInlineFlow.FlowInline(
					wrapper,
					effectiveWidth,
					ctx.DefaultFg,
					ctx.DefaultBg,
					linkColor: ctx.LinkColor,
					visitedLinkColor: ctx.VisitedLinkColor);

				for (int i = 0; i < lines.Count; i++)
				{
					var line = lines[i];
					if (i == 0)
					{
						// First line: prepend bullet/number prefix
						var prefixCells = new Cell[prefix.Length];
						for (int j = 0; j < prefix.Length; j++)
							prefixCells[j] = new Cell(prefix[j], ctx.DefaultFg, ctx.DefaultBg);

						var combined = new Cell[prefixCells.Length + line.Cells.Length];
						Array.Copy(prefixCells, 0, combined, 0, prefixCells.Length);
						Array.Copy(line.Cells, 0, combined, prefixCells.Length, line.Cells.Length);

						var bulletIndent = indent;
						line = new LayoutLine(0, bulletIndent, combined.Length, combined, line.Alignment, line.Links);
						ctx.AddLine(line, bulletIndent);
					}
					else
					{
						ctx.AddLine(line, itemIndent);
					}
				}
			}

			// Process nested lists
			foreach (var child in element.Children)
			{
				var childTag = child.LocalName.ToLowerInvariant();
				if (childTag == "ul")
					ProcessUnorderedList(child, ctx, itemIndent, listDepth + 1);
				else if (childTag == "ol")
					ProcessOrderedList(child, ctx, itemIndent, listDepth + 1);
			}
		}

		private static void ProcessBlockquote(
			IElement element,
			BlockContext ctx,
			int indent,
			int listDepth)
		{
			var bqIndent = indent + HtmlConstants.BlockquoteIndent;
			var effectiveWidth = ctx.MaxWidth - bqIndent;
			if (effectiveWidth <= 0) effectiveWidth = 1;

			var bqFg = HtmlConstants.DefaultBlockquoteTextColor;

			// Process children as blocks within the blockquote
			var innerCtx = new BlockContext
			{
				MaxWidth = ctx.MaxWidth,
				DefaultFg = bqFg,
				DefaultBg = ctx.DefaultBg,
				BlockSpacing = ctx.BlockSpacing,
				LinkColor = ctx.LinkColor,
				VisitedLinkColor = ctx.VisitedLinkColor,
				ShowImages = ctx.ShowImages,
				ImageCache = ctx.ImageCache,
			};

			ProcessChildren(element, innerCtx, bqIndent, listDepth);
			FlushInline(innerCtx, bqIndent);

			// Add blockquote bar to each line
			foreach (var line in innerCtx.Lines)
			{
				// Prepend the blockquote bar at the indent position
				var barCell = new Cell(HtmlConstants.BlockquoteBar, HtmlConstants.DefaultBlockquoteBarColor, ctx.DefaultBg);
				var spacer = new Cell(' ', bqFg, ctx.DefaultBg);

				var newCells = new Cell[line.Cells.Length + 2];
				newCells[0] = barCell;
				newCells[1] = spacer;
				Array.Copy(line.Cells, 0, newCells, 2, line.Cells.Length);

				var newLine = new LayoutLine(0, indent, newCells.Length, newCells, line.Alignment, line.Links);
				ctx.AddLine(newLine, indent);
			}
		}

		private static void ProcessPre(
			IElement element,
			BlockContext ctx,
			int indent)
		{
			// Get raw text content preserving whitespace
			var text = element.TextContent;
			var rawLines = text.Split('\n');

			var effectiveWidth = ctx.MaxWidth - indent;
			if (effectiveWidth <= 0) effectiveWidth = 1;

			foreach (var rawLine in rawLines)
			{
				var cells = TextToCells(rawLine, effectiveWidth, ctx.DefaultFg, HtmlConstants.DefaultCodeBackground);
				var line = new LayoutLine(0, indent, cells.Length, cells, TextAlignment.Left);
				ctx.AddLine(line, indent);
			}
		}

		private static void ProcessTable(
			IElement element,
			BlockContext ctx,
			int indent)
		{
			var availWidth = ctx.MaxWidth - indent;
			if (availWidth <= 0) availWidth = 1;

			var tableLines = HtmlTableLayout.LayoutTable(element, availWidth, ctx.DefaultFg, ctx.DefaultBg, ctx.LinkColor);
			if (tableLines.Length == 0)
				return;

			foreach (var line in tableLines)
				ctx.AddLine(line, indent);

			AddSpacing(ctx, indent, ctx.BlockSpacing);
		}

		private static void ProcessGenericBlock(
			IElement element,
			ResolvedStyle style,
			BlockContext ctx,
			int indent,
			int listDepth)
		{
			var extraIndent = style.MarginLeft + style.PaddingLeft;
			var totalIndent = indent + extraIndent;

			// Add margin-top spacing
			for (int i = 0; i < style.MarginTop + style.PaddingTop; i++)
				ctx.AddEmptyLine(totalIndent);

			// If display:grid, use grid layout engine
			if (style.DisplayGrid == "grid" && !string.IsNullOrEmpty(style.GridTemplateColumns))
			{
				var availWidth = ctx.MaxWidth - totalIndent;
				if (availWidth <= 0) availWidth = 1;

				var gridLines = HtmlGridLayout.LayoutGrid(element, availWidth, style.GridTemplateColumns, style.GridGap, ctx.DefaultFg, ctx.DefaultBg, ctx.LinkColor);
				foreach (var line in gridLines)
					ctx.AddLine(line, totalIndent);
			}
			else
			{
				ProcessChildren(element, ctx, totalIndent, listDepth);
			}
			FlushInline(ctx, totalIndent);

			// Add margin-bottom spacing
			for (int i = 0; i < style.MarginBottom + style.PaddingBottom; i++)
				ctx.AddEmptyLine(totalIndent);
		}

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

			// Determine image width: HTML attribute → natural size → clamp to available
			int effectiveWidth = maxAvailableWidth;
			var widthAttr = element.GetAttribute("width");
			if (widthAttr != null && int.TryParse(widthAttr, out int pxWidth) && pxWidth > 0)
			{
				effectiveWidth = Math.Min((int)Math.Ceiling(pxWidth / HtmlConstants.ImagePxToCharRatio), maxAvailableWidth);
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
							? HtmlImageLoader.RenderFromBuffer(cachedBuffer, effectiveWidth, ctx.DefaultBg)
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
					imageRows = HtmlImageLoader.LoadAndRender(src, effectiveWidth, ctx.DefaultBg);
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
				// Fallback to alt text with error info if available
				var alt = element.GetAttribute("alt") ?? "image";
				var errorSuffix = imageError != null ? $" (error: {imageError})" : "";
				var imgText = HtmlConstants.ImageAltPrefix + alt + errorSuffix + HtmlConstants.ImageAltSuffix;
				var effectiveAltWidth = ctx.MaxWidth - indent;
				var cells = TextToCells(imgText, effectiveAltWidth > 0 ? effectiveAltWidth : 80,
					ctx.DefaultFg, ctx.DefaultBg, TextDecoration.Dim | TextDecoration.Italic);

				var line = new LayoutLine(0, indent, cells.Length, cells, TextAlignment.Left);
				ctx.AddLine(line, indent);
			}
		}

		private static void FlushInline(BlockContext ctx, int indent)
		{
			if (ctx.InlineBuffer.Count == 0)
				return;

			// Check if all inline nodes are whitespace-only text
			bool allWhitespace = true;
			foreach (var node in ctx.InlineBuffer)
			{
				if (node is IText textNode)
				{
					if (!string.IsNullOrWhiteSpace(textNode.Data))
					{
						allWhitespace = false;
						break;
					}
				}
				else
				{
					allWhitespace = false;
					break;
				}
			}

			if (allWhitespace)
			{
				ctx.InlineBuffer.Clear();
				return;
			}

			var effectiveWidth = ctx.MaxWidth - indent;
			if (effectiveWidth <= 0) effectiveWidth = 1;

			var wrapper = new NodeListWrapper(ctx.InlineBuffer);
			var lines = HtmlInlineFlow.FlowInline(
				wrapper,
				effectiveWidth,
				ctx.DefaultFg,
				ctx.DefaultBg,
				linkColor: ctx.LinkColor,
				visitedLinkColor: ctx.VisitedLinkColor);

			foreach (var line in lines)
				ctx.AddLine(line, indent);

			ctx.InlineBuffer.Clear();
		}

		/// <summary>
		/// Creates a Cell array from text with proper wide-character continuation cells.
		/// Truncates at maxColumns (column width, not character count).
		/// </summary>
		private static Cell[] TextToCells(string text, int maxColumns, Color fg, Color bg, TextDecoration decorations = TextDecoration.None)
		{
			var cells = new List<Cell>();
			int col = 0;
			foreach (var rune in text.EnumerateRunes())
			{
				if (rune.Value == '\r') continue;
				int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
				if (col + rw > maxColumns) break;
				cells.Add(new Cell(rune, fg, bg, decorations));
				if (rw == 2)
					cells.Add(new Cell(' ', fg, bg) { IsWideContinuation = true });
				col += rw;
			}
			return cells.ToArray();
		}

		private static void AddSpacing(BlockContext ctx, int indent, int count)
		{
			for (int i = 0; i < count; i++)
				ctx.AddEmptyLine(indent);
		}

		/// <summary>
		/// Internal context for accumulating layout lines.
		/// </summary>
		private class BlockContext
		{
			public int MaxWidth;
			public Color DefaultFg;
			public Color DefaultBg;
			public int BlockSpacing;
			public Color? LinkColor;
			public Color? VisitedLinkColor;
			public bool ShowImages;
			public Dictionary<string, Imaging.PixelBuffer?>? ImageCache;

			public List<LayoutLine> Lines = new();
			public List<INode> InlineBuffer = new();

			public void AddLine(LayoutLine line, int indent)
			{
				line.Y = Lines.Count;
				line.X = indent;
				Lines.Add(line);
			}

			public void AddEmptyLine(int indent)
			{
				var line = new LayoutLine(Lines.Count, indent, 0, Array.Empty<Cell>(), TextAlignment.Left);
				Lines.Add(line);
			}
		}

		/// <summary>
		/// Wrapper to present a List of INode as an INodeList for HtmlInlineFlow.
		/// </summary>
		private class NodeListWrapper : INodeList
		{
			private readonly List<INode> _nodes;

			public NodeListWrapper(List<INode> nodes) => _nodes = new List<INode>(nodes);

			public INode this[int index] => _nodes[index];

			public int Length => _nodes.Count;

			public IEnumerator<INode> GetEnumerator() => _nodes.GetEnumerator();

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

			public void ToHtml(TextWriter writer, IMarkupFormatter formatter)
			{
				foreach (var node in _nodes)
					node.ToHtml(writer, formatter);
			}
		}
	}
}

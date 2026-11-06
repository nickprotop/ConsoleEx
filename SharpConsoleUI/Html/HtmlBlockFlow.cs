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
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Block-level layout engine for HTML rendering.
	/// Handles paragraphs, headings, lists, blockquotes, pre, hr, div, and
	/// delegates inline content to HtmlInlineFlow.
	/// </summary>
	public static partial class HtmlBlockFlow
	{
		private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
		{
			"p", "div", "h1", "h2", "h3", "h4", "h5", "h6",
			"hr", "br", "ul", "ol", "li", "blockquote", "pre", "table",
			"section", "article", "header", "footer", "nav", "main", "aside",
			"figure", "figcaption", "details", "summary", "dl", "dt", "dd",

			// Deprecated in HTML, and common on exactly the older, lighter pages a
			// terminal browser gets pointed at. Missing here it fell through to the
			// inline path, where a block child has no representation and was simply
			// dropped: Hacker News wraps its whole body in <center><table> and
			// rendered as three blank rows, with no error to explain it.
			"center",

			// Same class of omission, found while checking for others: all are
			// block-level, all appear on real pages, and all would lose any block
			// content nested inside them.
			"fieldset", "form", "address", "caption", "figure", "noframes"
		};

		/// <summary>
		/// Whether a tag is laid out as a block rather than flowed inline.
		/// </summary>
		/// <param name="tag">The element's local name; case does not matter.</param>
		/// <returns>True when the tag starts its own line.</returns>
		/// <remarks>
		/// Exposed so the table layout can ask the same question with the same
		/// answer. A second copy of this list would drift, and the drift would show
		/// up as content silently vanishing inside a cell.
		/// </remarks>
		public static bool IsBlockTag(string? tag) =>
			tag != null && BlockTags.Contains(tag);

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
			Dictionary<string, Imaging.PixelBuffer?>? imageCache = null,
			IDocument? cssDocument = null,
			Drivers.IGraphicsProtocol? graphicsProtocol = null,
			string? documentUrl = null)
		{
			var ctx = new BlockContext
			{
				DocumentUrl = documentUrl,
				MaxWidth = maxWidth,
				DefaultFg = defaultFg,
				DefaultBg = defaultBg,
				BlockSpacing = blockSpacing,
				LinkColor = linkColor,
				VisitedLinkColor = visitedLinkColor,
				ShowImages = showImages,
				ImageCache = imageCache,
				CssDocument = cssDocument,
				GraphicsProtocol = graphicsProtocol,
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

			void FlowText(INodeList nodes)
			{
				var lines = HtmlInlineFlow.FlowInline(
					nodes,
					effectiveWidth,
					style.Foreground,
					style.Background,
					alignment: style.Alignment,
					linkColor: ctx.LinkColor,
					visitedLinkColor: ctx.VisitedLinkColor);

				foreach (var line in lines)
					ctx.AddLine(line, indent);
			}

			if (!FlowInlineWithImages(element.ChildNodes, ctx, indent, FlowText))
				FlowText(element.ChildNodes);

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

			void FlowText(INodeList nodes)
			{
				var lines = HtmlInlineFlow.FlowInline(
					nodes,
					effectiveWidth,
					style.Foreground,
					style.Background,
					inheritedDecorations: headingDecorations,
					alignment: style.Alignment,
					linkColor: ctx.LinkColor,
					visitedLinkColor: ctx.VisitedLinkColor);

				foreach (var line in lines)
					ctx.AddLine(line, indent);
			}

			if (!FlowInlineWithImages(element.ChildNodes, ctx, indent, FlowText))
				FlowText(element.ChildNodes);

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

				// The bullet belongs on the first line of the item's text. An item
				// may now emit several runs with pictures between them, so "first"
				// is tracked across the whole item rather than per run - otherwise
				// a bullet would be repeated after every picture.
				bool prefixPending = true;

				void FlowText(INodeList nodes)
				{
					var lines = HtmlInlineFlow.FlowInline(
						nodes,
						effectiveWidth,
						ctx.DefaultFg,
						ctx.DefaultBg,
						linkColor: ctx.LinkColor,
						visitedLinkColor: ctx.VisitedLinkColor);

					foreach (var flowed in lines)
					{
						var line = flowed;
						if (prefixPending)
						{
							prefixPending = false;

							// First line: prepend bullet/number prefix
							var prefixCells = new Cell[prefix.Length];
							for (int j = 0; j < prefix.Length; j++)
								prefixCells[j] = new Cell(prefix[j], ctx.DefaultFg, ctx.DefaultBg);

							var combined = new Cell[prefixCells.Length + line.Cells.Length];
							Array.Copy(prefixCells, 0, combined, 0, prefixCells.Length);
							Array.Copy(line.Cells, 0, combined, prefixCells.Length, line.Cells.Length);

							// Only this line gains the bullet, so only this line's
							// regions move. The item's later lines are added below
							// unchanged, which is right: nothing was put in front
							// of them.
							var bulletIndent = indent;
							line = new LayoutLine(0, bulletIndent, combined.Length, combined, line.Alignment,
								ShiftLinks(line.Links, prefixCells.Length), line.IsImage,
								line.ContinuesNextLine, line.BrokeBetweenWords);
							ctx.AddLine(line, bulletIndent);
						}
						else
						{
							ctx.AddLine(line, itemIndent);
						}
					}
				}

				var wrapper = new NodeListWrapper(inlineNodes);
				if (!FlowInlineWithImages(inlineNodes, ctx, itemIndent, FlowText))
					FlowText(wrapper);
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
				CssDocument = ctx.CssDocument,
				DocumentUrl = ctx.DocumentUrl,
			};

			ProcessChildren(element, innerCtx, bqIndent, listDepth);
			FlushInline(innerCtx, bqIndent);

			// Add blockquote bar to each line
			foreach (var line in innerCtx.Lines)
			{
				// Prepend the blockquote bar at the indent position
				var barCell = new Cell(HtmlConstants.BlockquoteBar, HtmlConstants.DefaultBlockquoteBarColor, ctx.DefaultBg);
				var spacer = new Cell(' ', bqFg, ctx.DefaultBg);

				const int barWidth = 2; // the bar and the space after it

				var newCells = new Cell[line.Cells.Length + barWidth];
				newCells[0] = barCell;
				newCells[1] = spacer;
				Array.Copy(line.Cells, 0, newCells, barWidth, line.Cells.Length);

				// IsImage travels too. A half-block image row carries one pixel in
				// the glyph foreground and another in its background, and the focus
				// highlight reads this flag to know it must leave those colours
				// alone. Dropped here, a linked picture inside a blockquote was
				// scrambled the moment it took focus.
				var newLine = new LayoutLine(0, indent, newCells.Length, newCells,
					line.Alignment, ShiftLinks(line.Links, barWidth), line.IsImage,
					line.ContinuesNextLine, line.BrokeBetweenWords);
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

			// Code-block background derived from the page background (subtle on light AND dark themes).
			Color codeBg = ctx.DefaultBg.IsDark() ? ctx.DefaultBg.Tint(0.10) : ctx.DefaultBg.Shade(0.08);
			foreach (var rawLine in rawLines)
			{
				// A line too wide for the viewport continues onto further rows
				// rather than losing its tail. TextToCells stops at the edge,
				// which is right where a caller wants one row and no more, but
				// here it meant the characters past that column were never laid
				// out at all - not clipped at paint time, where scrolling could
				// still reach them, but absent. Most code is wider than a terminal
				// window, and the end of a line of code is usually the part that
				// matters.
				//
				// This is not reflow: no break is chosen by fitting words, nothing
				// is joined, and the author's own breaks still start new lines. It
				// is what a terminal does with a line too long for it.
				var rows = TextToRows(rawLine, effectiveWidth, ctx.DefaultFg, codeBg);
				for (int i = 0; i < rows.Count; i++)
				{
					// Every row but the last continues the same source line. The
					// author wrote one line; the viewport broke it. Copying it as
					// several would insert newlines that were never in the code.
					var line = new LayoutLine(0, indent, rows[i].Length, rows[i], TextAlignment.Left,
						links: null, isImage: false, continuesNextLine: i < rows.Count - 1);
					ctx.AddLine(line, indent);
				}
			}
		}

		private static void ProcessTable(
			IElement element,
			BlockContext ctx,
			int indent)
		{
			var availWidth = ctx.MaxWidth - indent;
			if (availWidth <= 0) availWidth = 1;

			// A cell can hold a picture, and rendering one needs the same context
			// the block flow carries. Handed over only when images are on, so a
			// text-only page takes the path it always did.
			var images = ctx.ShowImages
				? new HtmlTableLayout.ImageContext
				{
					ImageCache = ctx.ImageCache,
					CssDocument = ctx.CssDocument,
					GraphicsProtocol = ctx.GraphicsProtocol,
					DocumentUrl = ctx.DocumentUrl,
					BlockSpacing = ctx.BlockSpacing,
				}
				: null;

			var tableLines = HtmlTableLayout.LayoutTable(element, availWidth, ctx.DefaultFg, ctx.DefaultBg,
				ctx.LinkColor, ctx.VisitedLinkColor, images);
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

			// A wrapper that renders nothing costs nothing. A page is built from
			// layout elements that exist to position their children on a screen;
			// in a terminal they position nothing, and charging rows for one is
			// paying for a service not rendered. Remembered here and compared
			// after the children have run.
			int linesBefore = ctx.Lines.Count;

			// Margin above, requested in pixels and spent in rows. Capped: a page
			// says 20px meaning a small gap on a tall screen, and a row is worth
			// far more than a pixel. Uncapped, and multiplied by the dozens of
			// nested wrappers a modern page is built from, a real article came out
			// thirty six percent blank with runs of fourteen empty rows.
			int topSpacing = CollapseSpacing(style.MarginTop + style.PaddingTop);
			AddSpacing(ctx, totalIndent, topSpacing);

			// Where this element's own content would start, so "did it render
			// anything" asks about the content and not about its own margin.
			int contentStart = ctx.Lines.Count;

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

			// Nothing between the two marks means this element rendered nothing,
			// so its margins buy nothing either. Take back the leading spacing as
			// well: an empty wrapper should leave the page exactly as it found it.
			if (ctx.Lines.Count == contentStart)
			{
				ctx.TruncateTo(linesBefore);
				return;
			}

			AddSpacing(ctx, totalIndent, CollapseSpacing(style.MarginBottom + style.PaddingBottom));
		}

		/// <summary>
		/// How many rows a block's requested vertical space is worth.
		/// </summary>
		/// <remarks>
		/// <para>
		/// A page asks in pixels, for a screen hundreds of pixels tall. A terminal
		/// row is worth far more than a pixel, so the request is honoured in kind
		/// rather than in amount: any margin at all becomes one row, and that is
		/// the whole of it.
		/// </para>
		/// <para>
		/// One row is enough to separate two blocks, which is what the margin is
		/// for. More is what turned an article into a page the reader scrolls
		/// through hunting for the text.
		/// </para>
		/// </remarks>
		private static int CollapseSpacing(int requested) => requested > 0 ? 1 : 0;

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
		/// <summary>
		/// Lays text out as one or more rows, continuing rather than truncating.
		/// </summary>
		/// <remarks>
		/// <para>
		/// For preformatted text, where every character has to survive and none
		/// may be reordered. A run too wide for the viewport continues on the next
		/// row at the exact character where it ran out of room.
		/// </para>
		/// <para>
		/// A wide glyph is never split across the boundary: two columns cannot be
		/// halved, so it moves to the next row whole rather than leaving a stray
		/// continuation cell behind.
		/// </para>
		/// <para>
		/// Always returns at least one row, so a blank line in a code block stays
		/// a blank line.
		/// </para>
		/// </remarks>
		/// <param name="text">One source line, already split on newlines.</param>
		/// <param name="maxColumns">Columns available to each row.</param>
		/// <param name="fg">Foreground colour.</param>
		/// <param name="bg">Background colour.</param>
		/// <param name="decorations">Decorations applied to every cell.</param>
		/// <returns>The rows the text occupies, in order.</returns>
		private static List<Cell[]> TextToRows(string text, int maxColumns, Color fg, Color bg,
			TextDecoration decorations = TextDecoration.None)
		{
			if (maxColumns < 1)
				maxColumns = 1;

			var rows = new List<Cell[]>();
			var current = new List<Cell>();
			int col = 0;

			foreach (var rune in text.EnumerateRunes())
			{
				if (rune.Value == '\r') continue;

				var safe = Helpers.TextSanitizer.IsUnsafeRune(rune)
					? Helpers.TextSanitizer.ReplacementCharacter : rune;
				int rw = Helpers.UnicodeWidth.GetRuneWidth(safe);
				if (rw == 0) continue;

				// No room for this glyph: start a row rather than drop it. A wide
				// glyph that would straddle the edge moves whole.
				if (col + rw > maxColumns && current.Count > 0)
				{
					rows.Add(current.ToArray());
					current = new List<Cell>();
					col = 0;
				}

				current.Add(new Cell(safe, fg, bg, decorations));
				if (rw == 2)
					current.Add(new Cell(' ', fg, bg) { IsWideContinuation = true });
				col += rw;
			}

			// The last row, and the only row of an empty line: a blank line in a
			// code block is part of the code.
			rows.Add(current.ToArray());
			return rows;
		}

		private static Cell[] TextToCells(string text, int maxColumns, Color fg, Color bg, TextDecoration decorations = TextDecoration.None)
		{
			var cells = new List<Cell>();
			int col = 0;
			foreach (var rune in text.EnumerateRunes())
			{
				if (rune.Value == '\r') continue;
				var safe = Helpers.TextSanitizer.IsUnsafeRune(rune)
					? Helpers.TextSanitizer.ReplacementCharacter : rune;
				int rw = Helpers.UnicodeWidth.GetRuneWidth(safe);
				if (col + rw > maxColumns) break;
				cells.Add(new Cell(safe, fg, bg, decorations));
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
			public IDocument? CssDocument;

			/// <summary>
			/// Where this document came from, or null when the application supplied it
			/// directly. Decides whether its images may be read from local files.
			/// </summary>
			public string? DocumentUrl;
			public Drivers.IGraphicsProtocol? GraphicsProtocol;

			public List<LayoutLine> Lines = new();
			public List<INode> InlineBuffer = new();

			public void AddLine(LayoutLine line, int indent)
			{
				// A blank line arriving from a nested layout collapses the same
				// way one asked for directly does. Grid and table rows are padded
				// to their full width, so a visually empty row carries a hundred
				// spaces rather than no cells: a page built out of layout tables
				// kept long runs of them where its structure had been.
				//
				// An image row is never blank, whatever its cells look like: a
				// half-block glyph is not whitespace.
				if (!line.IsImage && IsBlank(line) && Lines.Count > 0 && IsBlank(Lines[^1]))
					return;

				line.Y = Lines.Count;
				line.X = indent;
				Lines.Add(line);
			}

			/// <summary>
			/// Drops every line added after a mark, undoing an element that
			/// turned out to render nothing.
			/// </summary>
			public void TruncateTo(int count)
			{
				if (count >= 0 && count < Lines.Count)
					Lines.RemoveRange(count, Lines.Count - count);
			}

			/// <summary>
			/// Adds a blank line, unless the page is already blank here.
			/// </summary>
			/// <remarks>
			/// <para>
			/// Blank lines come from many places at once - a block's own spacing,
			/// the margins of every wrapper around it, the padding of each one -
			/// and none of them can see the others. Each asks for a reasonable
			/// gap; together they produced runs of fourteen empty rows, and a real
			/// article that was thirty six percent blank.
			/// </para>
			/// <para>
			/// Collapsing here rather than at each caller is what makes it hold:
			/// the question "is there already a gap" has one answer, and only the
			/// thing holding the lines can give it. A gap stays a gap; a gap does
			/// not become a screenful.
			/// </para>
			/// <para>
			/// Leading blanks are dropped for the same reason: a page that opens
			/// with nested wrappers should not open with empty rows.
			/// </para>
			/// </remarks>
			public void AddEmptyLine(int indent)
			{
				if (Lines.Count == 0)
					return;

				if (IsBlank(Lines[^1]))
					return;

				var line = new LayoutLine(Lines.Count, indent, 0, Array.Empty<Cell>(), TextAlignment.Left);
				Lines.Add(line);
			}

			/// <summary>
			/// Whether a line shows nothing, however many cells it holds.
			/// </summary>
			/// <remarks>
			/// A line of spaces is as blank to a reader as a line of no cells.
			/// Table and grid rows arrive padded to their full width, so asking
			/// only about the cell count missed them and a page kept its runs of
			/// empty rows where a layout table had been.
			/// </remarks>
			internal static bool IsBlank(LayoutLine line)
			{
				foreach (var cell in line.Cells)
				{
					if (!char.IsWhiteSpace((char)cell.Character.Value))
						return false;
				}

				return true;
			}
		}

		/// <summary>
		/// Moves every link region right by the cells prepended in front of it.
		/// </summary>
		/// <remarks>
		/// <para>
		/// A line that gains a prefix - a blockquote's bar, a list item's bullet -
		/// is rebuilt with those cells in front, and everything after them moves.
		/// A link region is a pair of column numbers into that same line, so it
		/// has to move too.
		/// </para>
		/// <para>
		/// Left where it was, a region covered the prefix and lost the end of its
		/// own word: a link reading "LINK" after a bullet highlighted "&#8226; LI"
		/// and a click on its last characters landed on nothing. Both rebuild
		/// sites had this, so a link in any bulleted list - which is most link
		/// lists on a page - had a click target two columns off.
		/// </para>
		/// </remarks>
		/// <param name="links">The line's regions, or null when it has none.</param>
		/// <param name="offset">How many cells were prepended.</param>
		/// <returns>The shifted regions, or null when there were none.</returns>
		private static LinkRegion[]? ShiftLinks(LinkRegion[]? links, int offset)
		{
			if (links == null || offset == 0)
				return links;

			var shifted = new LinkRegion[links.Length];
			for (int i = 0; i < links.Length; i++)
			{
				var link = links[i];
				shifted[i] = new LinkRegion(
					link.StartX + offset,
					link.EndX + offset,
					link.Url,
					link.Text,
					link.LinkId);
			}

			return shifted;
		}

		/// <summary>
		/// Lays out a run of inline nodes that holds a picture, as text rows and
		/// image rows in the order they were written.
		/// </summary>
		/// <remarks>
		/// For a caller that has a node run rather than an element - a table cell,
		/// whose content is split into parts before it is laid out. The block flow
		/// is the only thing that can emit image rows, so a cell holding a picture
		/// comes here instead of to the inline flow, and gets the same text,
		/// picture, text treatment a paragraph gets.
		/// </remarks>
		/// <param name="nodes">The run of inline nodes.</param>
		/// <param name="maxWidth">Columns available to the run.</param>
		/// <param name="defaultFg">Foreground for the text.</param>
		/// <param name="defaultBg">Background for the text.</param>
		/// <param name="blockSpacing">Blank lines between blocks.</param>
		/// <param name="linkColor">Colour for unvisited links.</param>
		/// <param name="visitedLinkColor">Colour for visited links.</param>
		/// <param name="imageCache">Pictures already decoded.</param>
		/// <param name="cssDocument">The page's stylesheet, for declared widths.</param>
		/// <param name="graphicsProtocol">The terminal's graphics protocol, if any.</param>
		/// <param name="documentUrl">The document's URL, which decides local-file access.</param>
		/// <param name="inheritedDecorations">Decorations the container imposes, such as a header's weight.</param>
		/// <returns>The run's lines, text and picture alike.</returns>
		internal static List<LayoutLine> FlowInlineRunWithImages(
			INodeList nodes,
			int maxWidth,
			Color defaultFg,
			Color defaultBg,
			int blockSpacing,
			Color? linkColor,
			Color? visitedLinkColor,
			Dictionary<string, Imaging.PixelBuffer?>? imageCache,
			IDocument? cssDocument,
			Drivers.IGraphicsProtocol? graphicsProtocol,
			string? documentUrl,
			TextDecoration inheritedDecorations)
		{
			var ctx = new BlockContext
			{
				DocumentUrl = documentUrl,
				MaxWidth = maxWidth,
				DefaultFg = defaultFg,
				DefaultBg = defaultBg,
				BlockSpacing = blockSpacing,
				LinkColor = linkColor,
				VisitedLinkColor = visitedLinkColor,
				ShowImages = true,
				ImageCache = imageCache,
				CssDocument = cssDocument,
				GraphicsProtocol = graphicsProtocol,
			};

			void FlowText(INodeList run)
			{
				var flowed = HtmlInlineFlow.FlowInline(run, maxWidth, defaultFg, defaultBg,
					inheritedDecorations, TextAlignment.Left, linkColor, visitedLinkColor);

				foreach (var line in flowed)
					ctx.AddLine(line, 0);
			}

			if (!FlowInlineWithImages(nodes, ctx, indent: 0, FlowText))
				FlowText(nodes);

			return ctx.Lines;
		}

		/// <summary>
		/// Splits a run of inline nodes around any image that will render, and
		/// emits the pieces in order: text, picture, text.
		/// </summary>
		/// <remarks>
		/// <para>
		/// An image is laid out as block rows, and the inline flow cannot emit
		/// rows - its <c>img</c> case writes alt text. So a container that handed
		/// all its children to the inline flow in one call turned every picture
		/// inside it into alt text, and whether a picture appeared depended on
		/// which element it happened to sit in. This is the one place that knows
		/// how to interleave the two, so every such container behaves the same.
		/// </para>
		/// <para>
		/// A terminal cell is twice as tall as it is wide, so a picture cannot sit
		/// within a line of text and stay recognisable. It takes its own rows at
		/// the point in the text where it was written, which keeps reading order
		/// intact - the alternative, hoisting pictures out of the paragraph, moves
		/// them away from the sentence that introduces them.
		/// </para>
		/// <para>
		/// Returns false when there is nothing to split, so the caller keeps its
		/// own single-call path. That matters beyond speed: a split costs a line
		/// break, and a paragraph with no renderable picture must still lay out
		/// exactly as it did before.
		/// </para>
		/// </remarks>
		/// <param name="nodes">The inline nodes of one container.</param>
		/// <param name="ctx">The block context images are emitted into.</param>
		/// <param name="indent">Indent for the emitted image rows.</param>
		/// <param name="flowText">
		/// Emits one run of text nodes. Called once per run between pictures, and
		/// never with an empty run, so a container holding only a picture gains no
		/// blank line.
		/// </param>
		/// <returns>True if the run was split and emitted; false if it holds no renderable image.</returns>
		private static bool FlowInlineWithImages(
			IEnumerable<INode> nodes,
			BlockContext ctx,
			int indent,
			Action<INodeList> flowText)
		{
			var all = nodes.ToList();

			// Only an image that will actually render is worth a break. ShowImages
			// off, or a missing src, means alt text in the text run as before.
			//
			// A picture is routinely wrapped - <a>, <span>, <figure> - so the test
			// is "holds a renderable image", not "is one". Without that, a linked
			// thumbnail inside a paragraph stayed alt text, which is how an
			// encyclopedia writes nearly all of them.
			static IElement[] ImagesOf(INode node)
			{
				if (node is not IElement element)
					return Array.Empty<IElement>();

				if (element.LocalName.Equals("img", StringComparison.OrdinalIgnoreCase))
					return string.IsNullOrEmpty(element.GetAttribute("src"))
						? Array.Empty<IElement>()
						: new[] { element };

				return element.QuerySelectorAll("img[src]").ToArray();
			}

			bool Renders(INode node) => ctx.ShowImages && ImagesOf(node).Length > 0;

			if (!all.Any(Renders))
				return false;

			var run = new List<INode>();

			void FlushRun()
			{
				if (run.Count == 0)
					return;

				flowText(new NodeListWrapper(run));
				run.Clear();
			}

			foreach (var node in all)
			{
				var images = ctx.ShowImages ? ImagesOf(node) : Array.Empty<IElement>();
				if (images.Length > 0)
				{
					FlushRun();
					foreach (var image in images)
						ProcessImage(image, ctx, indent);
					continue;
				}

				run.Add(node);
			}

			FlushRun();
			return true;
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

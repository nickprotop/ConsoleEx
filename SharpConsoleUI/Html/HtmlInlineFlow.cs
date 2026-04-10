// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using AngleSharp.Dom;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Renders inline HTML nodes (text, b, i, a, code, img, span, br, etc.)
	/// into Cell arrays with word-wrapping.
	/// </summary>
	public static class HtmlInlineFlow
	{
		private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
		{
			"div", "p", "h1", "h2", "h3", "h4", "h5", "h6",
			"ul", "ol", "li", "blockquote", "pre", "hr", "table",
			"thead", "tbody", "tfoot", "tr", "td", "th",
			"section", "article", "aside", "nav", "header", "footer",
			"main", "figure", "figcaption", "details", "summary",
			"dl", "dt", "dd", "form", "fieldset"
		};

		/// <summary>
		/// Flows inline nodes into wrapped LayoutLines.
		/// </summary>
		public static List<LayoutLine> FlowInline(
			INodeList nodes,
			int maxWidth,
			Color defaultFg,
			Color defaultBg,
			TextDecoration inheritedDecorations = TextDecoration.None,
			TextAlignment alignment = TextAlignment.Left,
			Color? linkColor = null,
			Color? visitedLinkColor = null)
		{
			if (maxWidth <= 0)
				maxWidth = 1;

			var effectiveLinkColor = linkColor ?? HtmlConstants.DefaultLinkColor;
			var ctx = new FlowContext
			{
				MaxWidth = maxWidth,
				DefaultFg = defaultFg,
				DefaultBg = defaultBg,
				LinkColor = effectiveLinkColor,
				VisitedLinkColor = visitedLinkColor ?? HtmlConstants.DefaultVisitedLinkColor,
				Alignment = alignment,
			};

			WalkNodes(nodes, defaultFg, defaultBg, inheritedDecorations, null, ctx);

			// Finalize the last line
			ctx.FinishLine();

			return ctx.Lines;
		}

		private static void WalkNodes(
			INodeList nodes,
			Color fg,
			Color bg,
			TextDecoration decorations,
			string? linkUrl,
			FlowContext ctx)
		{
			foreach (var node in nodes)
			{
				if (node is IText textNode)
				{
					EmitText(textNode.Data, fg, bg, decorations, linkUrl, ctx);
				}
				else if (node is IElement element)
				{
					var tag = element.LocalName.ToLowerInvariant();

					// Skip non-rendering elements
					if (tag is "script" or "style" or "noscript")
						continue;

					// Skip block elements encountered inline
					if (BlockTags.Contains(tag))
						continue;

					// Resolve style
					var style = HtmlStyleResolver.Resolve(element, fg, bg);
					if (style.IsHidden)
						continue;

					var childFg = style.Foreground;
					var childBg = style.Background;
					var childDecorations = decorations | style.Decorations;
					var childLinkUrl = linkUrl;

					switch (tag)
					{
						case "br":
							ctx.FinishLine();
							continue;

						case "a":
							var href = element.GetAttribute("href") ?? "";
							childFg = ctx.LinkColor;
							childDecorations |= TextDecoration.Underline;
							childLinkUrl = href;
							break;

						case "code":
							childBg = HtmlConstants.DefaultCodeBackground;
							break;

						case "img":
							var alt = element.GetAttribute("alt") ?? "image";
							var imgText = HtmlConstants.ImageAltPrefix + alt + HtmlConstants.ImageAltSuffix;
							var imgDecorations = decorations | TextDecoration.Dim | TextDecoration.Italic;
							EmitText(imgText, fg, bg, imgDecorations, linkUrl, ctx, collapseWhitespace: false);
							continue;
					}

					WalkNodes(element.ChildNodes, childFg, childBg, childDecorations, childLinkUrl, ctx);
				}
			}
		}

		private static void EmitText(
			string text,
			Color fg,
			Color bg,
			TextDecoration decorations,
			string? linkUrl,
			FlowContext ctx,
			bool collapseWhitespace = true)
		{
			if (string.IsNullOrEmpty(text))
				return;

			string processed;
			if (collapseWhitespace)
			{
				// Collapse runs of whitespace to single space
				processed = CollapseWhitespace(text);
				if (processed.Length == 0)
					return;
			}
			else
			{
				processed = text;
			}

			// Split into words for wrapping
			var segments = SplitIntoWordSegments(processed);

			foreach (var segment in segments)
			{
				if (segment.IsSpace)
				{
					// Don't add leading space on a new empty line
					if (ctx.CurrentX == 0)
						continue;

					if (ctx.CurrentX >= ctx.MaxWidth)
					{
						ctx.FinishLine();
						continue;
					}

					ctx.AddCell(new Cell(' ', fg, bg, decorations), linkUrl);
				}
				else
				{
					var word = segment.Text;
					var runes = word.EnumerateRunes().ToArray();
					int wordWidth = runes.Sum(r => Helpers.UnicodeWidth.GetRuneWidth(r));

					// Check if word fits on current line
					if (ctx.CurrentX > 0 && ctx.CurrentX + wordWidth > ctx.MaxWidth)
					{
						// Wrap to next line
						ctx.FinishLine();
					}

					// If word is longer than maxWidth, break character by character
					if (wordWidth > ctx.MaxWidth)
					{
						foreach (var rune in runes)
						{
							int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
							if (ctx.CurrentX + rw > ctx.MaxWidth)
								ctx.FinishLine();
							ctx.AddCell(new Cell(rune, fg, bg, decorations), linkUrl);
						}
					}
					else
					{
						foreach (var rune in runes)
						{
							ctx.AddCell(new Cell(rune, fg, bg, decorations), linkUrl);
						}
					}
				}
			}
		}

		private static string CollapseWhitespace(string text)
		{
			var sb = new System.Text.StringBuilder(text.Length);
			bool lastWasSpace = false;
			foreach (var ch in text)
			{
				if (char.IsWhiteSpace(ch))
				{
					if (!lastWasSpace)
					{
						sb.Append(' ');
						lastWasSpace = true;
					}
				}
				else
				{
					sb.Append(ch);
					lastWasSpace = false;
				}
			}
			return sb.ToString();
		}

		private struct WordSegment
		{
			public string Text;
			public bool IsSpace;
		}

		private static List<WordSegment> SplitIntoWordSegments(string text)
		{
			var segments = new List<WordSegment>();
			int i = 0;
			while (i < text.Length)
			{
				if (text[i] == ' ')
				{
					segments.Add(new WordSegment { Text = " ", IsSpace = true });
					i++;
				}
				else
				{
					int start = i;
					while (i < text.Length && text[i] != ' ')
						i++;
					segments.Add(new WordSegment { Text = text.Substring(start, i - start), IsSpace = false });
				}
			}
			return segments;
		}

		private class FlowContext
		{
			public int MaxWidth;
			public Color DefaultFg;
			public Color DefaultBg;
			public Color LinkColor;
			public Color VisitedLinkColor;
			public TextAlignment Alignment;

			public List<LayoutLine> Lines = new();
			public List<Cell> CurrentCells = new();
			public int CurrentX;
			public int LineY;

			// Link tracking for the current line
			private List<LinkRegion> _currentLineLinks = new();
			private string? _currentLinkUrl;
			private int _currentLinkStartX = -1;
			private string _currentLinkText = "";
			private int _currentLinkId;
			private int _nextLinkId;

			public void AddCell(Cell cell, string? linkUrl)
			{
				// Handle link region transitions
				if (linkUrl != _currentLinkUrl)
				{
					// Close previous link region if any
					CloseLinkRegion();

					// Start new link region
					if (linkUrl != null)
					{
						_currentLinkUrl = linkUrl;
						_currentLinkStartX = CurrentX;
						_currentLinkText = "";
						_currentLinkId = _nextLinkId++;
					}
				}

				if (linkUrl != null)
				{
					_currentLinkText += cell.Character.ToString();
				}

				int runeWidth = Helpers.UnicodeWidth.GetRuneWidth(cell.Character);
				CurrentCells.Add(cell);
				// Wide characters (CJK, emoji) need a continuation cell for the second column
				if (runeWidth == 2)
				{
					CurrentCells.Add(new Cell(' ', cell.Foreground, cell.Background) { IsWideContinuation = true });
				}
				CurrentX += runeWidth;
			}

			public void FinishLine()
			{
				// Close any open link region for this line
				CloseLinkRegion();

				var cells = CurrentCells.ToArray();
				LinkRegion[]? links = _currentLineLinks.Count > 0 ? _currentLineLinks.ToArray() : null;

				Lines.Add(new LayoutLine(LineY, 0, cells.Length, cells, Alignment, links));

				LineY++;
				CurrentCells.Clear();
				CurrentX = 0;
				_currentLineLinks = new List<LinkRegion>();

				// If we were in a link, start a new region on the new line
				if (_currentLinkUrl != null)
				{
					var url = _currentLinkUrl;
					_currentLinkUrl = url;
					_currentLinkStartX = 0;
					// Keep accumulating text
				}
			}

			private void CloseLinkRegion()
			{
				if (_currentLinkUrl != null && _currentLinkStartX >= 0 && CurrentX > _currentLinkStartX)
				{
					_currentLineLinks.Add(new LinkRegion(
						_currentLinkStartX,
						CurrentX,
						_currentLinkUrl,
						_currentLinkText,
						_currentLinkId));
				}

				if (_currentLinkUrl == null)
				{
					_currentLinkStartX = -1;
					_currentLinkText = "";
				}
			}
		}
	}
}

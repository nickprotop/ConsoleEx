// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Highlighting;

namespace SharpConsoleUI.Parsing
{
	/// <summary>
	/// Translates a Markdown string into the library's native markup (the same dialect
	/// <see cref="MarkupParser"/> renders). Pure and stateless. Emphasis and headings emit
	/// colorless structural tags (they inherit the surrounding markup scope); only code,
	/// quotes, links and table borders carry colors, taken from <see cref="MarkdownStyle"/>.
	/// </summary>
	public static class MarkdownToMarkup
	{
		private static readonly MarkdownPipeline Pipeline =
			new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

		/// <summary>
		/// Converts <paramref name="markdown"/> to native markup.
		/// </summary>
		/// <param name="markdown">The Markdown source.</param>
		/// <param name="style">Style overrides; defaults to <see cref="MarkdownStyle.Default"/>.</param>
		/// <param name="availableWidth">Display columns available for rendering; tables are fitted/wrapped to
		/// this width. <see cref="int.MaxValue"/> (the default) means unbounded — tables render at natural width.</param>
		/// <returns>A native-markup string suitable for <see cref="MarkupParser.Parse(string, Color, Color)"/>.</returns>
		public static string Convert(string markdown, MarkdownStyle? style = null, int availableWidth = int.MaxValue)
		{
			if (string.IsNullOrEmpty(markdown))
				return string.Empty;

			style ??= MarkdownStyle.Default;
			var doc = Markdown.Parse(markdown, Pipeline);
			var sb = new StringBuilder(markdown.Length);
			WriteBlocks(doc, sb, style, availableWidth);
			return sb.ToString().TrimEnd('\n');
		}

		private static void WriteBlocks(IEnumerable<Block> blocks, StringBuilder sb, MarkdownStyle style, int availableWidth)
			=> WriteBlocks(blocks, sb, style, 0, availableWidth);

		private static void WriteBlocks(IEnumerable<Block> blocks, StringBuilder sb, MarkdownStyle style, int indent, int availableWidth = int.MaxValue)
		{
			foreach (var block in blocks)
				WriteBlock(block, sb, style, indent, availableWidth);
		}

		private static void WriteBlock(Block block, StringBuilder sb, MarkdownStyle style, int indent, int availableWidth = int.MaxValue)
		{
			switch (block)
			{
				case ParagraphBlock p:
					Indent(sb, indent);
					WriteInlines(p.Inline, sb, style);
					sb.Append('\n');
					break;

				case HeadingBlock h:
					Indent(sb, indent);
					WriteHeading(h, sb, style);
					break;

				case ThematicBreakBlock:
					{
						// Fill the available render width (minus indent) so the rule spans the panel, like a
						// real Markdown horizontal rule. When width is unbounded (non-wrap Parse path) there is
						// no width to fill, so fall back to the fixed default. (Issue #59.)
						int ruleWidth = availableWidth == int.MaxValue
							? ControlDefaults.MarkdownRuleWidth
							: Math.Max(1, availableWidth - indent);
						Indent(sb, indent);
						sb.Append(new string('─', ruleWidth)).Append('\n');
						break;
					}

				case ListBlock list:
					{
						int number = 1;
						foreach (var itemObj in list)
						{
							if (itemObj is ListItemBlock li)
							{
								Indent(sb, indent);
								sb.Append(list.IsOrdered ? $"{number}. " : $"{style.BulletGlyph} ");
								WriteListItem(li, sb, style, indent + style.ListIndent);
								number++;
							}
						}
						break;
					}

				case QuoteBlock quote:
					foreach (var child in quote)
					{
						if (child is ParagraphBlock qp)
						{
							var inner = new System.Text.StringBuilder();
							WriteInlines(qp.Inline, inner, style);
							string quoteOpen = $"[{ColorWord(style.QuoteColor)}]";
							foreach (var qline in inner.ToString().Split('\n'))
							{
								Indent(sb, indent);
								sb.Append(quoteOpen).Append(style.QuoteGlyph).Append(' ')
								  .Append(qline).Append("[/]\n");
							}
						}
						else
						{
							WriteBlock(child, sb, style, indent + style.ListIndent, availableWidth);
						}
					}
					break;

				case Markdig.Extensions.Tables.Table table:
					WriteTable(table, sb, style, indent, availableWidth);
					break;

				case CodeBlock codeBlock:
					{
						string? lang = (codeBlock as Markdig.Syntax.FencedCodeBlock)?.Info?.Trim();
						ISyntaxHighlighter? hl = ResolveCodeHighlighter(lang, style);

						string flatTag = $"[{ColorWord(style.CodeForeground)} on {ColorWord(style.CodeBackground)}]";
						string bgWord = ColorWord(style.CodeBackground);
						string defaultFgWord = ColorWord(style.CodeForeground);
						var codeLines = codeBlock.Lines;
						SyntaxLineState state = SyntaxLineState.Initial;

						for (int li = 0; li < codeLines.Count; li++)
						{
							string raw = codeLines.Lines[li].Slice.ToString();
							Indent(sb, indent);

							if (hl == null)
							{
								sb.Append(flatTag).Append(' ').Append(MarkupParser.Escape(raw)).Append(' ').Append("[/]").Append("[fillwidth]").Append('\n');
								continue;
							}

							var (tokens, endState) = hl.Tokenize(raw, li, state);
							state = endState;
							AppendHighlightedLine(sb, raw, tokens, bgWord, defaultFgWord);
						}
						break;
					}

				default:
					// Unhandled block constructs are skipped.
					break;
			}
		}

		private static void WriteHeading(HeadingBlock h, StringBuilder sb, MarkdownStyle style)
		{
			Color? headingColor = h.Level switch
			{
				1 => style.H1Color,
				2 => style.H2Color,
				3 => style.H3Color,
				4 => style.H4Color,
				5 => style.H5Color,
				_ => style.H6Color,
			};
			string weight = h.Level <= 1 ? "bold underline"
						  : h.Level <= 3 ? "bold"
						  : "dim bold";
			string open = headingColor is Color hc
				? $"[{weight} {ColorWord(hc)}]"
				: $"[{weight}]";
			sb.Append(open);
			WriteInlines(h.Inline, sb, style);
			sb.Append("[/]\n\n");
		}

		private static void WriteListItem(ListItemBlock item, StringBuilder sb, MarkdownStyle style, int childIndent)
		{
			bool firstParagraph = true;
			foreach (var child in item)
			{
				if (child is ParagraphBlock p && firstParagraph)
				{
					WriteInlines(p.Inline, sb, style); // flows right after the marker
					sb.Append('\n');
					firstParagraph = false;
				}
				else
				{
					WriteBlock(child, sb, style, childIndent); // nested lists/blocks
				}
			}
		}

		private static void Indent(StringBuilder sb, int spaces)
		{
			for (int i = 0; i < spaces; i++) sb.Append(' ');
		}

		private static void WriteInlines(ContainerInline? container, StringBuilder sb, MarkdownStyle style)
		{
			if (container == null) return;
			foreach (var inline in container)
				WriteInline(inline, sb, style);
		}

		/// <summary>Opens a link scope: the structural [link=…] tag wrapping the visible color+underline span.</summary>
		private static void AppendLinkOpen(StringBuilder sb, string escapedHref, MarkdownStyle style)
		{
			sb.Append("[link=").Append(escapedHref).Append(']');
			sb.Append('[').Append(ColorWord(style.LinkColor)).Append(" underline]");
		}

		/// <summary>Closes a link scope opened by <see cref="AppendLinkOpen"/> (color/underline, then link).</summary>
		private static void AppendLinkClose(StringBuilder sb)
		{
			sb.Append("[/]"); // close color/underline
			sb.Append("[/]"); // close link
		}

		private static void WriteInline(Inline inline, StringBuilder sb, MarkdownStyle style)
		{
			switch (inline)
			{
				case LiteralInline lit:
					sb.Append(MarkupParser.Escape(lit.Content.ToString()));
					break;

				case EmphasisInline em:
					string tag = em.DelimiterCount >= ControlDefaults.MarkdownDoubleDelimiterCount
						? (em.DelimiterChar == '~' ? "strikethrough" : "bold")
						: "italic";
					sb.Append('[').Append(tag).Append(']');
					foreach (var child in em)
						WriteInline(child, sb, style);
					sb.Append("[/]");
					break;

				case CodeInline code:
					string codeTag = $"[{ColorWord(style.CodeForeground)} on {ColorWord(style.CodeBackground)}]";
					sb.Append(codeTag).Append(MarkupParser.Escape(code.Content)).Append("[/]");
					break;

				case LinkInline link:
					AppendLinkOpen(sb, LinkUrl.Escape(link.Url ?? string.Empty), style);
					foreach (var child in link)
						WriteInline(child, sb, style);
					AppendLinkClose(sb);
					break;

				case AutolinkInline autolink:
					string rawUrl = autolink.Url ?? string.Empty;   // store once (fixes double-eval)
					AppendLinkOpen(sb, LinkUrl.Escape(rawUrl), style);
					sb.Append(MarkupParser.Escape(rawUrl));
					AppendLinkClose(sb);
					break;

				case LineBreakInline:
					sb.Append('\n');
					break;

				default:
					if (inline is ContainerInline ci)
						foreach (var child in ci)
							WriteInline(child, sb, style);
					break;
			}
		}

		private static void WriteTable(Markdig.Extensions.Tables.Table table, StringBuilder sb, MarkdownStyle style, int indent, int availableWidth)
		{
			var rows = new List<List<string>>();
			foreach (var rowObj in table)
			{
				if (rowObj is not Markdig.Extensions.Tables.TableRow row) continue;
				var cells = new List<string>();
				foreach (var cellObj in row)
				{
					if (cellObj is not Markdig.Extensions.Tables.TableCell cell) continue;
					var cellSb = new StringBuilder();
					foreach (var b in cell)
						if (b is ParagraphBlock p) WriteInlines(p.Inline, cellSb, style);
					cells.Add(cellSb.ToString());
				}
				rows.Add(cells);
			}
			if (rows.Count == 0) return;

			int cols = 0;
			foreach (var r in rows) cols = Math.Max(cols, r.Count);
			var widths = new int[cols];
			foreach (var r in rows)
				for (int c = 0; c < r.Count; c++)
					widths[c] = Math.Max(widths[c], VisibleWidth(r[c]));

			// Fit the columns to the available width. Chrome = (cols+1) separators + cols*2 pad spaces.
			int tableBudget = availableWidth == int.MaxValue ? int.MaxValue : Math.Max(1, availableWidth - indent);
			if (tableBudget != int.MaxValue)
			{
				// Each column needs ≥1 content char + 3 chrome chars (1 separator + 2 padding), plus the
				// leading '│': min_table_width_for_n_cols = 4n+1. Cap cols so the table fits the budget.
				int maxFittingCols = Math.Max(1, (tableBudget - 1) / 4);
				if (cols > maxFittingCols)
				{
					cols = maxFittingCols;
					var cappedWidths = new int[cols];
					Array.Copy(widths, cappedWidths, cols);
					widths = cappedWidths;
				}

				int chrome = (cols + 1) + cols * 2;
				int contentBudget = tableBudget - chrome;
				if (contentBudget < cols)
				{
					// Degenerate: too narrow for even 1 char per column — give 1 each (the outer clip trims
					// any residual). MarkdownToMarkup is static/pure (no logger), so this is silent.
					for (int c = 0; c < cols; c++) widths[c] = 1;
				}
				else
				{
					int naturalTotal = widths.Sum();
					if (naturalTotal > contentBudget)
					{
						int overflow = naturalTotal - contentBudget;
						int fairShare = Math.Max(1, contentBudget / cols);
						int excessTotal = 0;
						for (int c = 0; c < cols; c++)
							if (widths[c] > fairShare) excessTotal += widths[c] - fairShare;
						if (excessTotal > 0)
						{
							for (int c = 0; c < cols; c++)
								if (widths[c] > fairShare)
									widths[c] -= (int)Math.Round((double)overflow * (widths[c] - fairShare) / excessTotal);
						}
						for (int c = 0; c < cols; c++) widths[c] = Math.Max(1, widths[c]);
						// Correct any rounding drift so the columns sum exactly to the content budget.
						int drift = widths.Sum() - contentBudget;
						for (int c = 0; drift != 0 && c < cols; c++)
						{
							int adj = drift > 0 ? -1 : 1;
							if (widths[c] + adj >= 1) { widths[c] += adj; drift += adj > 0 ? 1 : -1; }
						}
					}
				}
			}

			string border = $"[{ColorWord(style.BorderColor)}]";
			void Rule(char left, char mid, char right)
			{
				Indent(sb, indent);
				sb.Append(border).Append(left);
				for (int c = 0; c < cols; c++)
				{
					sb.Append(new string('─', widths[c] + 2));
					sb.Append(c == cols - 1 ? right : mid);
				}
				sb.Append("[/]\n");
			}

			Rule('┌', '┬', '┐');
			for (int ri = 0; ri < rows.Count; ri++)
			{
				// Wrap each cell to its capped width (style-preserving). rowHeight = tallest cell.
				var wrapped = new List<List<string>>(cols);
				int rowHeight = 1;
				for (int c = 0; c < cols; c++)
				{
					string raw = c < rows[ri].Count ? rows[ri][c] : "";
					var lines = MarkupParser.WrapMarkupLines(raw, widths[c]);
					wrapped.Add(lines);
					rowHeight = Math.Max(rowHeight, lines.Count);
				}

				for (int k = 0; k < rowHeight; k++)
				{
					Indent(sb, indent);
					for (int c = 0; c < cols; c++)
					{
						string lineMarkup = k < wrapped[c].Count ? wrapped[c][k] : "";
						int vis = VisibleWidth(lineMarkup);
						int pad = Math.Max(0, widths[c] - vis);
						sb.Append(border).Append('│').Append("[/] ");
						if (ri == 0) sb.Append("[bold]").Append(lineMarkup).Append("[/]");
						else sb.Append(lineMarkup);
						sb.Append(new string(' ', pad)).Append(' ');
					}
					sb.Append(border).Append('│').Append("[/]\n");
				}
				if (ri == 0) Rule('├', '┼', '┤');
			}
			Rule('└', '┴', '┘');
		}

		/// <summary>Visible (display-column) width of a native-markup fragment.</summary>
		private static int VisibleWidth(string markupFragment) => MarkupParser.StripLength(markupFragment);

		/// <summary>Formats a color as a native markup color word: <c>#RRGGBB</c>.</summary>
		private static string ColorWord(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

		/// <summary>Resolves a code highlighter for a fence language: per-style override → registry → null.</summary>
		private static ISyntaxHighlighter? ResolveCodeHighlighter(string? lang, MarkdownStyle style)
		{
			if (string.IsNullOrWhiteSpace(lang)) return null;
			if (style.CodeHighlighters.TryGetValue(lang, out var overrideHl)) return overrideHl;
			return SyntaxHighlighters.For(lang);
		}

		/// <summary>
		/// Emits one shaded, highlighted code line. A leading and trailing pad space (matching the flat
		/// path) bracket the content. Characters covered by a token use the token color; gaps use the
		/// default code foreground. Consecutive same-color characters coalesce into one markup span.
		/// </summary>
		private static void AppendHighlightedLine(
			System.Text.StringBuilder sb, string raw, IReadOnlyList<SyntaxToken> tokens,
			string bgWord, string defaultFgWord)
		{
			int n = raw.Length;
			var fgWords = new string[n];
			for (int i = 0; i < n; i++) fgWords[i] = defaultFgWord;
			foreach (var tok in tokens)
			{
				int start = Math.Max(0, tok.StartIndex);
				int end = Math.Min(n, tok.StartIndex + tok.Length);
				string w = ColorWord(tok.ForegroundColor);
				for (int i = start; i < end; i++) fgWords[i] = w;
			}

			// Leading pad space (default fg over bg).
			sb.Append('[').Append(defaultFgWord).Append(" on ").Append(bgWord).Append("] [/]");

			int idx = 0;
			while (idx < n)
			{
				int runEnd = idx + 1;
				while (runEnd < n && fgWords[runEnd] == fgWords[idx]) runEnd++;
				string segment = raw.Substring(idx, runEnd - idx);
				sb.Append('[').Append(fgWords[idx]).Append(" on ").Append(bgWord).Append(']')
				  .Append(MarkupParser.Escape(segment)).Append("[/]");
				idx = runEnd;
			}

			// Trailing pad space.
			sb.Append('[').Append(defaultFgWord).Append(" on ").Append(bgWord).Append("] [/]").Append("[fillwidth]").Append('\n');
		}
	}
}

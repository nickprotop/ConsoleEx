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
		/// <returns>A native-markup string suitable for <see cref="MarkupParser.Parse"/>.</returns>
		public static string Convert(string markdown, MarkdownStyle? style = null)
		{
			if (string.IsNullOrEmpty(markdown))
				return string.Empty;

			style ??= MarkdownStyle.Default;
			var doc = Markdown.Parse(markdown, Pipeline);
			var sb = new StringBuilder(markdown.Length);
			WriteBlocks(doc, sb, style);
			return sb.ToString().TrimEnd('\n');
		}

		private static void WriteBlocks(IEnumerable<Block> blocks, StringBuilder sb, MarkdownStyle style)
			=> WriteBlocks(blocks, sb, style, 0);

		private static void WriteBlocks(IEnumerable<Block> blocks, StringBuilder sb, MarkdownStyle style, int indent)
		{
			foreach (var block in blocks)
				WriteBlock(block, sb, style, indent);
		}

		private static void WriteBlock(Block block, StringBuilder sb, MarkdownStyle style, int indent)
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
					sb.Append(new string('─', ControlDefaults.MarkdownRuleWidth)).Append('\n');
					break;

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
							WriteBlock(child, sb, style, indent + style.ListIndent);
						}
					}
					break;

				case Markdig.Extensions.Tables.Table table:
					WriteTable(table, sb, style, indent);
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
					sb.Append('[').Append(ColorWord(style.LinkColor)).Append(" underline]");
					foreach (var child in link)
						WriteInline(child, sb, style);
					sb.Append("[/]");
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

		private static void WriteTable(Markdig.Extensions.Tables.Table table, StringBuilder sb, MarkdownStyle style, int indent)
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
				Indent(sb, indent);
				for (int c = 0; c < cols; c++)
				{
					string raw = c < rows[ri].Count ? rows[ri][c] : "";
					int pad = widths[c] - VisibleWidth(raw);
					sb.Append(border).Append('│').Append("[/] ");
					if (ri == 0) sb.Append("[bold]").Append(raw).Append("[/]");
					else sb.Append(raw);
					sb.Append(new string(' ', Math.Max(0, pad))).Append(' ');
				}
				sb.Append(border).Append('│').Append("[/]\n");
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

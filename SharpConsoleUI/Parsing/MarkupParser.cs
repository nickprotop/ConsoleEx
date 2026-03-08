// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Parsing
{
	/// <summary>
	/// Parses Spectre-compatible markup strings directly into Cell sequences,
	/// bypassing the ANSI roundtrip.  Supports [bold red on blue]text[/] syntax,
	/// [rgb(r,g,b)], [#RRGGBB], and nested/closing tags.
	/// </summary>
	public static class MarkupParser
	{
		#region Public API

		/// <summary>
		/// Parses markup into a sequence of cells using the given default colors.
		/// </summary>
		public static List<Cell> Parse(string markup, Color defaultFg, Color defaultBg)
		{
			if (string.IsNullOrEmpty(markup))
				return new List<Cell>();

			// Pre-process gradient tags before normal parsing
			markup = PreProcessGradientTags(markup, defaultFg, defaultBg, out var gradientSpans);

			var cells = new List<Cell>();
			var styleStack = new Stack<MarkupStyle>();
			var currentFg = defaultFg;
			var currentBg = defaultBg;
			var currentDec = TextDecoration.None;

			int i = 0;
			int len = markup.Length;

			while (i < len)
			{
				if (markup[i] == '[')
				{
					// Escaped bracket [[
					if (i + 1 < len && markup[i + 1] == '[')
					{
						cells.Add(new Cell('[', currentFg, currentBg, currentDec));
						i += 2;
						continue;
					}

					// Find closing ]
					int tagEnd = markup.IndexOf(']', i + 1);
					if (tagEnd < 0)
					{
						// No closing bracket — emit as literal
						cells.Add(new Cell('[', currentFg, currentBg, currentDec));
						i++;
						continue;
					}

					string tagContent = markup.Substring(i + 1, tagEnd - i - 1);

					// Empty tag [] — emit as literal brackets
					if (string.IsNullOrEmpty(tagContent))
					{
						cells.Add(new Cell('[', currentFg, currentBg, currentDec));
						cells.Add(new Cell(']', currentFg, currentBg, currentDec));
						i = tagEnd + 1;
						continue;
					}

					i = tagEnd + 1;

					if (tagContent == "/")
					{
						// Close most recent style
						if (styleStack.Count > 0)
						{
							styleStack.Pop();
							RebuildCurrentStyle(styleStack, defaultFg, defaultBg, out currentFg, out currentBg, out currentDec);
						}
					}
					else
					{
						// Parse the tag into a style entry
						var style = ParseTag(tagContent, currentFg, currentBg);

						// If tag produced no style effect, emit as literal text
						if (!style.Foreground.HasValue && !style.Background.HasValue && style.AddedDecorations == TextDecoration.None)
						{
							cells.Add(new Cell('[', currentFg, currentBg, currentDec));
							foreach (char c in tagContent)
								cells.Add(new Cell(c, currentFg, currentBg, currentDec));
							cells.Add(new Cell(']', currentFg, currentBg, currentDec));
						}
						else
						{
							styleStack.Push(style);
							if (style.Foreground.HasValue) currentFg = style.Foreground.Value;
							if (style.Background.HasValue) currentBg = style.Background.Value;
							currentDec |= style.AddedDecorations;
						}
					}
				}
				else if (markup[i] == ']')
				{
					// Escaped bracket ]]
					if (i + 1 < len && markup[i + 1] == ']')
					{
						cells.Add(new Cell(']', currentFg, currentBg, currentDec));
						i += 2;
						continue;
					}
					// Stray ] — emit as literal
					cells.Add(new Cell(']', currentFg, currentBg, currentDec));
					i++;
				}
				else
				{
					cells.Add(new Cell(markup[i], currentFg, currentBg, currentDec));
					i++;
				}
			}

			// Apply gradient foreground colors to gradient spans
			if (gradientSpans != null)
			{
				ApplyGradientSpans(cells, gradientSpans);
			}

			return cells;
		}

		/// <summary>
		/// Returns the visible character length of a markup string (strips all tags).
		/// </summary>
		public static int StripLength(string markup)
		{
			if (string.IsNullOrEmpty(markup))
				return 0;

			// Handle multi-line: return max line length
			if (markup.Contains('\n'))
			{
				int maxLen = 0;
				foreach (var line in markup.Split('\n'))
				{
					int lineLen = StripLengthSingleLine(line);
					if (lineLen > maxLen) maxLen = lineLen;
				}
				return maxLen;
			}

			return StripLengthSingleLine(markup);
		}

		/// <summary>
		/// Truncates a markup string to maxLength visible characters,
		/// preserving and properly closing all tags.
		/// </summary>
		public static string Truncate(string markup, int maxLength)
		{
			if (string.IsNullOrEmpty(markup) || maxLength <= 0)
				return string.Empty;

			var output = new System.Text.StringBuilder();
			var openTags = new Stack<string>();
			int visibleLen = 0;
			int i = 0;
			int len = markup.Length;

			while (i < len && visibleLen < maxLength)
			{
				if (markup[i] == '[')
				{
					// Escaped [[
					if (i + 1 < len && markup[i + 1] == '[')
					{
						output.Append("[[");
						visibleLen++;
						i += 2;
						continue;
					}

					int tagEnd = markup.IndexOf(']', i + 1);
					if (tagEnd < 0)
					{
						// Broken tag — emit literal
						output.Append(markup[i]);
						visibleLen++;
						i++;
						continue;
					}

					string tagContent = markup.Substring(i + 1, tagEnd - i - 1);

					if (tagContent == "/")
					{
						if (openTags.Count > 0) openTags.Pop();
						output.Append("[/]");
					}
					else
					{
						openTags.Push(tagContent);
						output.Append('[').Append(tagContent).Append(']');
					}
					i = tagEnd + 1;
				}
				else if (markup[i] == ']' && i + 1 < len && markup[i + 1] == ']')
				{
					output.Append("]]");
					visibleLen++;
					i += 2;
				}
				else
				{
					output.Append(markup[i]);
					visibleLen++;
					i++;
				}
			}

			// Close remaining open tags
			while (openTags.Count > 0)
			{
				output.Append("[/]");
				openTags.Pop();
			}

			return output.ToString();
		}

		/// <summary>
		/// Escapes brackets in plain text so they won't be interpreted as markup.
		/// </summary>
		public static string Escape(string text)
		{
			if (string.IsNullOrEmpty(text))
				return text ?? string.Empty;

			return text.Replace("[", "[[").Replace("]", "]]");
		}

		/// <summary>
		/// Removes all markup tags from a string, returning only the plain text content.
		/// Escaped brackets ([[, ]]) are converted to single brackets.
		/// </summary>
		public static string Remove(string markup)
		{
			if (string.IsNullOrEmpty(markup))
				return markup ?? string.Empty;

			var sb = new System.Text.StringBuilder(markup.Length);
			int i = 0;
			int len = markup.Length;

			while (i < len)
			{
				if (markup[i] == '[')
				{
					if (i + 1 < len && markup[i + 1] == '[')
					{
						sb.Append('[');
						i += 2;
					}
					else
					{
						// Skip until closing ]
						int close = markup.IndexOf(']', i + 1);
						if (close == -1)
						{
							sb.Append(markup[i]);
							i++;
						}
						else
						{
							i = close + 1;
						}
					}
				}
				else if (markup[i] == ']' && i + 1 < len && markup[i + 1] == ']')
				{
					sb.Append(']');
					i += 2;
				}
				else
				{
					sb.Append(markup[i]);
					i++;
				}
			}

			return sb.ToString();
		}

		/// <summary>
		/// Parses markup with word-wrapping into multiple lines of cells.
		/// Carries the active style stack across line breaks.
		/// </summary>
		/// <param name="markup">Markup string to parse and wrap.</param>
		/// <param name="width">Maximum width per line in visible characters.</param>
		/// <param name="defaultFg">Default foreground color.</param>
		/// <param name="defaultBg">Default background color.</param>
		/// <returns>List of cell lists, one per wrapped line.</returns>
		public static List<List<Cell>> ParseLines(string markup, int width, Color defaultFg, Color defaultBg)
		{
			if (string.IsNullOrEmpty(markup) || width <= 0)
				return new List<List<Cell>> { new List<Cell>() };

			var result = new List<List<Cell>>();

			// First split on explicit newlines
			var explicitLines = markup.Split('\n');
			foreach (var line in explicitLines)
			{
				var cells = Parse(line, defaultFg, defaultBg);
				if (cells.Count <= width)
				{
					result.Add(cells);
				}
				else
				{
					// Word-wrap this line
					WrapCellLine(cells, width, result);
				}
			}

			return result;
		}

		#endregion

		#region Tag Parsing

		private static MarkupStyle ParseTag(string tagContent, Color currentFg, Color currentBg)
		{
			Color? fg = null;
			Color? bg = null;
			var dec = TextDecoration.None;

			// Handle "on" keyword for background: "red on blue", "bold red on blue", "on blue"
			string fgPart = tagContent;
			string? bgPart = null;
			int onIndex = tagContent.IndexOf(" on ", StringComparison.OrdinalIgnoreCase);
			if (onIndex >= 0)
			{
				fgPart = tagContent[..onIndex].Trim();
				bgPart = tagContent[(onIndex + 4)..].Trim();
			}
			else if (tagContent.StartsWith("on ", StringComparison.OrdinalIgnoreCase))
			{
				fgPart = string.Empty;
				bgPart = tagContent[3..].Trim();
			}

			// Parse foreground part (may contain decorations + color)
			if (!string.IsNullOrEmpty(fgPart))
			{
				var parts = fgPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				foreach (var part in parts)
				{
					if (TryParseDecoration(part, out var d))
						dec |= d;
					else if (TryParseColor(part, out var c))
						fg = c;
				}
			}

			// Parse background color
			if (!string.IsNullOrEmpty(bgPart))
			{
				if (TryParseColor(bgPart.Trim(), out var bgColor))
					bg = bgColor;
			}

			return new MarkupStyle(fg, bg, dec);
		}

		private static bool TryParseDecoration(string token, out TextDecoration decoration)
		{
			decoration = TextDecoration.None;
			switch (token.ToLowerInvariant())
			{
				case "bold": decoration = TextDecoration.Bold; return true;
				case "italic": decoration = TextDecoration.Italic; return true;
				case "underline": decoration = TextDecoration.Underline; return true;
				case "dim": decoration = TextDecoration.Dim; return true;
				case "strikethrough":
				case "strike": decoration = TextDecoration.Strikethrough; return true;
				case "invert":
				case "reverse": decoration = TextDecoration.Invert; return true;
				case "blink":
				case "slowblink":
				case "rapidblink": decoration = TextDecoration.Blink; return true;
				default: return false;
			}
		}

		private static bool TryParseColor(string token, out Color color)
		{
			// Try rgb(r,g,b)
			if (token.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(')'))
			{
				var inner = token.AsSpan()[4..^1];
				var commaIdx1 = inner.IndexOf(',');
				if (commaIdx1 > 0)
				{
					var rest = inner[(commaIdx1 + 1)..];
					var commaIdx2 = rest.IndexOf(',');
					if (commaIdx2 > 0)
					{
						if (byte.TryParse(inner[..commaIdx1].Trim(), out byte r) &&
							byte.TryParse(rest[..commaIdx2].Trim(), out byte g) &&
							byte.TryParse(rest[(commaIdx2 + 1)..].Trim(), out byte b))
						{
							color = new Color(r, g, b);
							return true;
						}
					}
				}
			}

			// Try hex (#RRGGBB or #RGB)
			if (token.StartsWith('#'))
			{
				if (Color.TryFromHex(token, out color))
					return true;
			}

			// Try named color
			if (Color.TryFromName(token, out color))
				return true;

			color = default;
			return false;
		}

		#endregion

		#region Style Stack

		private static void RebuildCurrentStyle(
			Stack<MarkupStyle> stack,
			Color defaultFg, Color defaultBg,
			out Color fg, out Color bg, out TextDecoration dec)
		{
			fg = defaultFg;
			bg = defaultBg;
			dec = TextDecoration.None;

			// Replay stack from bottom to top
			foreach (var style in stack.Reverse())
			{
				if (style.Foreground.HasValue) fg = style.Foreground.Value;
				if (style.Background.HasValue) bg = style.Background.Value;
				dec |= style.AddedDecorations;
			}
		}

		#endregion

		#region Word Wrapping

		private static void WrapCellLine(List<Cell> cells, int width, List<List<Cell>> output)
		{
			int start = 0;
			while (start < cells.Count)
			{
				int remaining = cells.Count - start;
				if (remaining <= width)
				{
					output.Add(cells.GetRange(start, remaining));
					break;
				}

				// Find last word boundary within width
				int breakAt = start + width;
				int wordBreak = breakAt;

				// If the character just past the width is a space, break there
				if (breakAt < cells.Count && cells[breakAt].Character == ' ')
				{
					wordBreak = breakAt;
				}
				else
				{
					// Search backward for a space
					for (int j = breakAt - 1; j > start; j--)
					{
						if (cells[j].Character == ' ')
						{
							wordBreak = j + 1; // break after space
							break;
						}
					}
				}

				int count = wordBreak - start;
				// Trim trailing spaces from the line
				while (count > 0 && cells[start + count - 1].Character == ' ')
					count--;

				output.Add(cells.GetRange(start, count));

				// Skip leading spaces on next line
				start = wordBreak;
				while (start < cells.Count && cells[start].Character == ' ')
					start++;
			}
		}

		#endregion

		#region Gradient Processing

		/// <summary>
		/// Represents a gradient span to be applied after initial parsing.
		/// </summary>
		private readonly struct GradientSpan
		{
			public readonly int CellStart;
			public readonly int CellCount;
			public readonly ColorGradient Gradient;

			public GradientSpan(int cellStart, int cellCount, ColorGradient gradient)
			{
				CellStart = cellStart;
				CellCount = cellCount;
				Gradient = gradient;
			}
		}

		/// <summary>
		/// Pre-processes gradient tags by extracting their spans and replacing them
		/// with their inner content for normal parsing. Uses two-pass approach:
		/// first measures visible text length, then records span for post-parse coloring.
		/// </summary>
		private static string PreProcessGradientTags(
			string markup,
			Color defaultFg,
			Color defaultBg,
			out List<GradientSpan>? spans)
		{
			spans = null;

			// Quick check: does the markup contain any gradient tags?
			if (!markup.Contains("gradient=", StringComparison.OrdinalIgnoreCase))
				return markup;

			var sb = new System.Text.StringBuilder(markup.Length);
			var gradientSpans = new List<GradientSpan>();
			int i = 0;
			int len = markup.Length;

			while (i < len)
			{
				if (markup[i] == '[' && i + 1 < len && markup[i + 1] != '[')
				{
					int tagEnd = markup.IndexOf(']', i + 1);
					if (tagEnd < 0)
					{
						sb.Append(markup[i]);
						i++;
						continue;
					}

					string tagContent = markup.Substring(i + 1, tagEnd - i - 1);

					if (tagContent.StartsWith("gradient=", StringComparison.OrdinalIgnoreCase))
					{
						string gradientSpec = tagContent.Substring("gradient=".Length);
						var gradient = ColorGradient.Parse(gradientSpec);

						if (gradient != null)
						{
							// Find matching [/] for this gradient tag
							int innerStart = tagEnd + 1;
							int closeTagIndex = FindMatchingCloseTag(markup, innerStart);

							if (closeTagIndex >= 0)
							{
								string innerMarkup = markup.Substring(innerStart, closeTagIndex - innerStart);

								// Measure visible length of inner content
								int innerVisibleLen = StripLengthSingleLine(innerMarkup);

								// Calculate visible char offset by measuring what we've built so far
								int currentVisibleOffset = StripLengthSingleLine(sb.ToString());

								// Record the gradient span
								gradientSpans.Add(new GradientSpan(currentVisibleOffset, innerVisibleLen, gradient));

								// Append inner markup as-is (it may contain nested style tags)
								sb.Append(innerMarkup);

								// Skip past the closing [/]
								i = closeTagIndex + 3; // past [/]
								continue;
							}
						}
					}

					// Not a gradient tag or failed to parse — pass through
					sb.Append(markup, i, tagEnd - i + 1);
					i = tagEnd + 1;
				}
				else
				{
					sb.Append(markup[i]);
					i++;
				}
			}

			if (gradientSpans.Count > 0)
				spans = gradientSpans;

			return sb.ToString();
		}

		/// <summary>
		/// Finds the matching [/] close tag, respecting nesting depth.
		/// </summary>
		private static int FindMatchingCloseTag(string markup, int startIndex)
		{
			int depth = 1;
			int i = startIndex;
			int len = markup.Length;

			while (i < len)
			{
				if (markup[i] == '[')
				{
					if (i + 1 < len && markup[i + 1] == '[')
					{
						i += 2; // escaped
						continue;
					}

					int tagEnd = markup.IndexOf(']', i + 1);
					if (tagEnd < 0) break;

					string tag = markup.Substring(i + 1, tagEnd - i - 1);
					if (tag == "/")
					{
						depth--;
						if (depth == 0)
							return i;
					}
					else if (!string.IsNullOrEmpty(tag))
					{
						depth++;
					}
					i = tagEnd + 1;
				}
				else
				{
					i++;
				}
			}

			return -1;
		}

		/// <summary>
		/// Applies gradient colors to cells for recorded gradient spans.
		/// Each span overwrites the foreground color with interpolated gradient colors.
		/// Inner decorations and background colors are preserved.
		/// </summary>
		private static void ApplyGradientSpans(List<Cell> cells, List<GradientSpan> spans)
		{
			foreach (var span in spans)
			{
				if (span.CellCount <= 0 || span.CellStart >= cells.Count)
					continue;

				int end = Math.Min(span.CellStart + span.CellCount, cells.Count);

				for (int j = span.CellStart; j < end; j++)
				{
					double t = span.CellCount <= 1
						? 0.0
						: (double)(j - span.CellStart) / (span.CellCount - 1);

					var gradientColor = span.Gradient.Interpolate(t);
					var cell = cells[j];
					cells[j] = new Cell(cell.Character, gradientColor, cell.Background, cell.Decorations);
				}
			}
		}

		#endregion

		#region Strip Length Helper

		private static int StripLengthSingleLine(string markup)
		{
			int visibleLen = 0;
			int i = 0;
			int len = markup.Length;

			while (i < len)
			{
				if (markup[i] == '[')
				{
					// Escaped [[
					if (i + 1 < len && markup[i + 1] == '[')
					{
						visibleLen++;
						i += 2;
						continue;
					}

					// Skip tag
					int tagEnd = markup.IndexOf(']', i + 1);
					if (tagEnd < 0)
					{
						// Broken tag — count as literal
						visibleLen++;
						i++;
						continue;
					}
					i = tagEnd + 1;
				}
				else if (markup[i] == ']')
				{
					// Escaped ]]
					if (i + 1 < len && markup[i + 1] == ']')
					{
						visibleLen++;
						i += 2;
						continue;
					}
					visibleLen++;
					i++;
				}
				else
				{
					visibleLen++;
					i++;
				}
			}

			return visibleLen;
		}

		#endregion
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using static SharpConsoleUI.Helpers.UnicodeWidth;

namespace SharpConsoleUI.Parsing
{
	/// <summary>
	/// Parses Spectre-compatible markup strings directly into Cell sequences,
	/// bypassing the ANSI roundtrip.  Supports [bold red on blue]text[/] syntax,
	/// [rgb(r,g,b)], [#RRGGBB], and nested/closing tags.
	/// </summary>
	public static partial class MarkupParser
	{
		#region Public API

		/// <summary>
		/// Parses markup into a sequence of cells using the given default colors.
		/// Also supports the inline [spinner] / [spinner &lt;style&gt;] tag, which renders an animated spinner glyph.
		/// </summary>
		public static List<Cell> Parse(string markup, Color defaultFg, Color defaultBg)
			=> Parse(markup, defaultFg, defaultBg, out _);

		/// <summary>
		/// Parses markup into cells using a specific Markdown style for any <c>[markdown]</c> regions.
		/// </summary>
		public static List<Cell> Parse(string markup, Color defaultFg, Color defaultBg, Configuration.MarkdownStyle? markdownStyle)
			=> Parse(markup, defaultFg, defaultBg, out _, markdownStyle);

		private const string LinkTagPrefix = "link=";

		/// <summary>
		/// Parses markup into cells, additionally reporting clickable link spans recorded from
		/// <c>[link=&lt;escaped-url&gt;]…[/]</c> tags (half-open <c>[StartCol,EndCol)</c> in display columns).
		/// </summary>
		public static List<Cell> Parse(string markup, Color defaultFg, Color defaultBg, out List<LinkSpan> links, Configuration.MarkdownStyle? markdownStyle = null)
		{
			links = new List<LinkSpan>();

			if (string.IsNullOrEmpty(markup))
				return new List<Cell>();

			// Expand any [markdown]…[/] regions into native markup first
			markup = PreProcessMarkdownTags(markup, markdownStyle);

			// Pre-process gradient tags before normal parsing
			markup = PreProcessGradientTags(markup, defaultFg, defaultBg, out var gradientSpans);

			var cells = new List<Cell>();
			var styleStack = new Stack<MarkupStyle>();
			var linkStack = new Stack<(int startCell, string url)>();
			var frameIsLink = new Stack<bool>(); // one entry per pushed scope ([style tag] OR [link=…])
			bool fillToWidth = false;
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
						// '[' is narrow, no continuation needed
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

					// Self-closing [fillwidth] marker: emit no cell, just flag this line so the
					// painter fills the trailing background to the row width. Does not touch the
					// style stack. The flag is applied to the line's last cell after parsing.
					if (tagContent == "fillwidth")
					{
						fillToWidth = true;
						continue;
					}

					// Inline spinner tag: emit the current animated glyph in the active scope color.
					if (TryParseSpinnerTag(tagContent, out var spinnerStyle, out var spinnerInterval, out var spinnerWidth))
					{
						string glyph = MarkupSpinnerClock.CurrentGlyph(spinnerStyle, spinnerInterval, spinnerWidth);
						foreach (var gcell in Parse(glyph, currentFg, currentBg))
						{
							if (currentDec == TextDecoration.None)
							{
								cells.Add(gcell);
							}
							else
							{
								cells.Add(new Cell(gcell.Character, gcell.Foreground, gcell.Background, currentDec)
								{
									IsWideContinuation = gcell.IsWideContinuation,
									Combiners = gcell.Combiners
								});
							}
						}
						MarkupSpinnerClock.MarkParsed();
						continue;
					}

					// [link=<escaped-url>] — structural: no cell, no style. Opens a link scope.
					if (tagContent.StartsWith(LinkTagPrefix, StringComparison.OrdinalIgnoreCase))
					{
						string url = LinkUrl.Unescape(tagContent.Substring(LinkTagPrefix.Length));
						linkStack.Push((cells.Count, url));
						frameIsLink.Push(true);
						continue;
					}

					if (tagContent == "/")
					{
						if (frameIsLink.Count > 0 && frameIsLink.Peek())
						{
							frameIsLink.Pop();
							var (start, url) = linkStack.Pop();
							int end = cells.Count;
							if (end > start)
							{
								var sb = new System.Text.StringBuilder();
								for (int c = start; c < end; c++)
									if (!cells[c].IsWideContinuation) sb.Append(cells[c].Character.ToString());
								links.Add(new LinkSpan(start, end, url, sb.ToString()));
							}
						}
						else if (styleStack.Count > 0)
						{
							if (frameIsLink.Count > 0) frameIsLink.Pop(); // pops the matching 'false' marker
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
							foreach (var rune in tagContent.EnumerateRunes())
							{
								var sanitized = TextSanitizer.IsUnsafeRune(rune)
									? TextSanitizer.ReplacementCharacter : rune;
								int runeWidth = GetRuneWidth(sanitized);
								if (runeWidth == 0 && cells.Count > 0)
								{
									if (!TextSanitizer.IsSafeCombiner(sanitized))
										continue;
									var lastIdx = cells.Count - 1;
									// Skip past continuation cells to attach to the base cell
									if (cells[lastIdx].IsWideContinuation && lastIdx > 0)
										lastIdx--;
									var lastCell = cells[lastIdx];
									// VS16 widens certain emoji from 1→2 columns
									if (IsVS16(sanitized) && IsVs16Widened(lastCell.Character) && !IsWideRune(lastCell.Character))
									{
										lastCell.AppendCombiner(sanitized);
										cells[lastIdx] = lastCell;
										cells.Add(new Cell(' ', currentFg, currentBg, currentDec) { IsWideContinuation = true });
									}
									else
									{
										lastCell.AppendCombiner(sanitized);
										cells[lastIdx] = lastCell;
									}
								}
								else
								{
									cells.Add(new Cell(sanitized, currentFg, currentBg, currentDec));
									if (IsWideRune(sanitized))
										cells.Add(new Cell(' ', currentFg, currentBg, currentDec) { IsWideContinuation = true });
								}
							}
							cells.Add(new Cell(']', currentFg, currentBg, currentDec));
						}
						else
						{
							styleStack.Push(style);
							frameIsLink.Push(false);
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
						// ']' is narrow, no continuation needed
						i += 2;
						continue;
					}
					// Stray ] — emit as literal
					cells.Add(new Cell(']', currentFg, currentBg, currentDec));
					i++;
				}
				else
				{
					if (Rune.TryGetRuneAt(markup, i, out var rune))
					{
						var sanitized = TextSanitizer.IsUnsafeRune(rune)
							? TextSanitizer.ReplacementCharacter : rune;
						int runeWidth = GetRuneWidth(sanitized);
						if (runeWidth == 0)
						{
							if (cells.Count > 0 && TextSanitizer.IsSafeCombiner(sanitized))
							{
								var lastIdx = cells.Count - 1;
								// Skip past continuation cells to attach to the base cell
								if (cells[lastIdx].IsWideContinuation && lastIdx > 0)
									lastIdx--;
								var lastCell = cells[lastIdx];
								// VS16 widens certain emoji from 1→2 columns
								if (IsVS16(sanitized) && IsVs16Widened(lastCell.Character) && !IsWideRune(lastCell.Character))
								{
									lastCell.AppendCombiner(sanitized);
									cells[lastIdx] = lastCell;
									cells.Add(new Cell(' ', currentFg, currentBg, currentDec) { IsWideContinuation = true });
								}
								else
								{
									lastCell.AppendCombiner(sanitized);
									cells[lastIdx] = lastCell;
								}
							}
							// else: zero-width rune with no preceding base cell, or unsafe
							// combiner — drop it. Creating a standalone cell for a zero-width
							// rune desynchronizes cell-count from visual width and misaligns
							// every subsequent cell (e.g. the FEFF-at-start-of-line rendering
							// bug with Outlook HTML).
						}
						else
						{
							cells.Add(new Cell(sanitized, currentFg, currentBg, currentDec));
							if (IsWideRune(sanitized))
								cells.Add(new Cell(' ', currentFg, currentBg, currentDec) { IsWideContinuation = true });
						}
						i += rune.Utf16SequenceLength;
					}
					else
					{
						// Invalid surrogate — skip it
						i++;
					}
				}
			}

			// Apply gradient foreground colors to gradient spans
			if (gradientSpans != null)
			{
				ApplyGradientSpans(cells, gradientSpans);
			}

			// Apply the [fillwidth] marker (if seen) to the line's last cell so the painter
			// extends its background to the available render width.
			if (fillToWidth && cells.Count > 0)
			{
				var last = cells[^1];
				last.FillToWidth = true;
				cells[^1] = last;
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
		/// Counts the number of clickable links in a markup string without building cells.
		/// Expands any <c>[markdown]…[/]</c> regions first so that Markdown-style links
		/// (converted to <c>[link=…]…[/]</c>) are included in the count.
		/// </summary>
		/// <param name="markup">The markup string to inspect. May be <c>null</c>.</param>
		/// <returns>
		/// The number of <c>[link=…]</c> opening tags found, which equals the number of
		/// clickable link spans the string would produce if fully parsed.
		/// Returns 0 for <c>null</c> or empty input.
		/// </returns>
		public static int CountLinks(string markup)
		{
			if (string.IsNullOrEmpty(markup))
				return 0;

			markup = PreProcessMarkdownTags(markup);

			int count = 0;
			int i = 0;
			int len = markup.Length;

			while (i < len)
			{
				if (markup[i] == '[')
				{
					// Escaped [[ — not a tag, skip both chars
					if (i + 1 < len && markup[i + 1] == '[')
					{
						i += 2;
						continue;
					}

					int tagEnd = markup.IndexOf(']', i + 1);
					if (tagEnd < 0)
					{
						// No closing bracket — treat [ as literal, advance past it
						i++;
						continue;
					}

					// Check whether the tag content starts with "link=" (OrdinalIgnoreCase)
					int tagContentLen = tagEnd - (i + 1);
					if (tagContentLen >= LinkTagPrefix.Length &&
						string.Compare(markup, i + 1, LinkTagPrefix, 0, LinkTagPrefix.Length, StringComparison.OrdinalIgnoreCase) == 0)
					{
						count++;
					}

					i = tagEnd + 1;
				}
				else if (markup[i] == ']' && i + 1 < len && markup[i + 1] == ']')
				{
					// Escaped ]] — skip both chars
					i += 2;
				}
				else
				{
					i++;
				}
			}

			return count;
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
			Rune? lastMeasuredRune = null;
			int i = 0;
			int len = markup.Length;

			while (i < len && visibleLen < maxLength)
			{
				if (markup[i] == '[')
				{
					// Escaped [[
					if (i + 1 < len && markup[i + 1] == '[')
					{
						int charWidth = GetCharWidth('[');
						if (visibleLen + charWidth > maxLength)
							break;
						output.Append("[[");
						visibleLen += charWidth;
						lastMeasuredRune = new Rune('[');
						i += 2;
						continue;
					}

					int tagEnd = markup.IndexOf(']', i + 1);
					if (tagEnd < 0)
					{
						// Broken tag — emit literal
						int charWidth = GetCharWidth('[');
						if (visibleLen + charWidth > maxLength)
							break;
						output.Append(markup[i]);
						visibleLen += charWidth;
						lastMeasuredRune = new Rune('[');
						i++;
						continue;
					}

					string tagContent = markup.Substring(i + 1, tagEnd - i - 1);

					if (tagContent == "/")
					{
						if (openTags.Count > 0) openTags.Pop();
						output.Append("[/]");
					}
					else if (TryParseSpinnerTag(tagContent, out var spinnerStyle, out _, out var spinnerWidthArg))
					{
						// Spinner tags are self-contained (no matching [/]) and expand to
						// ReservedWidth columns at render time. Count that width here and
						// emit verbatim, but do NOT push onto openTags.
						int spinnerWidth = MarkupSpinnerClock.ReservedWidth(spinnerStyle, spinnerWidthArg);
						if (visibleLen + spinnerWidth > maxLength)
							break;
						output.Append('[').Append(tagContent).Append(']');
						visibleLen += spinnerWidth;
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
					int charWidth = GetCharWidth(']');
					if (visibleLen + charWidth > maxLength)
						break;
					output.Append("]]");
					visibleLen += charWidth;
					lastMeasuredRune = new Rune(']');
					i += 2;
				}
				else
				{
					if (Rune.TryGetRuneAt(markup, i, out var rune))
					{
						int charWidth = GetRuneWidth(rune);
						// VS16 widens certain emoji from 1→2 columns
						if (IsVS16(rune) && lastMeasuredRune != null && IsVs16Widened(lastMeasuredRune.Value))
						{
							if (visibleLen + 1 > maxLength)
							{
								// Widening would exceed maxLength — remove the base char
								// Find and remove the last emitted base rune
								int baseLen = lastMeasuredRune.Value.Utf16SequenceLength;
								output.Remove(output.Length - baseLen, baseLen);
								visibleLen -= GetRuneWidth(lastMeasuredRune.Value);
								lastMeasuredRune = null;
								break;
							}
							output.Append(markup.AsSpan(i, rune.Utf16SequenceLength));
							visibleLen += 1;
							lastMeasuredRune = null;
							i += rune.Utf16SequenceLength;
							continue;
						}
						// Don't emit a wide char if it would exceed maxLength
						if (visibleLen + charWidth > maxLength)
							break;
						output.Append(markup.AsSpan(i, rune.Utf16SequenceLength));
						visibleLen += charWidth;
						if (charWidth > 0) lastMeasuredRune = rune;
						i += rune.Utf16SequenceLength;
					}
					else
					{
						if (visibleLen + 1 > maxLength)
							break;
						output.Append(markup[i]);
						visibleLen++;
						lastMeasuredRune = null;
						i++;
					}
				}
			}

			// Collect trailing zero-width characters (combining marks after last visible char)
			while (i < len && markup[i] != '[')
			{
				if (Rune.TryGetRuneAt(markup, i, out var trailingRune) && GetRuneWidth(trailingRune) == 0)
				{
					// If VS16 would widen past maxLength, stop collecting
					if (IsVS16(trailingRune) && lastMeasuredRune != null && IsVs16Widened(lastMeasuredRune.Value))
					{
						if (visibleLen + 1 > maxLength)
							break;
						visibleLen += 1;
						lastMeasuredRune = null;
					}
					output.Append(markup.AsSpan(i, trailingRune.Utf16SequenceLength));
					i += trailingRune.Utf16SequenceLength;
				}
				else
					break;
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
					if (Rune.TryGetRuneAt(markup, i, out var rune))
					{
						sb.Append(markup.AsSpan(i, rune.Utf16SequenceLength));
						i += rune.Utf16SequenceLength;
					}
					else
					{
						sb.Append(markup[i]);
						i++;
					}
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
			=> ParseLines(markup, width, defaultFg, defaultBg, out _);

		/// <summary>
		/// Word-wrapping parse using a specific Markdown style for any <c>[markdown]</c> regions.
		/// </summary>
		public static List<List<Cell>> ParseLines(string markup, int width, Color defaultFg, Color defaultBg, Configuration.MarkdownStyle? markdownStyle)
			=> ParseLines(markup, width, defaultFg, defaultBg, out _, markdownStyle);

		/// <summary>
		/// Word-wrapping parse that also reports per-row link spans, re-sliced so a link crossing a
		/// wrap boundary becomes one span per row (each carrying the same URL/Text).
		/// </summary>
		/// <param name="markup">Markup string to parse and wrap.</param>
		/// <param name="width">Maximum width per line in visible characters.</param>
		/// <param name="defaultFg">Default foreground color.</param>
		/// <param name="defaultBg">Default background color.</param>
		/// <param name="linksPerLine">
		/// Receives one link-span list per returned row (empty list when a row has no links). Always
		/// index-aligned with the returned cell rows.
		/// </param>
		/// <param name="markdownStyle">Style for any <c>[markdown]</c> regions; <c>null</c> uses the default.</param>
		/// <returns>List of cell lists, one per wrapped line.</returns>
		public static List<List<Cell>> ParseLines(string markup, int width, Color defaultFg, Color defaultBg, out List<List<LinkSpan>> linksPerLine, Configuration.MarkdownStyle? markdownStyle = null)
		{
			linksPerLine = new List<List<LinkSpan>>();
			if (string.IsNullOrEmpty(markup) || width <= 0)
			{
				linksPerLine.Add(new List<LinkSpan>());
				return new List<List<Cell>> { new List<Cell>() };
			}

			// Expand any [markdown]…[/] regions before splitting on newlines,
			// otherwise a multi-line region would be torn apart by the split.
			markup = PreProcessMarkdownTags(markup, markdownStyle);

			var result = new List<List<Cell>>();

			// First split on explicit newlines
			var explicitLines = markup.Split('\n');
			foreach (var line in explicitLines)
			{
				var cells = Parse(line, defaultFg, defaultBg, out var lineSpans);
				if (cells.Count <= width)
				{
					result.Add(cells);
					linksPerLine.Add(lineSpans);
				}
				else
				{
					// Word-wrap this line
					int beforeRows = result.Count;
					WrapCellLine(cells, width, result);
					SliceSpansAcrossRows(cells, result, beforeRows, lineSpans, linksPerLine);
				}
			}

			if (result.Count == 0)
			{
				result.Add(new List<Cell>());
				linksPerLine.Add(new List<LinkSpan>());
			}

			return result;
		}

		#endregion

		#region Tag Parsing

		/// <summary>
		/// Recognizes a <c>[spinner]</c> / <c>[spinner &lt;style&gt;]</c> tag and its optional
		/// arguments. Grammar (all arguments optional, after the style word):
		/// <list type="bullet">
		/// <item><c>[spinner]</c> — Braille style, per-style default interval, natural width.</item>
		/// <item><c>[spinner &lt;style&gt;]</c> — styled (unknown style → Braille).</item>
		/// <item><c>[spinner &lt;style&gt; &lt;ms&gt;]</c> — legacy positional interval in milliseconds.</item>
		/// <item><c>[spinner &lt;style&gt; ms:&lt;N&gt; width:&lt;N&gt;]</c> — named, order-independent args.</item>
		/// </list>
		/// <c>width</c> is a <em>minimum</em> reserved field width: a value narrower than the style's
		/// natural glyph width is clamped up so the spinner never clips. Returns the resolved style
		/// (default Braille), interval, and width (0 = use the style's natural width).
		/// </summary>
		private const string SpinnerTagPrefix = "spinner";

		private static bool TryParseSpinnerTag(string tagContent, out SpinnerStyle style, out int intervalMs, out int width)
		{
			style = SpinnerStyle.Braille;
			intervalMs = Configuration.ControlDefaults.SpinnerDefaultIntervalMs;
			width = 0; // 0 = use the style's natural reserved width
			if (string.IsNullOrEmpty(tagContent)) return false;
			if (!tagContent.StartsWith(SpinnerTagPrefix, System.StringComparison.OrdinalIgnoreCase)) return false;
			if (tagContent.Length == SpinnerTagPrefix.Length)
			{
				intervalMs = SpinnerControl.DefaultIntervalMs(style); // braille default
				return true;
			}
			if (tagContent[SpinnerTagPrefix.Length] != ' ') return false; // avoid matching "spinnerfoo"

			// Tokens after "spinner": <styleWord> followed by any mix of named (key:value) args
			// and a legacy positional interval. The first bare (non key:value) token is the style.
			string rest = tagContent.Substring(SpinnerTagPrefix.Length + 1).Trim();
			var tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);

			bool styleSet = false;
			bool intervalSet = false;
			foreach (var token in tokens)
			{
				int colon = token.IndexOf(':');
				if (colon > 0)
				{
					// Named argument: key:value (order-independent).
					string key = token.Substring(0, colon).ToLowerInvariant();
					string val = token.Substring(colon + 1);
					if ((key == "ms" || key == "interval") && int.TryParse(val, out int ms) && ms > 0)
					{
						intervalMs = ms;
						intervalSet = true;
					}
					else if ((key == "width" || key == "w") && int.TryParse(val, out int w) && w > 0)
					{
						width = w;
					}
					// Unknown key or bad value → ignored (lenient, matches the style fallback).
					continue;
				}

				if (!styleSet)
				{
					style = ParseSpinnerStyleWord(token);
					styleSet = true;
				}
				else if (!intervalSet && int.TryParse(token, out int legacyMs) && legacyMs > 0)
				{
					// Legacy positional interval: [spinner <style> <ms>]
					intervalMs = legacyMs;
					intervalSet = true;
				}
				// Extra/unrecognized bare tokens are ignored.
			}

			// When no explicit interval was supplied, use the resolved style's default.
			if (!intervalSet)
				intervalMs = SpinnerControl.DefaultIntervalMs(style);

			return true;
		}

		/// <summary>Maps a spinner style word to its <see cref="SpinnerStyle"/> (case-insensitive; unknown → Braille).</summary>
		private static SpinnerStyle ParseSpinnerStyleWord(string styleWord) => styleWord.ToLowerInvariant() switch
		{
			"circle" => SpinnerStyle.Circle,
			"dots" => SpinnerStyle.Dots,
			"line" => SpinnerStyle.Line,
			"arc" => SpinnerStyle.Arc,
			"bounce" => SpinnerStyle.Bounce,
			"braille" => SpinnerStyle.Braille,
			"star" => SpinnerStyle.Star,
			"growvertical" => SpinnerStyle.GrowVertical,
			"growhorizontal" => SpinnerStyle.GrowHorizontal,
			"toggle" => SpinnerStyle.Toggle,
			"arrow" => SpinnerStyle.Arrow,
			"bouncingbar" => SpinnerStyle.BouncingBar,
			"aestheticbar" => SpinnerStyle.AestheticBar,
			"brailledots" => SpinnerStyle.BrailleDots,
			"dotsbounce" => SpinnerStyle.DotsBounce,
			_ => SpinnerStyle.Braille,
		};

		/// <summary>Test-only wrapper over <see cref="TryParseSpinnerTag"/> (style + interval only).</summary>
		public static bool TryParseSpinnerTagForTests(string tagContent, out SpinnerStyle style, out int intervalMs)
			=> TryParseSpinnerTag(tagContent, out style, out intervalMs, out _);

		/// <summary>Test-only wrapper over <see cref="TryParseSpinnerTag"/> (style + interval + width).</summary>
		public static bool TryParseSpinnerTagForTests(string tagContent, out SpinnerStyle style, out int intervalMs, out int width)
			=> TryParseSpinnerTag(tagContent, out style, out intervalMs, out width);

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

		/// <summary>True if the cell is a literal space usable as a wrap break point (not a wide-char continuation).</summary>
		private static bool IsBreakableSpace(Cell cell)
			=> cell.Character == new Rune(' ') && !cell.IsWideContinuation;

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

				// Don't break inside a wide char pair (between char and its continuation)
				if (breakAt < cells.Count && cells[breakAt].IsWideContinuation)
					breakAt--;

				int wordBreak = breakAt;

				// If the character just past the width is a space, break there
				if (breakAt < cells.Count && IsBreakableSpace(cells[breakAt]))
				{
					wordBreak = breakAt;
				}
				else
				{
					// Search backward for a space (skip continuation cells)
					for (int j = breakAt - 1; j > start; j--)
					{
						if (IsBreakableSpace(cells[j]))
						{
							wordBreak = j + 1; // break after space
							break;
						}
					}
				}

				int count = wordBreak - start;
				// Trim trailing spaces from the line (but not continuation cells)
				while (count > 0 && IsBreakableSpace(cells[start + count - 1]))
					count--;

				output.Add(cells.GetRange(start, count));

				// Skip leading spaces on next line
				start = wordBreak;
				while (start < cells.Count && IsBreakableSpace(cells[start]))
					start++;
			}
		}

		/// <summary>
		/// Distributes the original line's <paramref name="lineSpans"/> onto the wrapped rows
		/// (indices <paramref name="beforeRows"/>..end of <paramref name="rows"/>). Each row gets the
		/// portion of every span inside its column window; a span crossing a wrap boundary yields one
		/// clamped span per row. URL/Text are preserved. Always appends exactly one span-list per row
		/// so <c>linksPerLine</c> stays index-aligned with <c>rows</c>.
		/// </summary>
		private static void SliceSpansAcrossRows(
			List<Cell> originalCells, List<List<Cell>> rows, int beforeRows,
			List<LinkSpan> lineSpans, List<List<LinkSpan>> linksPerLine)
		{
			int cursor = 0;
			var rowWindows = new List<(int from, int to)>();
			for (int r = beforeRows; r < rows.Count; r++)
			{
				// Skip leading spaces between rows (WrapCellLine skips them).
				while (cursor < originalCells.Count && IsBreakableSpace(originalCells[cursor]))
					cursor++;
				int from = cursor;
				int rowLen = rows[r].Count;
				int to = Math.Min(originalCells.Count, from + rowLen);
				rowWindows.Add((from, to));
				cursor = to;
			}

			foreach (var (from, to) in rowWindows)
			{
				var rowSpans = new List<LinkSpan>();
				foreach (var s in lineSpans)
				{
					int a = Math.Max(s.StartCol, from);
					int b = Math.Min(s.EndCol, to);
					if (b > a)
						rowSpans.Add(new LinkSpan(a - from, b - from, s.Url, s.Text));
				}
				linksPerLine.Add(rowSpans);
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
			Rune? lastMeasuredRune = null;
			int i = 0;
			int len = markup.Length;

			while (i < len)
			{
				if (markup[i] == '[')
				{
					// Escaped [[
					if (i + 1 < len && markup[i + 1] == '[')
					{
						visibleLen += GetCharWidth('[');
						lastMeasuredRune = new Rune('[');
						i += 2;
						continue;
					}

					// Skip tag
					int tagEnd = markup.IndexOf(']', i + 1);
					if (tagEnd < 0)
					{
						// Broken tag — count as literal
						visibleLen += GetCharWidth('[');
						lastMeasuredRune = new Rune('[');
						i++;
						continue;
					}
					// Only allocate tagContent when the tag could be a spinner tag ('s'/'S').
					// StripLength runs in hot layout/wrap loops; keep non-spinner tags allocation-free.
					if (i + 1 < tagEnd && (markup[i + 1] == 's' || markup[i + 1] == 'S'))
					{
						string tagContent = markup.Substring(i + 1, tagEnd - (i + 1));
						if (TryParseSpinnerTag(tagContent, out var spinnerStyle, out _, out var spinnerWidthArg))
							visibleLen += MarkupSpinnerClock.ReservedWidth(spinnerStyle, spinnerWidthArg);
					}
					i = tagEnd + 1;
				}
				else if (markup[i] == ']')
				{
					// Escaped ]]
					if (i + 1 < len && markup[i + 1] == ']')
					{
						visibleLen += GetCharWidth(']');
						lastMeasuredRune = new Rune(']');
						i += 2;
						continue;
					}
					visibleLen += GetCharWidth(markup[i]);
					lastMeasuredRune = new Rune(markup[i]);
					i++;
				}
				else
				{
					if (Rune.TryGetRuneAt(markup, i, out var rune))
					{
						int rw = GetRuneWidth(rune);
						// VS16 widens certain emoji from 1→2 columns
						if (IsVS16(rune) && lastMeasuredRune != null && IsVs16Widened(lastMeasuredRune.Value))
						{
							visibleLen += 1;
							lastMeasuredRune = null;
						}
						else
						{
							if (rw > 0) lastMeasuredRune = rune;
							visibleLen += rw;
						}
						i += rune.Utf16SequenceLength;
					}
					else
					{
						visibleLen++;
						lastMeasuredRune = null;
						i++;
					}
				}
			}

			return visibleLen;
		}

		#endregion
	}
}

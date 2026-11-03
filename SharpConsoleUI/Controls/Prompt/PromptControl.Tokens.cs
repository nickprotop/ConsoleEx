// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class PromptControl
	{
		#region Token colours

		private Color? _tokenBackgroundColor;
		private Color? _tokenForegroundColor;

		/// <summary>
		/// Gets or sets the background color a token's label is drawn on. When null the input
		/// field's own background is used, so a token reads as a chip only through its foreground.
		/// </summary>
		/// <remarks>
		/// Follows the same convention as <see cref="InputBackgroundColor"/>: an explicit override,
		/// resolved against the theme when left null.
		/// </remarks>
		public Color? TokenBackgroundColor
		{
			get => _tokenBackgroundColor;
			set => SetProperty(ref _tokenBackgroundColor, value);
		}

		/// <summary>
		/// Gets or sets the foreground color a token's label is drawn in, so the label reads as a
		/// chip standing for something rather than as text the user typed.
		/// </summary>
		/// <remarks>
		/// Follows the same convention as <see cref="InputForegroundColor"/>: an explicit override,
		/// resolved against the theme when left null.
		/// </remarks>
		public Color? TokenForegroundColor
		{
			get => _tokenForegroundColor;
			set => SetProperty(ref _tokenForegroundColor, value);
		}

		/// <summary>
		/// The colour a token label is painted in, given the colours the input field resolved to for
		/// this paint.
		/// </summary>
		/// <remarks>
		/// Falls back to the field's own foreground pulled towards cyan rather than to a fixed
		/// colour, for the same reason <see cref="PlaceholderColor"/> does: a token has to stay
		/// legible on a light field and a dark one, and one fixed colour cannot be.
		/// </remarks>
		/// <param name="inputForeground">The colour ordinary input text is being painted in.</param>
		private Color ResolveTokenForeground(Color inputForeground)
			=> ColorResolver.Coalesce(TokenForegroundColor) ?? DefaultTokenForeground(inputForeground);

		/// <summary>
		/// The background a token label is painted on, given the colours the input field resolved to.
		/// </summary>
		/// <remarks>
		/// No theme member backs this. <see cref="Themes.ITheme"/> is implemented outside this library, so
		/// widening it for one control's accent is a cost paid by every implementer; the derived
		/// default below needs no theme support to stay legible.
		/// </remarks>
		/// <param name="inputBackground">The colour the input field is being painted on.</param>
		private Color ResolveTokenBackground(Color inputBackground)
			=> ColorResolver.Coalesce(TokenBackgroundColor) ?? inputBackground;

		/// <summary>
		/// The fallback token colour: the field's own foreground shifted towards cyan, which reads as
		/// a distinct accent against either a light or a dark field.
		/// </summary>
		private static Color DefaultTokenForeground(Color inputForeground)
			=> new Color(
				(byte)(inputForeground.R / ControlDefaults.PromptTokenColorDampen),
				(byte)((inputForeground.G + ControlDefaults.PromptTokenColorChannelMax) / ControlDefaults.PromptTokenColorDampen),
				(byte)((inputForeground.B + ControlDefaults.PromptTokenColorChannelMax) / ControlDefaults.PromptTokenColorDampen));

		#endregion

		#region Token geometry

		/// <summary>One token's label as a range of <see cref="DisplayText"/>.</summary>
		/// <param name="Start">First character index of the label.</param>
		/// <param name="Length">Label length in characters.</param>
		/// <param name="Token">The token the label belongs to.</param>
		private readonly record struct TokenSpan(int Start, int Length, PromptToken Token)
		{
			/// <summary>One past the last character index of the label.</summary>
			public int End => Start + Length;
		}

		/// <summary>
		/// True when the content holds at least one token. The fast path every existing application
		/// takes: wrapping and painting stay byte for byte what they were when this is false.
		/// </summary>
		private bool HasTokens
		{
			get
			{
				foreach (var segment in _segments)
					if (segment.IsToken) return true;
				return false;
			}
		}

		/// <summary>
		/// Every token label's range in <see cref="DisplayText"/> coordinates, in order. Empty when
		/// the content holds no tokens.
		/// </summary>
		/// <remarks>
		/// A zero-length label yields no span: it occupies nothing, so there is nothing to keep
		/// whole, nothing to colour, and a span of length zero would make the overlap tests below
		/// claim every index belongs to it.
		/// </remarks>
		private List<TokenSpan> TokenSpans()
		{
			var spans = new List<TokenSpan>();
			int offset = 0;
			foreach (var segment in _segments)
			{
				if (segment.IsToken)
				{
					int length = segment.Token!.Label.Length;
					if (length > 0) spans.Add(new TokenSpan(offset, length, segment.Token!));
					offset += length;
				}
				else
				{
					offset += segment.Text!.Length;
				}
			}
			return spans;
		}

		/// <summary>
		/// The token label covering <paramref name="displayIndex"/>, counting its first character but
		/// not the index one past its end.
		/// </summary>
		/// <param name="spans">The spans from <see cref="TokenSpans"/>.</param>
		/// <param name="displayIndex">An index into <see cref="DisplayText"/>.</param>
		/// <param name="span">The span found.</param>
		/// <returns>True when a label covers the index.</returns>
		private static bool TryGetSpanCovering(List<TokenSpan> spans, int displayIndex, out TokenSpan span)
		{
			foreach (var candidate in spans)
			{
				if (displayIndex >= candidate.Start && displayIndex < candidate.End)
				{
					span = candidate;
					return true;
				}
			}
			span = default;
			return false;
		}

		/// <summary>
		/// Truncates a token label to <paramref name="columns"/> display columns, ending it with
		/// <see cref="ControlDefaults.PromptTokenEllipsis"/> so the cut is visible rather than
		/// looking like a shorter label.
		/// </summary>
		/// <remarks>
		/// Measured in DISPLAY COLUMNS throughout: a label of CJK or emoji costs two columns a glyph,
		/// so cutting it by character count would overrun the row it was being made to fit.
		/// </remarks>
		/// <param name="label">The label to truncate.</param>
		/// <param name="columns">The columns available. One or fewer yields the ellipsis alone.</param>
		/// <returns>The truncated label, including the ellipsis.</returns>
		private static string TruncateLabel(string label, int columns)
		{
			if (columns <= ControlDefaults.PromptTokenEllipsisWidth)
				return ControlDefaults.PromptTokenEllipsis.ToString();

			int room = columns - ControlDefaults.PromptTokenEllipsisWidth;
			var (endChar, _) = UnicodeWidth.TakeColumns(label, 0, room);
			return label.Substring(0, endChar) + ControlDefaults.PromptTokenEllipsis;
		}

		#endregion

		#region Token-aware painting

		/// <summary>
		/// Builds the cells for one row of the input, painting each token's label in the token
		/// colours and everything else in the field's own.
		/// </summary>
		/// <remarks>
		/// <para>
		/// A token is NOT masked when <see cref="MaskCharacter"/> is set. A masked prompt is a
		/// password field, and a host that puts a chip in one has made a mistake; drawing the label
		/// says so, where a row of asterisks standing for a value the host already holds would hide
		/// it. Ordinary text in the same row is still masked.
		/// </para>
		/// <para>
		/// A label wider than <paramref name="fieldWidth"/> is truncated with an ellipsis, as one
		/// unit, because the wrapper already gave it a row of its own and it still did not fit.
		/// </para>
		/// </remarks>
		/// <param name="rowStart">First character index of the row in <see cref="DisplayText"/>.</param>
		/// <param name="rowLength">Row length in characters.</param>
		/// <param name="spans">Every token label range, from <see cref="TokenSpans"/>.</param>
		/// <param name="textForeground">Colour for ordinary text.</param>
		/// <param name="background">Colour the row is painted on.</param>
		/// <param name="fieldWidth">Columns available to the row.</param>
		/// <returns>The row's cells, in display-column order.</returns>
		private List<Cell> BuildTokenRowCells(
			int rowStart,
			int rowLength,
			List<TokenSpan> spans,
			Color textForeground,
			Color background,
			int fieldWidth)
		{
			Color tokenForeground = ResolveTokenForeground(textForeground);
			Color tokenBackground = ResolveTokenBackground(background);

			var cells = new List<Cell>(rowLength);
			int rowEnd = rowStart + rowLength;
			int pos = rowStart;

			while (pos < rowEnd)
			{
				if (TryGetSpanCovering(spans, pos, out var span))
				{
					// Only the part of the label that lies in this row, which is the whole label: the
					// wrapper never splits one. Clamped anyway so a row built from a stale wrap cache
					// cannot read past its own end.
					int labelEnd = Math.Min(span.End, rowEnd);
					string label = DisplayText.Substring(pos, labelEnd - pos);

					int room = Math.Max(0, fieldWidth - cells.Count);
					if (UnicodeWidth.GetStringWidth(label) > room)
						label = TruncateLabel(label, room);

					foreach (var cell in Parsing.MarkupParser.Parse(
						Parsing.MarkupParser.Escape(label), tokenForeground, tokenBackground))
					{
						cells.Add(cell);
					}

					pos = labelEnd;
					continue;
				}

				// Ordinary text, up to the next label or the end of the row.
				int stretchEnd = rowEnd;
				foreach (var candidate in spans)
				{
					if (candidate.Start > pos && candidate.Start < stretchEnd) stretchEnd = candidate.Start;
				}

				string text = DisplayText.Substring(pos, stretchEnd - pos);
				string display = _maskCharacter.HasValue
					? new string(_maskCharacter.Value, UnicodeWidth.GetStringWidth(text))
					: Parsing.MarkupParser.Escape(text);

				foreach (var cell in Parsing.MarkupParser.Parse(display, textForeground, background))
					cells.Add(cell);

				pos = stretchEnd;
			}

			return cells;
		}

		#endregion

		#region Token-aware wrapping

		/// <summary>
		/// Wraps one logical line into visual rows, never splitting a token label across two of them.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The line is walked as an alternation of ordinary text stretches and token labels. A text
		/// stretch still breaks exactly where <see cref="TextWrapping.WrapWords"/> says it does, so a
		/// prompt and a <see cref="MultilineEditControl"/> cannot disagree about a string with no
		/// token in it. Only the label is placed by this method, as one indivisible piece.
		/// </para>
		/// <para>
		/// A label that does not fit the columns left on the current row moves to the next; one that
		/// does not fit a whole row is truncated at paint time (see
		/// <see cref="ControlDefaults.PromptTokenEllipsis"/>) while still occupying a single row, so
		/// the row count a caller reads matches the rows that get painted.
		/// </para>
		/// </remarks>
		/// <param name="rows">Destination; rows are appended in <see cref="DisplayText"/> coordinates.</param>
		/// <param name="scratch">A reused list, to keep the wrap off the allocation path.</param>
		/// <param name="spans">Every token label range, from <see cref="TokenSpans"/>.</param>
		/// <param name="lineStart">First character index of the logical line.</param>
		/// <param name="lineEnd">One past the last character index of the logical line.</param>
		/// <param name="width">Row width in display columns. At least 1.</param>
		private void WrapLineKeepingTokensWhole(
			List<TextWrapping.Segment> rows,
			List<TextWrapping.Segment> scratch,
			List<TokenSpan> spans,
			int lineStart,
			int lineEnd,
			int width)
		{
			// The row being built: where it starts, and how many columns it has already spent.
			int rowStart = lineStart;
			int rowColumns = 0;
			int pos = lineStart;

			// An empty line still occupies a row, matching TextWrapping.WrapWords.
			if (lineEnd <= lineStart)
			{
				rows.Add(new TextWrapping.Segment(lineStart, 0));
				return;
			}

			void CloseRow(int end)
			{
				rows.Add(new TextWrapping.Segment(rowStart, end - rowStart));
				rowStart = end;
				rowColumns = 0;
			}

			while (pos < lineEnd)
			{
				if (TryGetSpanCovering(spans, pos, out var span))
				{
					// A label is placed whole or not at all. Its own start is the only index the walk
					// can reach it from, because SpliceSegments keeps labels contiguous.
					int labelWidth = UnicodeWidth.GetStringWidth(DisplayText.Substring(span.Start, span.Length));
					int remaining = width - rowColumns;

					if (labelWidth > remaining && rowColumns > 0)
					{
						// Does not fit what is left: move it to the next row and try again there.
						CloseRow(pos);
						continue;
					}

					// Either it fits, or the row is empty and it is wider than a whole row: it takes
					// this row and is truncated when painted. Clamped so the spent columns never
					// exceed the row, which would make the next fit test negative.
					rowColumns += Math.Min(labelWidth, width);
					pos = span.End;
					if (rowColumns >= width && pos < lineEnd) CloseRow(pos);
					continue;
				}

				// An ordinary stretch, up to the next label or the end of the line.
				int stretchEnd = lineEnd;
				foreach (var candidate in spans)
				{
					if (candidate.Start > pos && candidate.Start < stretchEnd) stretchEnd = candidate.Start;
				}

				int available = Math.Max(1, width - rowColumns);
				scratch.Clear();
				TextWrapping.WrapWords(scratch, DisplayText.Substring(pos, stretchEnd - pos), available);

				// The first wrapped piece finishes the current row; every later piece starts its own,
				// so only the first can be narrower than the full width.
				var first = scratch[0];
				if (scratch.Count == 1)
				{
					rowColumns += UnicodeWidth.GetStringWidth(DisplayText.Substring(pos, first.Length));
					pos += first.Length;
					if (rowColumns >= width && pos < lineEnd) CloseRow(pos);
					continue;
				}

				// More than one piece: the stretch overflows the row, so close after the first piece
				// and re-wrap the remainder against a full row on the next pass.
				pos += first.Length;
				CloseRow(pos);
			}

			if (pos > rowStart || rows.Count == 0) CloseRow(pos);
		}

		#endregion
	}
}

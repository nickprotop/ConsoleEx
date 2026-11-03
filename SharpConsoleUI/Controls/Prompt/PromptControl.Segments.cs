// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class PromptControl
	{
		#region Segment storage

		// The content, in order: runs of ordinary text and tokens. Authoritative.
		//
		// Why a list and not the string it used to be: a token has to carry a value the box never
		// shows, which a string cannot hold without a sentinel character, and a sentinel in the one
		// field every read path hands outward is a leak waiting to happen.
		private readonly List<PromptSegment> _segments = new();

		// The projection the rest of the control works in: text runs verbatim, each token as its
		// LABEL. Wrapping, measurement, painting and every caret index are in these coordinates,
		// because TextWrapping.WrapWords takes a string and is shared with MultilineEditControl, so
		// the two must not disagree about where a string breaks.
		//
		// Derived and cached. Null means "rebuild on next read". Invalidated wherever the wrap cache
		// is, since the two go stale for exactly the same reasons.
		private string? _displayCache;

		/// <summary>
		/// The content as the box shows it: text runs verbatim, each token as its label. The
		/// coordinate space of <see cref="_cursorPosition"/>, the selection anchor, wrapping and
		/// painting.
		/// </summary>
		private string DisplayText
		{
			get
			{
				if (_displayCache == null)
					_displayCache = Project(label: true);
				return _displayCache;
			}
		}

		/// <summary>
		/// The content as it should be sent: text runs verbatim, each token as its value. What
		/// <see cref="Input"/> returns.
		/// </summary>
		private string ValueText => Project(label: false);

		/// <summary>
		/// Flattens the segment list to a string, taking either each token's label or its value.
		/// </summary>
		/// <param name="label">True for the label projection, false for the value projection.</param>
		private string Project(bool label) => ProjectSegments(_segments, label);

		/// <summary>
		/// The token whose label COVERS a display index, counting both edges as inside.
		/// </summary>
		/// <param name="displayIndex">An index into <see cref="DisplayText"/>.</param>
		/// <param name="start">Where the label starts.</param>
		/// <param name="length">The label's length.</param>
		/// <param name="token">The token found.</param>
		/// <returns>True when a label covers the index.</returns>
		/// <remarks>
		/// The question a POINTER asks, as opposed to <see cref="TryGetTokenSpanAt"/>, which tests
		/// the strict interior because the edges are legal caret positions. A click on the leftmost
		/// cell is a click on the chip, and a one-column label has no interior to hit.
		/// </remarks>
		private bool TryGetTokenSpanCoveringIndex(int displayIndex, out int start, out int length, out PromptToken token)
		{
			int offset = 0;
			foreach (var segment in _segments)
			{
				int segmentLength = segment.IsToken ? segment.Token!.Label.Length : segment.Text!.Length;

				if (segment.IsToken && displayIndex >= offset && displayIndex < offset + segmentLength)
				{
					start = offset;
					length = segmentLength;
					token = segment.Token!;
					return true;
				}

				offset += segmentLength;
			}

			start = 0;
			length = 0;
			token = null!;
			return false;
		}

		/// <summary>
		/// Collapses every newline in the text runs to a space, leaving the tokens alone.
		/// </summary>
		/// <remarks>
		/// For leaving multiline mode, where the content has to fit one row. Rebuilding from
		/// <see cref="DisplayText"/> would be simpler and would silently destroy every token: that
		/// projection carries each token's LABEL, so the content would come back as ordinary text
		/// and the values would be gone, with nothing saying so.
		/// </remarks>
		private void FlattenNewlinesInSegments()
		{
			var flattened = new List<PromptSegment>(_segments.Count);
			foreach (var segment in _segments)
			{
				flattened.Add(segment.IsToken
					? segment
					: PromptSegment.FromText(FlattenNewlines(segment.Text!)));
			}

			SetSegments(flattened);
		}

		/// <summary>
		/// Replaces the content with a stored segment list, honouring <see cref="MaxLength"/>.
		/// </summary>
		/// <remarks>
		/// For history and the draft stash, which hold segments rather than text. A cap lowered
		/// since the entry was banked drops whole segments from the end rather than cutting a label
		/// in half, so what comes back is always something the control could have held.
		/// </remarks>
		private void SetSegmentsClamped(IReadOnlyList<PromptSegment> entry)
		{
			if (!_maxLength.HasValue)
			{
				SetSegments(entry);
				return;
			}

			var kept = new List<PromptSegment>();
			int used = 0;
			foreach (var segment in entry)
			{
				int width = segment.IsToken ? segment.Token!.Label.Length : segment.Text!.Length;
				if (used + width > _maxLength.Value)
				{
					// Text can be cut; a token cannot, because half a label stands for nothing.
					int room = _maxLength.Value - used;
					if (!segment.IsToken && room > 0)
						kept.Add(PromptSegment.FromText(segment.Text!.Substring(0, room)));
					break;
				}

				kept.Add(segment);
				used += width;
			}

			SetSegments(kept);
		}

		/// <summary>
		/// Flattens any segment list to a string, taking either each token's label or its value.
		/// </summary>
		/// <param name="segments">The segments to flatten.</param>
		/// <param name="label">True for the label projection, false for the value projection.</param>
		/// <remarks>
		/// Static and list-agnostic because history holds segment lists of its own, and projecting
		/// those must be the same operation rather than a second implementation that can drift.
		/// </remarks>
		private static string ProjectSegments(IReadOnlyList<PromptSegment> segments, bool label)
		{
			if (segments.Count == 0) return string.Empty;
			if (segments.Count == 1 && !segments[0].IsToken) return segments[0].Text!;

			var sb = new StringBuilder();
			foreach (var segment in segments)
				sb.Append(segment.IsToken ? (label ? segment.Token!.Label : segment.Token!.Value) : segment.Text);
			return sb.ToString();
		}

		/// <summary>
		/// Projects a range of <see cref="DisplayText"/> to the text it stands for: ordinary text
		/// verbatim, and each token wholly covered by the range as its VALUE rather than its label.
		/// </summary>
		/// <remarks>
		/// What a copy takes. A token only partly covered contributes nothing, which cannot happen
		/// through any input path (the selection snaps outward as it moves) and is a floor rather
		/// than a case: half a label would stand for nothing, and half a value would be worse.
		/// </remarks>
		/// <param name="start">Start of the range, in <see cref="DisplayText"/> coordinates.</param>
		/// <param name="end">One past the end of the range.</param>
		private string ProjectValueRange(int start, int end)
		{
			start = Math.Clamp(start, 0, DisplayText.Length);
			end = Math.Clamp(end, start, DisplayText.Length);
			if (end == start) return string.Empty;

			var sb = new StringBuilder();
			int offset = 0;
			foreach (var segment in _segments)
			{
				if (segment.IsToken)
				{
					int length = segment.Token!.Label.Length;
					// Wholly covered only. A zero-length label covers nothing, so it contributes its
					// value when the range reaches the point it sits at.
					if (offset >= start && offset + length <= end)
						sb.Append(segment.Token!.Value);
					offset += length;
				}
				else
				{
					string text = segment.Text!;
					int from = Math.Clamp(start - offset, 0, text.Length);
					int to = Math.Clamp(end - offset, 0, text.Length);
					if (to > from) sb.Append(text, from, to - from);
					offset += text.Length;
				}
			}

			return sb.ToString();
		}

		/// <summary>
		/// Drops the cached display projection. Called wherever the wrap cache is dropped: the two
		/// are stale under exactly the same conditions.
		/// </summary>
		private void InvalidateDisplayCache() => _displayCache = null;

		#endregion

		#region Segment edits

		/// <summary>
		/// Replaces the whole content with one run of ordinary text, discarding any tokens. The
		/// meaning of the <see cref="Input"/> setter and of every path that assigns a new value
		/// wholesale.
		/// </summary>
		/// <param name="text">The new content.</param>
		private void SetDisplayText(string text)
		{
			_segments.Clear();
			if (text.Length > 0)
				_segments.Add(PromptSegment.FromText(text));
			_displayCache = text;
		}

		/// <summary>
		/// Replaces the whole content with the given segments, merging adjacent text runs and
		/// dropping empty ones so the list carries no artefact a projection would have to re-join.
		/// </summary>
		/// <param name="segments">The new content, in order.</param>
		private void SetSegments(IEnumerable<PromptSegment> segments)
		{
			_segments.Clear();
			foreach (var segment in segments)
			{
				if (segment.IsToken)
				{
					_segments.Add(segment);
					continue;
				}

				string text = segment.Text ?? string.Empty;
				if (text.Length == 0) continue;

				// Merge with the preceding run: two text segments side by side are the same run, and
				// leaving them split would make InputSegments report an edit boundary as content.
				int last = _segments.Count - 1;
				if (last >= 0 && !_segments[last].IsToken)
					_segments[last] = PromptSegment.FromText(_segments[last].Text + text);
				else
					_segments.Add(PromptSegment.FromText(text));
			}

			InvalidateDisplayCache();
		}

		/// <summary>
		/// Inserts ordinary text at a position in <see cref="DisplayText"/> coordinates, keeping every
		/// token intact. An index inside a token's label is snapped outward first: text can go before
		/// or after a token, never into it.
		/// </summary>
		/// <param name="displayIndex">Where to insert, as an index into <see cref="DisplayText"/>.</param>
		/// <param name="text">The text to insert.</param>
		private void InsertDisplayText(int displayIndex, string text)
		{
			if (text.Length == 0) return;

			displayIndex = SnapCaret(Math.Clamp(displayIndex, 0, DisplayText.Length), forward: true);
			SetSegments(SpliceSegments(displayIndex, 0, PromptSegment.FromText(text)));
		}

		/// <summary>
		/// Removes a range in <see cref="DisplayText"/> coordinates. A token whose label the range
		/// touches at all goes WHOLE: a half-removed label would leave the content showing characters
		/// that stand for nothing.
		/// </summary>
		/// <param name="displayIndex">Where the range starts, as an index into <see cref="DisplayText"/>.</param>
		/// <param name="length">How many characters to remove.</param>
		private void RemoveDisplayText(int displayIndex, int length)
		{
			if (length <= 0) return;
			int total = DisplayText.Length;
			displayIndex = Math.Clamp(displayIndex, 0, total);
			length = Math.Min(length, total - displayIndex);
			if (length <= 0) return;

			SetSegments(SpliceSegments(displayIndex, length, null));
		}

		/// <summary>
		/// Rebuilds the segment list with the display range [<paramref name="start"/>,
		/// <paramref name="start"/> + <paramref name="length"/>) removed and
		/// <paramref name="insert"/> put in its place.
		/// </summary>
		/// <remarks>
		/// The one place segment arithmetic happens, so insert, remove and replace cannot disagree
		/// about where a token ends. A token is kept only when the removal range misses its label
		/// entirely; any overlap takes the whole token.
		/// </remarks>
		/// <param name="start">Start of the removed range, in <see cref="DisplayText"/> coordinates.</param>
		/// <param name="length">Length of the removed range. Zero for a pure insert.</param>
		/// <param name="insert">The segment to insert at <paramref name="start"/>, or null for none.</param>
		/// <returns>The rebuilt segment sequence.</returns>
		private List<PromptSegment> SpliceSegments(int start, int length, PromptSegment? insert)
		{
			int end = start + length;
			var result = new List<PromptSegment>(_segments.Count + 2);
			int offset = 0;
			bool inserted = false;

			// Puts the new segment in as soon as the walk has passed the insertion point, so a pure
			// insert at a boundary lands between the two neighbours rather than after both.
			void EmitInsert()
			{
				if (inserted) return;
				inserted = true;
				if (insert.HasValue) result.Add(insert.Value);
			}

			foreach (var segment in _segments)
			{
				int segmentLength = segment.IsToken ? segment.Token!.Label.Length : segment.Text!.Length;
				int segmentEnd = offset + segmentLength;

				if (segment.IsToken)
				{
					if (offset >= start) EmitInsert();
					// Any overlap at all removes the token. Touching only its edges is not overlap:
					// a range ending exactly where the label starts leaves it alone.
					bool overlaps = length > 0 && start < segmentEnd && end > offset;
					if (!overlaps) result.Add(segment);
				}
				else
				{
					string text = segment.Text!;

					// The part of this run before the removed range.
					int keepBefore = Math.Clamp(start - offset, 0, segmentLength);
					if (keepBefore > 0) result.Add(PromptSegment.FromText(text.Substring(0, keepBefore)));

					if (segmentEnd >= start) EmitInsert();

					// The part after it.
					int afterStart = Math.Clamp(end - offset, 0, segmentLength);
					if (afterStart < segmentLength)
						result.Add(PromptSegment.FromText(text.Substring(afterStart)));
				}

				offset = segmentEnd;
			}

			EmitInsert();
			return result;
		}

		/// <summary>
		/// Widens a display range outward to whole token labels, so a selection never ends halfway
		/// through one.
		/// </summary>
		/// <param name="start">Start of the range. Snapped back to a token's leading edge.</param>
		/// <param name="end">End of the range. Snapped on to a token's trailing edge.</param>
		private void SnapRangeOutward(ref int start, ref int end)
		{
			start = SnapCaret(start, forward: false);
			end = SnapCaret(end, forward: true);
		}

		#endregion

		#region Token spans

		/// <summary>
		/// Finds the token whose label covers <paramref name="displayIndex"/>, if any. A position on
		/// a token's leading or trailing edge is NOT inside it: those are the two places a caret is
		/// allowed to rest.
		/// </summary>
		/// <param name="displayIndex">An index into <see cref="DisplayText"/>.</param>
		/// <param name="start">The token label's start index in <see cref="DisplayText"/>.</param>
		/// <param name="length">The token label's length in <see cref="DisplayText"/> characters.</param>
		/// <param name="token">The token found.</param>
		/// <returns>True when a token's label strictly contains the index.</returns>
		private bool TryGetTokenSpanAt(int displayIndex, out int start, out int length, out PromptToken token)
		{
			int offset = 0;
			foreach (var segment in _segments)
			{
				if (segment.IsToken)
				{
					int labelLength = segment.Token!.Label.Length;
					if (displayIndex > offset && displayIndex < offset + labelLength)
					{
						start = offset;
						length = labelLength;
						token = segment.Token!;
						return true;
					}
					offset += labelLength;
				}
				else
				{
					offset += segment.Text!.Length;
				}
			}

			start = 0;
			length = 0;
			token = null!;
			return false;
		}

		/// <summary>
		/// Moves an index out of a token's interior to the nearer edge in the direction of travel, so
		/// a caret can never rest inside a token.
		/// </summary>
		/// <param name="displayIndex">An index into <see cref="DisplayText"/>.</param>
		/// <param name="forward">
		/// True to push the index to the token's trailing edge, false to its leading edge.
		/// </param>
		/// <returns>The snapped index, or the original when it was not inside a token.</returns>
		private int SnapCaret(int displayIndex, bool forward)
			=> TryGetTokenSpanAt(displayIndex, out int start, out int length, out _)
				? (forward ? start + length : start)
				: displayIndex;

		#endregion

		#region Public reads

		/// <summary>
		/// Gets the content as a list of segments: runs of ordinary text and tokens, in order.
		/// </summary>
		/// <remarks>
		/// A SNAPSHOT rather than a live view, so a host that holds the result does not watch it
		/// change underneath them as the user types.
		/// </remarks>
		public IReadOnlyList<PromptSegment> InputSegments => _segments.ToArray();

		/// <summary>
		/// Gets the content as the box shows it: ordinary text verbatim, and each token as its label
		/// rather than the value it stands for.
		/// </summary>
		/// <remarks>
		/// Use this to show what the field looks like. Use <see cref="Input"/> to get what to send.
		/// With no token present the two are the same string.
		/// </remarks>
		public string InputLabelText => DisplayText;

		/// <summary>
		/// Replaces the whole content with the given segments, putting the caret at the end.
		/// </summary>
		/// <remarks>
		/// The way a host restores a message with its tokens intact. The counterpart of
		/// <see cref="InputSegments"/>: what comes out of one goes back into the other unchanged.
		/// </remarks>
		/// <param name="segments">The content, in order. Null is treated as empty.</param>
		public void SetInputSegments(IEnumerable<PromptSegment> segments)
		{
			SetSegments(segments ?? Array.Empty<PromptSegment>());

			_cursorPosition = DisplayText.Length;
			_horizontalScrollOffset = 0;
			_verticalScrollOffset = 0;
			ClearSelection();
			InvalidateWrapCache();

			Invalidate(Invalidation.Relayout);
			RaiseInputChanged();
		}

		#endregion
	}
}

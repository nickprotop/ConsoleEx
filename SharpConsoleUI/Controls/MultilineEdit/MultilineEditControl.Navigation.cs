// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Controls
{
	public partial class MultilineEditControl
	{
		#region Wrapping Infrastructure

		/// <summary>
		/// Represents a single visual line after wrapping a source line.
		/// </summary>
		private readonly record struct WrappedLineInfo(
			int SourceLineIndex,
			int SourceCharOffset,
			int Length,
			string DisplayText
		);

		/// <summary>
		/// Builds (or returns cached) list of wrapped lines for the current content and effective width.
		/// This is the SINGLE SOURCE OF TRUTH for wrapping used by PaintDOM, cursor navigation,
		/// scrolling, and all position calculations.
		/// </summary>
		private List<WrappedLineInfo> GetWrappedLines(int effectiveWidth)
		{
			lock (_contentLock)
			{
				if (_wrappedLinesCache != null && _wrappedLinesCacheWidth == effectiveWidth)
					return _wrappedLinesCache;

				var result = new List<WrappedLineInfo>();
				int safeWidth = Math.Max(1, effectiveWidth);

				for (int i = 0; i < _lines.Count; i++)
				{
					string line = _lines[i];

					if (_wrapMode == WrapMode.NoWrap)
					{
						result.Add(new WrappedLineInfo(i, 0, line.Length, line));
					}
					else if (_wrapMode == WrapMode.Wrap)
					{
						if (line.Length == 0)
						{
							result.Add(new WrappedLineInfo(i, 0, 0, string.Empty));
						}
						else
						{
							int j = 0;
							while (j < line.Length)
							{
								var (endChar, _) = UnicodeWidth.TakeColumns(line, j, safeWidth);
								int len = endChar - j;
								result.Add(new WrappedLineInfo(i, j, len, line.Substring(j, len)));
								j = endChar;
							}
						}
					}
					else // WrapWords
					{
						BuildWordWrappedLines(result, line, i, safeWidth);
					}
				}

				_wrappedLinesCache = result;
				_wrappedLinesCacheWidth = effectiveWidth;
				return result;
			}
		}

		/// <summary>
		/// Word-wraps a single source line, preserving original spacing.
		/// </summary>
		private static void BuildWordWrappedLines(List<WrappedLineInfo> result, string line, int sourceIndex, int width)
		{
			if (string.IsNullOrEmpty(line))
			{
				result.Add(new WrappedLineInfo(sourceIndex, 0, 0, string.Empty));
				return;
			}

			int pos = 0;
			while (pos < line.Length)
			{
				// Compute how many whole runes fit within 'width' DISPLAY columns from pos.
				// fitEnd is always > pos (TakeColumns advances at least one rune) and <= line.Length,
				// so all indexing/substring below is in-bounds and the loop always advances.
				var (fitEnd, _) = UnicodeWidth.TakeColumns(line, pos, width);

				if (fitEnd >= line.Length)
				{
					int rem = line.Length - pos;
					result.Add(new WrappedLineInfo(sourceIndex, pos, rem, line.Substring(pos, rem)));
					break;
				}

				// Find the last space within the column-fitting char range [pos, fitEnd) to break at
				int breakAt = -1;
				for (int j = fitEnd - 1; j > pos; j--)
				{
					if (line[j] == ' ')
					{
						breakAt = j;
						break;
					}
				}

				if (breakAt > pos)
				{
					// Break at word boundary (include the space in this visual line)
					int len = breakAt - pos + 1;
					result.Add(new WrappedLineInfo(sourceIndex, pos, len, line.Substring(pos, len)));
					pos += len;
				}
				else
				{
					// No space found - force break at the column-fit boundary (long word)
					int len = fitEnd - pos;
					result.Add(new WrappedLineInfo(sourceIndex, pos, len, line.Substring(pos, len)));
					pos += len;
				}
			}
		}

		/// <summary>
		/// Test-only accessor exposing the wrapped segments (source index, char offset, char length,
		/// display text) for the given effective width. Used by unit tests to assert wrap correctness
		/// without exposing the private <see cref="WrappedLineInfo"/> type. Not part of the public API.
		/// </summary>
		internal IReadOnlyList<(int SourceLineIndex, int SourceCharOffset, int Length, string Text)> GetWrappedSegmentsForTest(int effectiveWidth)
		{
			return GetWrappedLines(effectiveWidth)
				.Select(wl => (wl.SourceLineIndex, wl.SourceCharOffset, wl.Length, wl.DisplayText))
				.ToList();
		}

		/// <summary>
		/// Test-only accessor exposing the maximum line length in DISPLAY columns (terminal cells),
		/// as used for horizontal-scroll clamping and scrollbar sizing. Not part of the public API.
		/// </summary>
		internal int GetMaxLineLengthForTest() => GetMaxLineLength();

		/// <summary>
		/// Test-only setter for the horizontal scroll offset (a DISPLAY COLUMN value). Lets tests
		/// drive the NoWrap horizontal-scroll slice without simulating keystrokes. Not public API.
		/// </summary>
		internal void SetHorizontalScrollOffsetForTest(int columnOffset) => _horizontalScrollOffset = columnOffset;

		/// <summary>
		/// Test-only accessor reading the current horizontal scroll offset (a DISPLAY COLUMN value).
		/// </summary>
		internal int GetHorizontalScrollOffsetForTest() => _horizontalScrollOffset;

		/// <summary>
		/// Test-only setter for the logical cursor position (char indices) without triggering the
		/// Container-gated EnsureCursorVisible. Used together with <see cref="EnsureCursorVisibleForTest"/>.
		/// </summary>
		internal void SetCursorForTest(int charX, int lineY)
		{
			_cursorX = charX;
			_cursorY = lineY;
		}

		/// <summary>
		/// Test-only entry point that runs the PRODUCTION NoWrap horizontal cursor-follow scroll logic
		/// (<see cref="EnsureCursorColumnVisible"/>) with an explicit effective width, bypassing the
		/// Container null-guard in <see cref="EnsureCursorVisible"/>. Exercises the real code path.
		/// </summary>
		internal void EnsureCursorVisibleForTest(int effectiveWidth) => EnsureCursorColumnVisible(effectiveWidth);

		private void InvalidateWrappedLinesCache()
		{
			// Note: callers that already hold _contentLock can call this safely
			// since this only nulls references (no collection iteration)
			_wrappedLinesCache = null;
			_syntaxTokenCache = null;
			_lineStateCache = null;
		}

		/// <summary>
		/// Finds the wrapped line index that contains the current cursor position.
		/// Returns -1 if not found.
		/// </summary>
		private int FindWrappedLineForCursor(List<WrappedLineInfo> wrappedLines)
		{
			for (int i = 0; i < wrappedLines.Count; i++)
			{
				var wl = wrappedLines[i];
				if (wl.SourceLineIndex != _cursorY) continue;

				int endOffset = wl.SourceCharOffset + wl.Length;

				// Cursor is within this wrapped line
				if (_cursorX >= wl.SourceCharOffset && _cursorX < endOffset)
					return i;

				// Cursor is at the end of the last wrapped segment for this source line
				if (_cursorX == endOffset)
				{
					bool nextIsSameLine = (i + 1 < wrappedLines.Count &&
										   wrappedLines[i + 1].SourceLineIndex == _cursorY);
					if (!nextIsSameLine)
						return i;
				}
			}
			return -1;
		}

		private int GetTotalWrappedLineCount()
		{
			if (_wrapMode == WrapMode.NoWrap)
			{
				lock (_contentLock) { return _lines.Count; }
			}
			return GetWrappedLines(SafeEffectiveWidth).Count;
		}

		/// <summary>
		/// Gets a safe effective width value, ensuring it's never zero to prevent division by zero errors.
		/// </summary>
		private int SafeEffectiveWidth => _effectiveWidth > 0 ? _effectiveWidth : ControlDefaults.DefaultEditorWidth;

		#endregion

		#region Cursor / Viewport

		/// <summary>
		/// Ensures the cursor position is visible within the viewport by adjusting scroll offsets.
		/// </summary>
		public void EnsureCursorVisible()
		{
			if (Container == null) return;

			// Guard against uninitialized _effectiveWidth (can be 0 before first render)
			int effectiveWidth = _effectiveWidth > 0 ? _effectiveWidth : ControlDefaults.DefaultEditorWidth;

			// Special handling for wrap mode - use shared wrapping infrastructure
			if (_wrapMode != WrapMode.NoWrap)
			{
				var wrappedLines = GetWrappedLines(effectiveWidth);
				int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
				if (wrappedIndex >= 0)
				{
					int evh = GetEffectiveViewportHeight();
					if (wrappedIndex < _verticalScrollOffset)
						_verticalScrollOffset = wrappedIndex;
					else if (wrappedIndex >= _verticalScrollOffset + evh)
						_verticalScrollOffset = wrappedIndex - evh + 1;
				}

				// In wrap mode, we don't need horizontal scrolling as lines are wrapped
				_horizontalScrollOffset = 0;
			}
			else
			{
				int evh = GetEffectiveViewportHeight();
				// Standard vertical scrolling for non-wrapped text
				if (_cursorY < _verticalScrollOffset)
				{
					_verticalScrollOffset = _cursorY;
				}
				else if (_cursorY >= _verticalScrollOffset + evh)
				{
					_verticalScrollOffset = _cursorY - evh + 1;
				}

				// Standard horizontal scrolling for non-wrapped text.
				EnsureCursorColumnVisible(effectiveWidth);
			}

		}

		/// <summary>
		/// Adjusts <see cref="_horizontalScrollOffset"/> (a DISPLAY COLUMN value) so the cursor's
		/// display column is within the visible viewport. <see cref="_cursorX"/> is a CHAR index but
		/// the offset and <paramref name="effectiveWidth"/> are columns, so the cursor must be
		/// converted to its column first — otherwise cursor-follow scrolling is wrong with wide
		/// (CJK) characters and a char index would be assigned into a column-valued field.
		/// </summary>
		private void EnsureCursorColumnVisible(int effectiveWidth)
		{
			string cursorLine = (_cursorY >= 0 && _cursorY < _lines.Count) ? _lines[_cursorY] : string.Empty;
			int cursorColumn = SharpConsoleUI.Helpers.UnicodeWidth.CharOffsetToColumn(cursorLine, _cursorX);
			if (cursorColumn < _horizontalScrollOffset)
			{
				_horizontalScrollOffset = cursorColumn;
			}
			else if (cursorColumn >= _horizontalScrollOffset + effectiveWidth)
			{
				_horizontalScrollOffset = cursorColumn - effectiveWidth + 1;
			}
		}

		/// <summary>
		/// Moves the cursor to the specified line number (1-based).
		/// Clamps to valid range. Clears any active selection.
		/// </summary>
		/// <param name="lineNumber">The 1-based line number to navigate to.</param>
		public void GoToLine(int lineNumber)
		{
			lock (_contentLock)
			{
				int targetLine = Math.Clamp(lineNumber - 1, 0, _lines.Count - 1);
				ClearSelection();
				_cursorY = targetLine;
				_cursorX = 0;
			}
			EnsureCursorVisible();
			Invalidate(Invalidation.Relayout);
			CursorPositionChanged?.Invoke(this, (CurrentLine, CurrentColumn));
		}

		/// <summary>
		/// Moves the cursor to the end of the document content and ensures it's visible.
		/// </summary>
		public void GoToEnd()
		{
			lock (_contentLock)
			{
				// Set cursor to the last line
				_cursorY = _lines.Count - 1;

				// Set cursor to the end of the last line
				_cursorX = _lines[_cursorY].Length;

				// Clear any selection
				ClearSelection();
			}

			// Ensure the cursor is visible in the viewport
			EnsureCursorVisible();

			// Invalidate cached content to force redraw
			Invalidate(Invalidation.Relayout);
		}

		#endregion

		#region Selection

		/// <summary>
		/// Gets the currently selected text.
		/// </summary>
		/// <returns>The selected text, or an empty string if no selection exists.</returns>
		public string GetSelectedText()
		{
			if (!_hasSelection) return string.Empty;

			lock (_contentLock)
			{
				if (_lines.Count == 0) return string.Empty;

				// Ensure start is before end
				(int startX, int startY, int endX, int endY) = GetOrderedSelectionBounds();

				// Validate bounds
				if (startY < 0 || startY >= _lines.Count || endY < 0 || endY >= _lines.Count)
					return string.Empty;

				// Clamp X positions to line lengths
				startX = Math.Max(0, Math.Min(startX, _lines[startY].Length));
				endX = Math.Max(0, Math.Min(endX, _lines[endY].Length));

				if (startY == endY)
				{
					// Selection on same line
					if (startX >= endX) return string.Empty;
					return _lines[startY].Substring(startX, endX - startX);
				}

				var result = new StringBuilder();

				// First line
				result.AppendLine(_lines[startY].Substring(startX));

				// Middle lines (if any)
				for (int i = startY + 1; i < endY; i++)
				{
					result.AppendLine(_lines[i]);
				}

				// Last line
				result.Append(_lines[endY].Substring(0, endX));

				return result.ToString();
			}
		}

		/// <summary>
		/// Clears the current text selection.
		/// </summary>
		public void ClearSelection()
		{
			_hasSelection = false;
			_selectionStartX = _selectionEndX = _cursorX;
			_selectionStartY = _selectionEndY = _cursorY;
		}

		/// <summary>
		/// Selects the range (startLine, startCol) to (endLine, endCol), 0-based.
		/// Moves cursor to the end of the selection and scrolls it into view.
		/// </summary>
		public void SelectRange(int startLine, int startCol, int endLine, int endCol)
		{
			lock (_contentLock)
			{
				_selectionStartY = Math.Clamp(startLine, 0, _lines.Count - 1);
				_selectionStartX = Math.Clamp(startCol, 0, _lines[_selectionStartY].Length);
				_selectionEndY = Math.Clamp(endLine, 0, _lines.Count - 1);
				_selectionEndX = Math.Clamp(endCol, 0, _lines[_selectionEndY].Length);
				_hasSelection = true;
				_cursorY = _selectionEndY;
				_cursorX = _selectionEndX;
			}
			NotifySelectionActive();
			EnsureCursorVisible();
			Invalidate(Invalidation.Repaint);
		}

		private (int startX, int startY, int endX, int endY) GetOrderedSelectionBounds()
		{
			if (_selectionStartY < _selectionEndY || (_selectionStartY == _selectionEndY && _selectionStartX <= _selectionEndX))
			{
				return (_selectionStartX, _selectionStartY, _selectionEndX, _selectionEndY);
			}
			else
			{
				return (_selectionEndX, _selectionEndY, _selectionStartX, _selectionStartY);
			}
		}

		#endregion

		#region ILogicalCursorProvider

		/// <inheritdoc/>
		public Point? GetLogicalCursorPosition()
		{
			// Only show cursor when in editing mode
			if (!_isEditing)
				return null;

			// Guard against uninitialized _effectiveWidth
			int effectiveWidth = _effectiveWidth > 0 ? _effectiveWidth : ControlDefaults.DefaultEditorWidth;
			int gutterWidth = GetGutterWidth();

			if (_wrapMode == WrapMode.NoWrap)
			{
				// Convert logical char offsets to display columns so wide (e.g. CJK)
				// characters — which the renderer draws 2 columns wide — don't shift
				// the cursor. See GitHub issue #23.
				int cursorColumn = GetDisplayColumn(_cursorY, _cursorX);
				int scrollColumn = GetDisplayColumn(_cursorY, _horizontalScrollOffset);
				return new Point(
					Margin.Left + gutterWidth + cursorColumn - scrollColumn,
					Margin.Top + _cursorY - _verticalScrollOffset);
			}
			else
			{
				// Use shared wrapping infrastructure for correct position in all wrap modes
				var wrappedLines = GetWrappedLines(effectiveWidth);
				int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
				if (wrappedIndex < 0) return null;

				int visualY = wrappedIndex - _verticalScrollOffset;
				// Measure the display width of the segment text preceding the cursor
				// rather than the raw char count, so wide characters render correctly.
				int segmentStart = wrappedLines[wrappedIndex].SourceCharOffset;
				int visualX = GetDisplayColumn(_cursorY, _cursorX) - GetDisplayColumn(_cursorY, segmentStart);

				return new Point(Margin.Left + gutterWidth + visualX, Margin.Top + visualY);
			}
		}

		/// <summary>
		/// Converts a logical character offset on the given source line into a display-column
		/// offset, accounting for wide characters that occupy two terminal columns.
		/// </summary>
		/// <param name="lineIndex">The source line index.</param>
		/// <param name="charOffset">The logical (UTF-16) character offset into the line.</param>
		/// <returns>The display-column offset measured from the start of the line.</returns>
		private int GetDisplayColumn(int lineIndex, int charOffset)
		{
			if (charOffset <= 0)
				return 0;

			lock (_contentLock)
			{
				if (lineIndex < 0 || lineIndex >= _lines.Count)
					return charOffset;

				string line = _lines[lineIndex];
				if (charOffset >= line.Length)
					return UnicodeWidth.GetStringWidth(line);

				return UnicodeWidth.GetStringWidth(line.Substring(0, charOffset));
			}
		}

		/// <summary>
		/// Converts a display-column offset on the given source line into a logical character
		/// offset. A click landing on either cell of a wide character resolves to that
		/// character's start. Used to translate mouse coordinates into cursor positions.
		/// </summary>
		/// <param name="lineIndex">The source line index.</param>
		/// <param name="targetColumn">The display-column offset measured from the start of the line.</param>
		/// <returns>The logical (UTF-16) character offset closest to the target column.</returns>
		private int GetCharOffsetFromColumn(int lineIndex, int targetColumn)
		{
			if (targetColumn <= 0)
				return 0;

			lock (_contentLock)
			{
				if (lineIndex < 0 || lineIndex >= _lines.Count)
					return targetColumn;

				string line = _lines[lineIndex];
				int column = 0;
				int charOffset = 0;
				Rune? lastMeasured = null;

				foreach (var rune in line.EnumerateRunes())
				{
					int runeCharLen = rune.Utf16SequenceLength;

					// Mirror UnicodeWidth.GetStringWidth: a VS16 after a widenable emoji
					// adds 1 column to the previous rune rather than starting a new cell.
					if (UnicodeWidth.IsVS16(rune) && lastMeasured != null &&
						UnicodeWidth.IsVs16Widened(lastMeasured.Value))
					{
						column += 1;
						charOffset += runeCharLen;
						lastMeasured = null;
						if (column >= targetColumn) return charOffset;
						continue;
					}

					int rw = UnicodeWidth.GetRuneWidth(rune);
					if (rw > 0) lastMeasured = rune;

					// If the target column falls within this rune's cells, snap to its start.
					if (column + rw > targetColumn)
						return charOffset;

					column += rw;
					charOffset += runeCharLen;
				}

				return charOffset; // past end of line
			}
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			if (_wrapMode != WrapMode.NoWrap)
			{
				int effectiveWidth = _effectiveWidth > 0 ? _effectiveWidth : ControlDefaults.DefaultEditorWidth;
				var wrappedLines = GetWrappedLines(effectiveWidth);
				return new System.Drawing.Size(effectiveWidth, wrappedLines.Count);
			}
			else
			{
				List<string> linesSnapshot;
				lock (_contentLock) { linesSnapshot = _lines.ToList(); }
				int maxWidth = linesSnapshot.Count > 0 ? linesSnapshot.Max(line => line.Length) : 0;
				return new System.Drawing.Size(maxWidth, linesSnapshot.Count);
			}
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(Point position)
		{
			lock (_contentLock)
			{
				// Set the logical cursor position and ensure it's valid
				_cursorX = Math.Max(0, position.X);
				_cursorY = Math.Max(0, Math.Min(position.Y, _lines.Count - 1));

				// Ensure X position is within the current line bounds
				if (_cursorY < _lines.Count)
				{
					_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
				}
			}

			// Update visual scroll position to ensure cursor is visible
			EnsureCursorVisible();

			// Invalidate the control for redraw
			Invalidate(Invalidation.Relayout);
		}

		#endregion
	}
}

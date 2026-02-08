// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Extensions;
using System.Drawing;
using System.Text;

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
						for (int j = 0; j < line.Length; j += safeWidth)
						{
							int len = Math.Min(safeWidth, line.Length - j);
							result.Add(new WrappedLineInfo(i, j, len, line.Substring(j, len)));
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
				int remaining = line.Length - pos;
				if (remaining <= width)
				{
					result.Add(new WrappedLineInfo(sourceIndex, pos, remaining, line.Substring(pos, remaining)));
					break;
				}

				// Find the last space within [pos, pos+width) to break at
				int breakAt = -1;
				for (int j = pos + width - 1; j > pos; j--)
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
					// No space found - force break at width (long word)
					result.Add(new WrappedLineInfo(sourceIndex, pos, width, line.Substring(pos, width)));
					pos += width;
				}
			}
		}

		private void InvalidateWrappedLinesCache()
		{
			_wrappedLinesCache = null;
			_syntaxTokenCache = null;
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
				return _lines.Count;
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

				// Standard horizontal scrolling for non-wrapped text
				if (_cursorX < _horizontalScrollOffset)
				{
					_horizontalScrollOffset = _cursorX;
				}
				else if (_cursorX >= _horizontalScrollOffset + effectiveWidth)
				{
					_horizontalScrollOffset = _cursorX - effectiveWidth + 1;
				}
			}

		}

		/// <summary>
		/// Moves the cursor to the specified line number (1-based).
		/// Clamps to valid range. Clears any active selection.
		/// </summary>
		/// <param name="lineNumber">The 1-based line number to navigate to.</param>
		public void GoToLine(int lineNumber)
		{
			int targetLine = Math.Clamp(lineNumber - 1, 0, _lines.Count - 1);
			ClearSelection();
			_cursorY = targetLine;
			_cursorX = 0;
			EnsureCursorVisible();
			Container?.Invalidate(true);
			CursorPositionChanged?.Invoke(this, (CurrentLine, CurrentColumn));
		}

		/// <summary>
		/// Moves the cursor to the end of the document content and ensures it's visible.
		/// </summary>
		public void GoToEnd()
		{
			// Set cursor to the last line
			_cursorY = _lines.Count - 1;

			// Set cursor to the end of the last line
			_cursorX = _lines[_cursorY].Length;

			// Clear any selection
			ClearSelection();

			// Ensure the cursor is visible in the viewport
			EnsureCursorVisible();

			// Invalidate cached content to force redraw
			Container?.Invalidate(true);
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

		/// <summary>
		/// Clears the current text selection.
		/// </summary>
		public void ClearSelection()
		{
			_hasSelection = false;
			_selectionStartX = _selectionEndX = _cursorX;
			_selectionStartY = _selectionEndY = _cursorY;
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
				return new Point(
					_margin.Left + gutterWidth + _cursorX - _horizontalScrollOffset,
					_margin.Top + _cursorY - _verticalScrollOffset);
			}
			else
			{
				// Use shared wrapping infrastructure for correct position in all wrap modes
				var wrappedLines = GetWrappedLines(effectiveWidth);
				int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
				if (wrappedIndex < 0) return null;

				int visualY = wrappedIndex - _verticalScrollOffset;
				int visualX = _cursorX - wrappedLines[wrappedIndex].SourceCharOffset;

				return new Point(_margin.Left + gutterWidth + visualX, _margin.Top + visualY);
			}
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			if (_wrapMode != WrapMode.NoWrap)
			{
				int effectiveWidth = _effectiveWidth > 0 ? _effectiveWidth : ControlDefaults.DefaultEditorWidth;
				var wrappedLines = GetWrappedLines(effectiveWidth);
				return new System.Drawing.Size(effectiveWidth, wrappedLines.Count);
			}
			else
			{
				int maxWidth = _lines.Count > 0 ? _lines.Max(line => line.Length) : 0;
				return new System.Drawing.Size(maxWidth, _lines.Count);
			}
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(Point position)
		{
			// Set the logical cursor position and ensure it's valid
			_cursorX = Math.Max(0, position.X);
			_cursorY = Math.Max(0, Math.Min(position.Y, _lines.Count - 1));

			// Ensure X position is within the current line bounds
			if (_cursorY < _lines.Count)
			{
				_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
			}

			// Update visual scroll position to ensure cursor is visible
			EnsureCursorVisible();

			// Invalidate the control for redraw
			Container?.Invalidate(true);
		}

		#endregion

		#region IFocusableControl

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			var hadFocus = _hasFocus;
			_hasFocus = focus;

			// Enter edit mode when focused with mouse
			if (focus && !hadFocus && reason == FocusReason.Mouse && !_readOnly)
			{
				IsEditing = true;
			}

			// Exit edit mode when losing focus
			if (!focus && hadFocus && _isEditing)
			{
				IsEditing = false;
			}

			Container?.Invalidate(true);

			// Fire focus events
			if (focus && !hadFocus)
				GotFocus?.Invoke(this, EventArgs.Empty);
			else if (!focus && hadFocus)
				LostFocus?.Invoke(this, EventArgs.Empty);

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		#endregion
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using System.Text;

namespace SharpConsoleUI.Controls
{
	public partial class MultilineEditControl
	{
		#region Syntax Cache Helpers

		// Removes cached tokens for lines >= lineIndex and cached states for lines > lineIndex.
		// State at lineIndex itself (= end-state of line lineIndex-1) is preserved so that
		// EnsureStateUpToLine can resume from there without re-scanning from 0.
		private void InvalidateSyntaxFromLine(int lineIndex)
		{
			if (_syntaxTokenCache != null)
				foreach (var k in _syntaxTokenCache.Keys.Where(k => k >= lineIndex).ToList())
					_syntaxTokenCache.Remove(k);

			if (_lineStateCache != null)
				foreach (var k in _lineStateCache.Keys.Where(k => k > lineIndex).ToList())
					_lineStateCache.Remove(k);
		}

		#endregion

		#region Content CRUD

		/// <summary>
		/// Appends content to the end of the control and scrolls to make it visible.
		/// </summary>
		/// <param name="content">The content to append.</param>
		public void AppendContent(string content)
		{
			if (string.IsNullOrEmpty(content))
				return;

			content = SanitizeInputText(content);

			lock (_contentLock)
			{
				int dirtyFromLine = Math.Max(0, _lines.Count - 1);
				InvalidateSyntaxFromLine(dirtyFromLine);

				// Split the content into lines
				var newLines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

				// If the last line in existing content is empty, replace it with first line of new content
				if (_lines.Count > 0 && string.IsNullOrEmpty(_lines[_lines.Count - 1]))
				{
					_lines[_lines.Count - 1] = newLines[0];

					// Add remaining lines
					for (int i = 1; i < newLines.Length; i++)
					{
						_lines.Add(newLines[i]);
					}
				}
				else
				{
					// If the last line isn't empty, append the first new line to it
					if (_lines.Count > 0 && newLines.Length > 0)
					{
						_lines[_lines.Count - 1] += newLines[0];

						// Add remaining lines
						for (int i = 1; i < newLines.Length; i++)
						{
							_lines.Add(newLines[i]);
						}
					}
					else
					{
						// Just add all lines if we don't have content yet
						_lines.AddRange(newLines);
					}
				}

				InvalidateWrappedLinesCache();
			}

			// Reset flag to ensure scroll positions are updated properly
			_skipUpdateScrollPositionsInRender = false;

			// Force recalculation of scrollbars by invalidating
			Container?.Invalidate(true);

			// Go to the end of the content
			GoToEnd();

			// Notify that content has changed
			ContentChanged?.Invoke(this, GetContent());
		}

		/// <summary>
		/// Appends multiple lines to the end of the control and scrolls to make them visible.
		/// </summary>
		/// <param name="lines">The lines to append.</param>
		public void AppendContentLines(List<string> lines)
		{
			if (lines == null || lines.Count == 0)
				return;

			lines = lines.Select(SanitizeLine).ToList();

			lock (_contentLock)
			{
				int dirtyFromLine = Math.Max(0, _lines.Count - 1);
				InvalidateSyntaxFromLine(dirtyFromLine);

				// If the last line in existing content is empty, replace it with first line of new content
				if (_lines.Count > 0 && string.IsNullOrEmpty(_lines[_lines.Count - 1]))
				{
					if (lines.Count > 0)
					{
						_lines[_lines.Count - 1] = lines[0];

						// Add remaining lines
						for (int i = 1; i < lines.Count; i++)
						{
							_lines.Add(lines[i]);
						}
					}
				}
				else
				{
					// If the last line isn't empty, append the first new line to it
					if (_lines.Count > 0 && lines.Count > 0)
					{
						_lines[_lines.Count - 1] += lines[0];

						// Add remaining lines
						for (int i = 1; i < lines.Count; i++)
						{
							_lines.Add(lines[i]);
						}
					}
					else
					{
						// Just add all lines if we don't have content yet
						_lines.AddRange(lines);
					}
				}

				InvalidateWrappedLinesCache();
			}

			// Reset flag to ensure scroll positions are updated properly
			_skipUpdateScrollPositionsInRender = false;

			// Force recalculation of scrollbars by invalidating
			Container?.Invalidate(true);

			// Go to the end of the content
			GoToEnd();

			// Notify that content has changed
			ContentChanged?.Invoke(this, GetContent());
		}

		/// <summary>
		/// Gets the text content as a single string with line breaks.
		/// </summary>
		/// <returns>The complete text content.</returns>
		public string GetContent()
		{
			List<string> linesSnapshot;
			lock (_contentLock) { linesSnapshot = _lines.ToList(); }
			return string.Join(Environment.NewLine, linesSnapshot);
		}

		/// <summary>
		/// Sets the text content from a string, splitting on line breaks.
		/// </summary>
		/// <param name="content">The text content to set.</param>
		public void SetContent(string content)
		{
			lock (_contentLock)
			{
				if (content == null)
				{
					_lines = new List<string>() { string.Empty };
				}
				else
				{
					content = SanitizeInputText(content);
					_lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
					if (_lines.Count == 0)
					{
						_lines.Add(string.Empty);
					}
				}

				InvalidateSyntaxFromLine(0);
				InvalidateWrappedLinesCache();
				ClearSelection();
				_undoStack.Clear();
				_redoStack.Clear();
				_isModified = false;
				_savedContent = null;
				_pendingUndoBefore = null;

				_cursorX = 0;
				_cursorY = 0;
				_horizontalScrollOffset = 0;
				_verticalScrollOffset = 0;
			}

			EnsureCursorVisible();

			Container?.Invalidate(false);

			_skipUpdateScrollPositionsInRender = false;

			// Notify that content has changed
			ContentChanged?.Invoke(this, GetContent());
		}

		/// <summary>
		/// Sets the content of the control using a list of strings, with each string representing a line.
		/// </summary>
		/// <param name="lines">The lines to set as content.</param>
		public void SetContentLines(List<string> lines)
		{
			lock (_contentLock)
			{
				if (lines == null || lines.Count == 0)
				{
					_lines = new List<string>() { string.Empty };
				}
				else
				{
					_lines = lines.Select(SanitizeLine).ToList();
					if (_lines.Count == 0)
					{
						_lines.Add(string.Empty);
					}
				}

				InvalidateSyntaxFromLine(0);
				InvalidateWrappedLinesCache();
				ClearSelection();
				_undoStack.Clear();
				_redoStack.Clear();
				_isModified = false;
				_savedContent = null;
				_pendingUndoBefore = null;

				_cursorX = 0;
				_cursorY = 0;
				_horizontalScrollOffset = 0;
				_verticalScrollOffset = 0;
			}

			EnsureCursorVisible();

			Container?.Invalidate(false);

			_skipUpdateScrollPositionsInRender = false;

			// Notify that content has changed
			ContentChanged?.Invoke(this, GetContent());
		}

		/// <summary>
		/// Insert text at the current cursor position
		/// </summary>
		public void InsertText(string text)
		{
			if (_readOnly || string.IsNullOrEmpty(text))
				return;

			text = SanitizeInputText(text);

			lock (_contentLock)
			{
				text = TruncateToMaxLength(text);
				if (text.Length == 0)
					return;

				InsertTextAtCursor(text);
				InvalidateWrappedLinesCache();
			}

			EnsureCursorVisible();
			Container?.Invalidate(true);
			ContentChanged?.Invoke(this, GetContent());
		}

		/// <summary>
		/// Deletes up to <paramref name="count"/> characters immediately before the cursor on the current line.
		/// Stops at column 0 (does not merge lines).
		/// </summary>
		public void DeleteCharsBefore(int count)
		{
			if (_readOnly || count <= 0) return;

			lock (_contentLock)
			{
				int toDelete = Math.Min(count, _cursorX);
				if (toDelete == 0) return;

				_lines[_cursorY] = _lines[_cursorY].Remove(_cursorX - toDelete, toDelete);
				_cursorX -= toDelete;

				InvalidateSyntaxFromLine(_cursorY);
				InvalidateWrappedLinesCache();
			}

			EnsureCursorVisible();
			Container?.Invalidate(true);
			ContentChanged?.Invoke(this, GetContent());
		}

		/// <summary>
		/// Inserts text at the current cursor position without firing events or invalidating.
		/// Used internally by ProcessKey (Ctrl+V) where the caller manages events.
		/// </summary>
		private void InsertTextAtCursor(string text)
		{
			int dirtyCursorY = _cursorY;
			var textLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			InvalidateSyntaxFromLine(dirtyCursorY);

			if (textLines.Length == 1)
			{
				_lines[_cursorY] = _lines[_cursorY].Insert(_cursorX, textLines[0]);
				_cursorX += textLines[0].Length;
			}
			else
			{
				var currentLine = _lines[_cursorY];
				var beforeCursor = currentLine.Substring(0, _cursorX);
				var afterCursor = currentLine.Substring(_cursorX);

				_lines[_cursorY] = beforeCursor + textLines[0];

				for (int i = 1; i < textLines.Length - 1; i++)
				{
					_lines.Insert(_cursorY + i, textLines[i]);
				}

				_lines.Insert(_cursorY + textLines.Length - 1, textLines[textLines.Length - 1] + afterCursor);

				_cursorY += textLines.Length - 1;
				_cursorX = textLines[textLines.Length - 1].Length;
			}
		}

		private void DeleteSelectedText()
		{
			if (!_hasSelection) return;
			if (_lines.Count == 0) return;

			InvalidateSyntaxFromLine(Math.Min(_selectionStartY, _selectionEndY));
			var (startX, startY, endX, endY) = GetOrderedSelectionBounds();

			// Validate bounds
			if (startY < 0 || startY >= _lines.Count || endY < 0 || endY >= _lines.Count)
			{
				ClearSelection();
				return;
			}

			// Clamp X positions to line lengths
			startX = Math.Max(0, Math.Min(startX, _lines[startY].Length));
			endX = Math.Max(0, Math.Min(endX, _lines[endY].Length));

			if (startY == endY)
			{
				// Selection on the same line
				if (startX < endX)
				{
					_lines[startY] = _lines[startY].Remove(startX, endX - startX);
				}
			}
			else
			{
				// Selection spans multiple lines

				// Get the part before selection on the first line
				string firstLineStart = _lines[startY].Substring(0, startX);

				// Get the part after selection on the last line
				string lastLineEnd = _lines[endY].Substring(endX);

				// Create the joined line
				_lines[startY] = firstLineStart + lastLineEnd;

				// Remove the lines in between
				int linesToRemove = endY - startY;
				if (linesToRemove > 0 && startY + 1 < _lines.Count)
				{
					_lines.RemoveRange(startY + 1, Math.Min(linesToRemove, _lines.Count - startY - 1));
				}
			}

			// Move cursor to the selection start
			_cursorX = startX;
			_cursorY = startY;

			InvalidateWrappedLinesCache();
			// Clear the selection
			ClearSelection();
		}

		#endregion

		#region Input Validation

		/// <summary>
		/// Converts tabs to spaces and strips dangerous control characters from input text.
		/// Preserves newline characters (\n, \r) for line splitting.
		/// </summary>
		private string SanitizeInputText(string text)
		{
			text = text.Replace("\t", new string(' ', _tabSize));

			var sb = new StringBuilder(text.Length);
			foreach (char c in text)
			{
				if (c == '\n' || c == '\r' || !char.IsControl(c))
					sb.Append(c);
			}
			return sb.ToString();
		}

		/// <summary>
		/// Sanitizes each line individually (no newline handling needed).
		/// </summary>
		private string SanitizeLine(string line)
		{
			line = line.Replace("\t", new string(' ', _tabSize));

			var sb = new StringBuilder(line.Length);
			foreach (char c in line)
			{
				if (!char.IsControl(c))
					sb.Append(c);
			}
			return sb.ToString();
		}

		private int GetMaxLineLength()
		{
			List<string> linesSnapshot;
			lock (_contentLock) { linesSnapshot = _lines.ToList(); }
			int maxLength = 0;
			foreach (var line in linesSnapshot)
			{
				if (line.Length > maxLength)
					maxLength = line.Length;
			}
			return maxLength;
		}

		/// <summary>
		/// Returns the number of characters available for insertion before MaxLength is reached.
		/// Returns int.MaxValue if no limit is set.
		/// </summary>
		private int GetRemainingCapacity()
		{
			if (!_maxLength.HasValue)
				return int.MaxValue;

			int currentLength = 0;
			for (int i = 0; i < _lines.Count; i++)
			{
				currentLength += _lines[i].Length;
				if (i < _lines.Count - 1)
					currentLength += Environment.NewLine.Length;
			}
			return Math.Max(0, _maxLength.Value - currentLength);
		}

		/// <summary>
		/// Truncates text to fit within the remaining MaxLength capacity.
		/// Returns the truncated text, or the original if no limit or text fits.
		/// </summary>
		private string TruncateToMaxLength(string text)
		{
			int remaining = GetRemainingCapacity();
			if (remaining >= text.Length)
				return text;
			if (remaining <= 0)
				return string.Empty;
			return text.Substring(0, remaining);
		}

		#endregion

		#region Undo/Redo Infrastructure

		private sealed class UndoAction
		{
			public required string OldText { get; init; }
			public required string NewText { get; init; }
			public required int CursorXBefore { get; init; }
			public required int CursorYBefore { get; init; }
			public required int CursorXAfter { get; init; }
			public required int CursorYAfter { get; init; }
		}

		private void BeginUndoAction()
		{
			_pendingUndoBefore = GetContent();
			_pendingCursorXBefore = _cursorX;
			_pendingCursorYBefore = _cursorY;
		}

		private void CommitUndoAction()
		{
			if (_pendingUndoBefore == null) return;
			string after = GetContent();
			if (after == _pendingUndoBefore) { _pendingUndoBefore = null; return; }

			_undoStack.Push(new UndoAction
			{
				OldText = _pendingUndoBefore,
				NewText = after,
				CursorXBefore = _pendingCursorXBefore,
				CursorYBefore = _pendingCursorYBefore,
				CursorXAfter = _cursorX,
				CursorYAfter = _cursorY
			});

			// Trim stack to limit
			if (_undoStack.Count > _undoLimit)
			{
				var temp = _undoStack.ToArray();
				_undoStack.Clear();
				for (int i = 0; i < _undoLimit; i++)
					_undoStack.Push(temp[i]);
			}

			_redoStack.Clear();
			_pendingUndoBefore = null;
			_isModified = _savedContent != after;
		}

		/// <summary>
		/// Sets content without clearing undo/redo stacks or firing events.
		/// Used internally by undo/redo operations.
		/// </summary>
		private void SetContentInternal(string content)
		{
			lock (_contentLock)
			{
				_lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
				if (_lines.Count == 0)
					_lines.Add(string.Empty);
				InvalidateSyntaxFromLine(0);
				InvalidateWrappedLinesCache();
			}
		}

		/// <summary>
		/// Marks the current content as the saved state for IsModified tracking.
		/// </summary>
		public void MarkAsSaved()
		{
			_savedContent = GetContent();
			_isModified = false;
		}

		#endregion
	}
}

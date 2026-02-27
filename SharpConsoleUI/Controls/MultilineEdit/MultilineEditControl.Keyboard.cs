// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using System.Text;

namespace SharpConsoleUI.Controls
{
	public partial class MultilineEditControl
	{
		/// <inheritdoc/>
		/// <summary>
		/// KEY BUBBLING PHILOSOPHY:
		/// - Keys are only consumed (return true) if this control actually processes them
		/// - Unhandled keys bubble up (return false) to window/application handlers
		/// - In NOT EDITING mode: Only consume arrow keys for scrolling and Enter to start editing
		/// - In EDITING mode: Only consume keys that change content, cursor, or selection state
		/// - This ensures application shortcuts (Ctrl+S, Ctrl+P, etc.) work even when control has focus
		/// </summary>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled) return false;

			// Any keyboard action clears manual scroll override so cursor-follow resumes
			_scrollbarInteracted = false;

			// When focused but not editing, only handle specific navigation keys
			// All other keys (including Ctrl/Alt/Shift combinations) bubble up
			if (_hasFocus && !_isEditing)
			{
				switch (key.Key)
				{
					case ConsoleKey.Enter:
						IsEditing = true;
						return true;

					case ConsoleKey.LeftArrow:
						// Scroll content left if not at leftmost position
						if (_wrapMode == WrapMode.NoWrap && _horizontalScrollOffset > 0)
						{
							_skipUpdateScrollPositionsInRender = true;
							_horizontalScrollOffset--;
											Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.RightArrow:
						// Scroll content right if not at rightmost position
						if (_wrapMode == WrapMode.NoWrap)
						{
							int maxLineLength = GetMaxLineLength();
							if (_horizontalScrollOffset < maxLineLength - _effectiveWidth)
							{
								_skipUpdateScrollPositionsInRender = true;
								_horizontalScrollOffset++;
												Container?.Invalidate(true);
								return true;
							}
						}
						return false;

					case ConsoleKey.UpArrow:
						// Scroll content up if not at top
						if (_verticalScrollOffset > 0)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset--;
											Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.DownArrow:
						// Scroll content down if not at bottom
						int totalLines = GetTotalWrappedLineCount();
						if (_verticalScrollOffset < totalLines - GetEffectiveViewportHeight())
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset++;
											Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageUp:
						// Page up scrolling - move view up by viewport height
						int pageUpAmount = Math.Min(GetEffectiveViewportHeight(), _verticalScrollOffset);
						if (pageUpAmount > 0)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset -= pageUpAmount;
											Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageDown:
						// Page down scrolling - move view down by viewport height
						int totalWrappedLines = GetTotalWrappedLineCount();
						int evh = GetEffectiveViewportHeight();
						int pageDownAmount = Math.Min(evh, totalWrappedLines - _verticalScrollOffset - evh);
						if (pageDownAmount > 0)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset += pageDownAmount;
											Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.Home:
						// Scroll to top of document
						if (_verticalScrollOffset > 0 || _horizontalScrollOffset > 0)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset = 0;
							_horizontalScrollOffset = 0;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.End:
						// Scroll to bottom of document
						int endOffset = Math.Max(0, GetTotalWrappedLineCount() - GetEffectiveViewportHeight());
						if (_verticalScrollOffset != endOffset)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset = endOffset;
											Container?.Invalidate(true);
							return true;
						}
						return false;

					default:
						// Let other keys pass through
						return false;
				}
			}

			if (!_isEditing)
			{
				if (_hasFocus && key.Key == ConsoleKey.Enter)
				{
					IsEditing = true;
					return true;
				}
				return false;
			}

			bool contentChanged = false;
			bool isShiftPressed = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
			bool isCtrlPressed = key.Modifiers.HasFlag(ConsoleModifiers.Control);
			int oldCursorX;
			int oldCursorY;
			bool oldHasSelection;
			int oldSelEndX;
			int oldSelEndY;
			bool cursorMoved;
			bool selectionChanged;
			bool keyWasHandled;

		  lock (_contentLock)
		  {
			BeginUndoAction();
			oldCursorX = _cursorX;
			oldCursorY = _cursorY;
			oldHasSelection = _hasSelection;
			oldSelEndX = _selectionEndX;
			oldSelEndY = _selectionEndY;

			// If starting selection with Shift key
			if (isShiftPressed && !_hasSelection &&
				(key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow ||
				 key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow ||
				 key.Key == ConsoleKey.Home || key.Key == ConsoleKey.End ||
				 key.Key == ConsoleKey.PageUp || key.Key == ConsoleKey.PageDown))
			{
				_hasSelection = true;
				_selectionStartX = _cursorX;
				_selectionStartY = _cursorY;
			}
			// If continuing selection with Shift key
			else if (isShiftPressed && _hasSelection)
			{
				// Selection continues, update end will happen after cursor movement
			}
			// If movement without Shift key, clear selection
			else if (!isShiftPressed && _hasSelection &&
					 (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow ||
					  key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow ||
					  key.Key == ConsoleKey.Home || key.Key == ConsoleKey.End ||
					  key.Key == ConsoleKey.PageUp || key.Key == ConsoleKey.PageDown))
			{
				ClearSelection();
			}

			switch (key.Key)
			{
				case ConsoleKey.LeftArrow:
					if (isCtrlPressed)
					{
						if (_cursorX > 0)
							_cursorX = WordBoundaryHelper.FindPreviousWordBoundary(_lines[_cursorY], _cursorX);
						else if (_cursorY > 0)
						{
							_cursorY--;
							_cursorX = _lines[_cursorY].Length;
						}
					}
					else
					{
						if (_cursorX > 0)
						{
							_cursorX--;
						}
						else if (_cursorY > 0)
						{
							_cursorY--;
							_cursorX = _lines[_cursorY].Length;
						}
					}
					break;

				case ConsoleKey.RightArrow:
					if (isCtrlPressed)
					{
						if (_cursorX < _lines[_cursorY].Length)
							_cursorX = WordBoundaryHelper.FindNextWordBoundary(_lines[_cursorY], _cursorX);
						else if (_cursorY < _lines.Count - 1)
						{
							_cursorY++;
							_cursorX = 0;
						}
					}
					else
					{
						if (_cursorX < _lines[_cursorY].Length)
						{
							_cursorX++;
						}
						else if (_cursorY < _lines.Count - 1)
						{
							_cursorY++;
							_cursorX = 0;
						}
					}
					break;

				case ConsoleKey.UpArrow:
					if (_wrapMode != WrapMode.NoWrap)
					{
						var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
						int idx = FindWrappedLineForCursor(wrappedLines);
						if (idx > 0)
						{
							// Compute horizontal offset within current wrapped line
							int visualX = _cursorX - wrappedLines[idx].SourceCharOffset;
							var prev = wrappedLines[idx - 1];
							_cursorY = prev.SourceLineIndex;
							_cursorX = prev.SourceCharOffset + Math.Min(visualX, prev.Length);
						}
					}
					else
					{
						if (_cursorY > 0)
						{
							_cursorY--;
							_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
						}
					}
					break;

				case ConsoleKey.DownArrow:
					if (_wrapMode != WrapMode.NoWrap)
					{
						var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
						int idx = FindWrappedLineForCursor(wrappedLines);
						if (idx >= 0 && idx < wrappedLines.Count - 1)
						{
							int visualX = _cursorX - wrappedLines[idx].SourceCharOffset;
							var next = wrappedLines[idx + 1];
							_cursorY = next.SourceLineIndex;
							_cursorX = next.SourceCharOffset + Math.Min(visualX, next.Length);
						}
					}
					else
					{
						if (_cursorY < _lines.Count - 1)
						{
							_cursorY++;
							_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
						}
					}
					break;

				case ConsoleKey.Home:
					if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
					{
						_cursorX = 0;
						_cursorY = 0;
					}
					else
					{
						if (_wrapMode != WrapMode.NoWrap)
						{
							var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
							int idx = FindWrappedLineForCursor(wrappedLines);
							if (idx >= 0)
								_cursorX = wrappedLines[idx].SourceCharOffset;
							else
								_cursorX = 0;
						}
						else
						{
							_cursorX = 0;
						}
					}
					break;

				case ConsoleKey.End:
					if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
					{
						_cursorY = _lines.Count - 1;
						_cursorX = _lines[_cursorY].Length;
					}
					else
					{
						if (_wrapMode != WrapMode.NoWrap)
						{
							var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
							int idx = FindWrappedLineForCursor(wrappedLines);
							if (idx >= 0)
							{
								var wl = wrappedLines[idx];
								_cursorX = wl.SourceCharOffset + wl.Length;
							}
							else
							{
								_cursorX = _lines[_cursorY].Length;
							}
						}
						else
						{
							_cursorX = _lines[_cursorY].Length;
						}
					}
					break;

				case ConsoleKey.PageUp:
					if (_wrapMode != WrapMode.NoWrap)
					{
						var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
						int idx = FindWrappedLineForCursor(wrappedLines);
						if (idx >= 0)
						{
							int visualX = _cursorX - wrappedLines[idx].SourceCharOffset;
							int targetIdx = Math.Max(0, idx - GetEffectiveViewportHeight());
							var target = wrappedLines[targetIdx];
							_cursorY = target.SourceLineIndex;
							_cursorX = target.SourceCharOffset + Math.Min(visualX, target.Length);
						}
					}
					else
					{
						_cursorY = Math.Max(0, _cursorY - GetEffectiveViewportHeight());
						_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
					}
					break;

				case ConsoleKey.PageDown:
					if (_wrapMode != WrapMode.NoWrap)
					{
						var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
						int idx = FindWrappedLineForCursor(wrappedLines);
						if (idx >= 0)
						{
							int visualX = _cursorX - wrappedLines[idx].SourceCharOffset;
							int targetIdx = Math.Min(wrappedLines.Count - 1, idx + GetEffectiveViewportHeight());
							var target = wrappedLines[targetIdx];
							_cursorY = target.SourceLineIndex;
							_cursorX = target.SourceCharOffset + Math.Min(visualX, target.Length);
						}
					}
					else
					{
						_cursorY = Math.Min(_lines.Count - 1, _cursorY + GetEffectiveViewportHeight());
						_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
					}
					break;

				case ConsoleKey.Backspace:
					if (_readOnly) break;

					if (_hasSelection)
					{
						DeleteSelectedText();
						contentChanged = true;
					}
					else if (isCtrlPressed)
					{
						// Delete previous word
						if (_cursorX > 0)
						{
							int newPos = WordBoundaryHelper.FindPreviousWordBoundary(_lines[_cursorY], _cursorX);
							_lines[_cursorY] = _lines[_cursorY].Remove(newPos, _cursorX - newPos);
							_cursorX = newPos;
							contentChanged = true;
						}
						else if (_cursorY > 0)
						{
							int previousLineLength = _lines[_cursorY - 1].Length;
							_lines[_cursorY - 1] = _lines[_cursorY - 1] + _lines[_cursorY];
							_lines.RemoveAt(_cursorY);
							_cursorY--;
							_cursorX = previousLineLength;
							contentChanged = true;
						}
					}
					else if (_cursorX > 0)
					{
						_lines[_cursorY] = _lines[_cursorY].Remove(_cursorX - 1, 1);
						_cursorX--;
						contentChanged = true;
					}
					else if (_cursorY > 0)
					{
						int previousLineLength = _lines[_cursorY - 1].Length;
						_lines[_cursorY - 1] = _lines[_cursorY - 1] + _lines[_cursorY];
						_lines.RemoveAt(_cursorY);
						_cursorY--;
						_cursorX = previousLineLength;
						contentChanged = true;
					}
					break;

				case ConsoleKey.Delete:
					if (_readOnly) break;

					if (_hasSelection)
					{
						DeleteSelectedText();
						contentChanged = true;
					}
					else if (isCtrlPressed)
					{
						// Delete next word
						if (_cursorX < _lines[_cursorY].Length)
						{
							int endPos = WordBoundaryHelper.FindNextWordBoundary(_lines[_cursorY], _cursorX);
							_lines[_cursorY] = _lines[_cursorY].Remove(_cursorX, endPos - _cursorX);
							contentChanged = true;
						}
						else if (_cursorY < _lines.Count - 1)
						{
							_lines[_cursorY] = _lines[_cursorY] + _lines[_cursorY + 1];
							_lines.RemoveAt(_cursorY + 1);
							contentChanged = true;
						}
					}
					else if (_cursorX < _lines[_cursorY].Length)
					{
						_lines[_cursorY] = _lines[_cursorY].Remove(_cursorX, 1);
						contentChanged = true;
					}
					else if (_cursorY < _lines.Count - 1)
					{
						_lines[_cursorY] = _lines[_cursorY] + _lines[_cursorY + 1];
						_lines.RemoveAt(_cursorY + 1);
						contentChanged = true;
					}
					break;

				case ConsoleKey.Enter:
					// Any modified Enter key (Ctrl/Alt/Shift) is a command, not text insertion
					// Let the window/application handle it (e.g., Ctrl+Enter to send, Shift+Enter for special actions)
					if (isCtrlPressed || isShiftPressed || key.Modifiers.HasFlag(ConsoleModifiers.Alt))
					{
						return false;
					}

					if (_readOnly) break;

					if (_hasSelection)
					{
						// Delete selected text first
						DeleteSelectedText();
						contentChanged = true;
					}

					// MaxLength enforcement for newline insertion
					if (GetRemainingCapacity() < Environment.NewLine.Length)
						break;

					// Insert line break
					string currentLine = _lines[_cursorY];
					string lineBeforeCursor = currentLine.Substring(0, _cursorX);
					string lineAfterCursor = currentLine.Substring(_cursorX);

					_lines[_cursorY] = lineBeforeCursor;
					_lines.Insert(_cursorY + 1, lineAfterCursor);

					_cursorY++;
					_cursorX = 0;

					if (_autoIndent)
					{
						int indent = 0;
						while (indent < lineBeforeCursor.Length && lineBeforeCursor[indent] == ' ')
							indent++;
						if (indent > 0)
						{
							string indentStr = new string(' ', indent);
							_lines[_cursorY] = indentStr + _lines[_cursorY];
							_cursorX = indent;
						}
					}

					contentChanged = true;
					break;

				case ConsoleKey.Insert:
					OverwriteMode = !_overwriteMode;
					return true;

				case ConsoleKey.Escape:
					if (_hasSelection)
					{
						ClearSelection();
						Container?.Invalidate(true);
						return true;
					}
					if (_isEditing)
					{
						IsEditing = false;
						return true;
					}
					return false;

				case ConsoleKey.Tab:
					if (_readOnly) break;
					if (isShiftPressed)
					{
						// Shift+Tab: dedent
						if (_hasSelection)
						{
							var (sX, sY, eX, eY) = GetOrderedSelectionBounds();
							for (int ln = sY; ln <= eY; ln++)
							{
								int spaces = 0;
								while (spaces < _tabSize && spaces < _lines[ln].Length && _lines[ln][spaces] == ' ')
									spaces++;
								if (spaces > 0)
									_lines[ln] = _lines[ln].Substring(spaces);
							}
							contentChanged = true;
						}
						else
						{
							int spaces = 0;
							while (spaces < _tabSize && spaces < _lines[_cursorY].Length && _lines[_cursorY][spaces] == ' ')
								spaces++;
							if (spaces > 0)
							{
								_lines[_cursorY] = _lines[_cursorY].Substring(spaces);
								_cursorX = Math.Max(0, _cursorX - spaces);
								contentChanged = true;
							}
						}
					}
					else
					{
						// Tab: indent
						if (_hasSelection)
						{
							var (sX, sY, eX, eY) = GetOrderedSelectionBounds();
							string indent = new string(' ', _tabSize);
							int totalNeeded = (eY - sY + 1) * _tabSize;
							if (GetRemainingCapacity() >= totalNeeded)
							{
								for (int ln = sY; ln <= eY; ln++)
									_lines[ln] = indent + _lines[ln];
								contentChanged = true;
							}
						}
						else
						{
							int spacesToInsert = _tabSize - (_cursorX % _tabSize);
							if (GetRemainingCapacity() >= spacesToInsert)
							{
								_lines[_cursorY] = _lines[_cursorY].Insert(_cursorX, new string(' ', spacesToInsert));
								_cursorX += spacesToInsert;
								contentChanged = true;
							}
						}
					}
					break;

				default:
					// Handle Ctrl key combos
					if (isCtrlPressed)
					{
						switch (key.Key)
						{
							case ConsoleKey.A:
								// Ctrl+A: Select All
								if (_lines.Count > 0)
								{
									_hasSelection = true;
									_selectionStartX = 0;
									_selectionStartY = 0;
									_selectionEndX = _lines[_lines.Count - 1].Length;
									_selectionEndY = _lines.Count - 1;
									_cursorX = _selectionEndX;
									_cursorY = _selectionEndY;
									Container?.Invalidate(true);
								}
								return true;

							// Ctrl+C/X/V/Z/Y: standard clipboard and undo shortcuts
							// Ctrl+C is safe because TreatControlCAsInput = true in NetConsoleDriver
							case ConsoleKey.C:
								if (_hasSelection)
									ClipboardHelper.SetText(GetSelectedText());
								return true;

							case ConsoleKey.X:
								if (!_readOnly && _hasSelection)
								{
									ClipboardHelper.SetText(GetSelectedText());
									DeleteSelectedText();
									CommitUndoAction();
									InvalidateWrappedLinesCache();
									EnsureCursorVisible();
									Container?.Invalidate(true);
									ContentChanged?.Invoke(this, GetContent());
								}
								return true;

							case ConsoleKey.V:
								if (!_readOnly)
								{
									string clipText = ClipboardHelper.GetText();
									if (!string.IsNullOrEmpty(clipText))
									{
										clipText = SanitizeInputText(clipText);
										if (_hasSelection) DeleteSelectedText();
										clipText = TruncateToMaxLength(clipText);
										if (clipText.Length > 0)
										{
											InsertTextAtCursor(clipText);
											CommitUndoAction();
											InvalidateWrappedLinesCache();
											EnsureCursorVisible();
											Container?.Invalidate(true);
											ContentChanged?.Invoke(this, GetContent());
										}
									}
								}
								return true;

							case ConsoleKey.Z:
								if (_undoStack.Count > 0)
								{
									var action = _undoStack.Pop();
									_redoStack.Push(action);
									SetContentInternal(action.OldText);
									_cursorX = action.CursorXBefore;
									_cursorY = action.CursorYBefore;
									ClearSelection();
									_isModified = _savedContent != action.OldText;
									EnsureCursorVisible();
									Container?.Invalidate(true);
									ContentChanged?.Invoke(this, GetContent());
								}
								return true;

							case ConsoleKey.Y:
								if (_redoStack.Count > 0)
								{
									var action = _redoStack.Pop();
									_undoStack.Push(action);
									SetContentInternal(action.NewText);
									_cursorX = action.CursorXAfter;
									_cursorY = action.CursorYAfter;
									ClearSelection();
									_isModified = _savedContent != action.NewText;
									EnsureCursorVisible();
									Container?.Invalidate(true);
									ContentChanged?.Invoke(this, GetContent());
								}
								return true;

							default:
								break; // Other Ctrl combos bubble up
						}
					}

					// Let unhandled Ctrl/Alt combos bubble up
					if (key.Modifiers.HasFlag(ConsoleModifiers.Control) ||
					    key.Modifiers.HasFlag(ConsoleModifiers.Alt))
					{
						break;
					}

					if (!_readOnly && !char.IsControl(key.KeyChar))
					{
						if (_hasSelection)
						{
							// Replace selected text with typed character
							DeleteSelectedText();
							contentChanged = true;
						}

						// MaxLength enforcement for single character
						if (_overwriteMode && _cursorX < _lines[_cursorY].Length)
						{
							// Overwrite: replace character at cursor position
							var sb = new StringBuilder(_lines[_cursorY]);
							sb[_cursorX] = key.KeyChar;
							_lines[_cursorY] = sb.ToString();
							_cursorX++;
							contentChanged = true;
						}
						else if (GetRemainingCapacity() > 0)
						{
							_lines[_cursorY] = _lines[_cursorY].Insert(_cursorX, key.KeyChar.ToString());
							_cursorX++;
							contentChanged = true;
						}
					}
					break;
			}

			// Update selection end if we're in selection mode
			if (isShiftPressed && _hasSelection)
			{
				_selectionEndX = _cursorX;
				_selectionEndY = _cursorY;
				Container?.Invalidate(true);
			}

			// If content changed, invalidate caches and commit undo BEFORE ensuring cursor visibility
			// so EnsureCursorVisible works with fresh wrap data (not stale cached wrapped lines)
			if (contentChanged)
			{
				CommitUndoAction();
				InvalidateWrappedLinesCache();
			}

			// If cursor position changed, ensure it's visible
			if (_cursorX != oldCursorX || _cursorY != oldCursorY)
			{
				EnsureCursorVisible();
				Container?.Invalidate(true);
			}

			// Only consume the key if we actually did something with it
			// Check if content, cursor position, or selection state changed
			cursorMoved = (_cursorX != oldCursorX || _cursorY != oldCursorY);
			selectionChanged = (_hasSelection != oldHasSelection) ||
				(_hasSelection && (_selectionEndX != oldSelEndX || _selectionEndY != oldSelEndY));
			keyWasHandled = contentChanged || cursorMoved || selectionChanged;

		  } // end lock (_contentLock)

			// Fire events outside the lock to avoid potential deadlocks
			if (contentChanged)
			{
				Container?.Invalidate(true);
				ContentChanged?.Invoke(this, GetContent());
			}

			// Fire cursor position changed event
			if (cursorMoved)
				CursorPositionChanged?.Invoke(this, (CurrentLine, CurrentColumn));

			// Fire selection changed event
			if (selectionChanged)
				SelectionChanged?.Invoke(this, _hasSelection ? GetSelectedText() : string.Empty);

			return keyWasHandled;
		}
	}
}

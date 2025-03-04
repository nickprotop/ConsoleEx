// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;
using Spectre.Console;
using System.Text;

namespace ConsoleEx.Controls
{
	public enum WrapMode
	{
		NoWrap,
		Wrap,
		WrapWords
	}

	public class MultilineEditControl : IWIndowControl, IInteractiveControl
	{
		private Alignment _alignment = Alignment.Left;

		// Color properties
		private Color? _backgroundColorValue;

		private Color _borderColor = Color.White;
		private List<string>? _cachedContent;
		private int _cursorX = 0;
		private int _cursorY = 0;
		private int _effectiveWidth;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus = false;
		private bool _hasSelection = false;
		private int _horizontalScrollOffset = 0;
		private bool _invalidated = true;
		private bool _isEditing = false;
		private bool _isEnabled = true;
		private List<string> _lines = new List<string>() { string.Empty };
		private Margin _margin = new Margin(0, 0, 0, 0);
		private Color? _selectionBackgroundColorValue;
		private int _selectionEndX = 0;
		private int _selectionEndY = 0;
		private Color? _selectionForegroundColorValue;
		private int _selectionStartX = 0;
		private int _selectionStartY = 0;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private int _verticalScrollOffset = 0;
		private int _viewportHeight;
		private bool _visible = true;
		private int? _width;
		private WrapMode _wrapMode = WrapMode.Wrap;

		// Constructors
		public MultilineEditControl(int viewportHeight = 10)
		{
			_viewportHeight = Math.Max(1, viewportHeight);
		}

		public MultilineEditControl(string initialContent, int viewportHeight = 10)
		{
			_viewportHeight = Math.Max(1, viewportHeight);
			SetContent(initialContent);
		}

		// Event for content changes
		public event EventHandler<string>? ContentChanged;

		public int? ActualWidth
		{
			get
			{
				if (_cachedContent == null) return null;

				int maxLength = 0;
				foreach (var line in _cachedContent)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		public Alignment Alignment
		{ get => _alignment; set { _alignment = value; _cachedContent = null; Container?.Invalidate(true); } }

		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputBackgroundColor ?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color BorderColor
		{
			get => _borderColor;
			set
			{
				_borderColor = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public IContainer? Container { get; set; }

		public Color FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedBackgroundColor ?? Color.White;
			set
			{
				_focusedBackgroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color FocusedForegroundColor
		{
			get => _focusedForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedForegroundColor ?? Color.White;
			set
			{
				_focusedForegroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				_hasFocus = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool IsEditing
		{ get => _isEditing; set { _isEditing = value; Invalidate(); Container?.Invalidate(false); } }

		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_isEnabled = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Margin Margin
		{ get => _margin; set { _margin = value; _cachedContent = null; Container?.Invalidate(true); } }

		public Color SelectionBackgroundColor
		{
			get => _selectionBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Blue;
			set
			{
				_selectionBackgroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color SelectionForegroundColor
		{
			get => _selectionForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.White;
			set
			{
				_selectionForegroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		public object? Tag { get; set; }

		public int ViewportHeight
		{
			get => _viewportHeight;
			set
			{
				_viewportHeight = Math.Max(1, value);
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool Visible
		{ get => _visible; set { _visible = value; _cachedContent = null; Container?.Invalidate(true); } }

		public int? Width
		{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(true); } }

		public WrapMode WrapMode
		{
			get => _wrapMode;
			set
			{
				_wrapMode = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		// Add a helper method to clear selection
		public void ClearSelection()
		{
			_hasSelection = false;
			_selectionStartX = _selectionEndX = _cursorX;
			_selectionStartY = _selectionEndY = _cursorY;
			_cachedContent = null;
		}

		// Required interface methods
		public void Dispose()
		{
			Container = null;
		}

		// Cursor management
		public void EnsureCursorVisible()
		{
			// Special handling for wrap mode
			if (_wrapMode != WrapMode.NoWrap)
			{
				// Calculate how many wrapped lines this logical line occupies
				int lineLength = _lines[_cursorY].Length;
				int wrappedLineCount = (lineLength > 0) ? ((lineLength - 1) / _effectiveWidth) + 1 : 1;

				// Calculate which wrapped line within the current logical line contains the cursor
				int cursorWrappedLine = _cursorX / _effectiveWidth;

				// Calculate total wrapped lines before the current line
				int totalWrappedLinesBefore = 0;
				for (int i = 0; i < _cursorY; i++)
				{
					int len = _lines[i].Length;
					totalWrappedLinesBefore += (len > 0) ? ((len - 1) / _effectiveWidth) + 1 : 1;
				}

				// The absolute wrapped line position of the cursor
				int absoluteWrappedCursorLine = totalWrappedLinesBefore + cursorWrappedLine;

				// Adjust vertical scroll offset to ensure the cursor's wrapped line is visible
				if (absoluteWrappedCursorLine < _verticalScrollOffset)
				{
					_verticalScrollOffset = absoluteWrappedCursorLine;
				}
				else if (absoluteWrappedCursorLine >= _verticalScrollOffset + _viewportHeight)
				{
					_verticalScrollOffset = absoluteWrappedCursorLine - _viewportHeight + 1;
				}

				// In wrap mode, we don't need horizontal scrolling as lines are wrapped
				_horizontalScrollOffset = 0;
			}
			else
			{
				// Standard vertical scrolling for non-wrapped text
				if (_cursorY < _verticalScrollOffset)
				{
					_verticalScrollOffset = _cursorY;
				}
				else if (_cursorY >= _verticalScrollOffset + _viewportHeight)
				{
					_verticalScrollOffset = _cursorY - _viewportHeight + 1;
				}

				// Standard horizontal scrolling for non-wrapped text
				if (_cursorX < _horizontalScrollOffset)
				{
					_horizontalScrollOffset = _cursorX;
				}
				else if (_cursorX >= _horizontalScrollOffset + _effectiveWidth)
				{
					_horizontalScrollOffset = _cursorX - _effectiveWidth + 1;
				}
			}

			_invalidated = true;
			_cachedContent = null;
		}

		// Basic content management methods
		public string GetContent()
		{
			return string.Join(Environment.NewLine, _lines);
		}

		public (int Left, int Top)? GetCursorPosition()
		{
			if (_cachedContent == null) return null;

			if (!_isEditing)
			{
				return null;
			}

			int paddingLeft = 0;

			// Calculate centering if needed
			if (Alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(_cachedContent?.FirstOrDefault()?.Length ?? 0, _effectiveWidth);
			}

			// For wrapped modes, we need special handling
			if (_wrapMode != WrapMode.NoWrap)
			{
				// Calculate which wrapped line within the current logical line contains the cursor
				int cursorWrappedLine = _cursorX / _effectiveWidth;

				// Calculate total wrapped lines before the current logical line
				int totalWrappedLinesBefore = 0;
				for (int i = 0; i < _cursorY; i++)
				{
					int len = _lines[i].Length;
					totalWrappedLinesBefore += (len > 0) ? ((len - 1) / _effectiveWidth) + 1 : 1;
				}

				// The absolute wrapped line position of the cursor
				int absoluteWrappedCursorLine = totalWrappedLinesBefore + cursorWrappedLine;

				// The cursor position within the wrapped line
				int cursorPositionInWrappedLine = _cursorX % _effectiveWidth;

				// Calculate visible cursor position
				int visibleWrappedLine = absoluteWrappedCursorLine - _verticalScrollOffset;

				// Only return cursor position if it's in the visible area
				if (visibleWrappedLine >= 0 && visibleWrappedLine < _viewportHeight)
				{
					return (cursorPositionInWrappedLine + _margin.Left + paddingLeft, visibleWrappedLine + _margin.Top);
				}
			}
			else
			{
				// Standard handling for non-wrapped text
				int visibleCursorX = _cursorX - _horizontalScrollOffset;
				int visibleCursorY = _cursorY - _verticalScrollOffset;

				// Only return cursor position if it's in the visible area
				if (visibleCursorY >= 0 && visibleCursorY < _viewportHeight)
				{
					return (visibleCursorX + _margin.Left + paddingLeft, visibleCursorY + _margin.Top);
				}
			}

			return null;
		}

		// Add methods to get selected text
		public string GetSelectedText()
		{
			if (!_hasSelection) return string.Empty;

			// Ensure start is before end
			(int startX, int startY, int endX, int endY) = GetOrderedSelectionBounds();

			if (startY == endY)
			{
				// Selection on same line
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
		/// Insert text at the current cursor position
		/// </summary>
		public void InsertText(string text)
		{
			if (string.IsNullOrEmpty(text))
				return;

			// Split the text into lines
			var textLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

			if (textLines.Length == 1)
			{
				// Single line insertion
				_lines[_cursorY] = _lines[_cursorY].Insert(_cursorX, textLines[0]);
				_cursorX += textLines[0].Length;
			}
			else
			{
				// Multi-line insertion
				var currentLine = _lines[_cursorY];

				// First line: text before cursor + first new line
				var beforeCursor = currentLine.Substring(0, _cursorX);
				var afterCursor = currentLine.Substring(_cursorX);

				// Update the current line with text before cursor + first new line
				_lines[_cursorY] = beforeCursor + textLines[0];

				// Insert the middle lines
				for (int i = 1; i < textLines.Length - 1; i++)
				{
					_lines.Insert(_cursorY + i, textLines[i]);
				}

				// Insert the last new line + text after cursor
				_lines.Insert(_cursorY + textLines.Length - 1, textLines[textLines.Length - 1] + afterCursor);

				// Update cursor position
				_cursorY += textLines.Length - 1;
				_cursorX = textLines[textLines.Length - 1].Length;
			}

			_invalidated = true;
			_cachedContent = null;
			EnsureCursorVisible();
			Container?.Invalidate(true);

			// Notify that content has changed
			ContentChanged?.Invoke(this, GetContent());
		}

		public void Invalidate()
		{
			_invalidated = true;
			_cachedContent = null;
		}

		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled) return false;

			if (!_isEditing)
			{
				if (key.Key == ConsoleKey.Enter)
				{
					_isEditing = true;
					Invalidate();
					Container?.Invalidate(false);
					return true;
				}
				return false;
			}

			bool contentChanged = false;
			bool isShiftPressed = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
			int oldCursorX = _cursorX;
			int oldCursorY = _cursorY;

			// If starting selection with Shift key
			if (isShiftPressed && !_hasSelection &&
				(key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow ||
				 key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow ||
				 key.Key == ConsoleKey.Home || key.Key == ConsoleKey.End))
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
					  key.Key == ConsoleKey.Home || key.Key == ConsoleKey.End))
			{
				ClearSelection();
			}

			switch (key.Key)
			{
				case ConsoleKey.LeftArrow:
					if (_cursorX > 0)
					{
						_cursorX--;
					}
					else if (_cursorY > 0)
					{
						// Move to end of previous line
						_cursorY--;
						_cursorX = _lines[_cursorY].Length;
					}
					break;

				case ConsoleKey.RightArrow:
					if (_cursorX < _lines[_cursorY].Length)
					{
						_cursorX++;
					}
					else if (_cursorY < _lines.Count - 1)
					{
						// Move to beginning of next line
						_cursorY++;
						_cursorX = 0;
					}
					break;

				case ConsoleKey.UpArrow:
					if (_wrapMode != WrapMode.NoWrap)
					{
						// Handle up arrow in wrapped mode
						// First, find the current wrapped line that contains the cursor
						int cursorWrappedLine = _cursorX / _effectiveWidth;

						// If we're already on the first wrapped line of the current logical line
						if (cursorWrappedLine == 0)
						{
							if (_cursorY > 0)
							{
								// Go to the previous logical line
								_cursorY--;

								// Calculate how many wrapped lines this line has
								int prevLineLength = _lines[_cursorY].Length;
								int prevLineWrappedCount = (prevLineLength > 0) ? ((prevLineLength - 1) / _effectiveWidth) + 1 : 1;

								// Position the cursor at the same horizontal position on the last wrapped line
								int horizontalOffset = _cursorX % _effectiveWidth;
								_cursorX = Math.Min(prevLineLength, (prevLineWrappedCount - 1) * _effectiveWidth + horizontalOffset);
							}
						}
						else
						{
							// Move up to the previous wrapped line within the same logical line
							int horizontalOffset = _cursorX % _effectiveWidth;
							_cursorX = (cursorWrappedLine - 1) * _effectiveWidth + horizontalOffset;
						}
					}
					else
					{
						// Standard behavior for no-wrap mode
						if (_cursorY > 0)
						{
							_cursorY--;
							// Ensure cursor X is within bounds of the new line
							_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
						}
					}
					break;

				case ConsoleKey.DownArrow:
					if (_wrapMode != WrapMode.NoWrap)
					{
						// Handle down arrow in wrapped mode
						int cursorWrappedLine = _cursorX / _effectiveWidth;
						int lineLength = _lines[_cursorY].Length;
						int lineWrappedCount = (lineLength > 0) ? ((lineLength - 1) / _effectiveWidth) + 1 : 1;

						// If we're on the last wrapped line of the current logical line
						if (cursorWrappedLine >= lineWrappedCount - 1)
						{
							if (_cursorY < _lines.Count - 1)
							{
								// Go to the next logical line
								_cursorY++;

								// Position cursor at the same horizontal position on the first wrapped line
								int horizontalOffset = _cursorX % _effectiveWidth;
								_cursorX = Math.Min(_lines[_cursorY].Length, horizontalOffset);
							}
						}
						else
						{
							// Move down to the next wrapped line within the same logical line
							int horizontalOffset = _cursorX % _effectiveWidth;
							int newPos = (cursorWrappedLine + 1) * _effectiveWidth + horizontalOffset;

							// Make sure we don't go beyond the end of the line
							_cursorX = Math.Min(lineLength, newPos);
						}
					}
					else
					{
						// Standard behavior for no-wrap mode
						if (_cursorY < _lines.Count - 1)
						{
							_cursorY++;
							// Ensure cursor X is within bounds of the new line
							_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
						}
					}
					break;

				case ConsoleKey.Home:
					if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
					{
						// Go to start of document
						_cursorX = 0;
						_cursorY = 0;
					}
					else
					{
						if (_wrapMode != WrapMode.NoWrap)
						{
							// Go to start of wrapped line
							_cursorX = (_cursorX / _effectiveWidth) * _effectiveWidth;
						}
						else
						{
							// Go to start of line
							_cursorX = 0;
						}
					}
					break;

				case ConsoleKey.End:
					if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
					{
						// Go to end of document
						_cursorY = _lines.Count - 1;
						_cursorX = _lines[_cursorY].Length;
					}
					else
					{
						if (_wrapMode != WrapMode.NoWrap)
						{
							// Go to end of wrapped line
							int lineLength = _lines[_cursorY].Length;
							int cursorWrappedLine = _cursorX / _effectiveWidth;
							int wrappedLineCount = (lineLength > 0) ? ((lineLength - 1) / _effectiveWidth) + 1 : 1;
							_cursorX = Math.Min(lineLength, (cursorWrappedLine + 1) * _effectiveWidth - 1);
						}
						else
						{
							// Go to end of line
							_cursorX = _lines[_cursorY].Length;
						}
					}
					break;

				case ConsoleKey.PageUp:
					if (_wrapMode != WrapMode.NoWrap)
					{
						// Handle page up in wrapped mode
						int totalWrappedLinesBefore = 0;
						for (int i = 0; i < _cursorY; i++)
						{
							int len = _lines[i].Length;
							totalWrappedLinesBefore += (len > 0) ? ((len - 1) / _effectiveWidth) + 1 : 1;
						}

						int cursorWrappedLine = _cursorX / _effectiveWidth;
						int absoluteWrappedCursorLine = totalWrappedLinesBefore + cursorWrappedLine;
						int newWrappedLine = Math.Max(0, absoluteWrappedCursorLine - _viewportHeight);

						// Find the new cursor position
						int newCursorY = 0;
						int newCursorX = 0;
						int wrappedLinesCount = 0;
						for (int i = 0; i < _lines.Count; i++)
						{
							int len = _lines[i].Length;
							int wrappedCount = (len > 0) ? ((len - 1) / _effectiveWidth) + 1 : 1;
							if (wrappedLinesCount + wrappedCount > newWrappedLine)
							{
								newCursorY = i;
								newCursorX = (newWrappedLine - wrappedLinesCount) * _effectiveWidth;
								break;
							}
							wrappedLinesCount += wrappedCount;
						}

						_cursorY = newCursorY;
						_cursorX = Math.Min(_lines[_cursorY].Length, newCursorX);
					}
					else
					{
						// Standard behavior for no-wrap mode
						_cursorY = Math.Max(0, _cursorY - _viewportHeight);
						// Ensure cursor X is within bounds of the new line
						_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
					}
					break;

				case ConsoleKey.PageDown:
					if (_wrapMode != WrapMode.NoWrap)
					{
						// Handle page down in wrapped mode
						int totalWrappedLinesBefore = 0;
						for (int i = 0; i < _cursorY; i++)
						{
							int len = _lines[i].Length;
							totalWrappedLinesBefore += (len > 0) ? ((len - 1) / _effectiveWidth) + 1 : 1;
						}

						int cursorWrappedLine = _cursorX / _effectiveWidth;
						int absoluteWrappedCursorLine = totalWrappedLinesBefore + cursorWrappedLine;
						int newWrappedLine = Math.Min(absoluteWrappedCursorLine + _viewportHeight, _lines.Sum(line => (line.Length > 0) ? ((line.Length - 1) / _effectiveWidth) + 1 : 1) - 1);

						// Find the new cursor position
						int newCursorY = 0;
						int newCursorX = 0;
						int wrappedLinesCount = 0;
						for (int i = 0; i < _lines.Count; i++)
						{
							int len = _lines[i].Length;
							int wrappedCount = (len > 0) ? ((len - 1) / _effectiveWidth) + 1 : 1;
							if (wrappedLinesCount + wrappedCount > newWrappedLine)
							{
								newCursorY = i;
								newCursorX = (newWrappedLine - wrappedLinesCount) * _effectiveWidth;
								break;
							}
							wrappedLinesCount += wrappedCount;
						}

						_cursorY = newCursorY;
						_cursorX = Math.Min(_lines[_cursorY].Length, newCursorX);
					}
					else
					{
						// Standard behavior for no-wrap mode
						_cursorY = Math.Min(_lines.Count - 1, _cursorY + _viewportHeight);
						// Ensure cursor X is within bounds of the new line
						_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
					}
					break;

				case ConsoleKey.Backspace:
					if (_hasSelection)
					{
						// Delete selected text
						DeleteSelectedText();
						contentChanged = true;
					}
					else if (_cursorX > 0)
					{
						// Remove character before cursor
						_lines[_cursorY] = _lines[_cursorY].Remove(_cursorX - 1, 1);
						_cursorX--;
						contentChanged = true;
					}
					else if (_cursorY > 0)
					{
						// Join with previous line
						int previousLineLength = _lines[_cursorY - 1].Length;
						_lines[_cursorY - 1] = _lines[_cursorY - 1] + _lines[_cursorY];
						_lines.RemoveAt(_cursorY);
						_cursorY--;
						_cursorX = previousLineLength;
						contentChanged = true;
					}
					break;

				case ConsoleKey.Delete:
					if (_hasSelection)
					{
						// Delete selected text
						DeleteSelectedText();
						contentChanged = true;
					}
					else if (_cursorX < _lines[_cursorY].Length)
					{
						// Remove character at cursor
						_lines[_cursorY] = _lines[_cursorY].Remove(_cursorX, 1);
						contentChanged = true;
					}
					else if (_cursorY < _lines.Count - 1)
					{
						// Join with next line
						_lines[_cursorY] = _lines[_cursorY] + _lines[_cursorY + 1];
						_lines.RemoveAt(_cursorY + 1);
						contentChanged = true;
					}
					break;

				case ConsoleKey.Enter:
					if (_hasSelection)
					{
						// Delete selected text first
						DeleteSelectedText();
						contentChanged = true;
					}

					// Insert line break
					string currentLine = _lines[_cursorY];
					string lineBeforeCursor = currentLine.Substring(0, _cursorX);
					string lineAfterCursor = currentLine.Substring(_cursorX);

					_lines[_cursorY] = lineBeforeCursor;
					_lines.Insert(_cursorY + 1, lineAfterCursor);

					_cursorY++;
					_cursorX = 0;
					contentChanged = true;
					break;

				case ConsoleKey.Escape:
					if (_hasSelection)
					{
						// Clear selection but keep focus
						ClearSelection();
						_cachedContent = null;
						Container?.Invalidate(true);
						return true;
					}
					else
					{
						Invalidate();
						Container?.Invalidate(false);
						_isEditing = false;
						Invalidate();
						return true;
					}

				default:
					if (!char.IsControl(key.KeyChar))
					{
						if (_hasSelection)
						{
							// Replace selected text with typed character
							DeleteSelectedText();
							contentChanged = true;
						}

						// Insert character at cursor
						_lines[_cursorY] = _lines[_cursorY].Insert(_cursorX, key.KeyChar.ToString());
						_cursorX++;

						// If we're in wrap mode and typing at the edge of the viewport, ensure proper scrolling
						if (_wrapMode != WrapMode.NoWrap)
						{
							if (_cursorX > 0 && _cursorX % _effectiveWidth == 0)
							{
								// We're exactly at a wrap point
								// Adjust horizontal scroll to show beginning of the new wrapped line
								if (_horizontalScrollOffset < _cursorX && _cursorX - _horizontalScrollOffset >= _effectiveWidth)
								{
									_horizontalScrollOffset = _cursorX;
								}
							}
						}

						contentChanged = true;
					}
					break;
			}

			// Update selection end if we're in selection mode
			if (isShiftPressed && _hasSelection)
			{
				_selectionEndX = _cursorX;
				_selectionEndY = _cursorY;
				_cachedContent = null;
				Container?.Invalidate(true);
			}

			// If cursor position changed, ensure it's visible
			if (_cursorX != oldCursorX || _cursorY != oldCursorY)
			{
				EnsureCursorVisible();
				_cachedContent = null;
				Container?.Invalidate(true);
			}

			// If content changed, notify listeners and invalidate
			if (contentChanged)
			{
				_invalidated = true;
				_cachedContent = null;
				Container?.Invalidate(true);
				ContentChanged?.Invoke(this, GetContent());
			}

			return true;
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (!_invalidated && _cachedContent != null) return _cachedContent;

			Color bgColor = _hasFocus ? _isEditing ? FocusedBackgroundColor : Container?.GetConsoleWindowSystem?.Theme?.TextEditFocusedNotEditing ?? Color.LightSlateGrey : BackgroundColor;
			Color fgColor = _hasFocus ? FocusedForegroundColor : ForegroundColor;
			Color selBgColor = SelectionBackgroundColor;
			Color selFgColor = SelectionForegroundColor;

			_cachedContent = new List<string>();

			// Adjust the effective width to account for left and right margins
			int effectiveWidth = (_width ?? availableWidth ?? 80) - _margin.Left - _margin.Right;
			int paddingLeft = 0;

			_effectiveWidth = effectiveWidth;

			// Calculate centering if needed
			if (Alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, effectiveWidth);
			}

			// First, wrap all lines according to mode and calculate total wrapped lines
			List<string> allWrappedLines = new List<string>();
			List<int> sourceLineIndex = new List<int>(); // Tracks which original line each wrapped line comes from
			List<int> sourceLineOffset = new List<int>(); // Tracks the character offset within source line

			// Process each line and track which original line each wrapped line belongs to
			for (int i = 0; i < _lines.Count; i++)
			{
				string line = _lines[i];
				if (_wrapMode == WrapMode.NoWrap)
				{
					allWrappedLines.Add(line);
					sourceLineIndex.Add(i);
					sourceLineOffset.Add(0);
				}
				else if (_wrapMode == WrapMode.Wrap)
				{
					// Character-based wrapping
					if (line.Length == 0)
					{
						// Handle empty line
						allWrappedLines.Add(string.Empty);
						sourceLineIndex.Add(i);
						sourceLineOffset.Add(0);
					}
					else
					{
						for (int j = 0; j < line.Length; j += effectiveWidth)
						{
							allWrappedLines.Add(line.Substring(j, Math.Min(effectiveWidth, line.Length - j)));
							sourceLineIndex.Add(i);
							sourceLineOffset.Add(j);
						}
					}
				}
				else if (_wrapMode == WrapMode.WrapWords)
				{
					// Word-based wrapping
					var words = line.Split(' ');
					var currentLine = new StringBuilder();
					int currentOffset = 0;

					if (words.Length == 0)
					{
						// Handle empty line
						allWrappedLines.Add(string.Empty);
						sourceLineIndex.Add(i);
						sourceLineOffset.Add(0);
					}
					else
					{
						foreach (var word in words)
						{
							if (currentLine.Length + word.Length + (currentLine.Length > 0 ? 1 : 0) > effectiveWidth)
							{
								allWrappedLines.Add(currentLine.ToString());
								sourceLineIndex.Add(i);
								sourceLineOffset.Add(currentOffset);
								currentOffset += currentLine.Length + 1; // +1 for the space
								currentLine.Clear();
							}

							if (currentLine.Length > 0)
							{
								currentLine.Append(' ');
							}

							currentLine.Append(word);
						}

						if (currentLine.Length > 0)
						{
							allWrappedLines.Add(currentLine.ToString());
							sourceLineIndex.Add(i);
							sourceLineOffset.Add(currentOffset);
						}
					}
				}
			}

			// Find the wrapped line that contains our cursor
			int wrappedLineWithCursor = -1;
			if (_wrapMode != WrapMode.NoWrap)
			{
				for (int i = 0; i < sourceLineIndex.Count; i++)
				{
					if (sourceLineIndex[i] == _cursorY)
					{
						int startOffset = sourceLineOffset[i];
						int endOffset = (i + 1 < sourceLineIndex.Count && sourceLineIndex[i + 1] == _cursorY)
							? sourceLineOffset[i + 1]
							: _lines[_cursorY].Length;

						if (_cursorX >= startOffset && _cursorX < endOffset)
						{
							wrappedLineWithCursor = i;
							break;
						}

						// If this is the last segment of the line and cursor is exactly at the end
						if (_cursorX == endOffset && (i + 1 >= sourceLineIndex.Count || sourceLineIndex[i + 1] != _cursorY))
						{
							wrappedLineWithCursor = i;
							break;
						}
					}
				}
			}
			else
			{
				// In no-wrap mode, the wrapped line index is the same as the line index
				wrappedLineWithCursor = _cursorY;
			}

			// Adjust vertical scroll to make the cursor visible
			if (wrappedLineWithCursor >= 0)
			{
				if (wrappedLineWithCursor < _verticalScrollOffset)
				{
					_verticalScrollOffset = wrappedLineWithCursor;
				}
				else if (wrappedLineWithCursor >= _verticalScrollOffset + _viewportHeight)
				{
					_verticalScrollOffset = wrappedLineWithCursor - _viewportHeight + 1;
				}
			}

			// Get visible wrapped lines based on vertical scroll
			List<string> visibleLines = allWrappedLines
				.Skip(_verticalScrollOffset)
				.Take(_viewportHeight)
				.ToList();

			// Get selection bounds
			var (startX, startY, endX, endY) = GetOrderedSelectionBounds();

			// Render visible lines
			for (int i = 0; i < visibleLines.Count; i++)
			{
				int actualWrappedLineIndex = i + _verticalScrollOffset;
				int actualSourceLineIndex = sourceLineIndex[actualWrappedLineIndex];
				int actualSourceOffset = sourceLineOffset[actualWrappedLineIndex];
				string line = visibleLines[i];
				string visibleLine = line;

				// Apply horizontal scrolling
				if (_horizontalScrollOffset > 0 && _horizontalScrollOffset < line.Length)
				{
					visibleLine = line.Substring(_horizontalScrollOffset);
				}
				else if (_horizontalScrollOffset >= line.Length)
				{
					visibleLine = string.Empty;
				}

				// Pad to effective width
				if (visibleLine.Length < effectiveWidth)
				{
					visibleLine = visibleLine.PadRight(effectiveWidth);
				}
				else if (visibleLine.Length > effectiveWidth)
				{
					visibleLine = visibleLine.Substring(0, effectiveWidth);
				}

				string renderedLine;

				if (_hasSelection)
				{
					// Check if this wrapped line has any selection
					bool hasSelection = false;
					StringBuilder sb = new StringBuilder();

					// Check if this source line is within the selection range
					if (actualSourceLineIndex >= startY && actualSourceLineIndex <= endY)
					{
						for (int charPos = 0; charPos < visibleLine.Length; charPos++)
						{
							int actualCharPos = charPos + actualSourceOffset + _horizontalScrollOffset;
							bool isSelected = false;

							if (actualSourceLineIndex == startY && actualSourceLineIndex == endY)
							{
								// Selection within a single line
								isSelected = actualCharPos >= startX && actualCharPos < endX;
							}
							else if (actualSourceLineIndex == startY)
							{
								// First line of selection
								isSelected = actualCharPos >= startX;
							}
							else if (actualSourceLineIndex == endY)
							{
								// Last line of selection
								isSelected = actualCharPos < endX;
							}
							else if (actualSourceLineIndex > startY && actualSourceLineIndex < endY)
							{
								// Middle line of selection
								isSelected = true;
							}

							char c = visibleLine[charPos];
							if (isSelected)
							{
								hasSelection = true;
								sb.Append(AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
									c.ToString(),
									1,
									1,
									false,
									selBgColor,
									selFgColor
								)[0]);
							}
							else
							{
								sb.Append(AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
									c.ToString(),
									1,
									1,
									false,
									bgColor,
									fgColor
								)[0]);
							}
						}

						renderedLine = hasSelection ? sb.ToString() : AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
							visibleLine,
							effectiveWidth,
							1,
							false,
							bgColor,
							fgColor
						)[0];
					}
					else
					{
						// No selection on this line
						renderedLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
							visibleLine,
							effectiveWidth,
							1,
							false,
							bgColor,
							fgColor
						)[0];
					}
				}
				else
				{
					// No selection at all
					renderedLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						visibleLine,
						effectiveWidth,
						1,
						false,
						bgColor,
						fgColor
					)[0];
				}

				// Add left padding if needed
				if (paddingLeft > 0)
				{
					string paddingSpace = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', paddingLeft),
						paddingLeft,
						1,
						false,
						Container?.BackgroundColor,
						null
					)[0];

					_cachedContent.Add(paddingSpace + renderedLine);
				}
				else
				{
					_cachedContent.Add(renderedLine);
				}
			}

			// Fill remaining viewport with empty lines
			while (_cachedContent.Count < _viewportHeight)
			{
				string emptyLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', effectiveWidth),
					effectiveWidth,
					1,
					false,
					bgColor,
					fgColor
				)[0];

				if (paddingLeft > 0)
				{
					string paddingSpace = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', paddingLeft),
						paddingLeft,
						1,
						false,
						Container?.BackgroundColor,
						null
					)[0];

					_cachedContent.Add(paddingSpace + emptyLine);
				}
				else
				{
					_cachedContent.Add(emptyLine);
				}
			}

			// Add margin spacing
			if (_margin.Left > 0 || _margin.Right > 0 || _margin.Top > 0 || _margin.Bottom > 0)
			{
				List<string> withMargins = new List<string>();

				// Top margin
				string emptyLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', effectiveWidth + paddingLeft),
					effectiveWidth + paddingLeft,
					1,
					false,
					Container?.BackgroundColor,
					Container?.ForegroundColor
				)[0];

				for (int i = 0; i < _margin.Top; i++)
				{
					withMargins.Add(emptyLine);
				}

				// Add content with left and right margins
				foreach (var line in _cachedContent)
				{
					string leftMargin = string.Empty;
					if (_margin.Left > 0)
					{
						leftMargin = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
							new string(' ', _margin.Left),
							_margin.Left,
							1,
							false,
							Container?.BackgroundColor,
							null
						)[0];
					}

					string rightMargin = string.Empty;
					if (_margin.Right > 0)
					{
						rightMargin = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
							new string(' ', _margin.Right),
							_margin.Right,
							1,
							false,
							Container?.BackgroundColor,
							null
						)[0];
					}

					withMargins.Add(leftMargin + line + rightMargin);
				}

				// Bottom margin
				for (int i = 0; i < _margin.Bottom; i++)
				{
					withMargins.Add(emptyLine);
				}

				_cachedContent = withMargins;
			}

			_invalidated = false;
			return _cachedContent;
		}

		public void SetContent(string content)
		{
			if (content == null)
			{
				_lines = new List<string>() { string.Empty };
			}
			else
			{
				_lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
				if (_lines.Count == 0)
				{
					_lines.Add(string.Empty);
				}
			}

			ClearSelection();

			_cursorX = 0;
			_cursorY = 0;
			_horizontalScrollOffset = 0;
			_verticalScrollOffset = 0;
			_invalidated = true;
			_cachedContent = null;
			Container?.Invalidate(true);

			// Notify that content has changed
			ContentChanged?.Invoke(this, GetContent());
		}

		public void SetFocus(bool focus, bool backward)
		{
			_hasFocus = focus;
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		// Add this method to support deleting selected text
		private void DeleteSelectedText()
		{
			if (!_hasSelection) return;

			var (startX, startY, endX, endY) = GetOrderedSelectionBounds();

			if (startY == endY)
			{
				// Selection on the same line
				_lines[startY] = _lines[startY].Remove(startX, endX - startX);
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
				_lines.RemoveRange(startY + 1, endY - startY);
			}

			// Move cursor to the selection start
			_cursorX = startX;
			_cursorY = startY;

			// Clear the selection
			ClearSelection();
		}

		// Helper to get ordered selection bounds
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
	}
}
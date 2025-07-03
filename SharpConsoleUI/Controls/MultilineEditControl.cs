// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Drawing;
using System.Text;
using Color = Spectre.Console.Color;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI.Controls
{
	public enum ScrollbarVisibility
	{
		Auto,    // Show scrollbars only when needed
		Always,  // Always show scrollbars
		Never    // Never show scrollbars
	}

	public enum WrapMode
	{
		NoWrap,
		Wrap,
		WrapWords
	}

	public class MultilineEditControl : IWIndowControl, IInteractiveControl, IFocusableControl, ILogicalCursorProvider
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
		private ScrollbarVisibility _horizontalScrollbarVisibility = ScrollbarVisibility.Auto;
		private int _horizontalScrollOffset = 0;
		private bool _invalidated = true;
		private bool _isDraggingScrollbar = false;
		private bool _isDraggingVerticalScrollbar = false;
		private bool _isEditing = false;
		private bool _isEnabled = true;
		private List<string> _lines = new List<string>() { string.Empty };
		private Margin _margin = new Margin(0, 0, 0, 0);
		private bool _readOnly = false;
		private Color? _scrollbarColorValue;
		private int _scrollbarDragStartPosition = 0;
		private Color? _scrollbarThumbColorValue;
		private Color? _selectionBackgroundColorValue;
		private int _selectionEndX = 0;
		private int _selectionEndY = 0;
		private Color? _selectionForegroundColorValue;
		private int _selectionStartX = 0;
		private int _selectionStartY = 0;
		private bool _skipUpdateScrollPositionsInRender = false;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private ScrollbarVisibility _verticalScrollbarVisibility = ScrollbarVisibility.Auto;
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

		public string Content
		{
			get => GetContent();
			set => SetContent(value);
		}

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

		public ScrollbarVisibility HorizontalScrollbarVisibility
		{
			get => _horizontalScrollbarVisibility;
			set
			{
				_horizontalScrollbarVisibility = value;
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

		/// <summary>
		/// Gets or sets whether the control is in read-only mode.
		/// When read-only, users can navigate and select text, but cannot modify content.
		/// </summary>
		public bool ReadOnly
		{
			get => _readOnly;
			set
			{
				_readOnly = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color ScrollbarColor
		{
			get => _scrollbarColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.InactiveBorderForegroundColor ?? Color.Grey;
			set
			{
				_scrollbarColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color ScrollbarThumbColor
		{
			get => _scrollbarThumbColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonBackgroundColor ?? Color.White;
			set
			{
				_scrollbarThumbColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

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

		public ScrollbarVisibility VerticalScrollbarVisibility
		{
			get => _verticalScrollbarVisibility;
			set
			{
				_verticalScrollbarVisibility = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

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

		/// <summary>
		/// Appends content to the end of the control and scrolls to make it visible.
		/// </summary>
		/// <param name="content">The content to append.</param>
		public void AppendContent(string content)
		{
			if (string.IsNullOrEmpty(content))
				return;

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

			// Force recalculation of scrollbars by invalidating
			_invalidated = true;
			_cachedContent = null;

			// Reset flag to ensure scroll positions are updated properly
			_skipUpdateScrollPositionsInRender = false;

			_invalidated = true;
			_cachedContent = null;
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

			// Force recalculation of scrollbars by invalidating
			_invalidated = true;
			_cachedContent = null;

			// Reset flag to ensure scroll positions are updated properly
			_skipUpdateScrollPositionsInRender = false;

			_invalidated = true;
			_cachedContent = null;
			Container?.Invalidate(true);

			// Go to the end of the content
			GoToEnd();

			// Notify that content has changed
			ContentChanged?.Invoke(this, GetContent());
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
			if (Container == null) return;

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
			_invalidated = true;
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Insert text at the current cursor position
		/// </summary>
		public void InsertText(string text)
		{
			if (_readOnly || string.IsNullOrEmpty(text))
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

			// When focused but not editing, allow scrolling with arrow keys
			if (_hasFocus && !_isEditing)
			{
				if (key.Modifiers.HasFlag(ConsoleModifiers.Control) || key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Control))
				{
					return false;
				}

				switch (key.Key)
				{
					case ConsoleKey.Enter:
						_isEditing = true;
						Invalidate();
						Container?.Invalidate(false);
						return true;

					case ConsoleKey.LeftArrow:
						// Scroll content left if not at leftmost position
						if (_wrapMode == WrapMode.NoWrap && _horizontalScrollOffset > 0)
						{
							_skipUpdateScrollPositionsInRender = true;
							_horizontalScrollOffset--;
							_invalidated = true;
							_cachedContent = null;
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
								_invalidated = true;
								_cachedContent = null;
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
							_invalidated = true;
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.DownArrow:
						// Scroll content down if not at bottom
						int totalLines = GetTotalWrappedLineCount();
						if (_verticalScrollOffset < totalLines - _viewportHeight)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset++;
							_invalidated = true;
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageUp:
						// Page up scrolling - move view up by viewport height
						int pageUpAmount = Math.Min(_viewportHeight, _verticalScrollOffset);
						if (pageUpAmount > 0)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset -= pageUpAmount;
							_invalidated = true;
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageDown:
						// Page down scrolling - move view down by viewport height
						int totalWrappedLines = GetTotalWrappedLineCount();
						int pageDownAmount = Math.Min(_viewportHeight, totalWrappedLines - _verticalScrollOffset - _viewportHeight);
						if (pageDownAmount > 0)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset += pageDownAmount;
							_invalidated = true;
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.Home:
						// Scroll to top of document
						if (_verticalScrollOffset > 0)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset = 0;
							_horizontalScrollOffset = 0;
							_invalidated = true;
							_cachedContent = null;
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.End:
						// Scroll to bottom of document
						int endOffset = Math.Max(0, GetTotalWrappedLineCount() - _viewportHeight);
						if (_verticalScrollOffset != endOffset)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset = endOffset;
							_invalidated = true;
							_cachedContent = null;
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
					if (_readOnly) break;

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
					if (_readOnly) break;

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
					if (_readOnly) break;

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
					if (!_readOnly && !char.IsControl(key.KeyChar))
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

			_effectiveWidth = (_width ?? availableWidth ?? 80) - _margin.Left - _margin.Right;

			// Determine if scrollbars will be shown
			bool needsVerticalScrollbar = _verticalScrollbarVisibility == ScrollbarVisibility.Always ||
										(_verticalScrollbarVisibility == ScrollbarVisibility.Auto &&
										 GetTotalWrappedLineCount() > _viewportHeight);

			bool needsHorizontalScrollbar = _wrapMode == WrapMode.NoWrap &&
										  (_horizontalScrollbarVisibility == ScrollbarVisibility.Always ||
										  (_horizontalScrollbarVisibility == ScrollbarVisibility.Auto &&
										   GetMaxLineLength() > (_width ?? availableWidth ?? 80) - _margin.Left - _margin.Right));

			// Reserve space for scrollbars in effective width/height calculations
			int scrollbarWidth = needsVerticalScrollbar ? 1 : 0;
			int scrollbarHeight = needsHorizontalScrollbar ? 1 : 0;

			// Adjust the effective width to account for left and right margins AND scrollbar
			int effectiveWidth = (_width ?? availableWidth ?? 80) - _margin.Left - _margin.Right - scrollbarWidth;
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
				if (!_skipUpdateScrollPositionsInRender)
				{
					if (wrappedLineWithCursor < _verticalScrollOffset)
					{
						_verticalScrollOffset = wrappedLineWithCursor;
					}
					else if (wrappedLineWithCursor >= _verticalScrollOffset + _viewportHeight)
					{
						_verticalScrollOffset = wrappedLineWithCursor - _viewportHeight + 1;
					}

					_skipUpdateScrollPositionsInRender = false;
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

			// When adding scrollbars, use the reserved space we calculated earlier
			if (needsVerticalScrollbar || needsHorizontalScrollbar)
			{
				List<string> withScrollbars = new List<string>(_cachedContent);

				if (needsVerticalScrollbar)
				{
					// Calculate scrollbar metrics
					int totalLines = GetTotalWrappedLineCount();
					var scrollbar = RenderVerticalScrollbar(_viewportHeight, totalLines);

					// Apply vertical scrollbar to the right side of content
					for (int i = 0; i < Math.Min(withScrollbars.Count, scrollbar.Count); i++)
					{
						withScrollbars[i] = withScrollbars[i] + scrollbar[i];
					}
				}

				if (needsHorizontalScrollbar)
				{
					// Calculate scrollbar metrics
					int maxLineLength = GetMaxLineLength();
					string scrollbar = RenderHorizontalScrollbar(effectiveWidth, maxLineLength);

					// Add horizontal scrollbar at the bottom
					if (!string.IsNullOrEmpty(scrollbar))
					{
						// If we have a vertical scrollbar, add a corner character
						if (needsVerticalScrollbar)
						{
							scrollbar += AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
								"┘",
								1,
								1,
								false,
								BackgroundColor,
								ScrollbarColor
							)[0];
						}

						// Add proper padding if needed
						if (paddingLeft > 0)
						{
							string paddingStr = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
								new string(' ', paddingLeft),
								paddingLeft,
								1,
								false,
								Container?.BackgroundColor,
								null
							)[0];

							scrollbar = paddingStr + scrollbar;
						}

						withScrollbars.Add(scrollbar);
					}
				}

				_cachedContent = withScrollbars;
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

			EnsureCursorVisible();

			_invalidated = true;
			_cachedContent = null;
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
			if (lines == null || lines.Count == 0)
			{
				_lines = new List<string>() { string.Empty };
			}
			else
			{
				_lines = new List<string>(lines);
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

			EnsureCursorVisible();

			_invalidated = true;
			_cachedContent = null;
			Container?.Invalidate(false);

			_skipUpdateScrollPositionsInRender = false;

			// Notify that content has changed
			ContentChanged?.Invoke(this, GetContent());
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

		// Calculate maximum line length
		private int GetMaxLineLength()
		{
			int maxLength = 0;
			foreach (var line in _lines)
			{
				if (line.Length > maxLength)
					maxLength = line.Length;
			}
			return maxLength;
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

		// Calculate total number of wrapped lines
		private int GetTotalWrappedLineCount()
		{
			if (_wrapMode == WrapMode.NoWrap)
			{
				return _lines.Count;
			}

			int totalWrappedLines = 0;
			for (int i = 0; i < _lines.Count; i++)
			{
				int len = _lines[i].Length;
				totalWrappedLines += (len > 0) ? ((len - 1) / _effectiveWidth) + 1 : 1;
			}
			return totalWrappedLines;
		}

		// Render a horizontal scrollbar
		private string RenderHorizontalScrollbar(int width, int maxContentWidth)
		{
			StringBuilder sb = new StringBuilder();

			// Calculate scrollbar dimensions
			int thumbSize = Math.Max(1, width * width / Math.Max(1, maxContentWidth));
			int maxScrollOffset = Math.Max(0, maxContentWidth - width);
			int thumbPosition = (maxScrollOffset == 0) ? 0 :
								(int)((float)_horizontalScrollOffset / maxScrollOffset * (width - thumbSize));

			// Make sure thumb position is valid
			thumbPosition = Math.Max(0, Math.Min(width - thumbSize, thumbPosition));

			// Draw the scrollbar
			for (int i = 0; i < width; i++)
			{
				char c;
				Color color;

				if (i >= thumbPosition && i < thumbPosition + thumbSize)
				{
					c = '▬'; // Thumb character
					color = ScrollbarThumbColor;
				}
				else
				{
					c = '─'; // Track character
					color = ScrollbarColor;
				}

				sb.Append(AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					c.ToString(),
					1,
					1,
					false,
					BackgroundColor,
					color
				)[0]);
			}

			return sb.ToString();
		}

		// Render a vertical scrollbar
		private List<string> RenderVerticalScrollbar(int height, int maxContentHeight)
		{
			List<string> result = new List<string>();

			// Calculate scrollbar dimensions
			int thumbSize = Math.Max(1, height * height / Math.Max(1, maxContentHeight));
			int maxScrollOffset = Math.Max(0, maxContentHeight - height);
			int thumbPosition = (maxScrollOffset == 0) ? 0 :
								(int)((float)_verticalScrollOffset / maxScrollOffset * (height - thumbSize));

			// Make sure thumb position is valid
			thumbPosition = Math.Max(0, Math.Min(height - thumbSize, thumbPosition));

			// Draw the scrollbar
			for (int i = 0; i < height; i++)
			{
				char c;
				Color color;

				if (i >= thumbPosition && i < thumbPosition + thumbSize)
				{
					c = '█'; // Thumb character
					color = ScrollbarThumbColor;
				}
				else
				{
					c = '│'; // Track character
					color = ScrollbarColor;
				}

				result.Add(AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					c.ToString(),
					1,
					1,
					false,
					BackgroundColor,
					color
				)[0]);
			}

			return result;
		}

		// ILogicalCursorProvider implementation
		public Point? GetLogicalCursorPosition()
		{
			// Return the logical cursor position in content coordinates
			// This is the raw cursor position without any visual adjustments
			return new Point(_cursorX, _cursorY);
		}

		public System.Drawing.Size GetLogicalContentSize()
		{
			// Return the logical size of the content
			if (_wrapMode != WrapMode.NoWrap && _effectiveWidth > 0)
			{
				// For wrapped content, calculate total wrapped lines
				int totalWrappedLines = 0;
				foreach (var line in _lines)
				{
					int lineLength = line.Length;
					totalWrappedLines += lineLength > 0 ? ((lineLength - 1) / _effectiveWidth) + 1 : 1;
				}
				return new System.Drawing.Size(_effectiveWidth, totalWrappedLines);
			}
			else
			{
				// For non-wrapped content
				int maxWidth = _lines.Count > 0 ? _lines.Max(line => line.Length) : 0;
				return new System.Drawing.Size(maxWidth, _lines.Count);
			}
		}

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

		// IFocusableControl members
		public bool CanReceiveFocus => IsEnabled && !ReadOnly;

		public event EventHandler? GotFocus;
		public event EventHandler? LostFocus;

		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			var hadFocus = _hasFocus;
			_hasFocus = focus;
			_cachedContent = null;
			Container?.Invalidate(true);

			// Fire focus events
			if (focus && !hadFocus)
				GotFocus?.Invoke(this, EventArgs.Empty);
			else if (!focus && hadFocus)
				LostFocus?.Invoke(this, EventArgs.Empty);
		}
	}
}
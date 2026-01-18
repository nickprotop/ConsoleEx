// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using System.Drawing;
using System.Text;
using Color = Spectre.Console.Color;
using Size = SharpConsoleUI.Helpers.Size;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Specifies when scrollbars should be displayed.
	/// </summary>
	public enum ScrollbarVisibility
	{
		/// <summary>Show scrollbars only when content exceeds viewport.</summary>
		Auto,
		/// <summary>Always show scrollbars regardless of content size.</summary>
		Always,
		/// <summary>Never show scrollbars.</summary>
		Never
	}

	/// <summary>
	/// Specifies how text wrapping is handled in multiline controls.
	/// </summary>
	public enum WrapMode
	{
		/// <summary>No text wrapping; lines extend beyond viewport width.</summary>
		NoWrap,
		/// <summary>Wrap text at character boundaries.</summary>
		Wrap,
		/// <summary>Wrap text at word boundaries when possible.</summary>
		WrapWords
	}

	/// <summary>
	/// A multiline text editing control with support for text selection, scrolling, and word wrap.
	/// Provides full cursor navigation, cut/copy/paste-like operations, and configurable scrollbars.
	/// </summary>
	public class MultilineEditControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, ILogicalCursorProvider, ICursorShapeProvider, IDOMPaintable
	{
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;

		// Color properties
		private Color? _backgroundColorValue;

		private Color _borderColor = Color.White;
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
		private bool _isEditing = false;
		private bool _isEnabled = true;
		private List<string> _lines = new List<string>() { string.Empty };
		private Margin _margin = new Margin(0, 0, 0, 0);
		private bool _readOnly = false;
		private Color? _scrollbarColorValue;
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


		/// <summary>
		/// Initializes a new instance of the MultilineEditControl with the specified viewport height.
		/// </summary>
		/// <param name="viewportHeight">The number of visible lines in the viewport.</param>
		public MultilineEditControl(int viewportHeight = 10)
		{
			_viewportHeight = Math.Max(1, viewportHeight);
		}

		/// <summary>
		/// Initializes a new instance of the MultilineEditControl with initial content and viewport height.
		/// </summary>
		/// <param name="initialContent">The initial text content to display.</param>
		/// <param name="viewportHeight">The number of visible lines in the viewport.</param>
		public MultilineEditControl(string initialContent, int viewportHeight = 10)
		{
			_viewportHeight = Math.Max(1, viewportHeight);
			SetContent(initialContent);
		}

		/// <summary>
		/// Occurs when the text content changes.
		/// </summary>
		public event EventHandler<string>? ContentChanged;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;
		#pragma warning restore CS0067

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <summary>
		/// Gets the actual rendered width of the control content in characters.
		/// </summary>
		public int? ActualWidth
		{
			get
			{
				int maxLength = 0;
				foreach (var line in _lines)
				{
					if (line.Length > maxLength) maxLength = line.Length;
				}
				return maxLength + _margin.Left + _margin.Right;
			}
		}

		/// <summary>
		/// Gets or sets the text alignment within the control.
		/// </summary>
		public HorizontalAlignment HorizontalAlignment
		{ get => _horizontalAlignment; set { _horizontalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{ get => _verticalAlignment; set { _verticalAlignment = value; Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the background color when the control is not focused.
		/// </summary>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveBackground(_backgroundColorValue, Container);
			set
			{
				_backgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the border color of the control.
		/// </summary>
		public Color BorderColor
		{
			get => _borderColor;
			set
			{
				_borderColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <summary>
		/// Gets or sets the text content as a single string with line breaks.
		/// </summary>
		public string Content
		{
			get => GetContent();
			set => SetContent(value);
		}

		/// <summary>
		/// Gets or sets the background color when the control is focused.
		/// </summary>
		public Color FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedBackgroundColor ?? Color.White;
			set
			{
				_focusedBackgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color when the control is focused.
		/// </summary>
		public Color FocusedForegroundColor
		{
			get => _focusedForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedForegroundColor ?? Color.White;
			set
			{
				_focusedForegroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color when the control is not focused.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				_hasFocus = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets when the horizontal scrollbar is displayed.
		/// </summary>
		public ScrollbarVisibility HorizontalScrollbarVisibility
		{
			get => _horizontalScrollbarVisibility;
			set
			{
				_horizontalScrollbarVisibility = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether the control is currently in text editing mode.
		/// </summary>
		public bool IsEditing
		{ get => _isEditing; set { _isEditing = value; Invalidate(); Container?.Invalidate(false); } }

		/// <summary>
		/// Gets the preferred cursor shape based on editing state.
		/// Returns VerticalBar when editing (like modern text editors), null otherwise.
		/// </summary>
		public CursorShape? PreferredCursorShape => _isEditing ? CursorShape.VerticalBar : null;

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
		}

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
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the scrollbar track color.
		/// </summary>
		public Color ScrollbarColor
		{
			get => _scrollbarColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.InactiveBorderForegroundColor ?? Color.Grey;
			set
			{
				_scrollbarColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the scrollbar thumb (handle) color.
		/// </summary>
		public Color ScrollbarThumbColor
		{
			get => _scrollbarThumbColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonBackgroundColor ?? Color.White;
			set
			{
				_scrollbarThumbColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color for selected text.
		/// </summary>
		public Color SelectionBackgroundColor
		{
			get => _selectionBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Blue;
			set
			{
				_selectionBackgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color for selected text.
		/// </summary>
		public Color SelectionForegroundColor
		{
			get => _selectionForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.White;
			set
			{
				_selectionForegroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets when the vertical scrollbar is displayed.
		/// </summary>
		public ScrollbarVisibility VerticalScrollbarVisibility
		{
			get => _verticalScrollbarVisibility;
			set
			{
				_verticalScrollbarVisibility = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the number of visible lines in the viewport.
		/// </summary>
		public int ViewportHeight
		{
			get => _viewportHeight;
			set
			{
				var validatedValue = Math.Max(1, value);
				if (_viewportHeight != validatedValue)
				{
					_viewportHeight = validatedValue;
					Container?.Invalidate(true);
				}
			}
		}

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
	public int? Width
	{
		get => _width;
		set
		{
			var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
			if (_width != validatedValue)
			{
				_width = validatedValue;
				Container?.Invalidate(true);
			}
		}
	}

		/// <summary>
		/// Gets or sets the text wrapping mode.
		/// </summary>
		public WrapMode WrapMode
		{
			get => _wrapMode;
			set
			{
				_wrapMode = value;
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
		/// Clears the current text selection.
		/// </summary>
		public void ClearSelection()
		{
			_hasSelection = false;
			_selectionStartX = _selectionEndX = _cursorX;
			_selectionStartY = _selectionEndY = _cursorY;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Container = null;
		}

		/// <summary>
		/// Ensures the cursor position is visible within the viewport by adjusting scroll offsets.
		/// </summary>
		public void EnsureCursorVisible()
		{
			if (Container == null) return;

			// Guard against uninitialized _effectiveWidth (can be 0 before first render)
			int effectiveWidth = _effectiveWidth > 0 ? _effectiveWidth : 80;

			// Special handling for wrap mode
			if (_wrapMode != WrapMode.NoWrap)
			{
				// Calculate how many wrapped lines this logical line occupies
				int lineLength = _lines[_cursorY].Length;
				int wrappedLineCount = (lineLength > 0) ? ((lineLength - 1) / effectiveWidth) + 1 : 1;

				// Calculate which wrapped line within the current logical line contains the cursor
				int cursorWrappedLine = _cursorX / effectiveWidth;

				// Calculate total wrapped lines before the current line
				int totalWrappedLinesBefore = 0;
				for (int i = 0; i < _cursorY; i++)
				{
					int len = _lines[i].Length;
					totalWrappedLinesBefore += (len > 0) ? ((len - 1) / effectiveWidth) + 1 : 1;
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
				else if (_cursorX >= _horizontalScrollOffset + effectiveWidth)
				{
					_horizontalScrollOffset = _cursorX - effectiveWidth + 1;
				}
			}

		}

		/// <summary>
		/// Gets the text content as a single string with line breaks.
		/// </summary>
		/// <returns>The complete text content.</returns>
		public string GetContent()
		{
			return string.Join(Environment.NewLine, _lines);
		}

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

			EnsureCursorVisible();
			Container?.Invalidate(true);

			// Notify that content has changed
			ContentChanged?.Invoke(this, GetContent());
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

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

			// When focused but not editing, only handle specific navigation keys
			// All other keys (including Ctrl/Alt/Shift combinations) bubble up
			if (_hasFocus && !_isEditing)
			{
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
						if (_verticalScrollOffset < totalLines - _viewportHeight)
						{
							_skipUpdateScrollPositionsInRender = true;
							_verticalScrollOffset++;
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
			bool isCtrlPressed = key.Modifiers.HasFlag(ConsoleModifiers.Control);
			int oldCursorX = _cursorX;
			int oldCursorY = _cursorY;
			bool oldHasSelection = _hasSelection;

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
						int safeWidth = SafeEffectiveWidth;
						// First, find the current wrapped line that contains the cursor
						int cursorWrappedLine = _cursorX / safeWidth;

						// If we're already on the first wrapped line of the current logical line
						if (cursorWrappedLine == 0)
						{
							if (_cursorY > 0)
							{
								// Go to the previous logical line
								_cursorY--;

								// Calculate how many wrapped lines this line has
								int prevLineLength = _lines[_cursorY].Length;
								int prevLineWrappedCount = (prevLineLength > 0) ? ((prevLineLength - 1) / safeWidth) + 1 : 1;

								// Position the cursor at the same horizontal position on the last wrapped line
								int horizontalOffset = _cursorX % safeWidth;
								_cursorX = Math.Min(prevLineLength, (prevLineWrappedCount - 1) * safeWidth + horizontalOffset);
							}
						}
						else
						{
							// Move up to the previous wrapped line within the same logical line
							int horizontalOffset = _cursorX % safeWidth;
							_cursorX = (cursorWrappedLine - 1) * safeWidth + horizontalOffset;
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
						int safeWidth = SafeEffectiveWidth;
						int cursorWrappedLine = _cursorX / safeWidth;
						int lineLength = _lines[_cursorY].Length;
						int lineWrappedCount = (lineLength > 0) ? ((lineLength - 1) / safeWidth) + 1 : 1;

						// If we're on the last wrapped line of the current logical line
						if (cursorWrappedLine >= lineWrappedCount - 1)
						{
							if (_cursorY < _lines.Count - 1)
							{
								// Go to the next logical line
								_cursorY++;

								// Position cursor at the same horizontal position on the first wrapped line
								int horizontalOffset = _cursorX % safeWidth;
								_cursorX = Math.Min(_lines[_cursorY].Length, horizontalOffset);
							}
						}
						else
						{
							// Move down to the next wrapped line within the same logical line
							int horizontalOffset = _cursorX % safeWidth;
							int newPos = (cursorWrappedLine + 1) * safeWidth + horizontalOffset;

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
							int safeWidth = SafeEffectiveWidth;
							_cursorX = (_cursorX / safeWidth) * safeWidth;
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
							// Go to end of wrapped line segment (position after last char, not last char itself)
							int safeWidth = SafeEffectiveWidth;
							int lineLength = _lines[_cursorY].Length;
							int cursorWrappedLine = _cursorX / safeWidth;
							int wrappedLineEnd = (cursorWrappedLine + 1) * safeWidth;
							_cursorX = Math.Min(lineLength, wrappedLineEnd);
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
						int safeWidth = SafeEffectiveWidth;
						int totalWrappedLinesBefore = 0;
						for (int i = 0; i < _cursorY; i++)
						{
							int len = _lines[i].Length;
							totalWrappedLinesBefore += (len > 0) ? ((len - 1) / safeWidth) + 1 : 1;
						}

						int cursorWrappedLine = _cursorX / safeWidth;
						int absoluteWrappedCursorLine = totalWrappedLinesBefore + cursorWrappedLine;
						int newWrappedLine = Math.Max(0, absoluteWrappedCursorLine - _viewportHeight);

						// Find the new cursor position
						int newCursorY = 0;
						int newCursorX = 0;
						int wrappedLinesCount = 0;
						for (int i = 0; i < _lines.Count; i++)
						{
							int len = _lines[i].Length;
							int wrappedCount = (len > 0) ? ((len - 1) / safeWidth) + 1 : 1;
							if (wrappedLinesCount + wrappedCount > newWrappedLine)
							{
								newCursorY = i;
								newCursorX = (newWrappedLine - wrappedLinesCount) * safeWidth;
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
						int safeWidth = SafeEffectiveWidth;
						int totalWrappedLinesBefore = 0;
						for (int i = 0; i < _cursorY; i++)
						{
							int len = _lines[i].Length;
							totalWrappedLinesBefore += (len > 0) ? ((len - 1) / safeWidth) + 1 : 1;
						}

						int cursorWrappedLine = _cursorX / safeWidth;
						int absoluteWrappedCursorLine = totalWrappedLinesBefore + cursorWrappedLine;
						int newWrappedLine = Math.Min(absoluteWrappedCursorLine + _viewportHeight, GetTotalWrappedLineCount() - 1);

						// Find the new cursor position
						int newCursorY = 0;
						int newCursorX = 0;
						int wrappedLinesCount = 0;
						for (int i = 0; i < _lines.Count; i++)
						{
							int len = _lines[i].Length;
							int wrappedCount = (len > 0) ? ((len - 1) / safeWidth) + 1 : 1;
							if (wrappedLinesCount + wrappedCount > newWrappedLine)
							{
								newCursorY = i;
								newCursorX = (newWrappedLine - wrappedLinesCount) * safeWidth;
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
						Container?.Invalidate(true);
						return true;
					}
					else
					{
						_isEditing = false;
						Invalidate();
						Container?.Invalidate(true);
						return true;
					}

				default:
					// Keys with Ctrl or Alt modifiers are commands/shortcuts, not text input
					// Let them bubble up to window/application handlers
					if (key.Modifiers.HasFlag(ConsoleModifiers.Control) ||
					    key.Modifiers.HasFlag(ConsoleModifiers.Alt))
					{
						break;  // Don't insert, will bubble up since nothing changed
					}

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
				Container?.Invalidate(true);
			}

			// If cursor position changed, ensure it's visible
			if (_cursorX != oldCursorX || _cursorY != oldCursorY)
			{
				EnsureCursorVisible();
				Container?.Invalidate(true);
			}

			// If content changed, notify listeners and invalidate
			if (contentChanged)
			{
					Container?.Invalidate(true);
				ContentChanged?.Invoke(this, GetContent());
			}

			// Only consume the key if we actually did something with it
			// Check if content, cursor position, or selection state changed
			bool cursorMoved = (_cursorX != oldCursorX || _cursorY != oldCursorY);
			bool selectionChanged = (_hasSelection != oldHasSelection);
			bool keyWasHandled = contentChanged || cursorMoved || selectionChanged;

			return keyWasHandled;
		}

		/// <summary>
		/// Sets the text content from a string, splitting on line breaks.
		/// </summary>
		/// <param name="content">The text content to set.</param>
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

			Container?.Invalidate(false);

			_skipUpdateScrollPositionsInRender = false;

			// Notify that content has changed
			ContentChanged?.Invoke(this, GetContent());
		}


		private void DeleteSelectedText()
		{
			if (!_hasSelection) return;
			if (_lines.Count == 0) return;

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

			// Clear the selection
			ClearSelection();
		}

		/// <summary>
		/// Gets a safe effective width value, ensuring it's never zero to prevent division by zero errors.
		/// </summary>
		private int SafeEffectiveWidth => _effectiveWidth > 0 ? _effectiveWidth : 80;

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

		private int GetTotalWrappedLineCount()
		{
			if (_wrapMode == WrapMode.NoWrap)
			{
				return _lines.Count;
			}

			int safeWidth = SafeEffectiveWidth;
			int totalWrappedLines = 0;
			for (int i = 0; i < _lines.Count; i++)
			{
				int len = _lines[i].Length;
				totalWrappedLines += (len > 0) ? ((len - 1) / safeWidth) + 1 : 1;
			}
			return totalWrappedLines;
		}

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

		private List<string> RenderVerticalScrollbar(int height, int maxContentHeight, Color scrollbarBgColor)
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
					scrollbarBgColor,
					color
				)[0]);
			}

			return result;
		}

		/// <inheritdoc/>
		public Point? GetLogicalCursorPosition()
		{

			// Only show cursor when in editing mode
			if (!_isEditing)
			{
				return null;
			}

			// Guard against uninitialized _effectiveWidth
			int effectiveWidth = _effectiveWidth > 0 ? _effectiveWidth : 80;

			if (_wrapMode == WrapMode.NoWrap)
			{
				// NoWrap: return position adjusted for margins and scroll offsets
				var pos = new Point(
					_margin.Left + _cursorX - _horizontalScrollOffset,
					_margin.Top + _cursorY - _verticalScrollOffset);
				return pos;
			}
			else
			{
				// Wrap mode: calculate visual position within wrapped content
				// First, calculate total wrapped lines before the cursor's logical line
				int totalWrappedLinesBefore = 0;
				for (int i = 0; i < _cursorY; i++)
				{
					int len = _lines[i].Length;
					totalWrappedLinesBefore += (len > 0) ? ((len - 1) / effectiveWidth) + 1 : 1;
				}

				// Calculate which wrapped line within the current logical line contains the cursor
				int cursorWrappedLine = _cursorX / effectiveWidth;

				// Visual Y = total wrapped lines before + cursor's wrapped line - vertical scroll
				int visualY = totalWrappedLinesBefore + cursorWrappedLine - _verticalScrollOffset;

				// Visual X = cursor position within the current wrapped line segment
				int visualX = _cursorX % effectiveWidth;

				// Add margin offsets to visual position
				var pos = new Point(_margin.Left + visualX, _margin.Top + visualY);
				return pos;
			}
		}

		/// <inheritdoc/>
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

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <summary>
		/// Occurs when the control receives focus.
		/// </summary>
		public event EventHandler? GotFocus;

		/// <summary>
		/// Occurs when the control loses focus.
		/// </summary>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			var hadFocus = _hasFocus;
			_hasFocus = focus;

			// Enter edit mode when focused with mouse
			if (focus && !hadFocus && reason == FocusReason.Mouse && !_readOnly)
			{
				_isEditing = true;
			}

			// Exit edit mode when losing focus
			if (!focus && hadFocus && _isEditing)
			{
				_isEditing = false;
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

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Handle mouse clicks - enter edit mode if already focused
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				if (_hasFocus && !_readOnly)
				{
					_isEditing = true;
					Container?.Invalidate(true);
				}

				MouseClick?.Invoke(this, args);
				return true;
			}

			// Handle mouse position reports
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return false;
			}

			return false;
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int baseWidth = _width ?? constraints.MaxWidth - _margin.Left - _margin.Right;
			int contentHeight = _viewportHeight;

			// Account for horizontal scrollbar if needed
			bool needsHorizontalScrollbar = _wrapMode == WrapMode.NoWrap &&
				(_horizontalScrollbarVisibility == ScrollbarVisibility.Always ||
				 (_horizontalScrollbarVisibility == ScrollbarVisibility.Auto &&
				  GetMaxLineLength() > baseWidth));
			if (needsHorizontalScrollbar) contentHeight++;

			int width = baseWidth + _margin.Left + _margin.Right;
			int height = contentHeight + _margin.Top + _margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			Color bgColor = _hasFocus
				? (_isEditing ? FocusedBackgroundColor : Container?.GetConsoleWindowSystem?.Theme?.TextEditFocusedNotEditing ?? Color.LightSlateGrey)
				: BackgroundColor;
			Color fgColor = _hasFocus ? FocusedForegroundColor : ForegroundColor;
			Color selBgColor = SelectionBackgroundColor;
			Color selFgColor = SelectionForegroundColor;
			Color windowBgColor = Container?.BackgroundColor ?? defaultBg;

			int targetWidth = bounds.Width - _margin.Left - _margin.Right;
			if (targetWidth <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, windowBgColor);

			// Determine if scrollbars will be shown
			bool needsVerticalScrollbar = _verticalScrollbarVisibility == ScrollbarVisibility.Always ||
				(_verticalScrollbarVisibility == ScrollbarVisibility.Auto &&
				 GetTotalWrappedLineCount() > _viewportHeight);

			bool needsHorizontalScrollbar = _wrapMode == WrapMode.NoWrap &&
				(_horizontalScrollbarVisibility == ScrollbarVisibility.Always ||
				 (_horizontalScrollbarVisibility == ScrollbarVisibility.Auto &&
				  GetMaxLineLength() > targetWidth));

			int scrollbarWidth = needsVerticalScrollbar ? 1 : 0;
			int effectiveWidth = targetWidth - scrollbarWidth;
			_effectiveWidth = effectiveWidth;

			if (effectiveWidth <= 0) return;

			// Wrap lines and track source positions
			List<string> allWrappedLines = new List<string>();
			List<int> sourceLineIndex = new List<int>();
			List<int> sourceLineOffset = new List<int>();

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
					if (line.Length == 0)
					{
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
					if (string.IsNullOrEmpty(line))
					{
						allWrappedLines.Add(string.Empty);
						sourceLineIndex.Add(i);
						sourceLineOffset.Add(0);
					}
					else
					{
						var words = line.Split(' ');
						var currentLine = new StringBuilder();
						int currentOffset = 0;

						foreach (var word in words)
						{
							if (word.Length > effectiveWidth)
							{
								if (currentLine.Length > 0)
								{
									allWrappedLines.Add(currentLine.ToString());
									sourceLineIndex.Add(i);
									sourceLineOffset.Add(currentOffset);
									currentOffset += currentLine.Length + 1;
									currentLine.Clear();
								}

								for (int k = 0; k < word.Length; k += effectiveWidth)
								{
									int chunkLen = Math.Min(effectiveWidth, word.Length - k);
									allWrappedLines.Add(word.Substring(k, chunkLen));
									sourceLineIndex.Add(i);
									sourceLineOffset.Add(currentOffset + k);
								}
								currentOffset += word.Length + 1;
								continue;
							}

							if (currentLine.Length + word.Length + (currentLine.Length > 0 ? 1 : 0) > effectiveWidth)
							{
								allWrappedLines.Add(currentLine.ToString());
								sourceLineIndex.Add(i);
								sourceLineOffset.Add(currentOffset);
								currentOffset += currentLine.Length + 1;
								currentLine.Clear();
							}

							if (currentLine.Length > 0) currentLine.Append(' ');
							currentLine.Append(word);
						}

						allWrappedLines.Add(currentLine.ToString());
						sourceLineIndex.Add(i);
						sourceLineOffset.Add(currentOffset);
					}
				}
			}

			// Find wrapped line with cursor and adjust scroll
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
				wrappedLineWithCursor = _cursorY;
			}

			// Adjust vertical scroll
			if (wrappedLineWithCursor >= 0 && !_skipUpdateScrollPositionsInRender)
			{
				if (wrappedLineWithCursor < _verticalScrollOffset)
					_verticalScrollOffset = wrappedLineWithCursor;
				else if (wrappedLineWithCursor >= _verticalScrollOffset + _viewportHeight)
					_verticalScrollOffset = wrappedLineWithCursor - _viewportHeight + 1;
				_skipUpdateScrollPositionsInRender = false;
			}

			// Get selection bounds
			var (selStartX, selStartY, selEndX, selEndY) = GetOrderedSelectionBounds();

			// Paint visible lines
			int availableHeight = bounds.Height - _margin.Top - _margin.Bottom - (needsHorizontalScrollbar ? 1 : 0);
			int linesToPaint = Math.Min(_viewportHeight, availableHeight);

			for (int i = 0; i < linesToPaint; i++)
			{
				int paintY = startY + i;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
				{
					// Fill left margin
					if (_margin.Left > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, windowBgColor);
					}

					int wrappedIndex = i + _verticalScrollOffset;
					if (wrappedIndex < allWrappedLines.Count)
					{
						int actualSourceLineIndex = sourceLineIndex[wrappedIndex];
						int actualSourceOffset = sourceLineOffset[wrappedIndex];
						string line = allWrappedLines[wrappedIndex];
						string visibleLine = line;

						// Apply horizontal scrolling
						if (_horizontalScrollOffset > 0 && _horizontalScrollOffset < line.Length)
							visibleLine = line.Substring(_horizontalScrollOffset);
						else if (_horizontalScrollOffset >= line.Length)
							visibleLine = string.Empty;

						// Pad or truncate to effective width
						if (visibleLine.Length < effectiveWidth)
							visibleLine = visibleLine.PadRight(effectiveWidth);
						else if (visibleLine.Length > effectiveWidth)
							visibleLine = visibleLine.Substring(0, effectiveWidth);

						// Paint each character with selection handling
						for (int charPos = 0; charPos < effectiveWidth; charPos++)
						{
							int actualCharPos = charPos + actualSourceOffset + _horizontalScrollOffset;
							bool isSelected = false;

							if (_hasSelection && actualSourceLineIndex >= selStartY && actualSourceLineIndex <= selEndY)
							{
								if (actualSourceLineIndex == selStartY && actualSourceLineIndex == selEndY)
									isSelected = actualCharPos >= selStartX && actualCharPos < selEndX;
								else if (actualSourceLineIndex == selStartY)
									isSelected = actualCharPos >= selStartX;
								else if (actualSourceLineIndex == selEndY)
									isSelected = actualCharPos < selEndX;
								else
									isSelected = true;
							}

							char c = charPos < visibleLine.Length ? visibleLine[charPos] : ' ';
							int cellX = startX + charPos;
							if (cellX >= clipRect.X && cellX < clipRect.Right)
							{
								buffer.SetCell(cellX, paintY, c,
									isSelected ? selFgColor : fgColor,
									isSelected ? selBgColor : bgColor);
							}
						}
					}
					else
					{
						// Empty line beyond content
						buffer.FillRect(new LayoutRect(startX, paintY, effectiveWidth, 1), ' ', fgColor, bgColor);
					}

					// Paint vertical scrollbar
					if (needsVerticalScrollbar)
					{
						int scrollX = startX + effectiveWidth;
						if (scrollX >= clipRect.X && scrollX < clipRect.Right)
						{
							int totalLines = GetTotalWrappedLineCount();
							int thumbHeight = Math.Max(1, (_viewportHeight * _viewportHeight) / Math.Max(1, totalLines));
							int maxThumbPos = _viewportHeight - thumbHeight;
							int thumbPos = totalLines > _viewportHeight
								? (int)Math.Round((double)_verticalScrollOffset / (totalLines - _viewportHeight) * maxThumbPos)
								: 0;

							bool isThumb = i >= thumbPos && i < thumbPos + thumbHeight;
							char scrollChar = isThumb ? '█' : '│';
							buffer.SetCell(scrollX, paintY, scrollChar,
								isThumb ? ScrollbarThumbColor : ScrollbarColor,
								bgColor);
						}
					}

					// Fill right margin
					if (_margin.Right > 0)
					{
						int rightMarginX = startX + effectiveWidth + scrollbarWidth;
						buffer.FillRect(new LayoutRect(rightMarginX, paintY, _margin.Right, 1), ' ', fgColor, windowBgColor);
					}
				}
			}

			// Fill remaining viewport height with empty lines
			for (int i = linesToPaint; i < _viewportHeight && startY + i < bounds.Bottom; i++)
			{
				int paintY = startY + i;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
				{
					if (_margin.Left > 0)
						buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, windowBgColor);

					buffer.FillRect(new LayoutRect(startX, paintY, effectiveWidth, 1), ' ', fgColor, bgColor);

					if (needsVerticalScrollbar)
					{
						int scrollX = startX + effectiveWidth;
						if (scrollX >= clipRect.X && scrollX < clipRect.Right)
						{
							int totalLines = GetTotalWrappedLineCount();
							int thumbHeight = Math.Max(1, (_viewportHeight * _viewportHeight) / Math.Max(1, totalLines));
							int maxThumbPos = _viewportHeight - thumbHeight;
							int thumbPos = totalLines > _viewportHeight
								? (int)Math.Round((double)_verticalScrollOffset / (totalLines - _viewportHeight) * maxThumbPos)
								: 0;

							bool isThumb = i >= thumbPos && i < thumbPos + thumbHeight;
							char scrollChar = isThumb ? '█' : '│';
							buffer.SetCell(scrollX, paintY, scrollChar,
								isThumb ? ScrollbarThumbColor : ScrollbarColor,
								bgColor);
						}
					}

					if (_margin.Right > 0)
					{
						int rightMarginX = startX + effectiveWidth + scrollbarWidth;
						buffer.FillRect(new LayoutRect(rightMarginX, paintY, _margin.Right, 1), ' ', fgColor, windowBgColor);
					}
				}
			}

			// Paint horizontal scrollbar
			if (needsHorizontalScrollbar)
			{
				int scrollY = startY + _viewportHeight;
				if (scrollY >= clipRect.Y && scrollY < clipRect.Bottom && scrollY < bounds.Bottom)
				{
					if (_margin.Left > 0)
						buffer.FillRect(new LayoutRect(bounds.X, scrollY, _margin.Left, 1), ' ', fgColor, windowBgColor);

					int maxLineLength = GetMaxLineLength();
					int thumbWidth = Math.Max(1, (effectiveWidth * effectiveWidth) / Math.Max(1, maxLineLength));
					int maxThumbPos = effectiveWidth - thumbWidth;
					int thumbPos = maxLineLength > effectiveWidth
						? (int)Math.Round((double)_horizontalScrollOffset / (maxLineLength - effectiveWidth) * maxThumbPos)
						: 0;

					for (int x = 0; x < effectiveWidth; x++)
					{
						int cellX = startX + x;
						if (cellX >= clipRect.X && cellX < clipRect.Right)
						{
							bool isThumb = x >= thumbPos && x < thumbPos + thumbWidth;
							char scrollChar = isThumb ? '█' : '─';
							buffer.SetCell(cellX, scrollY, scrollChar,
								isThumb ? ScrollbarThumbColor : ScrollbarColor,
								bgColor);
						}
					}

					if (needsVerticalScrollbar)
					{
						int cornerX = startX + effectiveWidth;
						if (cornerX >= clipRect.X && cornerX < clipRect.Right)
						{
							buffer.SetCell(cornerX, scrollY, '┘', ScrollbarColor, bgColor);
						}
					}

					if (_margin.Right > 0)
					{
						int rightMarginX = startX + effectiveWidth + scrollbarWidth;
						buffer.FillRect(new LayoutRect(rightMarginX, scrollY, _margin.Right, 1), ' ', fgColor, windowBgColor);
					}
				}
			}

			// Fill bottom margin
			int contentEndY = startY + _viewportHeight + (needsHorizontalScrollbar ? 1 : 0);
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, contentEndY, fgColor, windowBgColor);
		}

		#endregion
	}
}
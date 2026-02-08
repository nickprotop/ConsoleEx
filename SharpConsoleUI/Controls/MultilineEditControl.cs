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

using SharpConsoleUI.Configuration;
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
		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;
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
		private bool _isDragging = false;
		private int _tabSize = ControlDefaults.DefaultTabSize;
		private string? _placeholderText;
		private int? _maxLength;

		// Undo/redo
		private readonly Stack<UndoAction> _undoStack = new();
		private readonly Stack<UndoAction> _redoStack = new();
		private int _undoLimit = ControlDefaults.DefaultUndoLimit;
		private bool _isModified = false;
		private string? _savedContent = null;
		private string? _pendingUndoBefore;
		private int _pendingCursorXBefore;
		private int _pendingCursorYBefore;

		// Overwrite mode
		private bool _overwriteMode;

		// Auto-indent
		private bool _autoIndent;

		// Current line highlight
		private bool _highlightCurrentLine;
		private Color? _currentLineHighlightColorValue;

		// Visible whitespace
		private bool _showWhitespace;

		// Line numbers
		private bool _showLineNumbers;
		private Color? _lineNumberColorValue;

		// Syntax highlighting
		private ISyntaxHighlighter? _syntaxHighlighter;
		private Dictionary<int, IReadOnlyList<SyntaxToken>>? _syntaxTokenCache;

		// Wrapping cache - invalidated on content change, resize, or wrap mode change
		private List<WrappedLineInfo>? _wrappedLinesCache;
		private int _wrappedLinesCacheWidth = -1;

		/// <summary>
		/// Initializes a new instance of the MultilineEditControl with the specified viewport height.
		/// </summary>
		/// <param name="viewportHeight">The number of visible lines in the viewport.</param>
		public MultilineEditControl(int viewportHeight = ControlDefaults.DefaultEditorViewportHeight)
		{
			_viewportHeight = Math.Max(1, viewportHeight);
		}

		/// <summary>
		/// Initializes a new instance of the MultilineEditControl with initial content and viewport height.
		/// </summary>
		/// <param name="initialContent">The initial text content to display.</param>
		/// <param name="viewportHeight">The number of visible lines in the viewport.</param>
		public MultilineEditControl(string initialContent, int viewportHeight = ControlDefaults.DefaultEditorViewportHeight)
		{
			_viewportHeight = Math.Max(1, viewportHeight);
			SetContent(initialContent);
		}

		/// <summary>
		/// Occurs when the text content changes.
		/// </summary>
		public event EventHandler<string>? ContentChanged;

		/// <summary>
		/// Occurs when the cursor position changes.
		/// Event argument is a tuple of (Line, Column) using 1-based indices.
		/// </summary>
		public event EventHandler<(int Line, int Column)>? CursorPositionChanged;

		/// <summary>
		/// Occurs when the selection state changes (selected/deselected or bounds change).
		/// Event argument is the currently selected text, or empty string if no selection.
		/// </summary>
		public event EventHandler<string>? SelectionChanged;

		/// <summary>
		/// Occurs when the editing mode changes (entering or leaving edit mode).
		/// Event argument is true when entering edit mode, false when leaving.
		/// </summary>
		public event EventHandler<bool>? EditingModeChanged;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		#pragma warning disable CS0067  // Event never raised (interface requirement)
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
		public int? ContentWidth
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

		/// <inheritdoc/>
		public int ActualX => _actualX;
		/// <inheritdoc/>
		public int ActualY => _actualY;
		/// <inheritdoc/>
		public int ActualWidth => _actualWidth;
		/// <inheritdoc/>
		public int ActualHeight => _actualHeight;

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
		{
			get => _isEditing;
			set
			{
				if (_isEditing == value) return;
				_isEditing = value;
				Container?.Invalidate(true);
				EditingModeChanged?.Invoke(this, _isEditing);
			}
		}

		/// <summary>
		/// Gets the preferred cursor shape based on editing state.
		/// Returns VerticalBar when editing (like modern text editors), null otherwise.
		/// </summary>
		public CursorShape? PreferredCursorShape => _isEditing ? (_overwriteMode ? CursorShape.Block : CursorShape.VerticalBar) : null;

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
				InvalidateWrappedLinesCache();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the number of spaces used for tab indentation.
		/// </summary>
		public int TabSize
		{
			get => _tabSize;
			set => _tabSize = Math.Clamp(value, 1, ControlDefaults.MaxTabSize);
		}

		/// <summary>
		/// Gets or sets the maximum number of undo actions retained.
		/// </summary>
		public int UndoLimit
		{
			get => _undoLimit;
			set => _undoLimit = Math.Max(1, value);
		}

		/// <summary>
		/// Gets whether the content has been modified since the last save point.
		/// </summary>
		public bool IsModified => _isModified;

		/// <summary>
		/// Gets the current cursor line number (1-based).
		/// </summary>
		public int CurrentLine => _cursorY + 1;

		/// <summary>
		/// Gets the current cursor column number (1-based).
		/// </summary>
		public int CurrentColumn => _cursorX + 1;

		/// <summary>
		/// Gets the total number of lines in the content.
		/// </summary>
		public int LineCount => _lines.Count;

		/// <summary>
		/// Gets or sets the placeholder text shown when the control is empty and not editing.
		/// </summary>
		public string? PlaceholderText
		{
			get => _placeholderText;
			set
			{
				_placeholderText = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the maximum total character length of the content.
		/// Null means no limit. Enforced on all insertion operations.
		/// </summary>
		public int? MaxLength
		{
			get => _maxLength;
			set => _maxLength = value.HasValue ? Math.Max(0, value.Value) : null;
		}

		/// <summary>
		/// Gets or sets whether overwrite mode is active.
		/// In overwrite mode, typed characters replace the character at the cursor instead of inserting.
		/// Toggle with the Insert key.
		/// </summary>
		public bool OverwriteMode
		{
			get => _overwriteMode;
			set
			{
				if (_overwriteMode == value) return;
				_overwriteMode = value;
				Container?.Invalidate(true);
				OverwriteModeChanged?.Invoke(this, _overwriteMode);
			}
		}

		/// <summary>
		/// Occurs when overwrite mode is toggled.
		/// Event argument is true when overwrite mode is active, false for insert mode.
		/// </summary>
		public event EventHandler<bool>? OverwriteModeChanged;

		/// <summary>
		/// Gets or sets whether auto-indent is enabled.
		/// When enabled, pressing Enter copies leading whitespace from the current line to the new line.
		/// </summary>
		public bool AutoIndent
		{
			get => _autoIndent;
			set => _autoIndent = value;
		}

		/// <summary>
		/// Gets or sets whether the current line is visually highlighted.
		/// </summary>
		public bool HighlightCurrentLine
		{
			get => _highlightCurrentLine;
			set
			{
				if (_highlightCurrentLine == value) return;
				_highlightCurrentLine = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color used to highlight the current line.
		/// Defaults to a slightly lighter shade of the editor background.
		/// </summary>
		public Color CurrentLineHighlightColor
		{
			get => _currentLineHighlightColorValue ?? Color.Grey11;
			set
			{
				_currentLineHighlightColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether whitespace characters are rendered with visible markers.
		/// Spaces are shown as middle dots.
		/// </summary>
		public bool ShowWhitespace
		{
			get => _showWhitespace;
			set
			{
				if (_showWhitespace == value) return;
				_showWhitespace = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether line numbers are displayed in a gutter on the left side.
		/// </summary>
		public bool ShowLineNumbers
		{
			get => _showLineNumbers;
			set
			{
				if (_showLineNumbers == value) return;
				_showLineNumbers = value;
				InvalidateWrappedLinesCache();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color for line numbers in the gutter.
		/// </summary>
		public Color LineNumberColor
		{
			get => _lineNumberColorValue ?? Color.Grey;
			set
			{
				_lineNumberColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the syntax highlighter used to colorize content.
		/// Set to null to disable syntax highlighting.
		/// </summary>
		public ISyntaxHighlighter? SyntaxHighlighter
		{
			get => _syntaxHighlighter;
			set
			{
				_syntaxHighlighter = value;
				_syntaxTokenCache = null;
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

			content = SanitizeInputText(content);

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
			int effectiveWidth = _effectiveWidth > 0 ? _effectiveWidth : ControlDefaults.DefaultEditorWidth;

			// Special handling for wrap mode - use shared wrapping infrastructure
			if (_wrapMode != WrapMode.NoWrap)
			{
				var wrappedLines = GetWrappedLines(effectiveWidth);
				int wrappedIndex = FindWrappedLineForCursor(wrappedLines);
				if (wrappedIndex >= 0)
				{
					if (wrappedIndex < _verticalScrollOffset)
						_verticalScrollOffset = wrappedIndex;
					else if (wrappedIndex >= _verticalScrollOffset + _viewportHeight)
						_verticalScrollOffset = wrappedIndex - _viewportHeight + 1;
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

			text = TruncateToMaxLength(SanitizeInputText(text));
			if (text.Length == 0)
				return;

			InsertTextAtCursor(text);

			InvalidateWrappedLinesCache();
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
			var textLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

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
				if (_hasFocus && key.Key == ConsoleKey.Enter)
				{
					IsEditing = true;
					return true;
				}
				return false;
			}

			bool contentChanged = false;
			BeginUndoAction();
			bool isShiftPressed = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
			bool isCtrlPressed = key.Modifiers.HasFlag(ConsoleModifiers.Control);
			int oldCursorX = _cursorX;
			int oldCursorY = _cursorY;
			bool oldHasSelection = _hasSelection;
			int oldSelEndX = _selectionEndX;
			int oldSelEndY = _selectionEndY;

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
							int targetIdx = Math.Max(0, idx - _viewportHeight);
							var target = wrappedLines[targetIdx];
							_cursorY = target.SourceLineIndex;
							_cursorX = target.SourceCharOffset + Math.Min(visualX, target.Length);
						}
					}
					else
					{
						_cursorY = Math.Max(0, _cursorY - _viewportHeight);
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
							int targetIdx = Math.Min(wrappedLines.Count - 1, idx + _viewportHeight);
							var target = wrappedLines[targetIdx];
							_cursorY = target.SourceLineIndex;
							_cursorX = target.SourceCharOffset + Math.Min(visualX, target.Length);
						}
					}
					else
					{
						_cursorY = Math.Min(_lines.Count - 1, _cursorY + _viewportHeight);
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

							// Clipboard and undo operations use Ctrl+Shift to avoid
							// conflicting with terminal OS-level shortcuts (Ctrl+C = SIGINT, etc.)
							case ConsoleKey.C:
								if (!isShiftPressed) break;
								if (_hasSelection)
									ClipboardHelper.SetText(GetSelectedText());
								return true;

							case ConsoleKey.X:
								if (!isShiftPressed) break;
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
								if (!isShiftPressed) break;
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
								if (!isShiftPressed) break;
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
								if (!isShiftPressed) break;
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

			// If cursor position changed, ensure it's visible
			if (_cursorX != oldCursorX || _cursorY != oldCursorY)
			{
				EnsureCursorVisible();
				Container?.Invalidate(true);
			}

			// If content changed, commit undo, notify listeners and invalidate
			if (contentChanged)
			{
				CommitUndoAction();
				InvalidateWrappedLinesCache();
				Container?.Invalidate(true);
				ContentChanged?.Invoke(this, GetContent());
			}

			// Only consume the key if we actually did something with it
			// Check if content, cursor position, or selection state changed
			bool cursorMoved = (_cursorX != oldCursorX || _cursorY != oldCursorY);
			bool selectionChanged = (_hasSelection != oldHasSelection) ||
				(_hasSelection && (_selectionEndX != oldSelEndX || _selectionEndY != oldSelEndY));
			bool keyWasHandled = contentChanged || cursorMoved || selectionChanged;

			// Fire cursor position changed event
			if (cursorMoved)
				CursorPositionChanged?.Invoke(this, (CurrentLine, CurrentColumn));

			// Fire selection changed event
			if (selectionChanged)
				SelectionChanged?.Invoke(this, _hasSelection ? GetSelectedText() : string.Empty);

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
				content = SanitizeInputText(content);
				_lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
				if (_lines.Count == 0)
				{
					_lines.Add(string.Empty);
				}
			}

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
				_lines = lines.Select(SanitizeLine).ToList();
				if (_lines.Count == 0)
				{
					_lines.Add(string.Empty);
				}
			}

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

			InvalidateWrappedLinesCache();
			// Clear the selection
			ClearSelection();
		}

		/// <summary>
		/// Gets a safe effective width value, ensuring it's never zero to prevent division by zero errors.
		/// </summary>
		private int SafeEffectiveWidth => _effectiveWidth > 0 ? _effectiveWidth : ControlDefaults.DefaultEditorWidth;

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
				return _lines.Count;
			return GetWrappedLines(SafeEffectiveWidth).Count;
		}

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
			_lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
			if (_lines.Count == 0)
				_lines.Add(string.Empty);
			InvalidateWrappedLinesCache();
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

		private int GetGutterWidth()
		{
			if (!_showLineNumbers) return 0;
			int digits = Math.Max(1, (int)Math.Floor(Math.Log10(Math.Max(1, _lines.Count))) + 1);
			return digits + ControlDefaults.LineNumberGutterPadding;
		}

		private Color GetSyntaxColor(int lineIndex, int charIndex, Color defaultColor)
		{
			if (_syntaxHighlighter == null) return defaultColor;

			_syntaxTokenCache ??= new Dictionary<int, IReadOnlyList<SyntaxToken>>();

			if (!_syntaxTokenCache.TryGetValue(lineIndex, out var tokens))
			{
				tokens = _syntaxHighlighter.Tokenize(_lines[lineIndex], lineIndex);
				_syntaxTokenCache[lineIndex] = tokens;
			}
			foreach (var token in tokens)
			{
				if (charIndex >= token.StartIndex && charIndex < token.StartIndex + token.Length)
					return token.ForegroundColor;
			}
			return defaultColor;
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

		#endregion

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

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Triple-click: select entire line
			if (args.HasFlag(MouseFlags.Button1TripleClicked))
			{
				if (_hasFocus)
				{
					IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					_hasSelection = true;
					_selectionStartX = 0;
					_selectionStartY = _cursorY;
					_selectionEndX = _lines[_cursorY].Length;
					_selectionEndY = _cursorY;
					_cursorX = _lines[_cursorY].Length;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Double-click: select word
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				if (_hasFocus)
				{
					IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					var (wordStart, wordEnd) = WordBoundaryHelper.FindWordAt(_lines[_cursorY], _cursorX);
					_hasSelection = wordStart != wordEnd;
					_selectionStartX = wordStart;
					_selectionStartY = _cursorY;
					_selectionEndX = wordEnd;
					_selectionEndY = _cursorY;
					_cursorX = wordEnd;
					Container?.Invalidate(true);
				}
				MouseDoubleClick?.Invoke(this, args);
				return true;
			}

			// Mouse button pressed: start potential drag
			if (args.HasFlag(MouseFlags.Button1Pressed))
			{
				if (_hasFocus && !_readOnly)
				{
					IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					_hasSelection = true;
					_selectionStartX = _cursorX;
					_selectionStartY = _cursorY;
					_selectionEndX = _cursorX;
					_selectionEndY = _cursorY;
					_isDragging = true;
					EnsureCursorVisible();
					Container?.Invalidate(true);
				}
				return true;
			}

			// Single click
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				if (_hasFocus && !_readOnly)
				{
					IsEditing = true;
					PositionCursorFromMouse(args.Position.X, args.Position.Y);
				}
				_isDragging = false;
				MouseClick?.Invoke(this, args);
				return true;
			}

			// Mouse button released: end drag
			if (args.HasFlag(MouseFlags.Button1Released))
			{
				_isDragging = false;
				return true;
			}

			// Mouse drag: extend selection
			if (args.HasFlag(MouseFlags.ReportMousePosition) && _isDragging)
			{
				PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
				_selectionEndX = _cursorX;
				_selectionEndY = _cursorY;
				_hasSelection = (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY);
				EnsureCursorVisible();
				Container?.Invalidate(true);
				return true;
			}

			// Mouse wheel up
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				int scrollAmount = Math.Min(ControlDefaults.DefaultScrollWheelLines, _verticalScrollOffset);
				if (scrollAmount > 0)
				{
					_skipUpdateScrollPositionsInRender = true;
					_verticalScrollOffset -= scrollAmount;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Mouse wheel down
			if (args.HasFlag(MouseFlags.WheeledDown))
			{
				int totalLines = GetTotalWrappedLineCount();
				int maxScroll = Math.Max(0, totalLines - _viewportHeight);
				int scrollAmount = Math.Min(ControlDefaults.DefaultScrollWheelLines, maxScroll - _verticalScrollOffset);
				if (scrollAmount > 0)
				{
					_skipUpdateScrollPositionsInRender = true;
					_verticalScrollOffset += scrollAmount;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Regular mouse move (no drag)
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return false;
			}

			return false;
		}

		/// <summary>
		/// Core positioning: maps control-relative mouse coordinates to cursor position.
		/// Does NOT clear selection or invalidate — caller decides.
		/// </summary>
		private void PositionCursorFromMouseCore(int mouseX, int mouseY)
		{
			int relX = mouseX - _margin.Left - GetGutterWidth();
			int relY = mouseY - _margin.Top;

			if (relX < 0) relX = 0;
			if (relY < 0) relY = 0;

			if (_wrapMode == WrapMode.NoWrap)
			{
				_cursorY = Math.Min(_lines.Count - 1, relY + _verticalScrollOffset);
				_cursorX = Math.Min(_lines[_cursorY].Length, relX + _horizontalScrollOffset);
			}
			else
			{
				var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
				int wrappedIndex = Math.Clamp(relY + _verticalScrollOffset, 0, wrappedLines.Count - 1);

				var wl = wrappedLines[wrappedIndex];
				_cursorY = wl.SourceLineIndex;
				_cursorX = Math.Min(wl.SourceCharOffset + relX, wl.SourceCharOffset + wl.Length);
				_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
			}
		}

		/// <summary>
		/// Maps control-relative mouse coordinates to cursor position, clears selection.
		/// Used for simple click positioning.
		/// </summary>
		private void PositionCursorFromMouse(int mouseX, int mouseY)
		{
			PositionCursorFromMouseCore(mouseX, mouseY);
			ClearSelection();
			EnsureCursorVisible();
			Container?.Invalidate(true);
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int baseWidth = _width ?? constraints.MaxWidth - _margin.Left - _margin.Right;
			int contentHeight = _viewportHeight;

			// Account for vertical scrollbar and gutter taking columns from content area
			bool needsVerticalScrollbar = _verticalScrollbarVisibility == ScrollbarVisibility.Always ||
				(_verticalScrollbarVisibility == ScrollbarVisibility.Auto &&
				 GetTotalWrappedLineCount() > _viewportHeight);
			int scrollbarWidth = needsVerticalScrollbar ? 1 : 0;
			int gutterWidth = GetGutterWidth();
			int contentWidth = baseWidth - scrollbarWidth - gutterWidth;

			// Account for horizontal scrollbar if needed (using content width after scrollbar and gutter)
			bool needsHorizontalScrollbar = _wrapMode == WrapMode.NoWrap &&
				(_horizontalScrollbarVisibility == ScrollbarVisibility.Always ||
				 (_horizontalScrollbarVisibility == ScrollbarVisibility.Auto &&
				  GetMaxLineLength() > contentWidth));
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
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			Color bgColor = _hasFocus
				? (_isEditing ? FocusedBackgroundColor : Container?.GetConsoleWindowSystem?.Theme?.TextEditFocusedNotEditing ?? Color.LightSlateGrey)
				: BackgroundColor;
			Color fgColor = _hasFocus ? FocusedForegroundColor : ForegroundColor;
			Color selBgColor = SelectionBackgroundColor;
			Color selFgColor = SelectionForegroundColor;
			Color windowBgColor = Container?.BackgroundColor ?? defaultBg;

			int targetWidth = bounds.Width - _margin.Left - _margin.Right;
			if (targetWidth <= 0) { _skipUpdateScrollPositionsInRender = false; return; }

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
			int gutterWidth = GetGutterWidth();
			int effectiveWidth = targetWidth - scrollbarWidth - gutterWidth;
			_effectiveWidth = effectiveWidth;

			if (effectiveWidth <= 0) { _skipUpdateScrollPositionsInRender = false; return; }

			// Use shared wrapping infrastructure
			var wrappedLines = GetWrappedLines(effectiveWidth);

			// Find wrapped line with cursor and adjust scroll
			int wrappedLineWithCursor = (_wrapMode != WrapMode.NoWrap)
				? FindWrappedLineForCursor(wrappedLines)
				: _cursorY;

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

			// Determine if placeholder should be shown
			bool showPlaceholder = !string.IsNullOrEmpty(_placeholderText) && !_isEditing &&
				_lines.Count == 1 && _lines[0].Length == 0;

			// Pre-compute vertical scrollbar thumb position (avoid per-row recalculation)
			int vThumbHeight = 0, vThumbPos = 0;
			int totalWrappedLineCount = 0;
			if (needsVerticalScrollbar)
			{
				totalWrappedLineCount = GetTotalWrappedLineCount();
				vThumbHeight = Math.Max(1, (_viewportHeight * _viewportHeight) / Math.Max(1, totalWrappedLineCount));
				int maxThumbPos = _viewportHeight - vThumbHeight;
				vThumbPos = totalWrappedLineCount > _viewportHeight
					? (int)Math.Round((double)_verticalScrollOffset / (totalWrappedLineCount - _viewportHeight) * maxThumbPos)
					: 0;
			}

			// Pre-compute scrollbar background color (fixed, not focus-dependent)
			Color scrollbarBg = BackgroundColor;

				int contentStartX = startX + gutterWidth;

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

					// Render gutter (line numbers)
					if (gutterWidth > 0)
					{
						Color gutterBg = bgColor;
						Color gutterFg = LineNumberColor;
						string gutterText;

						if (wrappedIndex < wrappedLines.Count)
						{
							var gwl = wrappedLines[wrappedIndex];
							bool isFirstWrappedSegment = gwl.SourceCharOffset == 0;
							bool isCurrentLineGutter = _highlightCurrentLine && _isEditing && gwl.SourceLineIndex == _cursorY;
							if (isCurrentLineGutter)
								gutterFg = fgColor;

							if (isFirstWrappedSegment)
								gutterText = (gwl.SourceLineIndex + 1).ToString().PadLeft(gutterWidth - ControlDefaults.LineNumberGutterPadding).PadRight(gutterWidth);
							else
								gutterText = new string(' ', gutterWidth);
						}
						else
						{
							gutterText = new string(' ', gutterWidth);
						}

						for (int g = 0; g < gutterWidth && startX + g < clipRect.Right; g++)
						{
							if (startX + g >= clipRect.X)
								buffer.SetCell(startX + g, paintY, gutterText[g], gutterFg, gutterBg);
						}
					}

					if (wrappedIndex < wrappedLines.Count)
					{
						// Render placeholder text on first visible line when content is empty
						if (showPlaceholder && i == 0)
						{
							string placeholderLine = _placeholderText!.Length > effectiveWidth
								? _placeholderText.Substring(0, effectiveWidth)
								: _placeholderText.PadRight(effectiveWidth);
							Color dimFg = Color.Grey;
							for (int charPos = 0; charPos < effectiveWidth; charPos++)
							{
								int cellX = contentStartX + charPos;
								if (cellX >= clipRect.X && cellX < clipRect.Right)
									buffer.SetCell(cellX, paintY, placeholderLine[charPos], dimFg, bgColor);
							}

							// Fill right margin and scrollbar area
							int rightMarginStart = contentStartX + effectiveWidth;
							int rightFill = bounds.Right - rightMarginStart;
							if (rightFill > 0)
								buffer.FillRect(new LayoutRect(rightMarginStart, paintY, rightFill, 1), ' ', fgColor, windowBgColor);

							continue;
						}

						var wl = wrappedLines[wrappedIndex];
						string line = wl.DisplayText;
						string visibleLine = line;

						// Apply horizontal scrolling (only in NoWrap mode)
						if (_wrapMode == WrapMode.NoWrap && _horizontalScrollOffset > 0)
						{
							if (_horizontalScrollOffset < line.Length)
								visibleLine = line.Substring(_horizontalScrollOffset);
							else
								visibleLine = string.Empty;
						}

						// Pad or truncate to effective width
						if (visibleLine.Length < effectiveWidth)
							visibleLine = visibleLine.PadRight(effectiveWidth);
						else if (visibleLine.Length > effectiveWidth)
							visibleLine = visibleLine.Substring(0, effectiveWidth);

						int hScrollForCalc = (_wrapMode == WrapMode.NoWrap) ? _horizontalScrollOffset : 0;

						// Determine current line highlight
						bool isCurrentLine = _highlightCurrentLine && _isEditing && wl.SourceLineIndex == _cursorY;
						Color lineBg = isCurrentLine ? CurrentLineHighlightColor : bgColor;

						// Paint each character with selection, syntax, and whitespace handling
						for (int charPos = 0; charPos < effectiveWidth; charPos++)
						{
							int actualCharPos = charPos + wl.SourceCharOffset + hScrollForCalc;
							bool isSelected = false;

							if (_hasSelection && wl.SourceLineIndex >= selStartY && wl.SourceLineIndex <= selEndY)
							{
								if (wl.SourceLineIndex == selStartY && wl.SourceLineIndex == selEndY)
									isSelected = actualCharPos >= selStartX && actualCharPos < selEndX;
								else if (wl.SourceLineIndex == selStartY)
									isSelected = actualCharPos >= selStartX;
								else if (wl.SourceLineIndex == selEndY)
									isSelected = actualCharPos < selEndX;
								else
									isSelected = true;
							}

							char c = charPos < visibleLine.Length ? visibleLine[charPos] : ' ';
							bool isContentChar = charPos + hScrollForCalc < wl.DisplayText.Length;

							// Color priority: Selection > Syntax > Visible whitespace > Default
							Color charFg;
							Color charBg;
							if (isSelected)
							{
								charFg = selFgColor;
								charBg = selBgColor;
							}
							else
							{
								charBg = lineBg;

								if (_showWhitespace && c == ' ' && isContentChar)
								{
									c = ControlDefaults.WhitespaceSpaceChar;
									charFg = Color.Grey37;
								}
								else if (_syntaxHighlighter != null)
								{
									charFg = GetSyntaxColor(wl.SourceLineIndex, actualCharPos, fgColor);
								}
								else
								{
									charFg = fgColor;
								}
							}

							int cellX = contentStartX + charPos;
							if (cellX >= clipRect.X && cellX < clipRect.Right)
							{
								buffer.SetCell(cellX, paintY, c, charFg, charBg);
							}
						}
					}
					else
					{
						// Empty line beyond content
						buffer.FillRect(new LayoutRect(contentStartX, paintY, effectiveWidth, 1), ' ', fgColor, bgColor);
					}

					// Paint vertical scrollbar
					if (needsVerticalScrollbar)
					{
						int scrollX = contentStartX + effectiveWidth;
						if (scrollX >= clipRect.X && scrollX < clipRect.Right)
						{
							bool isThumb = i >= vThumbPos && i < vThumbPos + vThumbHeight;
							char scrollChar = isThumb ? '█' : '│';
							buffer.SetCell(scrollX, paintY, scrollChar,
								isThumb ? ScrollbarThumbColor : ScrollbarColor,
								scrollbarBg);
						}
					}

					// Fill right margin
					if (_margin.Right > 0)
					{
						int rightMarginX = contentStartX + effectiveWidth + scrollbarWidth;
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

					// Fill gutter area for empty rows
					if (gutterWidth > 0)
						buffer.FillRect(new LayoutRect(startX, paintY, gutterWidth, 1), ' ', fgColor, bgColor);

					buffer.FillRect(new LayoutRect(contentStartX, paintY, effectiveWidth, 1), ' ', fgColor, bgColor);

					if (needsVerticalScrollbar)
					{
						int scrollX = contentStartX + effectiveWidth;
						if (scrollX >= clipRect.X && scrollX < clipRect.Right)
						{
							bool isThumb = i >= vThumbPos && i < vThumbPos + vThumbHeight;
							char scrollChar = isThumb ? '█' : '│';
							buffer.SetCell(scrollX, paintY, scrollChar,
								isThumb ? ScrollbarThumbColor : ScrollbarColor,
								scrollbarBg);
						}
					}

					if (_margin.Right > 0)
					{
						int rightMarginX = contentStartX + effectiveWidth + scrollbarWidth;
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

					// Fill gutter area under horizontal scrollbar
					if (gutterWidth > 0)
						buffer.FillRect(new LayoutRect(startX, scrollY, gutterWidth, 1), ' ', fgColor, bgColor);

					int maxLineLength = GetMaxLineLength();
					int thumbWidth = Math.Max(1, (effectiveWidth * effectiveWidth) / Math.Max(1, maxLineLength));
					int maxThumbPos = effectiveWidth - thumbWidth;
					int thumbPos = maxLineLength > effectiveWidth
						? (int)Math.Round((double)_horizontalScrollOffset / (maxLineLength - effectiveWidth) * maxThumbPos)
						: 0;

					for (int x = 0; x < effectiveWidth; x++)
					{
						int cellX = contentStartX + x;
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
						int cornerX = contentStartX + effectiveWidth;
						if (cornerX >= clipRect.X && cornerX < clipRect.Right)
						{
							buffer.SetCell(cornerX, scrollY, '┘', ScrollbarColor, bgColor);
						}
					}

					if (_margin.Right > 0)
					{
						int rightMarginX = contentStartX + effectiveWidth + scrollbarWidth;
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
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
	/// A multiline text editing control with support for text selection, scrolling, and word wrap.
	/// Provides full cursor navigation, cut/copy/paste-like operations, and configurable scrollbars.
	/// </summary>
	public partial class MultilineEditControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, ILogicalCursorProvider, ICursorShapeProvider
	{
		#region Fields

		private readonly object _contentLock = new();

		// Color properties
		private Color? _backgroundColorValue;

		private Color _borderColor = Color.White;
		private int _cursorX = 0;
		private int _cursorY = 0;
		private int _effectiveWidth;
		private int _effectiveViewportHeight;
		private bool _needsHorizontalScrollbar;
		private bool _needsVerticalScrollbar;
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
		private ScrollbarVisibility _verticalScrollbarVisibility = ScrollbarVisibility.Auto;
		private int _verticalScrollOffset = 0;
		private int _viewportHeight;
		private WrapMode _wrapMode = WrapMode.Wrap;
		private bool _isDragging = false;

		// Scrollbar drag state
		private bool _scrollbarInteracted = false;
		private bool _isVerticalScrollbarDragging = false;
		private int _verticalScrollbarDragStartY = 0;
		private int _verticalScrollbarDragStartOffset = 0;
		private bool _isHorizontalScrollbarDragging = false;
		private int _horizontalScrollbarDragStartX = 0;
		private int _horizontalScrollbarDragStartOffset = 0;

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

		// Gutter renderers
		private readonly List<IGutterRenderer> _gutterRenderers = new();
		private LineNumberGutterRenderer? _builtInLineNumberRenderer;

		// Line numbers
		private bool _showLineNumbers;
		private Color? _lineNumberColorValue;

		// Editing hints
		private bool _showEditingHints;

		// Syntax highlighting
		private ISyntaxHighlighter? _syntaxHighlighter;
		private Dictionary<int, IReadOnlyList<SyntaxToken>>? _syntaxTokenCache;
		private Dictionary<int, SyntaxLineState>? _lineStateCache;

		// Wrapping cache - invalidated on content change, resize, or wrap mode change
		private List<WrappedLineInfo>? _wrappedLinesCache;
		private int _wrappedLinesCacheWidth = -1;

		#endregion

		#region Constructors

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

		#endregion

		#region Events

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

		/// <summary>
		/// Occurs when the control is right-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;
		#pragma warning restore CS0067

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <summary>
		/// Occurs when overwrite mode is toggled.
		/// Event argument is true when overwrite mode is active, false for insert mode.
		/// </summary>
		public event EventHandler<bool>? OverwriteModeChanged;

		/// <summary>
		/// Occurs when the control receives focus.
		/// </summary>
		public event EventHandler? GotFocus;

		/// <summary>
		/// Occurs when the control loses focus.
		/// </summary>
		public event EventHandler? LostFocus;

		#endregion

		#region Properties

		/// <summary>
		/// Gets the actual rendered width of the control content in characters.
		/// </summary>
		public override int? ContentWidth
		{
			get
			{
				List<string> linesSnapshot;
				lock (_contentLock) { linesSnapshot = _lines.ToList(); }
				int maxLength = 0;
				foreach (var line in linesSnapshot)
				{
					if (line.Length > maxLength) maxLength = line.Length;
				}
				return maxLength + Margin.Left + Margin.Right;
			}
		}

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
		public override IContainer? Container { get; set; }

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

		/// <summary>
		/// Returns the effective viewport height, accounting for VerticalAlignment.Fill.
		/// When Fill is active and the control has been laid out, uses the actual
		/// layout bounds instead of the fixed ViewportHeight.
		/// </summary>
		private int GetEffectiveViewportHeight()
		{
			if (VerticalAlignment != VerticalAlignment.Fill || ActualHeight <= 0)
				return _viewportHeight;
			return Math.Max(_viewportHeight, ActualHeight - Margin.Top - Margin.Bottom);
		}

		/// <inheritdoc/>
		public override int? Width
		{
			get => base.Width;
			set
			{
				var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
				base.Width = validatedValue;
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
		/// Gets the current vertical scroll offset (lines scrolled from top of document).
		/// </summary>
		public int VerticalScrollOffset => _verticalScrollOffset;

		/// <summary>
		/// Gets the current horizontal scroll offset (columns scrolled from left of document).
		/// </summary>
		public int HorizontalScrollOffset => _horizontalScrollOffset;

		/// <summary>
		/// Gets the width of the line-number gutter in columns (0 when ShowLineNumbers is false).
		/// </summary>
		public int GutterWidth => GetGutterWidth();

		/// <summary>
		/// Gets the total number of lines in the content.
		/// </summary>
		public int LineCount { get { lock (_contentLock) { return _lines.Count; } } }

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
		/// When enabled, a built-in <see cref="LineNumberGutterRenderer"/> is inserted at index 0.
		/// </summary>
		public bool ShowLineNumbers
		{
			get => _showLineNumbers;
			set
			{
				if (_showLineNumbers == value) return;
				_showLineNumbers = value;

				lock (_contentLock)
			{
				if (value)
				{
					_builtInLineNumberRenderer = new LineNumberGutterRenderer();
					if (_lineNumberColorValue.HasValue)
						_builtInLineNumberRenderer.LineNumberColor = _lineNumberColorValue.Value;
					_gutterRenderers.Insert(0, _builtInLineNumberRenderer);
				}
				else
				{
					if (_builtInLineNumberRenderer != null)
					{
						_gutterRenderers.Remove(_builtInLineNumberRenderer);
						_builtInLineNumberRenderer = null;
					}
				}
			}

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
				if (_builtInLineNumberRenderer != null)
					_builtInLineNumberRenderer.LineNumberColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets the list of gutter renderers, rendered left-to-right.
		/// </summary>
		public IReadOnlyList<IGutterRenderer> GutterRenderers => _gutterRenderers;

		/// <summary>
		/// Adds a gutter renderer to the end of the renderer list.
		/// </summary>
		public void AddGutterRenderer(IGutterRenderer renderer)
		{
			lock (_contentLock) { _gutterRenderers.Add(renderer); }
			InvalidateWrappedLinesCache();
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Inserts a gutter renderer at the specified index.
		/// </summary>
		public void InsertGutterRenderer(int index, IGutterRenderer renderer)
		{
			lock (_contentLock) { _gutterRenderers.Insert(index, renderer); }
			InvalidateWrappedLinesCache();
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Removes a gutter renderer from the list.
		/// </summary>
		public bool RemoveGutterRenderer(IGutterRenderer renderer)
		{
			bool removed;
			lock (_contentLock) { removed = _gutterRenderers.Remove(renderer); }
			if (removed)
			{
				InvalidateWrappedLinesCache();
				Container?.Invalidate(true);
			}
			return removed;
		}

		/// <summary>
		/// Removes all gutter renderers.
		/// Also clears the built-in line number renderer if present.
		/// </summary>
		public void ClearGutterRenderers()
		{
			lock (_contentLock)
			{
				_gutterRenderers.Clear();
				if (_builtInLineNumberRenderer != null)
				{
					_builtInLineNumberRenderer = null;
					_showLineNumbers = false;
				}
			}
			InvalidateWrappedLinesCache();
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Gets or sets whether editing mode hints are shown at the bottom-right of the viewport.
		/// When enabled, shows "Enter to edit" in browse mode and "Esc to stop editing" in editing mode.
		/// </summary>
		public bool ShowEditingHints
		{
			get => _showEditingHints;
			set
			{
				if (_showEditingHints == value) return;
				_showEditingHints = value;
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
				lock (_contentLock)
				{
					_syntaxHighlighter = value;
					_syntaxTokenCache = null;
					_lineStateCache = null;
				}
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		#endregion

		#region Dispose

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			// No additional cleanup needed; base clears Container.
		}

		#endregion
	}
}

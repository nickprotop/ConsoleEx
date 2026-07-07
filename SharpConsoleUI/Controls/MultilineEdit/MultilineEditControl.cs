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
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Size = SharpConsoleUI.Helpers.Size;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// The sub-region of a <see cref="MultilineEditControl"/> that owns a captured mouse gesture.
	/// </summary>
	internal enum MleGestureRegion
	{
		/// <summary>The text area (selection / caret placement / drag-select).</summary>
		Text,

		/// <summary>The vertical scrollbar column.</summary>
		VScrollbar,

		/// <summary>The horizontal scrollbar row.</summary>
		HScrollbar,

		/// <summary>The gutter column (line numbers / breakpoints).</summary>
		Gutter
	}

	/// <summary>
	/// A multiline text editing control with support for text selection, scrolling, and word wrap.
	/// Provides full cursor navigation, cut/copy/paste-like operations, and configurable scrollbars.
	/// </summary>
	public partial class MultilineEditControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, ILogicalCursorProvider, ICursorShapeProvider, ISelectableControl, IPasteTarget, IDragAutoScrollTarget, IColorRoleableControl
	{

		#region ColorRole

		private ColorRole _role = ColorRole.Default;
		private ThemeMode? _colorRoleMode;
		private bool _outline;

		/// <inheritdoc/>
		public ColorRole ColorRole
		{
			get => _role;
			set => SetProperty(ref _role, value);
		}

		/// <inheritdoc/>
		public ThemeMode? ColorRoleMode
		{
			get => _colorRoleMode;
			set => SetProperty(ref _colorRoleMode, value);
		}

		/// <inheritdoc/>
		public bool Outline
		{
			get => _outline;
			set => SetProperty(ref _outline, value);
		}

		#endregion

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
		private int _lastDragRelativeY = 0;

		// Sub-region gesture ownership: a fresh Button1 press hit-tests one of the four regions (text /
		// vertical scrollbar / horizontal scrollbar / gutter) and captures it; every subsequent resent
		// press/drag routes to the captured region without re-hit-testing (SGR re-sends Button1Pressed on
		// motion). Replaces the former _isDragging / _gutterPressed / _isVerticalScrollbarDragging /
		// _isHorizontalScrollbarDragging flags.
		private readonly Helpers.MouseGestureCapture<MleGestureRegion> _gesture = new();

		// A thumb-drag is in progress only while the pointer started on the scrollbar thumb (not an arrow /
		// page-track press). Latched on the thumb-Down phase, cleared on Up.
		private bool _vThumbDragging = false;
		private bool _hThumbDragging = false;

		// Scrollbar drag state. _scrollbarInteracted is read OUTSIDE the mouse handler
		// (Rendering.cs suppresses the cursor-follow scroll update on the frame after a scrollbar
		// interaction; Keyboard.cs clears it on a keypress), so it is preserved.
		private bool _scrollbarInteracted = false;
		private int _verticalScrollbarDragStartY = 0;
		private int _verticalScrollbarDragStartOffset = 0;
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

		// Per-line background highlights (source line index → color)
		private Dictionary<int, Color> _lineHighlights = new();

		// Visible whitespace
		private bool _showWhitespace;

		// Gutter renderers
		private readonly List<IGutterRenderer> _gutterRenderers = new();
		private LineNumberGutterRenderer? _builtInLineNumberRenderer;
		// Total gutter width recorded at the last paint; the gutter-renderer self-invalidation handler
		// compares the post-mutation GetGutterWidth() against this to decide Relayout vs Repaint. -1 = never painted.
		private int _lastKnownGutterWidth = -1;

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

		/// <summary>Async counterpart of <see cref="ContentChanged"/>.</summary>
		public event Core.AsyncEventHandler<string>? ContentChangedAsync;

		/// <summary>
		/// Raises content-change notifications: <see cref="System.ComponentModel.INotifyPropertyChanged"/>
		/// for <see cref="Content"/> (so data binding to <c>Content</c> sees both programmatic and
		/// interactive edits), then the <see cref="ContentChanged"/> / <see cref="ContentChangedAsync"/> events.
		/// </summary>
		private void RaiseContentChanged()
		{
			OnPropertyChanged(nameof(Content));
			Core.AsyncEvent.Raise(ContentChanged, ContentChangedAsync, this, GetContent(), Container?.GetConsoleWindowSystem?.LogService);
		}

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
		/// Gets whether this editor currently has an active text selection.
		/// Part of <see cref="ISelectableControl"/> participation in the window's selection coordinator.
		/// </summary>
		public bool HasSelection => _hasSelection;

		/// <summary>
		/// Registers this control as the owner of the window's active selection, clearing any
		/// selection held by another selectable control (single-selection per window).
		/// Called whenever this editor gains or extends a selection.
		/// </summary>
		private void NotifySelectionActive()
		{
			if (_hasSelection)
				this.GetParentWindow()?.SelectionManager.SetActiveSelection(this);
		}

		/// <summary>
		/// Occurs when the editing mode changes (entering or leaving edit mode).
		/// Event argument is true when entering edit mode, false when leaving.
		/// </summary>
		public event EventHandler<bool>? EditingModeChanged;

		/// <summary>Async counterpart of <see cref="EditingModeChanged"/>.</summary>
		public event Core.AsyncEventHandler<bool>? EditingModeChangedAsync;

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

		/// <summary>Async counterpart of <see cref="OverwriteModeChanged"/>.</summary>
		public event Core.AsyncEventHandler<bool>? OverwriteModeChangedAsync;

		/// <summary>
		/// Occurs when the user clicks on the gutter area (left of the text).
		/// </summary>
		public event EventHandler<GutterClickEventArgs>? GutterClick;

		/// <summary>Async counterpart of <see cref="GutterClick"/>.</summary>
		public event Core.AsyncEventHandler<GutterClickEventArgs>? GutterClickAsync;

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
					int lineWidth = UnicodeWidth.GetStringWidth(line);
					if (lineWidth > maxLength) maxLength = lineWidth;
				}
				return maxLength + Margin.Left + Margin.Right;
			}
		}

		/// <summary>
		/// Gets or sets the background color when the control is not focused.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set
			{
				_backgroundColorValue = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		// Container is inherited from BaseControl — no override needed.

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
		public Color? FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue;
			set
			{
				_focusedBackgroundColorValue = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color when the control is focused.
		/// </summary>
		public Color FocusedForegroundColor
		{
			get => _focusedForegroundColorValue
				?? ColorResolver.ColorRoleTextOnBackground(ColorRole, Container, Outline, ColorRoleState.Focused, mode: ColorRoleMode)
				?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedForegroundColor ?? Color.White;
			set
			{
				_focusedForegroundColorValue = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color when the control is not focused.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue
				?? ColorResolver.ColorRoleTextOnBackground(ColorRole, Container, Outline, CurrentRoleState, mode: ColorRoleMode)
				?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => ComputeHasFocus();
		}

		/// <summary>
		/// When in edit mode and not read-only, Tab/Shift+Tab are handled by the editor
		/// (indent/dedent) instead of being intercepted for focus traversal.
		/// </summary>
		public bool WantsTabKey => _isEditing && !_readOnly;

		/// <summary>
		/// Gets or sets when the horizontal scrollbar is displayed.
		/// </summary>
		public ScrollbarVisibility HorizontalScrollbarVisibility
		{
			get => _horizontalScrollbarVisibility;
			set
			{
				_horizontalScrollbarVisibility = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Relayout);
			}
		}

		/// <summary>
		/// When true, pressing Escape will not exit editing mode.
		/// Useful for IDE-style editors where Escape is used for other purposes (e.g. dismissing popups).
		/// </summary>
		public bool EscapeExitsEditMode { get; set; } = true;

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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
				Core.AsyncEvent.Raise(EditingModeChanged, EditingModeChangedAsync, this, _isEditing, Container?.GetConsoleWindowSystem?.LogService);
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
			set => SetProperty(ref _isEnabled, value);
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>
		/// Gets or sets the scrollbar track color.
		/// </summary>
		public Color ScrollbarColor
		{
			get => _scrollbarColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ScrollbarTrackColor ?? Color.Grey23;
			set
			{
				_scrollbarColorValue = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>
		/// Gets or sets the scrollbar thumb (handle) color.
		/// </summary>
		public Color ScrollbarThumbColor
		{
			get => _scrollbarThumbColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ScrollbarThumbColor ?? Color.Cyan1;
			set
			{
				_scrollbarThumbColorValue = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
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
				OnPropertyChanged();
				Invalidate(Invalidation.Relayout);
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
					OnPropertyChanged();
					Invalidate(Invalidation.Relayout);
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
				OnPropertyChanged();
				InvalidateWrappedLinesCache();
				Invalidate(Invalidation.Relayout);
			}
		}

		/// <summary>
		/// Gets or sets the number of spaces used for tab indentation.
		/// </summary>
		public int TabSize
		{
			get => _tabSize;
			set => SetProperty(ref _tabSize, value, v => Math.Clamp(v, 1, ControlDefaults.MaxTabSize));
		}

		/// <summary>
		/// Gets or sets the maximum number of undo actions retained.
		/// </summary>
		public int UndoLimit
		{
			get => _undoLimit;
			set { _undoLimit = Math.Max(1, value); OnPropertyChanged(); }
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>
		/// Gets or sets the maximum total character length of the content.
		/// Null means no limit. Enforced on all insertion operations.
		/// </summary>
		public int? MaxLength
		{
			get => _maxLength;
			set { _maxLength = value.HasValue ? Math.Max(0, value.Value) : null; OnPropertyChanged(); }
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
				Core.AsyncEvent.Raise(OverwriteModeChanged, OverwriteModeChangedAsync, this, _overwriteMode, Container?.GetConsoleWindowSystem?.LogService);
			}
		}

		/// <summary>
		/// Gets or sets whether auto-indent is enabled.
		/// When enabled, pressing Enter copies leading whitespace from the current line to the new line.
		/// </summary>
		public bool AutoIndent
		{
			get => _autoIndent;
			set { _autoIndent = value; OnPropertyChanged(); }
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
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
				OnPropertyChanged();

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
				Invalidate(Invalidation.Relayout);
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
				OnPropertyChanged();
				if (_builtInLineNumberRenderer != null)
					_builtInLineNumberRenderer.LineNumberColor = value;
				Invalidate(Invalidation.Repaint);
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
			SubscribeGutterRenderer(renderer);
			InvalidateWrappedLinesCache();
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Inserts a gutter renderer at the specified index.
		/// </summary>
		public void InsertGutterRenderer(int index, IGutterRenderer renderer)
		{
			lock (_contentLock) { _gutterRenderers.Insert(index, renderer); }
			SubscribeGutterRenderer(renderer);
			InvalidateWrappedLinesCache();
			Invalidate(Invalidation.Relayout);
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
				UnsubscribeGutterRenderer(renderer);
				InvalidateWrappedLinesCache();
				Invalidate(Invalidation.Relayout);
			}
			return removed;
		}

		/// <summary>
		/// Removes all gutter renderers.
		/// Also clears the built-in line number renderer if present.
		/// </summary>
		public void ClearGutterRenderers()
		{
			List<IGutterRenderer> removed;
			lock (_contentLock)
			{
				removed = _gutterRenderers.ToList();
				_gutterRenderers.Clear();
				if (_builtInLineNumberRenderer != null)
				{
					_builtInLineNumberRenderer = null;
					_showLineNumbers = false;
				}
			}
			foreach (var r in removed)
				UnsubscribeGutterRenderer(r);
			InvalidateWrappedLinesCache();
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Subscribes to a renderer's <see cref="IGutterRenderer.Invalidated"/> event so that runtime state
		/// changes in the renderer self-invalidate this editor. A renderer that never raises the event
		/// (e.g. the built-in line-number renderer) is unaffected.
		/// </summary>
		private void SubscribeGutterRenderer(IGutterRenderer renderer)
			=> renderer.Invalidated += OnGutterRendererInvalidated;

		private void UnsubscribeGutterRenderer(IGutterRenderer renderer)
			=> renderer.Invalidated -= OnGutterRendererInvalidated;

		/// <summary>
		/// Handles a self-invalidation from an attached gutter renderer. The renderer only signals that its
		/// state changed; THIS editor derives the level from the single source of truth — the total gutter
		/// width (<see cref="GetGutterWidth"/>, the sum of every renderer's <see cref="IGutterRenderer.GetWidth"/>).
		/// If the gutter width changed, the text reflows (Relayout + wrapped-lines cache invalidation);
		/// otherwise it is a paint-only redraw (Repaint).
		/// </summary>
		private void OnGutterRendererInvalidated(object? sender, EventArgs e)
		{
			int newWidth = GetGutterWidth();
			if (newWidth != _lastKnownGutterWidth)
			{
				_lastKnownGutterWidth = newWidth;
				InvalidateWrappedLinesCache();
				Invalidate(Invalidation.Relayout);
			}
			else
			{
				Invalidate(Invalidation.Repaint);
			}
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
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
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <summary>
		/// Computes the current role state from the editor's enabled/focus state so role colours
		/// reflect the same visual state the renderer paints.
		/// </summary>
		private ColorRoleState CurrentRoleState =>
			!_isEnabled ? ColorRoleState.Disabled : (ComputeHasFocus() ? ColorRoleState.Focused : ColorRoleState.Normal);

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <summary>
		/// Gets or sets per-line background highlights keyed by 0-based source line index.
		/// </summary>
		public Dictionary<int, Color> LineHighlights
		{
			get => _lineHighlights;
			set
			{
				_lineHighlights = value ?? new();
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>
		/// Sets or clears a background highlight for a specific source line.
		/// Pass null to clear the highlight for that line.
		/// </summary>
		public void SetLineHighlight(int sourceLineIndex, Color? color)
		{
			if (color.HasValue)
				_lineHighlights[sourceLineIndex] = color.Value;
			else
				_lineHighlights.Remove(sourceLineIndex);
			Invalidate(Invalidation.Repaint);
		}

		/// <summary>
		/// Clears all per-line background highlights.
		/// </summary>
		public void ClearLineHighlights()
		{
			_lineHighlights.Clear();
			Invalidate(Invalidation.Repaint);
		}

		#endregion

		#region Dispose

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			// Abandon any captured mouse gesture so a half-finished drag/thumb interaction cannot
			// linger past disposal.
			_gesture.Reset();
			_vThumbDragging = false;
			_hThumbDragging = false;
			// No additional cleanup needed; base clears Container.
		}

		#endregion

		#region IDragAutoScrollTarget

		/// <inheritdoc/>
		bool IDragAutoScrollTarget.IsDragSelecting => _gesture.CapturedRegion == MleGestureRegion.Text;

		/// <inheritdoc/>
		bool IDragAutoScrollTarget.IsViewportReady => GetEffectiveViewportHeight() > 0;

		/// <inheritdoc/>
		int IDragAutoScrollTarget.LastDragRelativeY => _lastDragRelativeY;

		/// <inheritdoc/>
		int IDragAutoScrollTarget.ViewportHeightRows => GetEffectiveViewportHeight();

		/// <inheritdoc/>
		void IDragAutoScrollTarget.AutoScrollStep(int rows)
		{
			if (rows == 0) return;
			int max = Math.Max(0, GetTotalWrappedLineCount() - GetEffectiveViewportHeight());
			_verticalScrollOffset = Math.Clamp(_verticalScrollOffset + rows, 0, max);
			Invalidate(Invalidation.Relayout);
		}

		/// <inheritdoc/>
		void IDragAutoScrollTarget.ExtendSelectionToRevealedEdge(int direction)
		{
			int evh = GetEffectiveViewportHeight();
			int revealed = direction < 0 ? _verticalScrollOffset : _verticalScrollOffset + evh - 1;
			lock (_contentLock)
			{
				int maxLine = Math.Max(0, _lines.Count - 1);
				revealed = Math.Clamp(revealed, 0, maxLine);
				_selectionEndY = revealed;
				_selectionEndX = _lines[revealed].Length;
				_hasSelection = (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY);
			}
			Invalidate(Invalidation.Relayout);
		}

		#endregion
	}
}

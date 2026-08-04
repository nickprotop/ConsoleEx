// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Drawing;
using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A text input control with optional prompt text.
	/// <para>
	/// Single-line by default: it measures one row and scrolls horizontally when the text outgrows
	/// the field. Set <see cref="Multiline"/> to let it soft-wrap and grow between
	/// <see cref="MinRows"/> and <see cref="MaxRows"/> instead — the value stays one string either
	/// way, so callers binding to <see cref="Input"/> are unaffected by the mode.
	/// </para>
	/// <para>
	/// Split across partial files: <c>.Content</c> (the value, selection, history, clipboard),
	/// <c>.Keyboard</c> (the key table and completion), <c>.Mouse</c> (pointer and drag selection),
	/// and <c>.Rendering</c> (measure, paint, caret mapping).
	/// </para>
	/// </summary>
	public partial class PromptControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, ILogicalCursorProvider, ICursorShapeProvider, IPasteTarget, IColorRoleableControl
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

		/// <summary>
		/// Event fired when Enter is pressed (modern standardized event)
		/// </summary>
		public event EventHandler<string>? Entered;

		/// <summary>Async counterpart of <see cref="Entered"/>.</summary>
		public event Core.AsyncEventHandler<string>? EnteredAsync;

		/// <summary>Raises the <see cref="Entered"/> event for unit testing without simulating key input.</summary>
		internal void PerformEnterForTest()
			=> Core.AsyncEvent.Raise(Entered, EnteredAsync, this, _input, Container?.GetConsoleWindowSystem?.LogService);

		/// <summary>
		/// Sets the input text and raises <see cref="InputChanged"/> for unit testing without
		/// simulating key presses. Mirrors what the user typing then triggering a change would do.
		/// </summary>
		/// <param name="text">The text to set as the current input value.</param>
		internal void RaiseInputChangedForTest(string text)
		{
			_input = text ?? string.Empty;
			_cursorPosition = _input.Length;
			RaiseInputChanged();
		}

		/// <summary>
		/// Event fired when input text changes (modern standardized event)
		/// </summary>
		public event EventHandler<string>? InputChanged;

		/// <summary>
		/// Raises change notifications for the current input: the bespoke <see cref="InputChanged"/>
		/// event and <see cref="System.ComponentModel.INotifyPropertyChanged"/> for
		/// <see cref="Input"/> (so data binding to <c>Input</c> sees interactive edits, not just
		/// programmatic setter calls).
		/// </summary>
		private void RaiseInputChanged()
		{
			OnPropertyChanged(nameof(Input));
			InputChanged?.Invoke(this, _input);
		}

		private string _input = string.Empty;
		private Color? _inputBackgroundColor;
		private Color? _inputFocusedBackgroundColor;
		private Color? _inputFocusedForegroundColor;
		private Color? _inputForegroundColor;
		private int? _inputWidth;
		private string? _prompt;

		// Local edit state - controls own their edit state
		private int _cursorPosition = 0;
		private int _horizontalScrollOffset = 0;
		private char? _maskCharacter;

		// Cached alignment offset from last render (needed for cursor positioning)
		private int _lastAlignOffset = 0;

		// Auto-scroll: effective input width computed from render bounds
		private int _effectiveInputWidth;

		// History
		private bool _historyEnabled;
		private readonly List<string> _history = new();
		private int _historyIndex;

		// Tab completion
		private Func<string, int, IEnumerable<string>?>? _tabCompleter;

		// Selection: -1 means no selection active
		private int _selectionAnchor = -1;

		// Multiline state
		private bool _multiline;
		private int _minRows = 1;
		private int _maxRows = 6;
		private int _verticalScrollOffset;

		// Read-only helpers
		private int CurrentCursorPosition => _cursorPosition;
		private int CurrentScrollOffset => _horizontalScrollOffset;

		/// <summary>
		/// Gets the actual rendered width of the control content in characters.
		/// </summary>
		public override int? ContentWidth
		{
			get
			{
				int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);
				int inputLength = _inputWidth ?? UnicodeWidth.GetStringWidth(_input);
				return promptLength + inputLength + Margin.Left + Margin.Right;
			}
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => ComputeHasFocus();
		}

		/// <summary>
		/// Gets the preferred cursor shape - always VerticalBar for text input
		/// </summary>
		public CursorShape? PreferredCursorShape => CursorShape.VerticalBar;

		/// <summary>
		/// Gets or sets the background color of the input area when not focused.
		/// </summary>
		public Color? InputBackgroundColor
		{
			get => _inputBackgroundColor;
			set => SetProperty(ref _inputBackgroundColor, value);
		}

		/// <summary>
		/// Gets or sets the background color of the input area when focused.
		/// </summary>
		public Color? InputFocusedBackgroundColor
		{
			get => _inputFocusedBackgroundColor;
			set => SetProperty(ref _inputFocusedBackgroundColor, value);
		}

		/// <summary>
		/// Gets or sets the foreground color of the input text when focused.
		/// </summary>
		public Color? InputFocusedForegroundColor
		{
			get => _inputFocusedForegroundColor;
			set => SetProperty(ref _inputFocusedForegroundColor, value);
		}

		/// <summary>
		/// Gets or sets the foreground color of the input text when not focused.
		/// </summary>
		public Color? InputForegroundColor
		{
			get => _inputForegroundColor;
			set => SetProperty(ref _inputForegroundColor, value);
		}

		/// <summary>
		/// Gets or sets the width of the input area in characters. When set, enables horizontal scrolling.
		/// </summary>
		public int? InputWidth
		{
			get => _inputWidth;
			set => SetProperty(ref _inputWidth, value, v => v.HasValue ? Math.Max(1, v.Value) : v);
		}

		/// <summary>
		/// Gets or sets a character to display instead of the actual input (for password fields).
		/// When null, the actual input is displayed.
		/// </summary>
		public char? MaskCharacter
		{
			get => _maskCharacter;
			set => SetProperty(ref _maskCharacter, value);
		}

		/// <summary>
		/// Gets or sets the current input text entered by the user.
		/// </summary>
		public string Input
		{
			get => _input;
			set => SetInput(value);
		}

		private bool _isEnabled = true;
		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set { if (_isEnabled == value) return; _isEnabled = value; Invalidate(Invalidation.Repaint); }
		}

		private bool _readOnly;
		/// <summary>
		/// Gets or sets whether the text can be edited. A read-only prompt still takes focus, moves
		/// its cursor, selects and copies — it only refuses the operations that would change the
		/// value. Distinct from <see cref="IsEnabled"/>, which refuses everything and paints disabled.
		/// </summary>
		public bool ReadOnly
		{
			get => _readOnly;
			set => SetProperty(ref _readOnly, value);
		}

		/// <summary>
		/// Gets or sets the prompt text displayed before the input area.
		/// </summary>
		public string? Prompt
		{ get => _prompt; set => SetProperty(ref _prompt, value); }

		private string? _placeholder;
		/// <summary>
		/// Gets or sets text shown in place of an empty value, to say what belongs in the field.
		/// Rendered dimmed, and only while the value is empty; it is never part of <see cref="Input"/>
		/// and never submitted.
		/// </summary>
		public string? Placeholder
		{
			get => _placeholder;
			set => SetProperty(ref _placeholder, value);
		}

		private int? _maxLength;
		/// <summary>
		/// Gets or sets the maximum length of the value in characters, or null for unlimited.
		/// Enforced on typing, on paste (the pasted text is truncated to fit rather than rejected),
		/// and on <see cref="SetInput"/>. Counted the same way as
		/// <see cref="MultilineEditControl.MaxLength"/> — in characters, not display columns.
		/// </summary>
		public int? MaxLength
		{
			get => _maxLength;
			set => SetProperty(ref _maxLength, value, v => v.HasValue ? Math.Max(0, v.Value) : v);
		}

		/// <summary>
		/// Gets or sets whether the value may contain newlines and wrap across rows.
		/// <para>
		/// Defaults to <c>false</c>, which is the historical behaviour in full: one row, horizontal
		/// scrolling, and newlines flattened to spaces on both <see cref="SetInput"/> and
		/// <see cref="Paste"/>. Turning it on makes the control soft-wrap at its content width and
		/// measure between <see cref="MinRows"/> and <see cref="MaxRows"/> rows.
		/// </para>
		/// </summary>
		public bool Multiline
		{
			get => _multiline;
			set
			{
				if (_multiline == value) return;
				_multiline = value;
				// Leaving multiline mode cannot leave newlines in a control that no longer renders
				// them: flatten now rather than paint a value the layout has no rows for.
				if (!_multiline && _input.Contains('\n'))
				{
					_input = FlattenNewlines(_input);
					_cursorPosition = Math.Clamp(_cursorPosition, 0, _input.Length);
					ClearSelection();
					RaiseInputChanged();
				}
				_verticalScrollOffset = 0;
				InvalidateWrapCache();
				OnPropertyChanged();
				Invalidate(Invalidation.Relayout);
			}
		}

		/// <summary>
		/// Gets or sets the minimum number of rows the control measures when <see cref="Multiline"/>
		/// is set. Ignored in single-line mode, where the height is always one row.
		/// </summary>
		public int MinRows
		{
			get => _minRows;
			set => SetProperty(ref _minRows, value, v => Math.Max(1, v));
		}

		/// <summary>
		/// Gets or sets the maximum number of rows the control grows to when <see cref="Multiline"/>
		/// is set. Content taller than this scrolls vertically. Ignored in single-line mode.
		/// </summary>
		public int MaxRows
		{
			get => _maxRows;
			set => SetProperty(ref _maxRows, value, v => Math.Max(1, v));
		}

		private EnterBehavior _enterBehavior = EnterBehavior.Submit;
		/// <summary>
		/// Gets or sets what the Enter key does. Defaults to <see cref="EnterBehavior.Submit"/>, so
		/// Enter means the same thing it always has regardless of <see cref="Multiline"/>, and a
		/// command line becomes a wrapping command line by setting one flag.
		/// </summary>
		public EnterBehavior EnterBehavior
		{
			get => _enterBehavior;
			set => SetProperty(ref _enterBehavior, value);
		}

		/// <summary>
		/// Gets or sets whether the control loses focus when Enter is pressed.
		/// </summary>
		public bool UnfocusOnEnter { get; set; } = true;

		/// <summary>
		/// Gets or sets whether command history is enabled (Up/Down arrow recall).
		/// </summary>
		public bool HistoryEnabled
		{
			get => _historyEnabled;
			set => _historyEnabled = value;
		}

		private int _maxHistoryEntries = 500;
		/// <summary>
		/// Gets or sets how many entries the history keeps. The oldest are dropped past this bound,
		/// because a long-running command line would otherwise grow its history for the life of the
		/// process. Consecutive duplicates are not recorded at all.
		/// </summary>
		public int MaxHistoryEntries
		{
			get => _maxHistoryEntries;
			set
			{
				_maxHistoryEntries = Math.Max(1, value);
				TrimHistory();
			}
		}

		/// <summary>
		/// Gets or sets the tab completion delegate. When set, Tab key triggers completion.
		/// The delegate receives (input, cursorPosition) and returns completion candidates.
		/// When no completions match, Tab passes through to focus traversal.
		/// </summary>
		public Func<string, int, IEnumerable<string>?>? TabCompleter
		{
			get => _tabCompleter;
			set => _tabCompleter = value;
		}

		/// <summary>
		/// Whether this control wants Tab key events (for tab completion).
		/// </summary>
		public bool WantsTabKey => _tabCompleter != null;

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <summary>
		/// Computes the current role state from the prompt's enabled/focus state so role colours
		/// reflect the same visual state the renderer paints.
		/// </summary>
		private ColorRoleState CurrentRoleState =>
			!IsEnabled ? ColorRoleState.Disabled : (HasFocus ? ColorRoleState.Focused : ColorRoleState.Normal);

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
		}
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using System;
using System.Drawing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A single-line text input control with optional prompt text.
	/// Supports text editing, cursor navigation, and horizontal scrolling for overflow text.
	/// </summary>
	public class PromptControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, ILogicalCursorProvider, ICursorShapeProvider
	{
		/// <summary>
		/// Event fired when Enter is pressed (modern standardized event)
		/// </summary>
		public event EventHandler<string>? Entered;

		/// <summary>
		/// Event fired when input text changes (modern standardized event)
		/// </summary>
		public event EventHandler<string>? InputChanged;
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

		/// <inheritdoc/>
		public bool IsEnabled { get; set; } = true;

		/// <summary>
		/// Gets or sets the prompt text displayed before the input area.
		/// </summary>
		public string? Prompt
		{ get => _prompt; set => SetProperty(ref _prompt, value); }

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
		/// Clears the command history.
		/// </summary>
		public void ClearHistory() => _history.Clear();

		/// <summary>
		/// Gets the selected text, or null if no selection.
		/// </summary>
		public string? SelectedText
		{
			get
			{
				if (_selectionAnchor < 0) return null;
				int start = Math.Min(_selectionAnchor, _cursorPosition);
				int end = Math.Max(_selectionAnchor, _cursorPosition);
				if (start == end) return null;
				return _input.Substring(start, end - start);
			}
		}

		/// <summary>
		/// Gets whether there is an active text selection.
		/// </summary>
		public bool HasSelection => _selectionAnchor >= 0 && _selectionAnchor != _cursorPosition;

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
		}

		/// <inheritdoc/>
		public Point? GetLogicalCursorPosition()
		{
			// Only show cursor when control has focus
			if (!HasFocus)
			{
				return null;
			}

			// Return the visual cursor position within the input field
			// Account for scroll offset, margins, and alignment offset to get the position relative to visible content
			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);
			int visualCursorX = Margin.Left + _lastAlignOffset + promptLength + (CurrentCursorPosition - CurrentScrollOffset);
			var pos = new Point(visualCursorX, Margin.Top);

			return pos;
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			// Return the size of the prompt content (prompt + input area)
			string fullContent = (_prompt ?? string.Empty) + _input;
			int width = Math.Max(Parsing.MarkupParser.StripLength(fullContent), Width ?? 0);
			return new System.Drawing.Size(width, 1); // Single line control
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(Point position)
		{
			// Calculate cursor position within the input field (excluding prompt length)
			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);
			int inputCursorPos = Math.Max(0, position.X - promptLength);

			// Clamp to valid input range
			int newCursorPos = Math.Max(0, Math.Min(inputCursorPos, _input.Length));
			_cursorPosition = newCursorPos;

			// Update scroll offset if needed
			if (_inputWidth.HasValue)
			{
				int scrollOffset = CurrentScrollOffset;
				if (newCursorPos < scrollOffset)
				{
					SetScrollOffset(newCursorPos);
				}
				else if (newCursorPos >= scrollOffset + _inputWidth.Value)
				{
					SetScrollOffset(newCursorPos - _inputWidth.Value + 1);
				}
			}

			Container?.Invalidate(false, this);
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!IsEnabled) return false;

			int cursorPos = CurrentCursorPosition;
			int scrollOffset = CurrentScrollOffset;
			bool ctrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);
			bool shift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);

			// --- Ctrl combinations (readline-style) ---
			if (ctrl)
			{
				switch (key.Key)
				{
					case ConsoleKey.A: // Ctrl+A: select all
						_selectionAnchor = 0;
						MoveCursorTo(_input.Length);
						return true;

					case ConsoleKey.E: // Ctrl+E: cursor to end
						ClearSelection();
						MoveCursorTo(_input.Length);
						return true;

					case ConsoleKey.C: // Ctrl+C: copy selection
						if (HasSelection)
							ClipboardHelper.SetText(SelectedText!);
						return true;

					case ConsoleKey.V: // Ctrl+V: paste
					{
						var clip = ClipboardHelper.GetText();
						if (!string.IsNullOrEmpty(clip))
						{
							// Sanitize: single-line
							clip = clip.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
							DeleteSelection();
							_input = _input.Insert(_cursorPosition, clip);
							MoveCursorTo(_cursorPosition + clip.Length);
							InputChanged?.Invoke(this, _input);
						}
						return true;
					}

					case ConsoleKey.X: // Ctrl+X: cut
						if (HasSelection)
						{
							ClipboardHelper.SetText(SelectedText!);
							DeleteSelection();
							InputChanged?.Invoke(this, _input);
						}
						return true;

					case ConsoleKey.K: // Ctrl+K: kill from cursor to end
						if (cursorPos < _input.Length)
						{
							_input = _input.Substring(0, cursorPos);
							Container?.Invalidate(true);
							InputChanged?.Invoke(this, _input);
						}
						return true;

					case ConsoleKey.U: // Ctrl+U: kill from start to cursor
						if (cursorPos > 0)
						{
							_input = _input.Substring(cursorPos);
							MoveCursorTo(0);
							InputChanged?.Invoke(this, _input);
						}
						return true;

					case ConsoleKey.W: // Ctrl+W: kill word backward
						if (cursorPos > 0)
						{
							int wordStart = FindWordBoundaryLeft(cursorPos);
							_input = _input.Remove(wordStart, cursorPos - wordStart);
							MoveCursorTo(wordStart);
							InputChanged?.Invoke(this, _input);
						}
						return true;

					case ConsoleKey.LeftArrow: // Ctrl+Left: word left
						if (cursorPos > 0)
							MoveCursorTo(FindWordBoundaryLeft(cursorPos));
						return true;

					case ConsoleKey.RightArrow: // Ctrl+Right: word right
						if (cursorPos < _input.Length)
							MoveCursorTo(FindWordBoundaryRight(cursorPos));
						return true;
				}
			}

			// --- Standard keys ---
			if (key.Key == ConsoleKey.Enter)
			{
				if (_historyEnabled && !string.IsNullOrEmpty(_input))
				{
					_history.Add(_input);
					_historyIndex = _history.Count;
				}
				Entered?.Invoke(this, _input);
				if (UnfocusOnEnter)
				{
					_cursorPosition = 0;
					this.GetParentWindow()?.FocusManager.SetFocus(null, FocusReason.Keyboard);
				}
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.Backspace)
			{
				if (HasSelection)
				{
					DeleteSelection();
					InputChanged?.Invoke(this, _input);
					return true;
				}
				if (cursorPos > 0)
				{
					_input = _input.Remove(cursorPos - 1, 1);
					MoveCursorTo(cursorPos - 1);
					InputChanged?.Invoke(this, _input);
				}
				return true;
			}
			else if (key.Key == ConsoleKey.Delete)
			{
				if (HasSelection)
				{
					DeleteSelection();
					InputChanged?.Invoke(this, _input);
					return true;
				}
				if (cursorPos < _input.Length)
				{
					_input = _input.Remove(cursorPos, 1);
					Container?.Invalidate(true);
					InputChanged?.Invoke(this, _input);
				}
				return true;
			}
			else if (key.Key == ConsoleKey.Home)
			{
				if (shift) { if (_selectionAnchor < 0) _selectionAnchor = cursorPos; }
				else ClearSelection();
				MoveCursorTo(0);
				return true;
			}
			else if (key.Key == ConsoleKey.End)
			{
				if (shift) { if (_selectionAnchor < 0) _selectionAnchor = cursorPos; }
				else ClearSelection();
				MoveCursorTo(_input.Length);
				return true;
			}
			else if (key.Key == ConsoleKey.LeftArrow && cursorPos > 0)
			{
				if (shift) { if (_selectionAnchor < 0) _selectionAnchor = cursorPos; }
				else ClearSelection();
				MoveCursorTo(cursorPos - 1);
				return true;
			}
			else if (key.Key == ConsoleKey.RightArrow && cursorPos < _input.Length)
			{
				if (shift) { if (_selectionAnchor < 0) _selectionAnchor = cursorPos; }
				else ClearSelection();
				MoveCursorTo(cursorPos + 1);
				return true;
			}
			else if (key.Key == ConsoleKey.UpArrow && _historyEnabled && _historyIndex > 0)
			{
				_historyIndex--;
				_input = _history[_historyIndex];
				MoveCursorTo(_input.Length);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.DownArrow && _historyEnabled && _historyIndex < _history.Count)
			{
				_historyIndex++;
				_input = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
				MoveCursorTo(_input.Length);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.Tab && _tabCompleter != null)
			{
				var completions = _tabCompleter(_input, cursorPos)?.ToList();
				if (completions == null || completions.Count == 0)
					return false; // no matches — let focus leave

				if (completions.Count == 1)
				{
					if (completions[0] == _input)
						return false; // already complete — let focus leave
					_input = completions[0];
					MoveCursorTo(_input.Length);
					InputChanged?.Invoke(this, _input);
					return true;
				}

				// Multiple completions: find common prefix and insert it
				var prefix = CommonPrefix(completions);
				if (prefix.Length > _input.Length)
				{
					_input = prefix;
					MoveCursorTo(_input.Length);
					InputChanged?.Invoke(this, _input);
					return true;
				}

				// Common prefix didn't advance — can't complete further, let focus leave
				return false;
			}
			else if (key.Key == ConsoleKey.Escape)
			{
				this.GetParentWindow()?.FocusManager.SetFocus(null, FocusReason.Keyboard);
				Container?.Invalidate(true);
				return true;
			}
			else if (!char.IsControl(key.KeyChar))
			{
				if (HasSelection)
					DeleteSelection();
				cursorPos = _cursorPosition; // update after potential deletion
				_input = _input.Insert(cursorPos, key.KeyChar.ToString());
				ClearSelection();
				MoveCursorTo(cursorPos + 1);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Whether this control wants Tab key events (for tab completion).
		/// </summary>
		public bool WantsTabKey => _tabCompleter != null;

		/// <summary>
		/// Clears the current selection.
		/// </summary>
		private void ClearSelection()
		{
			_selectionAnchor = -1;
		}

		/// <summary>
		/// Deletes the selected text and positions the cursor at the selection start.
		/// </summary>
		private void DeleteSelection()
		{
			if (!HasSelection) return;
			int start = Math.Min(_selectionAnchor, _cursorPosition);
			int end = Math.Max(_selectionAnchor, _cursorPosition);
			_input = _input.Remove(start, end - start);
			_selectionAnchor = -1;
			MoveCursorTo(start);
		}

		/// <summary>
		/// Moves the cursor to the specified position and adjusts scroll.
		/// </summary>
		private void MoveCursorTo(int position)
		{
			position = Math.Clamp(position, 0, _input.Length);
			_cursorPosition = position;
			int effectiveWidth = _effectiveInputWidth > 0 ? _effectiveInputWidth : (_inputWidth ?? int.MaxValue);
			int scrollOffset = _horizontalScrollOffset;
			if (position < scrollOffset)
				SetScrollOffset(position);
			else if (position >= scrollOffset + effectiveWidth)
				SetScrollOffset(position - effectiveWidth + 1);
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Finds the start of the word to the left of the given position.
		/// </summary>
		private int FindWordBoundaryLeft(int pos)
		{
			if (pos <= 0) return 0;
			int i = pos - 1;
			// Skip whitespace
			while (i > 0 && char.IsWhiteSpace(_input[i])) i--;
			// Skip word characters
			while (i > 0 && !char.IsWhiteSpace(_input[i - 1])) i--;
			return i;
		}

		/// <summary>
		/// Finds the end of the word to the right of the given position.
		/// </summary>
		private int FindWordBoundaryRight(int pos)
		{
			if (pos >= _input.Length) return _input.Length;
			int i = pos;
			// Skip current word
			while (i < _input.Length && !char.IsWhiteSpace(_input[i])) i++;
			// Skip whitespace
			while (i < _input.Length && char.IsWhiteSpace(_input[i])) i++;
			return i;
		}

		/// <summary>
		/// Finds the longest common prefix among a list of strings.
		/// </summary>
		private static string CommonPrefix(List<string> strings)
		{
			if (strings.Count == 0) return string.Empty;
			var prefix = strings[0];
			for (int i = 1; i < strings.Count; i++)
			{
				int j = 0;
				while (j < prefix.Length && j < strings[i].Length && prefix[j] == strings[i][j]) j++;
				prefix = prefix.Substring(0, j);
			}
			return prefix;
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <summary>
		/// Sets the input text and positions the cursor at the end.
		/// </summary>
		/// <param name="input">The text to set as input.</param>
		public void SetInput(string? input)
		{
			// Sanitize: single-line control — collapse newlines to spaces
			if (input != null && input.Contains('\n'))
				input = input.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

			int newCursorPos = string.IsNullOrEmpty(input) ? 0 : input.Length;

			_input = input ?? string.Empty;

			// Set cursor and scroll via services (single source of truth)
			_cursorPosition = newCursorPos;
			_horizontalScrollOffset = 0;

			Container?.Invalidate(true);
			InputChanged?.Invoke(this, _input);
		}

		private void SetScrollOffset(int value)
		{
			int newOffset = Math.Max(0, value);
			// Set scroll position via service (single source of truth)
			_horizontalScrollOffset = newOffset;
		}

		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool WantsMouseEvents => true;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => CanReceiveFocus;

		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseClick;
		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseDoubleClick;
		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseRightClick;
		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseEnter;
		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseLeave;
		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseMove;

		/// <inheritdoc/>
		public bool ProcessMouseEvent(Events.MouseEventArgs args)
		{
			if (!IsEnabled) return false;

			// Focus on click
			if (args.HasFlag(Drivers.MouseFlags.Button1Clicked) ||
			    args.HasFlag(Drivers.MouseFlags.Button1Pressed))
			{
				if (!HasFocus && CanFocusWithMouse)
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);

				// Position cursor at clicked character
				int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);
				int clickX = args.Position.X - Margin.Left - _lastAlignOffset - promptLength;
				int charPos = clickX + _horizontalScrollOffset;
				charPos = Math.Clamp(charPos, 0, _input.Length);
				_cursorPosition = charPos;
				Container?.Invalidate(true);
				args.Handled = true;
				return true;
			}

			return false;
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);
			// Cap measured input width to available space — the control scrolls when text overflows
			int naturalInputWidth = Math.Max(UnicodeWidth.GetStringWidth(_input), 10);
			int inputFieldWidth = _inputWidth ?? Math.Min(naturalInputWidth, Math.Max(10, constraints.MaxWidth - promptLength - Margin.Left - Margin.Right));
			int contentWidth = promptLength + inputFieldWidth;
			int width = (Width ?? contentWidth) + Margin.Left + Margin.Right;
			int height = 1 + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			var bgColor = Container?.BackgroundColor ?? defaultBg;
			var fgColor = Container?.ForegroundColor ?? defaultFg;
			var effectiveBg = Color.Transparent;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;

			if (targetWidth <= 0) return;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);

			// Render the prompt line
			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (Margin.Left > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, startY, Margin.Left, 1), fgColor, effectiveBg);
				}

				// Calculate colors
				Color inputBackgroundColor = HasFocus
					? ColorResolver.Coalesce(InputFocusedBackgroundColor)
						?? ColorResolver.Coalesce(Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedBackgroundColor)
						?? Color.Transparent
					: ColorResolver.Coalesce(InputBackgroundColor)
						?? ColorResolver.Coalesce(Container?.GetConsoleWindowSystem?.Theme?.PromptInputBackgroundColor)
						?? Color.Transparent;
				Color inputForegroundColor = HasFocus
					? InputFocusedForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedForegroundColor ?? Color.Black
					: InputForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputForegroundColor ?? Color.White;

				int currentX = startX;
				int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);

				// Calculate alignment offset
				int inputFieldWidth = _inputWidth ?? (targetWidth - promptLength);
				_effectiveInputWidth = Math.Max(1, inputFieldWidth); // cache for scroll calculations
				int totalContentWidth = promptLength + inputFieldWidth;
				int alignOffset = 0;
				if (totalContentWidth < targetWidth)
				{
					switch (HorizontalAlignment)
					{
						case HorizontalAlignment.Center:
							alignOffset = (targetWidth - totalContentWidth) / 2;
							break;
						case HorizontalAlignment.Right:
							alignOffset = targetWidth - totalContentWidth;
							break;
					}
				}

				// Cache alignment offset for cursor positioning
				_lastAlignOffset = alignOffset;

				// Fill left alignment padding
				if (alignOffset > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX, startY, alignOffset, 1), fgColor, effectiveBg);
					currentX += alignOffset;
				}

				// Render prompt text (if any)
				if (!string.IsNullOrEmpty(_prompt))
				{
					var promptCells = Parsing.MarkupParser.Parse(_prompt, fgColor, bgColor);
					buffer.WriteCellsClipped(currentX, startY, promptCells, clipRect);
					currentX += promptLength;
				}

				// Calculate visible input with scroll offset
				int scrollOffset = CurrentScrollOffset;
				string visibleInput = _input;
				if (_inputWidth.HasValue && _input.Length > _inputWidth.Value)
				{
					int maxLength = Math.Min(_inputWidth.Value, _input.Length - scrollOffset);
					if (maxLength > 0 && scrollOffset < _input.Length)
					{
						visibleInput = _input.Substring(scrollOffset, maxLength);
					}
					else
					{
						visibleInput = string.Empty;
					}
				}
				else if (scrollOffset > 0 && scrollOffset < _input.Length)
				{
					visibleInput = _input.Substring(scrollOffset);
				}
				else if (scrollOffset >= _input.Length)
				{
					visibleInput = string.Empty;
				}

				// Render input field with background color
				int remainingWidth = bounds.Right - currentX - Margin.Right;
				int inputDisplayWidth = _inputWidth ?? Math.Max(remainingWidth, 0);
				inputDisplayWidth = Math.Min(inputDisplayWidth, remainingWidth);

				// Write the visible input text using Unicode-aware rendering
				// Escape markup in user input to prevent [ ] from being parsed as tags
				string displayInput = _maskCharacter.HasValue
					? new string(_maskCharacter.Value, UnicodeWidth.GetStringWidth(visibleInput))
					: Parsing.MarkupParser.Escape(visibleInput);
				var inputCells = Parsing.MarkupParser.Parse(displayInput, inputForegroundColor, inputBackgroundColor);
				int visibleDisplayWidth = inputCells.Count;

				// Clamp to inputDisplayWidth and write cells
				int cellsToWrite = Math.Min(visibleDisplayWidth, inputDisplayWidth);
				for (int i = 0; i < cellsToWrite; i++)
				{
					int x = currentX + i;
					if (x >= clipRect.X && x < clipRect.Right)
					{
						buffer.SetCell(x, startY, inputCells[i]);
					}
				}

				// Highlight selection (invert colors)
				if (HasSelection)
				{
					int selStart = Math.Min(_selectionAnchor, _cursorPosition);
					int selEnd = Math.Max(_selectionAnchor, _cursorPosition);
					int visStart = Math.Max(selStart - scrollOffset, 0);
					int visEnd = Math.Min(selEnd - scrollOffset, cellsToWrite);
					for (int i = visStart; i < visEnd; i++)
					{
						int x = currentX + i;
						if (x >= clipRect.X && x < clipRect.Right)
						{
							var cell = buffer.GetCell(x, startY);
							buffer.SetCellColors(x, startY, cell.Background, cell.Foreground);
						}
					}
				}

				// Fill remaining input field with background color
				int inputEndX = currentX + cellsToWrite;
				int fillWidth = inputDisplayWidth - cellsToWrite;
				if (fillWidth > 0 && inputEndX < bounds.Right - Margin.Right)
				{
					for (int i = 0; i < fillWidth; i++)
					{
						int x = inputEndX + i;
						if (x >= clipRect.X && x < clipRect.Right && x < bounds.Right - Margin.Right)
						{
							buffer.SetNarrowCell(x, startY, ' ', inputForegroundColor, inputBackgroundColor);
						}
					}
				}

				// Fill right padding (after input field, before margin)
				int rightPadStart = currentX + inputDisplayWidth;
				int rightPadWidth = bounds.Right - rightPadStart - Margin.Right;
				if (rightPadWidth > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightPadStart, startY, rightPadWidth, 1), fgColor, effectiveBg);
				}

				// Fill right margin
				if (Margin.Right > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, startY, Margin.Right, 1), fgColor, effectiveBg);
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + 1, fgColor, effectiveBg);
		}

		#endregion
	}
}

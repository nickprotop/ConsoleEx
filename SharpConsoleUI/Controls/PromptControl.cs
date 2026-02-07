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
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using System;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A single-line text input control with optional prompt text.
	/// Supports text editing, cursor navigation, and horizontal scrolling for overflow text.
	/// </summary>
	public class PromptControl : IWindowControl, IInteractiveControl, IFocusableControl, ILogicalCursorProvider, ICursorShapeProvider, IDOMPaintable
	{
		/// <summary>
		/// Event fired when Enter is pressed (modern standardized event)
		/// </summary>
		public event EventHandler<string>? Entered;

		/// <summary>
		/// Event fired when input text changes (modern standardized event)
		/// </summary>
		public event EventHandler<string>? InputChanged;
		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;
		private string _input = string.Empty;
		private Color? _inputBackgroundColor;
		private Color? _inputFocusedBackgroundColor;
		private Color? _inputFocusedForegroundColor;
		private Color? _inputForegroundColor;
		private int? _inputWidth;
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private string? _prompt;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		// Local edit state - controls own their edit state
		private int _cursorPosition = 0;
		private int _horizontalScrollOffset = 0;
		private char? _maskCharacter;

		// Cached alignment offset from last render (needed for cursor positioning)
		private int _lastAlignOffset = 0;

		// Read-only helpers
		private int CurrentCursorPosition => _cursorPosition;
		private int CurrentScrollOffset => _horizontalScrollOffset;

		/// <summary>
		/// Gets the actual rendered width of the control content in characters.
		/// </summary>
		public int? ContentWidth
		{
			get
			{
				int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty);
				int inputLength = _inputWidth ?? _input.Length;
				return promptLength + inputLength + _margin.Left + _margin.Right;
			}
		}

		public int ActualX => _actualX;
		public int ActualY => _actualY;
		public int ActualWidth => _actualWidth;
		public int ActualHeight => _actualHeight;

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{ get => _horizontalAlignment; set { _horizontalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{ get => _verticalAlignment; set { _verticalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public IContainer? Container { get; set; }
		
		private bool _hasFocus;

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				var hadFocus = _hasFocus;
				_hasFocus = value;

				// Fire focus events
				if (value && !hadFocus)
				{
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
				else if (!value && hadFocus)
				{
					LostFocus?.Invoke(this, EventArgs.Empty);
				}

				Container?.Invalidate(true);
			}
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
			set
			{
				_inputBackgroundColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color of the input area when focused.
		/// </summary>
		public Color? InputFocusedBackgroundColor
		{
			get => _inputFocusedBackgroundColor;
			set
			{
				_inputFocusedBackgroundColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the input text when focused.
		/// </summary>
		public Color? InputFocusedForegroundColor
		{
			get => _inputFocusedForegroundColor;
			set
			{
				_inputFocusedForegroundColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the input text when not focused.
		/// </summary>
		public Color? InputForegroundColor
		{
			get => _inputForegroundColor;
			set
			{
				_inputForegroundColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the width of the input area in characters. When set, enables horizontal scrolling.
		/// </summary>
		public int? InputWidth
		{
			get => _inputWidth;
			set
			{
				_inputWidth = value.HasValue ? Math.Max(1, value.Value) : value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets a character to display instead of the actual input (for password fields).
		/// When null, the actual input is displayed.
		/// </summary>
		public char? MaskCharacter
		{
			get => _maskCharacter;
			set { _maskCharacter = value; Container?.Invalidate(true); }
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

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
		}



		/// <summary>
		/// Gets or sets the prompt text displayed before the input area.
		/// </summary>
		public string? Prompt
		{ get => _prompt; set { _prompt = value; Container?.Invalidate(true); } }

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
		/// Gets or sets whether the control loses focus when Enter is pressed.
		/// </summary>
		public bool UnfocusOnEnter { get; set; } = true;

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set => PropertySetterHelper.SetDimensionProperty(ref _width, value, Container);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Container = null;
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
			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty);
			int visualCursorX = _margin.Left + _lastAlignOffset + promptLength + (CurrentCursorPosition - CurrentScrollOffset);
			var pos = new Point(visualCursorX, _margin.Top);

			return pos;
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			// Return the size of the prompt content (prompt + input area)
			string fullContent = (_prompt ?? string.Empty) + _input;
			int width = Math.Max(AnsiConsoleHelper.StripSpectreLength(fullContent), _width ?? 0);
			return new System.Drawing.Size(width, 1); // Single line control
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(Point position)
		{
			// Calculate cursor position within the input field (excluding prompt length)
			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty);
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
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			int cursorPos = CurrentCursorPosition;
			int scrollOffset = CurrentScrollOffset;

			if (key.Key == ConsoleKey.Enter)
			{
				Entered?.Invoke(this, _input);
				if (UnfocusOnEnter)
				{
					_cursorPosition = 0;
					HasFocus = false;
				}
				Container?.Invalidate(true);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.Backspace && cursorPos > 0)
			{
				_input = _input.Remove(cursorPos - 1, 1);
				int newCursorPos = cursorPos - 1;
				_cursorPosition = newCursorPos;
				if (newCursorPos < scrollOffset + (_inputWidth ?? _input.Length))
				{
					SetScrollOffset(scrollOffset - 1);
				}
				Container?.Invalidate(true);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.Delete && cursorPos < _input.Length)
			{
				_input = _input.Remove(cursorPos, 1);
				Container?.Invalidate(true);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.Home)
			{
				_cursorPosition = 0;
				SetScrollOffset(0);
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.End)
			{
				_cursorPosition = _input.Length;
				SetScrollOffset(Math.Max(0, _input.Length - (_inputWidth ?? _input.Length)));
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.LeftArrow && cursorPos > 0)
			{
				int newCursorPos = cursorPos - 1;
				_cursorPosition = newCursorPos;
				if (newCursorPos < scrollOffset + (_inputWidth ?? _input.Length))
				{
					SetScrollOffset(scrollOffset - 1);
				}
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.RightArrow && cursorPos < _input.Length)
			{
				int newCursorPos = cursorPos + 1;
				_cursorPosition = newCursorPos;
				if (newCursorPos >= (scrollOffset + (_inputWidth ?? _input.Length)))
				{
					SetScrollOffset(scrollOffset + 1);
				}
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.Escape)
			{
				HasFocus = false;
				Container?.Invalidate(true);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			else if (!char.IsControl(key.KeyChar))
			{
				_input = _input.Insert(cursorPos, key.KeyChar.ToString());
				int newCursorPos = cursorPos + 1;
				_cursorPosition = newCursorPos;
				if (_inputWidth.HasValue && newCursorPos > _inputWidth.Value)
				{
					SetScrollOffset(newCursorPos - _inputWidth.Value);
				}
				Container?.Invalidate(true);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			return false;
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;
		
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
			bool hadFocus = HasFocus;
			HasFocus = focus;

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		/// <summary>
		/// Sets the input text and positions the cursor at the end.
		/// </summary>
		/// <param name="input">The text to set as input.</param>
		public void SetInput(string? input)
		{
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

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty);
			int inputFieldWidth = _inputWidth ?? Math.Max(_input.Length, 10);
			int contentWidth = promptLength + inputFieldWidth;
			int width = (_width ?? contentWidth) + _margin.Left + _margin.Right;
			int height = 1 + _margin.Top + _margin.Bottom;

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

			var bgColor = Container?.BackgroundColor ?? defaultBg;
			var fgColor = Container?.ForegroundColor ?? defaultFg;
			int targetWidth = bounds.Width - _margin.Left - _margin.Right;

			if (targetWidth <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			// Render the prompt line
			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (_margin.Left > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.X, startY, _margin.Left, 1), ' ', fgColor, bgColor);
				}

				// Calculate colors
				Color inputBackgroundColor = HasFocus
					? InputFocusedBackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedBackgroundColor ?? Color.White
					: InputBackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputBackgroundColor ?? Color.Black;
				Color inputForegroundColor = HasFocus
					? InputFocusedForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedForegroundColor ?? Color.Black
					: InputForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputForegroundColor ?? Color.White;

				int currentX = startX;
				int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty);

				// Calculate alignment offset
				int inputFieldWidth = _inputWidth ?? (targetWidth - promptLength);
				int totalContentWidth = promptLength + inputFieldWidth;
				int alignOffset = 0;
				if (totalContentWidth < targetWidth)
				{
					switch (_horizontalAlignment)
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
					buffer.FillRect(new LayoutRect(startX, startY, alignOffset, 1), ' ', fgColor, bgColor);
					currentX += alignOffset;
				}

				// Render prompt text (if any)
				if (!string.IsNullOrEmpty(_prompt))
				{
					var promptAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(_prompt, promptLength, 1, false, bgColor, fgColor).FirstOrDefault() ?? string.Empty;
					var promptCells = AnsiParser.Parse(promptAnsi, fgColor, bgColor);
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
				int remainingWidth = bounds.Right - currentX - _margin.Right;
				int inputDisplayWidth = _inputWidth ?? Math.Max(remainingWidth, 0);
				inputDisplayWidth = Math.Min(inputDisplayWidth, remainingWidth);

				// Write the visible input text
				for (int i = 0; i < visibleInput.Length && i < inputDisplayWidth; i++)
				{
					int x = currentX + i;
					if (x >= clipRect.X && x < clipRect.Right)
					{
						char displayChar = _maskCharacter ?? visibleInput[i];
						buffer.SetCell(x, startY, displayChar, inputForegroundColor, inputBackgroundColor);
					}
				}

				// Fill remaining input field with background color
				int inputEndX = currentX + visibleInput.Length;
				int fillWidth = inputDisplayWidth - visibleInput.Length;
				if (fillWidth > 0 && inputEndX < bounds.Right - _margin.Right)
				{
					for (int i = 0; i < fillWidth; i++)
					{
						int x = inputEndX + i;
						if (x >= clipRect.X && x < clipRect.Right && x < bounds.Right - _margin.Right)
						{
							buffer.SetCell(x, startY, ' ', inputForegroundColor, inputBackgroundColor);
						}
					}
				}

				// Fill right padding (after input field, before margin)
				int rightPadStart = currentX + inputDisplayWidth;
				int rightPadWidth = bounds.Right - rightPadStart - _margin.Right;
				if (rightPadWidth > 0)
				{
					buffer.FillRect(new LayoutRect(rightPadStart, startY, rightPadWidth, 1), ' ', fgColor, bgColor);
				}

				// Fill right margin
				if (_margin.Right > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, startY, _margin.Right, 1), ' ', fgColor, bgColor);
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + 1, fgColor, bgColor);
		}

		#endregion
	}
}
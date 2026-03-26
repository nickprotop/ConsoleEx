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
	public class PromptControl : BaseControl, IInteractiveControl, IFocusableControl, ILogicalCursorProvider, ICursorShapeProvider
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

			if (key.Key == ConsoleKey.Enter)
			{
				Entered?.Invoke(this, _input);
				if (UnfocusOnEnter)
				{
					_cursorPosition = 0;
					this.GetParentWindow()?.FocusManager.SetFocus(null, FocusReason.Keyboard);
				}
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.Backspace && cursorPos > 0)
			{
				_input = _input.Remove(cursorPos - 1, 1);
				int newCursorPos = cursorPos - 1;
				_cursorPosition = newCursorPos;
				if (newCursorPos < scrollOffset)
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
				if (newCursorPos < scrollOffset)
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
				this.GetParentWindow()?.FocusManager.SetFocus(null, FocusReason.Keyboard);
				Container?.Invalidate(true);
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
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);
			int inputFieldWidth = _inputWidth ?? Math.Max(UnicodeWidth.GetStringWidth(_input), 10);
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
				string displayInput = _maskCharacter.HasValue
					? new string(_maskCharacter.Value, UnicodeWidth.GetStringWidth(visibleInput))
					: visibleInput;
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

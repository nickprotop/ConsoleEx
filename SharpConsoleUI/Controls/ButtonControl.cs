// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using System;
using System.Drawing;

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Extensions;
#pragma warning disable CS1591

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Defines the border style for a button control.
	/// </summary>
	public enum ButtonBorderStyle
	{
		/// <summary>No border — text with space padding on each side.</summary>
		None,
		/// <summary>Pipe border — │ text │ on a single line.</summary>
		Pipe,
		/// <summary>Full box border — ┌──┐ / │text│ / └──┘ across 3 lines.</summary>
		Full
	}

	/// <summary>
	/// A clickable button control that supports keyboard and mouse interaction.
	/// </summary>
	public class ButtonControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		private bool _enabled = true;
		private bool _focused;
		private string _text = "Button";
		private ButtonBorderStyle _borderStyle = ButtonBorderStyle.None;
		private Color? _backgroundColor;
		private Color? _foregroundColor;
		private Color? _focusedBackgroundColor;
		private Color? _focusedForegroundColor;
		private Color? _disabledBackgroundColor;
		private Color? _disabledForegroundColor;
		private Color? _borderColor;

		/// <summary>
		/// Initializes a new instance of the ButtonControl class with default settings.
		/// </summary>
		public ButtonControl()
		{
		}

		/// <summary>
		/// Gets the actual rendered width of the button in characters.
		/// </summary>
		public override int? ContentWidth => GetButtonWidth() + Margin.Left + Margin.Right;

		private int GetButtonWidth()
		{
			int chrome = _borderStyle == ButtonBorderStyle.None ? 2 : 4; // None: 1 space each side; Pipe/Full: │ + space each side
			return Width ?? (Parsing.MarkupParser.StripLength(_text) + chrome);
		}

		private int GetButtonHeight()
		{
			return _borderStyle == ButtonBorderStyle.Full ? 3 : 1;
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _focused;
			set
			{
				_focused = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether the button is enabled and can be interacted with.
		/// </summary>
		public bool IsEnabled
		{
			get => _enabled;
			set
			{
				_enabled = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the text displayed on the button.
		/// </summary>
		public string Text
		{
			get => _text;
			set
			{
				_text = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color of the button in its normal state.
		/// </summary>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveButtonBackground(_backgroundColor, Container);
			set { _backgroundColor = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the foreground color of the button in its normal state.
		/// </summary>
		public Color ForegroundColor
		{
			get => ColorResolver.ResolveButtonForeground(_foregroundColor, Container);
			set { _foregroundColor = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the background color when the button has focus.
		/// </summary>
		public Color FocusedBackgroundColor
		{
			get => ColorResolver.ResolveButtonFocusedBackground(_focusedBackgroundColor, Container);
			set { _focusedBackgroundColor = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the foreground color when the button has focus.
		/// </summary>
		public Color FocusedForegroundColor
		{
			get => ColorResolver.ResolveButtonFocusedForeground(_focusedForegroundColor, Container);
			set { _focusedForegroundColor = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the background color when the button is disabled.
		/// </summary>
		public Color DisabledBackgroundColor
		{
			get => ColorResolver.ResolveButtonDisabledBackground(_disabledBackgroundColor, Container);
			set { _disabledBackgroundColor = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the foreground color when the button is disabled.
		/// </summary>
		public Color DisabledForegroundColor
		{
			get => ColorResolver.ResolveButtonDisabledForeground(_disabledForegroundColor, Container);
			set { _disabledForegroundColor = value; Container?.Invalidate(true); }
		}

		public ButtonBorderStyle ButtonBorder
		{
			get => _borderStyle;
			set { _borderStyle = value; Container?.Invalidate(true); }
		}

		public Color? BorderColor
		{
			get => _borderColor;
			set { _borderColor = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar)
			{
				// Trigger the click event
				TriggerClick(new MouseEventArgs(
					new List<MouseFlags> { MouseFlags.Button1Clicked },
					new System.Drawing.Point(0, 0), // No specific position for keyboard
					new System.Drawing.Point(0, 0),
					new System.Drawing.Point(0, 0)
				));
				return true;
			}

			return false;
		}

		// IMouseAwareControl implementation
		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <summary>
		/// Event fired when the button is clicked (by mouse or keyboard).
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <summary>
		/// Event fired when the button is clicked (convenience event that provides the button as parameter).
		/// </summary>
		public event EventHandler<ButtonControl>? Click;

		/// <inheritdoc/>
		#pragma warning disable CS0067  // Event never raised (interface requirement)
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;
		#pragma warning restore CS0067

		/// <summary>
		/// Occurs when the button is right-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Handle mouse enter/leave
			if (args.HasFlag(MouseFlags.MouseEnter))
			{
				MouseEnter?.Invoke(this, args);
				return true;
			}

			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				MouseLeave?.Invoke(this, args);
				return true;
			}

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Handle mouse clicks
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				TriggerClick(args);
				args.Handled = true;
				return true;
			}

			// Handle mouse movement (for future hover effects)
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
			}

			return false;
		}

		/// <summary>
		/// Triggers the click event from either mouse or keyboard input
		/// </summary>
		private void TriggerClick(MouseEventArgs args)
		{
			// Fire the mouse click event
			MouseClick?.Invoke(this, args);

			// Fire the convenience click event
			Click?.Invoke(this, this);
		}

		// IFocusableControl implementation
		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			var hadFocus = HasFocus;
			HasFocus = focus;

			if (focus && !hadFocus)
			{
				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else if (!focus && hadFocus)
			{
				LostFocus?.Invoke(this, EventArgs.Empty);
			}

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int chrome = _borderStyle == ButtonBorderStyle.None ? 2 : 4;
			int minWidth = _borderStyle == ButtonBorderStyle.None ? 2 : 4;
			int buttonWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch ? constraints.MaxWidth - Margin.Left - Margin.Right : Parsing.MarkupParser.StripLength(_text) + chrome);
			buttonWidth = Math.Max(minWidth, buttonWidth);

			int width = buttonWidth + Margin.Left + Margin.Right;
			int height = GetButtonHeight() + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			Color windowBackground = Container?.BackgroundColor ?? defaultBg;

			Color backgroundColor;
			Color foregroundColor;
			if (!_enabled)
			{
				foregroundColor = DisabledForegroundColor;
				backgroundColor = DisabledBackgroundColor;
			}
			else if (_focused)
			{
				foregroundColor = FocusedForegroundColor;
				backgroundColor = FocusedBackgroundColor;
			}
			else
			{
				foregroundColor = ForegroundColor;
				backgroundColor = BackgroundColor;
			}

			Color borderFg = _borderColor ?? foregroundColor;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			int chrome = _borderStyle == ButtonBorderStyle.None ? 2 : 4;
			int minWidth = _borderStyle == ButtonBorderStyle.None ? 2 : 4;
			int buttonWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch ? targetWidth : Math.Min(Parsing.MarkupParser.StripLength(_text) + chrome, targetWidth));
			buttonWidth = Math.Max(minWidth, buttonWidth);

			int innerWidth = buttonWidth - chrome;
			string text = innerWidth > 0 ? TextTruncationHelper.Truncate(_text, innerWidth) : string.Empty;
			int textLen = Parsing.MarkupParser.StripLength(text);
			int totalInnerPad = Math.Max(0, innerWidth - textLen);
			int leftInnerPad = totalInnerPad / 2;
			int rightInnerPad = totalInnerPad - leftInnerPad;

			int startY = bounds.Y + Margin.Top;
			int startX = bounds.X + Margin.Left;
			int buttonHeight = GetButtonHeight();

			// Calculate alignment offset
			int alignOffset = 0;
			if (buttonWidth < targetWidth)
			{
				switch (HorizontalAlignment)
				{
					case HorizontalAlignment.Center:
						alignOffset = (targetWidth - buttonWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						alignOffset = targetWidth - buttonWidth;
						break;
				}
			}

			int bx = startX + alignOffset;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, foregroundColor, windowBackground);

			for (int row = 0; row < buttonHeight; row++)
			{
				int y = startY + row;
				if (y < clipRect.Y || y >= clipRect.Bottom || y >= bounds.Bottom)
					continue;

				// Fill left margin + alignment padding
				int leftFillWidth = Margin.Left + alignOffset;
				if (leftFillWidth > 0)
					buffer.FillRect(new LayoutRect(bounds.X, y, leftFillWidth, 1), ' ', foregroundColor, windowBackground);

				if (_borderStyle == ButtonBorderStyle.Full)
				{
					if (row == 0)
					{
						// Top border: ┌──┐
						buffer.SetCell(bx, y, BoxChars.Single.TopLeft, borderFg, backgroundColor);
						for (int x = 1; x < buttonWidth - 1; x++)
							buffer.SetCell(bx + x, y, BoxChars.Single.Horizontal, borderFg, backgroundColor);
						buffer.SetCell(bx + buttonWidth - 1, y, BoxChars.Single.TopRight, borderFg, backgroundColor);
					}
					else if (row == 2)
					{
						// Bottom border: └──┘
						buffer.SetCell(bx, y, BoxChars.Single.BottomLeft, borderFg, backgroundColor);
						for (int x = 1; x < buttonWidth - 1; x++)
							buffer.SetCell(bx + x, y, BoxChars.Single.Horizontal, borderFg, backgroundColor);
						buffer.SetCell(bx + buttonWidth - 1, y, BoxChars.Single.BottomRight, borderFg, backgroundColor);
					}
					else
					{
						// Middle row: │ text │
						buffer.SetCell(bx, y, BoxChars.Single.Vertical, borderFg, backgroundColor);
						string padded = $" {new string(' ', leftInnerPad)}{text}{new string(' ', rightInnerPad)} ";
						var cells = Parsing.MarkupParser.Parse(padded, foregroundColor, backgroundColor);
						buffer.WriteCellsClipped(bx + 1, y, cells, clipRect);
						buffer.SetCell(bx + buttonWidth - 1, y, BoxChars.Single.Vertical, borderFg, backgroundColor);
					}
				}
				else if (_borderStyle == ButtonBorderStyle.Pipe)
				{
					// │ text │
					buffer.SetCell(bx, y, BoxChars.Single.Vertical, borderFg, backgroundColor);
					string padded = $" {new string(' ', leftInnerPad)}{text}{new string(' ', rightInnerPad)} ";
					var cells = Parsing.MarkupParser.Parse(padded, foregroundColor, backgroundColor);
					buffer.WriteCellsClipped(bx + 1, y, cells, clipRect);
					buffer.SetCell(bx + buttonWidth - 1, y, BoxChars.Single.Vertical, borderFg, backgroundColor);
				}
				else
				{
					// None: space + text + space
					string padded = $"{new string(' ', leftInnerPad + 1)}{text}{new string(' ', rightInnerPad + 1)}";
					var cells = Parsing.MarkupParser.Parse(padded, foregroundColor, backgroundColor);
					buffer.WriteCellsClipped(bx, y, cells, clipRect);
				}

				// Fill right alignment padding + right margin
				int rightPadStart = bx + buttonWidth;
				int rightFillWidth = bounds.Right - rightPadStart;
				if (rightFillWidth > 0)
					buffer.FillRect(new LayoutRect(rightPadStart, y, rightFillWidth, 1), ' ', foregroundColor, windowBackground);
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + buttonHeight, foregroundColor, windowBackground);
		}

		#endregion
	}
}
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
using Spectre.Console;
using System;
using System.Drawing;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A clickable button control that supports keyboard and mouse interaction.
	/// </summary>
	public class ButtonControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IDOMPaintable
	{
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private bool _enabled = true;
		private bool _focused;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string _text = "Button";
		private bool _visible = true;
		private int? _width;

		/// <summary>
		/// Initializes a new instance of the ButtonControl class with default settings.
		/// </summary>
		public ButtonControl()
		{
		}

		/// <summary>
		/// Gets the actual rendered width of the button in characters.
		/// </summary>
		public int? ActualWidth => GetButtonWidth() + _margin.Left + _margin.Right;

		private int GetButtonWidth()
		{
			string text = $"{(_focused ? ">" : "")}{_text}{(_focused ? "<" : "")}";
			return _width ?? (AnsiConsoleHelper.StripSpectreLength(text) + 4);
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{ get => _horizontalAlignment; set { _horizontalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{ get => _verticalAlignment; set { _verticalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

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

		public bool IsEnabled
		{
			get => _enabled;
			set
			{
				_enabled = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{ get => _margin; set { _margin = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		public string Text
		{
			get => _text;
			set
			{
				_text = value;
				Container?.Invalidate(true);
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

		/// <inheritdoc/>
		public void Dispose()
		{
			Container = null;
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			int buttonWidth = GetButtonWidth();
			return new System.Drawing.Size(buttonWidth + _margin.Left + _margin.Right, 1 + _margin.Top + _margin.Bottom);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (key.Key == ConsoleKey.Enter)
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
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			string text = $"{(_focused ? ">" : "")}{_text}{(_focused ? "<" : "")}";
			int buttonWidth = _width ?? (_horizontalAlignment == HorizontalAlignment.Stretch ? constraints.MaxWidth - _margin.Left - _margin.Right : AnsiConsoleHelper.StripSpectreLength(text) + 4);
			buttonWidth = Math.Max(4, buttonWidth);

			int width = buttonWidth + _margin.Left + _margin.Right;
			int height = 1 + _margin.Top + _margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			Color backgroundColor = Container?.BackgroundColor ?? defaultBg;
			Color foregroundColor = Container?.ForegroundColor ?? defaultFg;
			Color windowBackground = Container?.BackgroundColor ?? defaultBg;

			// Get theme colors
			if (Container?.GetConsoleWindowSystem?.Theme != null)
			{
				var theme = Container.GetConsoleWindowSystem.Theme;
				if (!_enabled)
				{
					foregroundColor = theme.ButtonDisabledForegroundColor;
					backgroundColor = theme.ButtonDisabledBackgroundColor;
				}
				else if (_focused)
				{
					foregroundColor = theme.ButtonFocusedForegroundColor;
					backgroundColor = theme.ButtonFocusedBackgroundColor;
				}
				else
				{
					foregroundColor = theme.ButtonForegroundColor;
					backgroundColor = theme.ButtonBackgroundColor;
				}
			}

			int targetWidth = bounds.Width - _margin.Left - _margin.Right;
			if (targetWidth <= 0) return;

			string text = $"{(_focused ? ">" : "")}{_text}{(_focused ? "<" : "")}";
			int buttonWidth = _width ?? (_horizontalAlignment == HorizontalAlignment.Stretch ? targetWidth : Math.Min(AnsiConsoleHelper.StripSpectreLength(text) + 4, targetWidth));
			buttonWidth = Math.Max(4, buttonWidth);
			int maxTextLength = buttonWidth - 4;

			// Truncate text if needed
			if (maxTextLength > 0 && AnsiConsoleHelper.StripSpectreLength(text) > maxTextLength)
			{
				int truncateLength = Math.Max(0, maxTextLength - 3);
				text = truncateLength > 0
					? AnsiConsoleHelper.TruncateSpectre(text, truncateLength) + "..."
					: "...".Substring(0, Math.Max(0, maxTextLength));
			}
			else if (maxTextLength <= 0)
			{
				text = string.Empty;
			}

			// Build button text with padding
			int padding = Math.Max(0, (buttonWidth - AnsiConsoleHelper.StripSpectreLength(text) - 2) / 2);
			string buttonText = $"[{new string(' ', padding)}{text}{new string(' ', padding)}]";
			int visibleLen = AnsiConsoleHelper.StripSpectreLength(buttonText);
			if (visibleLen < buttonWidth)
			{
				buttonText = buttonText + new string(' ', buttonWidth - visibleLen);
			}

			int startY = bounds.Y + _margin.Top;
			int startX = bounds.X + _margin.Left;

			// Calculate alignment offset
			int alignOffset = 0;
			if (buttonWidth < targetWidth)
			{
				switch (_horizontalAlignment)
				{
					case HorizontalAlignment.Center:
						alignOffset = (targetWidth - buttonWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						alignOffset = targetWidth - buttonWidth;
						break;
				}
			}

			// Fill top margin
			for (int y = bounds.Y; y < startY && y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', foregroundColor, windowBackground);
				}
			}

			// Paint button line
			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (_margin.Left > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.X, startY, _margin.Left, 1), ' ', foregroundColor, windowBackground);
				}

				// Fill alignment padding (left side)
				if (alignOffset > 0)
				{
					buffer.FillRect(new LayoutRect(startX, startY, alignOffset, 1), ' ', foregroundColor, windowBackground);
				}

				// Render button text
				var ansiLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(buttonText, buttonWidth, 1, false, backgroundColor, foregroundColor).FirstOrDefault() ?? string.Empty;
				var cells = AnsiParser.Parse(ansiLine, foregroundColor, backgroundColor);
				buffer.WriteCellsClipped(startX + alignOffset, startY, cells, clipRect);

				// Fill alignment padding (right side)
				int rightPadStart = startX + alignOffset + buttonWidth;
				int rightPadWidth = bounds.Right - rightPadStart - _margin.Right;
				if (rightPadWidth > 0)
				{
					buffer.FillRect(new LayoutRect(rightPadStart, startY, rightPadWidth, 1), ' ', foregroundColor, windowBackground);
				}

				// Fill right margin
				if (_margin.Right > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, startY, _margin.Right, 1), ' ', foregroundColor, windowBackground);
				}
			}

			// Fill bottom margin
			for (int y = startY + 1; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', foregroundColor, windowBackground);
				}
			}
		}

		#endregion
	}
}
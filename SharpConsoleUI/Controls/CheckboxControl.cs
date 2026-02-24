// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A toggleable checkbox control that displays a label and checked/unchecked state.
	/// Supports keyboard interaction with Space or Enter keys to toggle state.
	/// </summary>
	public class CheckboxControl : BaseControl, IInteractiveControl,
		IFocusableControl, IMouseAwareControl
	{
		private Color? _backgroundColorValue;
		private bool _checked = false;
		private Color? _checkmarkColorValue;
		private Color? _disabledBackgroundColorValue;
		private Color? _disabledForegroundColorValue;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus = false;
		private bool _isEnabled = true;
		private string _label = "Checkbox";
		private LayoutRect _lastLayoutBounds;

		/// <summary>
	/// Initializes a new instance of the <see cref="CheckboxControl"/> class.
		/// </summary>
		/// <param name="label">The text label displayed next to the checkbox.</param>
		/// <param name="isChecked">The initial checked state of the checkbox.</param>
		public CheckboxControl(string label = "Checkbox", bool isChecked = false)
		{
			_label = label;
			_checked = isChecked;
		}

		/// <summary>
		/// Occurs when the checked state of the checkbox changes.
		/// </summary>
		public event EventHandler<bool>? CheckedChanged;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <summary>
		/// Occurs when the checkbox is clicked with the mouse.
	/// </summary>
	public event EventHandler<MouseEventArgs>? MouseClick;

	/// <summary>
	/// Occurs when the mouse enters the checkbox area.
	/// </summary>
	public event EventHandler<MouseEventArgs>? MouseEnter;

	/// <summary>
	/// Occurs when the mouse leaves the checkbox area.
	/// </summary>
	public event EventHandler<MouseEventArgs>? MouseLeave;

	/// <summary>
	/// Occurs when the mouse moves over the checkbox.
	/// </summary>
	public event EventHandler<MouseEventArgs>? MouseMove;

	/// <summary>
	/// Occurs when the checkbox is double-clicked with the mouse.
	/// </summary>
	public event EventHandler<MouseEventArgs>? MouseDoubleClick;

	/// <summary>
	/// Gets the actual rendered width of the control based on content.
		/// </summary>
		public override int? ContentWidth => GetCheckboxWidth() + Margin.Left + Margin.Right;

		private int GetCheckboxWidth()
		{
		// Build content with decorators (same as rendering)
		string checkmark = _checked ? "X" : " ";
		string content = _hasFocus
		  ? $">[{checkmark}] {_label}<"
			: $" [{checkmark}] {_label} ";

		int minWidth = AnsiConsoleHelper.StripSpectreLength(content);
		return Width ?? minWidth;
	}

		/// <summary>
		/// Gets or sets the background color of the checkbox in its normal state.
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
		/// Gets or sets the checked state of the checkbox.
		/// </summary>
		public bool Checked
		{
			get => _checked;
			set
			{
				if (_checked != value)
				{
					_checked = value;
					Container?.Invalidate(true);
					CheckedChanged?.Invoke(this, _checked);
				}
			}
		}

		/// <summary>
		/// Gets or sets the color of the checkmark character when checked.
		/// </summary>
		public Color CheckmarkColor
		{
			get => _checkmarkColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.Cyan1;
			set
			{
				_checkmarkColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color when the control is disabled.
		/// </summary>
		public Color DisabledBackgroundColor
		{
			get => _disabledBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor ?? Color.Grey;
			set
			{
				_disabledBackgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color when the control is disabled.
		/// </summary>
		public Color DisabledForegroundColor
		{
			get => _disabledForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor ?? Color.DarkSlateGray1;
			set
			{
				_disabledForegroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color when the control has focus.
		/// </summary>
		public Color FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Blue;
			set
			{
				_focusedBackgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color when the control has focus.
		/// </summary>
		public Color FocusedForegroundColor
		{
			get => _focusedForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.White;
			set
			{
				_focusedForegroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the checkbox in its normal state.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonForegroundColor ?? Color.White;
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
				if (_hasFocus != value)
				{
					_hasFocus = value;
					Container?.Invalidate(true);

					if (value)
						GotFocus?.Invoke(this, EventArgs.Empty);
					else
						LostFocus?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <summary>
		/// Gets or sets whether the checkbox is enabled and can be interacted with.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <summary>
		/// Gets or sets the label text displayed next to the checkbox.
		/// </summary>
		public string Label
		{
			get => _label;
			set
			{
				_label = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets whether the checkbox wants to receive mouse events.
	/// Only receives events when enabled.
	/// </summary>
	public bool WantsMouseEvents => _isEnabled;

	/// <summary>
	/// Gets whether the checkbox can receive focus via mouse click.
	/// Only can focus when enabled.
	/// </summary>
	public bool CanFocusWithMouse => _isEnabled;

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			// Toggle checkbox state when Space or Enter is pressed
			if (key.Key == ConsoleKey.Spacebar || key.Key == ConsoleKey.Enter)
			{
				Checked = !Checked; // Use property setter to trigger event
				return true;
			}

			return false;
		}

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
		/// Processes mouse events for the checkbox.
	/// Handles clicks to toggle the checked state and capture focus.
	/// </summary>
	public bool ProcessMouseEvent(MouseEventArgs args)
	{
		// Guard: Only process events when enabled
		if (!_isEnabled || !WantsMouseEvents)
			return false;

		// Handle mouse leave
		if (args.HasFlag(MouseFlags.MouseLeave))
		{
			MouseLeave?.Invoke(this, args);
			return true;
		}

		// Handle mouse enter
		if (args.HasFlag(MouseFlags.MouseEnter))
		{
			MouseEnter?.Invoke(this, args);
			return true;
		}

		// Validate click is within content area (not in margins)
		// Mouse coordinates are control-relative (already offset from control bounds)
		int contentHeight = (_lastLayoutBounds.Height > 0 ? _lastLayoutBounds.Height : 1);
		if (args.Position.Y < Margin.Top ||
			args.Position.Y >= contentHeight - Margin.Bottom ||
			args.Position.X < Margin.Left ||
			args.Position.X >= (_lastLayoutBounds.Width - Margin.Right))
		{
			// Click is in margin area - not interactive
			return false;
		}

		// Check if click is on the checkbox row (single-row control)
		// Content row is at Margin.Top
		bool isOnCheckbox = args.Position.Y == Margin.Top;

		if (!isOnCheckbox)
		{
			// Click is not on checkbox row (shouldn't happen for single-row control)
			return false;
		}

		// Handle mouse movement (for future hover effects or visual feedback)
		if (args.HasAnyFlag(MouseFlags.ReportMousePosition))
		{
			MouseMove?.Invoke(this, args);
			return true;
		}

		// Handle mouse click to toggle checkbox
		if (args.HasFlag(MouseFlags.Button1Clicked))
		{
			// Capture focus if not already focused
			if (!_hasFocus)
			{
				SetFocus(true, FocusReason.Mouse);
			}

			// Toggle checked state (uses property setter to fire CheckedChanged event)
			Checked = !Checked;

			// Fire mouse click event
			MouseClick?.Invoke(this, args);

			// Mark event as handled and trigger re-render
			args.Handled = true;
			Container?.Invalidate(true);

			return true;
		}

		// Handle double-click (same behavior as single click for checkboxes)
		if (args.HasFlag(MouseFlags.Button1DoubleClicked))
		{
			// Capture focus if not already focused
			if (!_hasFocus)
			{
				SetFocus(true, FocusReason.Mouse);
			}

			// Toggle checked state
			Checked = !Checked;

			// Fire double-click event
			MouseDoubleClick?.Invoke(this, args);

			// Mark event as handled and trigger re-render
			args.Handled = true;
			Container?.Invalidate(true);

			return true;
		}

		return false;
	}

	/// <summary>
	/// Toggles the checked state of the checkbox.
		/// </summary>
		public void Toggle()
		{
			Checked = !Checked;
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
		// Build content with decorators (same as rendering)
		string checkmark = _checked ? "X" : " ";
		string content = _hasFocus
			? $">[{checkmark}] {_label}<"
		 : $" [{checkmark}] {_label} ";

		int minWidth = AnsiConsoleHelper.StripSpectreLength(content);
		int checkboxWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch ? constraints.MaxWidth - Margin.Left - Margin.Right : minWidth);
		checkboxWidth = Math.Max(minWidth, checkboxWidth);

		int width = checkboxWidth + Margin.Left + Margin.Right;
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

		// Store bounds for mouse handling
		_lastLayoutBounds = bounds;

		Color backgroundColor;
			Color foregroundColor;
			Color windowBackground = Container?.BackgroundColor ?? defaultBg;

			// Determine colors based on enabled/focused state
			if (!_isEnabled)
			{
				backgroundColor = DisabledBackgroundColor;
				foregroundColor = DisabledForegroundColor;
			}
			else if (_hasFocus)
			{
				backgroundColor = FocusedBackgroundColor;
				foregroundColor = FocusedForegroundColor;
			}
			else
			{
				backgroundColor = BackgroundColor;
				foregroundColor = ForegroundColor;
			}

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			// Build checkbox content
			string checkmark = _checked ? "X" : " ";
			string tempContent = _hasFocus
			 ? $">[{checkmark}] {_label}<"
			: $" [{checkmark}] {_label} ";

			// Calculate checkbox width with decorators
		int minWidth = AnsiConsoleHelper.StripSpectreLength(tempContent);
		int checkboxWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch ? targetWidth : minWidth);
		checkboxWidth = Math.Min(Math.Max(minWidth, checkboxWidth), targetWidth);
			string checkboxContent;

			if (_hasFocus)
			{
				checkboxContent = $">[{checkmark}] {_label}<";
			}
			else
			{
				checkboxContent = $" [{checkmark}] {_label} ";
			}

			// Add checkmark color if checked
			if (_checked)
			{
				checkboxContent = checkboxContent.Replace(checkmark, $"[{CheckmarkColor.ToMarkup()}]{checkmark}[/]");
			}

			// Pad to checkboxWidth
			int visibleLen = AnsiConsoleHelper.StripSpectreLength(checkboxContent);
			if (visibleLen < checkboxWidth)
			{
				checkboxContent = checkboxContent + new string(' ', checkboxWidth - visibleLen);
			}

			int startY = bounds.Y + Margin.Top;
			int startX = bounds.X + Margin.Left;

			// Calculate alignment offset
			int alignOffset = 0;
			if (checkboxWidth < targetWidth)
			{
				switch (HorizontalAlignment)
				{
					case HorizontalAlignment.Center:
						alignOffset = (targetWidth - checkboxWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						alignOffset = targetWidth - checkboxWidth;
						break;
				}
			}

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, foregroundColor, windowBackground);

			// Paint checkbox line
			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (Margin.Left > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.X, startY, Margin.Left, 1), ' ', foregroundColor, windowBackground);
				}

				// Fill alignment padding (left side)
				if (alignOffset > 0)
				{
					buffer.FillRect(new LayoutRect(startX, startY, alignOffset, 1), ' ', foregroundColor, windowBackground);
				}

				// Render checkbox content
				var ansiLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(checkboxContent, checkboxWidth, 1, false, backgroundColor, foregroundColor).FirstOrDefault() ?? string.Empty;
				var cells = AnsiParser.Parse(ansiLine, foregroundColor, backgroundColor);
				buffer.WriteCellsClipped(startX + alignOffset, startY, cells, clipRect);

				// Fill alignment padding (right side)
				int rightPadStart = startX + alignOffset + checkboxWidth;
				int rightPadWidth = bounds.Right - rightPadStart - Margin.Right;
				if (rightPadWidth > 0)
				{
					buffer.FillRect(new LayoutRect(rightPadStart, startY, rightPadWidth, 1), ' ', foregroundColor, windowBackground);
				}

				// Fill right margin
				if (Margin.Right > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.Right - Margin.Right, startY, Margin.Right, 1), ' ', foregroundColor, windowBackground);
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + 1, foregroundColor, windowBackground);
		}

		#endregion
	}
}

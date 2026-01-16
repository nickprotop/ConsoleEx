// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
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
	public class CheckboxControl : IWindowControl, IInteractiveControl, IFocusableControl, IDOMPaintable
	{
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
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
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

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
		/// Gets the actual rendered width of the control based on content.
		/// </summary>
		public int? ActualWidth => GetCheckboxWidth() + _margin.Left + _margin.Right;

		private int GetCheckboxWidth()
		{
			// Checkbox format: "[X] Label" or ">[X] Label<" when focused
			int minWidth = AnsiConsoleHelper.StripSpectreLength($"[{(_checked ? "X" : " ")}] {_label}") + 2;
			return _width ?? minWidth;
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{ get => _horizontalAlignment; set { _horizontalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{ get => _verticalAlignment; set { _verticalAlignment = value; Container?.Invalidate(true); } }

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

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

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

		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_isEnabled = value;
				Container?.Invalidate(true);
			}
		}

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
		/// Gets or sets the margin around the control content.
		/// </summary>
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

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the fixed width of the control. When null, the control auto-sizes based on content.
		/// </summary>
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
			int width = GetCheckboxWidth() + _margin.Left + _margin.Right;
			return new System.Drawing.Size(width, 1 + _margin.Top + _margin.Bottom);
		}

		/// <summary>
		/// Invalidates the control, forcing a re-render on the next draw.
		/// </summary>
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

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
		/// Toggles the checked state of the checkbox.
		/// </summary>
		public void Toggle()
		{
			Checked = !Checked;
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int minWidth = AnsiConsoleHelper.StripSpectreLength($"[{(_checked ? "X" : " ")}] {_label}") + 2;
			int checkboxWidth = _width ?? (_horizontalAlignment == HorizontalAlignment.Stretch ? constraints.MaxWidth - _margin.Left - _margin.Right : minWidth);
			checkboxWidth = Math.Max(minWidth, checkboxWidth);

			int width = checkboxWidth + _margin.Left + _margin.Right;
			int height = 1 + _margin.Top + _margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
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

			int targetWidth = bounds.Width - _margin.Left - _margin.Right;
			if (targetWidth <= 0) return;

			// Calculate checkbox width
			int minWidth = AnsiConsoleHelper.StripSpectreLength($"[{(_checked ? "X" : " ")}] {_label}") + 2;
			int checkboxWidth = _width ?? (_horizontalAlignment == HorizontalAlignment.Stretch ? targetWidth : minWidth);
			checkboxWidth = Math.Min(Math.Max(minWidth, checkboxWidth), targetWidth);

			// Build checkbox content
			string checkmark = _checked ? "X" : " ";
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

			int startY = bounds.Y + _margin.Top;
			int startX = bounds.X + _margin.Left;

			// Calculate alignment offset
			int alignOffset = 0;
			if (checkboxWidth < targetWidth)
			{
				switch (_horizontalAlignment)
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
				if (_margin.Left > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.X, startY, _margin.Left, 1), ' ', foregroundColor, windowBackground);
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
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + 1, foregroundColor, windowBackground);
		}

		#endregion
	}
}

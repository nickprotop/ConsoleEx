// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A toggleable checkbox control that displays a label and checked/unchecked state.
	/// Supports keyboard interaction with Space or Enter keys to toggle state.
	/// </summary>
	public class CheckboxControl : BaseControl, IInteractiveControl,
		IFocusableControl, IMouseAwareControl, IColorRoleableControl
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

		private Color? _backgroundColorValue;
		private bool _checked = false;
		private string _checkedCharacter = "X";
		private Color? _checkmarkColorValue;
		private Color? _disabledBackgroundColorValue;
		private Color? _disabledForegroundColorValue;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _isEnabled = true;
		private string _label = "Checkbox";
		private LayoutRect _lastLayoutBounds;
		private string _uncheckedCharacter = " ";

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

		/// <summary>Async counterpart of <see cref="CheckedChanged"/>.</summary>
		public event Core.AsyncEventHandler<bool>? CheckedChangedAsync;

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
		/// Occurs when the checkbox is right-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <summary>
		/// Gets the actual rendered width of the control based on content.
		/// </summary>
		public override int? ContentWidth => GetCheckboxWidth() + Margin.Left + Margin.Right;

		private int GetCheckboxWidth()
		{
			// Build content with decorators (same as rendering)
			string checkmark = Parsing.MarkupParser.Escape(_checked ? _checkedCharacter : _uncheckedCharacter);
			string content = $" [[{checkmark}]] {_label} ";

			int minWidth = Parsing.MarkupParser.StripLength(content);
			return Width ?? minWidth;
		}

		/// <summary>
		/// Gets or sets the background color of the checkbox in its normal state.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set => SetProperty(ref _backgroundColorValue, value);
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
					OnPropertyChanged();
					Invalidate(Invalidation.Repaint);
					Core.AsyncEvent.Raise(CheckedChanged, CheckedChangedAsync, this, _checked, Container?.GetConsoleWindowSystem?.LogService);
				}
			}
		}

		/// <summary>
		/// Gets or sets the character displayed inside the checkbox when checked.
		/// Defaults to "X". Set to any single visible character (e.g. "✓", "✗", "●").
		/// Empty or null values fall back to the default.
		/// </summary>
		public string CheckedCharacter
		{
			get => _checkedCharacter;
			set => SetProperty(ref _checkedCharacter, string.IsNullOrEmpty(value) ? "X" : value);
		}

		/// <summary>
		/// Gets or sets the character displayed inside the checkbox when unchecked.
		/// Defaults to a space. Set to any single visible character.
		/// Empty or null values fall back to the default.
		/// </summary>
		public string UncheckedCharacter
		{
			get => _uncheckedCharacter;
			set => SetProperty(ref _uncheckedCharacter, string.IsNullOrEmpty(value) ? " " : value);
		}

		/// <summary>
		/// Gets or sets the color of the checkmark character when checked.
		/// </summary>
		public Color CheckmarkColor
		{
			get => _checkmarkColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.CheckboxCheckmarkColor ?? Color.Cyan1;
			set => SetProperty(ref _checkmarkColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the background color when the control is disabled.
		/// </summary>
		public Color? DisabledBackgroundColor
		{
			get => _disabledBackgroundColorValue;
			set => SetProperty(ref _disabledBackgroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the foreground color when the control is disabled.
		/// </summary>
		public Color DisabledForegroundColor
		{
			get => ResolveForeground(ColorRoleState.Disabled);
			set => SetProperty(ref _disabledForegroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the background color when the control has focus.
		/// </summary>
		public Color? FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue;
			set => SetProperty(ref _focusedBackgroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the foreground color when the control has focus.
		/// </summary>
		public Color FocusedForegroundColor
		{
			get => ResolveForeground(ColorRoleState.Focused);
			set => SetProperty(ref _focusedForegroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the foreground color of the checkbox in its normal state.
		/// </summary>
		public Color ForegroundColor
		{
			get => ResolveForeground(CurrentRoleState);
			set => SetProperty(ref _foregroundColorValue, (Color?)value);
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => ComputeHasFocus();
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <summary>
		/// Gets or sets whether the checkbox is enabled and can be interacted with.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		/// <summary>
		/// Gets or sets the label text displayed next to the checkbox.
		/// </summary>
		public string Label
		{
			get => _label;
			set => SetProperty(ref _label, value);
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
			if (!_isEnabled || !(ComputeHasFocus()))
				return false;

			// Toggle checkbox state when Space or Enter is pressed
			if (key.Key == ConsoleKey.Spacebar || key.Key == ConsoleKey.Enter)
			{
				Checked = !Checked; // Use property setter to trigger event
				return true;
			}

			return false;
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

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Handle mouse click to toggle checkbox
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				// Focus is already set by FocusManager.HandleClick before ProcessMouseEvent is called.
				// Toggle checked state (uses property setter to fire CheckedChanged event).
				// The setter self-invalidates, so no explicit Invalidate is needed here (CLAUDE.md rule #5).
				Checked = !Checked;

				// Fire mouse click event
				MouseClick?.Invoke(this, args);

				// Mark event as handled
				args.Handled = true;

				return true;
			}

			// Handle double-click — do NOT toggle again (Button1Clicked already toggled).
			// Just consume the event to prevent it from propagating.
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				MouseDoubleClick?.Invoke(this, args);
				args.Handled = true;
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

		#region Color Resolution

		/// <summary>
		/// Computes the current role state from the checkbox's enabled/focus state so role colours
		/// reflect the same visual state the renderer paints.
		/// </summary>
		private ColorRoleState CurrentRoleState =>
			!_isEnabled ? ColorRoleState.Disabled : (ComputeHasFocus() ? ColorRoleState.Focused : ColorRoleState.Normal);

		/// <summary>
		/// Resolves the painted label foreground: explicit override, then the role colour as text on the
		/// window surface (<see cref="ColorResolver.ColorRoleForeground"/>), then the legacy per-state default.
		/// For <see cref="ColorRole.Default"/> (no role) the role helper returns null, so this is the
		/// legacy resolved value. Pure in <paramref name="state"/>.
		/// </summary>
		private Color ResolveForeground(ColorRoleState state)
		{
			if (state == ColorRoleState.Disabled)
			{
				return _disabledForegroundColorValue
					?? ColorResolver.ColorRoleForeground(ColorRole, Container, Outline, state, mode: ColorRoleMode)
					?? Container?.GetConsoleWindowSystem?.Theme?.CheckboxDisabledForegroundColor ?? Color.Grey50;
			}
			if (state == ColorRoleState.Focused)
			{
				return _focusedForegroundColorValue
					?? ColorResolver.ColorRoleForeground(ColorRole, Container, Outline, state, mode: ColorRoleMode)
					?? Container?.GetConsoleWindowSystem?.Theme?.CheckboxFocusedForegroundColor ?? Color.White;
			}
			return _foregroundColorValue
				?? ColorResolver.ColorRoleForeground(ColorRole, Container, Outline, state, mode: ColorRoleMode)
				?? Container?.GetConsoleWindowSystem?.Theme?.CheckboxForegroundColor ?? Color.White;
		}

		/// <summary>
		/// Resolves the painted background fill: explicit override, then the legacy per-state default.
		/// A checkbox is a surface-text control (its label sits on the window surface), so a role must
		/// NOT introduce a solid background fill — the role colours the label foreground only (see
		/// <see cref="ResolveForeground"/>). Pure in <paramref name="state"/>.
		/// </summary>
		private Color ResolveBackground(ColorRoleState state)
		{
			if (state == ColorRoleState.Disabled)
			{
				return _disabledBackgroundColorValue
					?? ColorResolver.ResolveCheckboxDisabledBackground(null, Container);
			}
			if (state == ColorRoleState.Focused)
			{
				return _focusedBackgroundColorValue
					?? ColorResolver.ResolveCheckboxFocusedBackground(null, Container);
			}
			return _backgroundColorValue
				?? ColorResolver.ResolveCheckboxBackground(null, Container);
		}

		/// <summary>
		/// Test-only observability hook for the resolved background fill at the current role state.
		/// A role must not change this value (a checkbox has no solid fill); the label foreground
		/// carries the role colour instead.
		/// </summary>
		internal Color ResolvedBackgroundColor => ResolveBackground(CurrentRoleState);

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// Build content with decorators (same as rendering)
			string checkmark = Parsing.MarkupParser.Escape(_checked ? _checkedCharacter : _uncheckedCharacter);
			string content = $" [[{checkmark}]] {_label} ";

			int minWidth = Parsing.MarkupParser.StripLength(content);
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
			var effectiveBg = Color.Transparent;

			// Determine colors based on enabled/focused state (role link applied inside the resolvers).
			ColorRoleState roleState = CurrentRoleState;
			backgroundColor = ResolveBackground(roleState);
			foregroundColor = ResolveForeground(roleState);

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			// Build checkbox content
			string checkmark = Parsing.MarkupParser.Escape(_checked ? _checkedCharacter : _uncheckedCharacter);
			string tempContent = $" [[{checkmark}]] {_label} ";

			// Calculate checkbox width with decorators
			int minWidth = Parsing.MarkupParser.StripLength(tempContent);
			int checkboxWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch ? targetWidth : minWidth);
			checkboxWidth = Math.Min(Math.Max(minWidth, checkboxWidth), targetWidth);
			string checkboxContent;

			// Build checkmark display with optional color markup. Only tint with CheckmarkColor when
			// ENABLED: a disabled+checked checkbox must dim its mark to the resolved (grey) disabled
			// foreground like its label, not paint a vivid CheckmarkColor glyph that looks active.
			string checkmarkDisplay = _checked && _isEnabled
				? $"[{CheckmarkColor.ToMarkup()}]{checkmark}[/]"
				: checkmark;

			checkboxContent = $" [[{checkmarkDisplay}]] {_label} ";

			// Pad to checkboxWidth
			int visibleLen = Parsing.MarkupParser.StripLength(checkboxContent);
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
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, foregroundColor, effectiveBg);

			// Paint checkbox line
			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (Margin.Left > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, startY, Margin.Left, 1), foregroundColor, effectiveBg);
				}

				// Fill alignment padding (left side)
				if (alignOffset > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX, startY, alignOffset, 1), foregroundColor, effectiveBg);
				}

				// Render checkbox content
				var cellBg = backgroundColor; // backgroundColor is already Transparent when no explicit value
				var cells = Parsing.MarkupParser.Parse(checkboxContent, foregroundColor, cellBg);
				buffer.WriteCellsClipped(startX + alignOffset, startY, cells, clipRect);

				// Fill alignment padding (right side)
				int rightPadStart = startX + alignOffset + checkboxWidth;
				int rightPadWidth = bounds.Right - rightPadStart - Margin.Right;
				if (rightPadWidth > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightPadStart, startY, rightPadWidth, 1), foregroundColor, effectiveBg);
				}

				// Fill right margin
				if (Margin.Right > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, startY, Margin.Right, 1), foregroundColor, effectiveBg);
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + 1, foregroundColor, effectiveBg);
		}

		#endregion
	}
}

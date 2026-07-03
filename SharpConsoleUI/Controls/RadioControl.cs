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
using SharpConsoleUI.Logging;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A single radio option whose selection is coordinated by a <see cref="RadioGroup{T}"/>.
	/// Mirrors <see cref="CheckboxControl"/> but its <see cref="Checked"/> state is computed from the
	/// group (the group is the single source of truth), guaranteeing the single-selection invariant.
	/// Supports keyboard interaction with Space or Enter keys to select.
	/// </summary>
	/// <typeparam name="T">The value type this radio represents.</typeparam>
	public class RadioControl<T> : BaseControl, IInteractiveControl,
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

		private readonly RadioGroup<T> _group;
		private readonly T _value;
		private Color? _backgroundColorValue;
		private Color? _checkmarkColorValue;
		private Color? _disabledBackgroundColorValue;
		private Color? _disabledForegroundColorValue;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _isEnabled = true;
		private string _label;
		private LayoutRect _lastLayoutBounds;
		private string _selectedCharacter = "●";
		private string _unselectedCharacter = "○";
		private bool _wrap = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="RadioControl{T}"/> class and registers it with the group.
		/// </summary>
		/// <param name="group">The coordinating group that owns the single-selection invariant.</param>
		/// <param name="value">The value this radio represents.</param>
		/// <param name="label">The text label displayed next to the radio.</param>
		public RadioControl(RadioGroup<T> group, T value, string label = "")
		{
			_group = group;
			_value = value;
			_label = label;
			_group.Register(this);
		}

		/// <summary>
		/// Occurs when the radio is clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <summary>
		/// Occurs when the mouse enters the radio area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <summary>
		/// Occurs when the mouse leaves the radio area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <summary>
		/// Occurs when the mouse moves over the radio.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <summary>
		/// Occurs when the radio is double-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <summary>
		/// Occurs when the radio is right-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <summary>Gets the coordinating group that owns the single-selection invariant.</summary>
		public RadioGroup<T> Group => _group;

		/// <summary>Gets the value this radio represents.</summary>
		public T Value => _value;

		/// <summary>
		/// Gets a value indicating whether this radio is the group's selected option. Computed from the
		/// group (no stored state), so it always reflects the single-selection invariant.
		/// </summary>
		public bool Checked =>
			_group.HasSelection && EqualityComparer<T>.Default.Equals(_group.SelectedValue!, _value);

		/// <summary>
		/// Gets the actual rendered width of the control based on content.
		/// </summary>
		public override int? ContentWidth => GetRadioWidth() + Margin.Left + Margin.Right;

		private int GetRadioWidth()
		{
			string mark = Parsing.MarkupParser.Escape(Checked ? _selectedCharacter : _unselectedCharacter);
			string content = $"{mark} {Parsing.MarkupParser.Escape(_label)} ";

			int minWidth = Parsing.MarkupParser.StripLength(content);
			return Width ?? minWidth;
		}

		/// <summary>
		/// Gets or sets the background color of the radio in its normal state.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set => SetProperty(ref _backgroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the character displayed inside the radio when selected.
		/// Defaults to "●". Empty or null values fall back to the default.
		/// </summary>
		public string SelectedCharacter
		{
			get => _selectedCharacter;
			set => SetProperty(ref _selectedCharacter, string.IsNullOrEmpty(value) ? "●" : value);
		}

		/// <summary>
		/// Gets or sets the character displayed inside the radio when unselected.
		/// Defaults to "○". Empty or null values fall back to the default.
		/// </summary>
		public string UnselectedCharacter
		{
			get => _unselectedCharacter;
			set => SetProperty(ref _unselectedCharacter, string.IsNullOrEmpty(value) ? "○" : value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the label wraps across lines when too wide to fit.
		/// (Wrap geometry is applied by the paint pass; single-line rendering is used until then.)
		/// </summary>
		public bool Wrap
		{
			get => _wrap;
			set => SetProperty(ref _wrap, value);
		}

		/// <summary>
		/// Gets or sets the color of the mark character when selected.
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
		/// Gets or sets the foreground color of the radio in its normal state.
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
		/// Gets or sets whether the radio is enabled and can be interacted with.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		/// <summary>
		/// Gets or sets the label text displayed next to the radio.
		/// </summary>
		public string Label
		{
			get => _label;
			set => SetProperty(ref _label, value);
		}

		/// <summary>
		/// Gets whether the radio wants to receive mouse events.
		/// Only receives events when enabled.
		/// </summary>
		public bool WantsMouseEvents => _isEnabled;

		/// <summary>
		/// Gets whether the radio can receive focus via mouse click.
		/// Only can focus when enabled.
		/// </summary>
		public bool CanFocusWithMouse => _isEnabled;

		/// <summary>
		/// Selects this radio through the group. If this radio is already the selected one, the group's
		/// deselect/Required policy applies (classic radios stay selected).
		/// </summary>
		public void Select() => _group.RequestToggle(this);

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !(ComputeHasFocus()))
				return false;

			// Select this radio when Space or Enter is pressed
			if (key.Key == ConsoleKey.Spacebar || key.Key == ConsoleKey.Enter)
			{
				Select();
				return true;
			}

			return false;
		}

		/// <summary>
		/// Processes mouse events for the radio.
		/// Handles clicks to select this option and capture focus.
		/// </summary>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			// Guard: Only process events when enabled (WantsMouseEvents is derived from _isEnabled)
			if (!_isEnabled)
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

			// Check if the click lands on any row of the radio's content area. A radio may span
			// multiple rows when Wrap is true and its label wraps, so ALL rows between the top and
			// bottom margins are interactive — not just the marker row.
			int radioHeight = _lastLayoutBounds.Height > 0 ? _lastLayoutBounds.Height : 1;
			bool isOnRadio = args.Position.Y >= Margin.Top &&
				args.Position.Y < (radioHeight - Margin.Bottom);

			if (!isOnRadio)
			{
				// Click is outside the radio's content rows (margin area).
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

			// Handle mouse click to select this radio
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				// Focus is already set by FocusManager.HandleClick before ProcessMouseEvent is called.
				Select();

				// Fire mouse click event
				MouseClick?.Invoke(this, args);

				// Mark event as handled. Select() → group SetSelected → RepaintFromGroup already
				// invalidates the affected members, so no redundant Container.Invalidate here.
				args.Handled = true;

				return true;
			}

			// Handle double-click — do NOT select again (Button1Clicked already selected).
			// Just consume the event to prevent it from propagating.
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				MouseDoubleClick?.Invoke(this, args);
				args.Handled = true;
				return true;
			}

			return false;
		}

		#region Group hooks

		/// <summary>Repaints this radio in response to a group selection change.</summary>
		internal void RepaintFromGroup() => Invalidate(Invalidation.Repaint);

		/// <summary>Gets the log service for async-event dispatch, if a system is attached.</summary>
		internal ILogService? GetLogService() => Container?.GetConsoleWindowSystem?.LogService;

		#endregion

		#region Color Resolution

		/// <summary>
		/// Computes the current role state from the radio's enabled/focus state so role colours
		/// reflect the same visual state the renderer paints.
		/// </summary>
		private ColorRoleState CurrentRoleState =>
			!_isEnabled ? ColorRoleState.Disabled : (ComputeHasFocus() ? ColorRoleState.Focused : ColorRoleState.Normal);

		/// <summary>
		/// Resolves the painted label foreground: explicit override, then the role colour as text on the
		/// window surface (<see cref="ColorResolver.ColorRoleForeground"/>), then the legacy per-state default.
		/// Pure in <paramref name="state"/>.
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
		/// A radio is a surface-text control (its label sits on the window surface), so a role must
		/// NOT introduce a solid background fill — the role colours the label foreground only.
		/// Pure in <paramref name="state"/>.
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

		#endregion

		#region IDOMPaintable Implementation

		/// <summary>
		/// Builds the marker prefix markup (<c>"mark "</c>) for an explicit selection state,
		/// escaping the marker glyph so it is treated as a literal. The round marker glyph itself
		/// conveys selection, so no surrounding brackets are drawn.
		/// </summary>
		private string BuildMarkerPrefix(bool selected)
		{
			string mark = Parsing.MarkupParser.Escape(selected ? _selectedCharacter : _unselectedCharacter);
			return $"{mark} ";
		}

		/// <summary>
		/// Computes the display-column width of the marker prefix by counting parsed cells
		/// (never <c>string.Length</c>), so a wide marker glyph advances the label by its real width.
		/// The width is the MAX over BOTH selection states so the layout slot is always wide enough
		/// regardless of the current <see cref="Checked"/> value — otherwise measure and paint would
		/// disagree when the selected/unselected glyphs have different display widths (e.g. a 2-wide
		/// selected marker and a 1-wide unselected marker). This width also drives the hanging indent
		/// of wrapped continuation lines.
		/// </summary>
		private int MeasureMarkerPrefixWidth()
		{
			int selectedWidth = Parsing.MarkupParser.Parse(BuildMarkerPrefix(true), Color.White, Color.Black).Count;
			int unselectedWidth = Parsing.MarkupParser.Parse(BuildMarkerPrefix(false), Color.White, Color.Black).Count;
			return Math.Max(selectedWidth, unselectedWidth);
		}

		/// <summary>
		/// Wraps the (escaped) label to <paramref name="labelWidth"/> display columns using the same
		/// soft-wrap engine <see cref="MarkupControl"/> uses (<c>MarkupParser.WrapMarkupLines</c>).
		/// Returns at least one line.
		/// </summary>
		private List<string> WrapLabelLines(int labelWidth)
		{
			string escaped = Parsing.MarkupParser.Escape(_label);
			if (labelWidth <= 0)
				return new List<string> { escaped };
			return Parsing.MarkupParser.WrapMarkupLines(escaped, labelWidth);
		}

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int markerPrefixWidth = MeasureMarkerPrefixWidth();
			int hMargin = Margin.Left + Margin.Right;
			int vMargin = Margin.Top + Margin.Bottom;

			int labelWidth = Parsing.MarkupParser.StripLength(Parsing.MarkupParser.Escape(_label));

			int width;
			int height;

			if (!_wrap)
			{
				// Single-line: full content on one row.
				int contentWidth = markerPrefixWidth + labelWidth + 1; // trailing space
				int minWidth = contentWidth;
				int radioWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch ? constraints.MaxWidth - hMargin : minWidth);
				radioWidth = Math.Max(minWidth, radioWidth);
				width = radioWidth + hMargin;
				height = 1 + vMargin;
			}
			else
			{
				// Wrap: label wraps into the columns remaining after the marker prefix.
				int available = (Width ?? constraints.MaxWidth) - hMargin - markerPrefixWidth;
				available = Math.Max(1, available);
				var lines = WrapLabelLines(available);
				int lineCount = Math.Max(1, lines.Count);

				int widestLabelLine = 0;
				foreach (var line in lines)
					widestLabelLine = Math.Max(widestLabelLine, Parsing.MarkupParser.StripLength(line));

				int contentWidth = markerPrefixWidth + widestLabelLine;
				int radioWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch ? constraints.MaxWidth - hMargin : contentWidth);
				radioWidth = Math.Max(contentWidth, radioWidth);
				width = radioWidth + hMargin;
				height = lineCount + vMargin;
			}

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

			// Determine colors based on enabled/focused state (role link applied inside the resolvers).
			ColorRoleState roleState = CurrentRoleState;
			Color backgroundColor = ResolveBackground(roleState);
			Color foregroundColor = ResolveForeground(roleState);

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			// Build the marker prefix ("mark ") and its display-column width. The prefix width is
			// measured from parsed cells, so a wide marker glyph shifts the label right by its real width.
			string mark = Parsing.MarkupParser.Escape(Checked ? _selectedCharacter : _unselectedCharacter);
			// Only tint the selected mark with CheckmarkColor when ENABLED. A disabled radio must dim its
			// mark to the same (grey) foreground as its label — otherwise a disabled+selected radio paints a
			// vivid CheckmarkColor (cyan) glyph that looks active. Leaving the mark un-tinted lets it inherit
			// the resolved disabled foreground applied by the surrounding paint.
			string markDisplay = Checked && _isEnabled
				? $"[{CheckmarkColor.ToMarkup()}]{mark}[/]"
				: mark;
			string markerPrefix = $"{markDisplay} ";
			int markerPrefixWidth = MeasureMarkerPrefixWidth();

			// Wrap the label to the columns remaining after the marker prefix (when Wrap is enabled).
			int labelWidth = targetWidth - markerPrefixWidth;
			List<string> labelLines;
			if (_wrap && labelWidth > 0)
			{
				labelLines = WrapLabelLines(labelWidth);
			}
			else
			{
				labelLines = new List<string> { Parsing.MarkupParser.Escape(_label) };
			}
			int lineCount = Math.Max(1, labelLines.Count);

			// Content width = marker prefix + widest label line (in display columns), capped to target.
			int widestLabelLine = 0;
			foreach (var line in labelLines)
				widestLabelLine = Math.Max(widestLabelLine, Parsing.MarkupParser.StripLength(line));
			int radioWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch ? targetWidth : markerPrefixWidth + widestLabelLine);
			radioWidth = Math.Min(Math.Max(markerPrefixWidth + widestLabelLine, radioWidth), targetWidth);

			// Horizontal alignment offset (mirrors CheckboxControl).
			int alignOffset = 0;
			if (radioWidth < targetWidth)
			{
				switch (HorizontalAlignment)
				{
					case HorizontalAlignment.Center:
						alignOffset = (targetWidth - radioWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						alignOffset = targetWidth - radioWidth;
						break;
				}
			}

			int startX = bounds.X + Margin.Left + alignOffset;

			// Vertical alignment: offset the block within a taller box.
			int contentHeight = bounds.Height - Margin.Top - Margin.Bottom;
			int startY = bounds.Y + Margin.Top;
			if (contentHeight > lineCount)
			{
				switch (VerticalAlignment)
				{
					case VerticalAlignment.Center:
						startY += (contentHeight - lineCount) / 2;
						break;
					case VerticalAlignment.Bottom:
						startY += contentHeight - lineCount;
						break;
				}
			}

			var cellBg = backgroundColor; // Transparent when no explicit value

			// Row 0: marker prefix + first label line.
			for (int li = 0; li < lineCount; li++)
			{
				int rowY = startY + li;
				if (rowY < clipRect.Y || rowY >= clipRect.Bottom || rowY >= bounds.Bottom)
					continue;

				if (li == 0)
				{
					// Marker prefix.
					var prefixCells = Parsing.MarkupParser.Parse(markerPrefix, foregroundColor, cellBg);
					buffer.WriteCellsClipped(startX, rowY, prefixCells, clipRect);

					// First label line after the prefix.
					var labelCells = Parsing.MarkupParser.Parse(labelLines[0], foregroundColor, cellBg);
					buffer.WriteCellsClipped(startX + markerPrefixWidth, rowY, labelCells, clipRect);
				}
				else
				{
					// Continuation lines: hanging indent under the label (past the marker prefix).
					var labelCells = Parsing.MarkupParser.Parse(labelLines[li], foregroundColor, cellBg);
					buffer.WriteCellsClipped(startX + markerPrefixWidth, rowY, labelCells, clipRect);
				}
			}
		}

		#endregion
	}
}

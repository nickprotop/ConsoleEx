// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using Spectre.Console.Rendering;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that renders a bordered panel with content.
	/// Wraps Spectre.Console's Panel with SharpConsoleUI patterns.
	/// </summary>
	public class PanelControl : BaseControl, IMouseAwareControl
	{
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private int? _height;

		// Panel-specific properties
		private IRenderable? _content;
		private BorderStyle _borderStyle = BorderStyle.Single;
		private Color? _borderColorValue;
		private string? _header;
		private Justify _headerAlignment = Justify.Left;
		private Spectre.Console.Padding _padding = new Spectre.Console.Padding(1, 0, 1, 0);
		private bool _useSafeBorder = false;

		// Mouse interaction state
		private bool _wantsMouseEvents = true;
		private bool _canFocusWithMouse = false;
		private bool _isMouseInside = false;
		private DateTime _lastClickTime = DateTime.MinValue;
		private int _clickCount = 0;

		/// <summary>
		/// Initializes a new instance of the <see cref="PanelControl"/> class.
		/// </summary>
		public PanelControl()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PanelControl"/> class with content.
		/// </summary>
		/// <param name="content">The content to display inside the panel.</param>
		public PanelControl(IRenderable content)
		{
			_content = content;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PanelControl"/> class with text content.
		/// </summary>
		/// <param name="text">The text to display inside the panel (supports Spectre markup).</param>
		public PanelControl(string text)
		{
			_content = new Markup(text);
		}

		#region Properties

		/// <inheritdoc/>
		public override int? ContentWidth
		{
			get
			{
				var panel = CreateSpectrePanel(null, null, Color.Black, Color.White);
				if (panel == null) return Margin.Left + Margin.Right;

				var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(panel, Width ?? 80, null, Color.Black);

				int maxLength = 0;
				foreach (var line in content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength + Margin.Left + Margin.Right;
			}
		}

		/// <summary>
		/// Gets or sets the background color.
		/// When null, inherits from the container.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set => PropertySetterHelper.SetColorProperty(ref _backgroundColorValue, value, Container);
		}

		/// <summary>
		/// Gets or sets the foreground color.
		/// When null, inherits from the container.
		/// </summary>
		public Color? ForegroundColor
		{
			get => _foregroundColorValue;
			set => PropertySetterHelper.SetColorProperty(ref _foregroundColorValue, value, Container);
		}

		/// <summary>
		/// Gets or sets the explicit height of the panel.
		/// When set, the panel border will render at this height.
		/// When null and VerticalAlignment is Fill, the panel stretches to fill available height.
		/// </summary>
		public int? Height
		{
			get => _height;
			set => PropertySetterHelper.SetDimensionProperty(ref _height, value, Container);
		}

		#endregion

		#region Panel-specific Properties

		/// <summary>
		/// Gets or sets the content to display inside the panel.
		/// </summary>
		public IRenderable? Content
		{
			get => _content;
			set
			{
				_content = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the border style for the panel.
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set => PropertySetterHelper.SetEnumProperty(ref _borderStyle, value, Container);
		}

		/// <summary>
		/// Gets or sets the border color.
		/// When null, uses the resolved foreground color.
		/// </summary>
		public Color? BorderColor
		{
			get => _borderColorValue;
			set => PropertySetterHelper.SetColorProperty(ref _borderColorValue, value, Container);
		}

		/// <summary>
		/// Gets or sets the header text displayed at the top of the panel border.
		/// </summary>
		public string? Header
		{
			get => _header;
			set => PropertySetterHelper.SetStringProperty(ref _header!, value!, Container);
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the header text.
		/// </summary>
		public Justify HeaderAlignment
		{
			get => _headerAlignment;
			set
			{
				_headerAlignment = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the padding inside the panel border.
		/// </summary>
		public Spectre.Console.Padding Padding
		{
			get => _padding;
			set
			{
				_padding = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether to use safe border characters for better terminal compatibility.
		/// </summary>
		public bool UseSafeBorder
		{
			get => _useSafeBorder;
			set => PropertySetterHelper.SetBoolProperty(ref _useSafeBorder, value, Container);
		}

		#endregion

		#region IMouseAwareControl Properties

		/// <inheritdoc/>
		public bool WantsMouseEvents
		{
			get => _wantsMouseEvents;
			set
			{
				if (_wantsMouseEvents == value) return;
				_wantsMouseEvents = value;
			}
		}

		/// <inheritdoc/>
		public bool CanFocusWithMouse
		{
			get => _canFocusWithMouse;
			set
			{
				if (_canFocusWithMouse == value) return;
				_canFocusWithMouse = value;
			}
		}

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		#endregion

		#region Private Methods

		/// <summary>
		/// Converts SharpConsoleUI BorderStyle to Spectre.Console BoxBorder.
		/// </summary>
		private static BoxBorder ConvertBorderStyle(BorderStyle style) => style switch
		{
			BorderStyle.DoubleLine => BoxBorder.Double,
			BorderStyle.Single => BoxBorder.Square,
			BorderStyle.Rounded => BoxBorder.Rounded,
			BorderStyle.None => BoxBorder.None,
			_ => BoxBorder.Square
		};

		/// <summary>
		/// Creates the underlying Spectre.Console Panel with current settings.
		/// </summary>
		/// <param name="renderWidth">Optional width for rendering (overrides Width property).</param>
		/// <param name="renderHeight">Optional height for rendering (for Fill alignment).</param>
		/// <param name="bgColor">Resolved background color.</param>
		/// <param name="fgColor">Resolved foreground color.</param>
		private Panel? CreateSpectrePanel(int? renderWidth, int? renderHeight, Color bgColor, Color fgColor)
		{
			if (_content == null) return null;

			var borderColor = _borderColorValue ?? fgColor;

			var panel = new Panel(_content)
			{
				Border = ConvertBorderStyle(_borderStyle),
				BorderStyle = new Style(borderColor, bgColor),
				Expand = true, // Always true - our control system handles width constraints
				Padding = _padding,
				UseSafeBorder = _useSafeBorder
			};

			// Set explicit height if provided (already accounts for margins)
			if (renderHeight.HasValue)
			{
				panel.Height = renderHeight.Value;
			}

			if (!string.IsNullOrEmpty(_header))
			{
				panel.Header = new PanelHeader(_header, _headerAlignment);
			}

			return panel;
		}

		#endregion

		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!WantsMouseEvents)
				return false;

			if (args.Handled)
				return false;

			// Handle mouse leave
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				if (_isMouseInside)
				{
					_isMouseInside = false;
					MouseLeave?.Invoke(this, args);
					Container?.Invalidate(true);
				}
				return true;
			}

			// Handle mouse enter
			if (!_isMouseInside && args.HasFlag(MouseFlags.ReportMousePosition))
			{
				_isMouseInside = true;
				MouseEnter?.Invoke(this, args);
				Container?.Invalidate(true);
			}

			// Scroll events - ALWAYS bubble up (don't consume)
			if (args.HasFlag(MouseFlags.WheeledUp) || args.HasFlag(MouseFlags.WheeledDown) ||
				args.HasFlag(MouseFlags.WheeledLeft) || args.HasFlag(MouseFlags.WheeledRight))
			{
				return false;  // Let scroll events bubble to parent
			}

			// Handle driver-provided double-click (preferred method)
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				// Reset tracking state since driver handled the gesture
				_clickCount = 0;
				_lastClickTime = DateTime.MinValue;

				MouseDoubleClick?.Invoke(this, args);
				args.Handled = true;
				Container?.Invalidate(true);
				return true;
			}

			// Handle clicks with manual double-click detection (fallback)
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				var now = DateTime.UtcNow;
				var timeSince = (now - _lastClickTime).TotalMilliseconds;

				// Check for double-click before updating state
				bool isDoubleClick = timeSince <= ControlDefaults.DefaultDoubleClickThresholdMs &&
									_clickCount == 1;

				// Update tracking state
				if (isDoubleClick)
				{
					_clickCount = 0;
					_lastClickTime = DateTime.MinValue;
					MouseDoubleClick?.Invoke(this, args);
				}
				else
				{
					_clickCount = 1;
					_lastClickTime = now;
					MouseClick?.Invoke(this, args);
				}

				args.Handled = true;
				Container?.Invalidate(true);
				return true;
			}

			// Handle mouse movement
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return true;
			}

			return false;
		}

		#endregion

		#region BaseControl Overrides

		/// <summary>
		/// Called during Dispose before Container is set to null.
		/// Clears mouse event handlers to prevent memory leaks.
		/// </summary>
		protected override void OnDisposing()
		{
			MouseClick = null;
			MouseDoubleClick = null;
			MouseEnter = null;
			MouseLeave = null;
			MouseMove = null;
		}

		/// <summary>
		/// Creates a new builder for configuring a PanelControl
		/// </summary>
		/// <returns>A new builder instance</returns>
		public static Builders.PanelBuilder Create()
		{
			return new Builders.PanelBuilder();
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? Color.Black;

			var panel = CreateSpectrePanel(null, null, bgColor, Color.White);
			if (panel == null)
				return new System.Drawing.Size(Margin.Left + Margin.Right, Margin.Top + Margin.Bottom);

			// Reuse ContentWidth for width
			int width = ContentWidth ?? 0;

			// Calculate height
			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(panel, Width ?? 80, null, bgColor);
			int height = content.Count + Margin.Top + Margin.Bottom;

			return new System.Drawing.Size(width, height);
		}

		/// <summary>
		/// Sets the content to display inside the panel using text (supports Spectre markup).
		/// </summary>
		/// <param name="text">The text to display.</param>
		public void SetContent(string text)
		{
			_content = new Markup(text);
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets the content to display inside the panel.
		/// </summary>
		/// <param name="renderable">The renderable to display.</param>
		public void SetContent(IRenderable renderable)
		{
			_content = renderable;
			Container?.Invalidate(true);
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? Color.Black;
			Color fgColor = _foregroundColorValue ?? Container?.ForegroundColor ?? Color.White;

			var panel = CreateSpectrePanel(null, null, bgColor, fgColor);
			if (panel == null)
			{
				return new LayoutSize(
					Math.Clamp(Margin.Left + Margin.Right, constraints.MinWidth, constraints.MaxWidth),
					Math.Clamp(Margin.Top + Margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
				);
			}

			// If explicit width is set, it represents total control width (including margins)
			// Otherwise, use available width from constraints
			int totalWidth = Width ?? constraints.MaxWidth;
			int targetWidth = totalWidth - Margin.Left - Margin.Right;

			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(panel, targetWidth, null, bgColor);

			// If explicit width is set, use it; otherwise measure actual content
			int width = Width ?? (content.Count > 0 ? content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line)) + Margin.Left + Margin.Right : Margin.Left + Margin.Right);
			int height = content.Count + Margin.Top + Margin.Bottom;

			// If explicit height is set, use that
			if (_height.HasValue)
			{
				height = _height.Value;
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

			// Resolve colors using standard fallback chain
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? defaultBg;
			Color fgColor = _foregroundColorValue ?? Container?.ForegroundColor ?? defaultFg;

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			int targetHeight = bounds.Height - Margin.Top - Margin.Bottom;

			if (targetWidth <= 0 || targetHeight <= 0) return;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			// Determine render height: use targetHeight if explicit Height is set or Fill alignment
			int? panelRenderHeight = null;
			if (_height.HasValue || VerticalAlignment == VerticalAlignment.Fill)
			{
				panelRenderHeight = targetHeight;
			}

			// Always use targetWidth for rendering (bounds.Width already accounts for explicit _width)
			var panel = CreateSpectrePanel(targetWidth, panelRenderHeight, bgColor, fgColor);
			if (panel != null)
			{
				var renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(panel, targetWidth, panelRenderHeight, bgColor);

				int contentHeight = renderedContent.Count;
				int availableHeight = targetHeight;

				for (int i = 0; i < Math.Min(contentHeight, availableHeight); i++)
				{
					int paintY = startY + i;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
					{
						// Fill left margin
						if (Margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, paintY, Margin.Left, 1), ' ', fgColor, bgColor);
						}

						// Calculate alignment
						int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent[i]);
						int alignOffset = 0;
						if (lineWidth < targetWidth)
						{
							switch (HorizontalAlignment)
							{
								case HorizontalAlignment.Center:
									alignOffset = (targetWidth - lineWidth) / 2;
									break;
								case HorizontalAlignment.Right:
									alignOffset = targetWidth - lineWidth;
									break;
							}
						}

						// Fill left alignment padding
						if (alignOffset > 0)
						{
							buffer.FillRect(new LayoutRect(startX, paintY, alignOffset, 1), ' ', fgColor, bgColor);
						}

						// Parse and write the content line
						var cells = AnsiParser.Parse(renderedContent[i], fgColor, bgColor);
						buffer.WriteCellsClipped(startX + alignOffset, paintY, cells, clipRect);

						// Fill right padding
						int rightPadStart = startX + alignOffset + lineWidth;
						int rightPadWidth = bounds.Right - rightPadStart - Margin.Right;
						if (rightPadWidth > 0)
						{
							buffer.FillRect(new LayoutRect(rightPadStart, paintY, rightPadWidth, 1), ' ', fgColor, bgColor);
						}

						// Fill right margin
						if (Margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - Margin.Right, paintY, Margin.Right, 1), ' ', fgColor, bgColor);
						}
					}
				}

				// Fill any remaining height after content
				for (int y = startY + contentHeight; y < bounds.Bottom - Margin.Bottom; y++)
				{
					if (y >= clipRect.Y && y < clipRect.Bottom)
					{
						buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
					}
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, bgColor);
		}

		#endregion
	}
}

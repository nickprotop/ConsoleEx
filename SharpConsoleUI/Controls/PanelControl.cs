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
	public class PanelControl : IWindowControl, IDOMPaintable, IMouseAwareControl
	{
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;
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

		#region IWindowControl Properties

		/// <inheritdoc/>
		public int? ActualWidth
		{
			get
			{
				var panel = CreateSpectrePanel(null, null, Color.Black, Color.White);
				if (panel == null) return _margin.Left + _margin.Right;

				var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(panel, _width ?? 80, null, Color.Black);

				int maxLength = 0;
				foreach (var line in content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength + _margin.Left + _margin.Right;
			}
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _horizontalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _verticalAlignment, value, Container);
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

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
		}

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

		/// <inheritdoc/>
		public bool Visible
		{
			get => _visible;
			set => PropertySetterHelper.SetBoolProperty(ref _visible, value, Container);
		}

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set => PropertySetterHelper.SetDimensionProperty(ref _width, value, Container);
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

			// Set explicit height if provided (for Fill alignment) or from Height property
			var heightToUse = renderHeight ?? _height;
			if (heightToUse.HasValue)
			{
				panel.Height = heightToUse.Value;
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
			}

			return false;
		}

		#endregion

		#region IWindowControl Implementation

		/// <inheritdoc/>
		public void Dispose()
		{
			// Clear mouse event handlers to prevent memory leaks
			MouseClick = null;
			MouseDoubleClick = null;
			MouseEnter = null;
			MouseLeave = null;
			MouseMove = null;

			Container = null;
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
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? Color.Black;

			var panel = CreateSpectrePanel(null, null, bgColor, Color.White);
			if (panel == null)
				return new System.Drawing.Size(_margin.Left + _margin.Right, _margin.Top + _margin.Bottom);

			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(panel, _width ?? 80, null, bgColor);

			int maxWidth = content.Count > 0 ? content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line)) : 0;
			return new System.Drawing.Size(
				maxWidth + _margin.Left + _margin.Right,
				content.Count + _margin.Top + _margin.Bottom
			);
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
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? Color.Black;
			Color fgColor = _foregroundColorValue ?? Container?.ForegroundColor ?? Color.White;

			var panel = CreateSpectrePanel(null, null, bgColor, fgColor);
			if (panel == null)
			{
				return new LayoutSize(
					Math.Clamp(_margin.Left + _margin.Right, constraints.MinWidth, constraints.MaxWidth),
					Math.Clamp(_margin.Top + _margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
				);
			}

			int targetWidth = _width ?? constraints.MaxWidth - _margin.Left - _margin.Right;

			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(panel, targetWidth, null, bgColor);

			int maxWidth = content.Count > 0 ? content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line)) : 0;
			int width = maxWidth + _margin.Left + _margin.Right;
			int height = content.Count + _margin.Top + _margin.Bottom;

			// If explicit height is set, use that
			if (_height.HasValue)
			{
				height = _height.Value + _margin.Top + _margin.Bottom;
			}

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			// Resolve colors using standard fallback chain
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? defaultBg;
			Color fgColor = _foregroundColorValue ?? Container?.ForegroundColor ?? defaultFg;

			int targetWidth = bounds.Width - _margin.Left - _margin.Right;
			int targetHeight = bounds.Height - _margin.Top - _margin.Bottom;

			if (targetWidth <= 0 || targetHeight <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			// Determine render height: use explicit Height, or if Fill alignment, use available height
			int? panelRenderHeight = _height;
			if (!panelRenderHeight.HasValue && _verticalAlignment == VerticalAlignment.Fill)
			{
				panelRenderHeight = targetHeight;
			}

			var panel = CreateSpectrePanel(_width ?? targetWidth, panelRenderHeight, bgColor, fgColor);
			if (panel != null)
			{
				int renderWidth = _width ?? targetWidth;
				var renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(panel, renderWidth, panelRenderHeight, bgColor);

				int contentHeight = renderedContent.Count;
				int availableHeight = targetHeight;

				for (int i = 0; i < Math.Min(contentHeight, availableHeight); i++)
				{
					int paintY = startY + i;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
					{
						// Fill left margin
						if (_margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, bgColor);
						}

						// Calculate alignment
						int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent[i]);
						int alignOffset = 0;
						if (lineWidth < targetWidth)
						{
							switch (_horizontalAlignment)
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
						int rightPadWidth = bounds.Right - rightPadStart - _margin.Right;
						if (rightPadWidth > 0)
						{
							buffer.FillRect(new LayoutRect(rightPadStart, paintY, rightPadWidth, 1), ' ', fgColor, bgColor);
						}

						// Fill right margin
						if (_margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, paintY, _margin.Right, 1), ' ', fgColor, bgColor);
						}
					}
				}

				// Fill any remaining height after content
				for (int y = startY + contentHeight; y < bounds.Bottom - _margin.Bottom; y++)
				{
					if (y >= clipRect.Y && y < clipRect.Bottom)
					{
						buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
					}
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - _margin.Bottom, fgColor, bgColor);
		}

		#endregion
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that renders a bordered panel with content.
	/// Renders directly to CharacterBuffer using BoxChars and MarkupParser.
	/// </summary>
	public class PanelControl : BaseControl, IMouseAwareControl
	{
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private int? _height;

		// Panel-specific properties
		private string? _content;
		private BorderStyle _borderStyle = BorderStyle.Single;
		private Color? _borderColorValue;
		private string? _header;
		private TextJustification _headerAlignment = TextJustification.Left;
		private Padding _padding = new Padding(1, 0, 1, 0);
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
		/// Initializes a new instance of the <see cref="PanelControl"/> class with text content.
		/// </summary>
		/// <param name="text">The text to display inside the panel (supports markup).</param>
		public PanelControl(string text)
		{
			_content = text;
		}

		#region Properties

		/// <inheritdoc/>
		public override int? ContentWidth
		{
			get
			{
				int borderWidth = _borderStyle == BorderStyle.None ? 0 : 2;
				int innerPad = _padding.Left + _padding.Right;

				if (string.IsNullOrEmpty(_content))
					return borderWidth + innerPad + Margin.Left + Margin.Right;

				int contentWidth = MarkupParser.StripLength(_content);
				return contentWidth + borderWidth + innerPad + Margin.Left + Margin.Right;
			}
		}

		/// <summary>
		/// Gets or sets the background color.
		/// When null, inherits from the container.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set => SetProperty(ref _backgroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the foreground color.
		/// When null, inherits from the container.
		/// </summary>
		public Color? ForegroundColor
		{
			get => _foregroundColorValue;
			set => SetProperty(ref _foregroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the explicit height of the panel.
		/// When set, the panel border will render at this height.
		/// When null and VerticalAlignment is Fill, the panel stretches to fill available height.
		/// </summary>
		public override int? Height
		{
			get => _height;
			set => SetProperty(ref _height, value, v => v.HasValue ? Math.Max(0, v.Value) : v);
		}

		#endregion

		#region Panel-specific Properties

		/// <summary>
		/// Gets or sets the content to display inside the panel (supports markup).
		/// </summary>
		public string? Content
		{
			get => _content;
			set => SetProperty(ref _content, value);
		}

		/// <summary>
		/// Gets or sets the border style for the panel.
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set => SetProperty(ref _borderStyle, value);
		}

		/// <summary>
		/// Gets or sets the border color.
		/// When null, uses the resolved foreground color.
		/// </summary>
		public Color? BorderColor
		{
			get => _borderColorValue;
			set => SetProperty(ref _borderColorValue, value);
		}

		/// <summary>
		/// Gets or sets the header text displayed at the top of the panel border.
		/// </summary>
		public string? Header
		{
			get => _header;
			set => SetProperty(ref _header, value);
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the header text.
		/// </summary>
		public TextJustification HeaderAlignment
		{
			get => _headerAlignment;
			set => SetProperty(ref _headerAlignment, value);
		}

		/// <summary>
		/// Gets or sets the padding inside the panel border.
		/// </summary>
		public Padding Padding
		{
			get => _padding;
			set => SetProperty(ref _padding, value);
		}

		/// <summary>
		/// Gets or sets whether to use safe border characters for better terminal compatibility.
		/// </summary>
		public bool UseSafeBorder
		{
			get => _useSafeBorder;
			set => SetProperty(ref _useSafeBorder, value);
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
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		#endregion

		#region Private Rendering Methods

		private BoxChars GetBoxChars()
		{
			if (_useSafeBorder)
				return BoxChars.Ascii;
			return BoxChars.FromBorderStyle(_borderStyle);
		}

		/// <summary>
		/// Draws the top border line with optional header text embedded.
		/// </summary>
		private void DrawTopBorder(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			int innerWidth = width - 2; // minus corners

			// Left corner
			if (x >= clipRect.X && x < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(x, y, box.TopLeft, borderColor, cellBg);
			}

			if (string.IsNullOrEmpty(_header) || innerWidth < 4)
			{
				// No header — fill with horizontal chars
				for (int i = 0; i < innerWidth; i++)
				{
					int px = x + 1 + i;
					if (px >= clipRect.X && px < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(px, y, box.Horizontal, borderColor, cellBg);
					}
				}
			}
			else
			{
				// Parse header and calculate position
				var headerCells = MarkupParser.Parse(_header, borderColor, bgColor);
				int headerLen = headerCells.Count;
				int headerWithSpaces = headerLen + 2; // space before and after

				if (headerWithSpaces > innerWidth)
				{
					// Header too long — just fill with horizontal
					for (int i = 0; i < innerWidth; i++)
					{
						int px = x + 1 + i;
						if (px >= clipRect.X && px < clipRect.Right)
						{
							var cellBg = bgColor;
							buffer.SetNarrowCell(px, y, box.Horizontal, borderColor, cellBg);
						}
					}
				}
				else
				{
					int dashSpace = innerWidth - headerWithSpaces;
					int leftDashes, rightDashes;

					switch (_headerAlignment)
					{
						case TextJustification.Center:
							leftDashes = dashSpace / 2;
							rightDashes = dashSpace - leftDashes;
							break;
						case TextJustification.Right:
							leftDashes = dashSpace - 1;
							rightDashes = 1;
							break;
						default: // Left
							leftDashes = 1;
							rightDashes = dashSpace - 1;
							break;
					}

					int writeX = x + 1;

					// Left dashes
					for (int i = 0; i < leftDashes; i++)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = bgColor;
							buffer.SetNarrowCell(writeX, y, box.Horizontal, borderColor, cellBg);
						}
						writeX++;
					}

					// Space + header + space
					if (writeX >= clipRect.X && writeX < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(writeX, y, ' ', borderColor, cellBg);
					}
					writeX++;

					foreach (var cell in headerCells)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							buffer.SetCell(writeX, y, cell);
						}
						writeX++;
					}

					if (writeX >= clipRect.X && writeX < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(writeX, y, ' ', borderColor, cellBg);
					}
					writeX++;

					// Right dashes
					for (int i = 0; i < rightDashes; i++)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = bgColor;
							buffer.SetNarrowCell(writeX, y, box.Horizontal, borderColor, cellBg);
						}
						writeX++;
					}
				}
			}

			// Right corner
			int rightX = x + width - 1;
			if (rightX >= clipRect.X && rightX < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(rightX, y, box.TopRight, borderColor, cellBg);
			}
		}

		/// <summary>
		/// Draws the bottom border line.
		/// </summary>
		private void DrawBottomBorder(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			if (x >= clipRect.X && x < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(x, y, box.BottomLeft, borderColor, cellBg);
			}

			int innerWidth = width - 2;
			for (int i = 0; i < innerWidth; i++)
			{
				int px = x + 1 + i;
				if (px >= clipRect.X && px < clipRect.Right)
				{
					var cellBg = bgColor;
					buffer.SetNarrowCell(px, y, box.Horizontal, borderColor, cellBg);
				}
			}

			int rightX = x + width - 1;
			if (rightX >= clipRect.X && rightX < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(rightX, y, box.BottomRight, borderColor, cellBg);
			}
		}

		/// <summary>
		/// Draws a row with vertical borders and content between them.
		/// </summary>
		private void DrawBorderedRow(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor, List<Cell>? contentCells = null, int contentOffset = 0)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			int innerWidth = width - 2;

			// Left border
			if (x >= clipRect.X && x < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(x, y, box.Vertical, borderColor, cellBg);
			}

			// Inner area
			int innerX = x + 1;
			for (int i = 0; i < innerWidth; i++)
			{
				int px = innerX + i;
				if (px >= clipRect.X && px < clipRect.Right)
				{
					int contentIdx = i - _padding.Left;
					if (contentCells != null && contentIdx >= 0 && contentIdx < contentCells.Count)
					{
						var cell = contentCells[contentIdx];
						buffer.SetCell(px, y, cell);
					}
					else
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(px, y, ' ', borderColor, cellBg);
					}
				}
			}

			// Right border
			int rightX = x + width - 1;
			if (rightX >= clipRect.X && rightX < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(rightX, y, box.Vertical, borderColor, cellBg);
			}
		}

		/// <summary>
		/// Calculates content lines for the given inner width.
		/// </summary>
		private List<List<Cell>> GetContentLines(int innerContentWidth, Color fgColor, Color bgColor)
		{
			if (string.IsNullOrEmpty(_content) || innerContentWidth <= 0)
				return new List<List<Cell>> { new List<Cell>() };

			return MarkupParser.ParseLines(_content, innerContentWidth, fgColor, bgColor);
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

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				args.Handled = true;
				return true;
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
			MouseRightClick = null;
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
			int width = ContentWidth ?? 0;
			int borderHeight = _borderStyle == BorderStyle.None ? 0 : 2;
			int contentLineCount = 1;

			if (!string.IsNullOrEmpty(_content))
			{
				int innerWidth = (Width ?? 80) - (_borderStyle == BorderStyle.None ? 0 : 2) - _padding.Left - _padding.Right;
				if (innerWidth > 0)
				{
					Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? Color.Black;
					Color fgColor = _foregroundColorValue ?? Container?.ForegroundColor ?? Color.White;
					contentLineCount = GetContentLines(innerWidth, fgColor, bgColor).Count;
				}
			}

			int height = contentLineCount + borderHeight + _padding.Top + _padding.Bottom + Margin.Top + Margin.Bottom;
			return new System.Drawing.Size(width, height);
		}

		/// <summary>
		/// Sets the content to display inside the panel using text (supports markup).
		/// </summary>
		/// <param name="text">The text to display.</param>
		public void SetContent(string text)
		{
			Content = text;
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? Color.Black;
			Color fgColor = _foregroundColorValue ?? Container?.ForegroundColor ?? Color.White;

			bool hasBorder = _borderStyle != BorderStyle.None;
			int borderWidth = hasBorder ? 2 : 0;
			int borderHeight = hasBorder ? 2 : 0;

			int totalWidth = Width ?? constraints.MaxWidth;
			int innerContentWidth = totalWidth - Margin.Left - Margin.Right - borderWidth - _padding.Left - _padding.Right;

			int contentLineCount = 0;
			int maxContentWidth = 0;

			if (!string.IsNullOrEmpty(_content) && innerContentWidth > 0)
			{
				var lines = GetContentLines(innerContentWidth, fgColor, bgColor);
				contentLineCount = lines.Count;
				foreach (var line in lines)
				{
					if (line.Count > maxContentWidth)
						maxContentWidth = line.Count;
				}
			}

			int width;
			if (Width.HasValue)
			{
				width = Width.Value + Margin.Left + Margin.Right;
			}
			else
			{
				width = maxContentWidth + borderWidth + _padding.Left + _padding.Right + Margin.Left + Margin.Right;
			}

			int height = contentLineCount + borderHeight + _padding.Top + _padding.Bottom + Margin.Top + Margin.Bottom;

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
			Color borderColor = _borderColorValue ?? fgColor;
			var effectiveBg = (_backgroundColorValue == null && Container?.HasGradientBackground == true) ? Color.Transparent : bgColor;

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			int targetHeight = bounds.Height - Margin.Top - Margin.Bottom;

			if (targetWidth <= 0 || targetHeight <= 0) return;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);

			bool hasBorder = _borderStyle != BorderStyle.None;
			var box = GetBoxChars();

			// Determine actual render height
			int renderHeight = targetHeight;
			if (_height.HasValue || VerticalAlignment == VerticalAlignment.Fill)
			{
				renderHeight = targetHeight;
			}

			int currentY = startY;

			if (hasBorder)
			{
				// Top border with optional header
				if (currentY < startY + renderHeight)
				{
					// Fill left margin on this row
					if (Margin.Left > 0 && currentY >= clipRect.Y && currentY < clipRect.Bottom)
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, Margin.Left, 1), fgColor, effectiveBg);

					DrawTopBorder(buffer, startX, currentY, targetWidth, clipRect, box, borderColor, effectiveBg);

					// Fill right margin
					if (Margin.Right > 0 && currentY >= clipRect.Y && currentY < clipRect.Bottom)
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, currentY, Margin.Right, 1), fgColor, effectiveBg);

					currentY++;
				}
			}

			// Padding top rows
			for (int i = 0; i < _padding.Top && currentY < startY + renderHeight; i++)
			{
				if (Margin.Left > 0 && currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, Margin.Left, 1), fgColor, effectiveBg);

				if (hasBorder)
					DrawBorderedRow(buffer, startX, currentY, targetWidth, clipRect, box, borderColor, effectiveBg);
				else if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX, currentY, targetWidth, 1), fgColor, effectiveBg);

				if (Margin.Right > 0 && currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, currentY, Margin.Right, 1), fgColor, effectiveBg);

				currentY++;
			}

			// Content rows
			int innerContentWidth = targetWidth - (hasBorder ? 2 : 0) - _padding.Left - _padding.Right;
			var contentLines = GetContentLines(innerContentWidth, fgColor, bgColor);
			int maxContentRows = renderHeight - (hasBorder ? 2 : 0) - _padding.Top - _padding.Bottom;

			for (int i = 0; i < contentLines.Count && i < maxContentRows && currentY < startY + renderHeight; i++)
			{
				if (Margin.Left > 0 && currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, Margin.Left, 1), fgColor, effectiveBg);

				if (hasBorder)
					DrawBorderedRow(buffer, startX, currentY, targetWidth, clipRect, box, borderColor, effectiveBg, contentLines[i]);
				else
				{
					// No border — just draw content with padding
					if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
					{
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX, currentY, targetWidth, 1), fgColor, effectiveBg);
						int contentX = startX + _padding.Left;
						foreach (var cell in contentLines[i])
						{
							if (contentX >= clipRect.X && contentX < clipRect.Right)
								buffer.SetCell(contentX, currentY, cell);
							contentX++;
						}
					}
				}

				if (Margin.Right > 0 && currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, currentY, Margin.Right, 1), fgColor, effectiveBg);

				currentY++;
			}

			// Fill remaining content area (empty rows)
			int bottomBorderRow = startY + renderHeight - (hasBorder ? 1 : 0);
			while (currentY < bottomBorderRow)
			{
				if (Margin.Left > 0 && currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, Margin.Left, 1), fgColor, effectiveBg);

				if (hasBorder)
					DrawBorderedRow(buffer, startX, currentY, targetWidth, clipRect, box, borderColor, effectiveBg);
				else if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX, currentY, targetWidth, 1), fgColor, effectiveBg);

				if (Margin.Right > 0 && currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, currentY, Margin.Right, 1), fgColor, effectiveBg);

				currentY++;
			}

			if (hasBorder && currentY < startY + renderHeight)
			{
				// Bottom border
				if (Margin.Left > 0 && currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, Margin.Left, 1), fgColor, effectiveBg);

				DrawBottomBorder(buffer, startX, currentY, targetWidth, clipRect, box, borderColor, effectiveBg);

				if (Margin.Right > 0 && currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, currentY, Margin.Right, 1), fgColor, effectiveBg);

				currentY++;
			}

			// Fill any remaining height
			while (currentY < bounds.Bottom - Margin.Bottom)
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, bounds.Width, 1), fgColor, effectiveBg);
				currentY++;
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, effectiveBg);
		}

		#endregion
	}
}

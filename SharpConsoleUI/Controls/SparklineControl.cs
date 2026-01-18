// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A vertical column/sparkline graph control for visualizing time-series data.
	/// Displays vertical bars showing historical values over time.
	/// </summary>
	public class SparklineControl : IWindowControl, IDOMPaintable
	{
		private const int DEFAULT_HEIGHT = 8;
		private const int DEFAULT_MAX_DATA_POINTS = 50;
		private static readonly char[] VERTICAL_CHARS = { ' ', '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

		private Color? _backgroundColorValue;
		private Color _barColor = Color.Cyan1;
		private BorderStyle _borderStyle = BorderStyle.None;
		private Color? _borderColor;
		private IContainer? _container;
		private List<double> _dataPoints = new();
		private Color? _foregroundColorValue;
		private int _graphHeight = DEFAULT_HEIGHT;
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private int _maxDataPoints = DEFAULT_MAX_DATA_POINTS;
		private double? _maxValue;
		private double? _minValue;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string? _title;
		private Color? _titleColor;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private bool _visible = true;
		private int? _width;

		/// <summary>
		/// Initializes a new instance of the <see cref="SparklineControl"/> class.
		/// </summary>
		public SparklineControl()
		{
		}

		/// <summary>
		/// Gets or sets the background color of the control.
		/// When null, inherits from the container.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set
			{
				_backgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the color for the bars.
		/// </summary>
		public Color BarColor
		{
			get => _barColor;
			set
			{
				_barColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the border style around the graph.
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set
			{
				_borderStyle = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the border color.
		/// When null, uses the foreground color.
		/// </summary>
		public Color? BorderColor
		{
			get => _borderColor;
			set
			{
				_borderColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets the data points collection.
		/// </summary>
		public IReadOnlyList<double> DataPoints => _dataPoints.AsReadOnly();

		/// <summary>
		/// Gets or sets the foreground color for labels.
		/// When null, inherits from the container.
		/// </summary>
		public Color? ForegroundColor
		{
			get => _foregroundColorValue;
			set
			{
				_foregroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the height of the graph in lines.
		/// </summary>
		public int GraphHeight
		{
			get => _graphHeight;
			set
			{
				_graphHeight = Math.Max(1, value);
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the maximum number of data points to display.
		/// When exceeded, oldest points are removed.
		/// </summary>
		public int MaxDataPoints
		{
			get => _maxDataPoints;
			set
			{
				_maxDataPoints = Math.Max(1, value);
				TrimDataPoints();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the maximum value for the graph scale.
		/// When null, uses the maximum data point value.
		/// </summary>
		public double? MaxValue
		{
			get => _maxValue;
			set
			{
				_maxValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the minimum value for the graph scale.
		/// When null, uses the minimum data point value (or 0 if all positive).
		/// </summary>
		public double? MinValue
		{
			get => _minValue;
			set
			{
				_minValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the optional title displayed at the top of the graph (inside border if present).
		/// </summary>
		public string? Title
		{
			get => _title;
			set
			{
				_title = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the color for the title text.
		/// When null, uses the foreground color.
		/// </summary>
		public Color? TitleColor
		{
			get => _titleColor;
			set
			{
				_titleColor = value;
				Container?.Invalidate(true);
			}
		}

		#region IWindowControl Implementation

		/// <inheritdoc/>
		public int? ActualWidth => _width ?? (_dataPoints.Count + _margin.Left + _margin.Right);

		/// <inheritdoc/>
		public IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _horizontalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _verticalAlignment, value, Container);
		}

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
			set
			{
				var validatedValue = value.HasValue ? Math.Max(1, value.Value) : (int?)null;
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
			_dataPoints.Clear();
			Container = null;
		}

		/// <inheritdoc/>
		public Size GetLogicalContentSize()
		{
			int width = _dataPoints.Count + _margin.Left + _margin.Right;
			int height = _graphHeight + _margin.Top + _margin.Bottom;
			return new Size(width, height);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(false);
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int borderSize = _borderStyle != BorderStyle.None ? 2 : 0;
			int titleHeight = !string.IsNullOrEmpty(_title) ? 1 : 0;
			int width = (_width ?? _dataPoints.Count) + _margin.Left + _margin.Right + borderSize;
			int height = _graphHeight + _margin.Top + _margin.Bottom + borderSize + titleHeight;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			// Resolve colors
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? defaultBg;
			Color fgColor = _foregroundColorValue ?? Container?.ForegroundColor ?? defaultFg;

			// Fill margins with container background
			Color containerBg = Container?.BackgroundColor ?? defaultBg;
			for (int y = bounds.Y; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, containerBg);
				}
			}

			// Calculate content area (after margins, including border and title)
			int borderSize = _borderStyle != BorderStyle.None ? 1 : 0;
			int titleHeight = !string.IsNullOrEmpty(_title) ? 1 : 0;
			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;
			int contentWidth = bounds.Width - _margin.Left - _margin.Right;
			int contentHeight = _graphHeight + (borderSize * 2) + titleHeight;

			// Fill content area with control background
			for (int y = startY; y < startY + contentHeight && y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					int fillX = Math.Max(startX, clipRect.X);
					int fillWidth = Math.Min(startX + contentWidth, clipRect.Right) - fillX;
					if (fillWidth > 0)
					{
						buffer.FillRect(new LayoutRect(fillX, y, fillWidth, 1), ' ', fgColor, bgColor);
					}
				}
			}

			// Draw border if enabled
			if (_borderStyle != BorderStyle.None)
			{
				Color borderColor = _borderColor ?? fgColor;
				DrawBorder(buffer, startX, startY, contentWidth, contentHeight, clipRect, borderColor, bgColor);
			}

			// Draw title if present (inside border, below top border line)
			if (!string.IsNullOrEmpty(_title))
			{
				int titleY = startY + borderSize;
				int titleX = startX + borderSize + 1; // 1 char padding from left border
				int maxTitleWidth = contentWidth - (borderSize * 2) - 2; // 1 char padding on each side

				if (titleY >= clipRect.Y && titleY < clipRect.Bottom && maxTitleWidth > 0)
				{
					// Wrap title with color if TitleColor is set
					string processedTitle = _title;
					if (_titleColor.HasValue)
					{
						string colorName = $"rgb({_titleColor.Value.R},{_titleColor.Value.G},{_titleColor.Value.B})";
						processedTitle = $"[{colorName}]{_title}[/]";
					}

					// Convert markup to ANSI
					var ansiLines = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						processedTitle, maxTitleWidth, 1, false, bgColor, fgColor);

					if (ansiLines.Count > 0)
					{
						string ansiTitle = ansiLines[0];

						// Parse ANSI string into cells
						var cells = AnsiParser.Parse(ansiTitle, fgColor, bgColor);

						// Write cells to buffer
						buffer.WriteCellsClipped(titleX, titleY, cells, clipRect);
					}
				}
			}

			if (_dataPoints.Count == 0)
				return;

			// Calculate scale
			double min = _minValue ?? Math.Min(0, _dataPoints.Min());
			double max = _maxValue ?? Math.Max(_dataPoints.Max(), min + 1.0);
			double range = max - min;

			if (range < 0.001)
				range = 1.0;

			// Calculate graph area (inside border if present, below title if present)
			int graphStartX = startX + borderSize;
			int graphStartY = startY + borderSize + titleHeight;
			int graphWidth = contentWidth - (borderSize * 2);
			int graphBottom = graphStartY + _graphHeight - 1;

			// Determine which data points to display
			int availableWidth = graphWidth;
			int displayCount = Math.Min(_dataPoints.Count, availableWidth);
			int startIndex = Math.Max(0, _dataPoints.Count - displayCount);

			// Paint vertical bars
			for (int i = 0; i < displayCount; i++)
			{
				int dataIndex = startIndex + i;
				if (dataIndex >= _dataPoints.Count)
					break;

				double value = _dataPoints[dataIndex];
				double normalized = (value - min) / range; // 0.0 to 1.0
				double barHeight = normalized * _graphHeight;

				int paintX = graphStartX + i;
				if (paintX < clipRect.X || paintX >= clipRect.Right)
					continue;

				// Paint vertical bar from bottom to top
				for (int y = 0; y < _graphHeight; y++)
				{
					int paintY = graphBottom - y;
					if (paintY < clipRect.Y || paintY >= clipRect.Bottom)
						continue;

					// Calculate fill character based on how much of this row should be filled
					double rowTopThreshold = (y + 1);
					double rowBottomThreshold = y;

					char displayChar;
					if (barHeight >= rowTopThreshold)
					{
						// Fully filled row
						displayChar = VERTICAL_CHARS[8];
					}
					else if (barHeight > rowBottomThreshold)
					{
						// Partially filled row
						double fraction = barHeight - rowBottomThreshold;
						int charIndex = (int)Math.Round(fraction * 8);
						charIndex = Math.Clamp(charIndex, 0, 8);
						displayChar = VERTICAL_CHARS[charIndex];
					}
					else
					{
						// Empty row
						displayChar = VERTICAL_CHARS[0];
					}

					Color cellColor = displayChar == ' ' ? Color.Grey19 : _barColor;
					buffer.SetCell(paintX, paintY, displayChar, cellColor, bgColor);
				}
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Adds a new data point to the graph.
		/// If the maximum number of points is exceeded, the oldest point is removed.
		/// </summary>
		public void AddDataPoint(double value)
		{
			_dataPoints.Add(value);
			TrimDataPoints();
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Clears all data points from the graph.
		/// </summary>
		public void ClearDataPoints()
		{
			_dataPoints.Clear();
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets the data points for the graph, replacing any existing points.
		/// </summary>
		public void SetDataPoints(IEnumerable<double> dataPoints)
		{
			_dataPoints = new List<double>(dataPoints);
			TrimDataPoints();
			Container?.Invalidate(true);
		}

		#endregion

		#region Private Helper Methods

		private void TrimDataPoints()
		{
			while (_dataPoints.Count > _maxDataPoints)
			{
				_dataPoints.RemoveAt(0);
			}
		}

		private void DrawBorder(CharacterBuffer buffer, int x, int y, int width, int height, LayoutRect clipRect, Color borderColor, Color bgColor)
		{
			if (_borderStyle == BorderStyle.None || width < 2 || height < 2)
				return;

			// Get border characters based on style
			char topLeft, topRight, bottomLeft, bottomRight, horizontal, vertical;

			switch (_borderStyle)
			{
				case BorderStyle.Single:
					topLeft = '┌'; topRight = '┐'; bottomLeft = '└'; bottomRight = '┘';
					horizontal = '─'; vertical = '│';
					break;
				case BorderStyle.Rounded:
					topLeft = '╭'; topRight = '╮'; bottomLeft = '╰'; bottomRight = '╯';
					horizontal = '─'; vertical = '│';
					break;
				case BorderStyle.DoubleLine:
					topLeft = '╔'; topRight = '╗'; bottomLeft = '╚'; bottomRight = '╝';
					horizontal = '═'; vertical = '║';
					break;
				default:
					return;
			}

			// Draw top border
			if (y >= clipRect.Y && y < clipRect.Bottom)
			{
				if (x >= clipRect.X && x < clipRect.Right)
					buffer.SetCell(x, y, topLeft, borderColor, bgColor);

				for (int i = 1; i < width - 1; i++)
				{
					int drawX = x + i;
					if (drawX >= clipRect.X && drawX < clipRect.Right)
						buffer.SetCell(drawX, y, horizontal, borderColor, bgColor);
				}

				if (x + width - 1 >= clipRect.X && x + width - 1 < clipRect.Right)
					buffer.SetCell(x + width - 1, y, topRight, borderColor, bgColor);
			}

			// Draw bottom border
			int bottomY = y + height - 1;
			if (bottomY >= clipRect.Y && bottomY < clipRect.Bottom)
			{
				if (x >= clipRect.X && x < clipRect.Right)
					buffer.SetCell(x, bottomY, bottomLeft, borderColor, bgColor);

				for (int i = 1; i < width - 1; i++)
				{
					int drawX = x + i;
					if (drawX >= clipRect.X && drawX < clipRect.Right)
						buffer.SetCell(drawX, bottomY, horizontal, borderColor, bgColor);
				}

				if (x + width - 1 >= clipRect.X && x + width - 1 < clipRect.Right)
					buffer.SetCell(x + width - 1, bottomY, bottomRight, borderColor, bgColor);
			}

			// Draw left and right borders
			for (int i = 1; i < height - 1; i++)
			{
				int drawY = y + i;
				if (drawY >= clipRect.Y && drawY < clipRect.Bottom)
				{
					if (x >= clipRect.X && x < clipRect.Right)
						buffer.SetCell(x, drawY, vertical, borderColor, bgColor);

					if (x + width - 1 >= clipRect.X && x + width - 1 < clipRect.Right)
						buffer.SetCell(x + width - 1, drawY, vertical, borderColor, bgColor);
				}
			}
		}

		#endregion
	}
}

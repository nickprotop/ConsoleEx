// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
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
	/// Specifies the rendering mode for sparkline bars.
	/// </summary>
	public enum SparklineMode
	{
		/// <summary>
		/// Uses 9-level block characters (▁▂▃▄▅▆▇█) for traditional sparkline appearance.
		/// This is the default mode with 8 vertical levels per character cell.
		/// </summary>
		Block,

		/// <summary>
		/// Uses braille patterns for a smoother, denser appearance.
		/// Provides 5 vertical levels (0-4 dots) using the left column of braille cells.
		/// </summary>
		Braille,

		/// <summary>
		/// Bidirectional mode showing two data series: primary goes up from center,
		/// secondary goes down from center. Uses block characters.
		/// Useful for network upload/download visualization.
		/// </summary>
		Bidirectional,

		/// <summary>
		/// Bidirectional mode using braille patterns for smoother appearance.
		/// Primary series goes up from center, secondary goes down from center.
		/// </summary>
		BidirectionalBraille
	}

	/// <summary>
	/// Specifies the position of the title relative to the sparkline graph.
	/// </summary>
	public enum TitlePosition
	{
		/// <summary>
		/// Title appears above the graph (default behavior).
		/// </summary>
		Top,

		/// <summary>
		/// Title appears below the graph.
		/// </summary>
		Bottom
	}

	/// <summary>
	/// A vertical column/sparkline graph control for visualizing time-series data.
	/// Displays vertical bars showing historical values over time.
	/// </summary>
	public class SparklineControl : BaseControl
	{
		private const int DEFAULT_HEIGHT = 8;
		private const int DEFAULT_MAX_DATA_POINTS = 50;

		private readonly object _dataLock = new();

		// Block mode: 9 levels (0-8) using box drawing vertical bar characters (bottom-up)
		private static readonly char[] VERTICAL_CHARS = { ' ', '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

		// Block mode inverted: 9 levels (0-8) filling from top-down
		// Unicode lacks upper fractional blocks, so we reverse the array conceptually:
		// Index 0=empty, 1=▔(1/8 from top), 4=▀(1/2 from top), 8=█(full)
		// Approximation: ' ', '▔', '▔', '▀', '▀', '▀', '▆', '▇', '█'
		private static readonly char[] VERTICAL_CHARS_INVERTED = { ' ', '▔', '▔', '▀', '▀', '▀', '▆', '▇', '█' };

		// Braille mode: 5 levels (0-4) using left column of braille patterns (bottom-up)
		// Unicode braille: dots are numbered 1,2,3,7 for left column (top to bottom: 1,2,3,7)
		// Dot positions: 1=top-left, 2=mid-upper-left, 3=mid-lower-left, 7=bottom-left
		private static readonly char[] BRAILLE_CHARS =
		{
			'\u2800', // ⠀ empty (0 dots)
			'\u2840', // ⡀ dot 7 (bottom)
			'\u2844', // ⡄ dots 3,7
			'\u2846', // ⡆ dots 2,3,7
			'\u2847', // ⡇ dots 1,2,3,7 (full left column)
		};

		// Braille mode inverted: 5 levels (0-4) filling from top-down
		// Dot positions filled from top: 1, then 1+2, then 1+2+3, then 1+2+3+7
		private static readonly char[] BRAILLE_CHARS_INVERTED =
		{
			'\u2800', // ⠀ empty (0 dots)
			'\u2801', // ⠁ dot 1 (top)
			'\u2803', // ⠃ dots 1,2
			'\u2807', // ⠇ dots 1,2,3
			'\u2847', // ⡇ dots 1,2,3,7 (full left column)
		};

		private Color? _backgroundColorValue;
		private Color _barColor = Color.Cyan1;
		private BorderStyle _borderStyle = BorderStyle.None;
		private SparklineMode _mode = SparklineMode.Block;
		private Color? _borderColor;
		private IContainer? _container;
		private List<double> _dataPoints = new();
		private Color? _foregroundColorValue;
		private int _graphHeight = DEFAULT_HEIGHT;
		private int _maxDataPoints = DEFAULT_MAX_DATA_POINTS;
		private double? _maxValue;
		private double? _minValue;
		private string? _title;
		private Color? _titleColor;
		private TitlePosition _titlePosition = TitlePosition.Top;

		// Secondary data series (for bidirectional mode)
		private List<double> _secondaryDataPoints = new();
		private Color _secondaryBarColor = Color.Green;
		private double? _secondaryMaxValue;

		// Gradient support
		private ColorGradient? _gradient;
		private ColorGradient? _secondaryGradient;

		// Baseline support
		private bool _showBaseline = false;
		private char _baselineChar = '┈'; // U+2508 dotted line
		private Color _baselineColor = Color.Grey50;
		private TitlePosition _baselinePosition = TitlePosition.Bottom;
		private bool _inlineTitleWithBaseline = false;

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
		/// Gets or sets the rendering mode for sparkline bars.
		/// Block mode uses 9-level block characters, Braille mode uses 5-level braille patterns.
		/// </summary>
		public SparklineMode Mode
		{
			get => _mode;
			set
			{
				_mode = value;
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
		public IReadOnlyList<double> DataPoints
		{
			get
			{
				lock (_dataLock)
				{
					return new List<double>(_dataPoints);
				}
			}
		}

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
				lock (_dataLock)
				{
					_maxDataPoints = Math.Max(1, value);
					TrimDataPoints();
				}
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

		/// <summary>
		/// Gets or sets the position of the title relative to the sparkline graph.
		/// Default is Top (title above the graph).
		/// </summary>
		public TitlePosition TitlePosition
		{
			get => _titlePosition;
			set
			{
				_titlePosition = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets the secondary data points collection (for bidirectional mode).
		/// </summary>
		public IReadOnlyList<double> SecondaryDataPoints
		{
			get
			{
				lock (_dataLock)
				{
					return new List<double>(_secondaryDataPoints);
				}
			}
		}

		/// <summary>
		/// Gets or sets the color for the secondary bars (in bidirectional mode).
		/// </summary>
		public Color SecondaryBarColor
		{
			get => _secondaryBarColor;
			set
			{
				_secondaryBarColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the maximum value for the secondary data series scale.
		/// When null, uses the same scale as the primary series or the max secondary data value.
		/// </summary>
		public double? SecondaryMaxValue
		{
			get => _secondaryMaxValue;
			set
			{
				_secondaryMaxValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the color gradient for vertical color interpolation.
		/// When set, each bar column gets a color based on its height.
		/// When null, uses the solid BarColor.
		/// </summary>
		public ColorGradient? Gradient
		{
			get => _gradient;
			set
			{
				_gradient = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the color gradient for the secondary series in bidirectional mode.
		/// When null in bidirectional mode, uses SecondaryBarColor or the primary Gradient.
		/// </summary>
		public ColorGradient? SecondaryGradient
		{
			get => _secondaryGradient;
			set
			{
				_secondaryGradient = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether to show a dotted baseline at the bottom of the graph.
		/// </summary>
		public bool ShowBaseline
		{
			get => _showBaseline;
			set
			{
				_showBaseline = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the character used for the baseline (default: ┈).
		/// </summary>
		public char BaselineChar
		{
			get => _baselineChar;
			set
			{
				_baselineChar = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the color of the baseline (default: Grey50).
		/// </summary>
		public Color BaselineColor
		{
			get => _baselineColor;
			set
			{
				_baselineColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the position of the baseline (Top or Bottom, default: Bottom).
		/// When set to Top, baseline appears above the graph.
		/// </summary>
		public TitlePosition BaselinePosition
		{
			get => _baselinePosition;
			set
			{
				_baselinePosition = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether to show the title inline with the baseline.
		/// Only applies when TitlePosition and BaselinePosition are the same (both Top or both Bottom).
		/// Format: "Title ┈┈┈┈┈┈┈┈┈" (title followed by baseline fill).
		/// </summary>
		public bool InlineTitleWithBaseline
		{
			get => _inlineTitleWithBaseline;
			set
			{
				_inlineTitleWithBaseline = value;
				Container?.Invalidate(true);
			}
		}

		#region IWindowControl Implementation

		/// <inheritdoc/>
		public override int? ContentWidth
		{
			get
			{
				List<double> snapshot;
				lock (_dataLock)
				{
					snapshot = _dataPoints;
				}
				return Width ?? (snapshot.Count + Margin.Left + Margin.Right);
			}
		}

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public override int? Width
		{
			get => base.Width;
			set
			{
				var validatedValue = value.HasValue ? Math.Max(1, value.Value) : (int?)null;
				if (base.Width != validatedValue)
				{
					base.Width = validatedValue;
				}
			}
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			lock (_dataLock)
			{
				_dataPoints.Clear();
			}
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			int width = ContentWidth ?? 0;
			int height = _graphHeight + Margin.Top + Margin.Bottom;
			return new System.Drawing.Size(width, height);
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int borderSize = _borderStyle != BorderStyle.None ? 2 : 0;

			// Check if bidirectional mode
			bool isBidirectional = _mode == SparklineMode.Bidirectional || _mode == SparklineMode.BidirectionalBraille;

			// When title is inline with baseline, don't count title height separately
			bool titleAndBaselineInline = _inlineTitleWithBaseline &&
			                              _titlePosition == _baselinePosition &&
			                              _showBaseline;

			int titleHeight = !string.IsNullOrEmpty(_title) && !titleAndBaselineInline ? 1 : 0;

			// In bidirectional mode, baseline is in the middle (doesn't add height)
			// In standard mode, baseline adds height if not inline with title
			int baselineHeight = _showBaseline && !isBidirectional ? 1 : 0;

			// Calculate minimum width needed for title (visible chars only, not markup)
			int titleWidth = 0;
			if (!string.IsNullOrEmpty(_title))
			{
				titleWidth = Helpers.AnsiConsoleHelper.StripSpectreLength(_title);
			}

			// Width should be max of: explicit width, data points, or title width
			int dataCount;
			lock (_dataLock)
			{
				dataCount = _dataPoints.Count;
			}
			int dataWidth = Width ?? dataCount;
			int contentWidth = Math.Max(dataWidth, titleWidth);
			int width = contentWidth + Margin.Left + Margin.Right + borderSize;
			int height = _graphHeight + Margin.Top + Margin.Bottom + borderSize + titleHeight + baselineHeight;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

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

			// Calculate content area (after margins, including border, title, and baseline)
			int borderSize = _borderStyle != BorderStyle.None ? 1 : 0;

			// When title is inline with baseline, don't count title height separately
			bool titleAndBaselineInline = _inlineTitleWithBaseline &&
			                              _titlePosition == _baselinePosition &&
			                              _showBaseline;

			int titleHeight = !string.IsNullOrEmpty(_title) && !titleAndBaselineInline ? 1 : 0;

			// Baseline height will be determined after we know if bidirectional mode
			int baselineHeight = 0;  // Will be set later

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int contentWidth = bounds.Width - Margin.Left - Margin.Right;
			int contentHeight = _graphHeight + (borderSize * 2) + titleHeight + baselineHeight;

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

			// Calculate graph area (inside border if present)
			// Title position affects whether graph or title comes first vertically
			int graphStartX = startX + borderSize;
			int graphStartY = _titlePosition == TitlePosition.Top
				? startY + borderSize + titleHeight  // Graph below title
				: startY + borderSize;               // Graph at top, title below
			int graphWidth = contentWidth - (borderSize * 2);
			int graphBottom = graphStartY + _graphHeight - 1;

			// Draw title if present (position depends on TitlePosition setting)
			if (!string.IsNullOrEmpty(_title))
			{
				int titleY = _titlePosition == TitlePosition.Top
					? startY + borderSize                    // Title above graph
					: graphStartY + _graphHeight;            // Title below graph
				int titlePadding = borderSize > 0 ? 1 : 0; // Only pad when border is present
				int titleX = startX + borderSize + titlePadding;
				int maxTitleWidth = contentWidth - (borderSize * 2) - (titlePadding * 2);

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

			// Snapshot data points under lock for thread safety
			List<double> dataPointsSnapshot;
			List<double> secondaryDataPointsSnapshot;
			lock (_dataLock)
			{
				dataPointsSnapshot = new List<double>(_dataPoints);
				secondaryDataPointsSnapshot = new List<double>(_secondaryDataPoints);
			}

			if (dataPointsSnapshot.Count == 0 && secondaryDataPointsSnapshot.Count == 0)
				return;

			// Check if we're in bidirectional mode
			bool isBidirectional = _mode == SparklineMode.Bidirectional || _mode == SparklineMode.BidirectionalBraille;
			bool useBraille = _mode == SparklineMode.Braille || _mode == SparklineMode.BidirectionalBraille;

			// Now that we know if bidirectional, set baseline height
			// In bidirectional mode, baseline is in the middle (doesn't add height)
			// In standard mode, baseline adds height if not inline with title
			baselineHeight = _showBaseline && !isBidirectional ? 1 : 0;

			// Draw baseline BEFORE graph data so data can draw over it
			if (_showBaseline)
			{
				// Determine baseline Y position based on mode and BaselinePosition
				int baselineY;

				if (isBidirectional)
				{
					// In bidirectional mode, baseline is ALWAYS at the middle (centerline)
					// This is the horizontal line between upload (top) and download (bottom)
					int halfHeight = _graphHeight / 2;
					baselineY = graphStartY + halfHeight;
				}
				else if (_baselinePosition == TitlePosition.Top)
				{
					// Standard mode: Baseline at top (before graph, after title if title is also on top)
					baselineY = _titlePosition == TitlePosition.Top && !string.IsNullOrEmpty(_title) && !titleAndBaselineInline
						? startY + borderSize + 1  // After title
						: startY + borderSize;      // No title or title at bottom or inline
				}
				else
				{
					// Standard mode: Baseline at bottom (after graph, before title if title is also at bottom)
					baselineY = graphBottom + 1;
					// If title is at bottom and not inline, baseline comes first
					if (_titlePosition == TitlePosition.Bottom && !string.IsNullOrEmpty(_title) && !titleAndBaselineInline)
					{
						baselineY = graphBottom + 1;
					}
				}

				if (baselineY >= clipRect.Y && baselineY < clipRect.Bottom)
				{
					// Check if we should inline title with baseline
					// Inline only when title and baseline are on the same side (both top or both bottom)
					bool shouldInlineTitle = titleAndBaselineInline && !string.IsNullOrEmpty(_title);

					int baselineStartX = graphStartX;

					if (shouldInlineTitle)
					{
						// Render title at start of baseline
						int titlePadding = borderSize > 0 ? 1 : 0;
						int titleX = startX + borderSize + titlePadding;

						// Convert title markup to ANSI
						string processedTitle = _title!;
						if (_titleColor.HasValue)
						{
							string colorName = $"rgb({_titleColor.Value.R},{_titleColor.Value.G},{_titleColor.Value.B})";
							processedTitle = $"[{colorName}]{_title}[/]";
						}

						var ansiLines = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
							processedTitle, graphWidth, 1, false, bgColor, _foregroundColorValue ?? fgColor);

						if (ansiLines.Count > 0)
						{
							var cells = AnsiParser.Parse(ansiLines[0], fgColor, bgColor).ToList();
							buffer.WriteCellsClipped(titleX, baselineY, cells, clipRect);

							// Advance baseline start position past title
							int titleWidth = cells.Count;
							baselineStartX = titleX + titleWidth + 1; // +1 for space after title
						}
					}

					// Fill rest of line with baseline characters
					for (int x = baselineStartX; x < graphStartX + graphWidth; x++)
					{
						if (x >= clipRect.X && x < clipRect.Right)
						{
							buffer.SetCell(x, baselineY, _baselineChar, _baselineColor, bgColor);
						}
					}
				}
			}

			// Paint graph data AFTER baseline so data can draw over it
			if (isBidirectional)
			{
				PaintBidirectional(buffer, graphStartX, graphStartY, graphWidth, _graphHeight, graphBottom, clipRect, bgColor, useBraille, dataPointsSnapshot, secondaryDataPointsSnapshot);
			}
			else
			{
				PaintStandard(buffer, graphStartX, graphStartY, graphWidth, _graphHeight, graphBottom, clipRect, bgColor, useBraille, dataPointsSnapshot);
			}
		}

		private void PaintStandard(CharacterBuffer buffer, int graphStartX, int graphStartY, int graphWidth, int graphHeight, int graphBottom, LayoutRect clipRect, Color bgColor, bool useBraille, List<double> dataPoints)
		{
			if (dataPoints.Count == 0)
				return;

			// Calculate scale
			double min = _minValue ?? Math.Min(0, dataPoints.Min());
			double max = _maxValue ?? Math.Max(dataPoints.Max(), min + 1.0);
			double range = max - min;

			if (range < 0.001)
				range = 1.0;

			// Determine which data points to display
			int availableWidth = graphWidth;
			int displayCount = Math.Min(dataPoints.Count, availableWidth);
			int startIndex = Math.Max(0, dataPoints.Count - displayCount);

			// Paint vertical bars
			for (int i = 0; i < displayCount; i++)
			{
				int dataIndex = startIndex + i;
				if (dataIndex >= dataPoints.Count)
					break;

				double value = dataPoints[dataIndex];
				double normalized = (value - min) / range; // 0.0 to 1.0
				double barHeight = normalized * graphHeight;

				int paintX = graphStartX + i;
				if (paintX < clipRect.X || paintX >= clipRect.Right)
					continue;

				// Paint vertical bar from bottom to top
				for (int y = 0; y < graphHeight; y++)
				{
					int paintY = graphBottom - y;
					if (paintY < clipRect.Y || paintY >= clipRect.Bottom)
						continue;

					char displayChar = GetBarChar(barHeight, y, useBraille);

					// Determine cell color based on whether the character is "empty"
					bool isEmpty = useBraille ? displayChar == BRAILLE_CHARS[0] : displayChar == ' ';
					Color cellColor;
					if (isEmpty)
					{
						cellColor = Color.Grey19;
					}
					else if (_gradient != null)
					{
						// Vertical gradient: color based on row position (bottom to top)
						double rowHeightNormalized = (double)(y + 1) / graphHeight;
						cellColor = _gradient.Interpolate(rowHeightNormalized);
					}
					else
					{
						cellColor = _barColor;
					}
					buffer.SetCell(paintX, paintY, displayChar, cellColor, bgColor);
				}
			}
		}

		private void PaintBidirectional(CharacterBuffer buffer, int graphStartX, int graphStartY, int graphWidth, int graphHeight, int graphBottom, LayoutRect clipRect, Color bgColor, bool useBraille, List<double> dataPoints, List<double> secondaryDataPoints)
		{
			// In bidirectional mode:
			// - Primary series (upload) goes UP from the middle
			// - Secondary series (download) goes DOWN from the middle
			int topHalfHeight = graphHeight / 2;
			int bottomHalfHeight = graphHeight - topHalfHeight - 1; // -1 for baseline row
			int middleY = graphStartY + topHalfHeight;

			// Calculate scales for both series
			double primaryMax = _maxValue ?? (dataPoints.Count > 0 ? Math.Max(dataPoints.Max(), 0.001) : 1.0);
			double secondaryMax = _secondaryMaxValue ?? (secondaryDataPoints.Count > 0 ? Math.Max(secondaryDataPoints.Max(), 0.001) : primaryMax);

			// Determine display count based on larger dataset
			int availableWidth = graphWidth;
			int primaryCount = dataPoints.Count;
			int secondaryCount = secondaryDataPoints.Count;
			int maxDataCount = Math.Max(primaryCount, secondaryCount);
			int displayCount = Math.Min(maxDataCount, availableWidth);

			int primaryStartIndex = Math.Max(0, primaryCount - displayCount);
			int secondaryStartIndex = Math.Max(0, secondaryCount - displayCount);

			// Paint each column
			for (int i = 0; i < displayCount; i++)
			{
				int paintX = graphStartX + i;
				if (paintX < clipRect.X || paintX >= clipRect.Right)
					continue;

				// Paint primary (upload) - goes UP from middle
				int primaryDataIndex = primaryStartIndex + i;
				if (primaryDataIndex < primaryCount)
				{
					double value = dataPoints[primaryDataIndex];
					double normalized = Math.Clamp(value / primaryMax, 0, 1);
					double barHeight = normalized * topHalfHeight;

					for (int y = 0; y < topHalfHeight; y++)
					{
						int paintY = middleY - 1 - y; // Paint upward from middle
						if (paintY < clipRect.Y || paintY >= clipRect.Bottom)
							continue;

						char displayChar = GetBarChar(barHeight, y, useBraille);
						bool isEmpty = useBraille ? displayChar == BRAILLE_CHARS[0] : displayChar == ' ';
						Color cellColor;
						if (isEmpty)
						{
							cellColor = Color.Grey19;
						}
						else if (_gradient != null)
						{
							double rowHeightNormalized = (double)(y + 1) / topHalfHeight;
							cellColor = _gradient.Interpolate(rowHeightNormalized);
						}
						else
						{
							cellColor = _barColor;
						}
						buffer.SetCell(paintX, paintY, displayChar, cellColor, bgColor);
					}
				}

				// Paint secondary (download) - goes DOWN from middle
				int secondaryDataIndex = secondaryStartIndex + i;
				if (secondaryDataIndex < secondaryCount)
				{
					double value = secondaryDataPoints[secondaryDataIndex];
					double normalized = Math.Clamp(value / secondaryMax, 0, 1);
					double barHeight = normalized * bottomHalfHeight;

					for (int y = 0; y < bottomHalfHeight; y++)
					{
						int paintY = middleY + 1 + y; // Paint downward from middle (skip baseline row)
						if (paintY < clipRect.Y || paintY >= clipRect.Bottom)
							continue;

						// For downward bars, we use inverted block chars (▔▀ style) or just fill from top
						char displayChar = GetBarCharInverted(barHeight, y, useBraille);
						bool isEmpty = useBraille ? displayChar == BRAILLE_CHARS[0] : displayChar == ' ';
						Color cellColor;
						if (isEmpty)
						{
							cellColor = Color.Grey19;
						}
						else if (_secondaryGradient != null)
						{
							// Secondary has its own gradient
							double rowHeightNormalized = (double)(y + 1) / bottomHalfHeight;
							cellColor = _secondaryGradient.Interpolate(rowHeightNormalized);
						}
						else if (_gradient != null)
						{
							// Fall back to primary gradient if no secondary gradient
							double rowHeightNormalized = (double)(y + 1) / bottomHalfHeight;
							cellColor = _gradient.Interpolate(rowHeightNormalized);
						}
						else
						{
							cellColor = _secondaryBarColor;
						}
						buffer.SetCell(paintX, paintY, displayChar, cellColor, bgColor);
					}
				}
			}
		}

		private char GetBarChar(double barHeight, int rowIndex, bool useBraille)
		{
			double rowTopThreshold = rowIndex + 1;
			double rowBottomThreshold = rowIndex;

			if (useBraille)
			{
				if (barHeight >= rowTopThreshold)
					return BRAILLE_CHARS[4]; // Full
				else if (barHeight > rowBottomThreshold)
				{
					double fraction = barHeight - rowBottomThreshold;
					int charIndex = (int)Math.Round(fraction * 4);
					return BRAILLE_CHARS[Math.Clamp(charIndex, 0, 4)];
				}
				return BRAILLE_CHARS[0]; // Empty
			}
			else
			{
				if (barHeight >= rowTopThreshold)
					return VERTICAL_CHARS[8]; // Full
				else if (barHeight > rowBottomThreshold)
				{
					double fraction = barHeight - rowBottomThreshold;
					int charIndex = (int)Math.Round(fraction * 8);
					return VERTICAL_CHARS[Math.Clamp(charIndex, 0, 8)];
				}
				return VERTICAL_CHARS[0]; // Empty
			}
		}

		private char GetBarCharInverted(double barHeight, int rowIndex, bool useBraille)
		{
			// For inverted bars (growing downward), we fill from the top of each cell
			double rowTopThreshold = rowIndex + 1;
			double rowBottomThreshold = rowIndex;

			if (useBraille)
			{
				if (barHeight >= rowTopThreshold)
					return BRAILLE_CHARS_INVERTED[4]; // Full
				else if (barHeight > rowBottomThreshold)
				{
					double fraction = barHeight - rowBottomThreshold;
					int charIndex = (int)Math.Round(fraction * 4);
					// Use inverted braille chars for top-down fill
					return BRAILLE_CHARS_INVERTED[Math.Clamp(charIndex, 0, 4)];
				}
				return BRAILLE_CHARS_INVERTED[0]; // Empty
			}
			else
			{
				// For block mode, use upper block characters for downward bars
				if (barHeight >= rowTopThreshold)
					return VERTICAL_CHARS_INVERTED[8]; // Full
				else if (barHeight > rowBottomThreshold)
				{
					double fraction = barHeight - rowBottomThreshold;
					int charIndex = (int)Math.Round(fraction * 8);
					// Use inverted vertical chars for top-down fill
					return VERTICAL_CHARS_INVERTED[Math.Clamp(charIndex, 0, 8)];
				}
				return VERTICAL_CHARS_INVERTED[0]; // Empty
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
			lock (_dataLock)
			{
				_dataPoints.Add(value);
				TrimDataPoints();
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Clears all data points from the graph.
		/// </summary>
		public void ClearDataPoints()
		{
			lock (_dataLock)
			{
				_dataPoints.Clear();
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets the data points for the graph, replacing any existing points.
		/// </summary>
		public void SetDataPoints(IEnumerable<double> dataPoints)
		{
			lock (_dataLock)
			{
				_dataPoints = new List<double>(dataPoints);
				TrimDataPoints();
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Adds a new secondary data point (for bidirectional mode).
		/// If the maximum number of points is exceeded, the oldest point is removed.
		/// </summary>
		public void AddSecondaryDataPoint(double value)
		{
			lock (_dataLock)
			{
				_secondaryDataPoints.Add(value);
				TrimSecondaryDataPoints();
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Clears all secondary data points from the graph.
		/// </summary>
		public void ClearSecondaryDataPoints()
		{
			lock (_dataLock)
			{
				_secondaryDataPoints.Clear();
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets the secondary data points for bidirectional mode, replacing any existing points.
		/// </summary>
		public void SetSecondaryDataPoints(IEnumerable<double> dataPoints)
		{
			lock (_dataLock)
			{
				_secondaryDataPoints = new List<double>(dataPoints);
				TrimSecondaryDataPoints();
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets both primary and secondary data points at once (for bidirectional mode).
		/// More efficient than calling SetDataPoints and SetSecondaryDataPoints separately.
		/// </summary>
		public void SetBidirectionalData(IEnumerable<double> primaryData, IEnumerable<double> secondaryData)
		{
			lock (_dataLock)
			{
				_dataPoints = new List<double>(primaryData);
				_secondaryDataPoints = new List<double>(secondaryData);
				TrimDataPoints();
				TrimSecondaryDataPoints();
			}
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

		private void TrimSecondaryDataPoints()
		{
			while (_secondaryDataPoints.Count > _maxDataPoints)
			{
				_secondaryDataPoints.RemoveAt(0);
			}
		}

		private void DrawBorder(CharacterBuffer buffer, int x, int y, int width, int height, LayoutRect clipRect, Color borderColor, Color bgColor)
		{
			if (_borderStyle == BorderStyle.None || width < 2 || height < 2)
				return;

			// Get border characters using BoxChars abstraction
			var chars = BoxChars.FromBorderStyle(_borderStyle);

			// Draw top border
			if (y >= clipRect.Y && y < clipRect.Bottom)
			{
				if (x >= clipRect.X && x < clipRect.Right)
					buffer.SetCell(x, y, chars.TopLeft, borderColor, bgColor);

				for (int i = 1; i < width - 1; i++)
				{
					int drawX = x + i;
					if (drawX >= clipRect.X && drawX < clipRect.Right)
						buffer.SetCell(drawX, y, chars.Horizontal, borderColor, bgColor);
				}

				if (x + width - 1 >= clipRect.X && x + width - 1 < clipRect.Right)
					buffer.SetCell(x + width - 1, y, chars.TopRight, borderColor, bgColor);
			}

			// Draw bottom border
			int bottomY = y + height - 1;
			if (bottomY >= clipRect.Y && bottomY < clipRect.Bottom)
			{
				if (x >= clipRect.X && x < clipRect.Right)
					buffer.SetCell(x, bottomY, chars.BottomLeft, borderColor, bgColor);

				for (int i = 1; i < width - 1; i++)
				{
					int drawX = x + i;
					if (drawX >= clipRect.X && drawX < clipRect.Right)
						buffer.SetCell(drawX, bottomY, chars.Horizontal, borderColor, bgColor);
				}

				if (x + width - 1 >= clipRect.X && x + width - 1 < clipRect.Right)
					buffer.SetCell(x + width - 1, bottomY, chars.BottomRight, borderColor, bgColor);
			}

			// Draw left and right borders
			for (int i = 1; i < height - 1; i++)
			{
				int drawY = y + i;
				if (drawY >= clipRect.Y && drawY < clipRect.Bottom)
				{
					if (x >= clipRect.X && x < clipRect.Right)
						buffer.SetCell(x, drawY, chars.Vertical, borderColor, bgColor);

					if (x + width - 1 >= clipRect.X && x + width - 1 < clipRect.Right)
						buffer.SetCell(x + width - 1, drawY, chars.Vertical, borderColor, bgColor);
				}
			}
		}

		#endregion
	}
}

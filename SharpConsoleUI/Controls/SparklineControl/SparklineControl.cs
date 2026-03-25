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
	public partial class SparklineControl : BaseControl
	{
		private const int DEFAULT_HEIGHT = 8;
		private const int DEFAULT_MAX_DATA_POINTS = 50;

		private bool _autoFitDataPoints;
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
			set => SetProperty(ref _backgroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the color for the bars.
		/// </summary>
		public Color BarColor
		{
			get => _barColor;
			set => SetProperty(ref _barColor, value);
		}

		/// <summary>
		/// Gets or sets the border style around the graph.
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set => SetProperty(ref _borderStyle, value);
		}

		/// <summary>
		/// Gets or sets the rendering mode for sparkline bars.
		/// Block mode uses 9-level block characters, Braille mode uses 5-level braille patterns.
		/// </summary>
		public SparklineMode Mode
		{
			get => _mode;
			set => SetProperty(ref _mode, value);
		}

		/// <summary>
		/// Gets or sets the border color.
		/// When null, uses the foreground color.
		/// </summary>
		public Color? BorderColor
		{
			get => _borderColor;
			set => SetProperty(ref _borderColor, value);
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
			set => SetProperty(ref _foregroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the height of the graph in lines.
		/// </summary>
		public int GraphHeight
		{
			get => _graphHeight;
			set => SetProperty(ref _graphHeight, value, v => Math.Max(1, v));
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
				OnPropertyChanged();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether MaxDataPoints is automatically adjusted to match the rendered width.
		/// When true, the sparkline always fills the available horizontal space.
		/// </summary>
		public bool AutoFitDataPoints
		{
			get => _autoFitDataPoints;
			set => SetProperty(ref _autoFitDataPoints, value);
		}

		/// <summary>
		/// Gets or sets the maximum value for the graph scale.
		/// When null, uses the maximum data point value.
		/// </summary>
		public double? MaxValue
		{
			get => _maxValue;
			set => SetProperty(ref _maxValue, value);
		}

		/// <summary>
		/// Gets or sets the minimum value for the graph scale.
		/// When null, uses the minimum data point value (or 0 if all positive).
		/// </summary>
		public double? MinValue
		{
			get => _minValue;
			set => SetProperty(ref _minValue, value);
		}

		/// <summary>
		/// Gets or sets the optional title displayed at the top of the graph (inside border if present).
		/// </summary>
		public string? Title
		{
			get => _title;
			set => SetProperty(ref _title, value);
		}

		/// <summary>
		/// Gets or sets the color for the title text.
		/// When null, uses the foreground color.
		/// </summary>
		public Color? TitleColor
		{
			get => _titleColor;
			set => SetProperty(ref _titleColor, value);
		}

		/// <summary>
		/// Gets or sets the position of the title relative to the sparkline graph.
		/// Default is Top (title above the graph).
		/// </summary>
		public TitlePosition TitlePosition
		{
			get => _titlePosition;
			set => SetProperty(ref _titlePosition, value);
		}

		/// <summary>
		/// Gets or sets the color gradient for vertical color interpolation.
		/// When set, each bar column gets a color based on its height.
		/// When null, uses the solid BarColor.
		/// </summary>
		public ColorGradient? Gradient
		{
			get => _gradient;
			set => SetProperty(ref _gradient, value);
		}

		/// <summary>
		/// Gets or sets whether to show a dotted baseline at the bottom of the graph.
		/// </summary>
		public bool ShowBaseline
		{
			get => _showBaseline;
			set => SetProperty(ref _showBaseline, value);
		}

		/// <summary>
		/// Gets or sets the character used for the baseline (default: ┈).
		/// </summary>
		public char BaselineChar
		{
			get => _baselineChar;
			set => SetProperty(ref _baselineChar, value);
		}

		/// <summary>
		/// Gets or sets the color of the baseline (default: Grey50).
		/// </summary>
		public Color BaselineColor
		{
			get => _baselineColor;
			set => SetProperty(ref _baselineColor, value);
		}

		/// <summary>
		/// Gets or sets the position of the baseline (Top or Bottom, default: Bottom).
		/// When set to Top, baseline appears above the graph.
		/// </summary>
		public TitlePosition BaselinePosition
		{
			get => _baselinePosition;
			set => SetProperty(ref _baselinePosition, value);
		}

		/// <summary>
		/// Gets or sets whether to show the title inline with the baseline.
		/// Only applies when TitlePosition and BaselinePosition are the same (both Top or both Bottom).
		/// Format: "Title ┈┈┈┈┈┈┈┈┈" (title followed by baseline fill).
		/// </summary>
		public bool InlineTitleWithBaseline
		{
			get => _inlineTitleWithBaseline;
			set => SetProperty(ref _inlineTitleWithBaseline, value);
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
			get => base.Container;
			set
			{
				base.Container = value;
				OnPropertyChanged();
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

		#region Public Methods - Data Management

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
					buffer.SetNarrowCell(x, y, chars.TopLeft, borderColor, bgColor);

				for (int i = 1; i < width - 1; i++)
				{
					int drawX = x + i;
					if (drawX >= clipRect.X && drawX < clipRect.Right)
						buffer.SetNarrowCell(drawX, y, chars.Horizontal, borderColor, bgColor);
				}

				if (x + width - 1 >= clipRect.X && x + width - 1 < clipRect.Right)
					buffer.SetNarrowCell(x + width - 1, y, chars.TopRight, borderColor, bgColor);
			}

			// Draw bottom border
			int bottomY = y + height - 1;
			if (bottomY >= clipRect.Y && bottomY < clipRect.Bottom)
			{
				if (x >= clipRect.X && x < clipRect.Right)
					buffer.SetNarrowCell(x, bottomY, chars.BottomLeft, borderColor, bgColor);

				for (int i = 1; i < width - 1; i++)
				{
					int drawX = x + i;
					if (drawX >= clipRect.X && drawX < clipRect.Right)
						buffer.SetNarrowCell(drawX, bottomY, chars.Horizontal, borderColor, bgColor);
				}

				if (x + width - 1 >= clipRect.X && x + width - 1 < clipRect.Right)
					buffer.SetNarrowCell(x + width - 1, bottomY, chars.BottomRight, borderColor, bgColor);
			}

			// Draw left and right borders
			for (int i = 1; i < height - 1; i++)
			{
				int drawY = y + i;
				if (drawY >= clipRect.Y && drawY < clipRect.Bottom)
				{
					if (x >= clipRect.X && x < clipRect.Right)
						buffer.SetNarrowCell(x, drawY, chars.Vertical, borderColor, bgColor);

					if (x + width - 1 >= clipRect.X && x + width - 1 < clipRect.Right)
						buffer.SetNarrowCell(x + width - 1, drawY, chars.Vertical, borderColor, bgColor);
				}
			}
		}

		#endregion
	}
}

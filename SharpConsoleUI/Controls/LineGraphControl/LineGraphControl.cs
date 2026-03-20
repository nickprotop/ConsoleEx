// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Specifies the rendering mode for line graphs.
	/// </summary>
	public enum LineGraphMode
	{
		/// <summary>
		/// Uses braille patterns (2x4 pixel grid per cell) for smooth, high-resolution lines.
		/// </summary>
		Braille,

		/// <summary>
		/// Uses ASCII box-drawing characters for simple line display.
		/// </summary>
		Ascii
	}

	/// <summary>
	/// Represents a named data series in a line graph.
	/// </summary>
	public class LineGraphSeries
	{
		/// <summary>
		/// Gets or sets the display name of this series.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the line color for this series.
		/// </summary>
		public Color LineColor { get; set; } = Color.Cyan1;

		/// <summary>
		/// Gets or sets an optional horizontal color gradient for this series.
		/// When set, color interpolates across the graph width.
		/// </summary>
		public ColorGradient? Gradient { get; set; }

		/// <summary>
		/// The data points for this series.
		/// </summary>
		internal List<double> DataPoints { get; } = new();

		/// <summary>
		/// Creates a new series with the specified name and color.
		/// </summary>
		/// <param name="name">The series name.</param>
		/// <param name="lineColor">The line color.</param>
		/// <param name="gradient">Optional color gradient.</param>
		public LineGraphSeries(string name, Color lineColor, ColorGradient? gradient = null)
		{
			Name = name;
			LineColor = lineColor;
			Gradient = gradient;
		}
	}

	/// <summary>
	/// A line graph control for visualizing time-series data using braille or ASCII rendering.
	/// Supports multiple named series rendered as connected lines.
	/// </summary>
	public partial class LineGraphControl : BaseControl
	{
		#region Constants

		private const string DEFAULT_SERIES_NAME = "default";

		#endregion

		#region Fields

		private readonly object _dataLock = new();
		private readonly List<LineGraphSeries> _series = new();
		private readonly List<ReferenceLine> _referenceLines = new();
		private readonly List<ValueMarker> _valueMarkers = new();

		private bool _showHighLowLabels;
		private Color _highLabelColor = Color.Green;
		private Color _lowLabelColor = Color.Red;
		private MarkerSide _highLowLabelSide = MarkerSide.Right;

		private LineGraphMode _mode = LineGraphMode.Braille;
		private int _graphHeight = ControlDefaults.LineGraphDefaultHeight;
		private int _maxDataPoints = ControlDefaults.LineGraphDefaultMaxDataPoints;
		private bool _autoFitDataPoints;
		private double? _minValue;
		private double? _maxValue;

		private string? _title;
		private Color? _titleColor;
		private TitlePosition _titlePosition = TitlePosition.Top;

		private bool _showYAxisLabels;
		private string _axisLabelFormat = ControlDefaults.LineGraphDefaultAxisFormat;
		private Color _axisLabelColor = Color.Grey70;

		private bool _showBaseline;
		private char _baselineChar = '┈';
		private Color _baselineColor = Color.Grey50;
		private TitlePosition _baselinePosition = TitlePosition.Bottom;
		private bool _inlineTitleWithBaseline;
		private bool _showLegend;

		private BorderStyle _borderStyle = BorderStyle.None;
		private Color? _borderColor;
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private IContainer? _container;

		// Reusable pixel grid to avoid GC pressure in braille mode
		private bool[,]? _pixelGridCache;

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the rendering mode (Braille or Ascii).
		/// </summary>
		public LineGraphMode Mode
		{
			get => _mode;
			set => SetProperty(ref _mode, value);
		}

		/// <summary>
		/// Gets or sets the height of the graph area in lines.
		/// </summary>
		public int GraphHeight
		{
			get => _graphHeight;
			set => SetProperty(ref _graphHeight, value, v => Math.Max(ControlDefaults.LineGraphMinHeight, v));
		}

		/// <summary>
		/// Gets or sets the maximum number of data points per series.
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
					foreach (var series in _series)
						TrimSeriesData(series);
				}
				OnPropertyChanged();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether MaxDataPoints is automatically adjusted to match the rendered width.
		/// </summary>
		public bool AutoFitDataPoints
		{
			get => _autoFitDataPoints;
			set => SetProperty(ref _autoFitDataPoints, value);
		}

		/// <summary>
		/// Gets or sets the fixed minimum value for the Y axis.
		/// When null, auto-scales from data.
		/// </summary>
		public double? MinValue
		{
			get => _minValue;
			set => SetProperty(ref _minValue, value);
		}

		/// <summary>
		/// Gets or sets the fixed maximum value for the Y axis.
		/// When null, auto-scales from data.
		/// </summary>
		public double? MaxValue
		{
			get => _maxValue;
			set => SetProperty(ref _maxValue, value);
		}

		/// <summary>
		/// Gets or sets the graph title.
		/// </summary>
		public string? Title
		{
			get => _title;
			set => SetProperty(ref _title, value);
		}

		/// <summary>
		/// Gets or sets the title color. When null, uses the foreground color.
		/// </summary>
		public Color? TitleColor
		{
			get => _titleColor;
			set => SetProperty(ref _titleColor, value);
		}

		/// <summary>
		/// Gets or sets the title position (Top or Bottom).
		/// </summary>
		public TitlePosition TitlePosition
		{
			get => _titlePosition;
			set => SetProperty(ref _titlePosition, value);
		}

		/// <summary>
		/// Gets or sets whether to show Y-axis labels (min/max values).
		/// </summary>
		public bool ShowYAxisLabels
		{
			get => _showYAxisLabels;
			set => SetProperty(ref _showYAxisLabels, value);
		}

		/// <summary>
		/// Gets or sets the format string for Y-axis labels.
		/// </summary>
		public string AxisLabelFormat
		{
			get => _axisLabelFormat;
			set => SetProperty(ref _axisLabelFormat, value);
		}

		/// <summary>
		/// Gets or sets the color of Y-axis labels.
		/// </summary>
		public Color AxisLabelColor
		{
			get => _axisLabelColor;
			set => SetProperty(ref _axisLabelColor, value);
		}

		/// <summary>
		/// Gets or sets whether to show a baseline.
		/// </summary>
		public bool ShowBaseline
		{
			get => _showBaseline;
			set => SetProperty(ref _showBaseline, value);
		}

		/// <summary>
		/// Gets or sets the baseline character.
		/// </summary>
		public char BaselineChar
		{
			get => _baselineChar;
			set => SetProperty(ref _baselineChar, value);
		}

		/// <summary>
		/// Gets or sets the baseline color.
		/// </summary>
		public Color BaselineColor
		{
			get => _baselineColor;
			set => SetProperty(ref _baselineColor, value);
		}

		/// <summary>
		/// Gets or sets the baseline position.
		/// </summary>
		public TitlePosition BaselinePosition
		{
			get => _baselinePosition;
			set => SetProperty(ref _baselinePosition, value);
		}

		/// <summary>
		/// Gets or sets whether to show a legend displaying series names and colors.
		/// The legend is rendered on the title row, right-aligned.
		/// </summary>
		public bool ShowLegend
		{
			get => _showLegend;
			set => SetProperty(ref _showLegend, value);
		}

		/// <summary>
		/// Gets or sets whether the title is rendered inline with the baseline.
		/// Only applies when TitlePosition and BaselinePosition are the same.
		/// </summary>
		public bool InlineTitleWithBaseline
		{
			get => _inlineTitleWithBaseline;
			set => SetProperty(ref _inlineTitleWithBaseline, value);
		}

		/// <summary>
		/// Gets or sets the border style.
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set => SetProperty(ref _borderStyle, value);
		}

		/// <summary>
		/// Gets or sets the border color. When null, uses the foreground color.
		/// </summary>
		public Color? BorderColor
		{
			get => _borderColor;
			set => SetProperty(ref _borderColor, value);
		}

		/// <summary>
		/// Gets or sets the background color. When null, inherits from container.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set => SetProperty(ref _backgroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the foreground color. When null, inherits from container.
		/// </summary>
		public Color? ForegroundColor
		{
			get => _foregroundColorValue;
			set => SetProperty(ref _foregroundColorValue, value);
		}

		/// <summary>
		/// Gets the list of data series as a read-only snapshot.
		/// </summary>
		public IReadOnlyList<LineGraphSeries> Series
		{
			get
			{
				lock (_dataLock)
				{
					return new List<LineGraphSeries>(_series);
				}
			}
		}

		/// <summary>
		/// Gets the list of reference lines as a read-only snapshot.
		/// </summary>
		public IReadOnlyList<ReferenceLine> ReferenceLines
		{
			get
			{
				lock (_dataLock)
				{
					return new List<ReferenceLine>(_referenceLines);
				}
			}
		}

		/// <summary>
		/// Gets the list of value markers as a read-only snapshot.
		/// </summary>
		public IReadOnlyList<ValueMarker> ValueMarkers
		{
			get
			{
				lock (_dataLock)
				{
					return new List<ValueMarker>(_valueMarkers);
				}
			}
		}

		/// <summary>
		/// Gets or sets whether to display labels for the highest and lowest data values.
		/// </summary>
		public bool ShowHighLowLabels
		{
			get => _showHighLowLabels;
			set => SetProperty(ref _showHighLowLabels, value);
		}

		/// <summary>
		/// Gets or sets the color used for the high value label.
		/// </summary>
		public Color HighLabelColor
		{
			get => _highLabelColor;
			set => SetProperty(ref _highLabelColor, value);
		}

		/// <summary>
		/// Gets or sets the color used for the low value label.
		/// </summary>
		public Color LowLabelColor
		{
			get => _lowLabelColor;
			set => SetProperty(ref _lowLabelColor, value);
		}

		/// <summary>
		/// Gets or sets which side of the graph the high/low labels appear on.
		/// </summary>
		public MarkerSide HighLowLabelSide
		{
			get => _highLowLabelSide;
			set => SetProperty(ref _highLowLabelSide, value);
		}

		#endregion

		#region IWindowControl Implementation

		/// <inheritdoc/>
		public override int? ContentWidth
		{
			get
			{
				int maxPoints;
				lock (_dataLock)
				{
					maxPoints = _series.Count > 0
						? _series.Max(s => s.DataPoints.Count)
						: 0;
				}
				int yAxisWidth = _showYAxisLabels ? GetYAxisLabelWidth() + ControlDefaults.LineGraphYAxisLabelPadding : 0;
				return Width ?? (maxPoints + yAxisWidth + Margin.Left + Margin.Right);
			}
		}

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
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
				foreach (var series in _series)
					series.DataPoints.Clear();
				_series.Clear();
				_referenceLines.Clear();
				_valueMarkers.Clear();
			}
			_pixelGridCache = null;
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			int width = ContentWidth ?? 0;
			int height = _graphHeight + Margin.Top + Margin.Bottom;
			return new System.Drawing.Size(width, height);
		}

		#endregion

		#region Public Methods - Series Management

		/// <summary>
		/// Adds a new named data series to the graph.
		/// </summary>
		/// <param name="name">The series name (must be unique).</param>
		/// <param name="lineColor">The line color.</param>
		/// <param name="gradient">Optional color gradient.</param>
		/// <returns>The created series.</returns>
		public LineGraphSeries AddSeries(string name, Color lineColor, ColorGradient? gradient = null)
		{
			lock (_dataLock)
			{
				var existing = _series.Find(s => s.Name == name);
				if (existing != null)
					return existing;

				var series = new LineGraphSeries(name, lineColor, gradient);
				_series.Add(series);
				return series;
			}
		}

		/// <summary>
		/// Removes a series by name.
		/// </summary>
		/// <param name="name">The series name to remove.</param>
		/// <returns>True if the series was found and removed.</returns>
		public bool RemoveSeries(string name)
		{
			lock (_dataLock)
			{
				var series = _series.Find(s => s.Name == name);
				if (series == null)
					return false;

				series.DataPoints.Clear();
				_series.Remove(series);
			}
			Container?.Invalidate(true);
			return true;
		}

		#endregion

		#region Public Methods - Data Management

		/// <summary>
		/// Adds a data point to a single-series graph.
		/// Auto-creates a default series if none exist.
		/// </summary>
		/// <param name="value">The data point value.</param>
		public void AddDataPoint(double value)
		{
			lock (_dataLock)
			{
				var series = GetOrCreateDefaultSeries();
				series.DataPoints.Add(value);
				TrimSeriesData(series);
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Adds a data point to the specified series.
		/// </summary>
		/// <param name="seriesName">The series name.</param>
		/// <param name="value">The data point value.</param>
		public void AddDataPoint(string seriesName, double value)
		{
			lock (_dataLock)
			{
				var series = _series.Find(s => s.Name == seriesName);
				if (series == null)
					return;

				series.DataPoints.Add(value);
				TrimSeriesData(series);
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets all data points for the specified series, replacing existing data.
		/// </summary>
		/// <param name="seriesName">The series name.</param>
		/// <param name="data">The data points.</param>
		public void SetDataPoints(string seriesName, IEnumerable<double> data)
		{
			lock (_dataLock)
			{
				var series = _series.Find(s => s.Name == seriesName);
				if (series == null)
					return;

				series.DataPoints.Clear();
				series.DataPoints.AddRange(data);
				TrimSeriesData(series);
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets data points for a single-series graph.
		/// Auto-creates a default series if none exist.
		/// </summary>
		/// <param name="data">The data points.</param>
		public void SetDataPoints(IEnumerable<double> data)
		{
			lock (_dataLock)
			{
				var series = GetOrCreateDefaultSeries();
				series.DataPoints.Clear();
				series.DataPoints.AddRange(data);
				TrimSeriesData(series);
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Clears all data from all series.
		/// </summary>
		public void ClearAllData()
		{
			lock (_dataLock)
			{
				foreach (var series in _series)
					series.DataPoints.Clear();
			}
			Container?.Invalidate(true);
		}

		#endregion

		#region Public Methods - Overlays

		/// <summary>
		/// Adds a horizontal reference line at the specified Y-axis value.
		/// </summary>
		/// <param name="value">The Y-axis value where the line is drawn.</param>
		/// <param name="color">The color of the line.</param>
		/// <param name="lineChar">The character used to draw the line.</param>
		/// <param name="label">Optional label text.</param>
		/// <param name="labelPosition">Position of the label relative to the line.</param>
		public void AddReferenceLine(double value, Color color, char lineChar = '─', string? label = null, LabelPosition labelPosition = LabelPosition.None)
		{
			lock (_dataLock)
			{
				_referenceLines.Add(new ReferenceLine(value, color, lineChar, label, labelPosition));
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Removes all reference lines from the graph.
		/// </summary>
		public void ClearReferenceLines()
		{
			lock (_dataLock)
			{
				_referenceLines.Clear();
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Adds a labeled value marker pointing at the specified Y-axis value.
		/// </summary>
		/// <param name="value">The Y-axis value to mark.</param>
		/// <param name="label">The label text.</param>
		/// <param name="arrowColor">The color of the marker arrow.</param>
		/// <param name="labelColor">The color of the label text.</param>
		/// <param name="side">Which side of the graph the marker appears on.</param>
		public void AddValueMarker(double value, string label, Color arrowColor, Color labelColor, MarkerSide side = MarkerSide.Right)
		{
			lock (_dataLock)
			{
				_valueMarkers.Add(new ValueMarker(value, label, arrowColor, labelColor, side));
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Removes all value markers from the graph.
		/// </summary>
		public void ClearValueMarkers()
		{
			lock (_dataLock)
			{
				_valueMarkers.Clear();
			}
			Container?.Invalidate(true);
		}

		#endregion

		#region Private Helper Methods

		private LineGraphSeries GetOrCreateDefaultSeries()
		{
			var series = _series.Find(s => s.Name == DEFAULT_SERIES_NAME);
			if (series == null)
			{
				series = new LineGraphSeries(DEFAULT_SERIES_NAME, Color.Cyan1);
				_series.Add(series);
			}
			return series;
		}

		private void TrimSeriesData(LineGraphSeries series)
		{
			int excess = series.DataPoints.Count - _maxDataPoints;
			if (excess > 0)
			{
				series.DataPoints.RemoveRange(0, excess);
			}
		}

		private int GetYAxisLabelWidth()
		{
			List<(LineGraphSeries series, List<double> data)> snapshots;
			lock (_dataLock)
			{
				snapshots = _series.Select(s => (s, new List<double>(s.DataPoints))).ToList();
			}

			ComputeGlobalMinMaxFromSnapshots(snapshots, out double min, out double max);

			string minLabel = min.ToString(_axisLabelFormat);
			string maxLabel = max.ToString(_axisLabelFormat);

			return Math.Max(
				UnicodeWidth.GetStringWidth(minLabel),
				UnicodeWidth.GetStringWidth(maxLabel));
		}

		private void DrawBorder(CharacterBuffer buffer, int x, int y, int width, int height, LayoutRect clipRect, Color borderColor, Color bgColor)
		{
			if (_borderStyle == BorderStyle.None || width < 2 || height < 2)
				return;

			var chars = BoxChars.FromBorderStyle(_borderStyle);

			// Top border
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

			// Bottom border
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

			// Left and right borders
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

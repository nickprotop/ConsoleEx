// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Controls
{
	/// <summary>How a series is drawn.</summary>
	public enum ChartPlotType
	{
		/// <summary>Points joined by straight segments.</summary>
		Line,

		/// <summary>A line with the area beneath it filled.</summary>
		Area,

		/// <summary>One bar per point, rising from the baseline.</summary>
		Bar,

		/// <summary>Unconnected marks, one per point.</summary>
		Scatter,

		/// <summary>
		/// An inline readout: one row per series, the bar's length being the value.
		/// </summary>
		/// <remarks>
		/// There is no vertical dimension left in a single row, so length carries
		/// the reading and a gradient along it says where that reading sits on the
		/// scale. A label and the formatted value sit either side of the bar.
		/// </remarks>
		Gauge,
	}

	/// <summary>How the plot area is rasterised.</summary>
	/// <remarks>
	/// Deliberately a separate enum from <see cref="LineGraphMode"/>: that one's
	/// values and their order are already public, and tying a new control's surface
	/// to another control's enum couples two things that should move independently.
	/// </remarks>
	public enum ChartRenderMode
	{
		/// <summary>
		/// Half blocks, two independently coloured pixels per cell. The default,
		/// because a chart routinely shows several series and this is the only mode
		/// that keeps every series' colour where they overlap.
		/// </summary>
		Pixel,

		/// <summary>
		/// Braille dots, 2x4 per cell. Finer vertically, but a cell carries one
		/// foreground colour, so overlapping series lose one.
		/// </summary>
		Braille,

		/// <summary>Box-drawing characters, for terminals without the above.</summary>
		Ascii,
	}

	/// <summary>Which axis a reference line is measured against.</summary>
	public enum ChartReferenceAxis
	{
		/// <summary>A horizontal line at a value on the vertical axis.</summary>
		Y,

		/// <summary>A vertical line at a position on the horizontal axis.</summary>
		X,
	}

	/// <summary>A line drawn across the plot at a fixed value.</summary>
	/// <remarks>
	/// A target, a limit, an SLA, the moment of a deploy. A chart of numbers alone
	/// cannot answer "is this over the line" without the line.
	/// </remarks>
	public sealed class ChartReferenceLine
	{
		/// <summary>Where the line sits, in data coordinates.</summary>
		public required double Value { get; init; }

		/// <summary>The line colour.</summary>
		public required Color Color { get; init; }

		/// <summary>Text drawn at the line's end, if any.</summary>
		public string? Label { get; init; }

		/// <summary>Which axis <see cref="Value"/> is measured against.</summary>
		public ChartReferenceAxis Axis { get; init; } = ChartReferenceAxis.Y;
	}

	/// <summary>An arrow and label pointing at a value on the vertical axis.</summary>
	public sealed class ChartValueMarker
	{
		/// <summary>The value pointed at.</summary>
		public required double Value { get; init; }

		/// <summary>The text beside the arrow.</summary>
		public required string Label { get; init; }

		/// <summary>The marker colour.</summary>
		public required Color Color { get; init; }
	}

	/// <summary>A value past which a series takes a different colour.</summary>
	/// <param name="Above">The boundary; points at or above it take <paramref name="Color"/>.</param>
	/// <param name="Color">The colour to use.</param>
	/// <remarks>
	/// Several may be set; the highest boundary a point clears wins, so
	/// green-under-70, amber-over-70, red-over-90 reads in that order.
	/// </remarks>
	public readonly record struct ChartThreshold(double Above, Color Color);

	/// <summary>Where a chart's title and subtitle sit.</summary>
	public enum ChartTitleAlignment
	{
		/// <summary>Against the left edge. The default.</summary>
		Left,

		/// <summary>Centred over the control.</summary>
		Center,

		/// <summary>Against the right edge.</summary>
		Right,
	}

	/// <summary>Which way bars run.</summary>
	/// <remarks>
	/// Horizontal with a one-row plot is the inline gauge shape: there is no
	/// vertical dimension left, so the bar's LENGTH is the value and the colour
	/// along it says where that value sits on the scale.
	/// </remarks>
	public enum ChartOrientation
	{
		/// <summary>Bars rise from a baseline at the bottom. The default.</summary>
		Vertical,

		/// <summary>Bars run from a baseline at the left.</summary>
		Horizontal,
	}

	/// <summary>How the plot spends its width.</summary>
	/// <remarks>
	/// A live feed wants the second of these: a chart that rescales every time a
	/// sample lands makes the whole trace shift under the reader, while a fixed
	/// step lets it march rightward with earlier samples staying put. That is how
	/// <see cref="SparklineControl"/> has always behaved.
	/// </remarks>
	public enum ChartSizing
	{
		/// <summary>
		/// The data spans the whole plot, rescaling as points are added. The default.
		/// </summary>
		Stretch,

		/// <summary>
		/// Each point takes a fixed number of cells, so the plot grows with the data
		/// and earlier points do not move.
		/// </summary>
		FixedStep,
	}

	/// <summary>Which direction a series gradient runs in.</summary>
	/// <remarks>
	/// The library had two gradient meanings before this control: LineGraph coloured
	/// by position along the plot, Sparkline and BarGraph by value. Both are useful
	/// and they answer different questions - "where in the run is this?" against
	/// "how big is this?" - so the chart expresses both and lets each series choose.
	/// </remarks>
	public enum ChartGradientAxis
	{
		/// <summary>
		/// Along the plot, left to right: progression. The default, matching
		/// <see cref="LineGraphControl"/>.
		/// </summary>
		X,

		/// <summary>
		/// By value, bottom to top: magnitude. Low values take the first stop and
		/// high values the last, so a threshold is visible without reading the axis.
		/// </summary>
		Y,

		/// <summary>
		/// A diagonal blend of both positions.
		/// </summary>
		/// <remarks>
		/// One gradient sampled at the mean of the two positions, not a
		/// two-dimensional colour field: the result reads as a diagonal sweep. Most
		/// useful for scatter, where both coordinates carry information.
		/// </remarks>
		Both,
	}

	/// <summary>What an axis's numbers mean.</summary>
	public enum ChartAxisKind
	{
		/// <summary>Plain numbers.</summary>
		Numeric,

		/// <summary>DateTime ticks; labels are formatted as dates and times.</summary>
		Time,

		/// <summary>Indices into a list of category labels.</summary>
		Category,
	}

	/// <summary>Where the legend sits relative to the plot.</summary>
	public enum ChartLegendPosition
	{
		/// <summary>Above the plot.</summary>
		Top,

		/// <summary>Below the plot.</summary>
		Bottom,

		/// <summary>Left of the plot.</summary>
		Left,

		/// <summary>Right of the plot.</summary>
		Right,

		/// <summary>Not shown.</summary>
		None,
	}

	/// <summary>One datum: a position and an optional label.</summary>
	/// <param name="X">
	/// The horizontal position. For a time axis this is <see cref="DateTime.Ticks"/>;
	/// for a category axis it is the category's index.
	/// </param>
	/// <param name="Y">The value.</param>
	/// <param name="Label">A category name or point annotation, if any.</param>
	public readonly record struct ChartPoint(double X, double Y, string? Label = null);

	/// <summary>Carries the point a selection or activation refers to.</summary>
	public sealed class ChartPointEventArgs : EventArgs
	{
		/// <summary>The point, or null when the selection was cleared.</summary>
		public ChartPoint? Point { get; init; }

		/// <summary>The series the point belongs to, or null when cleared.</summary>
		public ChartSeries? Series { get; init; }
	}

	/// <summary>Carries the visible X range after a zoom or pan.</summary>
	public sealed class ChartRangeEventArgs : EventArgs
	{
		/// <summary>Low end of the visible range.</summary>
		public double Min { get; init; }

		/// <summary>High end of the visible range.</summary>
		public double Max { get; init; }

		/// <summary>False when the chart has been reset to the full data range.</summary>
		public bool IsZoomed { get; init; }
	}

	/// <summary>
	/// A named set of points with its own appearance.
	/// </summary>
	/// <remarks>
	/// <see cref="PlotType"/> is per series and defaults to the chart's, which is
	/// what makes a combo chart - bars with a line over them - a configuration
	/// rather than a special case.
	/// </remarks>
	public sealed class ChartSeries
	{
		private readonly List<ChartPoint> _points = new();

		/// <summary>The name shown in the legend and in hover readouts.</summary>
		public required string Name { get; init; }

		/// <summary>The series colour.</summary>
		public required Color Color { get; set; }

		/// <summary>How this series is drawn; null follows the chart's own setting.</summary>
		public ChartPlotType? PlotType { get; set; }

		/// <summary>Colours the series from a gradient instead of using <see cref="Color"/>.</summary>
		public ColorGradient? Gradient { get; set; }

		/// <summary>
		/// Which direction <see cref="Gradient"/> runs in. Defaults to
		/// <see cref="ChartGradientAxis.X"/>.
		/// </summary>
		public ChartGradientAxis GradientAxis { get; set; } = ChartGradientAxis.X;

		/// <summary>Whether the series is drawn. The legend toggles this.</summary>
		public bool IsVisible { get; set; } = true;

		/// <summary>
		/// Measures this series against the chart's secondary vertical axis.
		/// </summary>
		/// <remarks>
		/// For a series in different units from the rest - latency beside request
		/// rate - which on a shared scale flattens onto the floor and says nothing.
		/// </remarks>
		public bool UseSecondaryAxis { get; set; }

		/// <summary>The points, in the order given.</summary>
		public IReadOnlyList<ChartPoint> Points => _points;

		/// <summary>Colour boundaries, highest-first. Empty unless set.</summary>
		internal IReadOnlyList<ChartThreshold> Thresholds => _thresholds;

		private ChartThreshold[] _thresholds = Array.Empty<ChartThreshold>();

		/// <summary>
		/// Makes the series change colour past the given values.
		/// </summary>
		/// <param name="thresholds">The boundaries, in any order.</param>
		/// <remarks>
		/// Sorted highest-first so the first match wins, which is what makes
		/// overlapping boundaries read in the order a person would expect.
		/// Ignored when the series has a <see cref="Gradient"/>: both set is a
		/// contradiction, and the gradient is the more specific request.
		/// </remarks>
		public void SetThresholds(params ChartThreshold[] thresholds)
		{
			ArgumentNullException.ThrowIfNull(thresholds);
			_thresholds = thresholds.OrderByDescending(t => t.Above).ToArray();
		}

		/// <summary>Removes every colour boundary.</summary>
		public void ClearThresholds() => _thresholds = Array.Empty<ChartThreshold>();

		/// <summary>The colour a value takes, honouring any thresholds.</summary>
		internal Color ColorForValue(double value)
		{
			foreach (var t in _thresholds)
			{
				if (value >= t.Above) return t.Color;
			}
			return Color;
		}

		/// <summary>Replaces the points.</summary>
		/// <param name="points">The new points. Non-finite coordinates are dropped.</param>
		public void SetPoints(IEnumerable<ChartPoint> points)
		{
			ArgumentNullException.ThrowIfNull(points);
			_points.Clear();
			foreach (var p in points)
			{
				if (IsFinite(p.X) && IsFinite(p.Y)) _points.Add(p);
			}
		}

		/// <summary>Replaces the points with evenly spaced values, X running 0, 1, 2…</summary>
		/// <param name="yValues">The values. Non-finite ones are dropped.</param>
		public void SetValues(IEnumerable<double> yValues)
		{
			ArgumentNullException.ThrowIfNull(yValues);
			_points.Clear();
			int i = 0;
			foreach (double y in yValues)
			{
				// The index advances only for points that are kept, so a dropped
				// value closes the gap rather than leaving a hole in the X run.
				if (IsFinite(y)) _points.Add(new ChartPoint(i++, y));
			}
		}

		/// <summary>Replaces the points with labelled categories, X being the index.</summary>
		/// <param name="data">Category name and value pairs.</param>
		public void SetCategories(IEnumerable<(string Category, double Value)> data)
		{
			ArgumentNullException.ThrowIfNull(data);
			_points.Clear();
			int i = 0;
			foreach (var (category, value) in data)
			{
				if (IsFinite(value)) _points.Add(new ChartPoint(i++, value, category));
			}
		}

		/// <summary>Replaces the points with a time series; X carries <see cref="DateTime.Ticks"/>.</summary>
		/// <param name="data">Timestamp and value pairs.</param>
		public void SetTimeSeries(IEnumerable<(DateTime When, double Value)> data)
		{
			ArgumentNullException.ThrowIfNull(data);
			_points.Clear();
			foreach (var (when, value) in data)
			{
				if (IsFinite(value)) _points.Add(new ChartPoint(when.Ticks, value));
			}
		}

		/// <summary>Appends one point, for streaming data.</summary>
		/// <param name="point">The point. Ignored when either coordinate is non-finite.</param>
		public void AddPoint(ChartPoint point)
		{
			if (IsFinite(point.X) && IsFinite(point.Y)) _points.Add(point);
		}

		/// <summary>Removes every point.</summary>
		public void Clear() => _points.Clear();

		/// <summary>
		/// Keeps only the most recent <paramref name="max"/> points.
		/// </summary>
		/// <param name="max">How many points to keep. Negative counts as zero.</param>
		/// <remarks>For a live feed that would otherwise grow without bound.</remarks>
		public void TrimTo(int max)
		{
			if (max < 0) max = 0;
			if (_points.Count > max) _points.RemoveRange(0, _points.Count - max);
		}

		/// <summary>
		/// True when a value can take part in arithmetic.
		/// </summary>
		/// <remarks>
		/// NaN and the infinities are dropped at the door. One of them reaching the
		/// axis bounds makes every tick on that axis NaN, so the whole chart goes
		/// blank from a single bad sample - which a metrics feed will eventually send.
		/// </remarks>
		private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
	}
}

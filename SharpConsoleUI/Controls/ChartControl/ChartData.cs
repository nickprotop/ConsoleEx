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

		/// <summary>The points, in the order given.</summary>
		public IReadOnlyList<ChartPoint> Points => _points;

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

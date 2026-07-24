// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Geometry the <see cref="SparklineControl"/> hands an <c>AxisProvider</c> so the caller can
	/// decide where the axis ticks go and what they say. The control owns only this geometry; the
	/// caller owns the meaning (time, sample index, …) and the appearance.
	/// </summary>
	/// <param name="PointCount">Number of data points currently displayed (each occupies one column).</param>
	/// <param name="GraphWidth">Rendered width of the graph area, in columns.</param>
	/// <param name="UnitsPerPoint">The caller-supplied value each point represents (e.g. seconds-per-sample).</param>
	public readonly record struct SparklineAxisContext(int PointCount, int GraphWidth, double UnitsPerPoint);

	/// <summary>
	/// A single tick the caller asks the <see cref="SparklineControl"/> to draw on its X-axis. The
	/// control positions the label at the column of <paramref name="PointIndex"/> (0 = oldest shown
	/// point on the left, <c>PointCount-1</c> = newest on the right) and clips it to the graph width.
	/// </summary>
	/// <param name="PointIndex">Data-point index the tick anchors to (0..PointCount-1).</param>
	/// <param name="Label">Text to draw (markup supported).</param>
	/// <param name="Color">Optional label color; falls back to the sparkline foreground when null.</param>
	public readonly record struct SparklineAxisTick(int PointIndex, string Label, Color? Color = null);
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Specifies where a reference line label is displayed relative to the line.
	/// </summary>
	public enum LabelPosition
	{
		/// <summary>
		/// No label is displayed.
		/// </summary>
		None,

		/// <summary>
		/// The label is displayed on the left side.
		/// </summary>
		Left,

		/// <summary>
		/// The label is displayed on the right side.
		/// </summary>
		Right
	}

	/// <summary>
	/// Specifies which side of the graph a value marker arrow and label appear on.
	/// </summary>
	public enum MarkerSide
	{
		/// <summary>
		/// The marker appears on the left side of the graph.
		/// </summary>
		Left,

		/// <summary>
		/// The marker appears on the right side of the graph.
		/// </summary>
		Right
	}

	/// <summary>
	/// Represents a horizontal reference line drawn at a fixed Y-axis value across the graph area.
	/// Used to indicate thresholds, targets, or other significant values.
	/// </summary>
	public class ReferenceLine
	{
		/// <summary>
		/// Gets the Y-axis value where this reference line is drawn.
		/// </summary>
		public double Value { get; }

		/// <summary>
		/// Gets the color of the reference line.
		/// </summary>
		public Color Color { get; }

		/// <summary>
		/// Gets the character used to draw the reference line.
		/// </summary>
		public char LineChar { get; }

		/// <summary>
		/// Gets the optional label text displayed alongside the reference line.
		/// </summary>
		public string? Label { get; }

		/// <summary>
		/// Gets the position of the label relative to the reference line.
		/// </summary>
		public LabelPosition LabelPosition { get; }

		/// <summary>
		/// Creates a new reference line at the specified value.
		/// </summary>
		/// <param name="value">The Y-axis value where the line is drawn.</param>
		/// <param name="color">The color of the line.</param>
		/// <param name="lineChar">The character used to draw the line.</param>
		/// <param name="label">Optional label text.</param>
		/// <param name="labelPosition">Position of the label relative to the line.</param>
		public ReferenceLine(double value, Color color, char lineChar = '─', string? label = null, LabelPosition labelPosition = LabelPosition.None)
		{
			Value = value;
			Color = color;
			LineChar = lineChar;
			Label = label;
			LabelPosition = labelPosition;
		}
	}

	/// <summary>
	/// Represents a labeled arrow marker pointing at a specific Y-axis value on the graph edge.
	/// Used to highlight current values, targets, or notable data points.
	/// </summary>
	public class ValueMarker
	{
		/// <summary>
		/// Gets the Y-axis value this marker points to.
		/// </summary>
		public double Value { get; }

		/// <summary>
		/// Gets the label text displayed next to the marker arrow.
		/// </summary>
		public string Label { get; }

		/// <summary>
		/// Gets the color of the marker arrow.
		/// </summary>
		public Color ArrowColor { get; }

		/// <summary>
		/// Gets the color of the label text.
		/// </summary>
		public Color LabelColor { get; }

		/// <summary>
		/// Gets which side of the graph the marker appears on.
		/// </summary>
		public MarkerSide Side { get; }

		/// <summary>
		/// Creates a new value marker at the specified value.
		/// </summary>
		/// <param name="value">The Y-axis value to mark.</param>
		/// <param name="label">The label text.</param>
		/// <param name="arrowColor">The color of the marker arrow.</param>
		/// <param name="labelColor">The color of the label text.</param>
		/// <param name="side">Which side of the graph the marker appears on.</param>
		public ValueMarker(double value, string label, Color arrowColor, Color labelColor, MarkerSide side = MarkerSide.Right)
		{
			Value = value;
			Label = label;
			ArrowColor = arrowColor;
			LabelColor = labelColor;
			Side = side;
		}
	}

	/// <summary>
	/// Partial class for overlay-related rendering and measurement methods.
	/// </summary>
	public partial class LineGraphControl
	{
		/// <summary>Computes the Y row for a data value within the graph area.</summary>
		/// <param name="value">The data value to map to a row.</param>
		/// <param name="graphStartY">The top row of the graph area.</param>
		/// <param name="graphHeight">The height of the graph area in rows.</param>
		/// <param name="globalMin">The minimum value of the Y-axis range.</param>
		/// <param name="globalMax">The maximum value of the Y-axis range.</param>
		/// <returns>The clamped row index corresponding to the value.</returns>
		private static int ComputeYRow(double value, int graphStartY, int graphHeight, double globalMin, double globalMax)
		{
			double range = globalMax - globalMin;
			if (range < 0.001) range = 1.0;
			int row = graphStartY + graphHeight - 1 - (int)((value - globalMin) / range * (graphHeight - 1));
			return Math.Clamp(row, graphStartY, graphStartY + graphHeight - 1);
		}

		/// <summary>Computes width needed for right-side overlays.</summary>
		/// <param name="globalMin">The minimum value of the Y-axis range.</param>
		/// <param name="globalMax">The maximum value of the Y-axis range.</param>
		/// <returns>The number of columns to reserve on the right side.</returns>
		private int GetRightOverlayWidth(double globalMin, double globalMax)
		{
			int maxWidth = 0;
			lock (_dataLock)
			{
				foreach (var marker in _valueMarkers)
				{
					if (marker.Side == MarkerSide.Right)
					{
						int w = ControlDefaults.LineGraphMarkerPadding + 1 + UnicodeWidth.GetStringWidth(marker.Label);
						maxWidth = Math.Max(maxWidth, w);
					}
				}
				foreach (var refLine in _referenceLines)
				{
					if (refLine.LabelPosition == LabelPosition.Right && refLine.Label != null)
					{
						int w = ControlDefaults.LineGraphMarkerPadding + UnicodeWidth.GetStringWidth(refLine.Label);
						maxWidth = Math.Max(maxWidth, w);
					}
				}
			}
			if (_showHighLowLabels && _highLowLabelSide == MarkerSide.Right)
			{
				string highLabel = "H " + globalMax.ToString(_axisLabelFormat);
				string lowLabel = "L " + globalMin.ToString(_axisLabelFormat);
				int hlWidth = ControlDefaults.LineGraphMarkerPadding + 1 +
					Math.Max(UnicodeWidth.GetStringWidth(highLabel), UnicodeWidth.GetStringWidth(lowLabel));
				maxWidth = Math.Max(maxWidth, hlWidth);
			}
			return maxWidth;
		}

		/// <summary>Computes width needed for left-side overlays (additional to yAxisWidth).</summary>
		/// <param name="globalMin">The minimum value of the Y-axis range.</param>
		/// <param name="globalMax">The maximum value of the Y-axis range.</param>
		/// <returns>The number of columns to reserve on the left side.</returns>
		private int GetLeftOverlayWidth(double globalMin, double globalMax)
		{
			int maxWidth = 0;
			lock (_dataLock)
			{
				foreach (var marker in _valueMarkers)
				{
					if (marker.Side == MarkerSide.Left)
					{
						int w = UnicodeWidth.GetStringWidth(marker.Label) + ControlDefaults.LineGraphMarkerPadding + 1;
						maxWidth = Math.Max(maxWidth, w);
					}
				}
				foreach (var refLine in _referenceLines)
				{
					if (refLine.LabelPosition == LabelPosition.Left && refLine.Label != null)
					{
						int w = UnicodeWidth.GetStringWidth(refLine.Label) + ControlDefaults.LineGraphMarkerPadding;
						maxWidth = Math.Max(maxWidth, w);
					}
				}
			}
			if (_showHighLowLabels && _highLowLabelSide == MarkerSide.Left)
			{
				string highLabel = "H " + globalMax.ToString(_axisLabelFormat);
				string lowLabel = "L " + globalMin.ToString(_axisLabelFormat);
				int hlWidth = Math.Max(UnicodeWidth.GetStringWidth(highLabel), UnicodeWidth.GetStringWidth(lowLabel))
					+ ControlDefaults.LineGraphMarkerPadding + 1;
				maxWidth = Math.Max(maxWidth, hlWidth);
			}
			return maxWidth;
		}
	}
}

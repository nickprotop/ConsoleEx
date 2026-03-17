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
	/// Partial class for overlay-related rendering methods (implemented in later tasks).
	/// </summary>
	public partial class LineGraphControl
	{
	}
}

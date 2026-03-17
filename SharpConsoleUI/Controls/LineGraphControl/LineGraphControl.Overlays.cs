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
using SharpConsoleUI.Parsing;

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

		/// <summary>
		/// Braille empty character value used for detecting empty braille cells.
		/// </summary>
		private const int BrailleEmptyCodepoint = 0x2800;

		/// <summary>
		/// Paints reference lines across the graph area. Called AFTER series data
		/// so that reference lines only appear on cells not occupied by series dots.
		/// Cells containing active braille dots are preserved; empty braille cells
		/// and blank cells are overwritten with the reference line character.
		/// </summary>
		/// <param name="buffer">The character buffer to paint into.</param>
		/// <param name="graphStartX">The left column of the graph area.</param>
		/// <param name="graphStartY">The top row of the graph area.</param>
		/// <param name="graphWidth">The width of the graph area in columns.</param>
		/// <param name="graphHeight">The height of the graph area in rows.</param>
		/// <param name="clipRect">The clipping rectangle.</param>
		/// <param name="bgColor">The background color.</param>
		/// <param name="refLines">The reference lines to paint.</param>
		/// <param name="globalMin">The minimum value of the Y-axis range.</param>
		/// <param name="globalMax">The maximum value of the Y-axis range.</param>
		/// <param name="startX">The left edge of the content area.</param>
		/// <param name="borderSize">The border size (0 or 1).</param>
		private void PaintReferenceLines(
			CharacterBuffer buffer, int graphStartX, int graphStartY,
			int graphWidth, int graphHeight, LayoutRect clipRect, Color bgColor,
			List<ReferenceLine> refLines, double globalMin, double globalMax,
			int startX, int borderSize)
		{
			foreach (var refLine in refLines)
			{
				// Skip if value is outside the displayed range
				if (refLine.Value < globalMin || refLine.Value > globalMax)
					continue;

				int row = ComputeYRow(refLine.Value, graphStartY, graphHeight, globalMin, globalMax);
				if (row < clipRect.Y || row >= clipRect.Bottom)
					continue;

				// Draw the horizontal line across the graph area,
				// only on cells not occupied by active series data
				for (int x = graphStartX; x < graphStartX + graphWidth; x++)
				{
					if (x >= clipRect.X && x < clipRect.Right)
					{
						var existing = buffer.GetCell(x, row);
						int cp = existing.Character.Value;
						bool isEmpty = cp == 0 || cp == ' ' || cp == BrailleEmptyCodepoint;
						if (isEmpty)
							buffer.SetNarrowCell(x, row, refLine.LineChar, refLine.Color, bgColor);
					}
				}

				// Draw label if configured
				if (!string.IsNullOrEmpty(refLine.Label))
				{
					var labelCells = MarkupParser.Parse(refLine.Label, refLine.Color, bgColor);
					if (refLine.LabelPosition == LabelPosition.Left)
					{
						int labelX = startX + borderSize;
						buffer.WriteCellsClipped(labelX, row, labelCells, clipRect);
					}
					else if (refLine.LabelPosition == LabelPosition.Right)
					{
						int labelX = graphStartX + graphWidth + ControlDefaults.LineGraphMarkerPadding;
						buffer.WriteCellsClipped(labelX, row, labelCells, clipRect);
					}
				}
			}
		}

		/// <summary>
		/// Paints value markers (arrow + label) at graph edges. Called AFTER series data.
		/// Resolves overlapping markers by offsetting to adjacent rows.
		/// </summary>
		/// <param name="buffer">The character buffer to paint into.</param>
		/// <param name="graphStartX">The left column of the graph area.</param>
		/// <param name="graphStartY">The top row of the graph area.</param>
		/// <param name="graphWidth">The width of the graph area in columns.</param>
		/// <param name="graphHeight">The height of the graph area in rows.</param>
		/// <param name="clipRect">The clipping rectangle.</param>
		/// <param name="bgColor">The background color.</param>
		/// <param name="markers">The value markers to paint.</param>
		/// <param name="globalMin">The minimum value of the Y-axis range.</param>
		/// <param name="globalMax">The maximum value of the Y-axis range.</param>
		/// <param name="startX">The left edge of the content area.</param>
		/// <param name="borderSize">The border size (0 or 1).</param>
		private void PaintValueMarkers(
			CharacterBuffer buffer, int graphStartX, int graphStartY,
			int graphWidth, int graphHeight, LayoutRect clipRect, Color bgColor,
			List<ValueMarker> markers, double globalMin, double globalMax,
			int startX, int borderSize)
		{
			if (markers.Count == 0) return;

			// Compute initial Y rows and sort for overlap resolution
			var resolved = markers
				.Select(m => (marker: m, row: ComputeYRow(m.Value, graphStartY, graphHeight, globalMin, globalMax)))
				.OrderBy(x => x.row)
				.ToList();

			// Resolve overlaps: offset markers that share a row
			var usedRows = new HashSet<int>();
			for (int i = 0; i < resolved.Count; i++)
			{
				var (marker, row) = resolved[i];
				int originalRow = row;
				int offset = 0;
				while (usedRows.Contains(row))
				{
					offset++;
					// Try below first, then above
					int below = originalRow + offset;
					int above = originalRow - offset;
					if (below <= graphStartY + graphHeight - 1 && !usedRows.Contains(below))
					{
						row = below;
						break;
					}
					if (above >= graphStartY && !usedRows.Contains(above))
					{
						row = above;
						break;
					}
					if (offset > graphHeight)
						break; // No room — skip this marker
				}
				usedRows.Add(row);
				resolved[i] = (marker, row);
			}

			// Render each marker
			foreach (var (marker, row) in resolved)
			{
				if (row < clipRect.Y || row >= clipRect.Bottom)
					continue;

				if (marker.Side == MarkerSide.Right)
				{
					int arrowX = graphStartX + graphWidth;
					var arrowCells = MarkupParser.Parse(ControlDefaults.LineGraphMarkerArrowRight, marker.ArrowColor, bgColor);
					buffer.WriteCellsClipped(arrowX, row, arrowCells, clipRect);

					int labelX = arrowX + arrowCells.Count;
					var labelCells = MarkupParser.Parse(marker.Label, marker.LabelColor, bgColor);
					buffer.WriteCellsClipped(labelX, row, labelCells, clipRect);
				}
				else
				{
					var labelCells = MarkupParser.Parse(marker.Label, marker.LabelColor, bgColor);
					int arrowX = graphStartX - 1;
					int labelX = arrowX - labelCells.Count;
					labelX = Math.Max(startX + borderSize, labelX);
					buffer.WriteCellsClipped(labelX, row, labelCells, clipRect);

					var arrowCells = MarkupParser.Parse(ControlDefaults.LineGraphMarkerArrowLeft, marker.ArrowColor, bgColor);
					buffer.WriteCellsClipped(arrowX, row, arrowCells, clipRect);
				}
			}
		}

		/// <summary>
		/// Generates transient ValueMarker entries for high/low labels from data extremes.
		/// Skips rows already occupied by user-defined markers.
		/// </summary>
		/// <param name="snapshots">The series data snapshots.</param>
		/// <param name="globalMin">The minimum value of the Y-axis range.</param>
		/// <param name="globalMax">The maximum value of the Y-axis range.</param>
		/// <param name="usedRows">Set of rows already occupied by user-defined markers.</param>
		/// <param name="graphStartY">The top row of the graph area.</param>
		/// <param name="graphHeight">The height of the graph area in rows.</param>
		/// <returns>A list of value markers for the high and low data extremes.</returns>
		private List<ValueMarker> GenerateHighLowMarkers(
			List<(LineGraphSeries series, List<double> data)> snapshots,
			double globalMin, double globalMax,
			HashSet<int> usedRows, int graphStartY, int graphHeight)
		{
			var result = new List<ValueMarker>();
			if (!_showHighLowLabels)
				return result;

			double dataMin = double.MaxValue;
			double dataMax = double.MinValue;
			foreach (var (_, data) in snapshots)
			{
				foreach (var v in data)
				{
					if (v < dataMin) dataMin = v;
					if (v > dataMax) dataMax = v;
				}
			}
			if (dataMin == double.MaxValue)
				return result;

			string highLabel = "H " + dataMax.ToString(_axisLabelFormat);
			string lowLabel = "L " + dataMin.ToString(_axisLabelFormat);

			int highRow = ComputeYRow(dataMax, graphStartY, graphHeight, globalMin, globalMax);
			int lowRow = ComputeYRow(dataMin, graphStartY, graphHeight, globalMin, globalMax);

			if (!usedRows.Contains(highRow))
				result.Add(new ValueMarker(dataMax, highLabel, _highLabelColor, _highLabelColor, _highLowLabelSide));

			if (!usedRows.Contains(lowRow))
				result.Add(new ValueMarker(dataMin, lowLabel, _lowLabelColor, _lowLabelColor, _highLowLabelSide));

			return result;
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

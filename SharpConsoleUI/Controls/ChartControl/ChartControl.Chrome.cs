// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// The frame around the plot: title, subtitle, border, axis titles, axis
	/// labels and axis lines.
	/// </summary>
	/// <remarks>
	/// Separated from the plot rendering because the two answer different
	/// questions - where the data goes against what surrounds it - and because
	/// together they exceeded the file-size limit.
	/// </remarks>
	public partial class ChartControl
	{
		private void PaintTitle(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color fg, Color bg)
		{
			int border = _borderStyle != BorderStyle.None ? 1 : 0;
			int left = bounds.X + Margin.Left + border;
			int width = bounds.Width - Margin.Left - Margin.Right - (border * 2);
			int y = bounds.Y + Margin.Top + border;
			if (width <= 0) return;

			if (!string.IsNullOrEmpty(_title))
			{
				WriteAligned(buffer, clipRect, _title, left, y, width,
					_titleColor ?? fg, bg);
				y++;
			}

			if (!string.IsNullOrEmpty(_subtitle))
			{
				// Dimmed by default, so it reads as secondary to the title rather
				// than competing with it.
				WriteAligned(buffer, clipRect, _subtitle, left, y, width,
					_subtitleColor ?? ControlDefaults.ChartSubtitleColor, bg);
			}
		}

		/// <summary>Writes one line of header text at the chart's title alignment.</summary>
		private void WriteAligned(CharacterBuffer buffer, LayoutRect clipRect, string text,
			int left, int y, int width, Color fg, Color bg)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			var cells = MarkupParser.Parse(text, fg, bg);
			int textWidth = MarkupParser.StripLength(text);

			int x = _titleAlignment switch
			{
				ChartTitleAlignment.Center => left + Math.Max(0, (width - textWidth) / 2),
				ChartTitleAlignment.Right => left + Math.Max(0, width - textWidth),
				_ => left,
			};

			buffer.WriteCellsClipped(x, y, cells, clipRect);
		}

		/// <summary>Draws the border around the whole control.</summary>
		private void PaintBorder(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color fg, Color bg)
		{
			if (_borderStyle == BorderStyle.None) return;

			var box = _borderStyle == BorderStyle.DoubleLine ? BoxChars.Double : BoxChars.Single;
			Color colour = _borderColor ?? fg;

			int left = bounds.X + Margin.Left;
			int right = bounds.X + bounds.Width - Margin.Right - 1;
			int top = bounds.Y + Margin.Top;
			int bottom = bounds.Y + bounds.Height - Margin.Bottom - 1;
			if (right <= left || bottom <= top) return;

			for (int x = left + 1; x < right; x++)
			{
				Put(buffer, clipRect, x, top, box.Horizontal, colour, bg);
				Put(buffer, clipRect, x, bottom, box.Horizontal, colour, bg);
			}

			for (int y = top + 1; y < bottom; y++)
			{
				Put(buffer, clipRect, left, y, box.Vertical, colour, bg);
				Put(buffer, clipRect, right, y, box.Vertical, colour, bg);
			}

			Put(buffer, clipRect, left, top, box.TopLeft, colour, bg);
			Put(buffer, clipRect, right, top, box.TopRight, colour, bg);
			Put(buffer, clipRect, left, bottom, box.BottomLeft, colour, bg);
			Put(buffer, clipRect, right, bottom, box.BottomRight, colour, bg);
		}

		/// <summary>Draws the axis titles, if either axis names itself.</summary>
		private void PaintAxisTitles(CharacterBuffer buffer, LayoutRect bounds,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color fg, Color bg)
		{
			Color colour = ControlDefaults.ChartAxisTitleColor;

			if (!string.IsNullOrEmpty(YAxis.Title))
			{
				// Above the plot at the left, where the gutter already reserves room.
				int y = plot.Y - 1;
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					var cells = MarkupParser.Parse(YAxis.Title, colour, bg);
					buffer.WriteCellsClipped(bounds.X + Margin.Left, y, cells, clipRect);
				}
			}

			if (!string.IsNullOrEmpty(XAxis.Title))
			{
				// Below the tick labels, centred under the plot.
				int y = plot.Y + plot.Height + (XAxis.ShowLabels ? 1 : 0);
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					int textWidth = MarkupParser.StripLength(XAxis.Title);
					int x = plot.X + Math.Max(0, (plot.Width - textWidth) / 2);
					var cells = MarkupParser.Parse(XAxis.Title, colour, bg);
					buffer.WriteCellsClipped(x, y, cells, clipRect);
				}
			}
		}

		private static void Put(CharacterBuffer buffer, LayoutRect clip, int x, int y,
			char ch, Color fg, Color bg)
		{
			if (x < clip.X || x >= clip.Right || y < clip.Y || y >= clip.Bottom) return;
			buffer.SetNarrowCell(x, y, ch, fg, bg);
		}

		private void PaintYLabels(CharacterBuffer buffer, LayoutRect bounds,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color fg, Color bg,
			IReadOnlyList<ChartTick> ticks, double yMin, double yMax)
		{
			if (!YAxis.ShowLabels) return;

			for (int i = 0; i < ticks.Count; i++)
			{
				var tick = ticks[i];

				// A mirrored run is the same ticks twice: the first half labels the
				// upper band and the second the lower, both measured from the centre.
				int row = _splitView
					? RowInHalf(tick.Value, yMin, yMax, plot.Height,
						upper: i < ticks.Count / 2)
					: ValueToRow(tick.Value, yMin, yMax, plot.Height);

				int y = plot.Y + row;
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;

				var cells = MarkupParser.Parse(tick.Label, fg, bg);
				int width = UnicodeWidth.GetStringWidth(tick.Label);

				// Right-aligned against the plot edge, so the numbers line up.
				int x = plot.X - width - ControlDefaults.ChartAxisLabelPadding;
				if (x < bounds.X) x = bounds.X;

				buffer.WriteCellsClipped(x, y, cells, clipRect);
			}
		}

		/// <summary>Labels the secondary axis down the right of the plot.</summary>
		private void PaintSecondaryLabels(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color fg, Color bg,
			IReadOnlyList<ChartTick> ticks, double min, double max)
		{
			if (!SecondaryYAxis.ShowLabels) return;

			foreach (var tick in ticks)
			{
				int y = plot.Y + ValueToRow(tick.Value, min, max, plot.Height);
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;

				// MeasureYGutter already reserved label width plus the padding, so
				// the text starts one cell clear of the plot and ends inside it.
				int x = plot.X + plot.Width + ControlDefaults.ChartAxisLabelPadding;
				if (x >= clipRect.Right) continue;

				var cells = MarkupParser.Parse(tick.Label, fg, bg);
				buffer.WriteCellsClipped(x, y, cells, clipRect);
			}
		}

		private void PaintXLabels(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color fg, Color bg,
			IReadOnlyList<ChartTick> ticks, double xMin, double xMax, bool barred)
		{
			if (!XAxis.ShowLabels) return;

			int y = plot.Y + plot.Height;
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			int lastRight = int.MinValue;

			foreach (var tick in ticks)
			{
				int width = UnicodeWidth.GetStringWidth(tick.Label);
				if (width == 0) continue;

				// Bars occupy a slot and are inset by half of one, so their labels have
				// to use the same mapping or every label drifts off its own bar.
				int centre = plot.X + (barred
					? BarLabelColumn(tick.Value, xMin, xMax, plot.Width, ticks.Count)
					: ValueToColumn(tick.Value, xMin, xMax, plot.Width));
				int x = centre - width / 2;
				if (x < plot.X) x = plot.X;
				if (x + width > plot.X + plot.Width) x = plot.X + plot.Width - width;

				// Labels are placed independently, so two can overlap where ticks are
				// close. Dropping the later one keeps the axis readable rather than
				// painting one label's tail over another's head.
				if (x <= lastRight) continue;

				var cells = MarkupParser.Parse(tick.Label, fg, bg);
				buffer.WriteCellsClipped(x, y, cells, clipRect);
				lastRight = x + width;
			}
		}

		/// <summary>
		/// The Y ticks, mirrored about the centre when the plot is split.
		/// </summary>
		/// <remarks>
		/// In split view both series grow away from the middle, so the middle IS zero
		/// and each half runs the same positive range outward. One continuous tick run
		/// would label the centre with the midpoint of the data - saying 50 on the line
		/// that means 0 - which misreads every point on the chart.
		/// </remarks>
		private IReadOnlyList<ChartTick> SelectYTicks(double yMin, double yMax, int height)
		{
			if (!_splitView) return YAxis.SelectTicks(yMin, yMax, height);

			// Each half gets its own run over the same range, so the labels are
			// symmetric and the shared zero lands on the centre line.
			var half = YAxis.SelectTicks(yMin, yMax, Math.Max(1, height / 2));
			var mirrored = new List<ChartTick>(half.Count * 2);

			foreach (var t in half) mirrored.Add(t);
			foreach (var t in half) mirrored.Add(t);

			return mirrored;
		}

		/// <summary>
		/// Draws the rules along the left and bottom edges of the plot.
		/// </summary>
		/// <remarks>
		/// Painted after the plot so the frame reads as a frame, and only on cells the
		/// data did not claim - a rule that overwrote a point would hide exactly the
		/// values nearest the axis, which are usually the interesting ones.
		/// </remarks>
		private void PaintAxisLines(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg)
		{
			if (!_showAxisLines) return;

			var box = BoxChars.Single;
			Color line = ControlDefaults.ChartAxisLineColor;

			int axisX = plot.X - 1;                  // the column left of the plot
			int axisY = plot.Y + plot.Height;        // the row below it

			if (axisX >= clipRect.X)
			{
				for (int y = plot.Y; y < plot.Y + plot.Height; y++)
				{
					if (y < clipRect.Y || y >= clipRect.Bottom) continue;
					buffer.SetNarrowCell(axisX, y, box.Vertical, line, bg);
				}
			}

			if (axisY >= clipRect.Y && axisY < clipRect.Bottom)
			{
				for (int x = plot.X; x < plot.X + plot.Width; x++)
				{
					if (x < clipRect.X || x >= clipRect.Right) continue;
					buffer.SetNarrowCell(x, axisY, box.Horizontal, line, bg);
				}
			}

			// Where they meet.
			if (axisX >= clipRect.X && axisX < clipRect.Right
				&& axisY >= clipRect.Y && axisY < clipRect.Bottom)
			{
				buffer.SetNarrowCell(axisX, axisY, box.BottomLeft, line, bg);
			}
		}
	}
}

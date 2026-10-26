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
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	public partial class ChartControl
	{
		#region ColorRole

		private ColorRole _role = ColorRole.Default;
		private ThemeMode? _colorRoleMode;
		private bool _outline;

		/// <inheritdoc/>
		public ColorRole ColorRole
		{
			get => _role;
			set => SetProperty(ref _role, value);
		}

		/// <inheritdoc/>
		public ThemeMode? ColorRoleMode
		{
			get => _colorRoleMode;
			set => SetProperty(ref _colorRoleMode, value);
		}

		/// <inheritdoc/>
		public bool Outline
		{
			get => _outline;
			set => SetProperty(ref _outline, value);
		}

		#endregion

		/// <summary>Rows the chrome needs: the title and the X axis labels.</summary>
		private int ChromeHeight()
		{
			int rows = 0;
			if (!string.IsNullOrEmpty(_title)) rows++;
			if (!string.IsNullOrEmpty(_subtitle)) rows++;
			if (!string.IsNullOrEmpty(YAxis.Title)) rows++;
			if (!string.IsNullOrEmpty(XAxis.Title)) rows++;
			if (XAxis.ShowLabels) rows++;
			if (_borderStyle != BorderStyle.None) rows += 2;
			rows += LegendRows();
			return rows;
		}

		/// <summary>Centres the empty-state message in the control.</summary>
		private void PaintEmptyMessage(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color bg)
		{
			if (string.IsNullOrEmpty(_emptyText)) return;

			int width = MarkupParser.StripLength(_emptyText);
			int x = bounds.X + Math.Max(0, (bounds.Width - width) / 2);
			int y = bounds.Y + bounds.Height / 2;
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			var cells = MarkupParser.Parse(_emptyText, ControlDefaults.ChartEmptyTextColor, bg);
			buffer.WriteCellsClipped(x, y, cells, clipRect);
		}

		/// <summary>Rows the title and subtitle take between them.</summary>
		private int HeaderRows()
		{
			int rows = 0;
			if (!string.IsNullOrEmpty(_title)) rows++;
			if (!string.IsNullOrEmpty(_subtitle)) rows++;
			return rows;
		}

		/// <summary>The plot area in buffer cells, after the chrome takes its share.</summary>
		/// <remarks>
		/// Returned with a non-negative width and height even when the control is too
		/// small to show anything: callers then skip drawing rather than working with
		/// a negative extent.
		/// </remarks>
		private (int X, int Y, int Width, int Height) PlotRect(LayoutRect bounds, int gutter,
			int rightGutter = 0)
		{
			int border = _borderStyle != BorderStyle.None ? 1 : 0;
			// The Y axis title gets a row above the plot, since a terminal cell
			// cannot carry sideways text and a column of single letters reads worse
			// than a short heading.
			int titleRows = HeaderRows() + (string.IsNullOrEmpty(YAxis.Title) ? 0 : 1);
			int xLabelRows = XAxis.ShowLabels ? 1 : 0;
			int xTitleRows = string.IsNullOrEmpty(XAxis.Title) ? 0 : 1;
			int legendRows = LegendRows();
			int topLegend = _legendPosition == ChartLegendPosition.Top ? legendRows : 0;

			int x = bounds.X + Margin.Left + border + gutter;
			int y = bounds.Y + Margin.Top + border + titleRows + topLegend;
			int width = bounds.Width - Margin.Left - Margin.Right - (border * 2) - gutter - rightGutter;
			int height = bounds.Height - Margin.Top - Margin.Bottom - (border * 2)
				- titleRows - xLabelRows - xTitleRows - legendRows;

			// An explicit PlotHeight caps the plot even when layout handed over more
			// room - a Fill-aligned chart is given the whole container, and without
			// this the setting is honoured in measure and ignored in paint.
			if (_plotHeight.HasValue) height = Math.Min(height, _plotHeight.Value);

			return (x, y, Math.Max(0, width), Math.Max(0, height));
		}

		/// <summary>
		/// The width the Y axis labels need.
		/// </summary>
		/// <remarks>
		/// Measured with <see cref="UnicodeWidth.GetStringWidth"/>, never string.Length:
		/// a label can carry a wide glyph or a combining mark, and a column count that
		/// disagrees with the terminal's shifts the whole plot.
		/// </remarks>
		private int MeasureYGutter(IReadOnlyList<ChartTick> ticks, ChartAxis? axis = null)
		{
			// The axis is a parameter: measuring the secondary gutter against the
			// primary axis's ShowLabels gave the right-hand labels no room at all.
			if (!(axis ?? YAxis).ShowLabels || ticks.Count == 0) return 0;

			int widest = 0;
			foreach (var t in ticks)
			{
				// Stripped: a markup tag in a provider label is not printed width.
				widest = Math.Max(widest, MarkupParser.StripLength(t.Label));
			}

			int gutter = widest > 0 ? widest + ControlDefaults.ChartAxisLabelPadding : 0;

			// The Y axis title is written above the labels rather than rotated down
			// the side: a terminal cell cannot carry sideways text, and a column of
			// single letters is harder to read than a short heading.
			return gutter;
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color defaultForeground, Color defaultBackground)
		{
			ArgumentNullException.ThrowIfNull(buffer);

			_pointPositions.Clear();

			Color bg = ColorResolver.ResolveLineGraphBackground(_backgroundColorValue, Container);
			Color fg = _foregroundColorValue ?? defaultForeground;

			var snapshot = SnapshotSeries();
			var visible = snapshot.Where(s => s.Series.IsVisible && s.Points.Count > 0).ToList();

			// Nothing to plot, by any route: no series, none with points, or every
			// one toggled off. All three look the same, so all three read the same.
			if (visible.Count == 0 && !string.IsNullOrEmpty(_emptyText))
			{
				PaintBorder(buffer, bounds, clipRect, fg, bg);
				PaintTitle(buffer, bounds, clipRect, fg, bg);
				PaintEmptyMessage(buffer, bounds, clipRect, bg);
				return;
			}

			if (_stacked) ApplyStacking(visible);

			// X spans everything, but Y is per-axis: a secondary series must not
			// stretch the primary scale it is deliberately not measured against.
			ComputeRanges(visible, out double xMin, out double xMax, out _, out _);
			var primary = visible.Where(v => !v.Series.UseSecondaryAxis).ToList();
			ComputeRanges(primary.Count > 0 ? primary : visible, out _, out _,
				out double yMin, out double yMax);

			// A gauge is not a plot: no axes, no gridlines, one row per series. It
			// takes its own path rather than being a plot area with everything
			// switched off.
			if (IsGauge)
			{
				PaintBorder(buffer, bounds, clipRect, fg, bg);
				PaintTitle(buffer, bounds, clipRect, fg, bg);
				PaintGauges(buffer, bounds, clipRect, fg, bg, yMin, yMax);
				return;
			}

			// The circular dependency, broken with one correction pass: tick labels
			// steal width from the plot, and the plot's height decides how many Y ticks
			// fit. Measure against the full extent, reserve the gutter, then re-select
			// for what is left. Charting libraries do exactly this rather than iterate.
			var provisional = SelectYTicks(yMin, yMax, Math.Max(0, bounds.Height));
			int gutter = MeasureYGutter(provisional);

			// A right-hand gutter only when a visible series actually uses the
			// secondary axis: an empty one would cost the plot width for nothing.
			var secondarySeries = visible.Where(v => v.Series.UseSecondaryAxis).ToList();
			bool hasSecondary = secondarySeries.Count > 0;

			double s2Min = 0, s2Max = 1;
			int rightGutter = 0;
			if (hasSecondary)
			{
				ComputeRanges(secondarySeries, out _, out _, out s2Min, out s2Max, SecondaryYAxis);

				// Measured from the widest label the axis can produce, not from a
				// provisional tick run: the provisional ticks are chosen against the
				// full height and the final ones against the plot height, so the two
				// disagree and a gutter sized for the first clips the second.
				int widest = Math.Max(
					UnicodeWidth.GetStringWidth(SecondaryYAxis.FormatValue(s2Min)),
					UnicodeWidth.GetStringWidth(SecondaryYAxis.FormatValue(s2Max)));

				rightGutter = SecondaryYAxis.ShowLabels && widest > 0
					? widest + (ControlDefaults.ChartAxisLabelPadding * 2)
					: 0;
			}

			var plot = PlotRect(bounds, gutter, rightGutter);
			_lastPlotRect = plot;
			if (plot.Width <= 0 || plot.Height <= 0) return;

			// A fixed step fills the plot with as many steps as fit, so the X range
			// is decided by the width rather than by the data. Points past the edge
			// simply have not arrived on screen yet.
			if (_sizing == ChartSizing.FixedStep && !XAxis.Max.HasValue)
			{
				int steps = Math.Max(1, plot.Width / Math.Max(1, _stepWidth));
				xMax = xMin + steps;
			}

			if (_resampleToWidth)
			{
				// The plot width is not known until the gutters are measured, so
				// resampling cannot happen with the first range computation. It
				// rewrites X to column indices, so the X range has to be taken
				// again afterwards - otherwise the trace bunches into the left of
				// a plot whose axis still spans the original sample count.
				Resample(visible, plot.Width);
				ComputeRanges(visible, out xMin, out xMax, out _, out _);
			}

			var yTicks = SelectYTicks(yMin, yMax, plot.Height);
			var xTicks = XAxis.SelectTicks(xMin, xMax, plot.Width);

			PaintBorder(buffer, bounds, clipRect, fg, bg);
			PaintTitle(buffer, bounds, clipRect, fg, bg);
			PaintAxisTitles(buffer, bounds, plot, clipRect, fg, bg);
			PaintReferenceLines(buffer, plot, clipRect, bg, xMin, xMax, yMin, yMax);
			PaintPlot(buffer, plot, clipRect, bg,
				primary.Count > 0 ? primary : (hasSecondary ? primary : visible),
				xMin, xMax, yMin, yMax, yTicks, xTicks);

			if (hasSecondary)
			{
				// Drawn in a second pass against its own scale, into the same plot.
				PaintPlot(buffer, plot, clipRect, bg, secondarySeries,
					xMin, xMax, s2Min, s2Max, Array.Empty<ChartTick>(), Array.Empty<ChartTick>(),
					overlay: true);

				var s2Ticks = SecondaryYAxis.SelectTicks(s2Min, s2Max, plot.Height);
				PaintSecondaryLabels(buffer, plot, clipRect, fg, bg, s2Ticks, s2Min, s2Max);
			}
			PaintCentreAxis(buffer, plot, clipRect, bg);
			PaintMarkers(buffer, plot, clipRect, bg, visible, xMin, xMax, yMin, yMax);
			PaintAxisLines(buffer, plot, clipRect, bg);
			PaintYLabels(buffer, bounds, plot, clipRect, fg, bg, yTicks, yMin, yMax);
			PaintXLabels(buffer, plot, clipRect, fg, bg, xTicks, xMin, xMax, HasBarSeries(visible));
			PaintLegend(buffer, bounds, plot, clipRect, fg, bg);
			PaintSelection(buffer, plot, clipRect, bg);
			PaintHover(buffer, plot, clipRect, bg);
		}

		/// <summary>
		/// Replaces each series' values with its running total, for stacking.
		/// </summary>
		/// <remarks>
		/// Done once before anything measures or draws, so the axis range, the
		/// plot and hit-testing all agree about where a series sits. Only visible
		/// series contribute, so toggling one off in the legend restacks the rest.
		/// </remarks>
		private static void ApplyStacking(List<(ChartSeries Series, List<ChartPoint> Points)> visible)
		{
			if (visible.Count < 2) return;

			// Running total per X position, keyed by index within the series.
			var totals = new Dictionary<double, double>();

			foreach (var (_, points) in visible)
			{
				for (int i = 0; i < points.Count; i++)
				{
					double x = points[i].X;
					double below = totals.TryGetValue(x, out double t) ? t : 0;
					double stackedTop = below + points[i].Y;

					points[i] = points[i] with { Y = stackedTop };
					totals[x] = stackedTop;
				}
			}
		}

		/// <summary>
		/// Reduces each series to one point per column, keeping the extremes.
		/// </summary>
		/// <remarks>
		/// Taking every nth sample would drop a one-sample spike, and in a monitor
		/// that spike is usually the event worth seeing. Each column therefore
		/// keeps whichever of its samples is furthest from the series' middle.
		/// </remarks>
		private static void Resample(
			List<(ChartSeries Series, List<ChartPoint> Points)> visible, int columns)
		{
			if (columns <= 0) return;

			foreach (var (_, points) in visible)
			{
				if (points.Count <= columns) continue;

				double mid = 0;
				foreach (var p in points) mid += p.Y;
				mid /= points.Count;

				var reduced = new List<ChartPoint>(columns);

				for (int c = 0; c < columns; c++)
				{
					int from = (int)((long)c * points.Count / columns);
					int to = (int)((long)(c + 1) * points.Count / columns);
					if (to <= from) to = from + 1;
					if (from >= points.Count) break;

					var pick = points[from];
					double best = Math.Abs(pick.Y - mid);

					for (int i = from + 1; i < to && i < points.Count; i++)
					{
						double d = Math.Abs(points[i].Y - mid);
						if (d > best) { best = d; pick = points[i]; }
					}

					// X is rewritten to the column so the trace spans the plot
					// evenly rather than bunching where the samples were dense.
					reduced.Add(pick with { X = c });
				}

				points.Clear();
				points.AddRange(reduced);
			}
		}

		/// <summary>Finds the data bounds, honouring any the axes pin.</summary>
		/// <param name="visible"></param>
		/// <param name="xMin"></param>
		/// <param name="xMax"></param>
		/// <param name="yMin"></param>
		/// <param name="yMax"></param>
		/// <param name="yAxis">
		/// The axis whose pinned bounds apply. Passing the primary axis while
		/// computing the secondary range silently overwrote it with the primary's,
		/// so the axis is a parameter rather than a reach for YAxis.
		/// </param>
		private void ComputeRanges(
			List<(ChartSeries Series, List<ChartPoint> Points)> visible,
			out double xMin, out double xMax, out double yMin, out double yMax,
			ChartAxis? yAxis = null)
		{
			yAxis ??= YAxis;
			xMin = double.MaxValue; xMax = double.MinValue;
			yMin = double.MaxValue; yMax = double.MinValue;

			foreach (var (_, points) in visible)
			{
				foreach (var p in points)
				{
					if (p.X < xMin) xMin = p.X;
					if (p.X > xMax) xMax = p.X;
					if (p.Y < yMin) yMin = p.Y;
					if (p.Y > yMax) yMax = p.Y;
				}
			}

			// No data at all: a unit range keeps every downstream divisor sane and
			// still draws the frame, which is what an empty dashboard panel shows
			// before its first sample lands.
			if (xMin > xMax) { xMin = 0; xMax = 1; }
			if (yMin > yMax) { yMin = 0; yMax = 1; }

			// A bar's LENGTH is its value, so an axis that does not start at zero
			// lies about the data: three bars of 20, 60 and 40 auto-ranged from 20
			// draw the first as nothing and make a third of the scale look like all
			// of it. Line and scatter read positions rather than lengths, so they
			// keep the tighter range that shows their detail.
			if (!yAxis.Min.HasValue && HasBarSeries(visible))
			{
				if (yMin > 0) yMin = 0;
				if (yMax < 0) yMax = 0;
			}

			// Remember the data's own bounds so a zoom reset has something to
			// return to, then let any zoom narrow what is actually drawn.
			_lastDataXMin = XAxis.Min ?? xMin;
			_lastDataXMax = XAxis.Max ?? xMax;

			xMin = _viewXMin ?? XAxis.Min ?? xMin;
			xMax = _viewXMax ?? XAxis.Max ?? xMax;
			yMin = yAxis.Min ?? yMin;
			yMax = yAxis.Max ?? yMax;
		}

		/// <summary>
		/// Marks the selected point.
		/// </summary>
		/// <remarks>
		/// A selection nobody can see is not a selection: the marker is how a reader
		/// knows which point the arrow keys are sitting on. Drawn before the hover
		/// so a crosshair passing over it stays on top.
		/// </remarks>
		private void PaintSelection(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg)
		{
			if (!_enableSelection || _selectedSeries == null || _selectedIndex < 0) return;

			foreach (var (series, index, px, py) in _pointPositions)
			{
				if (series != _selectedSeries || index != _selectedIndex) continue;
				if (px < clipRect.X || px >= clipRect.Right) return;
				if (py < clipRect.Y || py >= clipRect.Bottom) return;

				buffer.SetNarrowCell(px, py, ControlDefaults.ChartSelectionMarker,
					ControlDefaults.ChartSelectionColor, bg);
				return;
			}
		}

		/// <summary>
		/// Draws the crosshair through the hovered point and a readout beside it.
		/// </summary>
		/// <remarks>
		/// Last of all, over everything, because it is a transient answer to "what
		/// is this?" rather than part of the chart. The readout is placed on the
		/// opposite side of the point from the plot edge it is nearest, so it never
		/// runs off.
		/// </remarks>
		private void PaintHover(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg)
		{
			// A keyboard user needs the same answer to "what is this point" as a
			// mouse user, so a selection stands in for a hover when there is none.
			var point = _hoveredPoint ?? (_enableSelection ? _selectedPoint : null);
			var series = _hoveredSeries ?? (_enableSelection ? _selectedSeries : null);

			if (!_enableHover || point == null || series == null) return;

			// The cell the hovered point was drawn at.
			int hx = -1, hy = -1;
			foreach (var (candidate, index, px, py) in _pointPositions)
			{
				if (candidate != series) continue;
				if ((uint)index >= (uint)candidate.Points.Count) continue;
				if (!Nullable.Equals((ChartPoint?)candidate.Points[index], point)) continue;
				hx = px; hy = py;
				break;
			}
			if (hx < 0) return;

			var box = BoxChars.Single;
			Color line = ControlDefaults.ChartCrosshairColor;

			for (int y = plot.Y; y < plot.Y + plot.Height; y++)
			{
				if (y == hy) continue;   // the point itself stays visible
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;
				if (hx < clipRect.X || hx >= clipRect.Right) continue;
				buffer.SetNarrowCell(hx, y, box.Vertical, line, bg);
			}

			for (int x = plot.X; x < plot.X + plot.Width; x++)
			{
				if (x == hx) continue;
				if (x < clipRect.X || x >= clipRect.Right) continue;
				if (hy < clipRect.Y || hy >= clipRect.Bottom) continue;
				buffer.SetNarrowCell(x, hy, box.Horizontal, line, bg);
			}

			PaintReadout(buffer, plot, clipRect, bg, hx, hy, series, point.Value);
		}

		/// <summary>Draws the hover readout: the series name and the point's values.</summary>
		private void PaintReadout(CharacterBuffer buffer,
			(int X, int Y, int Width, int Height) plot, LayoutRect clipRect, Color bg,
			int hx, int hy, ChartSeries series, ChartPoint point)
		{
			string text = $" {series.Name} {XAxis.FormatValue(point.X)}, {YAxis.FormatValue(point.Y)} ";

			int width = UnicodeWidth.GetStringWidth(text);

			// Put the readout on whichever side of the crosshair has room, so it
			// never runs off the plot.
			int x = hx + 2;
			if (x + width > plot.X + plot.Width) x = hx - 1 - width;
			if (x < plot.X) x = plot.X;

			int y = hy > plot.Y ? hy - 1 : hy + 1;
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			var cells = MarkupParser.Parse(text, ControlDefaults.ChartReadoutColor, bg);
			buffer.WriteCellsClipped(x, y, cells, clipRect);
		}

		/// <summary>True when any visible series is drawn as bars.</summary>
		private bool HasBarSeries(List<(ChartSeries Series, List<ChartPoint> Points)> visible)
		{
			foreach (var (series, _) in visible)
			{
				if ((series.PlotType ?? _plotType) == ChartPlotType.Bar) return true;
			}
			return false;
		}


		/// <summary>Maps a value to a row within one band of a split plot.</summary>
		/// <remarks>Mirrors ChartControl.Plots PixelYInHalf, in cell rows rather than pixels.</remarks>
		private static int RowInHalf(double value, double min, double max, int height, bool upper)
		{
			if (height <= 1) return 0;

			int centre = height / 2;
			double range = max - min;
			double fraction = range > 0 ? Math.Clamp((value - min) / range, 0, 1) : 0.5;

			if (upper)
			{
				int span = Math.Max(1, centre);
				return Math.Clamp(centre - (int)(fraction * span), 0, height - 1);
			}

			int lowerSpan = Math.Max(1, height - 1 - centre);
			return Math.Clamp(centre + (int)(fraction * lowerSpan), 0, height - 1);
		}

		/// <summary>Maps a value to a row inside the plot, top being the maximum.</summary>
		private static int ValueToRow(double value, double min, double max, int height)
		{
			if (height <= 1) return 0;
			double range = max - min;
			int row = range > 0
				? height - 1 - (int)((value - min) / range * (height - 1))
				: height / 2;
			return Math.Clamp(row, 0, height - 1);
		}

		/// <summary>The column a bar's label centres on, matching the bar's own inset.</summary>
		private static int BarLabelColumn(double value, double min, double max, int width, int slots)
		{
			double slot = (double)width / Math.Max(1, slots);
			double usable = Math.Max(1, width - slot);
			double range = max - min;
			double fraction = range > 0 ? Math.Clamp((value - min) / range, 0, 1) : 0.5;

			return (int)Math.Round(slot / 2 + fraction * usable);
		}

		/// <summary>Maps a value to a column inside the plot.</summary>
		private static int ValueToColumn(double value, double min, double max, int width)
		{
			if (width <= 1) return 0;
			double range = max - min;
			int col = range > 0
				? (int)((value - min) / range * (width - 1))
				: width / 2;
			return Math.Clamp(col, 0, width - 1);
		}
	}
}

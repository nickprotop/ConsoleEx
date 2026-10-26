// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Controls
{
	/// <summary>What an axis knows when it asks a caller to choose its ticks.</summary>
	/// <param name="Min">The low end of the range being drawn.</param>
	/// <param name="Max">The high end.</param>
	/// <param name="Extent">Cells available along the axis.</param>
	public readonly record struct ChartAxisContext(double Min, double Max, int Extent);

	/// <summary>A tick a caller asks the chart to draw.</summary>
	/// <param name="Value">Where it sits, in data coordinates.</param>
	/// <param name="Label">The text, parsed as markup.</param>
	/// <param name="Color">An override colour, or null for the axis default.</param>
	public readonly record struct ChartAxisTick(double Value, string Label, Color? Color = null);

	/// <summary>One mark on an axis: where it sits and what it says.</summary>
	/// <param name="Value">The position in data coordinates.</param>
	/// <param name="Label">The text drawn beside the mark.</param>
	/// <param name="Color">An override colour, or null for the axis default.</param>
	public readonly record struct ChartTick(double Value, string Label, Color? Color = null);

	/// <summary>
	/// One axis of a chart: its range, how its values are read, and where its ticks go.
	/// </summary>
	/// <remarks>
	/// The same type serves X and Y, and numeric, time and category data. Time is
	/// carried as <see cref="DateTime.Ticks"/> and categories as indices, so one
	/// tick engine serves all three and only the formatting differs.
	/// </remarks>
	public sealed class ChartAxis
	{
		private string[] _categories = Array.Empty<string>();

		/// <summary>The axis title, drawn alongside the labels.</summary>
		public string? Title { get; set; }

		/// <summary>Lower bound; null takes it from the data.</summary>
		public double? Min { get; set; }

		/// <summary>Upper bound; null takes it from the data.</summary>
		public double? Max { get; set; }

		/// <summary>What this axis's numbers mean.</summary>
		public ChartAxisKind Kind { get; set; } = ChartAxisKind.Numeric;

		/// <summary>A .NET format string for labels, such as "0.##" or "HH:mm".</summary>
		public string? LabelFormat { get; set; }

		/// <summary>Replaces formatting entirely when set.</summary>
		public Func<double, string>? LabelFormatter { get; set; }

		/// <summary>
		/// Chooses the ticks itself, replacing the nice-number engine.
		/// </summary>
		/// <remarks>
		/// For ticks whose positions follow something other than the value scale -
		/// a sample clock labelled "-1m" to "now", say, where no amount of
		/// formatting the numbers produces the right positions. Returning nothing
		/// leaves the axis bare.
		/// </remarks>
		public Func<ChartAxisContext, IEnumerable<ChartAxisTick>>? TickProvider { get; set; }

		/// <summary>Whether tick marks are drawn.</summary>
		public bool ShowTicks { get; set; } = true;

		/// <summary>Whether tick labels are drawn.</summary>
		public bool ShowLabels { get; set; } = true;

		/// <summary>Whether gridlines are drawn across the plot at each tick.</summary>
		public bool ShowGridlines { get; set; }

		/// <summary>An upper bound on tick count; null fits as many as the space allows.</summary>
		public int? MaxTicks { get; set; }

		/// <summary>The categories, empty unless this is a category axis.</summary>
		public IReadOnlyList<string> Categories => _categories;

		/// <summary>Names the categories a <see cref="ChartAxisKind.Category"/> axis indexes.</summary>
		/// <param name="categories">The category names, in axis order.</param>
		public void SetCategories(IEnumerable<string> categories)
		{
			ArgumentNullException.ThrowIfNull(categories);
			_categories = categories.ToArray();
		}

		/// <summary>Renders one axis value as its label.</summary>
		/// <param name="value">The value in data coordinates.</param>
		/// <returns>The label text, which may be empty.</returns>
		public string FormatValue(double value)
		{
			if (LabelFormatter != null) return LabelFormatter(value);

			switch (Kind)
			{
				case ChartAxisKind.Category:
					int i = (int)Math.Round(value);
					// A tick can land outside the list when the range is wider than the
					// categories; an empty label beats throwing on a repaint.
					return (uint)i < (uint)_categories.Length ? _categories[i] : string.Empty;

				case ChartAxisKind.Time:
					// A zoomed-out axis can compute a tick past the end of time, and
					// DateTime throws on those. Clamping keeps the paint alive over a
					// value no reader could use anyway.
					// Clamping in double space is not enough: DateTime.MaxValue.Ticks has
					// more significant digits than a double carries, so the round-trip
					// lands just above the limit and the constructor throws. Compare as
					// double, then take the exact long.
					long clamped = value <= DateTime.MinValue.Ticks ? DateTime.MinValue.Ticks
						: value >= DateTime.MaxValue.Ticks ? DateTime.MaxValue.Ticks
						: (long)value;
					return new DateTime(clamped).ToString(
						LabelFormat ?? ControlDefaults.ChartDefaultTimeFormat,
						CultureInfo.CurrentCulture);

				default:
					return value.ToString(
						LabelFormat ?? ControlDefaults.ChartDefaultNumberFormat,
						CultureInfo.CurrentCulture);
			}
		}

		/// <summary>
		/// Chooses the ticks for a range drawn across a given extent.
		/// </summary>
		/// <param name="min">The low end of the data range.</param>
		/// <param name="max">The high end of the data range.</param>
		/// <param name="availableExtent">Cells available along the axis.</param>
		/// <returns>Ticks in ascending order; empty when there is no room.</returns>
		/// <remarks>
		/// Steps are 1, 2 or 5 times a power of ten, because those are the numbers a
		/// reader can divide at a glance. The densest such step whose ticks still fit
		/// the extent wins.
		/// </remarks>
		public IReadOnlyList<ChartTick> SelectTicks(double min, double max, int availableExtent)
		{
			var ticks = new List<ChartTick>();
			if (availableExtent <= 0) return ticks;

			if (TickProvider != null)
			{
				// App code running inside a paint: a bad provider costs its own
				// ticks, not the whole chart.
				try
				{
					foreach (var t in TickProvider(new ChartAxisContext(min, max, availableExtent)))
					{
						ticks.Add(new ChartTick(t.Value, t.Label, t.Color));
					}
				}
				catch
				{
					ticks.Clear();
				}

				return ticks;
			}
			if (double.IsNaN(min) || double.IsNaN(max)) return ticks;

			// A flat range has no span to divide. One tick at the value is the honest
			// answer, and it keeps every downstream divisor non-zero.
			if (max <= min)
			{
				ticks.Add(new ChartTick(min, FormatValue(min)));
				return ticks;
			}

			int room = Math.Max(1, availableExtent / ControlDefaults.ChartMinCellsPerTick);
			int wanted = MaxTicks.HasValue ? Math.Min(MaxTicks.Value, room) : room;
			if (wanted <= 0) return ticks;

			// Room for only one nice-number tick puts it at the range minimum, so a
			// short plot reads "0" and says nothing about the scale. The two ENDS
			// are what an axis is for, so a cramped one shows those instead of one
			// arbitrary interior value.
			if (wanted < 2 && availableExtent >= ControlDefaults.ChartMinCellsPerEndTick)
			{
				ticks.Add(new ChartTick(min, FormatValue(min)));
				ticks.Add(new ChartTick(max, FormatValue(max)));
				return ticks;
			}

			// A category axis steps whole categories; fractional categories mean nothing.
			double rawStep = (max - min) / wanted;
			double step = Kind == ChartAxisKind.Category
				? Math.Max(1, Math.Round(rawStep))
				: NiceStep(rawStep);
			if (step <= 0 || double.IsInfinity(step)) return ticks;

			double first = Math.Ceiling(min / step) * step;
			double limit = max + step * ControlDefaults.ChartTickEpsilon;

			for (double v = first; v <= limit; v += step)
			{
				if (v < min) continue;
				double clamped = Math.Min(v, max);
				ticks.Add(new ChartTick(clamped, FormatValue(clamped)));
				if (MaxTicks.HasValue && ticks.Count >= MaxTicks.Value) break;
			}

			return ticks;
		}

		/// <summary>Rounds a step up to the next 1, 2 or 5 times a power of ten.</summary>
		private static double NiceStep(double rough)
		{
			if (rough <= 0) return 0;

			double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
			double normalised = rough / magnitude;

			double nice = normalised <= 1 ? 1
				: normalised <= 2 ? 2
				: normalised <= 5 ? 5
				: 10;

			return nice * magnitude;
		}
	}
}

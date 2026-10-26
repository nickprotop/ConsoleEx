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
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Builders
{
	/// <summary>
	/// Builds a <see cref="ChartControl"/> by chaining.
	/// </summary>
	/// <example>
	/// <code>
	/// var chart = Controls.Chart()
	///     .WithTitle("Throughput")
	///     .WithPlotType(ChartPlotType.Area)
	///     .WithYRange(0, 100)
	///     .WithGridlines()
	///     .AddSeries("down", Color.Green, downSamples)
	///     .AddSeries("up", Color.Cyan1, upSamples)
	///     .WithSplitView()
	///     .Interactive()
	///     .Build();
	/// </code>
	/// </example>
	public class ChartBuilder : IControlBuilder<ChartControl>
	{
		private readonly ChartControl _control = new();

		private readonly List<(string Name, Color Color, IEnumerable<double>? Values,
			ColorGradient? Gradient, ChartGradientAxis Axis, ChartPlotType? PlotType)> _pending = new();

		/// <summary>Sets the plot type series use when they do not name their own.</summary>
		/// <param name="type">The plot type.</param>
		public ChartBuilder WithPlotType(ChartPlotType type)
		{
			_control.PlotType = type;
			return this;
		}

		/// <summary>Sets how the plot area is rasterised.</summary>
		/// <param name="mode">The render mode.</param>
		public ChartBuilder WithRenderMode(ChartRenderMode mode)
		{
			_control.RenderMode = mode;
			return this;
		}

		/// <summary>Sets the heading drawn above the plot.</summary>
		/// <param name="title">The title text.</param>
		public ChartBuilder WithTitle(string title)
		{
			_control.Title = title;
			return this;
		}

		/// <summary>Shows the legend.</summary>
		/// <param name="position">Where it sits; bottom by default.</param>
		public ChartBuilder WithLegend(ChartLegendPosition position = ChartLegendPosition.Bottom)
		{
			_control.ShowLegend = true;
			_control.LegendPosition = position;
			return this;
		}

		/// <summary>Pins the vertical axis to a range.</summary>
		/// <param name="min">The low end.</param>
		/// <param name="max">The high end.</param>
		public ChartBuilder WithYRange(double min, double max)
		{
			_control.YAxis.Min = min;
			_control.YAxis.Max = max;
			return this;
		}

		/// <summary>Pins the horizontal axis to a range.</summary>
		/// <param name="min">The low end.</param>
		/// <param name="max">The high end.</param>
		public ChartBuilder WithXRange(double min, double max)
		{
			_control.XAxis.Min = min;
			_control.XAxis.Max = max;
			return this;
		}

		/// <summary>Draws gridlines behind the plot.</summary>
		/// <param name="horizontal">Lines across, at each Y tick.</param>
		/// <param name="vertical">Lines down, at each X tick.</param>
		public ChartBuilder WithGridlines(bool horizontal = true, bool vertical = false)
		{
			_control.YAxis.ShowGridlines = horizontal;
			_control.XAxis.ShowGridlines = vertical;
			return this;
		}

		/// <summary>Draws rules along the left and bottom edges of the plot.</summary>
		public ChartBuilder WithAxisLines()
		{
			_control.ShowAxisLines = true;
			return this;
		}

		/// <summary>Splits the plot about its centre, for a pair of opposed quantities.</summary>
		public ChartBuilder WithSplitView()
		{
			_control.SplitView = true;
			return this;
		}

		/// <summary>Fixes the plot height instead of filling the container.</summary>
		/// <param name="rows">Plot height in rows.</param>
		public ChartBuilder WithPlotHeight(int rows)
		{
			_control.PlotHeight = rows;
			return this;
		}

		/// <summary>Labels the horizontal axis with categories.</summary>
		/// <param name="categories">The category names, in axis order.</param>
		public ChartBuilder WithCategories(params string[] categories)
		{
			_control.XAxis.Kind = ChartAxisKind.Category;
			_control.XAxis.SetCategories(categories);
			return this;
		}

		/// <summary>Draws the chart as inline gauges, one row per series.</summary>
		/// <param name="showLabel">Whether each row names its series.</param>
		/// <param name="showValue">Whether each row prints its value.</param>
		public ChartBuilder AsGauge(bool showLabel = true, bool showValue = true)
		{
			_control.PlotType = ChartPlotType.Gauge;
			_control.ShowGaugeLabel = showLabel;
			_control.ShowGaugeValue = showValue;
			return this;
		}

		/// <summary>Runs bars across instead of up.</summary>
		public ChartBuilder Horizontal()
		{
			_control.Orientation = ChartOrientation.Horizontal;
			return this;
		}

		/// <summary>
		/// Gives each point a fixed width so the plot grows as data arrives.
		/// </summary>
		/// <param name="stepWidth">Cells per point.</param>
		public ChartBuilder WithFixedStep(int stepWidth = ControlDefaults.ChartDefaultStepWidth)
		{
			_control.Sizing = ChartSizing.FixedStep;
			_control.StepWidth = stepWidth;
			return this;
		}

		/// <summary>Stacks each series on the sum of those before it.</summary>
		public ChartBuilder Stacked()
		{
			_control.Stacked = true;
			return this;
		}

		/// <summary>Adds a subtitle under the title.</summary>
		/// <param name="subtitle">The text.</param>
		public ChartBuilder WithSubtitle(string subtitle)
		{
			_control.Subtitle = subtitle;
			return this;
		}

		/// <summary>Centres the title and subtitle.</summary>
		/// <param name="alignment">Where they sit.</param>
		public ChartBuilder WithTitleAlignment(ChartTitleAlignment alignment)
		{
			_control.TitleAlignment = alignment;
			return this;
		}

		/// <summary>Draws a border around the chart.</summary>
		/// <param name="style">The border style.</param>
		public ChartBuilder WithBorder(BorderStyle style = BorderStyle.Single)
		{
			_control.BorderStyle = style;
			return this;
		}

		/// <summary>Names the axes.</summary>
		/// <param name="xTitle">The horizontal axis title, if any.</param>
		/// <param name="yTitle">The vertical axis title, if any.</param>
		public ChartBuilder WithAxisTitles(string? xTitle = null, string? yTitle = null)
		{
			if (xTitle != null) _control.XAxis.Title = xTitle;
			if (yTitle != null) _control.YAxis.Title = yTitle;
			return this;
		}

		/// <summary>Draws a line across the plot at a value.</summary>
		/// <param name="value">Where the line sits.</param>
		/// <param name="color">The line colour.</param>
		/// <param name="label">Text at the line's end.</param>
		public ChartBuilder WithReferenceLine(double value, Color color, string? label = null)
		{
			_control.AddReferenceLine(value, color, label);
			return this;
		}

		/// <summary>Labels each series' highest and lowest points.</summary>
		public ChartBuilder WithHighLowLabels()
		{
			_control.ShowHighLowLabels = true;
			return this;
		}

		/// <summary>Shows a message when the chart has nothing to plot.</summary>
		/// <param name="text">The message.</param>
		public ChartBuilder WithEmptyText(string text)
		{
			_control.EmptyText = text;
			return this;
		}

		/// <summary>Turns on hover, selection and zoom together.</summary>
		/// <remarks>
		/// Each is also settable on its own; this is the common case of wanting a
		/// chart a reader can actually explore.
		/// </remarks>
		public ChartBuilder Interactive(bool hover = true, bool selection = true, bool zoom = true)
		{
			_control.EnableHover = hover;
			_control.EnableSelection = selection;
			_control.EnableZoom = zoom;
			return this;
		}

		/// <summary>Adds a series.</summary>
		/// <param name="name">The series name, shown in the legend.</param>
		/// <param name="color">The series colour.</param>
		/// <param name="values">Evenly spaced values, or null to fill in later.</param>
		/// <param name="gradient">An optional gradient, replacing the flat colour.</param>
		/// <param name="gradientAxis">Which way the gradient runs.</param>
		/// <param name="plotType">A plot type for this series alone.</param>
		public ChartBuilder AddSeries(string name, Color color,
			IEnumerable<double>? values = null,
			ColorGradient? gradient = null,
			ChartGradientAxis gradientAxis = ChartGradientAxis.X,
			ChartPlotType? plotType = null)
		{
			_pending.Add((name, color, values, gradient, gradientAxis, plotType));
			return this;
		}

		/// <summary>Builds the chart.</summary>
		/// <returns>The configured control.</returns>
		public ChartControl Build()
		{
			foreach (var (name, color, values, gradient, axis, plotType) in _pending)
			{
				var series = _control.AddSeries(name, color);
				series.Gradient = gradient;
				series.GradientAxis = axis;
				series.PlotType = plotType;
				if (values != null) series.SetValues(values);
			}

			return _control;
		}
	}
}

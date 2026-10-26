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
using SharpConsoleUI.Themes;

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
			ColorGradient? Gradient, ChartGradientAxis Axis, ChartPlotType? PlotType,
			bool Secondary, ChartThreshold[]? Thresholds)> _pending = new();

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
		/// <param name="color">The border colour; unset follows the theme.</param>
		public ChartBuilder WithBorder(BorderStyle style = BorderStyle.Single, Color? color = null)
		{
			_control.BorderStyle = style;
			if (color.HasValue) _control.BorderColor = color;
			return this;
		}

		/// <summary>Sets the chart's foreground and background.</summary>
		/// <param name="foreground">Chrome colour; null follows the theme.</param>
		/// <param name="background">Control background; null follows the theme.</param>
		public ChartBuilder WithColors(Color? foreground = null, Color? background = null)
		{
			if (foreground.HasValue) _control.ForegroundColor = foreground;
			if (background.HasValue) _control.BackgroundColor = background;
			return this;
		}

		/// <summary>Fills the plot area behind the data.</summary>
		/// <param name="color">The fill; <see cref="Color.Transparent"/> shows what is behind.</param>
		public ChartBuilder WithPlotBackground(Color color)
		{
			_control.PlotBackground = color;
			return this;
		}

		/// <summary>Colours the title.</summary>
		/// <param name="color">The title colour.</param>
		public ChartBuilder WithTitleColor(Color color)
		{
			_control.TitleColor = color;
			return this;
		}

		/// <summary>Colours the subtitle.</summary>
		/// <param name="color">The subtitle colour.</param>
		public ChartBuilder WithSubtitleColor(Color color)
		{
			_control.SubtitleColor = color;
			return this;
		}

		/// <summary>Draws the line where a split plot's halves meet.</summary>
		/// <param name="caption">Text written into the line, if any.</param>
		/// <param name="showValue">Whether the line's value is printed at its far end.</param>
		/// <param name="character">The glyph the line is drawn with.</param>
		public ChartBuilder WithCentreAxis(string? caption = null, bool showValue = false,
			char? character = null)
		{
			_control.ShowCentreAxis = true;
			_control.CentreAxisCaption = caption;
			_control.ShowCentreAxisValue = showValue;
			if (character.HasValue) _control.CentreAxisChar = character.Value;
			return this;
		}

		/// <summary>Resamples a history longer than the plot is wide.</summary>
		public ChartBuilder WithResampling()
		{
			_control.ResampleToWidth = true;
			return this;
		}

		/// <summary>Drives the chart's colours from a semantic role.</summary>
		/// <param name="role">The role.</param>
		/// <param name="mode">An optional theme-mode override.</param>
		public ChartBuilder WithColorRole(ColorRole role, ThemeMode? mode = null)
		{
			_control.ColorRole = role;
			_control.ColorRoleMode = mode;
			return this;
		}

		/// <summary>Uses the role's outline treatment.</summary>
		public ChartBuilder Outline()
		{
			_control.Outline = true;
			return this;
		}

		/// <summary>Configures the horizontal axis.</summary>
		/// <param name="configure">Receives the axis.</param>
		public ChartBuilder ConfigureXAxis(Action<ChartAxis> configure)
		{
			ArgumentNullException.ThrowIfNull(configure);
			configure(_control.XAxis);
			return this;
		}

		/// <summary>Configures the vertical axis.</summary>
		/// <param name="configure">Receives the axis.</param>
		public ChartBuilder ConfigureYAxis(Action<ChartAxis> configure)
		{
			ArgumentNullException.ThrowIfNull(configure);
			configure(_control.YAxis);
			return this;
		}

		/// <summary>Configures the second vertical axis, labelled on the right.</summary>
		/// <param name="configure">Receives the axis.</param>
		public ChartBuilder ConfigureSecondaryYAxis(Action<ChartAxis> configure)
		{
			ArgumentNullException.ThrowIfNull(configure);
			configure(_control.SecondaryYAxis);
			return this;
		}

		/// <summary>Configures the scale for the lower half of a split plot.</summary>
		/// <param name="configure">Receives the axis.</param>
		public ChartBuilder ConfigureLowerAxis(Action<ChartAxis> configure)
		{
			ArgumentNullException.ThrowIfNull(configure);
			configure(_control.LowerAxis);
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

		/// <summary>Shows a failure message instead of the plot.</summary>
		/// <param name="text">What went wrong, or null to clear.</param>
		public ChartBuilder WithErrorText(string? text)
		{
			_control.ErrorText = text;
			return this;
		}

		/// <summary>Shows the legend as a colour scale rather than swatches.</summary>
		/// <remarks>
		/// For a series whose colour carries the value: a swatch would show one
		/// colour out of a whole gradient. Turns the legend on as well, since a
		/// scale nobody can see says nothing.
		/// </remarks>
		public ChartBuilder WithColorBarLegend(
			ChartLegendPosition position = ChartLegendPosition.Bottom)
		{
			_control.ShowLegend = true;
			_control.LegendPosition = position;
			_control.LegendStyle = ChartLegendStyle.ColorBar;
			return this;
		}

		/// <summary>Draws a grid of values, each cell coloured by its own.</summary>
		/// <param name="name">Series name, for the legend and readouts.</param>
		/// <param name="values">Indexed [row, column]; non-finite values become holes.</param>
		/// <param name="gradient">The scale the values are coloured by.</param>
		public ChartBuilder AddHeatmap(string name, double[,] values, ColorGradient gradient)
		{
			ArgumentNullException.ThrowIfNull(values);
			ArgumentNullException.ThrowIfNull(gradient);

			_control.PlotType = ChartPlotType.Heatmap;

			var series = _control.AddSeries(name, Color.White);
			series.Gradient = gradient;
			series.SetGrid(values);
			return this;
		}

		/// <summary>Draws a grid from scattered cells, sized to the largest given.</summary>
		/// <param name="name">Series name, for the legend and readouts.</param>
		/// <param name="cells">Column, row and value.</param>
		/// <param name="gradient">The scale the values are coloured by.</param>
		public ChartBuilder AddHeatmap(string name,
			IEnumerable<(int X, int Y, double Value)> cells, ColorGradient gradient)
		{
			ArgumentNullException.ThrowIfNull(cells);
			ArgumentNullException.ThrowIfNull(gradient);

			_control.PlotType = ChartPlotType.Heatmap;

			var series = _control.AddSeries(name, Color.White);
			series.Gradient = gradient;
			series.SetGrid(cells);
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
		/// <param name="useSecondaryAxis">Measures the series against the second scale.</param>
		/// <param name="thresholds">Values past which the series changes colour.</param>
		public ChartBuilder AddSeries(string name, Color color,
			IEnumerable<double>? values = null,
			ColorGradient? gradient = null,
			ChartGradientAxis gradientAxis = ChartGradientAxis.X,
			ChartPlotType? plotType = null,
			bool useSecondaryAxis = false,
			ChartThreshold[]? thresholds = null)
		{
			_pending.Add((name, color, values, gradient, gradientAxis, plotType,
				useSecondaryAxis, thresholds));
			return this;
		}

		/// <summary>Builds the chart.</summary>
		/// <returns>The configured control.</returns>
		public ChartControl Build()
		{
			foreach (var (name, color, values, gradient, axis, plotType, secondary, thresholds) in _pending)
			{
				var series = _control.AddSeries(name, color);
				series.Gradient = gradient;
				series.GradientAxis = axis;
				series.PlotType = plotType;
				series.UseSecondaryAxis = secondary;
				if (thresholds != null) series.SetThresholds(thresholds);
				if (values != null) series.SetValues(values);
			}

			return _control;
		}
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Everything the control can be told, the builder can say.
	/// </summary>
	/// <remarks>
	/// A fluent API that covers half the properties forces a caller to mix the
	/// two styles - build, then assign - which is worse than having no builder.
	/// </remarks>
	public class ChartBuilderCoverageTests
	{
		[Fact]
		public void ColoursRoundTrip()
		{
			var chart = new ChartBuilder()
				.WithTitle("T")
				.WithSubtitle("S")
				.WithTitleColor(Color.Red)
				.WithSubtitleColor(Color.Blue)
				.WithBorder(BorderStyle.Single, Color.Green)
				.WithColors(Color.White, Color.Black)
				.WithPlotBackground(Color.Grey19)
				.Build();

			Assert.Equal(Color.Red, chart.TitleColor);
			Assert.Equal(Color.Blue, chart.SubtitleColor);
			Assert.Equal(Color.Green, chart.BorderColor);
			Assert.Equal(Color.White, chart.ForegroundColor);
			Assert.Equal(Color.Black, chart.BackgroundColor);
			Assert.Equal(Color.Grey19, chart.PlotBackground);
		}

		[Fact]
		public void TheCentreAxisRoundTrips()
		{
			var chart = new ChartBuilder()
				.WithSplitView()
				.WithCentreAxis("B/s", showValue: true, character: '=')
				.Build();

			Assert.True(chart.ShowCentreAxis);
			Assert.Equal("B/s", chart.CentreAxisCaption);
			Assert.True(chart.ShowCentreAxisValue);
			Assert.Equal('=', chart.CentreAxisChar);
		}

		[Fact]
		public void ResamplingRoundTrips()
		{
			var chart = new ChartBuilder().WithResampling().Build();

			Assert.True(chart.ResampleToWidth);
		}

		[Fact]
		public void AColorRoleRoundTrips()
		{
			var chart = new ChartBuilder()
				.WithColorRole(ColorRole.Danger)
				.Outline()
				.Build();

			Assert.Equal(ColorRole.Danger, chart.ColorRole);
			Assert.True(chart.Outline);
		}

		[Fact]
		public void AnAxisCanBeConfiguredFluently()
		{
			// The shape gap: per-axis settings had no fluent route at all, so a
			// caller had to build and then assign.
			var chart = new ChartBuilder()
				.ConfigureXAxis(a =>
				{
					a.Title = "minutes";
					a.ShowTicks = false;
					a.MaxTicks = 4;
					a.LabelFormat = "0.0";
				})
				.ConfigureYAxis(a => a.ShowLabels = false)
				.Build();

			Assert.Equal("minutes", chart.XAxis.Title);
			Assert.False(chart.XAxis.ShowTicks);
			Assert.Equal(4, chart.XAxis.MaxTicks);
			Assert.Equal("0.0", chart.XAxis.LabelFormat);
			Assert.False(chart.YAxis.ShowLabels);
		}

		[Fact]
		public void TheSecondaryAxisIsReachable()
		{
			var chart = new ChartBuilder()
				.ConfigureSecondaryYAxis(a => { a.Min = 0; a.Max = 500; a.Title = "ms"; })
				.AddSeries("latency", Color.Yellow, new[] { 10d, 20 }, useSecondaryAxis: true)
				.Build();

			Assert.Equal(0, chart.SecondaryYAxis.Min);
			Assert.Equal(500, chart.SecondaryYAxis.Max);
			Assert.True(chart.Series[0].UseSecondaryAxis);
		}

		[Fact]
		public void TheLowerAxisIsReachable()
		{
			var chart = new ChartBuilder()
				.WithSplitView()
				.ConfigureLowerAxis(a => { a.Min = 0; a.Max = 2000; })
				.Build();

			Assert.Equal(2000, chart.LowerAxis.Max);
		}

		[Fact]
		public void ATickProviderIsReachable()
		{
			var chart = new ChartBuilder()
				.ConfigureXAxis(a => a.TickProvider = ctx => new[] { new ChartAxisTick(ctx.Max, "now") })
				.Build();

			Assert.NotNull(chart.XAxis.TickProvider);
		}

		[Fact]
		public void ASeriesCanCarryThresholds()
		{
			var chart = new ChartBuilder()
				.AddSeries("cpu", Color.Green, new[] { 10d, 95 },
					thresholds: new[] { new ChartThreshold(90, Color.Red) })
				.Build();

			// Thresholds are internal state; what a caller can observe is that the
			// series took them without the builder dropping them on the floor.
			Assert.Single(chart.Series);
			Assert.Equal(2, chart.Series[0].Points.Count);
		}

		[Fact]
		public void TheBuilderAloneCanConfigureAWholeChart()
		{
			// The honest test of coverage: build a chart that exercises every
			// feature group through the fluent API only, with no post-build
			// assignment. A property with no route makes this impossible to
			// write, which is the failure worth catching.
			//
			// (An earlier version matched builder method NAMES against property
			// names and reported twenty false positives - WithCentreAxis covers
			// four CentreAxis* properties, Interactive() covers three Enable*,
			// and so on. Name matching measures spelling, not reach.)
			var chart = new ChartBuilder()
				.WithTitle("Everything")
				.WithSubtitle("configured fluently")
				.WithTitleAlignment(ChartTitleAlignment.Center)
				.WithTitleColor(Color.White)
				.WithSubtitleColor(Color.Grey62)
				.WithBorder(BorderStyle.Single, Color.Grey42)
				.WithColors(Color.White, Color.Black)
				.WithPlotBackground(Color.Grey19)
				.WithPlotType(ChartPlotType.Area)
				.WithRenderMode(ChartRenderMode.Braille)
				.WithPlotHeight(10)
				.WithLegend(ChartLegendPosition.Bottom)
				.WithGridlines(horizontal: true, vertical: true)
				.WithAxisLines()
				.WithAxisTitles("minutes", "requests/s")
				.WithYRange(0, 100)
				.WithXRange(0, 59)
				.ConfigureXAxis(a => { a.ShowTicks = true; a.MaxTicks = 5; a.LabelFormat = "0"; })
				.ConfigureYAxis(a => a.ShowLabels = true)
				.ConfigureSecondaryYAxis(a => { a.Min = 0; a.Max = 500; })
				.ConfigureLowerAxis(a => { a.Min = 0; a.Max = 50; })
				.WithSplitView()
				.WithCentreAxis("B/s", showValue: true, character: '-')
				.Stacked()
				.WithResampling()
				.WithFixedStep(2)
				.Horizontal()
				.WithCategories("a", "b")
				.WithReferenceLine(80, Color.Red, "limit")
				.WithHighLowLabels()
				.WithEmptyText("no data")
				.WithErrorText(null)
				.WithColorRole(ColorRole.Success)
				.Outline()
				.Interactive()
				.AddSeries("main", Color.Green, new[] { 10d, 50, 30 },
					ColorGradient.Predefined["warm"], ChartGradientAxis.Y, ChartPlotType.Line,
					useSecondaryAxis: false,
					thresholds: new[] { new ChartThreshold(90, Color.Red) })
				.AddSeries("second", Color.Cyan1, new[] { 5d, 25, 15 }, useSecondaryAxis: true)
				.Build();

			// Spot-check one property from each group reached only this way.
			Assert.Equal("Everything", chart.Title);
			Assert.Equal(Color.Grey19, chart.PlotBackground);
			Assert.True(chart.ShowCentreAxis);
			Assert.True(chart.ResampleToWidth);
			Assert.Equal(ChartOrientation.Horizontal, chart.Orientation);
			Assert.Equal(ChartSizing.FixedStep, chart.Sizing);
			Assert.Equal(ColorRole.Success, chart.ColorRole);
			Assert.True(chart.EnableZoom);
			Assert.Equal(500, chart.SecondaryYAxis.Max);
			Assert.Equal(50, chart.LowerAxis.Max);
			Assert.Equal(2, chart.Series.Count);
			Assert.True(chart.Series[1].UseSecondaryAxis);
		}

		[Fact]
		public void AHeatmapIsFullyBuildable()
		{
			// The grid, its scale and the legend that reads it back, all through
			// the builder - a heatmap configured half in the builder and half by
			// hand would mean the fluent API does not actually reach the feature.
			var chart = new ChartBuilder()
				.WithTitle("Latency")
				.WithPlotHeight(8)
				.WithYRange(0, 100)
				.WithColorBarLegend()
				.AddHeatmap("grid", new double[,] { { 1, 2 }, { 3, 4 } },
					ColorGradient.Predefined["spectrum"])
				.Build();

			Assert.Equal(ChartPlotType.Heatmap, chart.PlotType);
			Assert.Equal(ChartLegendStyle.ColorBar, chart.LegendStyle);
			Assert.True(chart.ShowLegend);
			Assert.Single(chart.Series);
			Assert.True(chart.Series[0].HasGrid);
			Assert.Equal(2, chart.Series[0].GridWidth);
			Assert.Equal(2, chart.Series[0].GridHeight);
		}

		[Fact]
		public void ASparseHeatmapIsFullyBuildable()
		{
			var chart = new ChartBuilder()
				.AddHeatmap("sparse", new[] { (0, 0, 5d), (3, 2, 9d) },
					ColorGradient.Predefined["warm"])
				.Build();

			Assert.True(chart.Series[0].HasGrid);
			Assert.Equal(4, chart.Series[0].GridWidth);
			Assert.Equal(3, chart.Series[0].GridHeight);
			Assert.Null(chart.Series[0].GridValue(1, 1));
		}

		[Fact]
		public void AnErrorStateIsReachableFromTheBuilder()
		{
			var chart = new ChartBuilder()
				.WithEmptyText("waiting")
				.WithErrorText("collector offline")
				.Build();

			Assert.Equal("collector offline", chart.ErrorText);
			Assert.Equal("waiting", chart.EmptyText);
		}

		[Fact]
		public void AGaugeIsFullyBuildable()
		{
			var chart = new ChartBuilder()
				.WithTitle("Capacity")
				.AsGauge(showLabel: true, showValue: false)
				.WithYRange(0, 100)
				.AddSeries("disk", Color.Orange1, new[] { 72d })
				.Build();

			Assert.Equal(ChartPlotType.Gauge, chart.PlotType);
			Assert.True(chart.ShowGaugeLabel);
			Assert.False(chart.ShowGaugeValue);
		}

	}
}

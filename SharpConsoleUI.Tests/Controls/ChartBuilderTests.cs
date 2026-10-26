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
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>Every builder method reaches the property it names.</summary>
	public class ChartBuilderTests
	{
		[Fact]
		public void TheFactoryReturnsABuilder()
		{
			Assert.NotNull(Builders.Controls.Chart());
		}

		[Fact]
		public void PlotTypeAndRenderModeRoundTrip()
		{
			var chart = new ChartBuilder()
				.WithPlotType(ChartPlotType.Area)
				.WithRenderMode(ChartRenderMode.Braille)
				.Build();

			Assert.Equal(ChartPlotType.Area, chart.PlotType);
			Assert.Equal(ChartRenderMode.Braille, chart.RenderMode);
		}

		[Fact]
		public void TitleAndLegendRoundTrip()
		{
			var chart = new ChartBuilder()
				.WithTitle("Traffic")
				.WithLegend(ChartLegendPosition.Top)
				.Build();

			Assert.Equal("Traffic", chart.Title);
			Assert.True(chart.ShowLegend);
			Assert.Equal(ChartLegendPosition.Top, chart.LegendPosition);
		}

		[Fact]
		public void AxisSettingsRoundTrip()
		{
			var chart = new ChartBuilder()
				.WithYRange(0, 100)
				.WithXRange(1, 9)
				.WithGridlines()
				.WithAxisLines()
				.Build();

			Assert.Equal(0, chart.YAxis.Min);
			Assert.Equal(100, chart.YAxis.Max);
			Assert.Equal(1, chart.XAxis.Min);
			Assert.Equal(9, chart.XAxis.Max);
			Assert.True(chart.YAxis.ShowGridlines);
			Assert.True(chart.ShowAxisLines);
		}

		[Fact]
		public void InteractionTogglesRoundTrip()
		{
			var chart = new ChartBuilder()
				.Interactive()
				.Build();

			Assert.True(chart.EnableHover);
			Assert.True(chart.EnableSelection);
			Assert.True(chart.EnableZoom);
		}

		[Fact]
		public void InteractionIsOffWhenNotAskedFor()
		{
			var chart = new ChartBuilder().Build();

			Assert.False(chart.EnableHover);
			Assert.False(chart.EnableSelection);
			Assert.False(chart.EnableZoom);
		}

		[Fact]
		public void SeriesAreAddedWithTheirData()
		{
			var chart = new ChartBuilder()
				.AddSeries("a", Color.Red, new[] { 1d, 2, 3 })
				.AddSeries("b", Color.Blue, new[] { 4d, 5 })
				.Build();

			Assert.Equal(2, chart.Series.Count);
			Assert.Equal(3, chart.Series[0].Points.Count);
			Assert.Equal(2, chart.Series[1].Points.Count);
		}

		[Fact]
		public void AGradientSeriesKeepsItsAxis()
		{
			var chart = new ChartBuilder()
				.AddSeries("g", Color.White, new[] { 1d, 2 },
					ColorGradient.Predefined["spectrum"], ChartGradientAxis.Y)
				.Build();

			Assert.NotNull(chart.Series[0].Gradient);
			Assert.Equal(ChartGradientAxis.Y, chart.Series[0].GradientAxis);
		}

		[Fact]
		public void SplitViewRoundTrips()
		{
			var chart = new ChartBuilder().WithSplitView().Build();

			Assert.True(chart.SplitView);
		}

		[Fact]
		public void CategoriesReachTheAxis()
		{
			var chart = new ChartBuilder()
				.WithCategories("cpu", "gpu")
				.Build();

			Assert.Equal(ChartAxisKind.Category, chart.XAxis.Kind);
			Assert.Equal(2, chart.XAxis.Categories.Count);
		}

		[Fact]
		public void GaugeRoundTrips()
		{
			var chart = new ChartBuilder().AsGauge(showValue: false).Build();

			Assert.Equal(ChartPlotType.Gauge, chart.PlotType);
			Assert.True(chart.ShowGaugeLabel);
			Assert.False(chart.ShowGaugeValue);
		}

		[Fact]
		public void HorizontalAndFixedStepRoundTrip()
		{
			var chart = new ChartBuilder().Horizontal().WithFixedStep(4).Build();

			Assert.Equal(ChartOrientation.Horizontal, chart.Orientation);
			Assert.Equal(ChartSizing.FixedStep, chart.Sizing);
			Assert.Equal(4, chart.StepWidth);
		}

		[Fact]
		public void ChromeAndAnnotationsRoundTrip()
		{
			var chart = new ChartBuilder()
				.WithTitle("T")
				.WithSubtitle("S")
				.WithTitleAlignment(ChartTitleAlignment.Center)
				.WithBorder()
				.WithAxisTitles("minutes", "requests/s")
				.WithReferenceLine(80, Color.Red, "limit")
				.WithHighLowLabels()
				.WithEmptyText("nothing yet")
				.Stacked()
				.Build();

			Assert.Equal("S", chart.Subtitle);
			Assert.Equal(ChartTitleAlignment.Center, chart.TitleAlignment);
			Assert.Equal(BorderStyle.Single, chart.BorderStyle);
			Assert.Equal("minutes", chart.XAxis.Title);
			Assert.Equal("requests/s", chart.YAxis.Title);
			Assert.Single(chart.ReferenceLines);
			Assert.True(chart.ShowHighLowLabels);
			Assert.Equal("nothing yet", chart.EmptyText);
			Assert.True(chart.Stacked);
		}

		[Fact]
		public void PlotHeightRoundTrips()
		{
			var chart = new ChartBuilder().WithPlotHeight(8).Build();

			Assert.Equal(8, chart.PlotHeight);
		}
	}
}

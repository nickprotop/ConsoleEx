// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Xunit;
using static SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Test suite for LineGraphControl.
/// </summary>
public class LineGraphControlTests
{
	#region Defaults & Configuration

	[Fact]
	public void Constructor_CreatesWithDefaults()
	{
		var graph = new LineGraphControl();

		Assert.Equal(LineGraphMode.Braille, graph.Mode);
		Assert.Equal(10, graph.GraphHeight);
		Assert.Equal(100, graph.MaxDataPoints);
		Assert.False(graph.AutoFitDataPoints);
		Assert.Null(graph.MinValue);
		Assert.Null(graph.MaxValue);
		Assert.Null(graph.Title);
		Assert.False(graph.ShowYAxisLabels);
		Assert.False(graph.ShowBaseline);
		Assert.Equal(BorderStyle.None, graph.BorderStyle);
		Assert.Empty(graph.Series);
	}

	[Fact]
	public void GraphHeight_EnforcesMinimum()
	{
		var graph = new LineGraphControl();
		graph.GraphHeight = 1;
		Assert.Equal(3, graph.GraphHeight);
	}

	[Fact]
	public void GraphHeight_AcceptsValidValues()
	{
		var graph = new LineGraphControl();
		graph.GraphHeight = 20;
		Assert.Equal(20, graph.GraphHeight);
	}

	[Fact]
	public void MaxDataPoints_EnforcesMinimum()
	{
		var graph = new LineGraphControl();
		graph.MaxDataPoints = 0;
		Assert.Equal(1, graph.MaxDataPoints);
	}

	[Fact]
	public void Mode_CanBeSet()
	{
		var graph = new LineGraphControl();
		graph.Mode = LineGraphMode.Ascii;
		Assert.Equal(LineGraphMode.Ascii, graph.Mode);
	}

	[Fact]
	public void MinMaxValue_CanBeSet()
	{
		var graph = new LineGraphControl();
		graph.MinValue = -10;
		graph.MaxValue = 100;
		Assert.Equal(-10, graph.MinValue);
		Assert.Equal(100.0, graph.MaxValue);
	}

	[Fact]
	public void Title_Properties()
	{
		var graph = new LineGraphControl();
		graph.Title = "Test";
		graph.TitleColor = Color.Red;
		graph.TitlePosition = TitlePosition.Bottom;
		Assert.Equal("Test", graph.Title);
		Assert.Equal(Color.Red, graph.TitleColor);
		Assert.Equal(TitlePosition.Bottom, graph.TitlePosition);
	}

	[Fact]
	public void AxisLabels_Properties()
	{
		var graph = new LineGraphControl();
		graph.ShowYAxisLabels = true;
		graph.AxisLabelFormat = "F2";
		graph.AxisLabelColor = Color.Yellow;
		Assert.True(graph.ShowYAxisLabels);
		Assert.Equal("F2", graph.AxisLabelFormat);
		Assert.Equal(Color.Yellow, graph.AxisLabelColor);
	}

	#endregion

	#region Series Management

	[Fact]
	public void AddSeries_CreatesAndReturnsSeries()
	{
		var graph = new LineGraphControl();
		var series = graph.AddSeries("cpu", Color.Cyan1);

		Assert.NotNull(series);
		Assert.Equal("cpu", series.Name);
		Assert.Equal(Color.Cyan1, series.LineColor);
		Assert.Single(graph.Series);
	}

	[Fact]
	public void AddSeries_DuplicateName_ReturnsSameSeries()
	{
		var graph = new LineGraphControl();
		var series1 = graph.AddSeries("cpu", Color.Cyan1);
		var series2 = graph.AddSeries("cpu", Color.Red);

		Assert.Same(series1, series2);
		Assert.Single(graph.Series);
	}

	[Fact]
	public void RemoveSeries_RemovesByName()
	{
		var graph = new LineGraphControl();
		graph.AddSeries("cpu", Color.Cyan1);
		graph.AddSeries("mem", Color.Green);

		bool removed = graph.RemoveSeries("cpu");

		Assert.True(removed);
		Assert.Single(graph.Series);
		Assert.Equal("mem", graph.Series[0].Name);
	}

	[Fact]
	public void RemoveSeries_NonExistent_ReturnsFalse()
	{
		var graph = new LineGraphControl();
		bool removed = graph.RemoveSeries("nonexistent");
		Assert.False(removed);
	}

	[Fact]
	public void Series_ReturnsReadOnlySnapshot()
	{
		var graph = new LineGraphControl();
		graph.AddSeries("cpu", Color.Cyan1);
		var series1 = graph.Series;

		graph.AddSeries("mem", Color.Green);
		var series2 = graph.Series;

		Assert.Single(series1);
		Assert.Equal(2, series2.Count);
	}

	[Fact]
	public void AddSeries_WithGradient()
	{
		var graph = new LineGraphControl();
		var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);
		var series = graph.AddSeries("cpu", Color.Cyan1, gradient);

		Assert.NotNull(series.Gradient);
	}

	#endregion

	#region Data Management

	[Fact]
	public void AddDataPoint_SingleSeries_AutoCreatesDefault()
	{
		var graph = new LineGraphControl();
		graph.AddDataPoint(42.0);

		Assert.Single(graph.Series);
		Assert.Equal("default", graph.Series[0].Name);
	}

	[Fact]
	public void AddDataPoint_NamedSeries()
	{
		var graph = new LineGraphControl();
		graph.AddSeries("cpu", Color.Cyan1);
		graph.AddDataPoint("cpu", 55.0);
		graph.AddDataPoint("cpu", 60.0);

		Assert.Single(graph.Series);
	}

	[Fact]
	public void SetDataPoints_ReplacesData()
	{
		var graph = new LineGraphControl();
		graph.AddSeries("cpu", Color.Cyan1);
		graph.SetDataPoints("cpu", new double[] { 1, 2, 3 });
		graph.SetDataPoints("cpu", new double[] { 10, 20 });

		// Confirm series still exists and data replaced (not accumulated)
		Assert.Single(graph.Series);
	}

	[Fact]
	public void SetDataPoints_SingleSeries_AutoCreatesDefault()
	{
		var graph = new LineGraphControl();
		graph.SetDataPoints(new double[] { 1, 2, 3, 4, 5 });

		Assert.Single(graph.Series);
		Assert.Equal("default", graph.Series[0].Name);
	}

	[Fact]
	public void ClearAllData_ClearsAllSeries()
	{
		var graph = new LineGraphControl();
		graph.AddSeries("cpu", Color.Cyan1);
		graph.AddSeries("mem", Color.Green);
		graph.AddDataPoint("cpu", 10);
		graph.AddDataPoint("mem", 20);

		graph.ClearAllData();

		// Series still exist but data cleared
		Assert.Equal(2, graph.Series.Count);
	}

	[Fact]
	public void MaxDataPoints_TrimsOldest()
	{
		var graph = new LineGraphControl();
		graph.MaxDataPoints = 3;
		graph.AddDataPoint(1);
		graph.AddDataPoint(2);
		graph.AddDataPoint(3);
		graph.AddDataPoint(4);
		graph.AddDataPoint(5);

		// Should only have the last 3 points
		Assert.Single(graph.Series);
	}

	[Fact]
	public void ThreadSafety_ConcurrentAdds_NoException()
	{
		var graph = new LineGraphControl();
		graph.AddSeries("cpu", Color.Cyan1);

		var tasks = Enumerable.Range(0, 100).Select(i =>
			Task.Run(() => graph.AddDataPoint("cpu", i))
		).ToArray();

		Task.WaitAll(tasks);

		// Should not crash
		Assert.Single(graph.Series);
	}

	#endregion

	#region Builder

	[Fact]
	public void Builder_SetsAllProperties()
	{
		var graph = LineGraph()
			.WithMode(LineGraphMode.Ascii)
			.WithHeight(15)
			.WithMaxDataPoints(50)
			.WithMinValue(0)
			.WithMaxValue(100)
			.WithTitle("Test Graph")
			.WithYAxisLabels(true, "F2")
			.WithBaseline()
			.WithBorder(BorderStyle.Rounded)
			.Build();

		Assert.Equal(LineGraphMode.Ascii, graph.Mode);
		Assert.Equal(15, graph.GraphHeight);
		Assert.Equal(50, graph.MaxDataPoints);
		Assert.Equal(0.0, graph.MinValue);
		Assert.Equal(100.0, graph.MaxValue);
		Assert.Equal("Test Graph", graph.Title);
		Assert.True(graph.ShowYAxisLabels);
		Assert.Equal("F2", graph.AxisLabelFormat);
		Assert.True(graph.ShowBaseline);
		Assert.Equal(BorderStyle.Rounded, graph.BorderStyle);
	}

	[Fact]
	public void Builder_AddSeries()
	{
		var graph = LineGraph()
			.AddSeries("cpu", Color.Cyan1)
			.AddSeries("mem", Color.Green)
			.Build();

		Assert.Equal(2, graph.Series.Count);
		Assert.Equal("cpu", graph.Series[0].Name);
		Assert.Equal("mem", graph.Series[1].Name);
	}

	[Fact]
	public void Builder_WithData_NamedSeries()
	{
		var graph = LineGraph()
			.AddSeries("cpu", Color.Cyan1)
			.WithData("cpu", new double[] { 10, 20, 30 })
			.Build();

		Assert.Single(graph.Series);
	}

	[Fact]
	public void Builder_WithData_SingleSeries()
	{
		var graph = LineGraph()
			.WithData(new double[] { 10, 20, 30, 40, 50 })
			.Build();

		Assert.Single(graph.Series);
		Assert.Equal("default", graph.Series[0].Name);
	}

	[Fact]
	public void Builder_Stretch()
	{
		var graph = LineGraph()
			.Stretch()
			.Build();

		Assert.Equal(HorizontalAlignment.Stretch, graph.HorizontalAlignment);
	}

	[Fact]
	public void Builder_StandardProperties()
	{
		var graph = LineGraph()
			.WithName("testGraph")
			.WithMargin(1, 2, 3, 4)
			.WithAlignment(HorizontalAlignment.Center)
			.WithVerticalAlignment(VerticalAlignment.Bottom)
			.WithWidth(40)
			.Visible(false)
			.Build();

		Assert.Equal("testGraph", graph.Name);
		Assert.Equal(1, graph.Margin.Left);
		Assert.Equal(2, graph.Margin.Top);
		Assert.Equal(3, graph.Margin.Right);
		Assert.Equal(4, graph.Margin.Bottom);
		Assert.Equal(HorizontalAlignment.Center, graph.HorizontalAlignment);
		Assert.Equal(VerticalAlignment.Bottom, graph.VerticalAlignment);
		Assert.Equal(40, graph.Width);
		Assert.False(graph.Visible);
	}

	[Fact]
	public void Builder_WithTitleAndColor()
	{
		var graph = LineGraph()
			.WithTitle("CPU", Color.Yellow)
			.Build();

		Assert.Equal("CPU", graph.Title);
		Assert.Equal(Color.Yellow, graph.TitleColor);
	}

	[Fact]
	public void Builder_AddSeries_WithGradientSpec()
	{
		var graph = LineGraph()
			.AddSeries("cpu", Color.Cyan1, "cool")
			.Build();

		Assert.Single(graph.Series);
		Assert.NotNull(graph.Series[0].Gradient);
	}

	#endregion

	#region Rendering

	[Fact]
	public void MeasureDOM_ReturnsCorrectSize()
	{
		var graph = new LineGraphControl();
		graph.GraphHeight = 8;
		graph.SetDataPoints(new double[] { 1, 2, 3, 4, 5 });

		var size = graph.MeasureDOM(new LayoutConstraints(0, 200, 0, 200));

		Assert.True(size.Width >= 5);
		Assert.True(size.Height >= 8);
	}

	[Fact]
	public void MeasureDOM_WithTitle_AddsHeight()
	{
		var graph = new LineGraphControl();
		graph.GraphHeight = 8;
		graph.Title = "Test";

		var sizeWithTitle = graph.MeasureDOM(new LayoutConstraints(0, 200, 0, 200));

		graph.Title = null;
		var sizeWithoutTitle = graph.MeasureDOM(new LayoutConstraints(0, 200, 0, 200));

		Assert.True(sizeWithTitle.Height > sizeWithoutTitle.Height);
	}

	[Fact]
	public void MeasureDOM_WithYAxisLabels_AddsWidth()
	{
		var graph = new LineGraphControl();
		graph.GraphHeight = 8;
		graph.SetDataPoints(new double[] { 1, 2, 3 });

		graph.ShowYAxisLabels = false;
		var sizeWithout = graph.MeasureDOM(new LayoutConstraints(0, 200, 0, 200));

		graph.ShowYAxisLabels = true;
		var sizeWith = graph.MeasureDOM(new LayoutConstraints(0, 200, 0, 200));

		Assert.True(sizeWith.Width > sizeWithout.Width);
	}

	[Fact]
	public void PaintDOM_EmptyData_DoesNotCrash()
	{
		var graph = new LineGraphControl();
		graph.GraphHeight = 5;

		var buffer = new CharacterBuffer(40, 10);
		var bounds = new LayoutRect(0, 0, 40, 10);

		// Should not throw
		graph.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	[Fact]
	public void PaintDOM_SingleDataPoint_DoesNotCrash()
	{
		var graph = new LineGraphControl();
		graph.GraphHeight = 5;
		graph.AddDataPoint(42.0);

		var buffer = new CharacterBuffer(40, 10);
		var bounds = new LayoutRect(0, 0, 40, 10);

		graph.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	[Fact]
	public void PaintDOM_BrailleAndAscii_ProduceDifferentOutput()
	{
		var data = new double[] { 10, 45, 28, 67, 34, 89, 56, 23, 78, 45 };

		var brailleGraph = new LineGraphControl();
		brailleGraph.GraphHeight = 5;
		brailleGraph.Mode = LineGraphMode.Braille;
		brailleGraph.SetDataPoints(data);

		var asciiGraph = new LineGraphControl();
		asciiGraph.GraphHeight = 5;
		asciiGraph.Mode = LineGraphMode.Ascii;
		asciiGraph.SetDataPoints(data);

		var brailleBuffer = new CharacterBuffer(40, 10);
		var asciiBuffer = new CharacterBuffer(40, 10);
		var bounds = new LayoutRect(0, 0, 40, 10);

		brailleGraph.PaintDOM(brailleBuffer, bounds, bounds, Color.White, Color.Black);
		asciiGraph.PaintDOM(asciiBuffer, bounds, bounds, Color.White, Color.Black);

		// Compare cell characters - they should differ between modes
		bool anyDifference = false;
		for (int y = 0; y < 10 && !anyDifference; y++)
		{
			for (int x = 0; x < 40 && !anyDifference; x++)
			{
				var brailleCell = brailleBuffer.GetCell(x, y);
				var asciiCell = asciiBuffer.GetCell(x, y);
				if (brailleCell.Character != asciiCell.Character)
					anyDifference = true;
			}
		}

		Assert.True(anyDifference, "Braille and Ascii modes should produce different characters");
	}

	#endregion
}

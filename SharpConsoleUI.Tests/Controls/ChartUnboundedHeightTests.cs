// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// A chart with no explicit PlotHeight, inside a container that does not
	/// bound height.
	/// </summary>
	/// <remarks>
	/// A ScrollablePanel measures its children against an unbounded height,
	/// because scrolling is how it accommodates whatever they ask for. A chart
	/// whose PlotHeight is null answers "as tall as I am allowed", which in that
	/// container is int.MaxValue - so it asked for an infinite row count and
	/// painted nothing at all. Measuring the chart on its own never showed this:
	/// a bare test passes a real MaxHeight, which is the one case that works.
	/// </remarks>
	public class ChartUnboundedHeightTests
	{
		private static ChartControl Gauge()
		{
			var chart = new ChartControl
			{
				Name = "g",
				PlotType = ChartPlotType.Gauge,
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("cpu", Color.Orange1).SetValues(new[] { 72d });
			chart.AddSeries("mem", Color.Orange1).SetValues(new[] { 45d });
			return chart;
		}

		[Fact]
		public void AnUnboundedHeightDoesNotAskForEveryRow()
		{
			var size = Gauge().MeasureDOM(new LayoutConstraints(0, 0, 80, int.MaxValue));

			Assert.True(size.Height < 1000,
				$"an unbounded height constraint produced a desired height of {size.Height}");
		}

		[Fact]
		public void AChartInAScrollablePanelPaints()
		{
			// The real nesting: this is the shape that broke, and the shape a
			// dashboard page actually uses.
			var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
			var window = new Window(system) { Left = 0, Top = 0, Width = 80, Height = 24 };

			var panel = new ScrollablePanelControl
			{
				Name = "page",
				VerticalAlignment = VerticalAlignment.Fill,
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			panel.AddControl(Gauge());

			window.AddControl(panel);
			system.AddWindow(window);

			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();
			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;

			// A gauge fills its bar with coloured SPACES, so counting glyphs sees
			// nothing whether it painted or not. The bar's background colour is
			// the only honest signal that it drew.
			int inked = 0;
			for (int y = 0; y < snap.Height; y++)
				for (int x = 0; x < snap.Width; x++)
				{
					var bg = snap.GetCell(x, y).Background;
					if (bg.R > 120 && bg.G > 60 && bg.B < 80) inked++;
				}

			Assert.True(inked > 0, "a gauge chart inside a scrollable panel painted no bar");
		}
	}
}

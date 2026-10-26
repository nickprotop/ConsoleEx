// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Text;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// The frame around the data: title, subtitle, axis titles, border.
	/// </summary>
	public class ChartChromeTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			int width = 96, int height = 30)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
			var chart = new ChartControl
			{
				RenderMode = ChartRenderMode.Pixel,
				VerticalAlignment = VerticalAlignment.Fill,
			};
			window.AddControl(chart);
			system.AddWindow(window);
			return (system, chart);
		}

		private static CharacterBufferSnapshot Draw(ConsoleWindowSystem system)
		{
			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();
			return system.RenderingDiagnostics!.LastBufferSnapshot!;
		}

		private static string TextOf(CharacterBufferSnapshot snap)
		{
			var sb = new StringBuilder();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character);
				sb.Append('\n');
			}
			return sb.ToString();
		}

		/// <summary>The column a text run starts at, or -1.</summary>
		private static int ColumnOf(CharacterBufferSnapshot snap, string text)
		{
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x + text.Length <= snap.Width; x++)
				{
					bool hit = true;
					for (int i = 0; i < text.Length && hit; i++)
					{
						hit = snap.GetCell(x + i, y).Character.ToString() == text[i].ToString();
					}
					if (hit) return x;
				}
			}
			return -1;
		}

		[Fact]
		public void ASubtitleIsDrawnUnderTheTitle()
		{
			var (system, chart) = Build();
			chart.Title = "Throughput";
			chart.Subtitle = "last 24 hours";
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			string text = TextOf(Draw(system));

			Assert.Contains("Throughput", text);
			Assert.Contains("last 24 hours", text);

			// The subtitle sits below the title, not beside it.
			var lines = text.Split('\n');
			int titleRow = Array.FindIndex(lines, l => l.Contains("Throughput"));
			int subRow = Array.FindIndex(lines, l => l.Contains("last 24 hours"));
			Assert.True(subRow > titleRow, $"subtitle at row {subRow}, title at {titleRow}");

			GC.KeepAlive(system);
		}

		[Fact]
		public void NoSubtitleTakesNoRow()
		{
			// A chart without one must not lose a row of plot to an empty string.
			var (with, a) = Build();
			a.Title = "T";
			a.Subtitle = "sub";
			a.AddSeries("s", Color.Red).SetValues(new[] { 1d, 9 });
			int withRows = PlotRows(Draw(with), Color.Red);

			var (without, b) = Build();
			b.Title = "T";
			b.AddSeries("s", Color.Red).SetValues(new[] { 1d, 9 });
			int withoutRows = PlotRows(Draw(without), Color.Red);

			Assert.True(withoutRows > withRows,
				$"a chart with no subtitle should have more plot: {withoutRows} against {withRows}");
			GC.KeepAlive(with);
			GC.KeepAlive(without);
		}

		[Fact]
		public void TheTitleCanBeCentred()
		{
			var (left, a) = Build();
			a.Title = "Centre me";
			a.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5 });
			int leftColumn = ColumnOf(Draw(left), "Centre me");

			var (centred, b) = Build();
			b.Title = "Centre me";
			b.TitleAlignment = ChartTitleAlignment.Center;
			b.AddSeries("s", Color.Red).SetValues(new[] { 1d, 5 });
			int centreColumn = ColumnOf(Draw(centred), "Centre me");

			Assert.True(leftColumn >= 0 && centreColumn >= 0, "the title did not draw");
			Assert.True(centreColumn > leftColumn + 10,
				$"centred title at column {centreColumn}, left-aligned at {leftColumn}");
			GC.KeepAlive(left);
			GC.KeepAlive(centred);
		}

		[Fact]
		public void TheYAxisTitleIsDrawn()
		{
			// ChartAxis.Title existed as a property and was never painted - declared
			// API that does nothing is worse than none, because it reads as supported.
			var (system, chart) = Build();
			chart.YAxis.Title = "requests/s";
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.Contains("requests/s", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheXAxisTitleIsDrawn()
		{
			var (system, chart) = Build();
			chart.XAxis.Title = "minutes";
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.Contains("minutes", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void ABorderIsDrawnWhenAsked()
		{
			var (system, chart) = Build();
			chart.BorderStyle = BorderStyle.Single;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			string text = TextOf(Draw(system));

			Assert.Contains("┌", text);   // top-left corner
			Assert.Contains("┘", text);   // bottom-right corner
			GC.KeepAlive(system);
		}

		[Fact]
		public void NoBorderByDefault()
		{
			// Additive: a chart that did not ask keeps the look it had.
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.DoesNotContain("┌", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void ABorderDoesNotSwallowThePlot()
		{
			var (system, chart) = Build();
			chart.BorderStyle = BorderStyle.Single;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 9, 2, 8 });

			Assert.True(PlotRows(Draw(system), Color.Red) > 0,
				"turning on the border lost the series");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ChromeSurvivesATinyChart()
		{
			// Title, subtitle, both axis titles and a border, in a window with no
			// room for any of them.
			var (system, chart) = Build(width: 14, height: 7);
			chart.Title = "T";
			chart.Subtitle = "S";
			chart.BorderStyle = BorderStyle.Single;
			chart.XAxis.Title = "x";
			chart.YAxis.Title = "y";
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		private static int PlotRows(CharacterBufferSnapshot snap, Color c)
		{
			int rows = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
				{
					var cell = snap.GetCell(x, y);
					if (cell.Foreground == c || cell.Background == c) { rows++; break; }
				}
			}
			return rows;
		}
	}
}

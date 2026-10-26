// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Ticks chosen by the caller rather than by the nice-number engine.
	/// </summary>
	/// <remarks>
	/// From cxgpu, which labels its sparklines "-1m", "-45s" ... "now" against a
	/// rolling history. Those positions follow the sample clock, not the value
	/// scale, so no amount of formatting the numbers produces them - the caller
	/// has to say where the ticks go.
	/// </remarks>
	public class ChartAxisProviderTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			int width = 90, int height = 20)
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

		[Fact]
		public void AProviderChoosesTheTicks()
		{
			var (system, chart) = Build();
			chart.XAxis.TickProvider = ctx => new[]
			{
				new ChartAxisTick(ctx.Min, "oldest"),
				new ChartAxisTick(ctx.Max, "now"),
			};
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2, 8, 3 });

			string text = TextOf(Draw(system));

			Assert.Contains("oldest", text);
			Assert.Contains("now", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheProviderReplacesTheNiceNumberTicks()
		{
			// Otherwise the two sets collide and the axis reads as noise.
			var (system, chart) = Build();
			chart.XAxis.Min = 0;
			chart.XAxis.Max = 100;
			chart.XAxis.TickProvider = _ => new[] { new ChartAxisTick(50, "MID") };
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			string text = TextOf(Draw(system));

			Assert.Contains("MID", text);
			Assert.DoesNotContain("100", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheContextCarriesTheRangeAndTheWidth()
		{
			// cxgpu needs the point count and the available width to choose a round
			// interval that fits without the labels overlapping.
			ChartAxisContext seen = default;

			var (system, chart) = Build();
			chart.XAxis.Min = 0;
			chart.XAxis.Max = 59;
			chart.XAxis.TickProvider = ctx =>
			{
				seen = ctx;
				return Array.Empty<ChartAxisTick>();
			};
			chart.AddSeries("a", Color.Red).SetValues(new double[60]);
			Draw(system);

			Assert.Equal(0, seen.Min);
			Assert.Equal(59, seen.Max);
			Assert.True(seen.Extent > 10, $"extent was {seen.Extent}; it should be the plot width");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ATickCanCarryItsOwnColour()
		{
			var (system, chart) = Build();
			chart.XAxis.TickProvider = ctx => new[]
			{
				new ChartAxisTick(ctx.Max, "end", Color.Magenta1),
			};
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			var snap = Draw(system);

			bool found = false;
			for (int y = 0; y < snap.Height && !found; y++)
			{
				for (int x = 0; x < snap.Width && !found; x++)
				{
					if (snap.GetCell(x, y).Foreground == Color.Magenta1) found = true;
				}
			}

			Assert.True(found, "the tick's own colour was not used");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ATickLabelIsParsedAsMarkup()
		{
			// cxgpu wraps its labels in a colour tag; the tag must not show through.
			var (system, chart) = Build();
			chart.XAxis.TickProvider = ctx => new[]
			{
				new ChartAxisTick(ctx.Max, "[red]now[/]"),
			};
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			string text = TextOf(Draw(system));

			Assert.Contains("now", text);
			Assert.DoesNotContain("[red]", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void AProviderReturningNothingLeavesTheAxisBare()
		{
			var (system, chart) = Build();
			chart.XAxis.Min = 0;
			chart.XAxis.Max = 100;
			chart.XAxis.TickProvider = _ => Array.Empty<ChartAxisTick>();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.DoesNotContain("100", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AThrowingProviderDoesNotKillTheRender()
		{
			// A provider is app code running inside a paint. A bad one should cost
			// its own ticks, not the whole chart.
			var (system, chart) = Build();
			chart.XAxis.TickProvider = _ => throw new InvalidOperationException("boom");
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheYAxisCanUseAProviderToo()
		{
			var (system, chart) = Build();
			chart.YAxis.TickProvider = ctx => new[] { new ChartAxisTick(ctx.Max, "TOP") };
			chart.AddSeries("a", Color.Red).SetValues(new[] { 1d, 5, 2 });

			Assert.Contains("TOP", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void AProviderWorksOnAShortPlotWithLabelsOn()
		{
			// cxgpu's cards are six rows tall with the plot about four. The ticks
			// drew in the unit tests at twenty rows and not in the app, so the
			// height is the variable worth pinning.
			var (system, chart) = Build(width: 120, height: 8);
			chart.PlotHeight = 4;
			chart.YAxis.ShowLabels = false;
			chart.XAxis.ShowLabels = true;
			chart.XAxis.TickProvider = ctx => new[]
			{
				new ChartAxisTick(ctx.Min, "-1m"),
				new ChartAxisTick(ctx.Max, "now"),
			};
			chart.AddSeries("a", Color.Red).SetValues(new double[48]);

			string text = TextOf(Draw(system));

			Assert.Contains("now", text);
			Assert.Contains("-1m", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void TicksInAnyOrderAllDraw()
		{
			// A provider walking back from "now" emits newest-first. The overlap
			// guard tracked only the rightmost label drawn so far, so after the
			// first tick landed at the right edge every later one was to its left
			// and was dropped - the axis showed one label and no more.
			var (system, chart) = Build(width: 120, height: 10);
			chart.XAxis.Min = 0;
			chart.XAxis.Max = 100;
			chart.XAxis.TickProvider = _ => new[]
			{
				new ChartAxisTick(100, "now"),
				new ChartAxisTick(75, "-30s"),
				new ChartAxisTick(50, "-1m"),
				new ChartAxisTick(25, "-90s"),
			};
			chart.AddSeries("a", Color.Red).SetValues(new double[120]);

			string text = TextOf(Draw(system));

			Assert.Contains("now", text);
			Assert.Contains("-30s", text);
			Assert.Contains("-1m", text);
			Assert.Contains("-90s", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void OverlappingTicksAreStillDropped()
		{
			// The guard still has to do its job: ticks packed tighter than their
			// labels must not paint over each other.
			var (system, chart) = Build(width: 40, height: 10);
			chart.XAxis.Min = 0;
			chart.XAxis.Max = 10;
			chart.XAxis.TickProvider = _ => new[]
			{
				new ChartAxisTick(0, "AAAAAAAA"),
				new ChartAxisTick(1, "BBBBBBBB"),
				new ChartAxisTick(2, "CCCCCCCC"),
			};
			chart.AddSeries("a", Color.Red).SetValues(new double[11]);

			string text = TextOf(Draw(system));

			Assert.Contains("AAAAAAAA", text);
			Assert.DoesNotContain("BBBBBBBB", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void NoProviderMeansNiceNumbersAsBefore()
		{
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 10d, 50, 90 });

			Assert.Null(chart.XAxis.TickProvider);
			Assert.Contains("100", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}
	}
}

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
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>Bars rising from a baseline, one per point.</summary>
	public class ChartBarPlotTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build(
			int width = 96, int height = 30)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
			var chart = new ChartControl
			{
				RenderMode = ChartRenderMode.Pixel,
				PlotType = ChartPlotType.Bar,
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

		/// <summary>True when the colour's dominant channel matches the target's.</summary>
		private static bool IsNear(Color c, Color target)
		{
			if (c == Color.Black) return false;
			if (target == Color.Red) return c.R > c.G + 20 && c.R > c.B + 20;
			if (target == Color.Blue) return c.B > c.R + 20 && c.B > c.G + 20;
			return c == target;
		}

		private static bool CellHas(CharacterBufferSnapshot snap, int x, int y, Color target)
		{
			if ((uint)x >= (uint)snap.Width || (uint)y >= (uint)snap.Height) return false;
			var c = snap.GetCell(x, y);
			return IsNear(c.Foreground, target) || IsNear(c.Background, target);
		}

		private static int CountNear(CharacterBufferSnapshot snap, Color target)
		{
			int n = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++) if (CellHas(snap, x, y, target)) n++;
			}
			return n;
		}

		/// <summary>The row span a colour occupies in one column.</summary>
		private static (int Top, int Bottom) ColumnSpan(CharacterBufferSnapshot snap, int x, Color target)
		{
			int top = int.MaxValue, bottom = int.MinValue;
			for (int y = 0; y < snap.Height; y++)
			{
				if (!CellHas(snap, x, y, target)) continue;
				if (y < top) top = y;
				if (y > bottom) bottom = y;
			}
			return (top, bottom);
		}

		/// <summary>Any column where the colour appears.</summary>
		private static int FirstColumnWith(CharacterBufferSnapshot snap, Color target)
		{
			for (int x = 0; x < snap.Width; x++)
			{
				for (int y = 0; y < snap.Height; y++) if (CellHas(snap, x, y, target)) return x;
			}
			return -1;
		}

		[Fact]
		public void ABarSeriesDrawsFilledColumns()
		{
			// The axis is pinned so the bars' heights are known: with auto-ranging the
			// smallest value sits ON the floor and its bar is one row, which is correct
			// but makes "is this a column?" depend on which bar you happen to pick.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 20d, 60, 40 });

			var snap = Draw(system);
			int tallest = 0;
			for (int x = 0; x < snap.Width; x++)
			{
				var (top, bottom) = ColumnSpan(snap, x, Color.Red);
				if (top == int.MaxValue) continue;
				tallest = Math.Max(tallest, bottom - top + 1);
			}

			Assert.True(tallest > 3,
				$"the tallest bar was {tallest} cells; bars should be columns, not points");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TallerValuesMakeTallerBars()
		{
			var (small, a) = Build();
			a.YAxis.Min = 0;
			a.YAxis.Max = 100;
			a.AddSeries("a", Color.Red).SetValues(new[] { 10d, 10, 10 });
			int shortBar = CountNear(Draw(small), Color.Red);

			var (big, b) = Build();
			b.YAxis.Min = 0;
			b.YAxis.Max = 100;
			b.AddSeries("a", Color.Red).SetValues(new[] { 90d, 90, 90 });
			int tallBar = CountNear(Draw(big), Color.Red);

			Assert.True(tallBar > shortBar,
				$"value 90 painted {tallBar} cells against value 10's {shortBar}");
			GC.KeepAlive(small);
			GC.KeepAlive(big);
		}

		[Fact]
		public void NegativeValuesDrawDownwardFromZero()
		{
			// A bar chart that cannot show a negative is not a bar chart. Zero has to
			// be a real position in the plot, not the floor.
			var (system, chart) = Build();
			chart.YAxis.Min = -10;
			chart.YAxis.Max = 10;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 5d, -5 });

			var snap = Draw(system);

			// The zero row: where a value of 0 maps inside the plot.
			int plotTop = 1, plotBottom = snap.Height - 2;
			int zeroRow = (plotTop + plotBottom) / 2;

			int positiveCol = FirstColumnWith(snap, Color.Red);
			Assert.True(positiveCol >= 0, "nothing drew");

			var positive = ColumnSpan(snap, positiveCol, Color.Red);
			Assert.True(positive.Top < zeroRow,
				$"the positive bar (rows {positive.Top}..{positive.Bottom}) did not rise above the zero row {zeroRow}");

			// Find a column belonging to the negative bar: it must extend below zero.
			bool anyBelow = false;
			for (int x = 0; x < snap.Width && !anyBelow; x++)
			{
				var span = ColumnSpan(snap, x, Color.Red);
				if (span.Bottom > zeroRow + 1) anyBelow = true;
			}

			Assert.True(anyBelow, "no bar fell below the zero row; negatives are not drawn downward");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TwoBarSeriesSitSideBySide()
		{
			// Grouped bars: the two series must not paint over each other.
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 40d, 60 });
			chart.AddSeries("b", Color.Blue).SetValues(new[] { 50d, 30 });

			var snap = Draw(system);

			int redCol = FirstColumnWith(snap, Color.Red);
			int blueCol = FirstColumnWith(snap, Color.Blue);

			Assert.True(redCol >= 0 && blueCol >= 0, "one of the series did not draw");
			Assert.NotEqual(redCol, blueCol);

			// No column may carry both: that would mean one bar painted over the other.
			for (int x = 0; x < snap.Width; x++)
			{
				bool red = ColumnSpan(snap, x, Color.Red).Top != int.MaxValue;
				bool blue = ColumnSpan(snap, x, Color.Blue).Top != int.MaxValue;
				Assert.False(red && blue, $"column {x} carries both series; grouped bars overlap");
			}

			GC.KeepAlive(system);
		}

		[Fact]
		public void BarsLeaveGapsBetweenThem()
		{
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 50d, 50, 50, 50 });

			var snap = Draw(system);

			// Walk the plot's widest row and count runs of bar against gap.
			int runs = 0;
			bool inBar = false;
			for (int x = 0; x < snap.Width; x++)
			{
				bool here = ColumnSpan(snap, x, Color.Red).Top != int.MaxValue;
				if (here && !inBar) runs++;
				inBar = here;
			}

			Assert.True(runs >= 4, $"four equal bars produced {runs} separate runs; they merged");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ACategoryAxisLabelsEachBar()
		{
			var (system, chart) = Build();
			chart.XAxis.Kind = ChartAxisKind.Category;
			chart.XAxis.SetCategories(new[] { "cpu", "gpu", "ram" });
			chart.AddSeries("a", Color.Red).SetCategories(
				new[] { ("cpu", 40d), ("gpu", 70d), ("ram", 55d) });

			var sb = new StringBuilder();
			var snap = Draw(system);
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character);
			}
			string text = sb.ToString();

			Assert.Contains("cpu", text);
			Assert.Contains("ram", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void ABarChartBaselinesAtZeroByDefault()
		{
			// A bar's length IS its value, so the axis has to start at zero or the
			// chart lies: auto-ranging from the minimum draws the smallest bar as a
			// sliver and makes a 10%% difference look like the whole scale.
			var (system, chart) = Build();
			chart.AddSeries("a", Color.Red).SetValues(new[] { 20d, 60, 40 });

			var snap = Draw(system);

			int shortest = int.MaxValue, tallest = 0;
			for (int x = 0; x < snap.Width; x++)
			{
				var (top, bottom) = ColumnSpan(snap, x, Color.Red);
				if (top == int.MaxValue) continue;
				int h = bottom - top + 1;
				shortest = Math.Min(shortest, h);
				tallest = Math.Max(tallest, h);
			}

			Assert.True(shortest > 1,
				$"the smallest bar was {shortest} cell(s); with a zero baseline a value "
				+ "of 20 against a max of 60 should be about a third of the tallest");

			// 20 of 60 is a third: the shortest bar should be roughly a third of the
			// tallest, not a floor-hugging sliver.
			Assert.True(shortest * 4 > tallest,
				$"shortest {shortest} against tallest {tallest} - the axis is not zero-based");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheLastBarStaysInsideThePlot()
		{
			// A bar centred on the last X position is half off the right edge unless
			// the ends are inset by half a slot.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.AddSeries("a", Color.Red).SetValues(new[] { 50d, 50, 50 });

			var snap = Draw(system);

			int rightmost = -1;
			for (int x = snap.Width - 1; x >= 0; x--)
			{
				if (ColumnSpan(snap, x, Color.Red).Top != int.MaxValue) { rightmost = x; break; }
			}

			Assert.True(rightmost >= 0, "nothing drew");
			Assert.True(rightmost < snap.Width - 1,
				$"the last bar reaches column {rightmost} of {snap.Width - 1}; it is clipped");
			GC.KeepAlive(system);
		}

		[Fact]
		public void CategoryLabelsSitUnderTheirBars()
		{
			// A label that is not under its bar is worse than no label: it attributes
			// the value to the wrong category.
			var (system, chart) = Build();
			chart.YAxis.Min = 0;
			chart.YAxis.Max = 100;
			chart.XAxis.Kind = ChartAxisKind.Category;
			chart.XAxis.SetCategories(new[] { "aa", "bb", "cc" });
			chart.AddSeries("s", Color.Red).SetCategories(
				new[] { ("aa", 50d), ("bb", 50d), ("cc", 50d) });

			var snap = Draw(system);

			// The label row is the one holding the category text.
			int labelRow = -1;
			for (int y = snap.Height - 1; y >= 0 && labelRow < 0; y--)
			{
				for (int x = 0; x + 1 < snap.Width; x++)
				{
					if (snap.GetCell(x, y).Character.ToString() == "a"
						&& snap.GetCell(x + 1, y).Character.ToString() == "a")
					{ labelRow = y; break; }
				}
			}
			Assert.True(labelRow >= 0, "no category label row found");

			// For each label, the bar columns must straddle its centre.
			foreach (string name in new[] { "aa", "bb", "cc" })
			{
				int at = ColumnOfText(snap, labelRow, name);
				Assert.True(at >= 0, $"label {name} not found");

				int labelCentre = at + name.Length / 2;
				var (barLeft, barRight) = BarRunAround(snap, labelCentre, Color.Red);

				Assert.True(barLeft >= 0,
					$"label {name} at column {labelCentre} has no bar near it");
				// Within the bar run itself, not merely adjacent: a systematic offset
				// of a cell or two still points the reader at the wrong column.
				Assert.True(labelCentre >= barLeft && labelCentre <= barRight,
					$"label {name} centred at {labelCentre} but its bar spans {barLeft}..{barRight}");
			}

			GC.KeepAlive(system);
		}

		/// <summary>The column a run of text starts at on a row, or -1.</summary>
		private static int ColumnOfText(CharacterBufferSnapshot snap, int row, string text)
		{
			for (int x = 0; x + text.Length <= snap.Width; x++)
			{
				bool hit = true;
				for (int i = 0; i < text.Length && hit; i++)
				{
					hit = snap.GetCell(x + i, row).Character.ToString() == text[i].ToString();
				}
				if (hit) return x;
			}
			return -1;
		}

		/// <summary>The span of the bar run nearest a column.</summary>
		private static (int Left, int Right) BarRunAround(CharacterBufferSnapshot snap, int column, Color c)
		{
			int best = -1, bestDistance = int.MaxValue;
			for (int x = 0; x < snap.Width; x++)
			{
				if (ColumnSpan(snap, x, c).Top == int.MaxValue) continue;
				int d = Math.Abs(x - column);
				if (d < bestDistance) { bestDistance = d; best = x; }
			}
			if (best < 0) return (-1, -1);

			int left = best, right = best;
			while (left > 0 && ColumnSpan(snap, left - 1, c).Top != int.MaxValue) left--;
			while (right + 1 < snap.Width && ColumnSpan(snap, right + 1, c).Top != int.MaxValue) right++;
			return (left, right);
		}

		[Fact]
		public void MoreBarsThanCellsDoesNotThrow()
		{
			var (system, chart) = Build(width: 20, height: 10);
			var many = new double[200];
			for (int i = 0; i < many.Length; i++) many[i] = i % 50;
			chart.AddSeries("a", Color.Red).SetValues(many);

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void BarsWorkInSplitView()
		{
			var (system, chart) = Build();
			chart.SplitView = true;
			chart.AddSeries("down", Color.Red).SetValues(new[] { 30d, 60 });
			chart.AddSeries("up", Color.Blue).SetValues(new[] { 20d, 40 });

			Assert.Null(Record.Exception(() => Draw(system)));
			GC.KeepAlive(system);
		}
	}
}

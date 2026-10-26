// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// The exact shape cxnet's network monitor needs.
	/// </summary>
	/// <remarks>
	/// Built from the real app rather than invented: split area plot, braille,
	/// transparent plot, per-height gradients, 512 samples resampled to the
	/// width, and each half rescaled to its own rolling peak every tick. The
	/// unit tests for those pieces all passed while the live app showed a flat
	/// upload half, so this reproduces the app's whole configuration at once.
	/// </remarks>
	public class ChartCxnetShapeTests
	{
		private const int GraphHeight = 14;

		private static (ConsoleWindowSystem System, ChartControl Chart) BuildMonitor(
			int width = 100, int height = 26)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };

			var c = new ChartControl
			{
				Name = "net",
				PlotType = ChartPlotType.Area,
				RenderMode = ChartRenderMode.Braille,
				SplitView = true,
				ResampleToWidth = true,
				PlotBackground = new Color(0, 0, 0, 0),
				PlotHeight = GraphHeight,
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			c.XAxis.ShowLabels = false;
			c.YAxis.ShowLabels = false;
			c.YAxis.Min = 0;
			c.LowerAxis.Min = 0;

			var down = c.AddSeries("down", Color.Cyan1);
			down.Gradient = ColorGradient.FromColors(Color.Cyan1.Shade(0.5), Color.Cyan1);
			down.GradientAxis = ChartGradientAxis.Y;

			var up = c.AddSeries("up", Color.Grey70);
			up.Gradient = ColorGradient.FromColors(Color.Grey35, Color.Grey70);
			up.GradientAxis = ChartGradientAxis.Y;

			window.AddControl(c);
			system.AddWindow(window);
			return (system, c);
		}

		/// <summary>Feeds the chart the way cxnet does on every sample.</summary>
		private static void Feed(ChartControl c, double[] down, double[] up)
		{
			c.Series[0].SetValues(down);
			c.Series[1].SetValues(up);
			c.YAxis.Max = Math.Max(down.DefaultIfEmpty(0).Max(), 1024);
			c.LowerAxis.Max = Math.Max(up.DefaultIfEmpty(0).Max(), 1024);
			c.Invalidate(Invalidation.Repaint);
		}

		private static CharacterBufferSnapshot Draw(ConsoleWindowSystem system)
		{
			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();
			return system.RenderingDiagnostics!.LastBufferSnapshot!;
		}

		/// <summary>
		/// Rows carrying a series, matched by dominant channel rather than exact colour.
		/// </summary>
		/// <remarks>
		/// Both series here carry a gradient, so almost no cell holds the exact
		/// colour that was passed in - only the one where the gradient reaches its
		/// bright end. Matching exactly counted a single row and read as a collapsed
		/// band when the renderer was fine.
		/// </remarks>
		private static (int Top, int Bottom, int Rows) Span(CharacterBufferSnapshot snap, Func<Color, bool> match)
		{
			int top = int.MaxValue, bottom = int.MinValue, rows = 0;
			for (int y = 0; y < snap.Height; y++)
			{
				bool hit = false;
				for (int x = 0; x < snap.Width && !hit; x++)
				{
					var cell = snap.GetCell(x, y);
					hit = match(cell.Foreground) || match(cell.Background);
				}
				if (!hit) continue;
				rows++;
				if (y < top) top = y;
				if (y > bottom) bottom = y;
			}
			return (top, bottom, rows);
		}

		/// <summary>Cyan-dominant: blue and green high, red low.</summary>
		private static bool IsCyanish(Color c) =>
			c != Color.Black && c.G > c.R + 20 && c.B > c.R + 20;

		/// <summary>Grey: all three channels close together, and not the backdrop.</summary>
		private static bool IsGreyish(Color c) =>
			c != Color.Black
			&& Math.Abs(c.R - c.G) < 18 && Math.Abs(c.G - c.B) < 18
			&& c.R > 40 && c.R < 230;

		private static double[] History(int seed, double peak)
		{
			var rng = new Random(seed);
			var v = new double[512];
			double level = peak * 0.4;
			for (int i = 0; i < v.Length; i++)
			{
				level = Math.Clamp(level + (rng.NextDouble() - 0.45) * peak * 0.15, 0, peak);
				v[i] = level;
			}
			return v;
		}

		[Fact]
		public void TheUploadHalfUsesItsOwnBand()
		{
			// cxnet's real numbers: about 6 MB/s down against 1 MB/s up. On a
			// shared scale the upload is a flat line at the centre; given its own
			// peak it must fill its half the way the download fills its own.
			var (system, chart) = BuildMonitor();
			Feed(chart, History(3, 6_000_000), History(9, 1_000_000));

			var snap = Draw(system);
			var down = Span(snap, IsCyanish);
			var up = Span(snap, IsGreyish);

			Assert.True(down.Rows > 2, $"the download half drew {down.Rows} rows");
			Assert.True(up.Rows > 2,
				$"the upload half drew only {up.Rows} rows; with its own peak it should "
				+ "use its band like the download does");
			GC.KeepAlive(system);
		}

		[Fact]
		public void BothHalvesGetRoughlyEqualRoom()
		{
			// The split centre belongs in the middle of the plot. A centre set too
			// high squeezes the lower band into a strip.
			var (system, chart) = BuildMonitor();
			Feed(chart, History(3, 6_000_000), History(9, 1_000_000));

			var snap = Draw(system);
			var down = Span(snap, IsCyanish);
			var up = Span(snap, IsGreyish);

			int upperRows = down.Rows, lowerRows = up.Rows;

			Assert.True(lowerRows * 3 >= upperRows,
				$"the halves are lopsided: upper {upperRows} rows, lower {lowerRows}");
			GC.KeepAlive(system);
		}

		[Fact]
		public void ACentreAxisCanBeDrawnBetweenTheHalves()
		{
			// Opt-in: the line that marks zero between the two directions.
			var (plain, a) = BuildMonitor();
			Feed(a, History(3, 6_000_000), History(9, 1_000_000));
			string without = TextOf(Draw(plain));

			var (lined, b) = BuildMonitor();
			b.ShowCentreAxis = true;
			Feed(b, History(3, 6_000_000), History(9, 1_000_000));
			string with = TextOf(Draw(lined));

			Assert.DoesNotContain("┄", without);
			Assert.Contains("┄", with);
			GC.KeepAlive(plain);
			GC.KeepAlive(lined);
		}

		[Fact]
		public void TheCentreAxisCanCarryACaption()
		{
			// The line marks zero; the caption says what zero is measured in.
			var (system, chart) = BuildMonitor();
			chart.ShowCentreAxis = true;
			chart.CentreAxisCaption = "B/s";
			Feed(chart, History(3, 6_000_000), History(9, 1_000_000));

			Assert.Contains("B/s", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheCaptionSitsAtTheLeftAndTheValueAtTheRight()
		{
			var (system, chart) = BuildMonitor();
			chart.ShowCentreAxis = true;
			chart.CentreAxisCaption = "CAPTION";
			chart.ShowCentreAxisValue = true;
			Feed(chart, History(3, 6_000_000), History(9, 1_000_000));

			var snap = Draw(system);
			int row = RowOf(snap, "CAPTION");
			Assert.True(row >= 0, "the caption did not draw");

			int capAt = ColumnOf(snap, row, "CAPTION");
			int zeroAt = LastColumnOf(snap, row, "0");

			Assert.True(capAt >= 0 && zeroAt >= 0, "caption or value missing from the centre row");
			Assert.True(zeroAt > capAt + 10,
				$"the value should sit at the far right: caption at {capAt}, value at {zeroAt}");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheLineResumesAfterTheCaption()
		{
			// Inline, as SparklineControl's InlineTitleWithBaseline does: the dashes
			// continue past the text rather than the caption replacing the line.
			var (system, chart) = BuildMonitor();
			chart.ShowCentreAxis = true;
			chart.CentreAxisCaption = "B/s";
			Feed(chart, History(3, 6_000_000), History(9, 1_000_000));

			var snap = Draw(system);
			int row = RowOf(snap, "B/s");
			Assert.True(row >= 0, "the caption did not draw");

			int dashes = 0;
			for (int x = 0; x < snap.Width; x++)
			{
				if (snap.GetCell(x, row).Character.Value == '\u2504') dashes++;
			}

			Assert.True(dashes > 10,
				$"only {dashes} line cells on the caption row; the line should resume after the text");
			GC.KeepAlive(system);
		}

		[Fact]
		public void TheCentreAxisCharacterIsSettable()
		{
			// Its own glyph, so a chart carrying both a centre axis and reference
			// lines does not draw the frame and the values identically.
			var (system, chart) = BuildMonitor();
			chart.ShowCentreAxis = true;
			chart.CentreAxisChar = '=';
			Feed(chart, History(3, 6_000_000), History(9, 1_000_000));

			string text = TextOf(Draw(system));

			Assert.Contains("=", text);
			Assert.DoesNotContain("\u2504", text);
			GC.KeepAlive(system);
		}

		[Fact]
		public void NoCaptionMeansNoText()
		{
			var (system, chart) = BuildMonitor();
			chart.ShowCentreAxis = true;
			Feed(chart, History(3, 6_000_000), History(9, 1_000_000));

			Assert.Null(chart.CentreAxisCaption);
			GC.KeepAlive(system);
		}

		/// <summary>The row a text run appears on, or -1.</summary>
		private static int RowOf(CharacterBufferSnapshot snap, string text)
		{
			for (int y = 0; y < snap.Height; y++)
			{
				if (ColumnOf(snap, y, text) >= 0) return y;
			}
			return -1;
		}

		/// <summary>The column a text run starts at on a row, or -1.</summary>
		private static int ColumnOf(CharacterBufferSnapshot snap, int row, string text)
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

		/// <summary>The last column a one-character run appears at on a row, or -1.</summary>
		private static int LastColumnOf(CharacterBufferSnapshot snap, int row, string text)
		{
			for (int x = snap.Width - text.Length; x >= 0; x--)
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

		[Fact]
		public void TheCentreAxisIsOffByDefault()
		{
			Assert.False(new ChartControl().ShowCentreAxis);
		}

		[Fact]
		public void TheCentreAxisNeedsSplitView()
		{
			// Without two halves there is no centre to mark.
			var (system, chart) = BuildMonitor();
			chart.SplitView = false;
			chart.ShowCentreAxis = true;
			Feed(chart, History(3, 6_000_000), History(9, 1_000_000));

			Assert.DoesNotContain("┄", TextOf(Draw(system)));
			GC.KeepAlive(system);
		}

		[Fact]
		public void RescalingEachTickDoesNotThrow()
		{
			// cxnet rewrites both axis maxima on every sample.
			var (system, chart) = BuildMonitor();

			var ex = Record.Exception(() =>
			{
				for (int tick = 0; tick < 5; tick++)
				{
					Feed(chart, History(tick, 1_000_000 * (tick + 1)), History(tick + 50, 200_000));
					Draw(system);
				}
			});

			Assert.Null(ex);
			GC.KeepAlive(system);
		}

		private static string TextOf(CharacterBufferSnapshot snap)
		{
			var sb = new System.Text.StringBuilder();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character);
			}
			return sb.ToString();
		}
	}
}

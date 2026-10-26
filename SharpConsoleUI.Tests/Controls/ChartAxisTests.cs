// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class ChartAxisTests
	{
		[Fact]
		public void TicksLandOnRoundNumbers()
		{
			// "Nice numbers": a human reading an axis expects 0, 20, 40 - never
			// 0, 17.3, 34.6. Steps are 1, 2 or 5 times a power of ten.
			//
			// The range matters. A range whose raw step is ALREADY round (0..100 over
			// a short extent gives exactly 50) passes even with no rounding at all, so
			// it proves nothing. 0..137 over this extent gives a raw step of 34.25,
			// which only lands on a round number if the rounding really happens.
			var axis = new ChartAxis();

			var ticks = axis.SelectTicks(0, 137, availableExtent: 32);

			Assert.NotEmpty(ticks);
			Assert.All(ticks, t =>
			{
				// Every tick must be a whole multiple of a 1/2/5-times-power-of-ten step.
				double tenths = t.Value / 10.0;
				Assert.Equal(tenths, System.Math.Round(tenths, 6));
			});
		}

		[Fact]
		public void AnAwkwardRangeStillStepsBy1_2_or5()
		{
			// Pins the step itself rather than the tick values, so a change that
			// happens to keep ticks divisible but abandons nice stepping is caught.
			var axis = new ChartAxis();

			var ticks = axis.SelectTicks(0, 137, availableExtent: 32);

			Assert.True(ticks.Count >= 2, "need two ticks to measure a step");
			double step = ticks[1].Value - ticks[0].Value;
			double magnitude = System.Math.Pow(10, System.Math.Floor(System.Math.Log10(step)));
			double normalised = System.Math.Round(step / magnitude, 6);

			Assert.True(normalised == 1 || normalised == 2 || normalised == 5,
				$"step {step} normalises to {normalised}, which is not 1, 2 or 5");
		}

		[Fact]
		public void MoreRoomGivesMoreTicks()
		{
			var axis = new ChartAxis();

			int few = axis.SelectTicks(0, 100, availableExtent: 8).Count;
			int many = axis.SelectTicks(0, 100, availableExtent: 80).Count;

			Assert.True(many > few, $"a wider axis produced no more ticks: {few} then {many}");
		}

		[Fact]
		public void TicksStayInsideTheRange()
		{
			var axis = new ChartAxis();

			var ticks = axis.SelectTicks(7, 93, availableExtent: 20);

			Assert.All(ticks, t => Assert.InRange(t.Value, 7, 93));
		}

		[Fact]
		public void AFlatRangeStillProducesOneTick()
		{
			// Every value identical: max - min is 0, which is a divisor everywhere
			// downstream. The axis must answer with something usable rather than
			// an empty list or a division by zero.
			var axis = new ChartAxis();

			var ticks = axis.SelectTicks(5, 5, availableExtent: 20);

			Assert.NotEmpty(ticks);
			Assert.All(ticks, t => Assert.Equal(5, t.Value));
		}

		[Fact]
		public void AZeroWidthAxisProducesNoTicksAndDoesNotThrow()
		{
			var axis = new ChartAxis();

			var ticks = axis.SelectTicks(0, 100, availableExtent: 0);

			Assert.Empty(ticks);
		}

		[Fact]
		public void MaxTicksIsRespected()
		{
			var axis = new ChartAxis { MaxTicks = 3 };

			var ticks = axis.SelectTicks(0, 100, availableExtent: 60);

			Assert.True(ticks.Count <= 3, $"MaxTicks 3 but got {ticks.Count}");
		}

		[Fact]
		public void ACategoryAxisLabelsByIndex()
		{
			var axis = new ChartAxis { Kind = ChartAxisKind.Category };
			axis.SetCategories(new[] { "cpu", "gpu", "ram" });

			Assert.Equal("cpu", axis.FormatValue(0));
			Assert.Equal("ram", axis.FormatValue(2));
		}

		[Fact]
		public void ACategoryIndexOutsideTheListFormatsEmpty()
		{
			var axis = new ChartAxis { Kind = ChartAxisKind.Category };
			axis.SetCategories(new[] { "only" });

			Assert.Equal(string.Empty, axis.FormatValue(5));
		}

		[Fact]
		public void ATimeAxisFormatsAsADate()
		{
			var axis = new ChartAxis { Kind = ChartAxisKind.Time, LabelFormat = "HH:mm" };
			var when = new System.DateTime(2026, 1, 1, 13, 45, 0);

			Assert.Equal("13:45", axis.FormatValue(when.Ticks));
		}

		[Fact]
		public void ATimeValueOutsideDateTimeRangeDoesNotThrow()
		{
			// A zoomed-out time axis can compute a tick past the end of time.
			// Throwing there would kill a repaint over a value nobody can read.
			var axis = new ChartAxis { Kind = ChartAxisKind.Time };

			Assert.Null(Record.Exception(() => axis.FormatValue(double.MaxValue)));
		}

		[Fact]
		public void ACustomFormatterWinsOverEverything()
		{
			var axis = new ChartAxis { LabelFormatter = v => $"<{v}>" };

			Assert.Equal("<42>", axis.FormatValue(42));
		}

		[Fact]
		public void ACategoryAxisStepsWholeCategories()
		{
			// Half a category is not a thing; ticks must land on indices.
			var axis = new ChartAxis { Kind = ChartAxisKind.Category };
			axis.SetCategories(new[] { "a", "b", "c", "d", "e" });

			var ticks = axis.SelectTicks(0, 4, availableExtent: 40);

			Assert.All(ticks, t => Assert.Equal(t.Value, System.Math.Round(t.Value)));
		}
	}
}

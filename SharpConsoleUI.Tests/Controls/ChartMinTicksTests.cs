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
	/// <summary>
	/// Asking an axis for MORE ticks than its height suggests.
	/// </summary>
	/// <remarks>
	/// Tick density is one tick per eight cells, which on a short Y axis leaves
	/// room for none - so the axis end-labels instead and a reader sees only the
	/// range. MaxTicks could not help: it is applied with Math.Min, so it only
	/// ever LOWERS the count. There was no way to ask for a denser axis.
	/// </remarks>
	public class ChartMinTicksTests
	{
		private static ChartAxis Axis() => new();

		[Fact]
		public void AShortAxisEndLabelsByDefault()
		{
			// The behaviour MinTicks is opting out of, pinned so a change to it
			// shows up here rather than silently redefining the default.
			var ticks = Axis().SelectTicks(0, 100, 10);

			Assert.Equal(2, ticks.Count);
			Assert.Equal(0, ticks[0].Value);
			Assert.Equal(100, ticks[1].Value);
		}

		[Fact]
		public void MinTicksAsksForMoreThanTheHeightWouldGive()
		{
			var axis = Axis();
			axis.MinTicks = 5;

			var ticks = axis.SelectTicks(0, 100, 10);

			Assert.True(ticks.Count >= 5,
				$"asked for at least 5 ticks on a 10-cell axis, got {ticks.Count}");
		}

		[Fact]
		public void MinTicksStillLandsOnNiceNumbers()
		{
			var axis = Axis();
			axis.MinTicks = 5;

			var ticks = axis.SelectTicks(0, 100, 10);

			// Denser must not mean arbitrary: the whole point of the tick engine
			// is numbers a reader can divide at a glance.
			foreach (var t in ticks)
				Assert.Equal(0, t.Value % 25 == 0 ? 0 : t.Value % 20 == 0 ? 0 : t.Value % 10 == 0 ? 0 : 1);
		}

		[Fact]
		public void MinTicksCannotDemandATickPerRow()
		{
			// A tick every cell is unreadable, so the request is clamped by what
			// the axis can actually show.
			var axis = Axis();
			axis.MinTicks = 500;

			var ticks = axis.SelectTicks(0, 100, 10);

			// One tick per cell is the ceiling; the range endpoints make that
			// cells + 1 labels at most, not an unbounded run.
			Assert.True(ticks.Count <= 11,
				$"a 10-cell axis produced {ticks.Count} ticks");
		}

		[Fact]
		public void NullMinTicksChangesNothing()
		{
			var withNull = Axis();
			var plain = Axis();
			plain.MinTicks = null;

			Assert.Equal(withNull.SelectTicks(0, 100, 40).Count,
				plain.SelectTicks(0, 100, 40).Count);
		}
	}
}

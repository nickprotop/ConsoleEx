// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Performance;
using Xunit;

namespace SharpConsoleUI.Tests.Performance;

/// <summary>
/// The window of frames the performance verb reports from.
/// </summary>
/// <remarks>
/// A rate needs a window: one interval cannot tell a steady sixty from a mean of
/// sixty with a stutter in it, and the stutter is the thing worth finding.
/// </remarks>
public class FrameSampleRingTests
{
	private static FrameSampleRing.Sample Frame(double ms) => new(ms, 0, 0, 0, 0, 0, 0, 0);

	[Fact]
	public void AnEmptyRingHasNothingToSay()
	{
		// Null rather than a row of zeroes: an application that has not rendered
		// is not an application rendering at zero frames a second.
		Assert.Null(new FrameSampleRing().Stats());
	}

	[Fact]
	public void TheRateIsFramesOverElapsedNotTheReciprocalOfTheMean()
	{
		var ring = new FrameSampleRing();

		// Three quick frames and one long one: mean 25ms, but four frames really
		// did take 100ms. Reciprocal-of-the-mean would say 40fps by coincidence
		// here only if every frame were equal; with a skew the two part company.
		foreach (double ms in new[] { 1.0, 1.0, 1.0, 97.0 }) ring.Add(Frame(ms));

		var stats = ring.Stats()!;

		Assert.Equal(100.0, stats.SpanMs, 3);
		Assert.Equal(25.0, stats.MeanMs, 3);
		Assert.Equal(40.0, stats.Fps, 3);
	}

	[Fact]
	public void ThePercentileFindsAStutterTheMeanHides()
	{
		var ring = new FrameSampleRing();

		for (int i = 0; i < 99; i++) ring.Add(Frame(16.0));
		ring.Add(Frame(500.0));

		var stats = ring.Stats()!;

		Assert.Equal(500.0, stats.MaxMs, 3);
		Assert.Equal(16.0, stats.MinMs, 3);

		// The mean barely moves - which is exactly why the maximum is reported.
		Assert.True(stats.MeanMs < 22, $"mean {stats.MeanMs} should hide the hitch");
	}

	[Fact]
	public void TheWindowKeepsTheNewestFramesWhenItOverflows()
	{
		var ring = new FrameSampleRing();

		for (int i = 0; i < FrameSampleRing.Capacity + 1; i++)
			ring.Add(Frame(i == 0 ? 999.0 : 10.0));

		var stats = ring.Stats()!;

		Assert.Equal(FrameSampleRing.Capacity, stats.Frames);

		// The 999 was the oldest and is gone; everything left is a 10.
		Assert.Equal(10.0, stats.MaxMs, 3);

		// Total frames counts what was rendered, not what is still held.
		Assert.Equal(FrameSampleRing.Capacity + 1, stats.TotalFrames);
	}

	[Fact]
	public void ResetEmptiesTheWindowSoAMeasurementCanStartHere()
	{
		var ring = new FrameSampleRing();
		ring.Add(Frame(16.0));

		ring.Reset();

		Assert.Null(ring.Stats());
		Assert.Equal(0, ring.TotalFrames);
	}

	[Fact]
	public void AWindowWithNoRendererMetricsSaysSoRatherThanReportingZeroCost()
	{
		var ring = new FrameSampleRing();
		ring.Add(Frame(16.0));

		// A stage cost of zero reads as "this stage is free"; it means nobody
		// measured it, and the difference has to survive to the caller.
		Assert.False(ring.Stats()!.Measured);
	}

	[Fact]
	public void AFrameCarryingRendererMetricsMarksTheWindowMeasured()
	{
		var ring = new FrameSampleRing();
		ring.Add(new FrameSampleRing.Sample(16.0, 2.0, 1.0, 0.5, 0.25, 1200, 40, 40));

		var stats = ring.Stats()!;

		Assert.True(stats.Measured);
		Assert.Equal(1.0, stats.Efficiency);
	}

	[Fact]
	public void EfficiencyIsNullWhenNothingWasMarkedDirty()
	{
		var ring = new FrameSampleRing();
		ring.Add(new FrameSampleRing.Sample(16.0, 1.0, 0, 0, 0, 0, 0, 0));

		// No work asked for is not inefficiency, so it is not a ratio of zero.
		Assert.Null(ring.Stats()!.Efficiency);
	}
}

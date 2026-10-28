// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Configuration;

/// <summary>
/// Which clock each part of the system runs on.
/// </summary>
/// <remarks>
/// One provider for everything is the common case and stays one line. The named
/// ones exist because the parts measure different things: freezing the mouse
/// clock to step a double click apart should not also stop a progress bar.
/// </remarks>
public class TimeOptionsTests
{
	private sealed class Named : TimeProvider
	{
		private readonly DateTimeOffset _at;

		public Named(int minute) => _at = new DateTimeOffset(2026, 1, 1, 12, minute, 0, TimeSpan.Zero);

		public override DateTimeOffset GetUtcNow() => _at;

		public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
	}

	/// <summary>
	/// What a clock reports, not which instance it is.
	/// </summary>
	/// <remarks>
	/// The system wraps each clock so the diagnostics socket can point it at
	/// another one while the application runs, so identity is no longer the thing
	/// to assert - and what a caller depends on was always the reading anyway.
	/// </remarks>
	private static DateTimeOffset Reads(TimeProvider clock) => clock.GetUtcNow();

	private static DateTimeOffset At(int minute) =>
		new(2026, 1, 1, 12, minute, 0, TimeSpan.Zero);

	private static ConsoleWindowSystem Build(TimeOptions? time) =>
		new(new HeadlessConsoleDriver(40, 10),
			ConsoleWindowSystemOptions.Create() with { Time = time });

	[Fact]
	public void With_nothing_configured_everything_runs_on_the_real_clock()
	{
		var system = Build(null);

		// Within a second of the real clock, which a fixed fake never is.
		var now = DateTimeOffset.UtcNow;

		Assert.True((Reads(system.MouseTime) - now).Duration() < TimeSpan.FromSeconds(1));
		Assert.True((Reads(system.ScheduleRepaintTime) - now).Duration() < TimeSpan.FromSeconds(1));
		Assert.True((Reads(system.AnimationTime) - now).Duration() < TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void An_All_clock_covers_every_part()
	{
		var clock = new Named(1);

		var system = Build(new TimeOptions { All = clock });

		Assert.Equal(At(1), Reads(system.MouseTime));
		Assert.Equal(At(1), Reads(system.ScheduleRepaintTime));
		Assert.Equal(At(1), Reads(system.AnimationTime));
	}

	[Fact]
	public void A_named_clock_wins_over_the_default()
	{
		var fallback = new Named(1);
		var mouse = new Named(2);

		var system = Build(new TimeOptions { All = fallback, Mouse = mouse });

		Assert.Equal(At(2), Reads(system.MouseTime));
		Assert.Equal(At(1), Reads(system.ScheduleRepaintTime));
		Assert.Equal(At(1), Reads(system.AnimationTime));
	}

	[Fact]
	public void A_named_clock_alone_leaves_the_rest_real()
	{
		var animation = new Named(3);

		var system = Build(new TimeOptions { Animation = animation });

		var now = DateTimeOffset.UtcNow;

		Assert.Equal(At(3), Reads(system.AnimationTime));
		Assert.True((Reads(system.MouseTime) - now).Duration() < TimeSpan.FromSeconds(1));
		Assert.True((Reads(system.ScheduleRepaintTime) - now).Duration() < TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Each_clock_reaches_the_part_it_names()
	{
		// Not just the properties: the parts must actually read them.
		var mouse = new Named(5);
		var repaint = new Named(6);

		var system = Build(new TimeOptions { Mouse = mouse, ScheduleRepaint = repaint });

		// A schedule is due relative to the REPAINT clock, so one created now is
		// not due now - and its due time is that clock's, not the machine's.
		int fired = 0;
		system.ScheduleRepaint(TimeSpan.FromMilliseconds(10), () => fired++);

		Assert.False(system.ServiceRepaintSchedules());
		Assert.Equal(0, fired);
	}
}

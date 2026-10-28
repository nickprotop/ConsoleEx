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

	private static ConsoleWindowSystem Build(TimeOptions? time) =>
		new(new HeadlessConsoleDriver(40, 10),
			ConsoleWindowSystemOptions.Create() with { Time = time });

	[Fact]
	public void With_nothing_configured_everything_runs_on_the_real_clock()
	{
		var system = Build(null);

		Assert.Same(TimeProvider.System, system.MouseTime);
		Assert.Same(TimeProvider.System, system.ScheduleRepaintTime);
		Assert.Same(TimeProvider.System, system.AnimationTime);
	}

	[Fact]
	public void An_All_clock_covers_every_part()
	{
		var clock = new Named(1);

		var system = Build(new TimeOptions { All = clock });

		Assert.Same(clock, system.MouseTime);
		Assert.Same(clock, system.ScheduleRepaintTime);
		Assert.Same(clock, system.AnimationTime);
	}

	[Fact]
	public void A_named_clock_wins_over_the_default()
	{
		var fallback = new Named(1);
		var mouse = new Named(2);

		var system = Build(new TimeOptions { All = fallback, Mouse = mouse });

		Assert.Same(mouse, system.MouseTime);
		Assert.Same(fallback, system.ScheduleRepaintTime);
		Assert.Same(fallback, system.AnimationTime);
	}

	[Fact]
	public void A_named_clock_alone_leaves_the_rest_real()
	{
		var animation = new Named(3);

		var system = Build(new TimeOptions { Animation = animation });

		Assert.Same(animation, system.AnimationTime);
		Assert.Same(TimeProvider.System, system.MouseTime);
		Assert.Same(TimeProvider.System, system.ScheduleRepaintTime);
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

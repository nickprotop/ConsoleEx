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

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// Periodic repainting, serviced by the frame rather than by a private timer.
/// </summary>
/// <remarks>
/// A clock in a panel, an FPS readout, an indeterminate progress bar and an
/// animated desktop each carried a System.Threading.Timer whose only job was
/// to call Invalidate on a schedule. Four private clocks, none of which the
/// system could see, none of which stopped when the application went idle,
/// and each firing on a pool thread so its control had to publish state with
/// Interlocked to be read safely by the renderer.
/// </remarks>
public class RepaintScheduleTests
{
	private sealed class TestClock : TimeProvider
	{
		private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

		public override DateTimeOffset GetUtcNow() => _now;

		public void Advance(TimeSpan by) => _now = _now.Add(by);
	}

	private static ConsoleWindowSystem Headless(TimeProvider clock) =>
		new(new HeadlessConsoleDriver(40, 10),
			ConsoleWindowSystemOptions.Create() with { Time = new TimeOptions { All = clock } });

	[Fact]
	public void A_schedule_fires_once_its_interval_has_passed()
	{
		var clock = new TestClock();
		var system = Headless(clock);
		int fired = 0;

		system.ScheduleRepaint(TimeSpan.FromMilliseconds(500), () => fired++);

		system.ServiceRepaintSchedules();
		Assert.Equal(0, fired);

		clock.Advance(TimeSpan.FromMilliseconds(600));
		system.ServiceRepaintSchedules();
		Assert.Equal(1, fired);
	}

	[Fact]
	public void It_repeats()
	{
		var clock = new TestClock();
		var system = Headless(clock);
		int fired = 0;

		system.ScheduleRepaint(TimeSpan.FromMilliseconds(100), () => fired++);

		for (int i = 0; i < 3; i++)
		{
			clock.Advance(TimeSpan.FromMilliseconds(100));
			system.ServiceRepaintSchedules();
		}

		Assert.Equal(3, fired);
	}

	[Fact]
	public void A_long_pause_does_not_fire_a_burst()
	{
		// An application idle for a minute must not wake to sixty ticks of a
		// one-second schedule. One catch-up tick is the honest answer.
		var clock = new TestClock();
		var system = Headless(clock);
		int fired = 0;

		system.ScheduleRepaint(TimeSpan.FromMilliseconds(100), () => fired++);

		clock.Advance(TimeSpan.FromSeconds(60));
		system.ServiceRepaintSchedules();

		Assert.Equal(1, fired);
	}

	[Fact]
	public void Cancelling_stops_it()
	{
		var clock = new TestClock();
		var system = Headless(clock);
		int fired = 0;

		var handle = system.ScheduleRepaint(TimeSpan.FromMilliseconds(100), () => fired++);
		handle.Dispose();

		clock.Advance(TimeSpan.FromMilliseconds(500));
		system.ServiceRepaintSchedules();

		Assert.Equal(0, fired);
	}

	[Fact]
	public void A_clock_element_repaints_once_it_is_attached()
	{
		// The real thing: the element reaches the system through its panel, so a
		// schedule asked for in the constructor would have nothing to attach to
		// and would be dropped without a sound.
		var clock = new TestClock();
		var driver = new HeadlessConsoleDriver(60, 12);

		var system = new ConsoleWindowSystem(driver,
			ConsoleWindowSystemOptions.Create() with
			{
				Time = new TimeOptions { All = clock },
				TopPanelConfig = panel => panel.Left(SharpConsoleUI.Panel.Elements.Clock()),
			});

		system.Render.UpdateDisplay();

		// Nothing is due yet.
		Assert.False(system.ServiceRepaintSchedules());

		clock.Advance(TimeSpan.FromSeconds(2));

		Assert.True(system.ServiceRepaintSchedules(),
			"an attached clock element should have a repaint due after two seconds");
	}

	[Fact]
	public void The_callback_runs_on_the_thread_that_services_it()
	{
		// The point of the change: a schedule is serviced by the UI loop, so a
		// callback may touch control state directly. A pool timer could not.
		var clock = new TestClock();
		var system = Headless(clock);
		int? ranOn = null;

		system.ScheduleRepaint(TimeSpan.FromMilliseconds(10),
			() => ranOn = Environment.CurrentManagedThreadId);

		clock.Advance(TimeSpan.FromMilliseconds(20));
		system.ServiceRepaintSchedules();

		Assert.Equal(Environment.CurrentManagedThreadId, ranOn);
	}
}

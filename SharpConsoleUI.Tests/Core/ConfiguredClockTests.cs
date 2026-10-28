// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// The configured clock is the one the system runs on.
/// </summary>
/// <remarks>
/// Two places read the machine clock directly and so ignored whatever
/// TimeProvider an application supplied: the animation delta in the main loop,
/// and the time a panel clock displays. Both made a configured clock a lie -
/// one silently, one by half-honouring it, which is worse: the element stopped
/// refreshing but jumped to real time whenever something else repainted it.
/// </remarks>
public class ConfiguredClockTests
{
	/// <summary>A clock that sits still until the test moves it.</summary>
	private sealed class FrozenClock : TimeProvider
	{
		private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

		public override DateTimeOffset GetUtcNow() => _now;

		public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

		public void Advance(TimeSpan by) => _now = _now.Add(by);
	}

	private sealed class Recorder : IAnimation
	{
		public TimeSpan Total { get; private set; }

		public bool IsComplete => false;

		public void Update(TimeSpan deltaTime) => Total += deltaTime;

		public void Cancel() { }
	}

	[Fact]
	public void A_frozen_clock_freezes_the_animation_delta()
	{
		var clock = new FrozenClock();
		var driver = new HeadlessConsoleDriver(40, 10);

		var system = new ConsoleWindowSystem(driver,
			ConsoleWindowSystemOptions.Create() with { Time = new TimeOptions { All = clock } });

		var recorder = new Recorder();
		system.Animations.Add(recorder);

		using var session = system.RunInBackground();

		// Real time passes; the configured clock does not move.
		System.Threading.Thread.Sleep(250);

		Assert.Equal(TimeSpan.Zero, recorder.Total);
	}

	[Fact]
	public void Advancing_the_clock_advances_animations()
	{
		var clock = new FrozenClock();
		var driver = new HeadlessConsoleDriver(40, 10);

		var system = new ConsoleWindowSystem(driver,
			ConsoleWindowSystemOptions.Create() with { Time = new TimeOptions { All = clock } });

		var recorder = new Recorder();
		system.Animations.Add(recorder);

		using var session = system.RunInBackground();

		// Poll rather than sleep a guessed amount: under a loaded test run the loop
		// can take longer to reach its first tick than any fixed wait, and this test
		// is about the clock, not about how busy the machine is.
		clock.Advance(TimeSpan.FromMilliseconds(20));

		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

		while (recorder.Total == TimeSpan.Zero && DateTime.UtcNow < deadline)
		{
			clock.Advance(TimeSpan.FromMilliseconds(20));
			System.Threading.Thread.Sleep(10);
		}

		Assert.True(recorder.Total > TimeSpan.Zero,
			"moving the configured clock should advance animations");
	}

	[Fact]
	public void A_panel_clock_shows_the_configured_time()
	{
		var clock = new FrozenClock();
		var driver = new HeadlessConsoleDriver(60, 10);

		var system = new ConsoleWindowSystem(driver,
			ConsoleWindowSystemOptions.Create() with
			{
				Time = new TimeOptions { All = clock },
				TopPanelConfig = panel => panel.Right(SharpConsoleUI.Panel.Elements.Clock()),
			});

		system.Render.UpdateDisplay();

		string screen = string.Join("\n", driver.CaptureComposed()!.ToLines());

		// The frozen clock says 12:00 UTC, whatever the machine says.
		Assert.Contains("12:00", screen);
	}
}

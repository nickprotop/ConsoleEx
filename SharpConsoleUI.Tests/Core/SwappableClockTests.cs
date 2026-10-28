// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// Changing a running application's clock.
/// </summary>
/// <remarks>
/// Consumers capture the provider they are handed - the gesture recognizer holds
/// a closure over it - so a swap has to happen behind them. These check it does,
/// through the part that actually reads the clock rather than through the
/// property.
/// </remarks>
public class SwappableClockTests
{
	private static ConsoleWindowSystem Headless() =>
		new(new HeadlessConsoleDriver(40, 10));

	[Fact]
	public void A_fresh_system_is_on_the_real_clock()
	{
		var system = Headless();

		foreach (string name in ConsoleWindowSystem.ClockNames)
			Assert.True(system.ClockFor(name).IsSystem, $"{name} should start on the real clock");
	}

	[Fact]
	public void Swapping_changes_what_the_clock_reports()
	{
		var system = Headless();
		var frozen = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

		var previous = system.ClockFor("mouse").Swap(frozen);

		Assert.Same(TimeProvider.System, previous);
		Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
			system.MouseTime.GetUtcNow());
	}

	[Fact]
	public void An_unknown_name_is_refused()
	{
		var system = Headless();

		var thrown = Assert.Throws<ArgumentException>(() => system.ClockFor("wristwatch"));

		Assert.Contains("mouse, repaint, animation", thrown.Message);
	}

	[Fact]
	public void A_swap_reaches_a_consumer_that_already_captured_the_clock()
	{
		// The real point. InputCoordinator builds the recognizer in the system's
		// constructor, so by the time anything swaps, the closure is long made.
		var system = Headless();
		var frozen = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

		system.ClockFor("mouse").Swap(frozen);

		EventHandler<ConsoleKeyInfo> keys = (_, k) => system.InputStateService.EnqueueKey(k);
		system.Input.RegisterEventHandlers(keys);
		system.Input.RecordEvents = true;

		var driver = (HeadlessConsoleDriver)system.ConsoleDriver;
		var point = new Point(5, 5);

		driver.SimulateClick(point);
		frozen.Advance(TimeSpan.FromMilliseconds(80));   // inside the multi-click window
		driver.SimulateClick(point);

		string tape = string.Join(" | ",
			System.Linq.Enumerable.Select(system.Input.ConsumeEventTape(), e => e.Flags));

		Assert.Contains("DoubleClicked", tape);
	}

	[Fact]
	public void A_clock_may_not_run_backwards()
	{
		var frozen = new ManualTimeProvider(DateTimeOffset.UtcNow);

		Assert.Throws<ArgumentOutOfRangeException>(
			() => frozen.Advance(TimeSpan.FromSeconds(-1)));
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Drawing;
using System.Linq;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Input;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// Telling one double click from two slow clicks without sleeping.
/// </summary>
/// <remarks>
/// The recognizer decides by comparing wall-clock times, so a test that wanted
/// two separate clicks had to wait out the real multi-click window. With a
/// TimeProvider the test moves the clock instead, and runs in microseconds.
/// </remarks>
public class GestureClockTests
{
	[Fact]
	public void Two_clicks_inside_the_window_are_a_double_click()
	{
		var time = new TestTimeProvider();
		var recognizer = new MouseGestureRecognizer(time: time);
		var point = new Point(3, 4);

		// A terminal sends press then release for each click, and the recognizer
		// derives the double on the SECOND PRESS - which is the whole reason a
		// control must test for DoubleClicked before Pressed.
		recognizer.Recognize([MouseFlags.Button1Pressed], point);
		recognizer.Recognize([MouseFlags.Button1Released], point);

		time.Advance(TimeSpan.FromMilliseconds(100));

		var secondPress = recognizer.Recognize([MouseFlags.Button1Pressed], point);

		Assert.Contains(MouseFlags.Button1DoubleClicked, secondPress);
	}

	[Fact]
	public void Two_clicks_past_the_window_stay_separate()
	{
		var time = new TestTimeProvider();
		var recognizer = new MouseGestureRecognizer(time: time);
		var point = new Point(3, 4);

		recognizer.Recognize([MouseFlags.Button1Pressed], point);
		recognizer.Recognize([MouseFlags.Button1Released], point);

		time.Advance(TimeSpan.FromSeconds(5));

		var secondPress = recognizer.Recognize([MouseFlags.Button1Pressed], point);

		Assert.DoesNotContain(MouseFlags.Button1DoubleClicked, secondPress);
	}

	[Fact]
	public void The_clock_reaches_the_recognizer_through_the_system()
	{
		// The real path: options -> ConsoleWindowSystem -> InputCoordinator ->
		// MouseGestureRecognizer. The unit tests above prove the recognizer honours
		// a clock; this proves an application can actually supply one, which is the
		// part that was unreachable before.
		var time = new TestTimeProvider();
		var driver = new HeadlessConsoleDriver(40, 10);

		var system = new ConsoleWindowSystem(driver,
			SharpConsoleUI.Configuration.ConsoleWindowSystemOptions.Create() with
			{
				PipedInput = new SharpConsoleUI.Configuration.PipedInputOptions(Enabled: false),
				TimeProvider = time
			});

		Assert.Same(time, system.Time);
	}

	[Fact]
	public void A_press_release_pair_yields_Button1Clicked()
	{
		// ButtonControl fires on the DERIVED Button1Clicked, not on a raw release,
		// so a driver-level click helper only works if the recognizer adds it.
		var recognizer = new MouseGestureRecognizer(time: new TestTimeProvider());
		var point = new Point(3, 4);

		recognizer.Recognize([MouseFlags.Button1Pressed], point);
		var release = recognizer.Recognize([MouseFlags.Button1Released], point);

		Assert.Contains(MouseFlags.Button1Clicked, release);
	}

	[Fact]
	public void The_systems_clock_decides_what_the_real_input_path_calls_a_double_click()
	{
		// Not the recognizer in isolation: a driver event going through
		// InputCoordinator, which is the path a real terminal takes. The only
		// thing that varies between the two halves is the clock.
		var time = new TestTimeProvider();
		var driver = new HeadlessConsoleDriver(40, 10);

		var system = new ConsoleWindowSystem(driver,
			SharpConsoleUI.Configuration.ConsoleWindowSystemOptions.Create() with
			{
				PipedInput = new SharpConsoleUI.Configuration.PipedInputOptions(Enabled: false),
				TimeProvider = time
			});

		// The tape records the flags AFTER the recognizer has had them, which is
		// what the controls see. Same thing cxdbg's `events` verb reports.
		// Handlers are registered inside Run(); a test that never runs must do it
		// itself, or SimulateClick fires into nothing.
		EventHandler<ConsoleKeyInfo> keys = (_, k) => system.InputStateService.EnqueueKey(k);
		system.Input.RegisterEventHandlers(keys);
		system.Input.RecordEvents = true;

		var point = new Point(5, 5);

		driver.SimulateClick(point);
		time.Advance(TimeSpan.FromMilliseconds(80));     // inside the window
		driver.SimulateClick(point);

		string near = string.Join(" | ", system.Input.ConsumeEventTape().Select(e => e.Flags));
		// Derived on the SECOND PRESS and carried alongside it, which is why a
		// control must test for the double before the press.
		Assert.Contains("Button1Pressed|Button1DoubleClicked", near);

		driver.SimulateClick(point);
		time.Advance(TimeSpan.FromSeconds(5));           // far outside it
		driver.SimulateClick(point);

		string far = string.Join(" | ", system.Input.ConsumeEventTape().Select(e => e.Flags));
		Assert.DoesNotContain("DoubleClicked", far);
	}

	/// <summary>A clock that only moves when the test says so.</summary>
	private sealed class TestTimeProvider : TimeProvider
	{
		private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

		public override DateTimeOffset GetUtcNow() => _now;

		public void Advance(TimeSpan by) => _now = _now.Add(by);
	}
}

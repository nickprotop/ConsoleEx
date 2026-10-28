// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Input;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// One place decides what a click is.
/// </summary>
/// <remarks>
/// NetConsoleDriver used to derive Clicked, DoubleClicked and TripleClicked
/// itself, with its own clock and its own threshold, while
/// MouseGestureRecognizer derived them again downstream. Two opinions on one
/// question is the shape of issue #92, where a control and the recognizer both
/// decided and a double click fired twice. The driver reports facts now.
/// </remarks>
public class DriverGestureOwnershipTests
{
	/// <summary>A clock the test moves rather than waits out.</summary>
	private sealed class TestClock : TimeProvider
	{
		private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

		public override DateTimeOffset GetUtcNow() => _now;

		public void Advance(TimeSpan by) => _now = _now.Add(by);
	}

	[Fact]
	public void The_recognizer_still_derives_a_click_from_a_press_and_a_release()
	{
		var recognizer = new MouseGestureRecognizer(time: new TestClock());
		var point = new Point(10, 5);

		recognizer.Recognize([MouseFlags.Button1Pressed], point);
		var release = recognizer.Recognize([MouseFlags.Button1Released], point);

		Assert.Contains(MouseFlags.Button1Clicked, release);
	}

	[Fact]
	public void And_a_double_click_from_two_of_them()
	{
		var clock = new TestClock();
		var recognizer = new MouseGestureRecognizer(time: clock);
		var point = new Point(10, 5);

		recognizer.Recognize([MouseFlags.Button1Pressed], point);
		recognizer.Recognize([MouseFlags.Button1Released], point);

		clock.Advance(TimeSpan.FromMilliseconds(80));

		var second = recognizer.Recognize([MouseFlags.Button1Pressed], point);

		Assert.Contains(MouseFlags.Button1DoubleClicked, second);
	}

	[Fact]
	public void A_release_whose_press_was_never_seen_is_not_a_click()
	{
		// What the driver's own 50 ms duplicate filter was guarding against. The
		// recognizer discards it on the press it never saw, with no timing at all,
		// so the filter was a second guard on a handled case.
		var recognizer = new MouseGestureRecognizer(time: new TestClock());
		var point = new Point(10, 5);

		var orphan = recognizer.Recognize([MouseFlags.Button1Released], point);

		Assert.DoesNotContain(MouseFlags.Button1Clicked, orphan);
	}

	[Fact]
	public void A_duplicate_release_does_not_click_twice()
	{
		var recognizer = new MouseGestureRecognizer(time: new TestClock());
		var point = new Point(10, 5);

		recognizer.Recognize([MouseFlags.Button1Pressed], point);

		var first = recognizer.Recognize([MouseFlags.Button1Released], point);
		var duplicate = recognizer.Recognize([MouseFlags.Button1Released], point);

		Assert.Contains(MouseFlags.Button1Clicked, first);
		Assert.DoesNotContain(MouseFlags.Button1Clicked, duplicate);
	}
}

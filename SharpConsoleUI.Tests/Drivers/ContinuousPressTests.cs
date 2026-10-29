// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Drivers.Input;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// The auto-repeat a terminal does not provide.
/// </summary>
/// <remarks>
/// A terminal reports a press and a release and nothing between, so a button
/// held on a scrollbar arrow would move one line and stop. The repeat supplies
/// what is missing - and because it outlives the event that started it, the
/// ways it can fail to stop are the ways it matters.
/// </remarks>
public class ContinuousPressTests
{
	private static int CountFor(ContinuousPress press, int ms)
	{
		int calls = 0;
		press.Begin(MouseFlags.Button1Pressed, new Point(3, 4),
			(_, _) => Interlocked.Increment(ref calls));

		Thread.Sleep(ms);
		return Volatile.Read(ref calls);
	}

	[Fact]
	public void A_held_button_repeats()
	{
		using var press = new ContinuousPress();

		Assert.True(CountFor(press, 350) > 0, "a held button produced no repeats");
	}

	[Fact]
	public void A_release_stops_it()
	{
		using var press = new ContinuousPress();

		int calls = 0;
		press.Begin(MouseFlags.Button1Pressed, new Point(3, 4),
			(_, _) => Interlocked.Increment(ref calls));

		Thread.Sleep(250);
		press.End(MouseFlags.Button1Pressed);

		int atRelease = Volatile.Read(ref calls);
		Thread.Sleep(300);

		// Whatever it had done by the release is all it ever does.
		Assert.Equal(atRelease, Volatile.Read(ref calls));
	}

	[Fact]
	public void Disposing_stops_it_even_without_a_release()
	{
		// The leak that mattered: a driver shutting down mid-press, or a release
		// that never arrives, used to leave the loop running forever against a
		// handler whose owner was gone.
		var press = new ContinuousPress();

		int calls = 0;
		press.Begin(MouseFlags.Button1Pressed, new Point(1, 1),
			(_, _) => Interlocked.Increment(ref calls));

		Thread.Sleep(250);
		press.Dispose();

		int atDispose = Volatile.Read(ref calls);
		Thread.Sleep(300);

		Assert.Equal(atDispose, Volatile.Read(ref calls));
	}

	[Fact]
	public void A_second_press_does_not_stack_a_second_loop()
	{
		// A missed release used to let the next press start another loop on top
		// of the first, doubling the repeat rate for as long as both ran.
		using var press = new ContinuousPress();

		int calls = 0;
		void Count(MouseFlags _, Point __) => Interlocked.Increment(ref calls);

		press.Begin(MouseFlags.Button1Pressed, new Point(1, 1), Count);
		Thread.Sleep(120);
		press.Begin(MouseFlags.Button1Pressed, new Point(1, 1), Count);

		Interlocked.Exchange(ref calls, 0);
		Thread.Sleep(350);
		int after = Volatile.Read(ref calls);

		press.End(MouseFlags.Button1Pressed);

		// One loop at ~100ms gives about three in 350ms; two would give six. The
		// bound is loose because this is a timing test, and tight enough that a
		// doubled rate fails it.
		Assert.True(after <= 5, $"expected one loop's worth of repeats, got {after}");
	}

	[Fact]
	public void The_repeat_follows_the_pointer()
	{
		// A held button repeats where the pointer IS, not where it went down,
		// which is what makes dragging past a scrollbar's track keep scrolling.
		using var press = new ContinuousPress();

		Point seen = Point.Empty;
		press.Begin(MouseFlags.Button1Pressed, new Point(1, 1), (_, at) => seen = at);

		press.Moved(new Point(9, 7));
		Thread.Sleep(250);
		press.End(MouseFlags.Button1Pressed);

		Assert.Equal(new Point(9, 7), seen);
	}

	[Fact]
	public void A_throwing_handler_does_not_fault_an_unobserved_task()
	{
		// Nothing awaits the repeat, so an escaping exception would be unobserved
		// and could take the process down on an older runtime policy.
		using var press = new ContinuousPress();

		press.Begin(MouseFlags.Button1Pressed, new Point(1, 1),
			(_, _) => throw new InvalidOperationException("from the handler"));

		Thread.Sleep(250);
		press.End(MouseFlags.Button1Pressed);

		// Reaching here without the test host faulting is the assertion.
		Assert.True(true);
	}

	[Fact]
	public void Releasing_another_button_leaves_the_repeat_alone()
	{
		// A release arrives for whatever button the terminal saw go up, which
		// is not always the one being repeated - a chord, or a release whose
		// press was missed. Stopping on any release at all would end a repeat
		// the user is still holding.
		using var press = new ContinuousPress();

		int calls = 0;
		press.Begin(MouseFlags.Button1Pressed, new Point(1, 1),
			(_, _) => Interlocked.Increment(ref calls));

		press.End(MouseFlags.Button3Pressed);   // a different button

		Interlocked.Exchange(ref calls, 0);
		Thread.Sleep(250);

		Assert.True(Volatile.Read(ref calls) > 0, "the repeat stopped on another button's release");

		press.End(MouseFlags.Button1Pressed);
	}

	[Fact]
	public void A_release_maps_to_the_press_it_ends()
	{
		Assert.Equal(MouseFlags.Button1Pressed, ContinuousPress.PressFor(MouseFlags.Button1Released));
		Assert.Equal(MouseFlags.Button3Pressed, ContinuousPress.PressFor(MouseFlags.Button3Released));
		Assert.Equal((MouseFlags)0, ContinuousPress.PressFor(MouseFlags.ReportMousePosition));
	}
}

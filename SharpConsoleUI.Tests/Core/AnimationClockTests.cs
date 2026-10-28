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
/// Animations advance by wall time, however often the loop ticks.
/// </summary>
/// <remarks>
/// The main loop measured elapsed time from the last RENDER but advanced
/// animations on every TICK. A tick that did not render left the timestamp
/// alone, so the next tick counted that span again - and a window being
/// dragged produces many such ticks, which is why an animated desktop
/// background visibly sped up while the mouse moved.
/// </remarks>
public class AnimationClockTests
{
	/// <summary>An animation that only records how far it was advanced.</summary>
	private sealed class Recorder : IAnimation
	{
		public TimeSpan Total { get; private set; }

		public bool IsComplete => false;

		public int Calls { get; private set; }

		public void Update(TimeSpan deltaTime)
		{
			Total += deltaTime;
			Calls++;
		}

		public void Cancel() { }
	}

	[Fact]
	public void Animations_advance_by_time_since_they_last_advanced()
	{
		// The loop ticks far more often than it renders, and animations advance on
		// EVERY tick. Told "time since the last RENDER" instead, a tick that does
		// not render re-counts a span already counted - and the more such ticks,
		// the faster animations run. Dragging a window is exactly that case, which
		// is where an animated desktop background was seen speeding up.
		//
		// Driven through TickCore directly: the background loop sleeps when idle,
		// so it renders on nearly every tick and the fault barely shows. Ticking by
		// hand is what produces the non-rendering ticks that expose it.
		var driver = new HeadlessConsoleDriver(40, 10);
		var system = new ConsoleWindowSystem(driver);

		var recorder = new Recorder();
		system.Animations.Add(recorder);

		using var session = system.RunInBackground();
		System.Threading.Thread.Sleep(50);

		var baseline = recorder.Total;
		int callsBefore = recorder.Calls;
		var started = DateTime.UtcNow;

		// Tick hard, so most ticks fall inside the frame interval and do not render.
		for (int i = 0; i < 200; i++) system.TickCore();

		var real = DateTime.UtcNow - started;
		var advanced = recorder.Total - baseline;
		int calls = recorder.Calls - callsBefore;

		Assert.True(calls > 20, $"expected many ticks; got {calls}");

		// The sum of the advances cannot exceed the time that actually passed.
		// Double counting makes it a multiple of it.
		Assert.True(advanced <= real + TimeSpan.FromMilliseconds(5),
			$"animations advanced {advanced.TotalMilliseconds:F0}ms over " +
			$"{real.TotalMilliseconds:F0}ms of real time, across {calls} ticks - " +
			"a tick that did not render is counting its time twice");
	}
}

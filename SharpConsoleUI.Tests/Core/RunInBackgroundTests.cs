// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// <see cref="ConsoleWindowSystem.RunInBackground"/> - the system owns the loop, the caller
/// keeps its thread. What a host needs when something else owns the main thread.
/// </summary>
public class RunInBackgroundTests
{
	private static ConsoleWindowSystem Build(int width = 60, int height = 16)
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(width, height),
			ConsoleWindowSystemOptions.Create());

		new WindowBuilder(system)
			.WithTitle("Background")
			.WithSize(30, 6)
			.Centered()
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("hello").Build())
			.BuildAndShow();

		return system;
	}

	[Fact]
	public void Returns_OnceAFrameHasBeenRendered()
	{
		// The contract that matters: a caller may read the screen or connect to the socket
		// the instant this returns, without polling for readiness.
		var system = Build();
		var driver = (HeadlessConsoleDriver)system.ConsoleDriver;

		using var session = system.RunInBackground();

		Assert.True(driver.FrameNumber > 0,
			$"returned before anything was painted (frame={driver.FrameNumber}, running={session.IsRunning}, fault={session.Fault?.Message ?? "none"})");
		Assert.True(session.IsRunning);
	}

	[Fact]
	public void KeepsRunning_AndRepaintsWhenSomethingChanges()
	{
		// An idle loop does NOT keep painting - the renderer skips an unchanged screen, which
		// is the whole point of the diff engine. So prove liveness by changing something and
		// watching the frame counter move, not by waiting for frames that should not happen.
		var system = Build();
		var driver = (HeadlessConsoleDriver)system.ConsoleDriver;

		using var session = system.RunInBackground();
		long first = driver.FrameNumber;

		var window = system.Windows.Values.First();
		system.EnqueueOnUIThread(() => window.Title = "Changed");

		long deadline = Environment.TickCount64 + 3000;
		while (driver.FrameNumber <= first && Environment.TickCount64 < deadline)
			Thread.Sleep(20);

		Assert.True(driver.FrameNumber > first,
			$"the loop did not repaint after a change: {first} -> {driver.FrameNumber}");
		Assert.True(session.IsRunning);
	}

	[Fact]
	public void DoesNotBlockTheCaller()
	{
		var system = Build();
		var start = DateTime.UtcNow;

		using var session = system.RunInBackground();

		// Returning at all is the assertion; the bound just catches a hang.
		Assert.True(DateTime.UtcNow - start < TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void Dispose_StopsTheLoop()
	{
		var system = Build();
		var driver = (HeadlessConsoleDriver)system.ConsoleDriver;

		var session = system.RunInBackground();
		session.Dispose();

		long after = driver.FrameNumber;
		Thread.Sleep(200);

		Assert.Equal(after, driver.FrameNumber);
		Assert.False(session.IsRunning);
	}

	[Fact]
	public void Dispose_IsIdempotent()
	{
		var system = Build();
		var session = system.RunInBackground();

		session.Dispose();
		session.Dispose();   // must not throw or hang
	}

	[Fact]
	public void Throws_WhenAlreadyRunning()
	{
		var system = Build();
		using var session = system.RunInBackground();

		Assert.Throws<InvalidOperationException>(() => system.RunInBackground());
	}

	[Fact]
	public void Throws_ForADriverThatCannotBeLooped()
	{
		// A single-threaded host has no second thread to give the loop, so this must refuse
		// for the same reason Run does rather than deadlocking.
		var system = new ConsoleWindowSystem(new NoLoopDriver(), ConsoleWindowSystemOptions.Create());

		var ex = Assert.Throws<InvalidOperationException>(() => system.RunInBackground());
		Assert.Contains("BeginHosted", ex.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Fault_IsNullWhileHealthy()
	{
		var system = Build();
		using var session = system.RunInBackground();

		Thread.Sleep(150);
		Assert.Null(session.Fault);
	}

	/// <summary>A driver that reports it cannot be driven by a loop on another thread.</summary>
	/// <remarks>
	/// Implements the interface explicitly: <c>SupportsBlockingLoop</c> is a default interface
	/// member, and a plain property on a subclass would shadow it rather than override it -
	/// the system reads it through <c>IConsoleDriver</c> and would still see <c>true</c>.
	/// </remarks>
	private sealed class NoLoopDriver : HeadlessConsoleDriver, IConsoleDriver
	{
		bool IConsoleDriver.SupportsBlockingLoop => false;
	}
}

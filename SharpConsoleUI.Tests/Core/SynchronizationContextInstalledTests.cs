// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Threading;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

[Collection("TimingSensitive")]
public class SynchronizationContextInstalledTests
{
	private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (DateTime.UtcNow < deadline) { if (condition()) return true; Thread.Sleep(25); }
		return condition();
	}

	[Fact]
	public void False_BeforeRun()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer);
		Assert.False(sys.SynchronizationContextInstalled);
	}

	[Fact]
	public void True_DuringRun_WhenOptedIn_AndFalse_AfterShutdown()
	{
		var opts = new ConsoleWindowSystemOptions() with { InstallSynchronizationContext = true };
		var sys = new ConsoleWindowSystem(RenderMode.Buffer, options: opts);

		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();

		var becameTrue = WaitFor(() => sys.SynchronizationContextInstalled, timeoutMs: 3000);
		sys.Shutdown(0);
		t.Join(2000);

		Assert.True(becameTrue, "SynchronizationContextInstalled never became true during Run()");
		Assert.False(sys.SynchronizationContextInstalled, "Should reset to false after Run() returns");
	}

	[Fact]
	public void False_DuringRun_WhenNotOptedIn()
	{
		var opts = new ConsoleWindowSystemOptions() with { InstallSynchronizationContext = false };
		var sys = new ConsoleWindowSystem(RenderMode.Buffer, options: opts);

		bool? observedDuringRun = null;
		var t = new Thread(() => { try { sys.Run(); } catch { } }) { IsBackground = true };
		t.Start();

		// Give the loop a moment to start, then observe the resolved state from a queued action.
		sys.EnqueueOnUIThread(() => observedDuringRun = sys.SynchronizationContextInstalled);
		WaitFor(() => observedDuringRun.HasValue, timeoutMs: 3000);

		sys.Shutdown(0);
		t.Join(2000);

		Assert.True(observedDuringRun.HasValue, "Queued observation never ran");
		Assert.False(observedDuringRun!.Value);
	}
}

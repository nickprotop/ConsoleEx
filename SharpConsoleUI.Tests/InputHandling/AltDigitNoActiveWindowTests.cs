// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.InputHandling;

/// <summary>
/// Covers Alt+1..9 window selection when no window is active.
/// </summary>
/// <remarks>
/// Alt+digit <b>selects</b> a window, so it is a system-level shortcut like the window-cycle key —
/// it does not act on the active window, it chooses one. But it was handled inside the
/// <c>else if (_context.ActiveWindow != null)</c> branch that routes keys TO the active window, so
/// with nothing active the chord was dropped: it worked once any window had been activated and did
/// nothing before that.
///
/// <para><c>HandleAltInput</c> itself never reads <c>ActiveWindow</c> — it enumerates
/// <c>Windows</c> and calls <c>SetActiveWindow</c> — so the handler was always capable of running
/// with none active; it simply was not reached.</para>
///
/// <para>Reported from the browser spike, where the launcher starts with no activated window.</para>
/// </remarks>
public class AltDigitNoActiveWindowTests
{
	private static ConsoleWindowSystem SystemWithWindows(int count, out Window[] windows)
	{
		var system = new ConsoleWindowSystem(new MockConsoleDriver(100, 30));
		windows = new Window[count];

		for (int i = 0; i < count; i++)
		{
			var w = new Window(system) { Title = $"W{i + 1}", Left = 1, Top = 1, Width = 30, Height = 8 };
			system.WindowStateService.AddWindow(w);
			windows[i] = w;
		}

		return system;
	}

	/// <summary>
	/// Reaches the genuine "nothing active" state, the way a user does.
	/// </summary>
	/// <remarks>
	/// <c>SetActiveWindow(null)</c> is a no-op by design, and the first window added always becomes
	/// active, so "nothing active" cannot be forced directly. The reachable path is the one that
	/// matters here: <c>FindNextActiveWindow</c> skips minimized windows, so minimizing every window
	/// and closing the active one leaves windows present with none active.
	///
	/// <para>That is not contrived — Alt+digit is the shortcut for restoring a minimized window, and
	/// <c>HandleAltInput</c> un-minimizes the one it selects. Minimize-everything is exactly when the
	/// user reaches for it, and exactly when it did nothing.</para>
	/// </remarks>
	private static ConsoleWindowSystem SystemWithNothingActive(int count, out Window[] windows)
	{
		var system = new ConsoleWindowSystem(new MockConsoleDriver(100, 30));

		windows = new Window[count];
		for (int i = 0; i < count; i++)
		{
			var w = new Window(system) { Title = $"W{i + 1}", Left = 1, Top = 1, Width = 30, Height = 8 };
			system.WindowStateService.AddWindow(w);
			windows[i] = w;
		}

		// A throwaway window takes the active slot, then closes. With every other window minimized,
		// FindNextActiveWindow has nothing to promote.
		var seed = new Window(system) { Title = "seed", Left = 1, Top = 1, Width = 20, Height = 5 };
		system.WindowStateService.AddWindow(seed);
		system.WindowStateService.SetActiveWindow(seed);

		foreach (var w in windows)
			w.State = WindowState.Minimized;

		system.WindowStateService.UnregisterWindow(seed);
		return system;
	}

	private static void PressAltDigit(ConsoleWindowSystem system, int digit)
	{
		char c = (char)('0' + digit);
		system.InputStateService.EnqueueKey(new ConsoleKeyInfo(c, (ConsoleKey)c, false, alt: true, false));
		system.Input.ProcessInput();
	}

	/// <summary>The regression: with nothing active, Alt+1 must still select the first window.</summary>
	[Fact]
	public void AltDigit_SelectsWindow_WhenNoWindowIsActive()
	{
		var system = SystemWithNothingActive(3, out var windows);

		// Precondition, asserted rather than assumed: these tests are vacuous if something is active.
		Assert.True(system.ActiveWindow == null,
			$"expected nothing active, but got '{system.ActiveWindow?.Title}'");

		PressAltDigit(system, 1);

		Assert.Same(windows[0], system.ActiveWindow);
	}

	[Fact]
	public void AltDigit_SelectsTheNthWindow_WhenNoWindowIsActive()
	{
		var system = SystemWithNothingActive(3, out var windows);

		PressAltDigit(system, 3);

		Assert.Same(windows[2], system.ActiveWindow);
	}

	/// <summary>The behaviour that already worked must keep working.</summary>
	[Fact]
	public void AltDigit_StillSelectsWindow_WhenOneIsAlreadyActive()
	{
		var system = SystemWithWindows(3, out var windows);
		system.WindowStateService.SetActiveWindow(windows[0]);

		PressAltDigit(system, 2);

		Assert.Same(windows[1], system.ActiveWindow);
	}

	/// <summary>A digit past the end of the list selects nothing and must not throw.</summary>
	[Fact]
	public void AltDigit_OutOfRange_IsIgnored_WhenNoWindowIsActive()
	{
		var system = SystemWithNothingActive(2, out _);
		var before = system.ActiveWindow;

		var ex = Record.Exception(() => PressAltDigit(system, 9));

		Assert.Null(ex);
		Assert.Same(before, system.ActiveWindow);   // unchanged
	}
}

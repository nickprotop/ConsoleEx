// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Input;

/// <summary>
/// Alt+1..9 activates a top-level window by index, and an application can now decline it.
/// <para>
/// Before <c>AltDigitSelectsWindow</c> there was no way to switch this off: an application wanting
/// Alt+digit for its own panes had to register all nine digits as global shortcuts — including the
/// ones out of range — purely to stop the key falling through to the built-in selector.
/// <c>Movable(false)</c> looked like it should help and did not, because window movability has
/// nothing to do with switching between windows.
/// </para>
/// </summary>
public class AltDigitWindowSelectTests
{
	private static (ConsoleWindowSystem system, Window first, Window second) TwoWindows(bool altDigitSelects)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(o => o with { AltDigitSelectsWindow = altDigitSelects });

		var first = new Window(system) { Title = "first", Left = 0, Top = 0, Width = 20, Height = 6 };
		var second = new Window(system) { Title = "second", Left = 22, Top = 0, Width = 20, Height = 6 };
		system.AddWindow(first);
		system.AddWindow(second);
		system.SetActiveWindow(first);

		return (system, first, second);
	}

	private static void PressAltDigit(ConsoleWindowSystem system, char digit)
	{
		// Alt+digit as the input layer sees it: the digit character with the Alt modifier set.
		system.InputStateService.EnqueueKey(new ConsoleKeyInfo(digit, ConsoleKey.D0 + (digit - '0'), false, true, false));
		system.Input.ProcessInput();
	}

	[Fact]
	public void ByDefault_AltDigit_ActivatesTheWindowAtThatIndex()
	{
		var (system, first, second) = TwoWindows(altDigitSelects: true);
		Assert.Same(first, system.ActiveWindow);

		PressAltDigit(system, '2');

		Assert.Same(second, system.ActiveWindow);
		GC.KeepAlive(system);
	}

	[Fact]
	public void WhenDisabled_AltDigit_LeavesTheActiveWindowAlone()
	{
		var (system, first, _) = TwoWindows(altDigitSelects: false);

		PressAltDigit(system, '2');

		Assert.Same(first, system.ActiveWindow);
		GC.KeepAlive(system);
	}

	[Fact]
	public void WhenDisabled_TheDefaultIsUnchangedForEveryoneElse()
	{
		// The option must default to the historical behaviour: an application that upgrades and
		// changes nothing keeps Alt+digit window switching.
		Assert.True(new SharpConsoleUI.Configuration.ConsoleWindowSystemOptions().AltDigitSelectsWindow);
	}

	[Fact]
	public void AGlobalShortcut_WinsOverTheBuiltInSelector_WhenEnabled()
	{
		// Global shortcuts are dispatched before the active window and before this fall-through, so an
		// application could already pre-empt the chord that way. That precedence is what the new option
		// saves callers from having to rely on, and it must keep working.
		var (system, first, _) = TwoWindows(altDigitSelects: true);
		bool ran = false;
		system.RegisterGlobalShortcut(ConsoleModifiers.Alt, ConsoleKey.D2, () => ran = true);

		PressAltDigit(system, '2');

		Assert.True(ran, "the application's global shortcut did not run");
		Assert.Same(first, system.ActiveWindow);
		GC.KeepAlive(system);
	}

	[Fact]
	public void AGlobalShortcut_StillRuns_WhenTheSelectorIsDisabled()
	{
		var (system, first, _) = TwoWindows(altDigitSelects: false);
		bool ran = false;
		system.RegisterGlobalShortcut(ConsoleModifiers.Alt, ConsoleKey.D3, () => ran = true);

		PressAltDigit(system, '3');

		Assert.True(ran);
		Assert.Same(first, system.ActiveWindow);
		GC.KeepAlive(system);
	}

	[Fact]
	public void OutOfRangeDigit_ActivatesNothing()
	{
		// Two windows exist; Alt+9 names none of them and must be a no-op rather than a wrap-around.
		var (system, first, _) = TwoWindows(altDigitSelects: true);

		PressAltDigit(system, '9');

		Assert.Same(first, system.ActiveWindow);
		GC.KeepAlive(system);
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Logging;
using Xunit;
using OurLevel = SharpConsoleUI.Logging.LogLevel;

namespace SharpConsoleUI.Tests.Logging;

/// <summary>
/// <see cref="LogViewerControl.GlobalFilterHotkey"/>: '/' opens the table's filter from anywhere
/// on the window, without stealing a typed slash from another control.
/// </summary>
public class LogViewerHotkeyTests
{
	private static ConsoleKeyInfo Slash() => new('/', ConsoleKey.Oem2, false, false, false);

	private static (ConsoleWindowSystem System, Window Window, LogViewerControl Viewer) Build(
		bool hotkey, params IWindowControl[] extraControlsFirst)
	{
		var driver = new HeadlessConsoleDriver(120, 24);
		var system = new ConsoleWindowSystem(driver);
		var window = new Window(system) { Title = "Page", Width = 110, Height = 20 };
		system.AddWindow(window);
		system.SetActiveWindow(window);   // key dispatch routes to the ACTIVE window
		system.LogService.MinimumLevel = OurLevel.Information;

		foreach (var c in extraControlsFirst)
			window.AddControl(c);

		var viewer = new LogViewerControl(system.LogService) { GlobalFilterHotkey = hotkey };
		window.AddControl(viewer);

		// Attach + first frame, so the control reaches its window and hooks the preview.
		var region = new List<Rectangle> { new(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);

		return (system, window, viewer);
	}

	private static void PressSlash(ConsoleWindowSystem system)
	{
		system.InputStateService.EnqueueKey(Slash());
		system.Input.ProcessInput();
		system.DrainUiThreadQueueForTests();
	}

	[Fact]
	public void Off_ByDefault()
	{
		var viewer = new LogViewerControl(new LogService());
		Assert.False(viewer.GlobalFilterHotkey);
	}

	/// <summary>With nothing else focused, '/' opens the filter even though the viewer is not focused.</summary>
	[Fact]
	public void Slash_OpensFilter_WhenViewerIsNotFocused()
	{
		var (system, window, viewer) = Build(hotkey: true);

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		Assert.Null(window.FocusManager.FocusedControl);

		PressSlash(system);

		Assert.True(viewer.IsFiltering);
	}

	/// <summary>A typed slash belongs to a focused text control, not to the log filter.</summary>
	[Fact]
	public void Slash_DeclinesWhileAnotherInteractiveControlHasFocus()
	{
		var edit = new MultilineEditControl { Content = "path", ViewportHeight = 3 };
		var (system, window, viewer) = Build(hotkey: true, edit);

		window.FocusManager.SetFocus(edit, FocusReason.Programmatic);
		Assert.Same(edit, window.FocusManager.FocusedControl);

		PressSlash(system);

		// The viewer declines, so the key travels on to the focused control as usual. Whether
		// the edit box then inserts the character depends on ITS own state (it needs edit mode),
		// which is not what this test is about: the point is that the log filter did not swallow
		// the keystroke on the way.
		Assert.False(viewer.IsFiltering);
	}

	/// <summary>Off means off: the key reaches whoever else wants it.</summary>
	[Fact]
	public void Slash_DoesNothing_WhenHotkeyIsOff()
	{
		var (system, window, viewer) = Build(hotkey: false);

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		PressSlash(system);

		Assert.False(viewer.IsFiltering);
	}

	/// <summary>Only a bare '/' is claimed; a modified one is someone else's shortcut.</summary>
	[Fact]
	public void ModifiedSlash_IsNotClaimed()
	{
		var (system, window, viewer) = Build(hotkey: true);

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		system.InputStateService.EnqueueKey(new ConsoleKeyInfo('/', ConsoleKey.Oem2, false, false, control: true));
		system.Input.ProcessInput();
		system.DrainUiThreadQueueForTests();

		Assert.False(viewer.IsFiltering);
	}

	/// <summary>
	/// Opening the filter is useless if the characters typed next do not reach it. The hotkey
	/// fires without the viewer having focus, so it must TAKE focus on the way in, or the prompt
	/// opens and then swallows nothing while the user types into the void.
	/// </summary>
	[Fact]
	public void Slash_FromUnfocused_LeavesTheFilterTypable()
	{
		var (system, window, viewer) = Build(hotkey: true);
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		PressSlash(system);
		Assert.True(viewer.IsFiltering, "the filter did not open");

		// Type into it the way a user would, through the real input path.
		foreach (var ch in "ERROR")
		{
			system.InputStateService.EnqueueKey(new ConsoleKeyInfo(ch, ConsoleKey.A, false, false, false));
			system.Input.ProcessInput();
		}
		system.DrainUiThreadQueueForTests();

		system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
		system.Input.ProcessInput();
		system.DrainUiThreadQueueForTests();

		Assert.Equal("ERROR", viewer.ActiveFilterText);
	}

	/// <summary>Turning it off after the fact unhooks the preview handler.</summary>
	[Fact]
	public void TurningItOff_StopsClaimingTheKey()
	{
		var (system, window, viewer) = Build(hotkey: true);
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		viewer.GlobalFilterHotkey = false;
		PressSlash(system);

		Assert.False(viewer.IsFiltering);
	}
}

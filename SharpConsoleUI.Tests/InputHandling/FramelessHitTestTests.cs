// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.InputHandling;

/// <summary>
/// Adversarial tests proving a Frameless window has NO interactive frame: HasInteractiveFrame gates
/// the six mouse frame-zone methods so chrome actions never fire on frameless windows.
/// </summary>
public class FramelessHitTestTests
{
	[Fact]
	public void Frameless_HasNoInteractiveFrame()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new Window(sys) { Width = 30, Height = 12, BorderStyle = BorderStyle.Frameless };
		Assert.False(win.HasInteractiveFrame);
	}

	[Fact]
	public void Bordered_HasInteractiveFrame()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new Window(sys) { Width = 30, Height = 12, BorderStyle = BorderStyle.DoubleLine };
		Assert.True(win.HasInteractiveFrame);
	}

	[Fact]
	public void None_StillHasInteractiveFrame()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new Window(sys) { Width = 30, Height = 12, BorderStyle = BorderStyle.None };
		Assert.True(win.HasInteractiveFrame);
	}

	// -------------------------------------------------------------------------------------------
	// Adversarial chrome-leak harness: SimulateMouseEvent drives the real InputCoordinator path.
	// -------------------------------------------------------------------------------------------

	/// <summary>
	/// Injects a full press/click/release at the given absolute screen cell and pumps input.
	/// </summary>
	private static void InjectClick(ConsoleWindowSystem sys, MockConsoleDriver driver, int x, int y)
	{
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(x, y));
		sys.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(x, y));
		sys.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Released }, new Point(x, y));
		sys.Input.ProcessInput();
	}

	/// <summary>
	/// POSITIVE CONTROL: proves the harness genuinely drives the chrome (close-button) path on a
	/// BORDERED window. If this fails, the no-leak tests below would pass vacuously.
	/// </summary>
	[Fact]
	public void Bordered_CloseButton_ClosesWindow_HarnessSanity()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem();
		var win = new Window(sys) { Left = 0, Top = 0, Width = 30, Height = 12, BorderStyle = BorderStyle.DoubleLine, ShowCloseButton = true };
		sys.AddWindow(win);
		sys.SetActiveWindow(win);

		bool closing = false;
		win.OnClosing += (_, _) => closing = true;

		int dy = sys.DesktopUpperLeft.Y;
		// Close button sits at the top-right of a bordered window (Width - 2, title row).
		InjectClick(sys, driver: (MockConsoleDriver)sys.ConsoleDriver, 30 - 2, 0 + dy);

		Assert.True(closing, "bordered close button click must drive the chrome path (harness sanity)");
	}

	/// <summary>
	/// THE KEY NET: clicking ANY former-chrome cell of a frameless window must never close, maximize,
	/// minimize, move, or resize it. Driven through the real InputCoordinator via SimulateMouseEvent.
	/// </summary>
	[Fact]
	public void Frameless_FormerChromeCells_DoNotTriggerChrome()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem();
		var driver = (MockConsoleDriver)sys.ConsoleDriver;
		var win = new Window(sys) { Left = 0, Top = 0, Width = 30, Height = 12, BorderStyle = BorderStyle.Frameless };
		win.AddControl(new MarkupControl(new List<string> { "frameless content area" }));
		sys.AddWindow(win);
		sys.SetActiveWindow(win);

		bool closed = false;
		win.OnClosing += (_, _) => closed = true;
		var startState = win.State;
		var startPos = (win.Left, win.Top);
		var startSize = (win.Width, win.Height);

		int dy = sys.DesktopUpperLeft.Y;
		// Former-chrome cells (window-relative): corners, title row, button columns, edges.
		var cells = new (int x, int y)[]
		{
			(0, 0), (29, 0), (0, 11), (29, 11),   // corners
			(15, 0),                              // title-row middle (bordered drag zone)
			(26, 0), (27, 0), (28, 0),            // close/max/min button columns (Width-4..Width-2)
			(0, 5), (29, 5), (15, 11),            // left/right/bottom edges
		};
		foreach (var (x, y) in cells)
		{
			InjectClick(sys, driver, x, y + dy);

			Assert.False(closed, $"click at ({x},{y}) must not close the frameless window");
			Assert.Equal(startState, win.State);
			Assert.Equal(startPos, (win.Left, win.Top));
			Assert.Equal(startSize, (win.Width, win.Height));
			Assert.False(sys.WindowStateService.IsDragging, $"click at ({x},{y}) must not start a drag");
			Assert.False(sys.WindowStateService.IsResizing, $"click at ({x},{y}) must not start a resize");
		}
	}

	/// <summary>
	/// PROPAGATION: a press/release on the interior of a frameless window reaches the window's
	/// content-propagation path (UnhandledMouseClick) rather than being swallowed by chrome.
	/// </summary>
	[Fact]
	public void Frameless_InteriorClick_ReachesContentPropagation()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem();
		var driver = (MockConsoleDriver)sys.ConsoleDriver;
		var win = new Window(sys) { Left = 0, Top = 0, Width = 30, Height = 12, BorderStyle = BorderStyle.Frameless };
		win.AddControl(new MarkupControl(new List<string> { "frameless content area" }));
		sys.AddWindow(win);
		sys.SetActiveWindow(win);

		bool propagated = false;
		win.UnhandledMouseClick += (_, _) => propagated = true;

		int dy = sys.DesktopUpperLeft.Y;
		// Interior cell that on a BORDERED window would be content but here is a former-chrome-adjacent
		// cell; the click must flow to the window propagation path, proving events reach content.
		InjectClick(sys, driver, 5, 3 + dy);

		Assert.True(propagated, "interior click on a frameless window must reach content propagation, not chrome");
		Assert.Equal(win, sys.WindowStateService.ActiveWindow);
	}

	/// <summary>
	/// A press on edge/corner cells (which START drag/resize on a bordered window) must not move or
	/// resize a frameless window whose content is a plain (non-drag-aware) MarkupControl.
	/// </summary>
	[Fact]
	public void Frameless_NonMouseAwareContent_NoDragOrResizeLeak()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem();
		var driver = (MockConsoleDriver)sys.ConsoleDriver;
		var win = new Window(sys) { Left = 0, Top = 0, Width = 30, Height = 12, BorderStyle = BorderStyle.Frameless };
		win.AddControl(new MarkupControl(new List<string> { "plain content here" }));
		sys.AddWindow(win);
		sys.SetActiveWindow(win);

		var startPos = (win.Left, win.Top);
		var startSize = (win.Width, win.Height);

		int dy = sys.DesktopUpperLeft.Y;
		foreach (var (x, y) in new[] { (0, 0), (29, 0), (0, 11), (29, 11), (0, 5), (29, 5) })
		{
			driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(x, y + dy));
			sys.Input.ProcessInput();

			Assert.Equal(startPos, (win.Left, win.Top));
			Assert.Equal(startSize, (win.Width, win.Height));
			Assert.False(sys.WindowStateService.IsDragging, $"press at ({x},{y}) must not start a drag");
			Assert.False(sys.WindowStateService.IsResizing, $"press at ({x},{y}) must not start a resize");

			driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Released }, new Point(x, y + dy));
			sys.Input.ProcessInput();
		}
	}

	// -------------------------------------------------------------------------------------------
	// Capability-intact: programmatic move/resize still work on a frameless window.
	// -------------------------------------------------------------------------------------------

	[Fact]
	public void Frameless_SetPositionAndSetSize_StillWork()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new Window(sys) { Width = 20, Height = 8, BorderStyle = BorderStyle.Frameless };
		win.SetPosition(new Point(7, 4));
		Assert.Equal(7, win.Left);
		Assert.Equal(4, win.Top);
		win.SetSize(25, 10);
		Assert.Equal(25, win.Width);
		Assert.Equal(10, win.Height);
	}

	[Fact]
	public void SettingFrameless_DoesNotMutateMovableResizable()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new Window(sys) { IsMovable = true, IsResizable = true };
		win.BorderStyle = BorderStyle.Frameless;
		Assert.True(win.IsMovable);
		Assert.True(win.IsResizable);
	}
}

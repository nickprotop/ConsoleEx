// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering;

/// <summary>
/// Regression for "window-renderer portals vanish on a DOM rebuild": a portal created via
/// <see cref="Window.CreatePortal"/> (UseDesktopPortals == false) must survive the structural
/// rebuild that reactive state changes trigger (ForceRebuildLayout / InvalidateDOM → _rootNode
/// rebuilt from the window's controls only). Before the fix, RebuildDOMTree re-added the controls
/// but not the imperatively-attached portals, orphaning them.
/// </summary>
public class WindowPortalSurvivalTests
{
	private static (ConsoleWindowSystem system, Window window) CreateSystem()
	{
		var driver = new HeadlessConsoleDriver(120, 40);
		var options = new ConsoleWindowSystemOptions(
			EnableFrameRateLimiting: false, ShowTopPanel: false, ShowBottomPanel: false);
		var system = new ConsoleWindowSystem(driver, options: options);
		var window = new Window(system) { Left = 0, Top = 0, Width = 100, Height = 30 };
		system.AddWindow(window);
		system.SetActiveWindow(window);
		return (system, window);
	}

	private static (PortalContentContainer container, MarkupControl owner) AddPortal(Window window)
	{
		var owner = new MarkupControl(new List<string> { "host" });
		window.AddControl(owner);

		var container = new PortalContentContainer { PortalBounds = new Rectangle(5, 2, 40, 5) };
		container.AddChild(new MarkupControl(new List<string> { "PALETTE-CONTENT" }));
		container.Container = window;
		window.CreatePortal(owner, container);
		window.RenderAndGetVisibleContent();
		return (container, owner);
	}

	private static bool PortalAttached(Window window, PortalContentContainer container)
		=> window.Renderer?.RootLayoutNode?.PortalChildren.Any(p => ReferenceEquals(p.Control, container)) ?? false;

	[Fact]
	public void WindowPortal_SurvivesStructuralRebuild()
	{
		var (system, window) = CreateSystem();
		var (container, _) = AddPortal(window);

		Assert.True(PortalAttached(window, container), "portal should be attached after creation");

		// This is the path a reactive structural change takes (e.g. a grid column change, or any
		// ForceRebuildLayout): _rootNode is nulled and rebuilt from the window's controls only.
		for (int i = 0; i < 3; i++)
		{
			window.ForceRebuildLayout();
			window.RenderAndGetVisibleContent();
			Assert.True(PortalAttached(window, container),
				$"portal should survive structural rebuild #{i + 1}");
		}
	}

	[Fact]
	public void WindowPortal_RemoveStillWorks_AfterRebuild()
	{
		var (system, window) = CreateSystem();
		var owner = new MarkupControl(new List<string> { "host" });
		window.AddControl(owner);
		var container = new PortalContentContainer { PortalBounds = new Rectangle(5, 2, 40, 5) };
		container.AddChild(new MarkupControl(new List<string> { "X" }));
		container.Container = window;
		var node = window.CreatePortal(owner, container);
		window.RenderAndGetVisibleContent();

		window.ForceRebuildLayout();
		window.RenderAndGetVisibleContent();
		Assert.True(PortalAttached(window, container), "portal alive after rebuild");

		// Explicit removal must fully remove it — and it must NOT come back on the next rebuild.
		window.RemovePortal(owner, node!);
		window.RenderAndGetVisibleContent();
		Assert.False(PortalAttached(window, container), "portal gone after RemovePortal");

		window.ForceRebuildLayout();
		window.RenderAndGetVisibleContent();
		Assert.False(PortalAttached(window, container), "removed portal must not be re-attached on rebuild");
	}

	[Fact]
	public void WindowPortal_FocusedPromptCaret_SurvivesStructuralRebuild()
	{
		var driver = new HeadlessConsoleDriver(120, 40);
		var options = new ConsoleWindowSystemOptions(
			EnableFrameRateLimiting: false, ShowTopPanel: false, ShowBottomPanel: false);
		var system = new ConsoleWindowSystem(driver, options: options);
		var window = new Window(system) { Left = 0, Top = 0, Width = 100, Height = 30 };
		system.AddWindow(window);
		system.SetActiveWindow(window);

		var owner = new MarkupControl(new List<string> { "host" });
		window.AddControl(owner);
		var container = new PortalContentContainer { PortalBounds = new Rectangle(5, 2, 40, 4) };
		var prompt = new PromptControl { Prompt = "> " };
		container.AddChild(prompt);
		container.Container = window;
		window.CreatePortal(owner, container);
		container.SetFocusOnFirstChild();
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);

		foreach (var ch in "ab") // local cursor 4 (margin 0 + promptLen 2 + 2)
			prompt.ProcessKey(new ConsoleKeyInfo(ch, ConsoleKey.A, false, false, false));
		system.Render.UpdateDisplay();
		system.UpdateCursor();
		// inset (1,1) + content origin (5,2) + local 4 = (1+5+4, 1+2) = (10,3)
		var expected = new Point(10, 3);
		Assert.Equal(expected, driver.CursorPosition);

		// A structural rebuild (reactive state change) must keep BOTH the portal and its caret.
		window.ForceRebuildLayout();
		system.Render.UpdateDisplay();
		system.UpdateCursor();
		Assert.True(PortalAttached(window, container), "portal must survive rebuild");
		Assert.True(driver.CursorVisible, "caret must survive rebuild");
		Assert.Equal(expected, driver.CursorPosition);
	}
}

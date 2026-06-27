// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering;

/// <summary>
/// "Real thing" tests for cursor rendering when a desktop portal (e.g. a command palette)
/// hosts a focused text-input control. Drives the actual render + cursor path via
/// <see cref="HeadlessConsoleDriver"/> (which tracks SetCursorPosition / SetCursorVisible)
/// rather than asserting on isolated control internals.
/// </summary>
public class PortalCursorTests
{
	private static (ConsoleWindowSystem system, HeadlessConsoleDriver driver, Window window) CreateSystem()
	{
		var driver = new HeadlessConsoleDriver(120, 40);
		var options = new ConsoleWindowSystemOptions(
			EnableFrameRateLimiting: false,
			ShowTopPanel: false,
			ShowBottomPanel: false);
		var system = new ConsoleWindowSystem(driver, options: options);

		var window = new Window(system) { Left = 0, Top = 0, Width = 120, Height = 40 };
		system.AddWindow(window);
		system.SetActiveWindow(window);
		return (system, driver, window);
	}

	/// <summary>
	/// Builds a real DesktopPortal hosting a PortalContentContainer with a focused PromptControl,
	/// anchored at a non-centered, non-zero offset (row 1), then drives one render + cursor pass.
	/// The caret must be driven to the absolute screen column matching the typed text.
	/// </summary>
	[Fact]
	public void PortalHostedFocusedPrompt_DrivesCursorToCorrectAbsolutePosition()
	{
		var (system, driver, _) = CreateSystem();

		// Non-centered anchor: portal buffer origin at (5, 1).
		var portalBounds = new Rectangle(5, 1, 40, 6);
		var container = new PortalContentContainer { PortalBounds = portalBounds };

		var prompt = new PromptControl { Prompt = "> " };
		container.AddChild(prompt);

		var portal = system.DesktopPortalService.CreatePortal(
			new DesktopPortalOptions(container, portalBounds));

		// Focus the prompt via the portal's own focus path (NOT the window FocusManager).
		container.SetFocusOnFirstChild();
		Assert.True(prompt.HasFocus, "prompt should report focus via the portal registry");

		// Type "abc" through the real input path → cursor sits at logical column 3.
		foreach (var ch in "abc")
			prompt.ProcessKey(new ConsoleKeyInfo(ch, ConsoleKey.A, false, false, false));

		// Render the frame (paints the portal + registers child bounds), then run the cursor pass.
		system.Render.UpdateDisplay();
		int beforePos = driver.SetCursorPositionCallCount;
		int beforeVis = driver.SetCursorVisibleCallCount;
		system.UpdateCursor();

		// Expected absolute column:
		//   BufferOrigin.X (5) + child-buffer X (0) + [Margin.Left 0 + promptLen 2 + cursorPos 3] = 10
		//   BufferOrigin.Y (1) + child-buffer Y (0) + Margin.Top 0 = 1
		Assert.True(driver.CursorVisible, "caret should be visible for a portal-hosted focused prompt");
		Assert.True(driver.SetCursorVisibleCallCount > beforeVis, "SetCursorVisible should have been driven");
		Assert.True(driver.SetCursorPositionCallCount > beforePos, "SetCursorPosition should have been driven");
		Assert.Equal(new Point(10, 1), driver.CursorPosition);

		system.DesktopPortalService.RemovePortal(portal);
	}

	/// <summary>
	/// Regression for the translation-coordination bug: when a portal's <c>BufferOrigin</c> differs
	/// from its <c>Bounds.Location</c> (the real configuration used by window-hosted dropdowns/menus,
	/// where the buffer covers the whole window-content area and BufferOrigin maps buffer (0,0) to the
	/// window content origin), the caret must still land on the correct absolute screen cell.
	/// </summary>
	[Fact]
	public void PortalHostedPrompt_CursorCorrect_WhenBufferOriginDiffersFromBounds()
	{
		var (system, driver, _) = CreateSystem();

		// Buffer covers a larger region; its origin (3,2) is NOT the bounds origin (13,6).
		// rootOff = Bounds - BufferOrigin = (10,4) in buffer space — the untested path.
		var bufferOrigin = new Point(3, 2);
		var bufferSize = new Size(60, 20);
		var bounds = new Rectangle(13, 6, 40, 6);

		var container = new PortalContentContainer { PortalBounds = bounds };
		var prompt = new PromptControl { Prompt = "> " };
		container.AddChild(prompt);

		var portal = system.DesktopPortalService.CreatePortal(
			new DesktopPortalOptions(container, bounds, BufferSize: bufferSize, BufferOrigin: bufferOrigin));
		container.SetFocusOnFirstChild();

		foreach (var ch in "ab")
			prompt.ProcessKey(new ConsoleKeyInfo(ch, ConsoleKey.A, false, false, false));

		system.Render.UpdateDisplay();
		system.UpdateCursor();

		// Child paints at buffer (10,4); cursor is promptLen(2)+typed(2)=4 cols in.
		// Screen = BufferOrigin(3,2) + bufferCursor(14,4) = (17,6).
		// Cross-check: bounds.Location (13,6) is where the prompt's first char lands; +4 cols = X 17.
		Assert.True(driver.CursorVisible);
		Assert.Equal(new Point(17, 6), driver.CursorPosition);

		system.DesktopPortalService.RemovePortal(portal);
	}

	/// <summary>
	/// The portal-hosted caret must follow cursor movement (LeftArrow) and editing (Backspace),
	/// not just the initial typed position.
	/// </summary>
	[Fact]
	public void PortalHostedPrompt_CaretTracksArrowAndBackspace()
	{
		var (system, driver, _) = CreateSystem();

		var portalBounds = new Rectangle(5, 1, 40, 6);
		var container = new PortalContentContainer { PortalBounds = portalBounds };
		var prompt = new PromptControl { Prompt = "> " };
		container.AddChild(prompt);
		var portal = system.DesktopPortalService.CreatePortal(
			new DesktopPortalOptions(container, portalBounds));
		container.SetFocusOnFirstChild();

		foreach (var ch in "abc")
			prompt.ProcessKey(new ConsoleKeyInfo(ch, ConsoleKey.A, false, false, false));
		system.Render.UpdateDisplay();
		system.UpdateCursor();
		Assert.Equal(new Point(10, 1), driver.CursorPosition); // 5 + 2 + 3

		// LeftArrow: cursor 3 -> 2, column 10 -> 9.
		prompt.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false));
		system.Render.UpdateDisplay();
		system.UpdateCursor();
		Assert.Equal(new Point(9, 1), driver.CursorPosition);

		// Backspace at cursor 2 deletes 'b' -> "ac", cursor 2 -> 1, column 9 -> 8.
		prompt.ProcessKey(new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
		system.Render.UpdateDisplay();
		system.UpdateCursor();
		Assert.Equal(new Point(8, 1), driver.CursorPosition);

		system.DesktopPortalService.RemovePortal(portal);
	}

	/// <summary>
	/// After the portal is removed, the portal-owned caret must not linger.
	/// </summary>
	[Fact]
	public void ClosingPortal_HidesCaret()
	{
		var (system, driver, _) = CreateSystem();

		var portalBounds = new Rectangle(5, 1, 40, 6);
		var container = new PortalContentContainer { PortalBounds = portalBounds };
		var prompt = new PromptControl { Prompt = "> " };
		container.AddChild(prompt);

		var portal = system.DesktopPortalService.CreatePortal(
			new DesktopPortalOptions(container, portalBounds));
		container.SetFocusOnFirstChild();

		system.Render.UpdateDisplay();
		system.UpdateCursor();
		Assert.True(driver.CursorVisible);

		// Close the portal: no window control is focused, so the caret must hide.
		system.DesktopPortalService.RemovePortal(portal);
		system.Render.UpdateDisplay();
		system.UpdateCursor();

		Assert.False(driver.CursorVisible, "caret should be hidden after the portal closes");
	}
}

/// <summary>
/// Regression tests for the DEFAULT window-renderer portal path (<see cref="Window.UseDesktopPortals"/>
/// == false, created via <see cref="Window.CreatePortal"/>). Here the portal content is a real
/// participant in the window's layout tree, so its focused child's caret must flow through the WINDOW
/// cursor path (TranslateLogicalCursorToContent) and land at the child's true content position — it
/// must NOT be double-counted by the desktop-portal cursor seam, and the portal's container must not
/// hijack the window's self-painting-cursor-host lookup.
/// </summary>
public class WindowRendererPortalCursorTests
{
	[Fact]
	public void WindowRendererPortal_FocusedPrompt_CaretAtCorrectColumn()
	{
		var driver = new HeadlessConsoleDriver(200, 50);
		var options = new ConsoleWindowSystemOptions(
			EnableFrameRateLimiting: false, ShowTopPanel: false, ShowBottomPanel: false);
		var system = new ConsoleWindowSystem(driver, options: options);

		var window = new Window(system) { Left = 0, Top = 0, Width = 120, Height = 40 };
		Assert.False(window.UseDesktopPortals); // default: window-renderer portal path
		var owner = new MarkupControl(new System.Collections.Generic.List<string> { "host" });
		window.AddControl(owner);
		system.AddWindow(window);
		system.SetActiveWindow(window);

		// Palette anchored at a non-trivial offset; content origin (10,3).
		var portalBounds = new Rectangle(10, 3, 50, 4);
		var container = new PortalContentContainer { PortalBounds = portalBounds };
		var prompt = new PromptControl { Prompt = "> obs " }; // prompt length 6
		container.AddChild(prompt);
		container.Container = window;

		window.CreatePortal(owner, container);

		// The app routes focus through the window FocusManager (mirrors the workbench palette).
		container.SetFocusOnFirstChild();
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);

		foreach (var ch in "abc") // 3 chars → cursor at local 9 (margin 0 + promptLen 6 + 3)
			prompt.ProcessKey(new ConsoleKeyInfo(ch, ConsoleKey.A, false, false, false));

		system.Render.UpdateDisplay();
		system.UpdateCursor();

		// Screen caret = window frame inset (1,1 for the default border) + portal content origin (10,3)
		// + child local cursor (9,0) = (1+10+9, 1+3) = (20,4). The bug produced the double-counted (30,7).
		Assert.True(driver.CursorVisible, "caret should be visible for a window-renderer portal-hosted prompt");
		Assert.Equal(new Point(20, 4), driver.CursorPosition);

		// The portal must survive repeated relayout ticks (the report's "closes on relayout" claim) and
		// the caret must stay put — the per-frame cursor read must be side-effect-free.
		for (int i = 0; i < 5; i++)
		{
			window.Invalidate(Invalidation.Relayout);
			system.Render.UpdateDisplay();
			system.UpdateCursor();
		}
		Assert.NotNull(window.Renderer?.GetLayoutNode(container));
		Assert.True(driver.CursorVisible, "caret should still be visible after relayout ticks");
		Assert.Equal(new Point(20, 4), driver.CursorPosition);
	}

	[Fact]
	public void WindowRendererPortal_CaretTracksTyping()
	{
		var driver = new HeadlessConsoleDriver(200, 50);
		var options = new ConsoleWindowSystemOptions(
			EnableFrameRateLimiting: false, ShowTopPanel: false, ShowBottomPanel: false);
		var system = new ConsoleWindowSystem(driver, options: options);

		var window = new Window(system) { Left = 0, Top = 0, Width = 120, Height = 40 };
		var owner = new MarkupControl(new System.Collections.Generic.List<string> { "host" });
		window.AddControl(owner);
		system.AddWindow(window);
		system.SetActiveWindow(window);

		var portalBounds = new Rectangle(10, 3, 50, 4);
		var container = new PortalContentContainer { PortalBounds = portalBounds };
		var prompt = new PromptControl { Prompt = "> " }; // promptLen 2
		container.AddChild(prompt);
		container.Container = window;
		window.CreatePortal(owner, container);
		container.SetFocusOnFirstChild();
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);

		// Type "ab" → local cursor 4 (margin 0 + promptLen 2 + 2). Screen = 1+10+4 = 15.
		foreach (var ch in "ab")
			prompt.ProcessKey(new ConsoleKeyInfo(ch, ConsoleKey.A, false, false, false));
		system.Render.UpdateDisplay();
		system.UpdateCursor();
		Assert.Equal(new Point(15, 4), driver.CursorPosition);

		// LeftArrow → cursor 3, screen 14.
		prompt.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false));
		system.Render.UpdateDisplay();
		system.UpdateCursor();
		Assert.Equal(new Point(14, 4), driver.CursorPosition);
	}
}

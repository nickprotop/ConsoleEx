// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A right-click context menu (a portal) commonly acts on the current text selection — e.g. a "Copy"
/// item. Clicking inside that menu must NOT clear the window's active selection, otherwise the menu's
/// action (CopySelectionToClipboard) runs against an already-cleared selection and copies nothing.
/// </summary>
public class ContextMenuSelectionTests
{
	private static MouseEventArgs WindowMouse(int wx, int wy, params MouseFlags[] flags)
	{
		var p = new Point(wx, wy);
		return new MouseEventArgs(flags.ToList(), p, p, p);
	}

	[Fact]
	public void Button1Press_InsideOpenPortal_DoesNotClearSelection()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 25);
		var window = new Window(system) { Title = "T", Left = 0, Top = 0, Width = 80, Height = 25 };
		var markup = new MarkupControl(new List<string> { "selectable text here" }) { EnableSelection = true };
		window.AddControl(markup);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Establish a real selection on the markup control.
		var buffer = new CharacterBuffer(45, 15);
		var bounds = new LayoutRect(0, 0, 40, 10);
		markup.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		markup.ProcessMouseEvent(new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(0, 0), new Point(0, 0), new Point(0, 0)));
		markup.ProcessMouseEvent(new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Dragged }, new Point(8, 0), new Point(8, 0), new Point(8, 0)));
		Assert.True(window.SelectionManager.HasSelection);

		// Open a context-menu-like portal with known bounds (content space).
		var portal = new PortalContentContainer { DismissOnOutsideClick = true };
		portal.PortalBounds = new Rectangle(5, 5, 20, 6); // content-space bounds
		var node = window.CreatePortal(markup, portal);
		Assert.NotNull(node);

		// A left-press INSIDE the portal bounds (window space = content + 1 border) — i.e. clicking a
		// menu item — must NOT clear the active selection.
		window.EventDispatcher!.ProcessMouseEvent(WindowMouse(10, 8, MouseFlags.Button1Pressed));

		Assert.True(window.SelectionManager.HasSelection,
			"Clicking inside an open context-menu portal must not clear the text selection.");
	}

	[Fact]
	public void Button1Press_OutsidePortal_StillClearsSelection()
	{
		// Regression guard: the clear-on-empty-click behavior is preserved for genuine content clicks.
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 25);
		var window = new Window(system) { Title = "T", Left = 0, Top = 0, Width = 80, Height = 25 };
		var markup = new MarkupControl(new List<string> { "selectable text here" }) { EnableSelection = true };
		window.AddControl(markup);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var buffer = new CharacterBuffer(45, 15);
		var bounds = new LayoutRect(0, 0, 40, 10);
		markup.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		markup.ProcessMouseEvent(new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(0, 0), new Point(0, 0), new Point(0, 0)));
		markup.ProcessMouseEvent(new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Dragged }, new Point(8, 0), new Point(8, 0), new Point(8, 0)));
		Assert.True(window.SelectionManager.HasSelection);

		// No portal open. Press on empty space far from the markup control clears the selection.
		window.EventDispatcher!.ProcessMouseEvent(WindowMouse(5, 20, MouseFlags.Button1Pressed));

		Assert.False(window.SelectionManager.HasSelection);
	}
}

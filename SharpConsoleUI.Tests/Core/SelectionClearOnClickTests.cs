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

namespace SharpConsoleUI.Tests.Core;

public class SelectionClearOnClickTests
{
	private static MouseEventArgs WindowMouse(int wx, int wy, params MouseFlags[] flags)
	{
		var p = new Point(wx, wy);
		// Position is refined per-control by the dispatcher; WindowPosition drives routing.
		return new MouseEventArgs(flags.ToList(), p, p, p);
	}

	[Fact]
	public void LeftPressOnEmptySpace_ClearsActiveSelection()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system) { Title = "T", Left = 0, Top = 0, Width = 80, Height = 20 };
		var markup = new MarkupControl(new List<string> { "selectable text" }) { EnableSelection = true };
		window.AddControl(markup);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Establish a selection directly on the markup control.
		var buffer = new CharacterBuffer(45, 15);
		var bounds = new LayoutRect(0, 0, 40, 10);
		markup.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		markup.ProcessMouseEvent(new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(0, 0), new Point(0, 0), new Point(0, 0)));
		markup.ProcessMouseEvent(new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Dragged }, new Point(5, 0), new Point(5, 0), new Point(5, 0)));
		Assert.True(window.SelectionManager.HasSelection);

		// Press on empty space far below the single-line markup control.
		window.EventDispatcher!.ProcessMouseEvent(WindowMouse(5, 15, MouseFlags.Button1Pressed));

		Assert.False(window.SelectionManager.HasSelection);
		Assert.Null(window.SelectionManager.ActiveSelection);
	}
}

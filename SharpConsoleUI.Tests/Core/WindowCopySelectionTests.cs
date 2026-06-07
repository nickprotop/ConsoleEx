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
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Core;

public class WindowCopySelectionTests
{
	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var p = new System.Drawing.Point(x, y);
		return new MouseEventArgs(flags.ToList(), p, p, p);
	}

	private static void Paint(MarkupControl control)
	{
		var buffer = new CharacterBuffer(45, 15);
		var bounds = new LayoutRect(0, 0, 40, 10);
		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	[Fact]
	public void CtrlC_CopiesActiveSelection_AsPlainText()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);

		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var control = new MarkupControl(new List<string> { "[green]Copy[/] me" }) { EnableSelection = true };
		window.AddControl(control);
		Paint(control);

		// Select "Copy me" (7 visible columns).
		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(7, 0, MouseFlags.Button1Dragged));
		Assert.True(window.SelectionManager.HasSelection);

		var ctrlC = new ConsoleKeyInfo('\u0003', ConsoleKey.C, shift: false, alt: false, control: true);
		bool handled = window.EventDispatcher!.ProcessInput(ctrlC);

		Assert.True(handled);
		Assert.Equal("Copy me", ClipboardHelper.GetText());
	}

	[Fact]
	public void CtrlC_WithNoSelection_DoesNotIntercept()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		ClipboardHelper.SetText("untouched");

		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var control = new MarkupControl(new List<string> { "no selection" }) { EnableSelection = true };
		window.AddControl(control);
		Paint(control);

		var ctrlC = new ConsoleKeyInfo('\u0003', ConsoleKey.C, shift: false, alt: false, control: true);
		window.EventDispatcher!.ProcessInput(ctrlC);

		// Clipboard not overwritten because there was no active selection.
		Assert.Equal("untouched", ClipboardHelper.GetText());
	}
}

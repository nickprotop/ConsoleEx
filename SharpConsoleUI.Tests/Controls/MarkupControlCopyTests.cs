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

namespace SharpConsoleUI.Tests.Controls;

public class MarkupControlCopyTests
{
	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var p = new Point(x, y);
		return new MouseEventArgs(flags.ToList(), p, p, p);
	}

	private static void Paint(MarkupControl control)
	{
		var buffer = new CharacterBuffer(45, 15);
		var bounds = new LayoutRect(0, 0, 40, 10);
		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	private static void SelectFirstWord(MarkupControl c, int cols)
	{
		c.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(Mouse(cols, 0, MouseFlags.Button1Dragged));
	}

	// --- Append API ---

	[Fact]
	public void AppendLine_AddsLineToContent()
	{
		var c = new MarkupControl(new List<string> { "one" });
		c.AppendLine("two");
		Assert.Equal("one\ntwo", c.Text);
	}

	[Fact]
	public void AppendLines_AddsMultiple()
	{
		var c = new MarkupControl(new List<string> { "a" });
		c.AppendLines(new[] { "b", "c" });
		Assert.Equal("a\nb\nc", c.Text);
	}

	[Fact]
	public void AppendText_SplitsOnNewlines()
	{
		var c = new MarkupControl(new List<string> { "x" });
		c.AppendText("y\nz");
		Assert.Equal("x\ny\nz", c.Text);
	}

	[Fact]
	public void Append_ClearsStaleSelection()
	{
		var c = new MarkupControl(new List<string> { "hello world" }) { EnableSelection = true };
		Paint(c);
		SelectFirstWord(c, 5);
		Assert.True(c.HasSelection);

		c.AppendLine("more");

		Assert.False(c.HasSelection);
	}

	// --- Programmatic copy ---

	[Fact]
	public void CopyToClipboard_CopiesAllPlainText()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		var c = new MarkupControl(new List<string> { "[red]Hello[/]", "[green]World[/]" });

		bool ok = c.CopyToClipboard();

		Assert.True(ok);
		Assert.Equal("Hello\nWorld", ClipboardHelper.GetText());
	}

	[Fact]
	public void CopySelectionToClipboard_CopiesSelectionOnly()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		var c = new MarkupControl(new List<string> { "[red]Hello[/] World" }) { EnableSelection = true };
		Paint(c);
		SelectFirstWord(c, 5);

		bool ok = c.CopySelectionToClipboard();

		Assert.True(ok);
		Assert.Equal("Hello", ClipboardHelper.GetText());
	}

	[Fact]
	public void CopySelectionToClipboard_NoSelection_ReturnsFalse()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		var c = new MarkupControl(new List<string> { "Hello" }) { EnableSelection = true };
		Assert.False(c.CopySelectionToClipboard());
	}

	// --- Customizable / disablable copy key (window level) ---

	private static (Window window, MarkupControl c) SetupSelected(out ConsoleWindowSystem system)
	{
		system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var c = new MarkupControl(new List<string> { "[green]Copy[/] me" }) { EnableSelection = true };
		window.AddControl(c);
		Paint(c);
		c.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(Mouse(7, 0, MouseFlags.Button1Dragged));
		return (window, c);
	}

	[Fact]
	public void CustomCopyKey_TriggersCopy()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		var (window, c) = SetupSelected(out _);
		c.CopyKey = ConsoleKey.Y; // remap copy to Ctrl+Y

		// Ctrl+C should now do nothing at the window level.
		ClipboardHelper.SetText("untouched");
		window.EventDispatcher!.ProcessInput(new ConsoleKeyInfo('\u0003', ConsoleKey.C, false, false, true));
		Assert.Equal("untouched", ClipboardHelper.GetText());

		// Ctrl+Y copies.
		window.EventDispatcher!.ProcessInput(new ConsoleKeyInfo('\u0019', ConsoleKey.Y, false, false, true));
		Assert.Equal("Copy me", ClipboardHelper.GetText());
	}

	[Fact]
	public void CopyDisabled_ShortcutDoesNotCopy()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		var (window, c) = SetupSelected(out _);
		c.CopyEnabled = false;
		ClipboardHelper.SetText("untouched");

		window.EventDispatcher!.ProcessInput(new ConsoleKeyInfo('\u0003', ConsoleKey.C, false, false, true));

		Assert.Equal("untouched", ClipboardHelper.GetText());
		// But programmatic copy still works.
		Assert.True(c.CopySelectionToClipboard());
		Assert.Equal("Copy me", ClipboardHelper.GetText());
	}

	// --- Richer event ---

	[Fact]
	public void TextSelectionChanged_FiresWithStateAndText()
	{
		var c = new MarkupControl(new List<string> { "hello world" }) { EnableSelection = true };
		Paint(c);
		TextSelectionChangedEventArgs? last = null;
		c.TextSelectionChanged += (_, e) => last = e;

		SelectFirstWord(c, 5);

		Assert.NotNull(last);
		Assert.True(last!.HasSelection);
		Assert.Equal("hello", last.SelectedText);

		c.ClearSelection();
		Assert.False(last!.HasSelection);
		Assert.Equal(string.Empty, last.SelectedText);
	}
}

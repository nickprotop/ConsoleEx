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

[Collection("EnvSerial")]
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
	public void AppendText_DefaultInlineIsFalse_StartsNewLine()
	{
		// Explicit default arg must match the implicit default (line-per-call).
		var c = new MarkupControl(new List<string> { "x" });
		c.AppendText("y\nz", inline: false);
		Assert.Equal("x\ny\nz", c.Text);
	}

	[Fact]
	public void AppendText_Inline_JoinsFirstSegmentOntoCurrentLine()
	{
		var c = new MarkupControl(new List<string> { "x" });
		c.AppendText("y\nz", inline: true);
		Assert.Equal("xy\nz", c.Text);
	}

	[Fact]
	public void AppendText_Inline_LeadingNewlineStartsNewLine()
	{
		var c = new MarkupControl(new List<string> { "x" });
		c.AppendText("\ny", inline: true);
		Assert.Equal("x\ny", c.Text);
	}

	[Fact]
	public void AppendText_Inline_MultipleLeadingNewlines_InsertBlankLines()
	{
		// "\n\ny" -> first (empty) segment joins "x" (no-op), then a blank line, then "y".
		var c = new MarkupControl(new List<string> { "x" });
		c.AppendText("\n\ny", inline: true);
		Assert.Equal("x\n\ny", c.Text);
	}

	[Fact]
	public void AppendInline_StringIsOnlyNewlines_AddsBlankLines()
	{
		// Inline-appending pure newlines just opens new (blank) lines below the current one.
		var c = new MarkupControl(new List<string> { "x" });
		c.AppendInline("\n\n");
		Assert.Equal("x\n\n", c.Text);
	}

	[Fact]
	public void AppendInline_JoinsFirstSegmentOntoCurrentLine()
	{
		var c = new MarkupControl(new List<string> { "x" });
		c.AppendInline("y\nz");
		Assert.Equal("xy\nz", c.Text);
	}

	[Fact]
	public void AppendInline_OnEmptyContent_AddsAsFirstLine()
	{
		var c = new MarkupControl(new List<string>());
		c.AppendInline("hello");
		Assert.Equal("hello", c.Text);
	}

	[Fact]
	public void Append_JoinsOntoCurrentLine_ConsoleWriteStyle()
	{
		// .NET-convention name: Append == StringBuilder.Append / Console.Write (inline).
		var c = new MarkupControl(new List<string> { "Hello, " });
		c.Append("world");
		Assert.Equal("Hello, world", c.Text);
	}

	[Fact]
	public void Append_NewlineStartsNewLine()
	{
		var c = new MarkupControl(new List<string> { "x" });
		c.Append("y\nz");
		Assert.Equal("xy\nz", c.Text);
	}

	[Fact]
	public void Append_ThenAppendLine_FormConventionPair()
	{
		var c = new MarkupControl(new List<string>());
		c.Append("Hello, ");
		c.Append("world");
		c.AppendLine("!");
		Assert.Equal("Hello, world\n!", c.Text);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void AppendText_NullOrEmpty_IsNoOp(string? text)
	{
		var c = new MarkupControl(new List<string> { "x" });
		c.AppendText(text!);
		c.AppendText(text!, inline: true);
		Assert.Equal("x", c.Text);
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

	// --- @YotPhiligan report: are SelectionChanged event args garbled for CJK/Cyrillic? ---
	// Expected: PASS. The args carry the exact in-memory string (RaiseSelectionChanged(GetSelectedText()),
	// Cell.Character is a Rune). If this passes, any garble the reporter saw is at their
	// display/console-output boundary, not in the library's event args.

	[Theory]
	[InlineData("中文测试")]            // CJK (wide cells)
	[InlineData("Привет")]              // Cyrillic (narrow cells)
	[InlineData("中文Привет")]          // mixed wide + narrow
	public void SelectionChangedArgs_CarryExactUnicodeText(string text)
	{
		var c = new MarkupControl(new List<string> { text }) { EnableSelection = true };
		Paint(c);

		string? selectionChangedArg = null;
		TextSelectionChangedEventArgs? textSelectionArg = null;
		c.SelectionChanged += (_, s) => selectionChangedArg = s;
		c.TextSelectionChanged += (_, e) => textSelectionArg = e;

		// Drag well past the end of the line so the whole logical line is selected
		// (GetSelectedText clamps the end column to the row's cell count).
		c.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(Mouse(40, 0, MouseFlags.Button1Dragged));

		Assert.True(c.HasSelection);
		Assert.Equal(text, c.GetSelectedText());
		Assert.Equal(text, selectionChangedArg);
		Assert.NotNull(textSelectionArg);
		Assert.True(textSelectionArg!.HasSelection);
		Assert.Equal(text, textSelectionArg.SelectedText);
	}

	// --- Selection round-trip adjacent to a wide char: must not split or include a half/extra glyph ---

	[Fact]
	public void Selection_EndingRightBeforeWideChar_ReturnsNarrowRunOnly()
	{
		// "ab中cd": columns a=0, b=1, 中=2..3 (wide), c=4, d=5.
		// Drag cols 0..2: selection ends exactly at the wide char's start → "ab" only,
		// no half of 中, no extra char.
		var c = new MarkupControl(new List<string> { "ab中cd" }) { EnableSelection = true };
		Paint(c);
		c.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(Mouse(2, 0, MouseFlags.Button1Dragged));

		Assert.True(c.HasSelection);
		Assert.Equal("ab", c.GetSelectedText());
	}

	[Fact]
	public void Selection_StartingRightAfterWideChar_ReturnsTailRunOnly()
	{
		// "ab中cd": the wide char 中 ends at column 4. Drag cols 4..6 selects "cd"
		// (the run starting immediately after the wide char) — no half of 中.
		var c = new MarkupControl(new List<string> { "ab中cd" }) { EnableSelection = true };
		Paint(c);
		c.ProcessMouseEvent(Mouse(4, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(Mouse(6, 0, MouseFlags.Button1Dragged));

		Assert.True(c.HasSelection);
		Assert.Equal("cd", c.GetSelectedText());
	}

	[Fact]
	public void Selection_SpanningWideChar_ReturnsWholeGlyph()
	{
		// Drag cols 1..4 spans b, the whole wide 中, up to (not incl.) c → "b中".
		var c = new MarkupControl(new List<string> { "ab中cd" }) { EnableSelection = true };
		Paint(c);
		c.ProcessMouseEvent(Mouse(1, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(Mouse(4, 0, MouseFlags.Button1Dragged));

		Assert.True(c.HasSelection);
		Assert.Equal("b中", c.GetSelectedText());
	}
}

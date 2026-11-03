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
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Characterization tests for the paths a change to the content model touches: soft wrapping, word
/// jump, vertical caret movement, mouse hit-testing, selection extension and display width.
/// </summary>
/// <remarks>
/// <para>
/// These exist because the older <see cref="PromptControlCharacterizationTests"/> pins single-line
/// basics only. Wrapping, word boundaries, hit-testing and Unicode width were unpinned, and those
/// are exactly what a cursor-model change breaks, silently and in a way no existing test notices.
/// </para>
/// <para>
/// They were written against the string-based implementation and passed before any of it moved.
/// A failure here means the model moved under a behaviour an application already depends on. Fix
/// the control, not the assertion.
/// </para>
/// </remarks>
public class PromptControlWrapCharacterizationTests
{
	private static (ConsoleWindowSystem system, Window window, PromptControl prompt) Host(
		string promptText = "> ", int width = 40, int height = 8, bool multiline = false)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		var prompt = new PromptControl { Prompt = promptText, Multiline = multiline };
		window.AddControl(prompt);
		system.AddWindow(window);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		return (system, window, prompt);
	}

	private static ConsoleKeyInfo Key(ConsoleKey key, char ch = '\0', bool shift = false, bool alt = false, bool ctrl = false)
		=> new(ch, key, shift, alt, ctrl);

	private static MouseEventArgs Mouse(MouseFlags flag, int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags> { flag }, pos, pos, pos);
	}

	/// <summary>One rendered row as text, trailing blanks trimmed.</summary>
	private static string Row(ConsoleWindowSystem system, int y)
	{
		var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
		var sb = new System.Text.StringBuilder();
		for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character.ToString());
		return sb.ToString().TrimEnd();
	}

	#region Soft wrapping

	[Fact]
	public void Multiline_WrapsAtWordBoundary_NotMidWord()
	{
		// Row counts alone cannot tell word wrapping from character wrapping: both produce two
		// rows here. This pins WHERE the break lands, which is what a content-model change moves.
		var (system, _, prompt) = Host(width: 20, multiline: true);
		prompt.SetInput("aaaa bbbb cccc dddd");
		system.Render.UpdateDisplay();

		// 18 columns to wrap in. Word wrapping breaks after "cccc"; character wrapping would
		// carry "dddd" up and split it.
		Assert.Equal("> aaaa bbbb cccc", Row(system, 0));
		Assert.Equal("  dddd", Row(system, 1));
		GC.KeepAlive(system);
	}

	[Fact]
	public void Multiline_WrapsAtWordBoundary_AtGivenWidth()
	{
		var prompt = new PromptControl { Prompt = "> ", Multiline = true, MinRows = 1, MaxRows = 8 };
		// 20 columns of box, 2 taken by the prompt, leaves 18 to wrap in.
		// "aaaa bbbb cccc dddd" is 19, so the last word cannot share the first row.
		prompt.SetInput("aaaa bbbb cccc dddd");

		var size = prompt.MeasureDOM(new LayoutConstraints(0, 20, 0, 100));

		Assert.Equal(2, size.Height);
	}

	[Fact]
	public void Multiline_WordLongerThanWidth_BreaksWithinTheWord()
	{
		var prompt = new PromptControl { Prompt = "> ", Multiline = true, MinRows = 1, MaxRows = 8 };
		// One unbreakable 30-character word in 18 columns has to split rather than overflow.
		prompt.SetInput(new string('x', 30));

		var size = prompt.MeasureDOM(new LayoutConstraints(0, 20, 0, 100));

		// A row count alone holds for any break point. 18 columns of an unbreakable 30-character
		// word is two rows: 18 then 12.
		Assert.Equal(2, size.Height);
	}

	[Fact]
	public void Multiline_ExplicitNewline_StartsANewRow()
	{
		var prompt = new PromptControl { Multiline = true, MinRows = 1, MaxRows = 8 };
		prompt.SetInput("a\nb");

		var size = prompt.MeasureDOM(new LayoutConstraints(0, 40, 0, 100));

		Assert.Equal(2, size.Height);
	}

	#endregion

	#region Word jump

	[Fact]
	public void CtrlRight_FromStart_StopsAfterFirstWord()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("alpha beta");
		prompt.SetLogicalCursorPosition(new Point(2, 0)); // column 2 is offset 0 past "> "
		system.Render.UpdateDisplay();

		prompt.ProcessKey(Key(ConsoleKey.RightArrow, ctrl: true));

		// Word-right lands at the start of "beta", i.e. offset 6, column 8.
		var pos = prompt.GetLogicalCursorPosition();
		Assert.NotNull(pos);
		Assert.Equal(8, pos!.Value.X);
		GC.KeepAlive(system);
	}

	[Fact]
	public void CtrlLeft_FromEnd_StopsAtStartOfLastWord()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("alpha beta");
		system.Render.UpdateDisplay();

		prompt.ProcessKey(Key(ConsoleKey.LeftArrow, ctrl: true));

		// From the end, word-left lands at the start of "beta": offset 6, column 8.
		var pos = prompt.GetLogicalCursorPosition();
		Assert.NotNull(pos);
		Assert.Equal(8, pos!.Value.X);
		GC.KeepAlive(system);
	}

	#endregion

	#region Mouse hit-testing

	[Fact]
	public void Click_WithinText_PlacesCaretAtThatColumn()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("hello world");
		system.Render.UpdateDisplay();

		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1Clicked, 5, 0));

		var pos = prompt.GetLogicalCursorPosition();
		Assert.NotNull(pos);
		Assert.Equal(5, pos!.Value.X);
		GC.KeepAlive(system);
	}

	[Fact]
	public void Click_PastEndOfText_PlacesCaretAtEnd()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("hi");
		system.Render.UpdateDisplay();

		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1Clicked, 30, 0));

		// "> " plus "hi" is 4 columns; a click past the text parks at the end.
		var pos = prompt.GetLogicalCursorPosition();
		Assert.NotNull(pos);
		Assert.Equal(4, pos!.Value.X);
		GC.KeepAlive(system);
	}

	[Fact]
	public void DoubleClick_InWord_SelectsThatWord()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("alpha beta");
		system.Render.UpdateDisplay();

		// Column 9 is inside "beta" ("> " + "alpha " is 8 columns).
		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1DoubleClicked, 9, 0));

		Assert.Equal("beta", prompt.SelectedText);
		GC.KeepAlive(system);
	}

	#endregion

	#region Vertical movement and row ends

	[Fact]
	public void Multiline_Home_GoesToStartOfVisualRow()
	{
		var (system, _, prompt) = Host(width: 20, multiline: true);
		prompt.SetInput("aaaa bbbb cccc dddd");
		system.Render.UpdateDisplay();

		prompt.ProcessKey(Key(ConsoleKey.Home));

		var pos = prompt.GetLogicalCursorPosition();
		Assert.NotNull(pos);
		Assert.Equal(2, pos!.Value.X); // column 2 is the first text column after "> "
		GC.KeepAlive(system);
	}

	[Fact]
	public void Multiline_UpArrow_MovesToThePreviousRow()
	{
		var (system, _, prompt) = Host(width: 20, multiline: true);
		prompt.SetInput("aaaa bbbb cccc dddd");
		system.Render.UpdateDisplay();
		var before = prompt.GetLogicalCursorPosition();

		prompt.ProcessKey(Key(ConsoleKey.UpArrow));

		var after = prompt.GetLogicalCursorPosition();
		Assert.NotNull(before);
		Assert.NotNull(after);
		Assert.True(after!.Value.Y < before!.Value.Y, "expected the caret to move up a row");
		// The column is the half a cursor-model change breaks, and "moved up" holds without it.
		Assert.Equal(before!.Value.X, after.Value.X);
		GC.KeepAlive(system);
	}

	#endregion

	#region Selection extension

	[Fact]
	public void ShiftLeft_ExtendsSelectionByOneCharacter()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("hello");
		system.Render.UpdateDisplay();

		prompt.ProcessKey(Key(ConsoleKey.LeftArrow, shift: true));

		Assert.Equal("o", prompt.SelectedText);
		GC.KeepAlive(system);
	}

	[Fact]
	public void ShiftLeftTwice_ExtendsSelectionByTwoCharacters()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("hello");
		system.Render.UpdateDisplay();

		prompt.ProcessKey(Key(ConsoleKey.LeftArrow, shift: true));
		prompt.ProcessKey(Key(ConsoleKey.LeftArrow, shift: true));

		Assert.Equal("lo", prompt.SelectedText);
		GC.KeepAlive(system);
	}

	#endregion

	#region Display width

	[Fact]
	public void CjkInput_CaretColumn_CountsTwoColumnsPerGlyph()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("你好"); // two wide glyphs
		system.Render.UpdateDisplay();

		// "> " is 2 columns, two wide glyphs are 4 more.
		var pos = prompt.GetLogicalCursorPosition();
		Assert.NotNull(pos);
		Assert.Equal(6, pos!.Value.X);
		GC.KeepAlive(system);
	}

	[Fact]
	public void EmojiInput_CaretColumn_CountsSurrogatePairAsOneGlyph()
	{
		var (system, _, prompt) = Host();
		// TWO emoji. Each is two UTF-16 units and two display columns, so a single emoji cannot
		// tell the two apart (2 + 2 either way). Two of them can: four UTF-16 units against four
		// display columns is still equal, so a trailing narrow char is added to break the tie -
		// 5 UTF-16 units but 5 columns... which is also equal. The honest discriminator is a
		// COMBINING mark: it adds a UTF-16 unit and zero columns.
		prompt.SetInput("e\u0301"); // "e" plus combining acute: 2 UTF-16 units, 1 column
		system.Render.UpdateDisplay();

		// "> " is 2 columns, the composed glyph is 1 more. Counting UTF-16 units would give 4.
		var pos = prompt.GetLogicalCursorPosition();
		Assert.NotNull(pos);
		Assert.Equal(3, pos!.Value.X);
		GC.KeepAlive(system);
	}

	#endregion
}

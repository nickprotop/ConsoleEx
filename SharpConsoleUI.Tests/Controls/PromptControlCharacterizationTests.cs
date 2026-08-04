// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Characterization tests: these pin the behaviour <see cref="PromptControl"/> had BEFORE the
/// multiline modernization, and they are the proof that <c>Multiline</c> defaulting to <c>false</c>
/// changes nothing for an application that upgrades and edits no code.
/// <para>
/// They are deliberately written against the DEFAULT-constructed control. Do not add
/// <c>Multiline = true</c> to any test in this file, and do not relax an assertion here to make a
/// new feature pass — a failure in this file means the default path moved, which is the one thing
/// this change promised not to do. <see cref="FormControl"/> builds its single-line fields on this
/// control, so the blast radius reaches forms as well as direct users.
/// </para>
/// </summary>
public class PromptControlCharacterizationTests
{
	private static (ConsoleWindowSystem system, Window window, PromptControl prompt) Host(
		string promptText = "> ", int width = 40, int height = 8)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		var prompt = new PromptControl { Prompt = promptText };
		window.AddControl(prompt);
		system.AddWindow(window);
		return (system, window, prompt);
	}

	[Fact]
	public void Default_MeasuresExactlyOneRow()
	{
		var prompt = new PromptControl { Prompt = "> " };
		prompt.SetInput("a string long enough to overflow any narrow field it is measured in");

		var size = prompt.MeasureDOM(new LayoutConstraints(0, 20, 0, 100));

		Assert.Equal(1, size.Height);
	}

	[Fact]
	public void Default_SetInput_FlattensNewlinesToSpaces()
	{
		var prompt = new PromptControl();

		prompt.SetInput("alpha\nbeta\r\ngamma\rdelta");

		Assert.Equal("alpha beta gamma delta", prompt.Input);
		Assert.DoesNotContain('\n', prompt.Input);
	}

	[Fact]
	public void Default_Paste_FlattensNewlinesToSpaces()
	{
		var prompt = new PromptControl();

		prompt.Paste("one\ntwo");

		Assert.Equal("one two", prompt.Input);
	}

	[Fact]
	public void Default_AsciiCaret_SitsAfterPromptPlusCharacterCount()
	{
		var (system, window, prompt) = Host("> ");
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		prompt.SetInput("hello");
		system.Render.UpdateDisplay();

		var pos = prompt.GetLogicalCursorPosition();

		// Prompt "> " is 2 columns; cursor parked at end of "hello" => column 7.
		Assert.NotNull(pos);
		Assert.Equal(7, pos!.Value.X);
		Assert.Equal(0, pos.Value.Y);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void Default_UnfocusedCaret_IsNull()
	{
		var prompt = new PromptControl();

		Assert.Null(prompt.GetLogicalCursorPosition());
	}

	[Fact]
	public void Default_LogicalContentSize_IsOneRowTall()
	{
		var prompt = new PromptControl { Prompt = "> " };
		prompt.SetInput("hello world");

		Assert.Equal(1, prompt.GetLogicalContentSize().Height);
	}

	[Fact]
	public void Default_Enter_RaisesEntered_WithCurrentText()
	{
		var prompt = new PromptControl();
		prompt.SetInput("submit me");
		string? captured = null;
		prompt.Entered += (_, text) => captured = text;

		prompt.ProcessKey(new System.ConsoleKeyInfo('\r', System.ConsoleKey.Enter, false, false, false));

		Assert.Equal("submit me", captured);
	}

	[Fact]
	public void Default_TypedCharacters_AppendAndRaiseInputChanged()
	{
		var prompt = new PromptControl();
		var seen = new List<string>();
		prompt.InputChanged += (_, text) => seen.Add(text);

		prompt.ProcessKey(new System.ConsoleKeyInfo('h', System.ConsoleKey.H, false, false, false));
		prompt.ProcessKey(new System.ConsoleKeyInfo('i', System.ConsoleKey.I, false, false, false));

		Assert.Equal("hi", prompt.Input);
		Assert.Equal(new[] { "h", "hi" }, seen);
	}

	[Fact]
	public void Default_RendersPromptThenInput_OnTheFirstRow()
	{
		var (system, window, prompt) = Host("> ");
		prompt.SetInput("abc");
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
		Assert.Contains("> abc", RowText(snap, FindRowContaining(snap, "abc"), snap.Width));
		System.GC.KeepAlive(system);
	}

	private static int FindRowContaining(SharpConsoleUI.Diagnostics.Snapshots.CharacterBufferSnapshot snap, string needle)
	{
		for (int y = 0; y < snap.Height; y++)
			if (RowText(snap, y, snap.Width).Contains(needle))
				return y;
		return -1;
	}

	private static string RowText(SharpConsoleUI.Diagnostics.Snapshots.CharacterBufferSnapshot snap, int y, int w)
	{
		var sb = new System.Text.StringBuilder();
		for (int x = 0; x < w; x++) sb.Append(snap.GetCell(x, y).Character.ToString());
		return sb.ToString();
	}
}

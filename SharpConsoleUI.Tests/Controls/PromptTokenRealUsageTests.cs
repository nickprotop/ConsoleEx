// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// The composer path end to end, the way an application actually builds and drives it.
/// </summary>
/// <remarks>
/// <para>
/// The isolated tests all construct a <see cref="PromptControl"/> directly and call its methods.
/// That has repeatedly passed green while the live application was visibly broken, because a bare
/// control is laid out at whatever size it asks for and a method call skips the dispatch that
/// decides who receives the key.
/// </para>
/// <para>
/// So this one builds the real nesting a chat composer uses, at a width narrow enough that the
/// content wraps, drives it through <see cref="InputStateService.EnqueueKey"/> and
/// <c>system.Input.ProcessInput()</c> rather than touching the control, and asserts the state
/// SURVIVES a re-render rather than only existing between the action and the assert.
/// </para>
/// </remarks>
public class PromptTokenRealUsageTests
{
	private const string PastedBody =
		"first pasted line\nsecond pasted line\nthird pasted line\nfourth pasted line";

	private const string TokenLabel = "[Pasted text #1 +4 lines]";

	[Fact]
	public void Composer_PasteFilteredToToken_SurvivesEditingAndARerender()
	{
		// A narrow box, so the content wraps and the token has to be placed rather than trivially
		// fitting on one row.
		var system = TestWindowSystemBuilder.CreateTestSystem(34, 10);
		var window = new Window(system) { Left = 0, Top = 0, Width = 34, Height = 10 };

		var prompt = new PromptBuilder()
			.WithPrompt("> ")
			.Multiline()
			.WithPasteFilter(text =>
			{
				int lines = text.Split('\n').Length;
				return lines > 3 ? new PromptToken($"[Pasted text #1 +{lines} lines]", text) : null;
			})
			.Build();

		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel().Build();
		panel.AddControl(prompt);
		window.AddControl(panel);
		system.AddWindow(window);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		system.Render.UpdateDisplay();

		// The host's own paste path, not a method call on the control.
		prompt.Paste(PastedBody);
		system.Render.UpdateDisplay();

		Assert.Contains(prompt.InputSegments, segment => segment.IsToken);
		Assert.Equal(TokenLabel, prompt.InputLabelText);
		Assert.Equal(PastedBody, prompt.Input);

		// Type after the token through the REAL dispatch: enqueue, then let the input loop route it.
		foreach (char c in " ok")
		{
			system.InputStateService.EnqueueKey(new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false));
			system.Input.ProcessInput();
		}

		system.Render.UpdateDisplay();

		// Survives the re-render: the token is still a token, still holding its value, and the typed
		// text landed after it rather than inside the label.
		Assert.Contains(prompt.InputSegments, segment => segment.IsToken);
		Assert.Equal($"{TokenLabel} ok", prompt.InputLabelText);
		Assert.Equal($"{PastedBody} ok", prompt.Input);

		// One arrow left through the real path puts the caret between the token and the text, never
		// inside the label.
		system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false));
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();

		// Content cannot change on an arrow key, so asserting Input here would hold wherever the
		// caret went, including inside the label. The caret is the thing under test.
		var caret = prompt.GetLogicalCursorPosition();
		Assert.NotNull(caret);
		int labelEnd = TokenLabel.Length + 2;   // the prompt "> " precedes it
		Assert.True(caret!.Value.X >= labelEnd,
			$"caret at column {caret.Value.X} is inside the label, which ends at {labelEnd}");

		// Backspace over the token's trailing edge takes the whole token, not one label character.
		for (int i = 0; i < 3; i++)
		{
			system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\0', ConsoleKey.Backspace, false, false, false));
			system.Input.ProcessInput();
		}

		system.Render.UpdateDisplay();

		Assert.DoesNotContain(prompt.InputSegments, segment => segment.IsToken);
		Assert.DoesNotContain("Pasted", prompt.InputLabelText);
		GC.KeepAlive(system);
	}
}

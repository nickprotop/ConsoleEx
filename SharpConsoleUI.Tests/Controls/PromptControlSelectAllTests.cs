// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Selecting the whole input from code.
/// </summary>
/// <remarks>
/// <para>
/// The control could already select everything from the keyboard - Home, then
/// Shift+End - but an application could not ask for it. The gap showed in the
/// browser demo: focusing the address bar should offer the current URL ready to
/// be replaced, the way a browser does, and with no way to say so the demo had
/// to blank the field instead, throwing the address away.
/// </para>
/// <para>
/// Selection here is an anchor plus the cursor, so selecting everything is the
/// anchor at the start with the cursor at the end: the same state the keyboard
/// path produces, which is what makes the existing delete, copy and replace
/// paths work on it unchanged.
/// </para>
/// </remarks>
public class PromptControlSelectAllTests
{
	private static ConsoleKeyInfo Key(ConsoleKey key, char ch = '\0', bool shift = false) =>
		new(ch, key, shift, false, false);

	[Fact]
	public void SelectAllSelectsTheWholeInput()
	{
		var prompt = new PromptControl { Input = "https://example.com" };

		prompt.SelectAll();

		Assert.True(prompt.HasSelection);
	}

	[Fact]
	public void TypingOverASelectionReplacesIt()
	{
		// What the feature is for. The browser behaviour: focus the address,
		// type, and the old address is gone.
		var prompt = new PromptControl { Input = "https://old.example" };

		prompt.SelectAll();
		prompt.ProcessKey(Key(ConsoleKey.X, 'x'));

		Assert.Equal("x", prompt.Input);
	}

	[Fact]
	public void BackspaceOverASelectionClearsTheInput()
	{
		var prompt = new PromptControl { Input = "some text" };

		prompt.SelectAll();
		prompt.ProcessKey(Key(ConsoleKey.Backspace));

		Assert.Equal(string.Empty, prompt.Input);
	}

	[Fact]
	public void SelectAllOnAnEmptyInputSelectsNothing()
	{
		// An empty selection is not a selection, and must not leave the control
		// believing it has one: a stale anchor is what used to crash the delete
		// path.
		var prompt = new PromptControl { Input = "" };

		prompt.SelectAll();

		Assert.False(prompt.HasSelection);
	}

	[Fact]
	public void SelectAllSurvivesTheTextChangingUnderIt()
	{
		// Setting Input clears the selection, as every other replace path does.
		// Without that the anchor would outlive the text it referred to.
		var prompt = new PromptControl { Input = "a long original value" };

		prompt.SelectAll();
		prompt.Input = "short";

		Assert.False(prompt.HasSelection);
	}
}

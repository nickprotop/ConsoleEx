// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A selection index that outlives the text it referred to must never crash the host.
/// <para>
/// <c>DeleteSelection</c> computed its range from <c>_selectionAnchor</c> and <c>_cursorPosition</c>
/// without bounding either against <c>_input.Length</c>, so a stale anchor produced a range past the
/// end of the string and <c>string.Remove</c> threw. The exception escaped <c>ProcessKey</c> and the
/// input loop in <c>ConsoleWindowSystem.Run()</c>, so one Backspace ended the user's whole session.
/// </para>
/// <para>
/// Two independent defects fed it: the tab-completion paths replaced the text without clearing the
/// selection (unlike the <c>Input</c> setter and history replace, which both clear it), and
/// <c>DeleteSelection</c> trusted the fields. Both are covered here — the clamp is the one that
/// matters, because an anchor is written from many paths and will go stale again.
/// </para>
/// </summary>
public class PromptControlDeleteSelectionCrashTests
{
	private static (ConsoleWindowSystem system, Window window, PromptControl prompt) Host(int width = 40, int height = 8)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		var prompt = new PromptControl { Prompt = "> " };
		window.AddControl(prompt);
		system.AddWindow(window);
		return (system, window, prompt);
	}

	private static ConsoleKeyInfo Key(ConsoleKey key, char ch = '\0', bool shift = false) =>
		new ConsoleKeyInfo(ch, key, shift, false, false);

	/// <summary>
	/// Selects the whole input the way a user does: Home, then Shift+End. This leaves the ANCHOR at 0
	/// and the cursor at the end.
	/// </summary>
	private static void SelectAll(PromptControl prompt)
	{
		prompt.ProcessKey(Key(ConsoleKey.Home));
		prompt.ProcessKey(Key(ConsoleKey.End, shift: true));
	}

	/// <summary>
	/// Selects the whole input BACKWARDS: End, then Shift+Home. This leaves the anchor at the END of the
	/// text — which is what makes a shortened replacement fatal. <c>MoveCursorTo</c> clamps the CURSOR to
	/// the new length, so a forward selection survives by luck (its low index is the clamped cursor); only
	/// the stale high anchor drives the range past the end of the string.
	/// </summary>
	private static void SelectAllBackwards(PromptControl prompt)
	{
		prompt.ProcessKey(Key(ConsoleKey.End));
		prompt.ProcessKey(Key(ConsoleKey.Home, shift: true));
	}

	/// <summary>
	/// The reported path: a live selection, then the host replaces the text from a button-click
	/// handler, then the user presses Backspace. This took the whole application down.
	/// </summary>
	[Fact]
	public void Backspace_AfterInputReplacedExternally_WithLiveSelection_DoesNotThrow()
	{
		var (_, _, prompt) = Host();
		prompt.Input = "a much longer piece of text";
		SelectAllBackwards(prompt);

		// Host replaces the composer text (e.g. cancelling a queued message) while a selection is live.
		prompt.Input = "hi";

		var ex = Record.Exception(() => prompt.ProcessKey(Key(ConsoleKey.Backspace)));
		Assert.Null(ex);
	}

	/// <summary>
	/// Tab completion replaced the text and moved the cursor but left the anchor pointing into the
	/// discarded string. Completing to something SHORTER than the selection is what puts the stale
	/// anchor past the end.
	/// </summary>
	[Fact]
	public void Backspace_AfterTabCompletionToShorterText_DoesNotThrow()
	{
		var (_, _, prompt) = Host();
		prompt.TabCompleter = (_, _) => new List<string> { "ab" };
		prompt.Input = "a long current value";
		SelectAllBackwards(prompt);

		prompt.ProcessKey(Key(ConsoleKey.Tab, '\t'));

		var ex = Record.Exception(() => prompt.ProcessKey(Key(ConsoleKey.Backspace)));
		Assert.Null(ex);
	}

	/// <summary>
	/// The common-prefix branch of tab completion is a separate code path and needs its own cover.
	/// </summary>
	[Fact]
	public void Backspace_AfterCommonPrefixCompletion_DoesNotThrow()
	{
		var (_, _, prompt) = Host();
		prompt.TabCompleter = (_, _) => new List<string> { "commit", "commitment" };
		prompt.Input = "com";
		SelectAll(prompt);

		prompt.ProcessKey(Key(ConsoleKey.Tab, '\t'));

		var ex = Record.Exception(() => prompt.ProcessKey(Key(ConsoleKey.Backspace)));
		Assert.Null(ex);
	}

	/// <summary>
	/// Tab completion must leave no selection behind at all — the direct assertion of fix #1, rather
	/// than only its crash symptom.
	/// </summary>
	[Fact]
	public void TabCompletion_ClearsTheSelection()
	{
		var (_, _, prompt) = Host();
		prompt.TabCompleter = (_, _) => new List<string> { "ab" };
		prompt.Input = "a long current value";
		SelectAll(prompt);
		Assert.True(prompt.HasSelection, "precondition: a selection is live before completing");

		prompt.ProcessKey(Key(ConsoleKey.Tab, '\t'));

		Assert.False(prompt.HasSelection, "tab completion must not leave a selection into discarded text");
	}

	/// <summary>
	/// The fix must not turn a genuine deletion into a no-op — the risk of clamping too eagerly.
	/// </summary>
	[Fact]
	public void OrdinarySelectionDelete_StillDeletes()
	{
		var (_, _, prompt) = Host();
		prompt.Input = "hello world";
		SelectAll(prompt);

		prompt.ProcessKey(Key(ConsoleKey.Backspace));

		Assert.Equal(string.Empty, prompt.Input);
		Assert.False(prompt.HasSelection);
	}

	/// <summary>
	/// And a partial selection deletes exactly its own range, leaving the rest intact.
	/// </summary>
	[Fact]
	public void PartialSelectionDelete_RemovesOnlyTheSelectedRange()
	{
		var (_, _, prompt) = Host();
		prompt.Input = "hello world";

		// Select the trailing " world": End, then Shift+Left six times.
		prompt.ProcessKey(Key(ConsoleKey.End));
		for (int i = 0; i < 6; i++) prompt.ProcessKey(Key(ConsoleKey.LeftArrow, shift: true));

		prompt.ProcessKey(Key(ConsoleKey.Backspace));

		Assert.Equal("hello", prompt.Input);
	}

	/// <summary>
	/// Delete (forward) shares <c>DeleteSelection</c> with Backspace, so it must be equally safe.
	/// </summary>
	[Fact]
	public void ForwardDelete_AfterInputReplacedExternally_DoesNotThrow()
	{
		var (_, _, prompt) = Host();
		prompt.Input = "a much longer piece of text";
		SelectAllBackwards(prompt);
		prompt.Input = "hi";

		var ex = Record.Exception(() => prompt.ProcessKey(Key(ConsoleKey.Delete)));
		Assert.Null(ex);
	}

	/// <summary>
	/// Typing a character over a selection also deletes it, so the same staleness reaches the
	/// insert path.
	/// </summary>
	[Fact]
	public void TypingOverStaleSelection_DoesNotThrow()
	{
		var (_, _, prompt) = Host();
		prompt.TabCompleter = (_, _) => new List<string> { "ab" };
		prompt.Input = "a long current value";
		SelectAllBackwards(prompt);
		prompt.ProcessKey(Key(ConsoleKey.Tab, '\t'));

		var ex = Record.Exception(() => prompt.ProcessKey(Key(ConsoleKey.X, 'x')));
		Assert.Null(ex);
	}
}

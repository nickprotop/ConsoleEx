// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Browsing history must not destroy what the user was typing. History is a committed list; the
/// unsent draft lives in a one-slot stash just past the newest entry, as readline does — first Up
/// stashes it, and one Down past the newest entry restores it.
/// <para>
/// Before this, both handlers replaced the buffer unconditionally (and Down past the end replaced it
/// with <c>string.Empty</c>), so a half-written message was gone with no way back.
/// </para>
/// </summary>
public class PromptControlHistoryDraftTests
{
	private static PromptControl Host(params string[] history)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(60, 12);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 12 };
		var prompt = new PromptControl { Prompt = "> ", HistoryEnabled = true };
		window.AddControl(prompt);
		system.AddWindow(window);
		foreach (var h in history) prompt.RecordHistory(h);
		return prompt;
	}

	private static ConsoleKeyInfo Key(ConsoleKey key) => new ConsoleKeyInfo('\0', key, false, false, false);

	private static void Up(PromptControl p) => p.ProcessKey(Key(ConsoleKey.UpArrow));
	private static void Down(PromptControl p) => p.ProcessKey(Key(ConsoleKey.DownArrow));

	private static void Type(PromptControl p, string text)
	{
		foreach (char c in text)
			p.ProcessKey(new ConsoleKeyInfo(c, ConsoleKey.A, false, false, false));
	}

	/// <summary>The headline: type, browse away, come back — the draft is still there.</summary>
	[Fact]
	public void Draft_SurvivesAnUpDownRoundTrip()
	{
		var p = Host("first", "second");
		Type(p, "hello");

		Up(p);
		Assert.Equal("second", p.Input);

		Down(p);
		Assert.Equal("hello", p.Input);
	}

	/// <summary>The caret comes back with the text, or restoring a paragraph strands it at the end.</summary>
	[Fact]
	public void Draft_RestoresTheCursorPosition()
	{
		var p = Host("first");
		Type(p, "hello world");
		p.ProcessKey(Key(ConsoleKey.Home));
		p.ProcessKey(Key(ConsoleKey.RightArrow));
		p.ProcessKey(Key(ConsoleKey.RightArrow));
		int savedCursor = p.CursorPositionForTest;

		Up(p);
		Down(p);

		Assert.Equal("hello world", p.Input);
		Assert.Equal(savedCursor, p.CursorPositionForTest);
	}

	/// <summary>Walking several entries deep and back still returns the draft.</summary>
	[Fact]
	public void Draft_SurvivesWalkingSeveralEntriesDeep()
	{
		var p = Host("one", "two", "three");
		Type(p, "my draft");

		Up(p); Up(p); Up(p);
		Assert.Equal("one", p.Input);

		Down(p); Down(p); Down(p);
		Assert.Equal("my draft", p.Input);
	}

	/// <summary>
	/// Down past the newest entry must restore the draft, not clear the line — the string.Empty that
	/// made a stray Down destructive.
	/// </summary>
	[Fact]
	public void DownPastNewestEntry_RestoresDraft_DoesNotClear()
	{
		var p = Host("only");
		Type(p, "unsent text");

		Up(p);
		Assert.Equal("only", p.Input);

		Down(p);
		Assert.Equal("unsent text", p.Input);
	}

	/// <summary>
	/// The subtle requirement: an UNMODIFIED history entry must not block browsing. A naive
	/// "buffer is non-empty" guard would strand the user on the first entry they browsed onto.
	/// </summary>
	[Fact]
	public void UnmodifiedHistoryEntry_DoesNotBlockBrowsing()
	{
		var p = Host("oldest", "middle", "newest");

		Up(p);
		Assert.Equal("newest", p.Input);
		Up(p);
		Assert.Equal("middle", p.Input);
		Up(p);
		Assert.Equal("oldest", p.Input);
	}

	/// <summary>
	/// An empty buffer browses without stashing anything, and coming back yields an empty line rather
	/// than a resurrected ghost.
	/// </summary>
	[Fact]
	public void EmptyBuffer_BrowsesAndReturnsToEmpty()
	{
		var p = Host("first", "second");

		Up(p);
		Assert.Equal("second", p.Input);
		Down(p);
		Assert.Equal(string.Empty, p.Input);
	}

	/// <summary>
	/// Editing a browsed entry banks the edit into the single draft slot (spec decision 1, chosen over
	/// bash-style per-slot undo). Two consequences, both asserted here because they are the contract:
	/// walking back onto the entry shows its PRISTINE text, and the edit returns at the draft position
	/// past the newest entry. The edit is preserved either way — never silently dropped.
	/// </summary>
	[Fact]
	public void EditedHistoryEntry_IsBankedAsTheDraft()
	{
		var p = Host("first", "second");

		Up(p);
		Assert.Equal("second", p.Input);
		Type(p, "-edited");
		Assert.Equal("second-edited", p.Input);

		Up(p);
		Assert.Equal("first", p.Input);

		// The entry itself is unmodified on the way back...
		Down(p);
		Assert.Equal("second", p.Input);

		// ...and the edit is waiting in the draft slot.
		Down(p);
		Assert.Equal("second-edited", p.Input);
	}

	/// <summary>
	/// Committing clears the stash. Note the submitted text legitimately BECOMES a history entry, so
	/// the check is that the draft position is empty afterwards — the stash did not survive the send
	/// to be restored on top of a fresh line.
	/// </summary>
	[Fact]
	public void Enter_ClearsTheStash()
	{
		var p = Host("first");
		Type(p, "stashed");
		Up(p);                                   // stash "stashed", load "first"
		Assert.Equal("first", p.Input);

		p.ProcessKey(Key(ConsoleKey.Enter));     // commit while browsing — ends the browse

		// Walk back down to the draft position: it must be empty, not the pre-send stash.
		Up(p);
		Down(p);
		Assert.NotEqual("stashed", p.Input);
	}

	/// <summary>
	/// The row boundary is unchanged: with the caret on a lower row of a multiline buffer, Up moves the
	/// caret and never reaches history.
	/// </summary>
	[Fact]
	public void MultilineRowBoundary_StillHolds()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(60, 12);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 12 };
		var p = new PromptControl { Prompt = "> ", HistoryEnabled = true, Multiline = true };
		window.AddControl(p);
		system.AddWindow(window);
		p.RecordHistory("a history entry");

		p.Input = "line one\nline two";
		system.Render.UpdateDisplay();

		// Caret is at the end (row 2). Up must move the caret within the buffer, not load history.
		Up(p);
		Assert.Equal("line one\nline two", p.Input);
	}

	/// <summary>
	/// A pasted multi-line block is the reported trigger and the case with the most text at stake.
	/// </summary>
	[Fact]
	public void PastedMultilineDraft_IsNotLost()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(60, 12);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 12 };
		var p = new PromptControl { Prompt = "> ", HistoryEnabled = true, Multiline = true };
		window.AddControl(p);
		system.AddWindow(window);
		p.RecordHistory("previous");

		const string pasted = "paragraph one\nparagraph two\nparagraph three";
		p.Input = pasted;
		system.Render.UpdateDisplay();

		// From the TOP row, Up crosses into history; Down must bring the whole block back.
		p.ProcessKey(Key(ConsoleKey.Home));
		for (int i = 0; i < 5; i++) Up(p);
		Assert.Equal("previous", p.Input);

		Down(p);
		Assert.Equal(pasted, p.Input);
	}
}

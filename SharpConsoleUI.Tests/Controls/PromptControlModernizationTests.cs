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
/// The modernized <see cref="PromptControl"/>: optional wrapping, the Enter contract, the value
/// limits, and the four defects that were fixed unconditionally.
/// <para>
/// The default-mode behaviour these sit alongside is pinned separately in
/// <see cref="PromptControlCharacterizationTests"/>; nothing here may require a change there.
/// </para>
/// </summary>
public class PromptControlModernizationTests
{
	private static (ConsoleWindowSystem system, Window window, PromptControl prompt) Host(
		Action<PromptControl>? configure = null, int width = 40, int height = 12)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		var prompt = new PromptControl { Prompt = "> " };
		configure?.Invoke(prompt);
		window.AddControl(prompt);
		system.AddWindow(window);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		return (system, window, prompt);
	}

	private static ConsoleKeyInfo Key(ConsoleKey key, char ch = '\0', bool shift = false, bool alt = false, bool ctrl = false)
		=> new(ch, key, shift, alt, ctrl);

	private static ConsoleKeyInfo Ch(char c) => new(c, ConsoleKey.NoName, false, false, false);

	#region The opt-in

	[Fact]
	public void Multiline_DefaultsToFalse()
	{
		Assert.False(new PromptControl().Multiline);
	}

	[Fact]
	public void Multiline_AllowsNewlinesThroughSetInput()
	{
		var prompt = new PromptControl { Multiline = true };

		prompt.SetInput("alpha\nbeta");

		Assert.Equal("alpha\nbeta", prompt.Input);
	}

	[Fact]
	public void Multiline_Paste_KeepsNewlines()
	{
		var prompt = new PromptControl { Multiline = true };

		prompt.Paste("one\ntwo");

		Assert.Equal("one\ntwo", prompt.Input);
	}

	[Fact]
	public void TurningMultilineOff_FlattensTheValueItCanNoLongerRender()
	{
		var prompt = new PromptControl { Multiline = true };
		prompt.SetInput("alpha\nbeta");

		prompt.Multiline = false;

		Assert.Equal("alpha beta", prompt.Input);
	}

	#endregion

	#region Wrap and grow

	[Fact]
	public void Multiline_GrowsARowWhenTheTextWraps()
	{
		var prompt = new PromptControl { Prompt = "> ", Multiline = true, MinRows = 1, MaxRows = 8 };
		// 20 columns of box, 2 taken by the prompt => 18 to wrap in.
		prompt.SetInput("aaaa bbbb cccc dddd eeee");

		var size = prompt.MeasureDOM(new LayoutConstraints(0, 20, 0, 100));

		Assert.True(size.Height > 1, $"expected the box to grow, measured {size.Height} rows");
	}

	[Fact]
	public void Multiline_ShortText_StaysAtMinRows()
	{
		var prompt = new PromptControl { Multiline = true, MinRows = 3, MaxRows = 8 };
		prompt.SetInput("hi");

		var size = prompt.MeasureDOM(new LayoutConstraints(0, 40, 0, 100));

		Assert.Equal(3, size.Height);
	}

	[Fact]
	public void Multiline_NeverGrowsPastMaxRows()
	{
		var prompt = new PromptControl { Multiline = true, MinRows = 1, MaxRows = 3 };
		prompt.SetInput(string.Join("\n", new[] { "1", "2", "3", "4", "5", "6", "7", "8" }));

		var size = prompt.MeasureDOM(new LayoutConstraints(0, 40, 0, 100));

		Assert.Equal(3, size.Height);
	}

	[Fact]
	public void SingleLine_IgnoresMinRows()
	{
		// MinRows is documented as multiline-only: a single-line prompt is always one row.
		var prompt = new PromptControl { Multiline = false, MinRows = 5 };
		prompt.SetInput("hi");

		Assert.Equal(1, prompt.MeasureDOM(new LayoutConstraints(0, 40, 0, 100)).Height);
	}

	[Fact]
	public void Multiline_CaretMovesToTheSecondRow_WhenTheTextWrapped()
	{
		var (system, _, prompt) = Host(p => { p.Multiline = true; p.MaxRows = 6; }, width: 24);
		prompt.SetInput("aaaa bbbb cccc dddd eeee ffff");
		system.Render.UpdateDisplay();

		var pos = prompt.GetLogicalCursorPosition();

		Assert.NotNull(pos);
		Assert.True(pos!.Value.Y > 0, $"caret should sit on a wrapped row, got Y={pos.Value.Y}");
		System.GC.KeepAlive(system);
	}

	#endregion

	#region The Enter contract

	[Fact]
	public void Submit_IsTheDefaultBehaviour()
	{
		Assert.Equal(EnterBehavior.Submit, new PromptControl().EnterBehavior);
	}

	[Fact]
	public void Submit_PlainEnter_Submits_EvenWhenMultiline()
	{
		var prompt = new PromptControl { Multiline = true };
		prompt.SetInput("send me");
		string? captured = null;
		prompt.Entered += (_, t) => captured = t;

		prompt.ProcessKey(Key(ConsoleKey.Enter, '\r'));

		Assert.Equal("send me", captured);
		Assert.DoesNotContain('\n', prompt.Input);
	}

	[Fact]
	public void Submit_AltEnter_InsertsNewline_WhenMultiline()
	{
		var prompt = new PromptControl { Multiline = true };
		prompt.SetInput("ab");
		bool submitted = false;
		prompt.Entered += (_, _) => submitted = true;

		prompt.ProcessKey(Key(ConsoleKey.Enter, '\r', alt: true));

		Assert.Equal("ab\n", prompt.Input);
		Assert.False(submitted);
	}

	[Fact]
	public void Submit_CtrlL_InsertsNewline_WhenMultiline()
	{
		var prompt = new PromptControl { Multiline = true };
		prompt.SetInput("ab");

		prompt.ProcessKey(Key(ConsoleKey.L, '\f', ctrl: true));

		Assert.Equal("ab\n", prompt.Input);
	}

	[Fact]
	public void Submit_AltEnter_SubmitsWhenNotMultiline_BecauseThereIsNoNewlineToInsert()
	{
		var prompt = new PromptControl();
		prompt.SetInput("x");
		string? captured = null;
		prompt.Entered += (_, t) => captured = t;

		prompt.ProcessKey(Key(ConsoleKey.Enter, '\r', alt: true));

		Assert.Equal("x", captured);
	}

	[Fact]
	public void InsertNewline_PlainEnter_Inserts_AndCtrlEnterSubmits()
	{
		var prompt = new PromptControl { Multiline = true, EnterBehavior = EnterBehavior.InsertNewline };
		prompt.SetInput("ab");
		string? captured = null;
		prompt.Entered += (_, t) => captured = t;

		prompt.ProcessKey(Key(ConsoleKey.Enter, '\r'));
		Assert.Equal("ab\n", prompt.Input);
		Assert.Null(captured);

		prompt.ProcessKey(Key(ConsoleKey.Enter, '\r', ctrl: true));
		Assert.Equal("ab\n", captured);
	}

	#endregion

	#region MaxLength

	[Fact]
	public void MaxLength_StopsTyping_AtTheLimit()
	{
		var prompt = new PromptControl { MaxLength = 3 };

		foreach (char c in "abcdef")
			prompt.ProcessKey(Ch(c));

		Assert.Equal("abc", prompt.Input);
	}

	[Fact]
	public void MaxLength_TruncatesPaste_RatherThanRejectingIt()
	{
		var prompt = new PromptControl { MaxLength = 4 };

		prompt.Paste("abcdefgh");

		Assert.Equal("abcd", prompt.Input);
	}

	[Fact]
	public void MaxLength_TruncatesSetInput()
	{
		var prompt = new PromptControl { MaxLength = 2 };

		prompt.SetInput("abcdef");

		Assert.Equal("ab", prompt.Input);
	}

	[Fact]
	public void MaxLength_Null_IsUnlimited()
	{
		var prompt = new PromptControl { MaxLength = null };

		prompt.SetInput(new string('x', 5000));

		Assert.Equal(5000, prompt.Input.Length);
	}

	#endregion

	#region Placeholder and ReadOnly

	[Fact]
	public void Placeholder_IsNeverPartOfTheValue()
	{
		var prompt = new PromptControl { Placeholder = "type here" };

		Assert.Equal(string.Empty, prompt.Input);
	}

	[Fact]
	public void Placeholder_Renders_WhileTheValueIsEmpty()
	{
		var (system, _, _) = Host(p => p.Placeholder = "type here");
		system.Render.UpdateDisplay();

		var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
		Assert.True(ContainsText(snap, "type here"), "placeholder was not painted");
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void Placeholder_Disappears_OnceThereIsAValue()
	{
		var (system, _, prompt) = Host(p => p.Placeholder = "type here");
		prompt.SetInput("abc");
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
		Assert.False(ContainsText(snap, "type here"), "placeholder outlived the empty value");
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void ReadOnly_RefusesTyping_AndDeletion()
	{
		var prompt = new PromptControl { ReadOnly = true };
		prompt.SetInput("fixed");

		prompt.ProcessKey(Ch('x'));
		prompt.ProcessKey(Key(ConsoleKey.Backspace, '\b'));
		prompt.ProcessKey(Key(ConsoleKey.Delete));
		prompt.Paste("nope");

		Assert.Equal("fixed", prompt.Input);
	}

	[Fact]
	public void ReadOnly_StillNavigatesAndSelects()
	{
		var prompt = new PromptControl { ReadOnly = true };
		prompt.SetInput("fixed");

		prompt.ProcessKey(Key(ConsoleKey.Home));
		prompt.ProcessKey(Key(ConsoleKey.A, 'a', ctrl: true));

		Assert.Equal("fixed", prompt.SelectedText);
	}

	#endregion

	#region Defect: Unicode caret

	[Fact]
	public void Caret_AfterWideCharacters_IsMeasuredInDisplayColumns()
	{
		// "中文" is two CJK characters, each two columns wide. With a 2-column prompt the caret at the
		// end of the value belongs at column 2 + 4 = 6. Counting characters instead would say 4.
		var (system, _, prompt) = Host();
		prompt.SetInput("中文");
		system.Render.UpdateDisplay();

		var pos = prompt.GetLogicalCursorPosition();

		Assert.NotNull(pos);
		Assert.Equal(6, pos!.Value.X);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void Caret_BetweenWideCharacters_IsMeasuredInDisplayColumns()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("中文");
		prompt.ProcessKey(Key(ConsoleKey.LeftArrow));
		system.Render.UpdateDisplay();

		var pos = prompt.GetLogicalCursorPosition();

		Assert.NotNull(pos);
		Assert.Equal(4, pos!.Value.X); // prompt 2 + one wide char
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void Caret_NarrowOnly_IsUnchanged()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("hello");
		system.Render.UpdateDisplay();

		Assert.Equal(7, prompt.GetLogicalCursorPosition()!.Value.X);
		System.GC.KeepAlive(system);
	}

	#endregion

	#region Defect: mouse events were declared and never raised

	[Fact]
	public void MouseClick_Raises_MouseClick()
	{
		var (system, _, prompt) = Host();
		bool raised = false;
		prompt.MouseClick += (_, _) => raised = true;

		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1Clicked, 4, 0));

		Assert.True(raised, "MouseClick was declared but not raised");
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void RightClick_Raises_MouseRightClick()
	{
		var (system, _, prompt) = Host();
		bool raised = false;
		prompt.MouseRightClick += (_, _) => raised = true;

		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button3Clicked, 4, 0));

		Assert.True(raised);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void MouseEnterAndLeave_AreRaised()
	{
		var (system, _, prompt) = Host();
		bool entered = false, left = false;
		prompt.MouseEnter += (_, _) => entered = true;
		prompt.MouseLeave += (_, _) => left = true;

		prompt.ProcessMouseEvent(Mouse(MouseFlags.MouseEnter, 2, 0));
		prompt.ProcessMouseEvent(Mouse(MouseFlags.MouseLeave, 2, 0));

		Assert.True(entered);
		Assert.True(left);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void DoubleClick_SelectsTheWordUnderThePointer()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("alpha beta gamma");
		system.Render.UpdateDisplay();

		// "alpha beta gamma" starts at column 2 (after "> "); column 9 is inside "beta".
		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1DoubleClicked, 9, 0));

		Assert.Equal("beta", prompt.SelectedText);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void PressThenDrag_SelectsTheRange()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("alpha beta");
		system.Render.UpdateDisplay();

		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1Pressed, 2, 0));   // before 'a'
		prompt.ProcessMouseEvent(Mouse(MouseFlags.ReportMousePosition, 7, 0)); // after "alpha"

		Assert.Equal("alpha", prompt.SelectedText);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void Click_OnSecondHalfOfAWideCharacter_LandsOnThatCharacter()
	{
		var (system, _, prompt) = Host();
		prompt.SetInput("中文ab");
		system.Render.UpdateDisplay();

		// Columns: prompt 0-1, 中 at 2-3, 文 at 4-5. Clicking column 4 is the start of 文 => index 1.
		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1Pressed, 4, 0));
		prompt.ProcessMouseEvent(Mouse(MouseFlags.ReportMousePosition, 6, 0));

		Assert.Equal("文", prompt.SelectedText);
		System.GC.KeepAlive(system);
	}

	#endregion

	#region Defect: unbounded history

	[Fact]
	public void History_IsCapped_AtMaxHistoryEntries()
	{
		var prompt = new PromptControl { HistoryEnabled = true, MaxHistoryEntries = 3 };

		for (int i = 0; i < 10; i++)
		{
			prompt.SetInput($"cmd{i}");
			prompt.ProcessKey(Key(ConsoleKey.Enter, '\r'));
		}

		// Walk back further than the cap; the oldest entries must be gone rather than retained.
		var recalled = new List<string>();
		for (int i = 0; i < 5; i++)
		{
			prompt.ProcessKey(Key(ConsoleKey.UpArrow));
			recalled.Add(prompt.Input);
		}

		Assert.DoesNotContain("cmd0", recalled);
		Assert.Contains("cmd9", recalled);
	}

	[Fact]
	public void History_DoesNotRecordConsecutiveDuplicates()
	{
		var prompt = new PromptControl { HistoryEnabled = true };

		for (int i = 0; i < 3; i++)
		{
			prompt.SetInput("same");
			prompt.ProcessKey(Key(ConsoleKey.Enter, '\r'));
		}

		prompt.ProcessKey(Key(ConsoleKey.UpArrow));
		Assert.Equal("same", prompt.Input);

		// A second Up must not find another copy of the same entry behind the first.
		prompt.ProcessKey(Key(ConsoleKey.UpArrow));
		Assert.Equal("same", prompt.Input);
	}

	[Fact]
	public void ClearHistory_ResetsRecall()
	{
		var prompt = new PromptControl { HistoryEnabled = true };
		prompt.SetInput("one");
		prompt.ProcessKey(Key(ConsoleKey.Enter, '\r'));

		prompt.ClearHistory();
		prompt.SetInput(string.Empty);
		prompt.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(string.Empty, prompt.Input);
	}

	#endregion

	#region Builder

	[Fact]
	public void Builder_RoundTripsTheNewOptions()
	{
		var prompt = new SharpConsoleUI.Builders.PromptBuilder()
			.Multiline()
			.WithRows(2, 7)
			.WithEnterBehavior(EnterBehavior.InsertNewline)
			.WithPlaceholder("say something")
			.WithMaxLength(120)
			.WithMaxHistoryEntries(25)
			.Build();

		Assert.True(prompt.Multiline);
		Assert.Equal(2, prompt.MinRows);
		Assert.Equal(7, prompt.MaxRows);
		Assert.Equal(EnterBehavior.InsertNewline, prompt.EnterBehavior);
		Assert.Equal("say something", prompt.Placeholder);
		Assert.Equal(120, prompt.MaxLength);
		Assert.Equal(25, prompt.MaxHistoryEntries);
	}

	[Fact]
	public void Builder_AppliesMultilineBeforeTheInitialValue()
	{
		// Order matters: SetInput flattens newlines in single-line mode, so a builder that set the
		// value before the mode would silently destroy a multiline initial value.
		var prompt = new SharpConsoleUI.Builders.PromptBuilder()
			.Multiline()
			.WithInput("alpha\nbeta")
			.Build();

		Assert.Equal("alpha\nbeta", prompt.Input);
	}

	[Fact]
	public void Builder_ReadOnly_StillAcceptsTheValueItWasBuiltWith()
	{
		var prompt = new SharpConsoleUI.Builders.PromptBuilder()
			.WithInput("preset")
			.ReadOnly()
			.Build();

		Assert.Equal("preset", prompt.Input);
		Assert.True(prompt.ReadOnly);
	}

	[Fact]
	public void Builder_DefaultsAreUnchanged()
	{
		var prompt = new SharpConsoleUI.Builders.PromptBuilder().Build();

		Assert.False(prompt.Multiline);
		Assert.False(prompt.ReadOnly);
		Assert.Null(prompt.MaxLength);
		Assert.Null(prompt.Placeholder);
		Assert.Equal(EnterBehavior.Submit, prompt.EnterBehavior);
	}

	#endregion

	private static MouseEventArgs Mouse(MouseFlags flag, int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags> { flag }, pos, pos, pos);
	}

	private static bool ContainsText(SharpConsoleUI.Diagnostics.Snapshots.CharacterBufferSnapshot snap, string needle)
	{
		for (int y = 0; y < snap.Height; y++)
		{
			var sb = new System.Text.StringBuilder();
			for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character.ToString());
			if (sb.ToString().Contains(needle)) return true;
		}
		return false;
	}
}

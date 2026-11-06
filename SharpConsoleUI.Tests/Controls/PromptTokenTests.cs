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
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// The token types a <see cref="PromptControl"/> holds: what a token is, and how a piece of the
/// input is either ordinary text or one of them.
/// </summary>
public class PromptTokenTests
{
	#region PromptToken

	[Fact]
	public void PromptToken_IsValueEqual_ByLabelValueAndTag()
	{
		var a = new PromptToken("[Pasted text #1]", "a long value", "paste");
		var b = new PromptToken("[Pasted text #1]", "a long value", "paste");

		Assert.Equal(a, b);
	}

	[Fact]
	public void PromptToken_DiffersWhenValueDiffers()
	{
		var a = new PromptToken("[Pasted text #1]", "one", "paste");
		var b = new PromptToken("[Pasted text #1]", "two", "paste");

		Assert.NotEqual(a, b);
	}

	[Fact]
	public void PromptToken_TagIsOptional()
	{
		var token = new PromptToken("[Image #1]", "/tmp/a.png");

		Assert.Null(token.Tag);
	}

	[Fact]
	public void PromptToken_NullLabel_Throws()
	{
		Assert.Throws<ArgumentNullException>(() => new PromptToken(null!, "value"));
	}

	[Fact]
	public void PromptToken_NullValue_Throws()
	{
		Assert.Throws<ArgumentNullException>(() => new PromptToken("[x]", null!));
	}

	/// <summary>
	/// A degenerate token is allowed. The control must not break on one, and refusing it here
	/// would push the check onto every host instead.
	/// </summary>
	[Fact]
	public void PromptToken_EmptyLabelAndValue_Allowed()
	{
		var token = new PromptToken(string.Empty, string.Empty);

		Assert.Equal(string.Empty, token.Label);
		Assert.Equal(string.Empty, token.Value);
	}

	#endregion

	#region PromptSegment

	[Fact]
	public void PromptSegment_FromText_IsNotToken_AndCarriesText()
	{
		var segment = PromptSegment.FromText("hello");

		Assert.False(segment.IsToken);
		Assert.Equal("hello", segment.Text);
		Assert.Null(segment.Token);
	}

	[Fact]
	public void PromptSegment_FromToken_IsToken_AndCarriesToken()
	{
		var token = new PromptToken("[Image #1]", "/tmp/a.png");

		var segment = PromptSegment.FromToken(token);

		Assert.True(segment.IsToken);
		Assert.Same(token, segment.Token);
		Assert.Null(segment.Text);
	}

	[Fact]
	public void PromptSegment_FromText_NullText_Throws()
	{
		Assert.Throws<ArgumentNullException>(() => PromptSegment.FromText(null!));
	}

	[Fact]
	public void PromptSegment_FromToken_NullToken_Throws()
	{
		Assert.Throws<ArgumentNullException>(() => PromptSegment.FromToken(null!));
	}

	#endregion

	#region Segment model with no tokens

	/// <summary>
	/// The storage is a segment list, but with no token present the projection is the identical
	/// string the control always stored, so a plain value round trips unchanged.
	/// </summary>
	[Fact]
	public void NoTokens_InputRoundTripsThroughSetInput()
	{
		var prompt = new PromptControl();

		prompt.SetInput("hello world");

		Assert.Equal("hello world", prompt.Input);
	}

	/// <summary>
	/// With no token there is nothing to project differently: the value text and the label text are
	/// the same string.
	/// </summary>
	[Fact]
	public void NoTokens_InputEqualsInputLabelText()
	{
		var prompt = new PromptControl();

		prompt.SetInput("hello world");

		Assert.Equal(prompt.Input, prompt.InputLabelText);
	}

	[Fact]
	public void NoTokens_InputSegments_IsSingleTextSegment()
	{
		var prompt = new PromptControl();

		prompt.SetInput("hello");

		var segments = prompt.InputSegments;
		Assert.Single(segments);
		Assert.False(segments[0].IsToken);
		Assert.Equal("hello", segments[0].Text);
	}

	/// <summary>
	/// Empty content is no segments rather than one empty text segment, so a host reading the
	/// segments of an empty box does not have to filter an artefact out.
	/// </summary>
	[Fact]
	public void EmptyInput_InputSegments_IsEmpty()
	{
		var prompt = new PromptControl();

		Assert.Empty(prompt.InputSegments);
	}

	/// <summary>
	/// A snapshot, not a live view. CLAUDE.md records that <c>List.AsReadOnly()</c> is a live
	/// wrapper, so a host holding the result would otherwise watch it change underneath them.
	/// </summary>
	[Fact]
	public void InputSegments_IsSnapshot_NotLiveView()
	{
		var prompt = new PromptControl();
		prompt.SetInput("first");

		var taken = prompt.InputSegments;
		prompt.SetInput("second");

		Assert.Single(taken);
		Assert.Equal("first", taken[0].Text);
	}

	[Fact]
	public void NoTokens_InputLabelText_IsEmptyForEmptyInput()
	{
		var prompt = new PromptControl();

		Assert.Equal(string.Empty, prompt.InputLabelText);
	}

	/// <summary>
	/// The setter keeps today's meaning in full: single-line mode still flattens newlines, which is
	/// the behaviour every existing caller depends on.
	/// </summary>
	[Fact]
	public void NoTokens_SingleLineSetter_StillFlattensNewlines()
	{
		var prompt = new PromptControl();

		prompt.SetInput("a\nb");

		Assert.Equal("a b", prompt.Input);
		Assert.Single(prompt.InputSegments);
	}

	/// <summary>
	/// Editing through the real key path leaves the segment list a single text run: no edit may
	/// shatter the content into fragments a later projection would have to re-join.
	/// </summary>
	[Fact]
	public void NoTokens_AfterTypingAndDeleting_StillOneTextSegment()
	{
		var prompt = new PromptControl();
		prompt.SetInput("abc");

		prompt.ProcessKey(new ConsoleKeyInfo('d', ConsoleKey.D, false, false, false));
		prompt.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.Backspace, false, false, false));
		prompt.ProcessKey(new ConsoleKeyInfo('e', ConsoleKey.E, false, false, false));

		Assert.Equal("abce", prompt.Input);
		Assert.Single(prompt.InputSegments);
	}

	#endregion

	#region The behaviour matrix: a token is one unit

	// A three-character label, so there are interior indices a caret could wrongly land on. A
	// one-character label would make every "steps over the whole token" test pass by accident.
	private const string TokenLabel = "[P1]";
	private const string TokenValue = "a very long pasted value";

	private static PromptToken Token(string label = TokenLabel, string value = TokenValue)
		=> new(label, value);

	/// <summary>Builds "before" + token + "after" and parks the caret at the start.</summary>
	private static PromptControl WithToken(string before, string after, PromptToken? token = null)
	{
		var prompt = new PromptControl();
		var segments = new List<PromptSegment>();
		if (before.Length > 0) segments.Add(PromptSegment.FromText(before));
		segments.Add(PromptSegment.FromToken(token ?? Token()));
		if (after.Length > 0) segments.Add(PromptSegment.FromText(after));
		prompt.SetInputSegments(segments);
		return prompt;
	}

	private static ConsoleKeyInfo Key(ConsoleKey key, bool shift = false, bool ctrl = false)
		=> new('\0', key, shift, false, ctrl);

	private static ConsoleKeyInfo Typed(char c)
		=> new(c, ConsoleKey.NoName, false, false, false);

	private static void Press(PromptControl prompt, ConsoleKey key, bool shift = false, bool ctrl = false)
		=> prompt.ProcessKey(Key(key, shift, ctrl));

	private static void MoveCaretToStart(PromptControl prompt)
		=> prompt.ProcessKey(Key(ConsoleKey.Home, ctrl: true));

	[Fact]
	public void Right_BeforeToken_StepsOverWholeToken()
	{
		var prompt = WithToken("ab", "cd");
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow);

		// Caret is now immediately before the token's label.
		Assert.Equal("ab".Length, prompt.CursorPositionForTest);

		Press(prompt, ConsoleKey.RightArrow);

		// One keystroke, the whole label.
		Assert.Equal("ab".Length + TokenLabel.Length, prompt.CursorPositionForTest);
	}

	[Fact]
	public void Left_AfterToken_StepsOverWholeToken()
	{
		var prompt = WithToken("ab", "cd");
		Press(prompt, ConsoleKey.LeftArrow);
		Press(prompt, ConsoleKey.LeftArrow);

		Assert.Equal("ab".Length + TokenLabel.Length, prompt.CursorPositionForTest);

		Press(prompt, ConsoleKey.LeftArrow);

		Assert.Equal("ab".Length, prompt.CursorPositionForTest);
	}

	[Fact]
	public void Backspace_AfterToken_RemovesWholeToken()
	{
		var prompt = WithToken("ab", "cd");
		Press(prompt, ConsoleKey.LeftArrow);
		Press(prompt, ConsoleKey.LeftArrow);

		Press(prompt, ConsoleKey.Backspace);

		Assert.Equal("abcd", prompt.Input);
		Assert.DoesNotContain(prompt.InputSegments, s => s.IsToken);
	}

	[Fact]
	public void Backspace_BeforeToken_DeletesPrecedingTextOnly()
	{
		var prompt = WithToken("ab", "cd");
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow);

		Press(prompt, ConsoleKey.Backspace);

		Assert.Equal("a" + TokenValue + "cd", prompt.Input);
		Assert.Contains(prompt.InputSegments, s => s.IsToken);
	}

	[Fact]
	public void Delete_BeforeToken_RemovesWholeToken()
	{
		var prompt = WithToken("ab", "cd");
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow);

		Press(prompt, ConsoleKey.Delete);

		Assert.Equal("abcd", prompt.Input);
		Assert.DoesNotContain(prompt.InputSegments, s => s.IsToken);
	}

	[Fact]
	public void Delete_AfterToken_DeletesFollowingTextOnly()
	{
		var prompt = WithToken("ab", "cd");
		Press(prompt, ConsoleKey.LeftArrow);
		Press(prompt, ConsoleKey.LeftArrow);

		Press(prompt, ConsoleKey.Delete);

		Assert.Equal("ab" + TokenValue + "d", prompt.Input);
		Assert.Contains(prompt.InputSegments, s => s.IsToken);
	}

	[Fact]
	public void ShiftRight_BeforeToken_SelectionCoversWholeToken()
	{
		var prompt = WithToken(string.Empty, "cd");
		MoveCaretToStart(prompt);

		Press(prompt, ConsoleKey.RightArrow, shift: true);

		Assert.Equal(TokenLabel, prompt.SelectedText);
	}

	[Fact]
	public void ShiftLeft_AfterToken_SelectionCoversWholeToken()
	{
		var prompt = WithToken("ab", string.Empty);

		Press(prompt, ConsoleKey.LeftArrow, shift: true);

		Assert.Equal(TokenLabel, prompt.SelectedText);
	}

	/// <summary>
	/// Snapping happens as the selection MOVES, not when it is used, so the highlight on screen is
	/// always exactly what a copy or a delete will take.
	/// </summary>
	[Fact]
	public void SelectionEndingInsideToken_SnapsToWholeToken()
	{
		var prompt = WithToken("ab", "cd");
		MoveCaretToStart(prompt);

		// Two unshifted steps to the token's leading edge, then shift-step in: the caret cannot stop
		// inside, so the selection covers the whole label at once.
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow, shift: true);

		Assert.Equal(TokenLabel, prompt.SelectedText);
		Assert.Equal("ab".Length + TokenLabel.Length, prompt.CursorPositionForTest);
	}

	[Fact]
	public void Typing_WithSelectionOverToken_ReplacesTokenToo()
	{
		var prompt = WithToken("ab", "cd");
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow, shift: true);

		prompt.ProcessKey(Typed('X'));

		Assert.Equal("abXcd", prompt.Input);
		Assert.DoesNotContain(prompt.InputSegments, s => s.IsToken);
	}

	[Fact]
	public void Typing_CaretBeforeToken_InsertsBeforeIt()
	{
		var prompt = WithToken("ab", "cd");
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow);

		prompt.ProcessKey(Typed('X'));

		Assert.Equal("abX" + TokenValue + "cd", prompt.Input);
		Assert.Equal("abX" + TokenLabel + "cd", prompt.InputLabelText);
	}

	[Fact]
	public void Typing_CaretAfterToken_InsertsAfterIt()
	{
		var prompt = WithToken("ab", "cd");
		Press(prompt, ConsoleKey.LeftArrow);
		Press(prompt, ConsoleKey.LeftArrow);

		prompt.ProcessKey(Typed('X'));

		Assert.Equal("ab" + TokenValue + "Xcd", prompt.Input);
	}

	/// <summary>
	/// Two tokens with no text between them. Review Focus 3: the pair must not fuse into one unit
	/// the caret skips in a single step.
	/// </summary>
	[Fact]
	public void AdjacentTokens_EachSteppableIndividually()
	{
		var first = new PromptToken("[A1]", "alpha");
		var second = new PromptToken("[B2]", "beta");
		var prompt = new PromptControl();
		prompt.SetInputSegments(new[] { PromptSegment.FromToken(first), PromptSegment.FromToken(second) });

		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);
		Assert.Equal(first.Label.Length, prompt.CursorPositionForTest);

		Press(prompt, ConsoleKey.RightArrow);
		Assert.Equal(first.Label.Length + second.Label.Length, prompt.CursorPositionForTest);

		Press(prompt, ConsoleKey.LeftArrow);
		Assert.Equal(first.Label.Length, prompt.CursorPositionForTest);
	}

	[Fact]
	public void AdjacentTokens_BackspaceRemovesOnlyTheNearer()
	{
		var first = new PromptToken("[A1]", "alpha");
		var second = new PromptToken("[B2]", "beta");
		var prompt = new PromptControl();
		prompt.SetInputSegments(new[] { PromptSegment.FromToken(first), PromptSegment.FromToken(second) });

		Press(prompt, ConsoleKey.Backspace);

		var segments = prompt.InputSegments;
		Assert.Single(segments);
		Assert.Same(first, segments[0].Token);
		Assert.Equal("alpha", prompt.Input);
	}

	/// <summary>
	/// The invariant the whole matrix rests on. Walked over the content in both directions, in
	/// steps and in word jumps, the caret never once rests on an interior index of a label.
	/// </summary>
	[Fact]
	public void CaretCannotRestInsideToken_AfterAnyArrowSequence()
	{
		var prompt = WithToken("ab cd ", " ef gh");
		int tokenStart = "ab cd ".Length;
		int tokenEnd = tokenStart + TokenLabel.Length;

		void AssertNotInside()
		{
			int pos = prompt.CursorPositionForTest;
			Assert.False(pos > tokenStart && pos < tokenEnd,
				$"caret rested at {pos}, inside the token span [{tokenStart},{tokenEnd})");
		}

		MoveCaretToStart(prompt);
		for (int i = 0; i < prompt.InputLabelText.Length + 2; i++)
		{
			Press(prompt, ConsoleKey.RightArrow);
			AssertNotInside();
		}
		for (int i = 0; i < prompt.InputLabelText.Length + 2; i++)
		{
			Press(prompt, ConsoleKey.LeftArrow);
			AssertNotInside();
		}
		for (int i = 0; i < 6; i++)
		{
			Press(prompt, ConsoleKey.RightArrow, ctrl: true);
			AssertNotInside();
		}
		for (int i = 0; i < 6; i++)
		{
			Press(prompt, ConsoleKey.LeftArrow, ctrl: true);
			AssertNotInside();
		}
	}

	/// <summary>
	/// Where the caret ENDS UP after a token is backspaced away, which the content assertions above
	/// cannot see. The splice removes a touched token whole regardless, so without the span lookup
	/// the content is still right while the caret lands a label-length too far along.
	/// </summary>
	[Fact]
	public void Backspace_AfterToken_LeavesCaretWhereTokenStarted()
	{
		var prompt = WithToken("ab", "cd");
		Press(prompt, ConsoleKey.LeftArrow);
		Press(prompt, ConsoleKey.LeftArrow);

		Press(prompt, ConsoleKey.Backspace);

		Assert.Equal("ab".Length, prompt.CursorPositionForTest);
	}

	/// <summary>
	/// The Delete mirror: the caret stays put where the token was, so a second Delete takes the
	/// character that followed it rather than skipping one.
	/// </summary>
	[Fact]
	public void Delete_BeforeToken_LeavesCaretWhereTokenStarted_AndSecondDeleteTakesNextChar()
	{
		var prompt = WithToken("ab", "cd");
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow);

		Press(prompt, ConsoleKey.Delete);
		Assert.Equal("ab".Length, prompt.CursorPositionForTest);

		Press(prompt, ConsoleKey.Delete);
		Assert.Equal("abd", prompt.Input);
	}

	#endregion

	#region The anchor side of snapping, driven by the mouse

	// The keyboard can only ever walk the CARET into a token, because PrepareSelection drops the
	// anchor where the caret already legally stood. The mouse is what puts an ANCHOR inside a
	// label: a press lands wherever it was clicked, and a double-click's word scan runs over the
	// label's characters. These tests are therefore the ones that exercise anchor snapping.

	/// <summary>
	/// A click on a token tells the host and leaves the caret alone. The control does not decide
	/// what activation means, so it must not quietly expand or move into the label.
	/// </summary>
	[Fact]
	public void Click_OnToken_RaisesTokenActivated_AndDoesNotMoveCaretInside()
	{
		var (system, prompt) = HostWithToken("ab", string.Empty);
		PromptToken? activated = null;
		prompt.TokenActivated += (_, token) => activated = token;

		// Column 3 is inside the label: "ab" takes columns 0 and 1, so the label starts at 2.
		prompt.ProcessMouseEvent(ClickAt(3));

		Assert.NotNull(activated);
		Assert.Equal(TokenLabel, activated!.Label);
		Assert.True(prompt.InputSegments[1].IsToken, "the token must survive a click on it");
		GC.KeepAlive(system);
	}

	/// <summary>
	/// The leftmost cell of a chip is part of the chip. The caret-interior test treats both edges as
	/// outside, which is right for "may the caret rest here" and wrong for "did the user click it".
	/// </summary>
	[Fact]
	public void Click_OnFirstColumnOfLabel_RaisesTokenActivated()
	{
		var (system, prompt) = HostWithToken("ab", string.Empty);
		PromptToken? activated = null;
		prompt.TokenActivated += (_, token) => activated = token;

		// "ab" occupies columns 0 and 1, so the label's first column is 2.
		prompt.ProcessMouseEvent(ClickAt(2));

		Assert.NotNull(activated);
		GC.KeepAlive(system);
	}

	/// <summary>
	/// A one-column label has no interior at all, so a strict-interior test makes it permanently
	/// unclickable: the whole feature would look broken to a host using short labels.
	/// </summary>
	[Fact]
	public void Click_OnSingleColumnLabel_RaisesTokenActivated()
	{
		var (system, prompt) = HostWithToken("ab", string.Empty, new PromptToken("#", "a value"));
		PromptToken? activated = null;
		prompt.TokenActivated += (_, token) => activated = token;

		prompt.ProcessMouseEvent(ClickAt(2));

		Assert.NotNull(activated);
		Assert.Equal("#", activated!.Label);
		GC.KeepAlive(system);
	}


	private static MouseEventArgs ClickAt(int x)
	{
		var pos = new Point(x, 0);
		return new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Clicked }, pos, pos, pos);
	}


	private static (ConsoleWindowSystem system, PromptControl prompt) HostWithToken(
		string before, string after, PromptToken? token = null, int width = 40)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, 8);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = 8 };
		var prompt = new PromptControl { Prompt = string.Empty };
		window.AddControl(prompt);
		system.AddWindow(window);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);

		var segments = new List<PromptSegment>();
		if (before.Length > 0) segments.Add(PromptSegment.FromText(before));
		segments.Add(PromptSegment.FromToken(token ?? Token()));
		if (after.Length > 0) segments.Add(PromptSegment.FromText(after));
		prompt.SetInputSegments(segments);

		system.Render.UpdateDisplay();
		return (system, prompt);
	}

	/// <summary>
	/// A motion-while-held frame, as a terminal actually sends one.
	/// </summary>
	/// <remarks>
	/// SGR reports motion with a button down as the button plus the motion bit,
	/// so the frame carries ReportMousePosition AND the button state. A bare
	/// ReportMousePosition means the mouse moved with nothing held, and mouse
	/// capture never routes one to a dragging control
	/// (WindowEventDispatcher routes only Pressed, Dragged or Released frames).
	/// </remarks>
	private static MouseEventArgs Drag(int x, int y = 0)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags>
			{
				MouseFlags.ReportMousePosition,
				MouseFlags.Button1Pressed,
				MouseFlags.Button1Dragged,
			},
			pos, pos, pos);
	}

	private static MouseEventArgs Mouse(MouseFlags flag, int x, int y = 0)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags> { flag }, pos, pos, pos);
	}

	/// <summary>
	/// A double-click inside an ordinary label. The whole chip is highlighted, with no fragment of
	/// it left out and nothing of the text either side pulled in.
	/// </summary>
	[Fact]
	public void DoubleClickInsideTokenLabel_SelectionCoversWholeToken()
	{
		// Content is "ab [P1] cd" with no prompt, so the label occupies columns 3 to 6.
		var (system, prompt) = HostWithToken("ab ", " cd");

		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1DoubleClicked, 4));

		Assert.Equal(TokenLabel, prompt.SelectedText);
		GC.KeepAlive(system);
	}

	/// <summary>
	/// A press inside the label then a drag out of it. The press sets the anchor directly, so this
	/// is the one path where an unsnapped anchor reaches the selection, and the delete that follows
	/// must still take the token whole rather than bisect its label.
	/// </summary>
	[Fact]
	public void DragFromInsideTokenLabel_ThenDelete_RemovesWholeToken()
	{
		var (system, prompt) = HostWithToken("ab", "cd");

		// A real motion-while-held frame carries the button state as well as the
		// position: a bare ReportMousePosition means the mouse moved with nothing
		// held, and mouse capture never routes one to a dragging control.
		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1Pressed, 3));
		prompt.ProcessMouseEvent(Drag(7));
		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1Released, 7));

		Press(prompt, ConsoleKey.Backspace);

		// Whatever the drag covered, no fragment of the label may survive and no token may remain.
		Assert.DoesNotContain(prompt.InputSegments, s => s.IsToken);
		Assert.DoesNotContain("[", prompt.Input);
		Assert.DoesNotContain("]", prompt.Input);
		GC.KeepAlive(system);
	}

	/// <summary>
	/// A label with a SPACE in it, double-clicked. The word scan stops at that space, so without
	/// anchor snapping the highlight is the half-chip "[P" or "1]": the user would see a bisected
	/// chip and then get something different from what the highlight showed, which is precisely the
	/// alternative the spec rejected.
	/// </summary>
	[Theory]
	[InlineData(5)]
	[InlineData(6)]
	[InlineData(7)]
	public void DoubleClickInsideLabelContainingSpace_HighlightsWholeToken(int column)
	{
		var (system, prompt) = HostWithToken("ab ", " cd", new PromptToken("[P 1]", TokenValue));

		prompt.ProcessMouseEvent(Mouse(MouseFlags.Button1DoubleClicked, column));

		Assert.Equal("[P 1]", prompt.SelectedText);
		GC.KeepAlive(system);
	}

	#endregion

	#region SetInputSegments

	[Fact]
	public void SetInputSegments_ProjectsLabelsAndValuesSeparately()
	{
		var prompt = WithToken("ab", "cd");

		Assert.Equal("ab" + TokenValue + "cd", prompt.Input);
		Assert.Equal("ab" + TokenLabel + "cd", prompt.InputLabelText);
	}

	[Fact]
	public void SetInputSegments_MergesAdjacentTextRuns()
	{
		var prompt = new PromptControl();

		prompt.SetInputSegments(new[] { PromptSegment.FromText("ab"), PromptSegment.FromText("cd") });

		Assert.Single(prompt.InputSegments);
		Assert.Equal("abcd", prompt.Input);
	}

	/// <summary>
	/// An empty-label token is degenerate but legal, and must not corrupt the caret maths around
	/// it: the segment survives, and a step past where it sits still moves one character.
	/// </summary>
	[Fact]
	public void SetInputSegments_EmptyLabelToken_DoesNotBreakCaretMaths()
	{
		var prompt = WithToken("ab", "cd", new PromptToken(string.Empty, "hidden"));

		Assert.Equal("abhiddencd", prompt.Input);
		Assert.Equal("abcd", prompt.InputLabelText);

		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);

		Assert.Equal(1, prompt.CursorPositionForTest);
	}

	#endregion

	#region Kill and cut paths

	private static ConsoleKeyInfo Chord(ConsoleKey key) => new('\0', key, false, false, true);

	[Fact]
	public void CtrlK_FromBeforeToken_RemovesTokenToo()
	{
		var prompt = WithToken("a ", " b", new PromptToken("[T]", "the value"));
		prompt.SetLogicalCursorPosition(new System.Drawing.Point(2, 0));
		prompt.ProcessKey(Chord(ConsoleKey.K));

		Assert.Equal("a ", prompt.Input);
		Assert.DoesNotContain(prompt.InputSegments, seg => seg.IsToken);
	}

	[Fact]
	public void CtrlU_FromAfterToken_RemovesTokenToo()
	{
		var prompt = WithToken("a ", " b", new PromptToken("[T]", "the value"));
		prompt.ProcessKey(Chord(ConsoleKey.U));

		Assert.Equal(string.Empty, prompt.Input);
		Assert.Empty(prompt.InputSegments);
	}

	[Fact]
	public void CtrlW_WhenPreviousWordIsToken_RemovesWholeToken()
	{
		// Caret immediately after the token: the word to the left IS the token.
		var prompt = WithToken("a ", string.Empty, new PromptToken("[T]", "the value"));
		prompt.ProcessKey(Chord(ConsoleKey.W));

		Assert.DoesNotContain(prompt.InputSegments, seg => seg.IsToken);
		Assert.DoesNotContain("the value", prompt.Input);
	}

	[Fact]
	public void CtrlW_NeverLeavesHalfALabel()
	{
		var prompt = WithToken("a ", string.Empty, new PromptToken("[Pasted text #1]", "v"));
		prompt.ProcessKey(Chord(ConsoleKey.W));

		// Whatever it removed, no fragment of the label may survive as ordinary text.
		Assert.DoesNotContain("Pasted", prompt.InputLabelText);
		Assert.DoesNotContain("#1", prompt.InputLabelText);
	}

	/// <summary>
	/// Word-left from the end must land at the token's start, never inside its label. The caret is
	/// read rather than the content, because a word jump moves the caret without editing anything,
	/// and a caret resting inside a label is what "edited as one unit" forbids.
	/// </summary>
	[Fact]
	public void FindWordBoundary_TreatsTokenAsOneWord_WhenSteppingLeft()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 8);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 8 };
		var prompt = WithToken("alpha ", string.Empty, new PromptToken("[Tok]", "v"));
		window.AddControl(prompt);
		system.AddWindow(window);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		system.Render.UpdateDisplay();

		prompt.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, true));

		var pos = prompt.GetLogicalCursorPosition();
		Assert.NotNull(pos);
		Assert.Equal("alpha ".Length, pos!.Value.X);
		GC.KeepAlive(system);
	}

	#endregion

	#region Rendering, wrapping and copy

	// A token label is painted in its own colour, wraps as one unit, and contributes its VALUE to a
	// copy. These tests read the real buffer rather than asking the control what it would paint: the
	// bug this guards against is a disagreement between the two.

	private const string WideLabel = "[Pasted text #1 plus eighteen more lines]";

	// A label with a SPACE in it, 14 columns wide. This is the discriminating shape: word wrapping
	// would break it at that space unprompted, where a label of one unbroken word is kept whole by
	// the shared wrapper without anything token-aware happening at all.
	private const string SpacedLabel = "[Pasted text]";

	/// <summary>
	/// A multiline prompt in a window of <paramref name="width"/>, with no prompt text so the
	/// content starts at column 0 and a column index is a character index for ASCII content.
	/// </summary>
	private static (ConsoleWindowSystem system, PromptControl prompt) HostMultiline(
		int width = 40, int height = 10)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		var prompt = new PromptControl { Prompt = string.Empty, Multiline = true, MinRows = 1, MaxRows = 8 };
		window.AddControl(prompt);
		system.AddWindow(window);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		return (system, prompt);
	}

	private static void SetContent(PromptControl prompt, params PromptSegment[] segments)
		=> prompt.SetInputSegments(segments);

	/// <summary>One rendered row as text, trailing blanks trimmed.</summary>
	private static string Row(ConsoleWindowSystem system, int y)
	{
		var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
		var sb = new System.Text.StringBuilder();
		for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character.ToString());
		return sb.ToString().TrimEnd();
	}

	/// <summary>Every row of the rendered buffer, as text.</summary>
	private static IEnumerable<string> AllRows(ConsoleWindowSystem system)
	{
		var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
		for (int y = 0; y < snap.Height; y++) yield return Row(system, y);
	}

	/// <summary>The foreground colour of one rendered cell.</summary>
	private static Color Foreground(ConsoleWindowSystem system, int x, int y)
		=> system.RenderingDiagnostics!.LastBufferSnapshot!.GetCell(x, y).Foreground;

	/// <summary>
	/// The label is painted in the token colour and the text either side is not, which is what makes
	/// a chip read as a chip rather than as something the user typed.
	/// </summary>
	[Fact]
	public void TokenLabel_PaintedInTokenForegroundColor()
	{
		var (system, prompt) = HostMultiline();
		prompt.InputForegroundColor = Color.White;
		prompt.TokenForegroundColor = Color.Red;
		SetContent(prompt,
			PromptSegment.FromText("ab"),
			PromptSegment.FromToken(Token()),
			PromptSegment.FromText("cd"));
		system.Render.UpdateDisplay();

		// Content is "ab[P1]cd": the label occupies columns 2 to 5.
		Assert.Equal(Color.White, Foreground(system, 0, 0));
		for (int x = 2; x < 2 + TokenLabel.Length; x++)
			Assert.Equal(Color.Red, Foreground(system, x, 0));
		Assert.Equal(Color.White, Foreground(system, 2 + TokenLabel.Length, 0));
		GC.KeepAlive(system);
	}

	/// <summary>
	/// A label that does not fit the columns left on a row moves to the next row whole, rather than
	/// breaking where an ordinary word would.
	/// </summary>
	[Fact]
	public void TokenLabel_NeverSplitAcrossWrappedRows()
	{
		// A REALISTIC label, with a space in it. Word wrapping would break it at that space all on
		// its own, which is what makes this the case that discriminates: a label with no space in it
		// is one word, and the shared wrapper keeps a word whole without being asked to.
		var (system, prompt) = HostMultiline(width: 20);
		SetContent(prompt,
			PromptSegment.FromText("aaaa bbbb "),
			PromptSegment.FromToken(new PromptToken(SpacedLabel, TokenValue)));
		system.Render.UpdateDisplay();

		// 20 columns. "aaaa bbbb " spends 10, and the 14-column label does not fit the 10 left, so
		// it moves to the next row WHOLE rather than putting "[Pasted" on the first.
		Assert.Equal("aaaa bbbb", Row(system, 0));
		Assert.Equal(SpacedLabel, Row(system, 1));
		GC.KeepAlive(system);
	}

	/// <summary>
	/// Every row either holds the whole label or none of it, whatever the width. Swept rather than
	/// pinned at one width, because a boundary off by one column is exactly the bug that survives a
	/// single-width test.
	/// </summary>
	[Theory]
	[InlineData(14)]
	[InlineData(15)]
	[InlineData(16)]
	[InlineData(17)]
	[InlineData(18)]
	[InlineData(19)]
	[InlineData(20)]
	[InlineData(21)]
	[InlineData(22)]
	[InlineData(23)]
	[InlineData(24)]
	public void TokenLabel_NeverSplitAcrossWrappedRows_AtAnyWidth(int width)
	{
		var (system, prompt) = HostMultiline(width: width);
		SetContent(prompt,
			PromptSegment.FromText("aaaa bbbb "),
			PromptSegment.FromToken(new PromptToken(SpacedLabel, TokenValue)),
			PromptSegment.FromText(" cccc dddd"));
		system.Render.UpdateDisplay();

		// Exactly one row carries the label, and it carries it as ONE UNIT: either whole, or cut with
		// the ellipsis because the row it was given is narrower than the label. What may never
		// happen is the label's tail turning up on a second row, which is what a split is.
		int rowsHoldingAnyOfIt = 0;
		foreach (string row in AllRows(system))
		{
			bool whole = row.Contains(SpacedLabel);
			bool truncated = row.Contains("[Pasted") && row.Contains(ControlDefaults.PromptTokenEllipsis);
			if (whole || truncated) { rowsHoldingAnyOfIt++; continue; }

			// A row holding neither must hold no fragment of the label at all.
			Assert.DoesNotContain("[Pasted", row);
			Assert.DoesNotContain("text]", row);
			Assert.DoesNotContain("asted", row);
		}

		Assert.Equal(1, rowsHoldingAnyOfIt);
		GC.KeepAlive(system);
	}

	/// <summary>
	/// A label wider than a whole row cannot move anywhere, so it is cut with an ellipsis and still
	/// occupies one row as one unit. Review Focus 4.
	/// </summary>
	[Fact]
	public void TokenLabel_WiderThanRow_TruncatedWithEllipsis_AsOneUnit()
	{
		var (system, prompt) = HostMultiline(width: 12);
		SetContent(prompt, PromptSegment.FromToken(new PromptToken(WideLabel, "the value")));
		system.Render.UpdateDisplay();

		string row0 = Row(system, 0);

		Assert.EndsWith(ControlDefaults.PromptTokenEllipsis.ToString(), row0);
		Assert.True(row0.Length <= 12, $"truncated label overran the row: '{row0}'");
		Assert.StartsWith("[Pasted", row0);
		// The remainder is not carried on to a second row: the label is one unit, not a paragraph.
		Assert.Equal(string.Empty, Row(system, 1));
		GC.KeepAlive(system);
	}

	/// <summary>
	/// A label of CJK costs two columns a glyph. Measured by characters, three glyphs would look
	/// like three columns and the row would overrun by three. Review Focus 1.
	/// </summary>
	[Fact]
	public void TokenLabel_WithCjk_MeasuresInDisplayColumns()
	{
		// Six CJK glyphs is 12 columns, which cannot fit a 10-column row.
		var (system, prompt) = HostMultiline(width: 10);
		SetContent(prompt, PromptSegment.FromToken(new PromptToken("文字文字文字", "v")));
		system.Render.UpdateDisplay();

		var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
		int painted = 0;
		for (int x = 0; x < snap.Width; x++)
		{
			char c = snap.GetCell(x, 0).Character.ToString()[0];
			if (c == '文' || c == '字') painted++;
		}

		// At most four glyphs plus the ellipsis fit in ten columns. Measured by characters the code
		// would have thought all six fit and painted past the row.
		Assert.True(painted <= 4, $"painted {painted} wide glyphs into 10 columns");
		Assert.Contains(ControlDefaults.PromptTokenEllipsis, Row(system, 0));
		Assert.Equal(string.Empty, Row(system, 1));
		GC.KeepAlive(system);
	}

	/// <summary>
	/// An emoji is one glyph of two columns spelled as two UTF-16 chars, so character counts and
	/// column counts disagree in both directions at once. Review Focus 1.
	/// </summary>
	[Fact]
	public void TokenLabel_WithEmoji_MeasuresInDisplayColumns()
	{
		var (system, prompt) = HostMultiline(width: 20);
		prompt.InputForegroundColor = Color.White;
		prompt.TokenForegroundColor = Color.Red;
		SetContent(prompt,
			PromptSegment.FromText("ab"),
			PromptSegment.FromToken(new PromptToken("\U0001F4E6\U0001F4E6", "/tmp/a.png")),
			PromptSegment.FromText("cd"));
		system.Render.UpdateDisplay();

		// An emoji is ONE glyph of two columns spelled as TWO chars, so the label is painted as a
		// lead cell plus a wide continuation. Splitting the surrogate pair, or treating each char as
		// its own column, lands the boundary somewhere other than column 6.
		var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
		Assert.Equal(Color.White, Foreground(system, 0, 0));
		Assert.Equal(Color.White, Foreground(system, 1, 0));
		for (int x = 2; x < 6; x++)
			Assert.Equal(Color.Red, Foreground(system, x, 0));
		Assert.Equal(Color.White, Foreground(system, 6, 0));

		// The pair survived as one rune rather than as two replacement characters.
		Assert.Equal("\U0001F4E6", snap.GetCell(2, 0).Character.ToString());
		Assert.True(snap.GetCell(3, 0).IsWideContinuation);
		Assert.Equal("\U0001F4E6", snap.GetCell(4, 0).Character.ToString());
		Assert.True(snap.GetCell(5, 0).IsWideContinuation);
		Assert.Equal('c', snap.GetCell(6, 0).Character.ToString()[0]);
		GC.KeepAlive(system);
	}

	/// <summary>
	/// An empty label occupies nothing, so it can neither be split nor move a wrap point. Review
	/// Focus 4.
	/// </summary>
	[Fact]
	public void TokenLabel_Empty_DoesNotBreakWrapping()
	{
		var (system, prompt) = HostMultiline(width: 20);
		SetContent(prompt,
			PromptSegment.FromText("aaaa bbbb cccc dddd"),
			PromptSegment.FromToken(new PromptToken(string.Empty, "hidden")));
		system.Render.UpdateDisplay();

		// The same break an all-text value of the same label projection would take.
		Assert.Equal("aaaa bbbb cccc dddd", prompt.InputLabelText);
		Assert.Equal("aaaa bbbb cccc ", Row(system, 0) + " ");
		Assert.Equal("dddd", Row(system, 1));
		GC.KeepAlive(system);
	}

	/// <summary>
	/// Copying a selection that covers a token puts its VALUE on the clipboard: the label is a
	/// summary that means nothing in another application.
	/// </summary>
	[Fact]
	public void Copy_SelectionSpanningToken_YieldsValue_NotLabel()
	{
		var prompt = WithToken("ab", "cd");
		prompt.ProcessKey(Key(ConsoleKey.A, ctrl: true));

		Assert.Equal("ab" + TokenValue + "cd", prompt.SelectedValueText);
		Assert.Equal("ab" + TokenLabel + "cd", prompt.SelectedText);
	}

	[Fact]
	public void SelectedValueText_SelectionCoveringOnlyTheToken_IsTheValue()
	{
		var prompt = WithToken(string.Empty, "cd");
		MoveCaretToStart(prompt);

		Press(prompt, ConsoleKey.RightArrow, shift: true);

		Assert.Equal(TokenValue, prompt.SelectedValueText);
	}

	[Fact]
	public void SelectedValueText_NoSelection_IsNull()
	{
		var prompt = WithToken("ab", "cd");

		Assert.Null(prompt.SelectedValueText);
	}

	/// <summary>
	/// A token is NOT masked. A masked prompt is a password field, and a host that puts a chip in one
	/// has made a mistake; drawing the label says so where asterisks would hide it. Ordinary text in
	/// the same field is still masked.
	/// </summary>
	[Fact]
	public void Token_IsNotMasked_WhenMaskCharacterSet()
	{
		var (system, prompt) = HostMultiline();
		prompt.MaskCharacter = '*';
		SetContent(prompt,
			PromptSegment.FromText("ab"),
			PromptSegment.FromToken(Token()),
			PromptSegment.FromText("cd"));
		system.Render.UpdateDisplay();

		Assert.Equal("**" + TokenLabel + "**", Row(system, 0));
		GC.KeepAlive(system);
	}

	/// <summary>
	/// A token counts as one wide glyph as wide as its label, so End lands past the whole label and
	/// Home before it, in one keystroke each.
	/// </summary>
	[Fact]
	public void HomeEnd_WithToken_TreatTokenAsOneWideGlyph()
	{
		var (system, prompt) = HostMultiline();
		SetContent(prompt,
			PromptSegment.FromText("ab"),
			PromptSegment.FromToken(Token()),
			PromptSegment.FromText("cd"));
		system.Render.UpdateDisplay();

		Press(prompt, ConsoleKey.Home);
		Assert.Equal(0, prompt.CursorPositionForTest);

		Press(prompt, ConsoleKey.End);
		Assert.Equal(("ab" + TokenLabel + "cd").Length, prompt.CursorPositionForTest);
		GC.KeepAlive(system);
	}

	/// <summary>
	/// The token colours are ordinary properties with the reactive setter contract: setting one
	/// changes what the next paint produces, with no call to Invalidate from the host.
	/// </summary>
	[Fact]
	public void TokenColors_RoundTripThroughTheProperties()
	{
		var prompt = new PromptControl { TokenForegroundColor = Color.Red, TokenBackgroundColor = Color.Blue };

		Assert.Equal(Color.Red, prompt.TokenForegroundColor);
		Assert.Equal(Color.Blue, prompt.TokenBackgroundColor);
	}

	[Fact]
	public void TokenBackgroundColor_PaintedBehindTheLabel()
	{
		var (system, prompt) = HostMultiline();
		prompt.InputBackgroundColor = Color.Black;
		prompt.TokenBackgroundColor = Color.Blue;
		SetContent(prompt,
			PromptSegment.FromText("ab"),
			PromptSegment.FromToken(Token()),
			PromptSegment.FromText("cd"));
		system.Render.UpdateDisplay();

		var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
		for (int x = 2; x < 2 + TokenLabel.Length; x++)
			Assert.Equal(Color.Blue, snap.GetCell(x, 0).Background);
		GC.KeepAlive(system);
	}

	/// <summary>
	/// Single-line mode paints the same way. The two paths build their cells separately, so a token
	/// coloured in one and not the other is a live possibility.
	/// </summary>
	[Fact]
	public void TokenLabel_PaintedInTokenColor_InSingleLineMode()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 8);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 8 };
		var prompt = new PromptControl
		{
			Prompt = string.Empty,
			InputForegroundColor = Color.White,
			TokenForegroundColor = Color.Red,
		};
		window.AddControl(prompt);
		system.AddWindow(window);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		SetContent(prompt,
			PromptSegment.FromText("ab"),
			PromptSegment.FromToken(Token()),
			PromptSegment.FromText("cd"));
		system.Render.UpdateDisplay();

		Assert.Equal(Color.White, Foreground(system, 0, 0));
		for (int x = 2; x < 2 + TokenLabel.Length; x++)
			Assert.Equal(Color.Red, Foreground(system, x, 0));
		GC.KeepAlive(system);
	}

	#endregion

	#region Insert, paste filter, expand and activate

	[Fact]
	public void InsertToken_AtCaret_AppearsAsOneSegment()
	{
		var prompt = new PromptControl();
		prompt.SetInput("ab");

		prompt.InsertToken(new PromptToken("[T]", "value"));

		Assert.Equal(2, prompt.InputSegments.Count);
		Assert.True(prompt.InputSegments[1].IsToken);
		Assert.Equal("abvalue", prompt.Input);
		Assert.Equal("ab[T]", prompt.InputLabelText);
	}

	[Fact]
	public void InsertToken_WithSelection_ReplacesSelection()
	{
		var prompt = new PromptControl();
		prompt.SetInput("abcd");
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow, shift: true);
		Press(prompt, ConsoleKey.RightArrow, shift: true);

		prompt.InsertToken(new PromptToken("[T]", "value"));

		Assert.Equal("valuecd", prompt.Input);
	}

	[Fact]
	public void InsertToken_Null_Throws()
	{
		var prompt = new PromptControl();

		Assert.Throws<ArgumentNullException>(() => prompt.InsertToken(null!));
	}

	[Fact]
	public void Paste_WithFilterReturningToken_InsertsTokenNotText()
	{
		var prompt = new PromptControl { PasteFilter = text => new PromptToken("[P]", text) };

		prompt.Paste("a long pasted body");

		Assert.Single(prompt.InputSegments);
		Assert.True(prompt.InputSegments[0].IsToken);
		Assert.Equal("a long pasted body", prompt.Input);
		Assert.Equal("[P]", prompt.InputLabelText);
	}

	[Fact]
	public void Paste_WithFilterReturningNull_PastesAsTextAsToday()
	{
		var prompt = new PromptControl { PasteFilter = _ => null };

		prompt.Paste("plain");

		Assert.Equal("plain", prompt.Input);
		Assert.DoesNotContain(prompt.InputSegments, seg => seg.IsToken);
	}

	/// <summary>
	/// The filter is the only thing that can create a token from a paste. With none set, the control
	/// must behave exactly as it did before tokens existed.
	/// </summary>
	[Fact]
	public void Paste_WithNoFilter_BehavesExactlyAsToday()
	{
		var prompt = new PromptControl();

		prompt.Paste("a\nb");

		Assert.Equal("a b", prompt.Input);
		Assert.DoesNotContain(prompt.InputSegments, seg => seg.IsToken);
	}

	[Fact]
	public void ExpandTokenAtCaret_ReplacesTokenWithValue_CaretAtEndOfInsertedText()
	{
		var prompt = WithToken("x ", " y");
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow);

		Assert.True(prompt.ExpandTokenAtCaret());

		Assert.Equal($"x {TokenValue} y", prompt.Input);
		Assert.DoesNotContain(prompt.InputSegments, seg => seg.IsToken);
	}

	/// <summary>
	/// Expanding puts the whole VALUE into the box, which can be far longer than the label it
	/// replaces. A cap that would be blown is refused rather than clamped: half a pasted body is
	/// not something the user asked for, and silently truncating it loses the rest with no event.
	/// </summary>
	[Fact]
	public void ExpandTokenAtCaret_ExceedingMaxLength_IsRefused_AndChangesNothing()
	{
		var prompt = new PromptControl { MaxLength = 12 };
		prompt.InsertToken(new PromptToken("[P1]", new string('v', 400)));
		string before = prompt.Input;

		Assert.False(prompt.ExpandTokenAtCaret());

		Assert.Equal(before, prompt.Input);
		Assert.Contains(prompt.InputSegments, seg => seg.IsToken);
	}


	[Fact]
	public void ExpandTokenAtCaret_NotAtToken_ReturnsFalse_AndChangesNothing()
	{
		var prompt = new PromptControl();
		prompt.SetInput("plain");

		Assert.False(prompt.ExpandTokenAtCaret());
		Assert.Equal("plain", prompt.Input);
	}

	[Fact]
	public void MaxLength_CountsLabelLength_NotValueLength()
	{
		// The label is 4 columns and the value is far longer. A cap above the label but below the
		// value must still admit the token, because the label is what occupies the box.
		var prompt = new PromptControl { MaxLength = 10 };

		prompt.InsertToken(new PromptToken("[P1]", new string('v', 200)));

		Assert.Single(prompt.InputSegments);
		Assert.Equal("[P1]", prompt.InputLabelText);
	}

	[Fact]
	public void InsertToken_ExceedingMaxLength_IsRefused_SegmentsUnchanged()
	{
		var prompt = new PromptControl { MaxLength = 3 };
		prompt.SetInput("ab");

		prompt.InsertToken(new PromptToken("[LongLabel]", "v"));

		Assert.Equal("ab", prompt.Input);
		Assert.DoesNotContain(prompt.InputSegments, seg => seg.IsToken);
	}

	/// <summary>
	/// Lowering the cap under content that is already there must not corrupt the segment list. The
	/// control truncates rather than rejecting, and a token is kept or dropped whole.
	/// </summary>
	[Fact]
	public void MaxLengthLowered_BelowExistingContent_DoesNotCorruptSegments()
	{
		var prompt = WithToken("abc", "def");

		prompt.MaxLength = 2;

		foreach (var segment in prompt.InputSegments)
			Assert.True(segment.IsToken ? segment.Token is not null : segment.Text is not null);
	}

	#endregion

	#region History and draft

	/// <summary>
	/// An entry recorded on Enter keeps its tokens, and Up brings the token back AS a token, still
	/// holding its value. Without this a recalled message would send a label where the user meant
	/// the pasted body.
	/// </summary>
	[Fact]
	public void UpFromHistory_RestoresTokenAsToken_WithItsValue()
	{
		var prompt = new PromptControl { HistoryEnabled = true };
		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromText("send "),
			PromptSegment.FromToken(Token()),
		});

		Press(prompt, ConsoleKey.Enter);
		Press(prompt, ConsoleKey.UpArrow);

		Assert.Contains(prompt.InputSegments, seg => seg.IsToken);
		Assert.Equal($"send {TokenValue}", prompt.Input);
	}

	/// <summary>
	/// The string history is existing public API and keeps its shape, projecting each token to the
	/// value a caller would have sent.
	/// </summary>
	[Fact]
	public void History_StringProperty_ProjectsTokensToValues()
	{
		var prompt = new PromptControl { HistoryEnabled = true };
		prompt.SetInputSegments(new[] { PromptSegment.FromToken(Token()) });

		Press(prompt, ConsoleKey.Enter);

		Assert.Single(prompt.History);
		Assert.Equal(TokenValue, prompt.History[0]);
	}

	/// <summary>
	/// The draft a user had typed is stashed when they walk into history and restored when they
	/// walk back out. A token in that draft must survive the round trip.
	/// </summary>
	[Fact]
	public void DraftStash_KeepsTokens_WhenHistoryNavigatedAndReturned()
	{
		var prompt = new PromptControl { HistoryEnabled = true };
		prompt.SetInput("an earlier message");
		Press(prompt, ConsoleKey.Enter);

		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromText("draft "),
			PromptSegment.FromToken(Token()),
		});

		Press(prompt, ConsoleKey.UpArrow);
		Press(prompt, ConsoleKey.DownArrow);

		Assert.Contains(prompt.InputSegments, seg => seg.IsToken);
		Assert.Equal($"draft {TokenValue}", prompt.Input);
	}

	#endregion


	#region Mode switching

	/// <summary>
	/// Turning multiline off flattens newlines, which must not cost the tokens their values. The
	/// trigger is a newline anywhere in the content, not the token, so a composer holding a pasted
	/// chip plus a typed newline would otherwise send the label where the user meant the paste.
	/// </summary>
	[Fact]
	public void LeavingMultiline_KeepsTokensAndTheirValues()
	{
		var prompt = new PromptControl { Multiline = true };
		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromText("before\n"),
			PromptSegment.FromToken(Token()),
			PromptSegment.FromText("\nafter"),
		});

		prompt.Multiline = false;

		Assert.Contains(prompt.InputSegments, seg => seg.IsToken);
		Assert.Equal($"before {TokenValue} after", prompt.Input);
	}

	/// <summary>Flattening still happens: the newlines around the token become spaces.</summary>
	[Fact]
	public void LeavingMultiline_StillFlattensNewlinesAroundAToken()
	{
		var prompt = new PromptControl { Multiline = true };
		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromText("a\nb"),
			PromptSegment.FromToken(Token()),
		});

		prompt.Multiline = false;

		Assert.DoesNotContain("\n", prompt.InputLabelText);
		Assert.Equal($"a b{TokenLabel}", prompt.InputLabelText);
	}

	#endregion


	#region Adjacent tokens

	/// <summary>
	/// Two tokens with nothing between them: the caret between the pair touches both, and expanding
	/// must take the one behind, which is the one the caret just stepped over.
	/// </summary>
	[Fact]
	public void ExpandTokenAtCaret_BetweenTwoTokens_TakesTheOneBehind()
	{
		var first = new PromptToken("[A]", "alpha");
		var second = new PromptToken("[B]", "beta");
		var prompt = new PromptControl();
		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromToken(first),
			PromptSegment.FromToken(second),
		});

		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);

		Assert.True(prompt.ExpandTokenAtCaret());

		Assert.Equal("alpha[B]", prompt.InputLabelText);
		Assert.Equal("alphabeta", prompt.Input);
	}

	#endregion


	#region Ctrl+W over-deletion

	/// <summary>The reviewer's exact case: "a [Pasted text] b" with the caret at the end.</summary>
	[Fact]
	public void CtrlW_AtEnd_AfterTokenAndSpace_RemovesOnlyTheLastWord()
	{
		var prompt = new PromptControl();
		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromText("a "),
			PromptSegment.FromToken(new PromptToken("[Pasted text]", "body")),
			PromptSegment.FromText(" b"),
		});

		Press(prompt, ConsoleKey.W, ctrl: true);

		Assert.Contains(prompt.InputSegments, seg => seg.IsToken);
		Assert.Equal("a [Pasted text] ", prompt.InputLabelText);
	}


	/// <summary>
	/// Ctrl+W kills ONE word. With a token before the caret it must take the word only, leaving the
	/// token and the text before it alone. The existing Ctrl+W tests assert DoesNotContain, which
	/// an empty buffer satisfies, so they cannot see over-deletion.
	/// </summary>
	[Fact]
	public void CtrlW_WithTokenEarlier_RemovesOnlyTheWord()
	{
		var prompt = new PromptControl();
		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromText("a "),
			PromptSegment.FromToken(new PromptToken("[Pasted text #1]", "body")),
			PromptSegment.FromText(" word"),
		});

		Press(prompt, ConsoleKey.W, ctrl: true);

		Assert.Contains(prompt.InputSegments, seg => seg.IsToken);
		Assert.Equal("a [Pasted text #1] ", prompt.InputLabelText);
	}

	#endregion


	#region TokenAtCaret

	/// <summary>
	/// A host whose tokens are not all alike asks what it is touching before deciding what a key
	/// means. The answer must be the one ExpandTokenAtCaret would act on, or the two disagree.
	/// </summary>
	[Fact]
	public void TokenAtCaret_AfterAToken_IsThatToken()
	{
		var prompt = WithToken("x ", string.Empty);

		Assert.NotNull(prompt.TokenAtCaret);
		Assert.Equal(TokenLabel, prompt.TokenAtCaret!.Label);
	}

	[Fact]
	public void TokenAtCaret_BeforeAToken_IsThatToken()
	{
		var prompt = WithToken("x ", " y");
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);
		Press(prompt, ConsoleKey.RightArrow);

		Assert.NotNull(prompt.TokenAtCaret);
		Assert.Equal(TokenLabel, prompt.TokenAtCaret!.Label);
	}

	/// <summary>
	/// Between two adjacent tokens the caret touches both, and the answer must be the one behind,
	/// which is what ExpandTokenAtCaret takes.
	/// </summary>
	[Fact]
	public void TokenAtCaret_BetweenTwoTokens_IsTheOneBehind()
	{
		var prompt = new PromptControl();
		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromToken(new PromptToken("[A]", "alpha")),
			PromptSegment.FromToken(new PromptToken("[B]", "beta")),
		});
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);

		Assert.Equal("[A]", prompt.TokenAtCaret!.Label);
	}

	[Fact]
	public void TokenAtCaret_InPlainText_IsNull()
	{
		var prompt = new PromptControl();
		prompt.SetInput("just text");

		Assert.Null(prompt.TokenAtCaret);
	}

	[Fact]
	public void TokenAtCaret_InAnEmptyComposer_IsNull()
	{
		Assert.Null(new PromptControl().TokenAtCaret);
	}

	/// <summary>
	/// Reading it must change nothing. The workaround it replaces expanded the token to find out
	/// what it was, which moved the caret and rewrote the line.
	/// </summary>
	[Fact]
	public void TokenAtCaret_ChangesNothing()
	{
		var prompt = WithToken("x ", " y");
		string inputBefore = prompt.Input;
		string labelBefore = prompt.InputLabelText;
		int segmentsBefore = prompt.InputSegments.Count;

		_ = prompt.TokenAtCaret;
		_ = prompt.TokenAtCaret;

		Assert.Equal(inputBefore, prompt.Input);
		Assert.Equal(labelBefore, prompt.InputLabelText);
		Assert.Equal(segmentsBefore, prompt.InputSegments.Count);
	}

	/// <summary>It agrees with what ExpandTokenAtCaret actually expands.</summary>
	[Fact]
	public void TokenAtCaret_AgreesWithWhatExpandTakes()
	{
		var prompt = new PromptControl();
		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromToken(new PromptToken("[A]", "alpha")),
			PromptSegment.FromToken(new PromptToken("[B]", "beta")),
		});
		MoveCaretToStart(prompt);
		Press(prompt, ConsoleKey.RightArrow);

		var reported = prompt.TokenAtCaret;
		Assert.True(prompt.ExpandTokenAtCaret());

		// The expanded one is gone and its value is in the text; the other token remains.
		Assert.Contains(reported!.Value, prompt.Input);
		Assert.Contains(prompt.InputSegments, seg => seg.IsToken);
	}

	#endregion

}

/// <summary>
/// The real copy path: Ctrl+C on a selection that covers a token must put the token's VALUE on the
/// clipboard, not the label the box shows.
/// </summary>
/// <remarks>
/// A separate class because it registers the OSC 52 emitter, which is process-global. Placed in the
/// same serial collection as the clipboard tests so the two cannot observe each other's writes.
/// </remarks>
[Collection("EnvSerial")]
public class PromptTokenClipboardTests : IDisposable
{
	private readonly List<string> _emitted = new();

	public PromptTokenClipboardTests()
	{
		ClipboardHelper.RegisterOsc52Emitter(s => _emitted.Add(s));
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
	}

	public void Dispose()
	{
		ClipboardHelper.DrainPendingWritesForTests();
		ClipboardHelper.RegisterOsc52Emitter(null);
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
	}

	[Fact]
	public void CtrlC_SelectionSpanningToken_CopiesTheValue_NotTheLabel()
	{
		const string label = "[Pasted text #1]";
		const string value = "a long pasted value no box would show";

		var prompt = new PromptControl();
		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromText("send "),
			PromptSegment.FromToken(new PromptToken(label, value)),
			PromptSegment.FromText(" now"),
		});

		// Ctrl+A then Ctrl+C: the real keyboard path, not the property read.
		prompt.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.A, false, false, true));
		prompt.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.C, false, false, true));

		Assert.Equal("send " + value + " now", ClipboardHelper.GetText());
		Assert.DoesNotContain(label, ClipboardHelper.GetText());
	}

	[Fact]
	public void CtrlX_SelectionSpanningToken_CutsTheTokenAndCopiesItsValue()
	{
		const string label = "[Pasted text #1]";
		const string value = "the value";

		var prompt = new PromptControl();
		prompt.SetInputSegments(new[]
		{
			PromptSegment.FromText("a "),
			PromptSegment.FromToken(new PromptToken(label, value)),
		});

		prompt.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.A, false, false, true));
		prompt.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.X, false, false, true));

		Assert.Equal("a " + value, ClipboardHelper.GetText());
		Assert.Equal(string.Empty, prompt.Input);
		Assert.DoesNotContain(prompt.InputSegments, s => s.IsToken);
	}

}

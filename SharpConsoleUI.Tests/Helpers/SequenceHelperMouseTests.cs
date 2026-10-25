using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Input;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// <see cref="SequenceHelper.GetMouse"/> decodes SGR bytes into the facts the terminal
/// reported: a button went down, came up, moved, or a wheel turned.
/// </summary>
/// <remarks>
/// It no longer synthesises clicks, double-clicks or drags. Those are gestures, derived
/// once for every platform by <see cref="MouseGestureRecognizer"/>, so the tests that
/// used to assert <c>Button1Clicked</c> here now drive the decoder and the recogniser
/// together - which is the path a real click actually takes.
/// </remarks>
public class SequenceHelperMouseTests
{
	// GetMouse still keeps a little press state, for the continuous-press loop that
	// repeats a held button. Reset it between tests rather than relying on timing.
	public SequenceHelperMouseTests()
	{
		Type type = typeof(SequenceHelper);
		type.GetField("_isButtonPressed", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
		type.GetField("_lastMouseButtonPressed", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
		type.GetField("_point", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
	}

	// Builds the ConsoleKeyInfo[] GetMouse expects from the raw SGR body (e.g. "<32;3;5M").
	private static ConsoleKeyInfo[] Cki(string seq) =>
		seq.Select(ch => new ConsoleKeyInfo(ch, default, false, false, false)).ToArray();

	private static void NoOp(MouseFlags f, Point p) { }

	/// <summary>Decodes a sequence and runs it through the recogniser, as the coordinator does.</summary>
	private static List<MouseFlags> Decode(MouseGestureRecognizer recognizer, string seq)
	{
		SequenceHelper.GetMouse(Cki(seq), out List<MouseFlags> flags, out var pos, NoOp);
		return recognizer.Recognize(flags, pos);
	}

	#region What the decoder reports

	[Fact]
	public void GetMouse_PlainPress_ReportsAPress()
	{
		SequenceHelper.GetMouse(Cki("<0;3;1M"), out List<MouseFlags> flags, out var pos, NoOp);

		Assert.True(flags[0].HasFlag(MouseFlags.Button1Pressed), $"flags were {flags[0]}");
		Assert.Equal(2, pos.X);   // 1-based 3 -> 0-based 2
		Assert.Equal(0, pos.Y);
	}

	[Fact]
	public void GetMouse_PlainPress_DoesNotSurfaceDragged()
	{
		// A fresh press (code 0, no motion bit) is not a drag.
		SequenceHelper.GetMouse(Cki("<0;3;1M"), out List<MouseFlags> flags, out _, NoOp);

		Assert.False(flags[0].HasFlag(MouseFlags.Button1Dragged), $"flags were {flags[0]}");
	}

	[Fact]
	public void GetMouse_MotionWhileButton1Held_SurfacesButton1Dragged()
	{
		// Motion-while-held (button code 32 = button 0 + motion bit 0x20) carries the
		// drag bit from the wire itself, so the decoder reports it without inferring
		// anything. Parity with the Unix path (#45).
		SequenceHelper.GetMouse(Cki("<0;3;1M"), out _, out _, NoOp);
		SequenceHelper.GetMouse(Cki("<32;3;5M"), out List<MouseFlags> flags, out var pos, NoOp);

		Assert.True(flags[0].HasFlag(MouseFlags.Button1Dragged), $"flags were {flags[0]}");
		Assert.True(flags[0].HasFlag(MouseFlags.ReportMousePosition), $"flags were {flags[0]}");
		Assert.Equal(2, pos.X);
		Assert.Equal(4, pos.Y);
	}

	#endregion

	#region Modifiers survive into the gesture

	[Fact]
	public void CtrlClick_SurfacesButton1ClickedWithCtrl()
	{
		// SGR code 16 = button 0 + Ctrl (0x10). The modifier used to be OR'd into the
		// value the state machine compared against Button1Pressed, so the comparison
		// never matched and the click was silently dropped. The decoder must still
		// carry the modifier through, and the gesture must still be recognised.
		var r = new MouseGestureRecognizer();

		Decode(r, "<16;3;1M");
		var flags = Decode(r, "<16;3;1m");

		Assert.Contains(MouseFlags.Button1Clicked, flags);
		Assert.True(
			flags.Exists(f => f.HasFlag(MouseFlags.ButtonCtrl)),
			$"flags were {string.Join(",", flags)}");
	}

	[Fact]
	public void ShiftClick_SurfacesButton1ClickedWithShift()
	{
		// SGR code 4 = button 0 + Shift (0x04) with no Alt/Ctrl. A hand-enumerated
		// switch table once omitted the bare-Shift code entirely, so the press was
		// never recognised and the whole click vanished.
		var r = new MouseGestureRecognizer();

		Decode(r, "<4;3;1M");
		var flags = Decode(r, "<4;3;1m");

		Assert.Contains(MouseFlags.Button1Clicked, flags);
		Assert.True(
			flags.Exists(f => f.HasFlag(MouseFlags.ButtonShift)),
			$"flags were {string.Join(",", flags)}");
	}

	#endregion

	#region The decoder and recogniser together

	[Fact]
	public void AFastRunOfClicks_KeepsEveryPress()
	{
		// The whole reason the state machine moved. Four fast clicks must produce
		// four presses, not press/double/triple/press with two of them lost.
		var r = new MouseGestureRecognizer();
		int presses = 0;

		for (int i = 0; i < 4; i++)
		{
			if (Decode(r, "<0;3;1M").Contains(MouseFlags.Button1Pressed)) presses++;
			Decode(r, "<0;3;1m");
		}

		Assert.Equal(4, presses);
	}

	[Fact]
	public void ASecondFastClick_IsAlsoADoubleClick()
	{
		var r = new MouseGestureRecognizer();

		Decode(r, "<0;3;1M");
		Decode(r, "<0;3;1m");
		var second = Decode(r, "<0;3;1M");

		Assert.Contains(MouseFlags.Button1Pressed, second);
		Assert.Contains(MouseFlags.Button1DoubleClicked, second);
	}

	#endregion
}

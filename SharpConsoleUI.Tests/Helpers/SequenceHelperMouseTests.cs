using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class SequenceHelperMouseTests
{
	// SequenceHelper.GetMouse tracks click/press/drag state in private static fields shared across
	// every call in the process. xUnit runs test classes without an explicit [Collection] in parallel
	// with each other by default, and even sequentially within this class the click tests' async
	// DefaultDebounceMs (300ms) reset could still be pending when the next test starts under CI load,
	// so a fresh test would inherit e.g. a lingering _isButtonClicked and be misread as a double-click.
	// Resetting these fields directly via reflection before every test is deterministic and avoids
	// relying on wall-clock sleeps racing the debounce timer.
	public SequenceHelperMouseTests()
	{
		Type type = typeof(SequenceHelper);
		type.GetField("_isButtonClicked", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
		type.GetField("_isButtonDoubleClicked", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
		type.GetField("_isButtonPressed", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
		type.GetField("_isButtonTripleClicked", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
		type.GetField("_lastMouseButtonPressed", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
		type.GetField("_point", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
		type.GetField("_lastClickTime", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, DateTime.MinValue);
	}

	// Builds the ConsoleKeyInfo[] GetMouse expects from the raw SGR body (e.g. "<32;3;5M").
	private static ConsoleKeyInfo[] Cki(string seq) =>
		seq.Select(ch => new ConsoleKeyInfo(ch, default, false, false, false)).ToArray();

	private static void NoOp(MouseFlags f, System.Drawing.Point p) { }

	[Fact]
	public void GetMouse_MotionWhileButton1Held_SurfacesButton1Dragged()
	{
		// The Windows Console.ReadKey path decodes SGR mouse via SequenceHelper. For parity with the
		// Unix AnsiInputParser, a motion-while-held report (button code 32 = button 0 + motion bit 0x20)
		// must surface Button1Dragged alongside Button1Pressed|ReportMousePosition — otherwise drag-aware
		// controls see a different flag set on Windows than on Linux (#45).

		// Press first so GetMouse's internal _isButtonPressed state treats the next report as a drag.
		SequenceHelper.GetMouse(Cki("<0;3;1M"), out _, out _, NoOp);

		SequenceHelper.GetMouse(Cki("<32;3;5M"), out List<MouseFlags> flags, out var pos, NoOp);

		Assert.True(flags[0].HasFlag(MouseFlags.Button1Dragged), $"flags were {flags[0]}");
		Assert.True(flags[0].HasFlag(MouseFlags.ReportMousePosition), $"flags were {flags[0]}");
		Assert.Equal(2, pos.X); // 1-based 3 -> 0-based 2
		Assert.Equal(4, pos.Y); // 1-based 5 -> 0-based 4
	}

	[Fact]
	public void GetMouse_PlainPress_DoesNotSurfaceDragged()
	{
		// A fresh button press (code 0, no motion bit) must not be reported as a drag.
		SequenceHelper.GetMouse(Cki("<0;3;1M"), out List<MouseFlags> flags, out _, NoOp);
		Assert.False(flags[0].HasFlag(MouseFlags.Button1Dragged), $"flags were {flags[0]}");
	}

	[Fact]
	public void GetMouse_CtrlClick_SurfacesButton1ClickedWithCtrl()
	{
		// SGR code 16 = button 0 (Button1) + Ctrl bit (0x10). buttonState used to be compared with
		// `== MouseFlags.Button1Pressed/Released` while still carrying the OR'd-in ButtonCtrl bit, so
		// the comparison never matched and Button1Clicked was never synthesized (mirrors the
		// UnixStdinReader ExtractButtonState bug fixed in fc0948ec, but in this separate Windows path).
		SequenceHelper.GetMouse(Cki("<16;3;1M"), out _, out _, NoOp);
		SequenceHelper.GetMouse(Cki("<16;3;1m"), out List<MouseFlags> flags, out _, NoOp);

		Assert.Contains(MouseFlags.Button1Clicked, flags);
		Assert.True(flags[0].HasFlag(MouseFlags.ButtonCtrl), $"flags were {string.Join(",", flags)}");
	}

	[Fact]
	public void GetMouse_ShiftClick_SurfacesButton1ClickedWithShift()
	{
		// SGR code 4 = button 0 (Button1) + Shift bit (0x04) alone, with no Alt/Ctrl. The old
		// hand-enumerated switch table never listed a bare-Shift code for Button1/Button2 at all
		// (only Shift combined with Alt and/or Ctrl was covered for Button3), so buttonState stayed
		// at its default 0 and the press/release/click were never recognized — the click was
		// silently dropped instead of just missing its modifier.
		SequenceHelper.GetMouse(Cki("<4;3;1M"), out _, out _, NoOp);
		SequenceHelper.GetMouse(Cki("<4;3;1m"), out List<MouseFlags> flags, out _, NoOp);

		Assert.Contains(MouseFlags.Button1Clicked, flags);
		Assert.True(flags[0].HasFlag(MouseFlags.ButtonShift), $"flags were {string.Join(",", flags)}");
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class SequenceHelperMouseTests
{
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
}

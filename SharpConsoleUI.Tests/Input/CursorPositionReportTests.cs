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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Drivers.Input;
using Xunit;

namespace SharpConsoleUI.Tests.Input;

/// <summary>
/// A cursor-position report is the terminal ANSWERING a DSR query (<c>ESC[6n</c>), not a keystroke —
/// and because a terminal replies on the input stream, it arrives looking exactly like one.
///
/// <para>It shares its final byte with F3, so a parser that only knows CSI-R-means-F3 delivered a
/// function key nobody pressed, and any bytes it failed to consume were painted as text. Seen live:
/// <c>R;3R 6;3R [46;3R</c> smeared across a transcript while TerminalCapabilities probed emoji
/// widths — one reply re-entered at several offsets, each leaving a different tail behind.</para>
///
/// <para>The parameters are the only thing separating the two, so these tests pin both directions:
/// a report must be swallowed, and every form of the F3 key must still arrive.</para>
/// </summary>
public class CursorPositionReportTests
{
	private static InputEvent[] Parse(string sequence)
	{
		var parser = new AnsiInputParser();
		var bytes = Encoding.ASCII.GetBytes(sequence);
		return parser.Parse(bytes, bytes.Length).ToArray();
	}

	private static KeyInputEvent[] Keys(string sequence) =>
		Parse(sequence).OfType<KeyInputEvent>().ToArray();

	// --- The report is swallowed ------------------------------------------------------------------

	[Theory]
	[InlineData("\x1b[46;3R")]    // the live case
	[InlineData("\x1b[1;80R")]    // row 1, but a column no modifier code reaches
	[InlineData("\x1b[24;80R")]
	[InlineData("\x1b[999;999R")]
	public void ACursorPositionReportProducesNoKey(string sequence)
	{
		Assert.Empty(Keys(sequence));
	}

	[Fact]
	public void ARepeatedReportLeavesNothingBehind()
	{
		// The live symptom was fragments accumulating, so one report must not leak into the next.
		Assert.Empty(Keys("\x1b[46;3R\x1b[46;3R\x1b[46;3R"));
	}

	[Fact]
	public void AReportBetweenTwoKeystrokesDoesNotDisturbThem()
	{
		// The report arrives mid-stream, unannounced, while the user is typing.
		var keys = Keys("a\x1b[46;3Rb");

		Assert.Equal(2, keys.Length);
		Assert.Equal('a', keys[0].KeyInfo.KeyChar);
		Assert.Equal('b', keys[1].KeyInfo.KeyChar);
	}

	// --- F3 still works ---------------------------------------------------------------------------

	[Fact]
	public void BareCsiRIsStillF3()
	{
		// No parameters at all: unambiguously the key.
		var keys = Keys("\x1b[R");

		Assert.Single(keys);
		Assert.Equal(System.ConsoleKey.F3, keys[0].KeyInfo.Key);
	}

	[Theory]
	[InlineData("\x1b[1;2R", false, true)]    // Shift+F3
	[InlineData("\x1b[1;5R", true, false)]    // Ctrl+F3
	public void ModifiedF3IsStillAKey(string sequence, bool ctrl, bool shift)
	{
		// The xterm modifier form is `ESC[1;<mod>R` — first parameter the literal 1, second a code in
		// 2..16. It looks like a report and must not be swallowed as one.
		var keys = Keys(sequence);

		Assert.Single(keys);
		Assert.Equal(System.ConsoleKey.F3, keys[0].KeyInfo.Key);
		Assert.Equal(ctrl, keys[0].KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Control));
		Assert.Equal(shift, keys[0].KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Shift));
	}

	[Fact]
	public void SS3RIsStillF3()
	{
		// `ESC O R` is a different encoding of F3 and carries no parameters, so it is untouched by
		// the CSI rule — pinned so a later change cannot conflate the two paths.
		var keys = Keys("\x1bOR");

		Assert.Single(keys);
		Assert.Equal(System.ConsoleKey.F3, keys[0].KeyInfo.Key);
	}

	// --- The real thing --------------------------------------------------------------------------

	/// <summary>
	/// END TO END, through the actual stdin read loop rather than the parser alone.
	///
	/// <para>Required by this repo's testing rule, and it earns its keep here: the isolated tests
	/// above assert what <see cref="AnsiInputParser"/> returns, but the defect is only visible in
	/// what the APPLICATION receives. A report reaching the app as an F3 keypress is the failure —
	/// the app cannot tell it apart from a real one, and the key it triggers is whatever F3 is bound
	/// to. Driving raw bytes through UnixStdinReader is the path a live terminal takes.</para>
	///
	/// <para>The bytes are interleaved with real keystrokes, because that is how it happens: the
	/// probe's reply lands mid-stream while the user is typing.</para>
	/// </summary>
	[Fact]
	public void ThroughTheRealReadLoop_AReportNeverReachesTheApplication()
	{
		var stdin = new MemoryStream(Encoding.ASCII.GetBytes("a\x1b[46;3Rb"));
		var reader = new UnixStdinReader(stdin, new AnsiInputParser());

		var received = new List<ConsoleKeyInfo>();
		reader.ReadLoop(
			CancellationToken.None,
			key => received.Add(key),
			(_, _) => { },
			_ => { },
			(_, _) => { });

		// Both real keystrokes arrive; the report between them does not.
		Assert.Equal(2, received.Count);
		Assert.Equal('a', received[0].KeyChar);
		Assert.Equal('b', received[1].KeyChar);
		Assert.DoesNotContain(received, k => k.Key == ConsoleKey.F3);
	}
}

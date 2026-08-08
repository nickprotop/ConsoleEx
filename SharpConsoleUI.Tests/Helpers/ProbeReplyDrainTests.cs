// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// Probing must not leave replies behind for the application's key parser to find.
///
/// <para>Terminal capability probing writes DSR queries (<c>ESC[6n</c>) and reads the cursor-position
/// replies. The replies arrive on the INPUT stream — the same stream the app reads keystrokes from —
/// so any reply a probe does not consume is handed to the key parser next.</para>
///
/// <para>Draining only the parse-failure paths was not enough, and this is the case that proved it:
/// when every probe SUCCEEDS, each reader consumes exactly one reply and returns, so a reply that
/// arrives late — after its own reader timed out — is simply left queued. Live, that painted
/// <c>3R ;3R [46;3R</c> into a transcript. The FRAGMENTS are the tell: a whole <c>ESC[46;3R</c> is
/// recognisable and can be discarded downstream, but a tail whose ESC was already eaten by a probe
/// reader is indistinguishable from typed text.</para>
/// </summary>
public class ProbeReplyDrainTests
{
	/// <summary>A byte source that replays a script, then reports "nothing there" (-1) forever.</summary>
	private sealed class ScriptedInput
	{
		private readonly Queue<int> _bytes = new();

		public int Reads { get; private set; }

		public void Feed(string text)
		{
			foreach (var c in text) _bytes.Enqueue(c);
		}

		public int ReadByte()
		{
			Reads++;
			return _bytes.Count > 0 ? _bytes.Dequeue() : -1;
		}

		public int Remaining => _bytes.Count;
	}

	[Fact]
	public void AReplyLeftOverAfterProbingIsConsumed()
	{
		// Four probes go out. Feed FIVE replies: the extra one models a reply that arrived after its
		// reader had already timed out and moved on — exactly the live failure.
		var input = new ScriptedInput();
		for (var i = 0; i < 5; i++) input.Feed("\x1b[46;3R");

		TerminalCapabilities.Probe(write: _ => { }, readByte: input.ReadByte);

		// Nothing may be left for the key parser to pick up and paint.
		Assert.Equal(0, input.Remaining);
	}

	[Fact]
	public void ProbingStopsAtTheFirstQuietRead_AndDoesNotSpin()
	{
		// The drain must end when the stream goes quiet rather than reading to its cap — otherwise
		// every startup pays for a bounded-but-pointless read loop.
		var input = new ScriptedInput();
		input.Feed("\x1b[46;3R");

		TerminalCapabilities.Probe(write: _ => { }, readByte: input.ReadByte);

		Assert.Equal(0, input.Remaining);

		// Generous, but far below the 256-byte cap: reaching that would mean the quiet read is not
		// ending the drain.
		Assert.True(input.Reads < 100, $"drained with {input.Reads} reads — expected it to stop at the first quiet read");
	}

	[Fact]
	public void ASilentTerminalIsNotAProblem()
	{
		// A terminal that answers nothing at all (no DSR support, or output redirected) must still
		// leave probing safe to call — it falls back to assumptions, and the drain finds nothing.
		var input = new ScriptedInput();

		TerminalCapabilities.Probe(write: _ => { }, readByte: input.ReadByte);

		Assert.Equal(0, input.Remaining);
	}
}

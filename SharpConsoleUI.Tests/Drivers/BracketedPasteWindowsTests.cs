using System.Text;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// Tests for the Windows ReadKey-path bracketed-paste marker logic (issue #42). The Windows
/// input path now recognizes ESC[200~..ESC[201~ and raises Paste atomically, mirroring the Unix
/// AnsiInputParser. These cover the pure marker-extraction helpers (no live console needed).
/// </summary>
public class BracketedPasteWindowsTests
{
	[Fact]
	public void TryExtract_EndMarkerPresent_ReturnsPayloadWithoutMarker()
	{
		var sb = new StringBuilder("Привет\x1b[201~");
		bool done = NetConsoleDriver.TryExtractBracketedPaste(sb, out string payload);
		Assert.True(done);
		Assert.Equal("Привет", payload);
	}

	[Fact]
	public void TryExtract_NoEndMarker_ReturnsFalse()
	{
		var sb = new StringBuilder("Привет");
		bool done = NetConsoleDriver.TryExtractBracketedPaste(sb, out string payload);
		Assert.False(done);
		Assert.Equal(string.Empty, payload);
	}

	[Fact]
	public void TryExtract_PartialEndMarker_ReturnsFalse()
	{
		// Only "ESC[201" so far — the trailing '~' hasn't arrived yet.
		var sb = new StringBuilder("text\x1b[201");
		bool done = NetConsoleDriver.TryExtractBracketedPaste(sb, out _);
		Assert.False(done);
	}

	[Fact]
	public void TryExtract_EmptyPaste_ReturnsEmptyPayload()
	{
		var sb = new StringBuilder("\x1b[201~");
		bool done = NetConsoleDriver.TryExtractBracketedPaste(sb, out string payload);
		Assert.True(done);
		Assert.Equal(string.Empty, payload);
	}

	[Fact]
	public void TryExtract_MultilineAndCjkPayload_Preserved()
	{
		string body = "line1\nline2 中文 📦";
		var sb = new StringBuilder(body + "\x1b[201~");
		bool done = NetConsoleDriver.TryExtractBracketedPaste(sb, out string payload);
		Assert.True(done);
		Assert.Equal(body, payload);
	}

	[Fact]
	public void TryExtract_MarkerBytesInsidePayloadDoNotFalseTrigger()
	{
		// A literal "201~" in the middle (not preceded by ESC[ at the tail) must not end the paste.
		var sb = new StringBuilder("a201~b");
		bool done = NetConsoleDriver.TryExtractBracketedPaste(sb, out _);
		Assert.False(done);
	}

	[Fact]
	public void StripEndMarker_TrailingFullMarker_Removed()
	{
		Assert.Equal("abc", NetConsoleDriver.StripPasteEndMarker("abc\x1b[201~"));
	}

	[Fact]
	public void StripEndMarker_TrailingPartialMarker_Removed()
	{
		Assert.Equal("abc", NetConsoleDriver.StripPasteEndMarker("abc\x1b[20"));
	}

	[Fact]
	public void StripEndMarker_NoMarker_Unchanged()
	{
		Assert.Equal("abc", NetConsoleDriver.StripPasteEndMarker("abc"));
	}

	// --- CSI/SS3 sequence reader (issue #42, consecutive-paste regression) ---------------------
	//
	// The Windows ReadKey path used to collect the bytes after ESC[ with `while (Console.KeyAvailable)`,
	// which bails the instant the next byte is momentarily unavailable. On back-to-back pastes the
	// terminal can split the ESC[200~ start marker across console-input chunks, so the loop saw e.g.
	// "20" and gave up — the rest of the paste ("0~中文…") then leaked through the per-char parser and
	// garbled. The fix reads via a gap-tolerant IConsoleKeySource that waits briefly for each byte.
	// These tests drive ReadAnsiSequence with a fake source so no live console is needed.

	/// <summary>Fake key source. Each TryReadKey dequeues the next scripted key; an empty queue
	/// returns false (mimicking the bounded wait expiring on a genuinely incomplete sequence).</summary>
	private sealed class FakeKeySource : NetConsoleDriver.IConsoleKeySource
	{
		private readonly Queue<ConsoleKeyInfo> _keys;

		public FakeKeySource(string chars)
		{
			_keys = new Queue<ConsoleKeyInfo>(
				chars.Select(c => new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false)));
		}

		public bool TryReadKey(out ConsoleKeyInfo key)
		{
			if (_keys.Count == 0) { key = default; return false; }
			key = _keys.Dequeue();
			return true;
		}

		public char ReadNext() => _keys.Dequeue().KeyChar;
	}

	[Fact]
	public void ReadAnsiSequence_CollectsFullStartMarker()
	{
		// The whole "200~" must be returned — this is what gets compared against "200~" to detect a
		// bracketed-paste start. Splitting it (the old bug) is what garbled consecutive pastes.
		var src = new FakeKeySource("200~");
		string result = NetConsoleDriver.ReadAnsiSequence(src, new List<ConsoleKeyInfo>());
		Assert.Equal("200~", result);
	}

	[Fact]
	public void ReadAnsiSequence_StopsAtFinalByte_DoesNotConsumePayload()
	{
		// After the '~' final byte the reader must stop, leaving the paste payload for the dedicated
		// payload reader. Over-reading here would swallow the first payload chars.
		var src = new FakeKeySource("200~中文\x1b[201~");
		string result = NetConsoleDriver.ReadAnsiSequence(src, new List<ConsoleKeyInfo>());
		Assert.Equal("200~", result);
		Assert.Equal('中', src.ReadNext()); // payload still intact
	}

	[Fact]
	public void ReadAnsiSequence_AppendsEveryByteToSequenceList()
	{
		var seq = new List<ConsoleKeyInfo>();
		NetConsoleDriver.ReadAnsiSequence(new FakeKeySource("200~"), seq);
		Assert.Equal("200~", new string(seq.Select(k => k.KeyChar).ToArray()));
	}

	[Fact]
	public void ReadAnsiSequence_SgrMouseSequence_ReadToFinalLetter()
	{
		// Non-paste CSI sequences must still frame correctly (final byte is a letter).
		var src = new FakeKeySource("<35;10;5M");
		Assert.Equal("<35;10;5M", NetConsoleDriver.ReadAnsiSequence(src, new List<ConsoleKeyInfo>()));
	}

	[Fact]
	public void ReadAnsiSequence_IncompleteSequence_TerminatesAtSourceExhaustion()
	{
		// A lone ESC[ (e.g. Alt+[) yields no final byte. The reader must return what it has rather
		// than hang — here the source is empty, modelling the bounded wait expiring.
		var src = new FakeKeySource("");
		Assert.Equal(string.Empty, NetConsoleDriver.ReadAnsiSequence(src, new List<ConsoleKeyInfo>()));
	}

	[Fact]
	public void ReadAnsiSequence_RunawaySequence_BoundedByMaxChars()
	{
		// A malformed sequence with no final byte must not grow unbounded.
		var src = new FakeKeySource(new string('0', NetConsoleDriver.MaxAnsiSequenceChars * 2));
		string result = NetConsoleDriver.ReadAnsiSequence(src, new List<ConsoleKeyInfo>());
		Assert.Equal(NetConsoleDriver.MaxAnsiSequenceChars, result.Length);
	}
}

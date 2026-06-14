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
}

using System.Text;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class Osc52Tests
{
	[Fact]
	public void BuildSequence_Plain_IsByteExact()
	{
		// "Hello" -> base64 "SGVsbG8="
		var seq = Osc52.BuildSequence("Hello", tmuxWrap: false, maxBytes: 1000);
		Assert.Equal("\x1b]52;c;SGVsbG8=\x07", seq);
	}

	[Fact]
	public void BuildSequence_UsesClipboardSelection_c()
	{
		var seq = Osc52.BuildSequence("x", tmuxWrap: false, maxBytes: 1000)!;
		Assert.StartsWith("\x1b]52;c;", seq);
	}

	[Fact]
	public void BuildSequence_RoundTripsUtf8AndEmoji()
	{
		string text = "café 📦 漢字";
		var seq = Osc52.BuildSequence(text, tmuxWrap: false, maxBytes: 10000)!;
		int start = seq.IndexOf(";c;", StringComparison.Ordinal) + 3;
		int end = seq.IndexOf('\x07');
		string b64 = seq.Substring(start, end - start);
		string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
		Assert.Equal(text, decoded);
	}

	[Fact]
	public void BuildSequence_TmuxWrap_DoublesEscAndWraps()
	{
		var seq = Osc52.BuildSequence("Hi", tmuxWrap: true, maxBytes: 1000)!;
		Assert.Equal("\x1bPtmux;\x1b\x1b]52;c;SGk=\x07\x1b\\", seq);
	}

	[Fact]
	public void BuildSequence_OverMaxBytes_ReturnsNull()
	{
		string text = new string('a', 100);
		Assert.Null(Osc52.BuildSequence(text, tmuxWrap: false, maxBytes: 10));
	}

	[Fact]
	public void BuildSequence_AtExactCap_Emits()
	{
		string text = "abc"; // base64 "YWJj" = 4 chars
		Assert.NotNull(Osc52.BuildSequence(text, tmuxWrap: false, maxBytes: 4));
		Assert.Null(Osc52.BuildSequence(text, tmuxWrap: false, maxBytes: 3));
	}

	[Fact]
	public void BuildSequence_NullInput_TreatedAsEmpty()
	{
		var seq = Osc52.BuildSequence(null!, tmuxWrap: false, maxBytes: 1000);
		Assert.Equal("\x1b]52;c;\x07", seq);
	}

	[Fact]
	public void BuildSequence_EmptyString_EmitsEvenWithZeroCap()
	{
		// base64 of "" is "" (length 0), so 0 > maxBytes(0) is false -> emits.
		var seq = Osc52.BuildSequence("", tmuxWrap: false, maxBytes: 0);
		Assert.Equal("\x1b]52;c;\x07", seq);
	}
}

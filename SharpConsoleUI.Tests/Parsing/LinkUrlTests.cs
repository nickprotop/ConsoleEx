using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing
{
	public class LinkUrlTests
	{
		[Theory]
		[InlineData("https://x/y?a=1&b=2")]                 // clean — unchanged
		[InlineData("http://h/a b]c%d")]                     // space ] %
		[InlineData("http://h/(paren)[brk]%20already")]      // ( ) [ ] %
		[InlineData("https://e.com/path?q=a b c&z=]")]
		public void EscapeUnescape_RoundTrips(string url)
		{
			string escaped = LinkUrl.Escape(url);
			Assert.DoesNotContain(']', escaped);             // can't break the tag
			Assert.Equal(url, LinkUrl.Unescape(escaped));    // exact inverse
		}

		[Fact]
		public void Escape_CleanUrl_Unchanged()
		{
			const string url = "https://x/y?a=1&b=2";
			Assert.Equal(url, LinkUrl.Escape(url));
		}

		[Fact]
		public void Escape_EncodesPercentFirst_Lossless()
		{
			// a literal %5D must NOT decode back to ']'
			Assert.Equal("%255D", LinkUrl.Escape("%5D"));
			Assert.Equal("%5D", LinkUrl.Unescape("%255D"));
		}

		[Theory]
		[InlineData("%")]        // lone trailing %
		[InlineData("abc%xy")]   // % followed by non-hex
		[InlineData("abc%2")]    // % followed by only one hex digit (short tail)
		public void Unescape_MalformedPercent_LeftLiteral(string input)
		{
			Assert.Equal(input, LinkUrl.Unescape(input));
		}

		[Fact]
		public void NullInput_ReturnsEmpty()
		{
			Assert.Equal(string.Empty, LinkUrl.Escape(null!));
			Assert.Equal(string.Empty, LinkUrl.Unescape(null!));
		}

		[Fact]
		public void Unescape_LowercaseHex_DecodesCaseInsensitive()
		{
			Assert.Equal("]", LinkUrl.Unescape("%5d"));
		}

		[Fact]
		public void Escape_NonAscii_UnchangedAndRoundTrips()
		{
			const string url = "https://e.com/ü";
			string escaped = LinkUrl.Escape(url);
			Assert.Equal(url, escaped);                       // non-ASCII passes through
			Assert.Equal(url, LinkUrl.Unescape(escaped));     // round-trips
		}
	}
}

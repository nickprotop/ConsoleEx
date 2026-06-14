using System.Text;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// Regression tests for issue #42: the Windows clip.exe write path must produce UTF-16LE
/// with a BOM, NOT UTF-8. Forcing UTF-8 into clip.exe (db36415) garbled Cyrillic/CJK because
/// clip.exe reads BOM-less stdin as the OEM/ANSI code page.
/// </summary>
public class ClipboardWindowsEncodingTests
{
	[Fact]
	public void BuildUtf16BomBytes_StartsWithLittleEndianBom()
	{
		byte[] bytes = ClipboardHelper.BuildUtf16BomBytes("x");
		Assert.True(bytes.Length >= 2);
		Assert.Equal(0xFF, bytes[0]);
		Assert.Equal(0xFE, bytes[1]);
	}

	[Fact]
	public void BuildUtf16BomBytes_BodyRoundTripsAsUtf16LE()
	{
		string text = "Текст на русском"; // Cyrillic — the issue #42 repro
		byte[] bytes = ClipboardHelper.BuildUtf16BomBytes(text);

		// Decode skipping the 2-byte BOM; must reconstruct the original string exactly.
		string decoded = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
		Assert.Equal(text, decoded);
	}

	[Fact]
	public void BuildUtf16BomBytes_CjkAndEmojiRoundTrip()
	{
		string text = "中文测试 📦 漢字";
		byte[] bytes = ClipboardHelper.BuildUtf16BomBytes(text);
		string decoded = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
		Assert.Equal(text, decoded);
	}

	[Fact]
	public void BuildUtf16BomBytes_IsNotUtf8_ForNonAscii()
	{
		// The regression guard: the payload must NOT be the UTF-8 encoding of the text.
		// UTF-8 of a Cyrillic string differs in length and bytes from UTF-16LE+BOM.
		string text = "Привет";
		byte[] produced = ClipboardHelper.BuildUtf16BomBytes(text);
		byte[] utf8 = Encoding.UTF8.GetBytes(text);

		// UTF-16LE has 2 bytes/BMP char + 2 BOM bytes; UTF-8 Cyrillic is 2 bytes/char, no BOM.
		Assert.NotEqual(utf8.Length, produced.Length);
		Assert.NotEqual<byte[]>(utf8, produced);
	}

	[Fact]
	public void BuildUtf16BomBytes_AsciiStillRoundTrips()
	{
		string text = "Hello, World!";
		byte[] bytes = ClipboardHelper.BuildUtf16BomBytes(text);
		string decoded = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
		Assert.Equal(text, decoded);
	}

	[Fact]
	public void BuildUtf16BomBytes_NullTreatedAsEmpty()
	{
		byte[] bytes = ClipboardHelper.BuildUtf16BomBytes(null);
		Assert.Equal(2, bytes.Length); // BOM only
		Assert.Equal(0xFF, bytes[0]);
		Assert.Equal(0xFE, bytes[1]);
	}
}

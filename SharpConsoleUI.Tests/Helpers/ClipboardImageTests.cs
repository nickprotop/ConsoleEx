// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Imaging;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// Reading an image off the clipboard.
/// </summary>
/// <remarks>
/// The backends cannot be exercised here: a test machine has no clipboard owner holding a PNG, and
/// spawning the real tools would read whatever the developer last copied. What CAN be pinned is the
/// part with no system in it, which is also the part most likely to be wrong: the DIB a Windows
/// clipboard hands over is headerless, and reconstructing the file header is arithmetic that fails
/// silently by decoding the palette as pixels.
/// </remarks>
[Collection("EnvSerial")]
public class ClipboardImageTests : IDisposable
{
	private readonly ClipboardBackend _saved = ClipboardHelper.Backend;

	public void Dispose() => ClipboardHelper.ForceBackendForTests(_saved);

	#region DIB to BMP

	/// <summary>
	/// A 2x2 24-bit DIB, bottom-up as Windows stores it, with each row padded to four bytes.
	/// </summary>
	private static byte[] Dib24(int width, int height)
	{
		int rowBytes = ((width * 3 + 3) / 4) * 4;
		var dib = new byte[40 + rowBytes * height];

		BitConverter.TryWriteBytes(dib.AsSpan(0), 40);            // header size
		BitConverter.TryWriteBytes(dib.AsSpan(4), width);
		BitConverter.TryWriteBytes(dib.AsSpan(8), height);
		BitConverter.TryWriteBytes(dib.AsSpan(12), (short)1);     // planes
		BitConverter.TryWriteBytes(dib.AsSpan(14), (short)24);    // bits per pixel
		BitConverter.TryWriteBytes(dib.AsSpan(16), 0);            // BI_RGB
		BitConverter.TryWriteBytes(dib.AsSpan(20), rowBytes * height);

		// Fill the pixels with something non-zero, so a decoder that silently read the wrong
		// offset would produce a different image rather than the same black square.
		for (int i = 40; i < dib.Length; i++)
			dib[i] = (byte)(i % 251);

		return dib;
	}

	/// <summary>
	/// The reconstructed bytes must actually decode, at the size the DIB declared. Asserting the
	/// header fields alone would pass with the pixel offset wrong, which is the whole hazard.
	/// </summary>
	[Fact]
	public void DibToBmp_ProducesBytesThatDecodeAtTheDeclaredSize()
	{
		byte[]? bmp = ClipboardHelper.DibToBmp(Dib24(4, 3));

		Assert.NotNull(bmp);
		using var stream = new MemoryStream(bmp!);
		var image = PixelBuffer.FromStream(stream);

		Assert.Equal(4, image.Width);
		Assert.Equal(3, image.Height);
	}

	[Fact]
	public void DibToBmp_WritesTheBitmapFileMagicAndTotalSize()
	{
		byte[] dib = Dib24(4, 3);

		byte[]? bmp = ClipboardHelper.DibToBmp(dib);

		Assert.NotNull(bmp);
		Assert.Equal((byte)'B', bmp![0]);
		Assert.Equal((byte)'M', bmp[1]);
		Assert.Equal(dib.Length + 14, BitConverter.ToInt32(bmp, 2));
		Assert.Equal(dib.Length + 14, bmp.Length);
	}

	/// <summary>
	/// The pixel offset is the field that has to be computed rather than copied: 14 for the file
	/// header plus the DIB header, with no palette at 24 bits.
	/// </summary>
	[Fact]
	public void DibToBmp_PointsPastTheHeaderWhenThereIsNoPalette()
	{
		byte[]? bmp = ClipboardHelper.DibToBmp(Dib24(4, 3));

		Assert.Equal(14 + 40, BitConverter.ToInt32(bmp!, 10));
	}

	/// <summary>An 8-bit DIB carries 256 palette entries of four bytes between header and pixels.</summary>
	[Fact]
	public void DibToBmp_CountsTheDefaultPaletteWhenTheDepthHasOne()
	{
		int rowBytes = 4;
		var dib = new byte[40 + 256 * 4 + rowBytes];
		BitConverter.TryWriteBytes(dib.AsSpan(0), 40);
		BitConverter.TryWriteBytes(dib.AsSpan(4), 1);
		BitConverter.TryWriteBytes(dib.AsSpan(8), 1);
		BitConverter.TryWriteBytes(dib.AsSpan(12), (short)1);
		BitConverter.TryWriteBytes(dib.AsSpan(14), (short)8);
		BitConverter.TryWriteBytes(dib.AsSpan(32), 0);   // zero means the default, 1 << 8

		byte[]? bmp = ClipboardHelper.DibToBmp(dib);

		Assert.Equal(14 + 40 + 256 * 4, BitConverter.ToInt32(bmp!, 10));
	}

	[Fact]
	public void DibToBmp_RefusesABlockTooSmallToHoldAHeader()
	{
		Assert.Null(ClipboardHelper.DibToBmp(new byte[10]));
	}

	/// <summary>
	/// A header size the block cannot contain is a malformed DIB, not a large one: believing it
	/// would compute a pixel offset past the end of what was copied.
	/// </summary>
	[Fact]
	public void DibToBmp_RefusesAHeaderSizeBiggerThanTheBlock()
	{
		var dib = new byte[64];
		BitConverter.TryWriteBytes(dib.AsSpan(0), 4096);

		Assert.Null(ClipboardHelper.DibToBmp(dib));
	}

	#endregion

	#region Type negotiation

	[Fact]
	public void PickImageType_PrefersPngWhenSeveralAreOffered()
	{
		Assert.Equal("image/png", ClipboardHelper.PickImageType("text/plain\nimage/bmp\nimage/png\nimage/jpeg"));
	}

	[Fact]
	public void PickImageType_FallsToJpegThenBmp()
	{
		Assert.Equal("image/jpeg", ClipboardHelper.PickImageType("text/plain\nimage/bmp\nimage/jpeg"));
		Assert.Equal("image/bmp", ClipboardHelper.PickImageType("text/plain\nimage/bmp"));
	}

	/// <summary>
	/// A clipboard with only text in it is the common case, and must not send a read after a type
	/// the owner never offered.
	/// </summary>
	[Fact]
	public void PickImageType_ReturnsNullWhenNoImageIsOffered()
	{
		Assert.Null(ClipboardHelper.PickImageType("TARGETS\nSTRING\nUTF8_STRING\ntext/plain"));
		Assert.Null(ClipboardHelper.PickImageType(string.Empty));
	}

	/// <summary>TIFF is offered by real clipboards and is deliberately not decodable (see PixelBuffer).</summary>
	[Fact]
	public void PickImageType_DoesNotAskForATypeThatCannotBeDecoded()
	{
		Assert.Null(ClipboardHelper.PickImageType("image/tiff\nimage/x-xpixmap"));
	}

	#endregion

	#region Backends

	/// <summary>
	/// The in-process buffer holds TEXT. Offering it as an image would hand a caller bytes that are
	/// not an image at all, so this path has no answer rather than a wrong one.
	/// </summary>
	[Fact]
	public void InternalFallback_HasNoImage()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		ClipboardHelper.SetText("this is text, not an image");

		Assert.Null(ClipboardHelper.GetImage());
		Assert.Null(ClipboardHelper.GetImageWithTimeout());
	}

	/// <summary>xsel cannot read a binary target, so the backend says so instead of mangling one.</summary>
	[Fact]
	public void Xsel_HasNoImage()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.Xsel);

		Assert.Null(ClipboardHelper.GetImage());
	}

	/// <summary>
	/// A read that stalls must return within the timeout, and must return NULL rather than the text
	/// buffer. This is the image half of issue #42: a stalled child process cannot hold the render
	/// loop, and the fallback the text path has does not exist here.
	/// </summary>
	[Fact]
	public void GetImageWithTimeout_GivesUpWithinTheTimeout_AndNeverServesTheTextBuffer()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.Xclip);
		ClipboardHelper.SetText("text that must not come back as an image");

		var clock = Stopwatch.StartNew();
		byte[]? image = ClipboardHelper.GetImageWithTimeout(120);
		clock.Stop();

		Assert.Null(image);

		// Generous, because the bound under test is "does not wait for the tool", not the scheduler.
		// Without the timeout this waits on the process read instead, which is an order of magnitude
		// longer.
		Assert.True(clock.ElapsedMilliseconds < 2000,
			$"took {clock.ElapsedMilliseconds}ms, which is the unbounded read rather than the timeout");
	}

	#endregion

	#region Telling a timeout from an empty clipboard

	/// <summary>
	/// A read that does not finish in time reports false. GetTextWithTimeout serves the in-process
	/// buffer instead, so a caller choosing between formats reads a stalled owner as "there is
	/// text" and never looks for the image that was actually on the clipboard.
	/// </summary>
	/// <remarks>
	/// Zero milliseconds rather than a stalled tool, because a tool that is absent fails fast and
	/// a tool that is present answers: neither stalls on a test machine. What is under test is the
	/// deadline, and a deadline that has already passed is the honest way to reach it.
	/// </remarks>
	[Fact]
	public void TryGetTextWithTimeout_WhenTheReadDoesNotFinishInTime_ReportsFalse()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.Xclip);
		ClipboardHelper.SetText("the buffer, which is not the clipboard");

		bool answered = ClipboardHelper.TryGetTextWithTimeout(out string text, 0);

		Assert.False(answered);
		// The buffer is still handed back, so a caller ignoring the bool behaves as before.
		Assert.Equal("the buffer, which is not the clipboard", text);
	}

	/// <summary>An in-process read cannot stall, so its answer is always the clipboard's.</summary>
	[Fact]
	public void TryGetTextWithTimeout_OnTheInternalBackend_ReportsTrue()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		ClipboardHelper.SetText("copied here");

		Assert.True(ClipboardHelper.TryGetTextWithTimeout(out string text));
		Assert.Equal("copied here", text);
	}

	/// <summary>An empty clipboard is an answer, not a failure: there is simply no text.</summary>
	[Fact]
	public void TryGetTextWithTimeout_WithAnEmptyClipboard_ReportsTrueAndEmpty()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		ClipboardHelper.SetText(string.Empty);

		Assert.True(ClipboardHelper.TryGetTextWithTimeout(out string text));
		Assert.Equal(string.Empty, text);
	}

	/// <summary>It gives up within the timeout, like the read it is based on.</summary>
	[Fact]
	public void TryGetTextWithTimeout_GivesUpWithinTheTimeout()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.Xclip);

		var clock = System.Diagnostics.Stopwatch.StartNew();
		ClipboardHelper.TryGetTextWithTimeout(out _, 100);
		clock.Stop();

		Assert.True(clock.ElapsedMilliseconds < 2000,
			$"took {clock.ElapsedMilliseconds}ms, which is the unbounded read rather than the timeout");
	}

	#endregion

}

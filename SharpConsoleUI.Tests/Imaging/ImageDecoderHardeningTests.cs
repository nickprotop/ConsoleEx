// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Imaging;
using StbImageWriteSharp;
using Xunit;

namespace SharpConsoleUI.Tests.Imaging;

/// <summary>
/// Images reach the decoder from places the application does not control — a <c>data:</c> URI in
/// HTML, an <c>&lt;img&gt;</c> fetched from the network, a file the user picked. TIFF decoders have
/// carried two heap out-of-bounds writes in their CCITT fax paths and a BigTIFF header that never
/// terminates. No TIFF decoder is linked into the library, so a TIFF is refused on inspection,
/// before any format-specific parsing runs.
/// </summary>
public class ImageDecoderHardeningTests
{
	/// <summary>
	/// The 24-byte BigTIFF from GHSA-wmxv-xphr-5c9g: little-endian, magic 0x2B, 8-byte offsets,
	/// first IFD at 16, entry count 5,000,000,000. Decoding it with TIFF enabled does not return.
	/// </summary>
	private static byte[] NonTerminatingBigTiff()
	{
		var ms = new MemoryStream();
		var w = new BinaryWriter(ms);
		w.Write((byte)'I');
		w.Write((byte)'I');
		w.Write((ushort)0x2B);
		w.Write((ushort)8);
		w.Write((ushort)0);
		w.Write((ulong)16);
		w.Write((ulong)5_000_000_000);
		w.Flush();
		return ms.ToArray();
	}

	/// <summary>A minimal classic TIFF header — the form the two fax overflows arrive in.</summary>
	private static byte[] ClassicTiffHeader()
	{
		var ms = new MemoryStream();
		var w = new BinaryWriter(ms);
		w.Write((byte)'I');
		w.Write((byte)'I');
		w.Write((ushort)42);
		w.Write((uint)8);
		w.Write((ushort)0);
		w.Write((uint)0);
		w.Flush();
		return ms.ToArray();
	}

	#region TIFF never reaches the decoder

	/// <summary>
	/// The reported denial of service: 24 bytes used to spin forever. It must now be refused, and
	/// refused PROMPTLY — a slow rejection would still be a freeze.
	/// </summary>
	[Fact]
	public void TheNonTerminatingBigTiff_IsRefusedImmediately()
	{
		using var stream = new MemoryStream(NonTerminatingBigTiff());

		var stopwatch = Stopwatch.StartNew();
		Assert.Throws<UnsupportedImageFormatException>(() => PixelBuffer.FromStream(stream));
		stopwatch.Stop();

		Assert.True(stopwatch.Elapsed.TotalSeconds < 2,
			$"rejecting the BigTIFF took {stopwatch.Elapsed.TotalSeconds:F1}s");
	}

	/// <summary>
	/// Classic TIFF has to go too. The two heap out-of-bounds writes are in the CCITT fax paths,
	/// which classic TIFF reaches — refusing only BigTIFF would leave both open.
	/// </summary>
	[Fact]
	public void AClassicTiff_IsRefused()
	{
		using var stream = new MemoryStream(ClassicTiffHeader());

		Assert.Throws<UnsupportedImageFormatException>(() => PixelBuffer.FromStream(stream));
	}

	[Fact]
	public void AClassicTiffOnDisk_IsRefused()
	{
		string path = Path.Combine(Path.GetTempPath(), $"scui-tiff-{Guid.NewGuid():N}.tif");
		File.WriteAllBytes(path, ClassicTiffHeader());
		try
		{
			Assert.Throws<UnsupportedImageFormatException>(() => PixelBuffer.FromFile(path));
		}
		finally
		{
			File.Delete(path);
		}
	}

	#endregion

	#region Everything else still decodes

	/// <summary>
	/// Hardening is worthless if it breaks the formats people actually use. Each of these is
	/// encoded and decoded back through the real entry point.
	/// </summary>
	[Theory]
	[InlineData("png")]
	[InlineData("jpeg")]
	[InlineData("bmp")]
	[InlineData("tga")]
	public void SupportedFormats_StillDecode(string format)
	{
		using var stream = new MemoryStream(Encode(format));

		var buffer = PixelBuffer.FromStream(stream);

		Assert.Equal(4, buffer.Width);
		Assert.Equal(4, buffer.Height);
	}

	/// <summary>
	/// GIF has no encoder here, so it is covered by a hand-built 4x4 single-frame file. It stays
	/// in the supported set because HTML pages still carry GIFs.
	/// </summary>
	[Fact]
	public void AGif_StillDecodes()
	{
		using var stream = new MemoryStream(MinimalGif4x4());

		var buffer = PixelBuffer.FromStream(stream);

		Assert.Equal(4, buffer.Width);
		Assert.Equal(4, buffer.Height);
	}

	/// <summary>
	/// Formats the library no longer decodes. They must fail as an unsupported format — the same
	/// documented exception any other undecodable input produces — and not crash or hang.
	/// </summary>
	[Theory]
	[InlineData("webp")]
	[InlineData("qoi")]
	public void FormatsNoLongerSupported_AreRefusedCleanly(string format)
	{
		using var stream = new MemoryStream(UnsupportedFormatHeader(format));

		Assert.Throws<UnsupportedImageFormatException>(() => PixelBuffer.FromStream(stream));
	}

	/// <summary>Round-trips 24-bit RGB through the raw-pixel seam for externally decoded images.</summary>
	[Fact]
	public void FromRgb24Bytes_ReadsPixelsInRowMajorOrder()
	{
		// 2x1: red then green.
		byte[] rgb = { 255, 0, 0, 0, 255, 0 };

		var buffer = PixelBuffer.FromRgb24Bytes(rgb, 2, 1);

		Assert.Equal(255, buffer.GetPixel(0, 0).R);
		Assert.Equal(0, buffer.GetPixel(0, 0).G);
		Assert.Equal(0, buffer.GetPixel(1, 0).R);
		Assert.Equal(255, buffer.GetPixel(1, 0).G);
	}

	/// <summary>The RGBA seam drops alpha rather than blending it.</summary>
	[Fact]
	public void FromRgba32Bytes_IgnoresAlpha()
	{
		byte[] rgba = { 10, 20, 30, 0, 40, 50, 60, 255 };

		var buffer = PixelBuffer.FromRgba32Bytes(rgba, 2, 1);

		Assert.Equal(10, buffer.GetPixel(0, 0).R);
		Assert.Equal(30, buffer.GetPixel(0, 0).B);
		Assert.Equal(40, buffer.GetPixel(1, 0).R);
		Assert.Equal(60, buffer.GetPixel(1, 0).B);
	}

	[Fact]
	public void FromRgb24Bytes_RejectsAShortBuffer()
	{
		Assert.Throws<ArgumentException>(() => PixelBuffer.FromRgb24Bytes(new byte[5], 2, 1));
	}

	private static byte[] Encode(string format)
	{
		// A 4x4 RGB image, encoded through the real writer so each file is a genuine one.
		var rgb = new byte[4 * 4 * 3];
		for (int i = 0; i < rgb.Length; i += 3)
		{
			rgb[i] = 200;
			rgb[i + 1] = 100;
			rgb[i + 2] = 50;
		}

		var ms = new MemoryStream();
		var writer = new ImageWriter();
		switch (format)
		{
			case "png":
				writer.WritePng(rgb, 4, 4, StbImageWriteSharp.ColorComponents.RedGreenBlue, ms);
				break;
			case "jpeg":
				writer.WriteJpg(rgb, 4, 4, StbImageWriteSharp.ColorComponents.RedGreenBlue, ms, 90);
				break;
			case "bmp":
				writer.WriteBmp(rgb, 4, 4, StbImageWriteSharp.ColorComponents.RedGreenBlue, ms);
				break;
			case "tga":
				writer.WriteTga(rgb, 4, 4, StbImageWriteSharp.ColorComponents.RedGreenBlue, ms);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(format), format, null);
		}
		return ms.ToArray();
	}

	/// <summary>A 4x4 GIF87a with a 2-colour table and one LZW-coded frame.</summary>
	private static byte[] MinimalGif4x4()
	{
		return new byte[]
		{
			0x47, 0x49, 0x46, 0x38, 0x37, 0x61, // GIF87a
			0x04, 0x00, 0x04, 0x00,             // 4x4
			0x80, 0x00, 0x00,                   // global colour table, 2 entries
			0xFF, 0x00, 0x00,                   // red
			0x00, 0x00, 0xFF,                   // blue
			0x2C,                               // image descriptor
			0x00, 0x00, 0x00, 0x00,             // left, top
			0x04, 0x00, 0x04, 0x00,             // 4x4
			0x00,                               // no local table
			0x02,                               // LZW minimum code size
			0x03, 0x44, 0x01, 0x00,             // one data sub-block
			0x00,                               // block terminator
			0x3B,                               // trailer
		};
	}

	/// <summary>Valid file headers for formats the library no longer decodes.</summary>
	private static byte[] UnsupportedFormatHeader(string format) => format switch
	{
		// RIFF....WEBPVP8
		"webp" => new byte[]
		{
			0x52, 0x49, 0x46, 0x46, 0x20, 0x00, 0x00, 0x00,
			0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20,
			0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		},
		// qoif + 4x4, 3 channels, sRGB
		"qoi" => new byte[]
		{
			0x71, 0x6F, 0x69, 0x66,
			0x00, 0x00, 0x00, 0x04,
			0x00, 0x00, 0x00, 0x04,
			0x03, 0x00,
		},
		_ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
	};

	#endregion

	#region Through the HTML path, which is how untrusted images arrive

	/// <summary>
	/// The reported vector: an inline TIFF in HTML. Layout decodes <c>data:</c> URIs in place on
	/// the UI thread, so a decoder that never returns freezes the application. The page must
	/// render, and promptly.
	/// </summary>
	[Fact]
	public void AnInlineTiffInHtml_DoesNotHangLayout()
	{
		// A TIFF announced as a PNG, so the content type cannot be what saves us.
		string dataUri = "data:image/png;base64," + Convert.ToBase64String(NonTerminatingBigTiff());
		using var html = new HtmlControl { ShowImages = true };

		var stopwatch = Stopwatch.StartNew();
		html.SetContent($"<p>Hello</p><img src=\"{dataUri}\" alt=\"bad\">");
		var text = string.Join("\n", Controls.ContainerTestHelpers.RenderToLines(html, 80, 10));
		stopwatch.Stop();

		Assert.True(stopwatch.Elapsed.TotalSeconds < 3,
			$"rendering the page took {stopwatch.Elapsed.TotalSeconds:F1}s");
		Assert.Contains("Hello", text);
	}

	/// <summary>The undecodable image degrades to its alt text, as any failed decode does.</summary>
	[Fact]
	public void AnInlineTiffInHtml_FallsBackToAltText()
	{
		string dataUri = "data:image/tiff;base64," + Convert.ToBase64String(ClassicTiffHeader());
		using var html = new HtmlControl { ShowImages = true };

		html.SetContent($"<p>Hello</p><img src=\"{dataUri}\" alt=\"bad\">");
		var text = string.Join("\n", Controls.ContainerTestHelpers.RenderToLines(html, 80, 10));

		Assert.Contains("[bad]", text);
	}

	/// <summary>A legitimate inline PNG still renders, so the guard has not broken inline images.</summary>
	[Fact]
	public void AnInlinePngInHtml_StillRenders()
	{
		string dataUri = "data:image/png;base64," + Convert.ToBase64String(Encode("png"));
		using var html = new HtmlControl { ShowImages = true };

		html.SetContent($"<p>Hello</p><img src=\"{dataUri}\" alt=\"good\">");
		var text = string.Join("\n", Controls.ContainerTestHelpers.RenderToLines(html, 80, 10));

		Assert.Contains("Hello", text);
		Assert.DoesNotContain("[good]", text);
	}

	#endregion
}

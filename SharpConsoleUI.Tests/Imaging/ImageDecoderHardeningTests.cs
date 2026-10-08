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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SharpConsoleUI.Tests.Imaging;

/// <summary>
/// Images reach the decoder from places the application does not control — a <c>data:</c> URI in
/// HTML, an <c>&lt;img&gt;</c> fetched from the network, a file the user picked. The TIFF decoder
/// in the ImageSharp 3.x line, which no longer receives fixes, carries two heap out-of-bounds
/// writes in its CCITT fax paths and a BigTIFF header that never terminates, so TIFF is refused
/// before any of its code runs.
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
		Assert.Throws<UnknownImageFormatException>(() => PixelBuffer.FromStream(stream));
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

		Assert.Throws<UnknownImageFormatException>(() => PixelBuffer.FromStream(stream));
	}

	[Fact]
	public void AClassicTiffOnDisk_IsRefused()
	{
		string path = Path.Combine(Path.GetTempPath(), $"scui-tiff-{Guid.NewGuid():N}.tif");
		File.WriteAllBytes(path, ClassicTiffHeader());
		try
		{
			Assert.Throws<UnknownImageFormatException>(() => PixelBuffer.FromFile(path));
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
	[InlineData("gif")]
	[InlineData("bmp")]
	[InlineData("webp")]
	[InlineData("tga")]
	[InlineData("pbm")]
	[InlineData("qoi")]
	public void SupportedFormats_StillDecode(string format)
	{
		using var stream = new MemoryStream(Encode(format));

		var buffer = PixelBuffer.FromStream(stream);

		Assert.Equal(4, buffer.Width);
		Assert.Equal(4, buffer.Height);
	}

	private static byte[] Encode(string format)
	{
		using var image = new Image<Rgb24>(4, 4);
		var encoder = format switch
		{
			"png" => (SixLabors.ImageSharp.Formats.IImageEncoder)new PngEncoder(),
			"jpeg" => new JpegEncoder(),
			"gif" => new GifEncoder(),
			"bmp" => new BmpEncoder(),
			"webp" => new WebpEncoder(),
			"tga" => new TgaEncoder(),
			"pbm" => new PbmEncoder(),
			"qoi" => new QoiEncoder(),
			_ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
		};

		var ms = new MemoryStream();
		image.Save(ms, encoder);
		return ms.ToArray();
	}

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

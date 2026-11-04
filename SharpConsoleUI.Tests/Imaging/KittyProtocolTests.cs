using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Imaging;

public class KittyProtocolTests
{
	#region Placeholder Encoding

	[Fact]
	public void BuildPlaceholderCombiners_Row0_Col0_UsesTwoFirstDiacritics()
	{
		// Row 0 and Col 0 both use the first diacritic (U+0305)
		var combiners = KittyProtocol.BuildPlaceholderCombiners(row: 0, col: 0);
		Assert.Equal(2, combiners.Length);
		Assert.Equal('\u0305', combiners[0]); // row 0
		Assert.Equal('\u0305', combiners[1]); // col 0
	}

	[Fact]
	public void BuildPlaceholderCombiners_Row0_Col1_UsesCorrectDiacritics()
	{
		var combiners = KittyProtocol.BuildPlaceholderCombiners(row: 0, col: 1);
		Assert.Equal('\u0305', combiners[0]); // row 0 = U+0305
		Assert.Equal('\u030D', combiners[1]); // col 1 = U+030D
	}

	[Fact]
	public void BuildPlaceholderCombiners_Row1_Col0_UsesCorrectDiacritics()
	{
		var combiners = KittyProtocol.BuildPlaceholderCombiners(row: 1, col: 0);
		Assert.Equal('\u030D', combiners[0]); // row 1 = U+030D
		Assert.Equal('\u0305', combiners[1]); // col 0 = U+0305
	}

	[Fact]
	public void BuildPlaceholderCombiners_Row5_Col3_UsesCorrectDiacritics()
	{
		var combiners = KittyProtocol.BuildPlaceholderCombiners(row: 5, col: 3);
		Assert.Equal(KittyProtocol.RowColumnDiacritics[5], combiners[0]);
		Assert.Equal(KittyProtocol.RowColumnDiacritics[3], combiners[1]);
	}

	[Fact]
	public void ImageIdToForegroundColor_EncodesAs24BitRgb()
	{
		// Image ID 1 = 0x000001 → R=0, G=0, B=1
		var color1 = KittyProtocol.ImageIdToForegroundColor(1);
		Assert.Equal(0, color1.R);
		Assert.Equal(0, color1.G);
		Assert.Equal(1, color1.B);

		// Image ID 256 = 0x000100 → R=0, G=1, B=0
		var color256 = KittyProtocol.ImageIdToForegroundColor(256);
		Assert.Equal(0, color256.R);
		Assert.Equal(1, color256.G);
		Assert.Equal(0, color256.B);

		// Image ID 0x010203 → R=1, G=2, B=3
		var colorLarge = KittyProtocol.ImageIdToForegroundColor(0x010203);
		Assert.Equal(1, colorLarge.R);
		Assert.Equal(2, colorLarge.G);
		Assert.Equal(3, colorLarge.B);
	}

	[Fact]
	public void PlaceholderRune_IsU10EEEE()
	{
		Assert.Equal(new Rune(0x10EEEE), ImagingDefaults.KittyPlaceholder);
	}

	[Fact]
	public void RowColumnDiacritics_Has283BmpEntries()
	{
		Assert.Equal(283, KittyProtocol.RowColumnDiacritics.Length);
	}

	[Fact]
	public void MaxRowColumnValue_Is297()
	{
		Assert.Equal(297, KittyProtocol.MaxRowColumnValue);
	}

	#endregion

	#region Escape Sequence Building

	[Fact]
	public void BuildTransmitCommand_SmallPayload_SingleChunk()
	{
		byte[] data = new byte[100];
		var chunks = KittyProtocol.BuildTransmitChunks(imageId: 1, pngData: data, columns: 10, rows: 5);

		Assert.Single(chunks);
		Assert.StartsWith("\x1b_G", chunks[0]);
		Assert.EndsWith("\x1b\\", chunks[0]);
		Assert.Contains("a=T", chunks[0]);
		Assert.Contains("f=100", chunks[0]);
		Assert.Contains("i=1", chunks[0]);
		Assert.Contains("U=1", chunks[0]);
		Assert.Contains("c=10", chunks[0]);
		Assert.Contains("r=5", chunks[0]);
		Assert.Contains("m=0", chunks[0]);
	}

	[Fact]
	public void BuildTransmitChunks_LargePayload_ProducesMultipleChunks()
	{
		byte[] data = new byte[ImagingDefaults.KittyChunkSize * 3 + 100];
		var chunks = KittyProtocol.BuildTransmitChunks(imageId: 42, pngData: data, columns: 20, rows: 10);

		Assert.True(chunks.Count >= 4);
		Assert.Contains("a=T", chunks[0]);
		Assert.Contains("m=1", chunks[0]);
		Assert.Contains("i=42", chunks[0]);

		for (int i = 1; i < chunks.Count - 1; i++)
			Assert.Contains("m=1", chunks[i]);

		Assert.Contains("m=0", chunks[^1]);
	}

	[Fact]
	public void BuildDeleteCommand_ProducesCorrectSequence()
	{
		string cmd = KittyProtocol.BuildDeleteCommand(imageId: 7);
		Assert.Equal("\x1b_Ga=d,d=I,i=7\x1b\\", cmd);
	}

	#endregion

	#region PNG Encoding

	[Fact]
	public void EncodePng_ProducesValidPngBytes()
	{
		var pixels = new PixelBuffer(2, 2);
		pixels.SetPixel(0, 0, new ImagePixel(255, 0, 0));
		pixels.SetPixel(1, 0, new ImagePixel(0, 255, 0));
		pixels.SetPixel(0, 1, new ImagePixel(0, 0, 255));
		pixels.SetPixel(1, 1, new ImagePixel(255, 255, 255));

		byte[] png = KittyProtocol.EncodePng(pixels);

		Assert.True(png.Length > 8);
		Assert.Equal(0x89, png[0]);
		Assert.Equal((byte)'P', png[1]);
		Assert.Equal((byte)'N', png[2]);
		Assert.Equal((byte)'G', png[3]);
	}

	[Fact]
	public void EncodePng_ScaledToTargetDimensions()
	{
		var pixels = new PixelBuffer(10, 10);
		byte[] png = KittyProtocol.EncodePng(pixels, targetWidth: 5, targetHeight: 5);

		Assert.True(png.Length > 8);
		Assert.Equal(0x89, png[0]);
	}

	#endregion

	#region Quiet uploads

	/// <summary>
	/// Every upload asks the terminal not to answer.
	/// </summary>
	/// <remarks>
	/// Kitty replies to a graphics command with an APC sequence, and a reply is not typing: in a
	/// real full-screen Kitty the reply to a PNG upload was read as keystrokes, typed itself into a
	/// composer, and closed a portal that dismissed on any key. The raw-pixel path always sent
	/// q=2; this one did not.
	/// </remarks>
	[Fact]
	public void PngUpload_AsksForNoReply()
	{
		var chunks = KittyProtocol.BuildTransmitChunks(31, new byte[] { 1, 2, 3, 4 }, 4, 2);

		Assert.NotEmpty(chunks);
		Assert.Contains("q=2", chunks[0]);
	}

	/// <summary>
	/// On the FIRST chunk, where the control keys live. A later chunk carries only m= and payload.
	/// </summary>
	[Fact]
	public void PngUpload_CarriesTheControlKeysOnTheFirstChunkOnly()
	{
		var big = new byte[16 * 1024];
		var chunks = KittyProtocol.BuildTransmitChunks(31, big, 4, 2);

		Assert.True(chunks.Count > 1, "expected a payload large enough to be chunked");
		Assert.Contains("q=2", chunks[0]);
		Assert.Contains("a=T", chunks[0]);
		Assert.DoesNotContain("q=2", chunks[1]);
	}

	/// <summary>The raw-pixel path keeps asking too; the two must not drift apart again.</summary>
	[Fact]
	public void RawPixelUpload_AlsoAsksForNoReply()
	{
		var chunks = KittyProtocol.BuildRawRgbTransmitChunks(31, new byte[] { 1, 2, 3 }, 1, 1, 1, 1);

		Assert.NotEmpty(chunks);
		Assert.Contains("q=2", chunks[0]);
	}

	#endregion
}

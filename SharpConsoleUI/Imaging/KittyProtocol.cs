// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.IO.Compression;
using System.Text;
using SharpConsoleUI.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SharpConsoleUI.Imaging
{
	/// <summary>
	/// Static helpers for building Kitty graphics protocol escape sequences,
	/// encoding placeholder cells, and PNG conversion.
	/// </summary>
	/// <remarks>
	/// See https://sw.kovidgoyal.net/kitty/graphics-protocol/#unicode-placeholders
	/// for the virtual placement specification.
	///
	/// Encoding summary:
	/// - Image ID: encoded via foreground color (24-bit true color SGR)
	/// - Placement ID: encoded via underline color (optional)
	/// - Row/Column: each encoded as a single combining diacritic from a 297-entry table
	///   derived from gen/rowcolumn-diacritics.txt in the Kitty source
	/// </remarks>
	internal static class KittyProtocol
	{
		#region Row/Column Diacritics Table

		/// <summary>
		/// The 297 combining diacritics used to encode row and column indices
		/// in Kitty virtual placements. Index 0 = row/col 0, index 1 = row/col 1, etc.
		/// Derived from kitty/gen/rowcolumn-diacritics.txt (Unicode 6.0 combining class 230).
		/// </summary>
		internal static readonly char[] RowColumnDiacritics =
		{
			'\u0305', '\u030D', '\u030E', '\u0310', '\u0312', '\u033D', '\u033E', '\u033F',
			'\u0346', '\u034A', '\u034B', '\u034C', '\u0350', '\u0351', '\u0352', '\u0357',
			'\u035B', '\u0363', '\u0364', '\u0365', '\u0366', '\u0367', '\u0368', '\u0369',
			'\u036A', '\u036B', '\u036C', '\u036D', '\u036E', '\u036F', '\u0483', '\u0484',
			'\u0485', '\u0486', '\u0487', '\u0592', '\u0593', '\u0594', '\u0595', '\u0597',
			'\u0598', '\u0599', '\u059C', '\u059D', '\u059E', '\u059F', '\u05A0', '\u05A1',
			'\u05A8', '\u05A9', '\u05AB', '\u05AC', '\u05AF', '\u05C4', '\u0610', '\u0611',
			'\u0612', '\u0613', '\u0614', '\u0615', '\u0616', '\u0617', '\u0657', '\u0658',
			'\u0659', '\u065A', '\u065B', '\u065D', '\u065E', '\u06D6', '\u06D7', '\u06D8',
			'\u06D9', '\u06DA', '\u06DB', '\u06DC', '\u06DF', '\u06E0', '\u06E1', '\u06E2',
			'\u06E4', '\u06E7', '\u06E8', '\u06EB', '\u06EC', '\u0730', '\u0732', '\u0733',
			'\u0735', '\u0736', '\u073A', '\u073D', '\u073F', '\u0740', '\u0741', '\u0743',
			'\u0745', '\u0747', '\u0749', '\u074A', '\u07EB', '\u07EC', '\u07ED', '\u07EE',
			'\u07EF', '\u07F0', '\u07F1', '\u07F3', '\u0816', '\u0817', '\u0818', '\u0819',
			'\u081B', '\u081C', '\u081D', '\u081E', '\u081F', '\u0820', '\u0821', '\u0822',
			'\u0823', '\u0825', '\u0826', '\u0827', '\u0829', '\u082A', '\u082B', '\u082C',
			'\u082D', '\u0951', '\u0953', '\u0954', '\u0F82', '\u0F83', '\u0F86', '\u0F87',
			'\u135D', '\u135E', '\u135F', '\u17DD', '\u193A', '\u1A17', '\u1A75', '\u1A76',
			'\u1A77', '\u1A78', '\u1A79', '\u1A7A', '\u1A7B', '\u1A7C', '\u1B6B', '\u1B6D',
			'\u1B6E', '\u1B6F', '\u1B70', '\u1B71', '\u1B72', '\u1B73', '\u1CD0', '\u1CD1',
			'\u1CD2', '\u1CDA', '\u1CDB', '\u1CE0', '\u1DC0', '\u1DC1', '\u1DC3', '\u1DC4',
			'\u1DC5', '\u1DC6', '\u1DC7', '\u1DC8', '\u1DC9', '\u1DCB', '\u1DCC', '\u1DD1',
			'\u1DD2', '\u1DD3', '\u1DD4', '\u1DD5', '\u1DD6', '\u1DD7', '\u1DD8', '\u1DD9',
			'\u1DDA', '\u1DDB', '\u1DDC', '\u1DDD', '\u1DDE', '\u1DDF', '\u1DE0', '\u1DE1',
			'\u1DE2', '\u1DE3', '\u1DE4', '\u1DE5', '\u1DE6', '\u1DFE', '\u20D0', '\u20D1',
			'\u20D4', '\u20D5', '\u20D6', '\u20D7', '\u20DB', '\u20DC', '\u20E1', '\u20E7',
			'\u20E9', '\u20F0', '\u2CEF', '\u2CF0', '\u2CF1', '\u2DE0', '\u2DE1', '\u2DE2',
			'\u2DE3', '\u2DE4', '\u2DE5', '\u2DE6', '\u2DE7', '\u2DE8', '\u2DE9', '\u2DEA',
			'\u2DEB', '\u2DEC', '\u2DED', '\u2DEE', '\u2DEF', '\u2DF0', '\u2DF1', '\u2DF2',
			'\u2DF3', '\u2DF4', '\u2DF5', '\u2DF6', '\u2DF7', '\u2DF8', '\u2DF9', '\u2DFA',
			'\u2DFB', '\u2DFC', '\u2DFD', '\u2DFE', '\u2DFF', '\uA66F', '\uA67C', '\uA67D',
			'\uA6F0', '\uA6F1', '\uA8E0', '\uA8E1', '\uA8E2', '\uA8E3', '\uA8E4', '\uA8E5',
			'\uA8E6', '\uA8E7', '\uA8E8', '\uA8E9', '\uA8EA', '\uA8EB', '\uA8EC', '\uA8ED',
			'\uA8EE', '\uA8EF', '\uA8F0', '\uA8F1', '\uAAB0', '\uAAB2', '\uAAB3', '\uAAB7',
			'\uAAB8', '\uAABE', '\uAABF', '\uAAC1', '\uFE20', '\uFE21', '\uFE22', '\uFE23',
			'\uFE24', '\uFE25', '\uFE26',
		};

		// Supplementary plane diacritics (require surrogate pairs, stored as strings)
		private static readonly string[] SupplementaryDiacritics =
		{
			"\U00010A0F", "\U00010A38",
			"\U0001D185", "\U0001D186", "\U0001D187", "\U0001D188", "\U0001D189",
			"\U0001D1AA", "\U0001D1AB", "\U0001D1AC", "\U0001D1AD",
			"\U0001D242", "\U0001D243", "\U0001D244",
		};

		/// <summary>Total number of supported row/column values (BMP + supplementary).</summary>
		internal static readonly int MaxRowColumnValue = RowColumnDiacritics.Length + SupplementaryDiacritics.Length; // 297

		#endregion

		#region Placeholder Encoding

		/// <summary>
		/// Builds the combining diacritics string for a Kitty virtual placement cell.
		/// Per the Kitty spec, row and column are each encoded as a single diacritic
		/// from the rowcolumn-diacritics table.
		/// </summary>
		/// <param name="row">Row index within the image (0-based, max 296).</param>
		/// <param name="col">Column index within the image (0-based, max 296).</param>
		/// <returns>A string of two combining diacritics (row + column) to append after U+10EEEE.</returns>
		public static string BuildPlaceholderCombiners(int row, int col)
		{
			return $"{GetDiacritic(row)}{GetDiacritic(col)}";
		}

		/// <summary>
		/// Encodes an image ID as a 24-bit foreground Color for use in placeholder cells.
		/// The Kitty spec encodes image ID via SGR foreground color.
		/// </summary>
		public static Color ImageIdToForegroundColor(uint imageId)
		{
			byte r = (byte)((imageId >> 16) & 0xFF);
			byte g = (byte)((imageId >> 8) & 0xFF);
			byte b = (byte)(imageId & 0xFF);
			return new Color(r, g, b);
		}

		/// <summary>
		/// Gets the combining diacritic string for the given row/column index.
		/// </summary>
		private static string GetDiacritic(int index)
		{
			if (index < RowColumnDiacritics.Length)
				return RowColumnDiacritics[index].ToString();

			int suppIndex = index - RowColumnDiacritics.Length;
			if (suppIndex < SupplementaryDiacritics.Length)
				return SupplementaryDiacritics[suppIndex];

			// Beyond 296: clamp to last valid diacritic
			return SupplementaryDiacritics[^1];
		}

		#endregion

		#region Escape Sequence Building

		/// <summary>
		/// Builds the Kitty APC escape sequence chunks for transmitting an image.
		/// Large payloads are split into chunks of <see cref="ImagingDefaults.KittyChunkSize"/> bytes.
		/// </summary>
		public static List<string> BuildTransmitChunks(uint imageId, byte[] pngData, int columns, int rows)
		{
			var chunks = new List<string>();
			string base64 = Convert.ToBase64String(pngData);
			int chunkSize = ImagingDefaults.KittyChunkSize;
			int offset = 0;

			while (offset < base64.Length)
			{
				int remaining = base64.Length - offset;
				int thisChunk = Math.Min(remaining, chunkSize);
				bool isFirst = offset == 0;
				bool isLast = offset + thisChunk >= base64.Length;
				string payload = base64.Substring(offset, thisChunk);

				var sb = new StringBuilder(thisChunk + 64);
				sb.Append("\x1b_G");

				if (isFirst)
					sb.Append($"a=T,f=100,i={imageId},U=1,c={columns},r={rows},");
				sb.Append(isLast ? "m=0" : "m=1");
				sb.Append(';');
				sb.Append(payload);
				sb.Append("\x1b\\");

				chunks.Add(sb.ToString());
				offset += thisChunk;
			}

			return chunks;
		}

		/// <summary>
		/// Builds the Kitty APC escape sequence chunks for transmitting a raw RGB24 image
		/// (Kitty format <c>f=24</c>). The image is placed via virtual placements at the
		/// given column/row span, and <c>q=2</c> suppresses terminal responses so per-frame
		/// video transmissions do not flood stdin.
		/// </summary>
		/// <param name="imageId">Image identifier (reused across frames to update in place).</param>
		/// <param name="rgbData">Raw RGB24 pixel data: <paramref name="pixelWidth"/> * <paramref name="pixelHeight"/> * 3 bytes, row-major.</param>
		/// <param name="pixelWidth">Pixel width of the image being transmitted.</param>
		/// <param name="pixelHeight">Pixel height of the image being transmitted.</param>
		/// <param name="columns">Number of terminal columns the placement spans.</param>
		/// <param name="rows">Number of terminal rows the placement spans.</param>
		public static List<string> BuildRawRgbTransmitChunks(uint imageId, byte[] rgbData, int pixelWidth, int pixelHeight, int columns, int rows)
		{
			byte[] compressed = CompressZlib(rgbData);
			var chunks = new List<string>();
			string base64 = Convert.ToBase64String(compressed);
			int chunkSize = ImagingDefaults.KittyChunkSize;
			int offset = 0;

			while (offset < base64.Length)
			{
				int remaining = base64.Length - offset;
				int thisChunk = Math.Min(remaining, chunkSize);
				bool isFirst = offset == 0;
				bool isLast = offset + thisChunk >= base64.Length;
				string payload = base64.Substring(offset, thisChunk);

				var sb = new StringBuilder(thisChunk + 96);
				sb.Append("\x1b_G");

				if (isFirst)
					sb.Append($"a=T,f=24,i={imageId},s={pixelWidth},v={pixelHeight},o=z,U=1,c={columns},r={rows},q=2,");
				sb.Append(isLast ? "m=0" : "m=1");
				sb.Append(';');
				sb.Append(payload);
				sb.Append("\x1b\\");

				chunks.Add(sb.ToString());
				offset += thisChunk;
			}

			return chunks;
		}

		/// <summary>
		/// Builds the Kitty APC escape sequence chunks for <b>updating</b> the root frame
		/// (<c>r=1</c>) of an already-transmitted image with fresh raw RGB24 data. This is the
		/// correct wire protocol for real-time video frame updates on a persistent virtual
		/// placement: using <c>a=f,r=1</c> edits the root frame's pixel data in place without
		/// deleting any placements that reference the image, which is what <c>a=T</c> would do.
		/// </summary>
		/// <param name="imageId">Image identifier of the previously-transmitted image to update.</param>
		/// <param name="rgbData">Raw RGB24 pixel data: <paramref name="pixelWidth"/> * <paramref name="pixelHeight"/> * 3 bytes.</param>
		/// <param name="pixelWidth">Pixel width of the new frame (should match the original transmit).</param>
		/// <param name="pixelHeight">Pixel height of the new frame (should match the original transmit).</param>
		public static List<string> BuildRawRgbFrameUpdateChunks(uint imageId, byte[] rgbData, int pixelWidth, int pixelHeight)
		{
			byte[] compressed = CompressZlib(rgbData);
			var chunks = new List<string>();
			string base64 = Convert.ToBase64String(compressed);
			int chunkSize = ImagingDefaults.KittyChunkSize;
			int offset = 0;

			while (offset < base64.Length)
			{
				int remaining = base64.Length - offset;
				int thisChunk = Math.Min(remaining, chunkSize);
				bool isFirst = offset == 0;
				bool isLast = offset + thisChunk >= base64.Length;
				string payload = base64.Substring(offset, thisChunk);

				var sb = new StringBuilder(thisChunk + 96);
				sb.Append("\x1b_G");

				if (isFirst)
					sb.Append($"a=f,i={imageId},r=1,f=24,s={pixelWidth},v={pixelHeight},o=z,q=2,");
				sb.Append(isLast ? "m=0" : "m=1");
				sb.Append(';');
				sb.Append(payload);
				sb.Append("\x1b\\");

				chunks.Add(sb.ToString());
				offset += thisChunk;
			}

			return chunks;
		}

		/// <summary>
		/// zlib-compresses the given data using <see cref="CompressionLevel.Fastest"/>. The
		/// output carries the standard zlib header/adler32 trailer that Kitty's <c>o=z</c>
		/// compression mode expects. "Fastest" trades a few percent of ratio for a big CPU
		/// win — critical at 30 fps video where each compress call is on the hot path.
		/// </summary>
		private static byte[] CompressZlib(byte[] data)
		{
			using var ms = new MemoryStream(data.Length / 2);
			using (var zs = new ZLibStream(ms, CompressionLevel.Fastest, leaveOpen: true))
			{
				zs.Write(data, 0, data.Length);
			}
			return ms.ToArray();
		}

		/// <summary>
		/// Builds the Kitty APC escape sequence to delete an image by ID.
		/// </summary>
		public static string BuildDeleteCommand(uint imageId)
		{
			return $"\x1b_Ga=d,d=I,i={imageId}\x1b\\";
		}

		/// <summary>
		/// Builds the Kitty APC query sequence to detect protocol support.
		/// </summary>
		public static string BuildQueryCommand()
		{
			return "\x1b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\";
		}

		#endregion

		#region PNG Encoding

		/// <summary>
		/// Encodes a PixelBuffer as PNG bytes using ImageSharp.
		/// </summary>
		public static byte[] EncodePng(PixelBuffer source, int targetWidth = 0, int targetHeight = 0)
		{
			var effectiveSource = source;
			if (targetWidth > 0 && targetHeight > 0 &&
				(targetWidth != source.Width || targetHeight != source.Height))
			{
				effectiveSource = source.Resize(targetWidth, targetHeight);
			}

			using var image = new Image<Rgb24>(effectiveSource.Width, effectiveSource.Height);

			image.ProcessPixelRows(accessor =>
			{
				for (int y = 0; y < accessor.Height; y++)
				{
					var row = accessor.GetRowSpan(y);
					for (int x = 0; x < row.Length; x++)
					{
						var pixel = effectiveSource.GetPixel(x, y);
						row[x] = new Rgb24(pixel.R, pixel.G, pixel.B);
					}
				}
			});

			using var ms = new MemoryStream();
			image.SaveAsPng(ms);
			return ms.ToArray();
		}

		#endregion
	}
}

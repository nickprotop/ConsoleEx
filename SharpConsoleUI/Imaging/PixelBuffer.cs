// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using StbImageSharp;

namespace SharpConsoleUI.Imaging
{
	/// <summary>
	/// A 2D buffer of RGB pixels used as the source for image rendering.
	/// </summary>
	public class PixelBuffer
	{
		private ImagePixel[,] _pixels;

		/// <summary>Width in pixels.</summary>
		public int Width { get; private set; }

		/// <summary>Height in pixels.</summary>
		public int Height { get; private set; }

		internal const int MaxPixelBufferDimension = 4096;

		/// <summary>Creates a new pixel buffer with the specified dimensions.</summary>
		public PixelBuffer(int width, int height)
		{
			if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
			if (width > MaxPixelBufferDimension || height > MaxPixelBufferDimension)
				throw new ArgumentOutOfRangeException(
					$"Image dimensions {width}x{height} exceed maximum {MaxPixelBufferDimension}.");

			Width = width;
			Height = height;
			_pixels = new ImagePixel[width, height];
		}

		/// <summary>Sets the pixel at the given coordinates. Out-of-bounds writes are silently ignored.</summary>
		public void SetPixel(int x, int y, ImagePixel pixel)
		{
			if (x < 0 || x >= Width || y < 0 || y >= Height)
				return;
			_pixels[x, y] = pixel;
		}

		/// <summary>Gets the pixel at the given coordinates. Returns default for out-of-bounds reads.</summary>
		public ImagePixel GetPixel(int x, int y)
		{
			if (x < 0 || x >= Width || y < 0 || y >= Height)
				return default;
			return _pixels[x, y];
		}

		/// <summary>
		/// Creates a new PixelBuffer at exactly the size asked for.
		/// </summary>
		/// <param name="targetWidth">Width in pixels. Must be positive.</param>
		/// <param name="targetHeight">Height in pixels. Must be positive.</param>
		/// <remarks>
		/// <para>
		/// Shrinking averages the source area each target pixel covers, a box filter, because
		/// bilinear sampling reads four source pixels per target pixel and therefore throws away
		/// most of an image being reduced by more than half: a 4000-pixel photo brought down to
		/// 1568 aliases into a shimmer of whichever four pixels happened to be sampled. Enlarging
		/// stays bilinear, where there is nothing to average and interpolation is what is wanted.
		/// </para>
		/// <para>
		/// NO SIZE CAP. This used to clamp each side to 500 independently, so an image asked to
		/// become 1568x882 came back 500x500: the wrong size, and the aspect ratio destroyed, with
		/// nothing reporting it. A cap belongs to whatever is displaying the result and knows what
		/// it can show, not to the resize itself.
		/// </para>
		/// </remarks>
		public PixelBuffer Resize(int targetWidth, int targetHeight)
		{
			if (targetWidth <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
			if (targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetHeight));

			// Shrinking on either axis takes the averaging path: a target pixel covering more than
			// one source pixel has an area to average, and sampling four of them would not see it.
			if (targetWidth < Width || targetHeight < Height)
				return ResizeByArea(targetWidth, targetHeight);

			var result = new PixelBuffer(targetWidth, targetHeight);

			for (int ty = 0; ty < targetHeight; ty++)
			{
				double srcY = (double)ty / targetHeight * Height;
				int y0 = Math.Min((int)srcY, Height - 1);
				int y1 = Math.Min(y0 + 1, Height - 1);
				double yFrac = srcY - y0;

				for (int tx = 0; tx < targetWidth; tx++)
				{
					double srcX = (double)tx / targetWidth * Width;
					int x0 = Math.Min((int)srcX, Width - 1);
					int x1 = Math.Min(x0 + 1, Width - 1);
					double xFrac = srcX - x0;

					var p00 = _pixels[x0, y0];
					var p10 = _pixels[x1, y0];
					var p01 = _pixels[x0, y1];
					var p11 = _pixels[x1, y1];

					byte r = BilinearInterpolate(p00.R, p10.R, p01.R, p11.R, xFrac, yFrac);
					byte g = BilinearInterpolate(p00.G, p10.G, p01.G, p11.G, xFrac, yFrac);
					byte b = BilinearInterpolate(p00.B, p10.B, p01.B, p11.B, xFrac, yFrac);

					result._pixels[tx, ty] = new ImagePixel(r, g, b);
				}
			}

			return result;
		}

		/// <summary>
		/// Resizes by averaging the source rectangle each target pixel covers.
		/// </summary>
		/// <remarks>
		/// Every source pixel contributes to exactly one target pixel, so nothing is dropped. The
		/// sums are long because a large source region times 255 overflows an int well before it
		/// overflows a long, and the count is never zero: a target pixel always covers at least the
		/// one source pixel its own position falls on.
		/// </remarks>
		private PixelBuffer ResizeByArea(int targetWidth, int targetHeight)
		{
			var result = new PixelBuffer(targetWidth, targetHeight);

			for (int ty = 0; ty < targetHeight; ty++)
			{
				int sy0 = (int)((long)ty * Height / targetHeight);
				int sy1 = (int)((long)(ty + 1) * Height / targetHeight);
				if (sy1 <= sy0) sy1 = sy0 + 1;
				if (sy1 > Height) sy1 = Height;

				for (int tx = 0; tx < targetWidth; tx++)
				{
					int sx0 = (int)((long)tx * Width / targetWidth);
					int sx1 = (int)((long)(tx + 1) * Width / targetWidth);
					if (sx1 <= sx0) sx1 = sx0 + 1;
					if (sx1 > Width) sx1 = Width;

					long r = 0, g = 0, b = 0, count = 0;
					for (int sy = sy0; sy < sy1; sy++)
					{
						for (int sx = sx0; sx < sx1; sx++)
						{
							var pixel = _pixels[sx, sy];
							r += pixel.R;
							g += pixel.G;
							b += pixel.B;
							count++;
						}
					}

					result._pixels[tx, ty] = new ImagePixel(
						(byte)(r / count), (byte)(g / count), (byte)(b / count));
				}
			}

			return result;
		}

		/// <summary>
		/// Creates a PixelBuffer from a flat pixel array in row-major order (left to right, top to bottom).
		/// </summary>
		public static PixelBuffer FromPixelArray(ImagePixel[] pixels, int width, int height)
		{
			if (pixels.Length != width * height)
				throw new ArgumentException("Pixel array length must equal width * height.");

			var buffer = new PixelBuffer(width, height);
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					buffer._pixels[x, y] = pixels[y * width + x];
				}
			}
			return buffer;
		}

		/// <summary>
		/// Creates a PixelBuffer from a flat array of ARGB 32-bit integers (alpha is ignored).
		/// </summary>
		public static PixelBuffer FromArgbArray(int[] argbPixels, int width, int height)
		{
			if (argbPixels.Length != width * height)
				throw new ArgumentException("ARGB array length must equal width * height.");

			var buffer = new PixelBuffer(width, height);
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int argb = argbPixels[y * width + x];
					byte r = (byte)((argb >> 16) & 0xFF);
					byte g = (byte)((argb >> 8) & 0xFF);
					byte b = (byte)(argb & 0xFF);
					buffer._pixels[x, y] = new ImagePixel(r, g, b);
				}
			}
			return buffer;
		}

		/// <summary>
		/// Decodes image data into a <see cref="PixelBuffer"/>.
		/// </summary>
		/// <remarks>
		/// The decoder recognises PNG, JPEG, BMP, GIF and TGA, and identifies each from its
		/// content rather than from a file extension or a <c>Content-Type</c>.
		/// <para>
		/// TIFF IS NOT DECODABLE HERE, and that is a security property rather than an omission.
		/// Images reach this method from places the application does not control — a <c>data:</c>
		/// URI or an <c>&lt;img&gt;</c> in HTML from the network, a file the user opened. TIFF
		/// decoders have carried heap out-of-bounds writes in their CCITT fax paths
		/// (CVE-2026-106117, CVE-2026-106118) and a BigTIFF header that spins forever on 24 bytes
		/// of input (GHSA-wmxv-xphr-5c9g). No TIFF decoder is linked into the library at all, so a
		/// TIFF is refused on inspection, before any format-specific parsing runs.
		/// </para>
		/// </remarks>
		private static PixelBuffer Decode(byte[] data)
		{
			ImageResult image;
			try
			{
				image = ImageResult.FromMemory(data, StbImageSharp.ColorComponents.RedGreenBlue);
			}
			catch (Exception ex)
			{
				// The decoder reports an unrecognised or malformed image by throwing. Translate it
				// so callers have one documented type to catch regardless of the backend.
				throw new UnsupportedImageFormatException(
					"The data is not in a supported image format (PNG, JPEG, BMP, GIF or TGA).", ex);
			}

			if (image.Width <= 0 || image.Height <= 0)
				throw new UnsupportedImageFormatException("The image reports empty dimensions.");

			if (image.Width > MaxPixelBufferDimension || image.Height > MaxPixelBufferDimension)
				throw new ArgumentOutOfRangeException(
					nameof(data),
					$"Image dimensions {image.Width}x{image.Height} exceed maximum {MaxPixelBufferDimension}.");

			return FromRgb24Bytes(image.Data, image.Width, image.Height);
		}

		/// <summary>
		/// Creates a PixelBuffer by loading an image from a file path.
		/// Supports PNG, JPEG, BMP, GIF and TGA formats.
		/// </summary>
		/// <param name="filePath">Path to the image file.</param>
		/// <returns>A PixelBuffer containing the decoded image pixels.</returns>
		/// <exception cref="UnsupportedImageFormatException">
		/// The data is not one of the supported formats. A TIFF lands here by design — see
		/// <see cref="Decode"/>.
		/// </exception>
		public static PixelBuffer FromFile(string filePath)
		{
			return Decode(File.ReadAllBytes(filePath));
		}

		/// <summary>
		/// Creates a PixelBuffer by loading an image from a stream.
		/// Supports PNG, JPEG, BMP, GIF and TGA formats.
		/// </summary>
		/// <param name="stream">Stream containing image data.</param>
		/// <returns>A PixelBuffer containing the decoded image pixels.</returns>
		/// <exception cref="UnsupportedImageFormatException">
		/// The data is not one of the supported formats. A TIFF lands here by design — see
		/// <see cref="Decode"/>.
		/// </exception>
		public static PixelBuffer FromStream(Stream stream)
		{
			if (stream is MemoryStream ms)
				return Decode(ms.ToArray());

			using var copy = new MemoryStream();
			stream.CopyTo(copy);
			return Decode(copy.ToArray());
		}

		/// <summary>
		/// Creates a PixelBuffer from tightly packed 24-bit RGB bytes in row-major order
		/// (left to right, top to bottom, three bytes per pixel).
		/// </summary>
		/// <param name="rgb">The pixel bytes. Length must be at least <c>width * height * 3</c>.</param>
		/// <param name="width">Image width in pixels.</param>
		/// <param name="height">Image height in pixels.</param>
		/// <remarks>
		/// This is the seam for pixels produced by another imaging library: copy that library's
		/// pixels into a byte buffer and hand it here, and no imaging dependency is shared with
		/// SharpConsoleUI.
		/// </remarks>
		public static PixelBuffer FromRgb24Bytes(ReadOnlySpan<byte> rgb, int width, int height)
		{
			if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

			long required = (long)width * height * 3;
			if (rgb.Length < required)
				throw new ArgumentException(
					$"Expected at least {required} bytes for {width}x{height} RGB, got {rgb.Length}.",
					nameof(rgb));

			var buffer = new PixelBuffer(width, height);
			for (int y = 0; y < height; y++)
			{
				int row = y * width * 3;
				for (int x = 0; x < width; x++)
				{
					int i = row + x * 3;
					buffer._pixels[x, y] = new ImagePixel(rgb[i], rgb[i + 1], rgb[i + 2]);
				}
			}
			return buffer;
		}

		/// <summary>
		/// Creates a PixelBuffer from tightly packed 32-bit RGBA bytes in row-major order
		/// (left to right, top to bottom, four bytes per pixel). The alpha byte is ignored.
		/// </summary>
		/// <param name="rgba">The pixel bytes. Length must be at least <c>width * height * 4</c>.</param>
		/// <param name="width">Image width in pixels.</param>
		/// <param name="height">Image height in pixels.</param>
		public static PixelBuffer FromRgba32Bytes(ReadOnlySpan<byte> rgba, int width, int height)
		{
			if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

			long required = (long)width * height * 4;
			if (rgba.Length < required)
				throw new ArgumentException(
					$"Expected at least {required} bytes for {width}x{height} RGBA, got {rgba.Length}.",
					nameof(rgba));

			var buffer = new PixelBuffer(width, height);
			for (int y = 0; y < height; y++)
			{
				int row = y * width * 4;
				for (int x = 0; x < width; x++)
				{
					int i = row + x * 4;
					buffer._pixels[x, y] = new ImagePixel(rgba[i], rgba[i + 1], rgba[i + 2]);
				}
			}
			return buffer;
		}

		private static byte BilinearInterpolate(byte v00, byte v10, byte v01, byte v11, double xFrac, double yFrac)
		{
			double top = v00 + (v10 - v00) * xFrac;
			double bottom = v01 + (v11 - v01) * xFrac;
			return (byte)Math.Clamp(top + (bottom - top) * yFrac, 0, 255);
		}
	}
}

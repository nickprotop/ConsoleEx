// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

		/// <summary>Creates a new pixel buffer with the specified dimensions.</summary>
		public PixelBuffer(int width, int height)
		{
			if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

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
		/// Creates a new PixelBuffer resized to the target dimensions using bilinear interpolation.
		/// </summary>
		public PixelBuffer Resize(int targetWidth, int targetHeight)
		{
			if (targetWidth <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
			if (targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetHeight));

			targetWidth = Math.Min(targetWidth, ImagingDefaults.MaxImageDimension);
			targetHeight = Math.Min(targetHeight, ImagingDefaults.MaxImageDimension);

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
		/// Creates a PixelBuffer by loading an image from a file path.
		/// Supports PNG, JPEG, BMP, GIF, TIFF, TGA, PBM, and WebP formats.
		/// </summary>
		/// <param name="filePath">Path to the image file.</param>
		/// <returns>A PixelBuffer containing the decoded image pixels.</returns>
		public static PixelBuffer FromFile(string filePath)
		{
			using var image = SixLabors.ImageSharp.Image.Load<Rgb24>(filePath);
			return FromImageSharp(image);
		}

		/// <summary>
		/// Creates a PixelBuffer by loading an image from a stream.
		/// Supports PNG, JPEG, BMP, GIF, TIFF, TGA, PBM, and WebP formats.
		/// </summary>
		/// <param name="stream">Stream containing image data.</param>
		/// <returns>A PixelBuffer containing the decoded image pixels.</returns>
		public static PixelBuffer FromStream(Stream stream)
		{
			using var image = SixLabors.ImageSharp.Image.Load<Rgb24>(stream);
			return FromImageSharp(image);
		}

		/// <summary>
		/// Creates a PixelBuffer from an ImageSharp Image&lt;Rgb24&gt;.
		/// </summary>
		public static PixelBuffer FromImageSharp(Image<Rgb24> image)
		{
			var buffer = new PixelBuffer(image.Width, image.Height);

			image.ProcessPixelRows(accessor =>
			{
				for (int y = 0; y < accessor.Height; y++)
				{
					var row = accessor.GetRowSpan(y);
					for (int x = 0; x < row.Length; x++)
					{
						ref var pixel = ref row[x];
						buffer._pixels[x, y] = new ImagePixel(pixel.R, pixel.G, pixel.B);
					}
				}
			});

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

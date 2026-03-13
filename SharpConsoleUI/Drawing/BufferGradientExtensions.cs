// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Drawing
{
	/// <summary>
	/// Extension methods for gradient, pattern, and stipple fills on CharacterBuffer.
	/// </summary>
	public static class BufferGradientExtensions
	{
		#region Gradient Fills

		/// <summary>
		/// Fills a rectangle with a horizontal foreground color gradient (left to right).
		/// </summary>
		public static void GradientFillHorizontal(this CharacterBuffer buffer, LayoutRect rect,
			char ch, Color fgStart, Color fgEnd, Color bg, LayoutRect? clipRect = null)
		{
			var clip = clipRect ?? buffer.Bounds;
			if (rect.Width <= 0 || rect.Height <= 0)
				return;

			var colors = InterpolateColors(fgStart, fgEnd, rect.Width);

			for (int y = rect.Y; y < rect.Bottom; y++)
			{
				for (int x = rect.X; x < rect.Right; x++)
				{
					if (clip.Contains(x, y))
						buffer.SetNarrowCell(x, y, ch, colors[x - rect.X], bg);
				}
			}
		}

		/// <summary>
		/// Fills a rectangle with a vertical foreground color gradient (top to bottom).
		/// </summary>
		public static void GradientFillVertical(this CharacterBuffer buffer, LayoutRect rect,
			char ch, Color fgStart, Color fgEnd, Color bg, LayoutRect? clipRect = null)
		{
			var clip = clipRect ?? buffer.Bounds;
			if (rect.Width <= 0 || rect.Height <= 0)
				return;

			var colors = InterpolateColors(fgStart, fgEnd, rect.Height);

			for (int y = rect.Y; y < rect.Bottom; y++)
			{
				var fg = colors[y - rect.Y];
				for (int x = rect.X; x < rect.Right; x++)
				{
					if (clip.Contains(x, y))
						buffer.SetNarrowCell(x, y, ch, fg, bg);
				}
			}
		}

		/// <summary>
		/// Fills a rectangle with a background color gradient (no visible character).
		/// </summary>
		public static void GradientFillRect(this CharacterBuffer buffer, LayoutRect rect,
			Color bgStart, Color bgEnd, bool horizontal, LayoutRect? clipRect = null)
		{
			var clip = clipRect ?? buffer.Bounds;
			if (rect.Width <= 0 || rect.Height <= 0)
				return;

			int length = horizontal ? rect.Width : rect.Height;
			var colors = InterpolateColors(bgStart, bgEnd, length);

			for (int y = rect.Y; y < rect.Bottom; y++)
			{
				for (int x = rect.X; x < rect.Right; x++)
				{
					if (clip.Contains(x, y))
					{
						var bgColor = colors[horizontal ? x - rect.X : y - rect.Y];
						buffer.SetNarrowCell(x, y, ' ', Color.White, bgColor);
					}
				}
			}
		}

		private static Color[] InterpolateColors(Color start, Color end, int length)
		{
			if (length <= 0)
				return Array.Empty<Color>();

			var colors = new Color[length];
			if (length == 1)
			{
				colors[0] = start;
				return colors;
			}

			for (int i = 0; i < length; i++)
			{
				double t = (double)i / (length - 1);
				byte r = (byte)(start.R + (end.R - start.R) * t);
				byte g = (byte)(start.G + (end.G - start.G) * t);
				byte b = (byte)(start.B + (end.B - start.B) * t);
				colors[i] = new Color(r, g, b);
			}

			return colors;
		}

		#endregion

		#region Pattern Fills

		/// <summary>
		/// Fills a rectangle with a repeating 2D character pattern.
		/// Each string in the pattern array represents one row of the repeating tile.
		/// </summary>
		public static void PatternFill(this CharacterBuffer buffer, LayoutRect rect,
			string[] pattern, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (pattern.Length == 0)
				return;

			var clip = clipRect ?? buffer.Bounds;

			for (int y = rect.Y; y < rect.Bottom; y++)
			{
				string row = pattern[(y - rect.Y) % pattern.Length];
				if (row.Length == 0)
					continue;

				for (int x = rect.X; x < rect.Right; x++)
				{
					if (clip.Contains(x, y))
					{
						char ch = row[(x - rect.X) % row.Length];
						buffer.SetNarrowCell(x, y, ch, fg, bg);
					}
				}
			}
		}

		/// <summary>
		/// Fills a rectangle with alternating characters in a checkerboard pattern.
		/// </summary>
		public static void CheckerFill(this CharacterBuffer buffer, LayoutRect rect,
			char ch1, char ch2, Color fg1, Color fg2, Color bg, LayoutRect? clipRect = null)
		{
			var clip = clipRect ?? buffer.Bounds;

			for (int y = rect.Y; y < rect.Bottom; y++)
			{
				for (int x = rect.X; x < rect.Right; x++)
				{
					if (clip.Contains(x, y))
					{
						bool even = (x + y) % 2 == 0;
						buffer.SetNarrowCell(x, y, even ? ch1 : ch2, even ? fg1 : fg2, bg);
					}
				}
			}
		}

		/// <summary>
		/// Fills a rectangle with a density-based stipple pattern.
		/// Density ranges from 0.0 (empty) to 1.0 (fully filled).
		/// Uses a deterministic hash for consistent results across redraws.
		/// </summary>
		public static void StippleFill(this CharacterBuffer buffer, LayoutRect rect,
			double density, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			var clip = clipRect ?? buffer.Bounds;
			density = Math.Clamp(density, 0.0, 1.0);

			int threshold = (int)(density * DrawingConstants.StippleModulus);

			// Choose character based on density ranges
			char ch = density switch
			{
				<= DrawingConstants.StippleLightThreshold => DrawingConstants.BlockLight,
				<= DrawingConstants.StippleMediumThreshold => DrawingConstants.BlockMedium,
				<= DrawingConstants.StippleDarkThreshold => DrawingConstants.BlockDark,
				_ => DrawingConstants.BlockFull
			};

			for (int y = rect.Y; y < rect.Bottom; y++)
			{
				for (int x = rect.X; x < rect.Right; x++)
				{
					if (clip.Contains(x, y))
					{
						int hash = ((x * DrawingConstants.StipplePrimeX) + (y * DrawingConstants.StipplePrimeY)) % DrawingConstants.StippleModulus;
						if (hash < 0) hash += DrawingConstants.StippleModulus;

						if (hash < threshold)
							buffer.SetNarrowCell(x, y, ch, fg, bg);
						else
							buffer.SetNarrowCell(x, y, ' ', fg, bg);
					}
				}
			}
		}

		#endregion
	}
}

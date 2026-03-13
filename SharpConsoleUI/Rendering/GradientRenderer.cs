using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Rendering
{
	/// <summary>
	/// Fills regions of a CharacterBuffer with gradient colors.
	/// Supports horizontal, vertical, and diagonal gradient directions.
	/// </summary>
	public static class GradientRenderer
	{
		/// <summary>
		/// Fills the background color of cells in the specified rectangle with a gradient.
		/// Existing characters and foreground colors are preserved.
		/// </summary>
		/// <param name="buffer">The buffer to paint into.</param>
		/// <param name="rect">The rectangle to fill.</param>
		/// <param name="gradient">The color gradient to apply.</param>
		/// <param name="direction">The direction of the gradient.</param>
		public static void FillGradientBackground(
			CharacterBuffer buffer,
			LayoutRect rect,
			ColorGradient gradient,
			GradientDirection direction)
		{
			var clipped = rect.Intersect(buffer.Bounds);
			if (clipped.IsEmpty || clipped.Width == 0 || clipped.Height == 0)
				return;

			for (int y = clipped.Y; y < clipped.Bottom; y++)
			{
				for (int x = clipped.X; x < clipped.Right; x++)
				{
					double t = CalculateNormalizedPosition(
						x, y, clipped.X, clipped.Y, clipped.Width, clipped.Height, direction);
					var bgColor = gradient.Interpolate(t);
					var existing = buffer.GetCell(x, y);
					var updated = new Cell(existing.Character, existing.Foreground, bgColor, existing.Decorations)
					{
						IsWideContinuation = existing.IsWideContinuation,
						Combiners = existing.Combiners
					};
					buffer.SetCell(x, y, updated);
				}
			}
		}

		/// <summary>
		/// Fills the foreground color of cells in the specified rectangle with a gradient.
		/// Existing characters and background colors are preserved.
		/// </summary>
		/// <param name="buffer">The buffer to paint into.</param>
		/// <param name="rect">The rectangle to fill.</param>
		/// <param name="gradient">The color gradient to apply.</param>
		/// <param name="direction">The direction of the gradient.</param>
		public static void FillGradientForeground(
			CharacterBuffer buffer,
			LayoutRect rect,
			ColorGradient gradient,
			GradientDirection direction)
		{
			var clipped = rect.Intersect(buffer.Bounds);
			if (clipped.IsEmpty || clipped.Width == 0 || clipped.Height == 0)
				return;

			for (int y = clipped.Y; y < clipped.Bottom; y++)
			{
				for (int x = clipped.X; x < clipped.Right; x++)
				{
					double t = CalculateNormalizedPosition(
						x, y, clipped.X, clipped.Y, clipped.Width, clipped.Height, direction);
					var fgColor = gradient.Interpolate(t);
					var existing = buffer.GetCell(x, y);
					var updated = new Cell(existing.Character, fgColor, existing.Background, existing.Decorations)
					{
						IsWideContinuation = existing.IsWideContinuation,
						Combiners = existing.Combiners
					};
					buffer.SetCell(x, y, updated);
				}
			}
		}

		/// <summary>
		/// Calculates the normalized position (0.0 to 1.0) for gradient interpolation
		/// based on direction and cell coordinates within the region.
		/// </summary>
		internal static double CalculateNormalizedPosition(
			int x, int y,
			int regionX, int regionY,
			int regionWidth, int regionHeight,
			GradientDirection direction)
		{
			switch (direction)
			{
				case GradientDirection.Horizontal:
					return regionWidth <= 1 ? 0.0 : (double)(x - regionX) / (regionWidth - 1);

				case GradientDirection.Vertical:
					return regionHeight <= 1 ? 0.0 : (double)(y - regionY) / (regionHeight - 1);

				case GradientDirection.DiagonalDown:
				{
					int maxDistance = (regionWidth - 1) + (regionHeight - 1);
					if (maxDistance == 0) return 0.0;
					int distance = (x - regionX) + (y - regionY);
					return (double)distance / maxDistance;
				}

				case GradientDirection.DiagonalUp:
				{
					int maxDistance = (regionWidth - 1) + (regionHeight - 1);
					if (maxDistance == 0) return 0.0;
					int distance = (x - regionX) + ((regionHeight - 1) - (y - regionY));
					return (double)distance / maxDistance;
				}

				default:
					return 0.0;
			}
		}
	}
}

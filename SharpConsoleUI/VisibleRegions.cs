// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;

namespace SharpConsoleUI
{
	/// <summary>
	/// Calculates visible regions of windows by subtracting overlapping areas from higher z-order windows.
	/// Used by the rendering system to determine which portions of a window need to be drawn.
	/// </summary>
	public class VisibleRegions
	{
		private readonly ConsoleWindowSystem _consoleWindowSystem;

		// Performance optimization: dual-buffer pooling to avoid List allocations
		private readonly List<Rectangle> _regionsBuffer1 = new List<Rectangle>(8);
		private readonly List<Rectangle> _regionsBuffer2 = new List<Rectangle>(8);

		/// <summary>
		/// Initializes a new instance of the <see cref="VisibleRegions"/> class.
		/// </summary>
		/// <param name="consoleWindowSystem">The console window system that owns this visible regions calculator.</param>
		public VisibleRegions(ConsoleWindowSystem consoleWindowSystem)
		{
			_consoleWindowSystem = consoleWindowSystem;
		}

		/// <summary>
		/// Calculates the visible regions of a window by subtracting areas covered by overlapping windows.
		/// </summary>
		/// <param name="window">The window to calculate visible regions for.</param>
		/// <param name="overlappingWindows">List of windows that overlap with the target window (typically higher z-order).</param>
		/// <returns>A list of rectangles representing the visible portions of the window.</returns>
		public List<Rectangle> CalculateVisibleRegions(Window window, List<Window> overlappingWindows)
		{
			// Use pooled buffers to avoid allocations
			_regionsBuffer1.Clear();
			_regionsBuffer1.Add(new Rectangle(window.Left, window.Top, window.Width, window.Height));

			var current = _regionsBuffer1;
			var next = _regionsBuffer2;

			// For each overlapping window, subtract its area from the visible regions
			foreach (var other in overlappingWindows)
			{
				var overlappingRect = new Rectangle(
					other.Left,
					other.Top,
					other.Width,
					other.Height);

				next.Clear();
				SubtractRectangle(current, overlappingRect, next);

				// Swap buffers for next iteration
				var temp = current;
				current = next;
				next = temp;
			}

			// Return copy for caller (they expect ownership)
			return new List<Rectangle>(current);
		}

		private bool DoRectanglesIntersect(Rectangle r1, Rectangle r2)
		{
			return r1.Left < r2.Left + r2.Width &&
				   r1.Left + r1.Width > r2.Left &&
				   r1.Top < r2.Top + r2.Height &&
				   r1.Top + r1.Height > r2.Top;
		}

		private Rectangle GetIntersection(Rectangle r1, Rectangle r2)
		{
			int left = Math.Max(r1.Left, r2.Left);
			int top = Math.Max(r1.Top, r2.Top);
			int right = Math.Min(r1.Left + r1.Width, r2.Left + r2.Width);
			int bottom = Math.Min(r1.Top + r1.Height, r2.Top + r2.Height);

			return new Rectangle(
				left,
				top,
				Math.Max(0, right - left),
				Math.Max(0, bottom - top));
		}

		private void SubtractRectangle(List<Rectangle> regions, Rectangle subtract, List<Rectangle> result)
		{
			// result is pre-cleared by caller

			foreach (var region in regions)
			{
				// If regions don't intersect, keep the original region
				if (!DoRectanglesIntersect(region, subtract))
				{
					result.Add(region);
					continue;
				}

				// Calculate the intersection
				var intersection = GetIntersection(region, subtract);

				// If region is completely covered, skip it
				if (intersection.Width == region.Width && intersection.Height == region.Height)
				{
					continue;
				}

				// Otherwise, split the region into up to 4 sub-regions

				// Region above the intersection
				if (intersection.Top > region.Top)
				{
					result.Add(new Rectangle(
						region.Left,
						region.Top,
						region.Width,
						intersection.Top - region.Top));
				}

				// Region below the intersection
				if (intersection.Top + intersection.Height < region.Top + region.Height)
				{
					result.Add(new Rectangle(
						region.Left,
						intersection.Top + intersection.Height,
						region.Width,
						region.Top + region.Height - (intersection.Top + intersection.Height)));
				}

				// Region to the left of the intersection
				if (intersection.Left > region.Left)
				{
					result.Add(new Rectangle(
						region.Left,
						Math.Max(region.Top, intersection.Top),
						intersection.Left - region.Left,
						Math.Min(region.Top + region.Height, intersection.Top + intersection.Height) - Math.Max(region.Top, intersection.Top)));
				}

				// Region to the right of the intersection
				if (intersection.Left + intersection.Width < region.Left + region.Width)
				{
					result.Add(new Rectangle(
						intersection.Left + intersection.Width,
						Math.Max(region.Top, intersection.Top),
						region.Left + region.Width - (intersection.Left + intersection.Width),
						Math.Min(region.Top + region.Height, intersection.Top + intersection.Height) - Math.Max(region.Top, intersection.Top)));
				}
			}

			}
	}
}
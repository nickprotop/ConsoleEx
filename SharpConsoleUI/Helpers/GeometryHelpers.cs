// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides static helper methods for Rectangle geometry operations.
	/// Extracted from ConsoleWindowSystem as part of Phase 3.1 refactoring.
	/// Consolidates duplicate geometry logic from multiple classes.
	/// </summary>
	public static class GeometryHelpers
	{
		/// <summary>
		/// Checks if two rectangles intersect.
		/// </summary>
		/// <param name="rect1">The first rectangle.</param>
		/// <param name="rect2">The second rectangle.</param>
		/// <returns>True if the rectangles intersect; false otherwise.</returns>
		public static bool DoesRectangleIntersect(Rectangle rect1, Rectangle rect2)
		{
			return rect1.X < rect2.X + rect2.Width &&
				   rect1.X + rect1.Width > rect2.X &&
				   rect1.Y < rect2.Y + rect2.Height &&
				   rect1.Y + rect1.Height > rect2.Y;
		}

		/// <summary>
		/// Checks if a rectangle overlaps with a window's bounds.
		/// </summary>
		/// <param name="rect">The rectangle to check.</param>
		/// <param name="window">The window to check against.</param>
		/// <returns>True if the rectangle overlaps the window; false otherwise.</returns>
		public static bool DoesRectangleOverlapWindow(Rectangle rect, Window window)
		{
			return rect.X < window.Left + window.Width &&
				   rect.X + rect.Width > window.Left &&
				   rect.Y < window.Top + window.Height &&
				   rect.Y + rect.Height > window.Top;
		}

		/// <summary>
		/// Calculates the intersection of two rectangles.
		/// </summary>
		/// <param name="rect1">The first rectangle.</param>
		/// <param name="rect2">The second rectangle.</param>
		/// <returns>The intersection rectangle, or Rectangle.Empty if no intersection.</returns>
		public static Rectangle GetRectangleIntersection(Rectangle rect1, Rectangle rect2)
		{
			int left = Math.Max(rect1.X, rect2.X);
			int top = Math.Max(rect1.Y, rect2.Y);
			int right = Math.Min(rect1.X + rect1.Width, rect2.X + rect2.Width);
			int bottom = Math.Min(rect1.Y + rect1.Height, rect2.Y + rect2.Height);

			if (left < right && top < bottom)
				return new Rectangle(left, top, right - left, bottom - top);
			else
				return Rectangle.Empty;
		}

		/// <summary>
		/// Subtracts a rectangle from a list of regions.
		/// Returns the parts of the regions that are not covered by the subtracted rectangle.
		/// </summary>
		/// <param name="regions">The list of regions to subtract from.</param>
		/// <param name="subtract">The rectangle to subtract.</param>
		/// <returns>A list of rectangles representing the uncovered areas.</returns>
		public static List<Rectangle> SubtractRectangleFromRegions(List<Rectangle> regions, Rectangle subtract)
		{
			var result = new List<Rectangle>();

			foreach (var region in regions)
			{
				// If regions don't intersect, keep the original region
				if (!DoesRectangleIntersect(region, subtract))
				{
					result.Add(region);
					continue;
				}

				// Calculate the intersection
				var intersection = GetRectangleIntersection(region, subtract);

				// If region is completely covered, skip it
				if (intersection.Width == region.Width && intersection.Height == region.Height)
				{
					continue;
				}

				// Split the region into up to 4 sub-regions around the intersection

				// Region above the intersection
				if (intersection.Y > region.Y)
				{
					result.Add(new Rectangle(region.X, region.Y, region.Width, intersection.Y - region.Y));
				}

				// Region below the intersection
				if (intersection.Y + intersection.Height < region.Y + region.Height)
				{
					result.Add(new Rectangle(region.X, intersection.Y + intersection.Height, region.Width,
						region.Y + region.Height - (intersection.Y + intersection.Height)));
				}

				// Region to the left of the intersection
				if (intersection.X > region.X)
				{
					result.Add(new Rectangle(region.X, intersection.Y, intersection.X - region.X, intersection.Height));
				}

				// Region to the right of the intersection
				if (intersection.X + intersection.Width < region.X + region.Width)
				{
					result.Add(new Rectangle(intersection.X + intersection.Width, intersection.Y,
						region.X + region.Width - (intersection.X + intersection.Width), intersection.Height));
				}
			}

			return result;
		}

		/// <summary>
		/// Calculates which parts of a region are not covered by a list of windows.
		/// </summary>
		/// <param name="region">The region to check.</param>
		/// <param name="coveringWindows">The windows that may cover the region.</param>
		/// <returns>A list of rectangles representing the uncovered areas.</returns>
		public static List<Rectangle> CalculateUncoveredRegions(Rectangle region, List<Window> coveringWindows)
		{
			// Start with the entire region
			var regions = new List<Rectangle> { region };

			// Subtract each covering window's area
			foreach (var window in coveringWindows)
			{
				var windowRect = new Rectangle(window.Left, window.Top, window.Width, window.Height);
				regions = SubtractRectangleFromRegions(regions, windowRect);
			}

			return regions;
		}

		/// <summary>
		/// Calculates regions that were covered by oldBounds but are not covered by newBounds.
		/// </summary>
		/// <param name="oldBounds">The old rectangle bounds.</param>
		/// <param name="newBounds">The new rectangle bounds.</param>
		/// <returns>A list of rectangles representing the exposed areas.</returns>
		public static List<Rectangle> CalculateExposedRegions(Rectangle oldBounds, Rectangle newBounds)
		{
			// If the old and new bounds are the same, no exposed regions
			if (oldBounds.Equals(newBounds))
				return new List<Rectangle>();

			// If there's no overlap between old and new, the entire old bounds is exposed
			if (!DoesRectangleIntersect(oldBounds, newBounds))
				return new List<Rectangle> { oldBounds };

			// Start with the old bounds
			var regions = new List<Rectangle> { oldBounds };

			// Subtract the new bounds from the old bounds to get exposed areas
			return SubtractRectangleFromRegions(regions, newBounds);
		}

		/// <summary>
		/// Optimizes exposed regions by removing empty rectangles.
		/// Could be enhanced to merge adjacent rectangles.
		/// </summary>
		/// <param name="regions">The regions to optimize.</param>
		/// <returns>The optimized list of regions.</returns>
		public static List<Rectangle> OptimizeExposedRegions(List<Rectangle> regions)
		{
			// For now, just return regions with positive dimensions
			// Could be enhanced later to merge adjacent rectangles
			return regions.Where(r => r.Width > 0 && r.Height > 0).ToList();
		}
	}
}

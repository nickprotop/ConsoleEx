using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx.Drivers
{
	public class VisibleRegions
	{
		private readonly ConsoleWindowSystem _consoleWindowSystem;

		public VisibleRegions(ConsoleWindowSystem consoleWindowSystem)
		{
			_consoleWindowSystem = consoleWindowSystem;
		}

		public List<Rectangle> CalculateVisibleRegions(Window window, List<Window> overlappingWindows)
		{
			// Start with the entire window as visible
			var regions = new List<Rectangle>
			{
				new Rectangle(window.Left, window.Top, window.Width, window.Height)
			};

			// For each overlapping window, subtract its area from the visible regions
			foreach (var other in overlappingWindows)
			{
				var overlappingRect = new Rectangle(
					other.Left,
					other.Top,
					other.Width,
					other.Height);

				regions = SubtractRectangle(regions, overlappingRect);
			}

			return regions;
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

		private List<Rectangle> SubtractRectangle(List<Rectangle> regions, Rectangle subtract)
		{
			var result = new List<Rectangle>();

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
						(region.Top + region.Height) - (intersection.Top + intersection.Height)));
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
						(region.Left + region.Width) - (intersection.Left + intersection.Width),
						Math.Min(region.Top + region.Height, intersection.Top + intersection.Height) - Math.Max(region.Top, intersection.Top)));
				}
			}

			return result;
		}
	}
}
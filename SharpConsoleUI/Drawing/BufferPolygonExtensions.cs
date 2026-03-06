// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using Spectre.Console;

namespace SharpConsoleUI.Drawing
{
	/// <summary>
	/// Extension methods for polygon drawing on CharacterBuffer.
	/// </summary>
	public static class BufferPolygonExtensions
	{
		#region Triangle Drawing

		/// <summary>
		/// Draws a triangle outline by connecting three vertices with lines.
		/// </summary>
		public static void DrawTriangle(this CharacterBuffer buffer,
			int x0, int y0, int x1, int y1, int x2, int y2,
			char ch, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			buffer.DrawLine(x0, y0, x1, y1, ch, fg, bg, clipRect);
			buffer.DrawLine(x1, y1, x2, y2, ch, fg, bg, clipRect);
			buffer.DrawLine(x2, y2, x0, y0, ch, fg, bg, clipRect);
		}

		/// <summary>
		/// Draws a filled triangle using scanline rasterization.
		/// Vertices are sorted by Y, then edges are interpolated per scanline.
		/// </summary>
		public static void FillTriangle(this CharacterBuffer buffer,
			int x0, int y0, int x1, int y1, int x2, int y2,
			char ch, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			var clip = clipRect ?? buffer.Bounds;

			// Sort vertices by Y coordinate
			if (y0 > y1) { (x0, y0, x1, y1) = (x1, y1, x0, y0); }
			if (y0 > y2) { (x0, y0, x2, y2) = (x2, y2, x0, y0); }
			if (y1 > y2) { (x1, y1, x2, y2) = (x2, y2, x1, y1); }

			// Now y0 <= y1 <= y2
			if (y0 == y2)
			{
				// Degenerate triangle (all on same line)
				int minX = Math.Min(x0, Math.Min(x1, x2));
				int maxX = Math.Max(x0, Math.Max(x1, x2));
				BufferGraphicsExtensions.DrawHorizontalSpan(buffer, minX, maxX, y0, ch, fg, bg, clip);
				return;
			}

			int totalHeight = y2 - y0;

			for (int y = y0; y <= y2; y++)
			{
				bool secondHalf = y > y1 || y1 == y0;
				int segmentHeight = secondHalf ? y2 - y1 : y1 - y0;

				if (segmentHeight == 0)
					continue;

				double alpha = (double)(y - y0) / totalHeight;
				double beta = secondHalf
					? (double)(y - y1) / segmentHeight
					: (double)(y - y0) / segmentHeight;

				int xa = x0 + (int)Math.Round((x2 - x0) * alpha);
				int xb = secondHalf
					? x1 + (int)Math.Round((x2 - x1) * beta)
					: x0 + (int)Math.Round((x1 - x0) * beta);

				if (xa > xb) (xa, xb) = (xb, xa);

				BufferGraphicsExtensions.DrawHorizontalSpan(buffer, xa, xb, y, ch, fg, bg, clip);
			}
		}

		#endregion

		#region Polygon Drawing

		/// <summary>
		/// Draws a polygon outline by connecting consecutive points with lines.
		/// </summary>
		public static void DrawPolygon(this CharacterBuffer buffer, ReadOnlySpan<(int X, int Y)> points,
			char ch, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (points.Length < 2)
				return;

			for (int i = 0; i < points.Length; i++)
			{
				int next = (i + 1) % points.Length;
				buffer.DrawLine(points[i].X, points[i].Y, points[next].X, points[next].Y, ch, fg, bg, clipRect);
			}
		}

		/// <summary>
		/// Draws a filled polygon using scanline rasterization with the even-odd rule.
		/// </summary>
		public static void FillPolygon(this CharacterBuffer buffer, ReadOnlySpan<(int X, int Y)> points,
			char ch, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (points.Length < 3)
				return;

			var clip = clipRect ?? buffer.Bounds;

			// Find bounding box
			int minY = points[0].Y, maxY = points[0].Y;
			for (int i = 1; i < points.Length; i++)
			{
				if (points[i].Y < minY) minY = points[i].Y;
				if (points[i].Y > maxY) maxY = points[i].Y;
			}

			// Clamp to clip region
			minY = Math.Max(minY, clip.Y);
			maxY = Math.Min(maxY, clip.Y + clip.Height - 1);

			// Reusable intersection list
			var intersections = new List<int>();

			for (int y = minY; y <= maxY; y++)
			{
				intersections.Clear();

				// Find all X intersections with polygon edges
				for (int i = 0; i < points.Length; i++)
				{
					int j = (i + 1) % points.Length;
					int yi = points[i].Y;
					int yj = points[j].Y;

					if (yi == yj)
						continue;

					// Check if scanline crosses this edge
					if ((y >= yi && y < yj) || (y >= yj && y < yi))
					{
						double t = (double)(y - yi) / (yj - yi);
						int xIntersect = points[i].X + (int)Math.Round((points[j].X - points[i].X) * t);
						intersections.Add(xIntersect);
					}
				}

				intersections.Sort();

				// Fill between pairs of intersections (even-odd rule)
				for (int i = 0; i + 1 < intersections.Count; i += 2)
				{
					BufferGraphicsExtensions.DrawHorizontalSpan(buffer, intersections[i], intersections[i + 1], y, ch, fg, bg, clip);
				}
			}
		}

		#endregion
	}
}

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
	/// Extension methods for drawing lines, circles, ellipses, and arcs on CharacterBuffer.
	/// </summary>
	public static class BufferGraphicsExtensions
	{
		#region Clip Helpers

		private static LayoutRect ResolveClipRect(CharacterBuffer buffer, LayoutRect? clipRect)
			=> clipRect ?? buffer.Bounds;

		private static bool InClip(LayoutRect clip, int x, int y)
			=> x >= clip.X && x < clip.X + clip.Width && y >= clip.Y && y < clip.Y + clip.Height;

		private static void PlotCell(CharacterBuffer buffer, int x, int y, char ch, Color fg, Color bg, LayoutRect clip)
		{
			if (InClip(clip, x, y))
				buffer.SetNarrowCell(x, y, ch, fg, bg);
		}

		#endregion

		#region Line Drawing

		/// <summary>
		/// Draws a line between two points using Bresenham's algorithm.
		/// </summary>
		public static void DrawLine(this CharacterBuffer buffer, int x0, int y0, int x1, int y1,
			char ch, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			var clip = ResolveClipRect(buffer, clipRect);

			int dx = Math.Abs(x1 - x0);
			int dy = -Math.Abs(y1 - y0);
			int sx = x0 < x1 ? 1 : -1;
			int sy = y0 < y1 ? 1 : -1;
			int err = dx + dy;

			while (true)
			{
				PlotCell(buffer, x0, y0, ch, fg, bg, clip);

				if (x0 == x1 && y0 == y1)
					break;

				int e2 = err * 2;
				if (e2 >= dy)
				{
					err += dy;
					x0 += sx;
				}
				if (e2 <= dx)
				{
					err += dx;
					y0 += sy;
				}
			}
		}

		#endregion

		#region Circle Drawing

		/// <summary>
		/// Draws a circle outline using the midpoint circle algorithm.
		/// </summary>
		public static void DrawCircle(this CharacterBuffer buffer, int cx, int cy, int radius,
			char ch, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (radius < 0)
				return;

			var clip = ResolveClipRect(buffer, clipRect);
			int x = radius;
			int y = 0;
			int d = 1 - radius;

			while (x >= y)
			{
				PlotCircleOctants(buffer, cx, cy, x, y, ch, fg, bg, clip);
				y++;

				if (d <= 0)
				{
					d += 2 * y + 1;
				}
				else
				{
					x--;
					d += 2 * (y - x) + 1;
				}
			}
		}

		/// <summary>
		/// Draws a filled circle using the midpoint algorithm with horizontal scanlines.
		/// </summary>
		public static void FillCircle(this CharacterBuffer buffer, int cx, int cy, int radius,
			char ch, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (radius < 0)
				return;

			var clip = ResolveClipRect(buffer, clipRect);
			int x = radius;
			int y = 0;
			int d = 1 - radius;

			while (x >= y)
			{
				DrawHorizontalSpan(buffer, cx - x, cx + x, cy + y, ch, fg, bg, clip);
				DrawHorizontalSpan(buffer, cx - x, cx + x, cy - y, ch, fg, bg, clip);
				DrawHorizontalSpan(buffer, cx - y, cx + y, cy + x, ch, fg, bg, clip);
				DrawHorizontalSpan(buffer, cx - y, cx + y, cy - x, ch, fg, bg, clip);
				y++;

				if (d <= 0)
				{
					d += 2 * y + 1;
				}
				else
				{
					x--;
					d += 2 * (y - x) + 1;
				}
			}
		}

		private static void PlotCircleOctants(CharacterBuffer buffer, int cx, int cy, int x, int y,
			char ch, Color fg, Color bg, LayoutRect clip)
		{
			PlotCell(buffer, cx + x, cy + y, ch, fg, bg, clip);
			PlotCell(buffer, cx - x, cy + y, ch, fg, bg, clip);
			PlotCell(buffer, cx + x, cy - y, ch, fg, bg, clip);
			PlotCell(buffer, cx - x, cy - y, ch, fg, bg, clip);
			PlotCell(buffer, cx + y, cy + x, ch, fg, bg, clip);
			PlotCell(buffer, cx - y, cy + x, ch, fg, bg, clip);
			PlotCell(buffer, cx + y, cy - x, ch, fg, bg, clip);
			PlotCell(buffer, cx - y, cy - x, ch, fg, bg, clip);
		}

		#endregion

		#region Ellipse Drawing

		/// <summary>
		/// Draws an ellipse outline using the midpoint ellipse algorithm.
		/// </summary>
		public static void DrawEllipse(this CharacterBuffer buffer, int cx, int cy, int rx, int ry,
			char ch, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (rx < 0 || ry < 0)
				return;

			var clip = ResolveClipRect(buffer, clipRect);

			long rx2 = (long)rx * rx;
			long ry2 = (long)ry * ry;
			int x = 0;
			int y = ry;
			long dx = 2 * ry2 * x;
			long dy = 2 * rx2 * y;

			// Region 1: dy/dx > -1
			long d1 = ry2 - rx2 * ry + rx2 / 4;
			while (dx < dy)
			{
				PlotEllipseQuadrants(buffer, cx, cy, x, y, ch, fg, bg, clip);
				x++;
				dx += 2 * ry2;
				if (d1 < 0)
				{
					d1 += dx + ry2;
				}
				else
				{
					y--;
					dy -= 2 * rx2;
					d1 += dx - dy + ry2;
				}
			}

			// Region 2: dy/dx < -1
			long d2 = ry2 * ((2L * x + 1) * (2L * x + 1)) / 4 + rx2 * ((long)(y - 1) * (y - 1)) - rx2 * ry2;
			while (y >= 0)
			{
				PlotEllipseQuadrants(buffer, cx, cy, x, y, ch, fg, bg, clip);
				y--;
				dy -= 2 * rx2;
				if (d2 > 0)
				{
					d2 += rx2 - dy;
				}
				else
				{
					x++;
					dx += 2 * ry2;
					d2 += dx - dy + rx2;
				}
			}
		}

		/// <summary>
		/// Draws a filled ellipse using the midpoint algorithm with horizontal scanlines.
		/// </summary>
		public static void FillEllipse(this CharacterBuffer buffer, int cx, int cy, int rx, int ry,
			char ch, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (rx < 0 || ry < 0)
				return;

			var clip = ResolveClipRect(buffer, clipRect);

			long rx2 = (long)rx * rx;
			long ry2 = (long)ry * ry;
			int x = 0;
			int y = ry;
			long dx = 2 * ry2 * x;
			long dy = 2 * rx2 * y;

			// Region 1
			long d1 = ry2 - rx2 * ry + rx2 / 4;
			while (dx < dy)
			{
				DrawHorizontalSpan(buffer, cx - x, cx + x, cy + y, ch, fg, bg, clip);
				DrawHorizontalSpan(buffer, cx - x, cx + x, cy - y, ch, fg, bg, clip);
				x++;
				dx += 2 * ry2;
				if (d1 < 0)
				{
					d1 += dx + ry2;
				}
				else
				{
					y--;
					dy -= 2 * rx2;
					d1 += dx - dy + ry2;
				}
			}

			// Region 2
			long d2 = ry2 * ((2L * x + 1) * (2L * x + 1)) / 4 + rx2 * ((long)(y - 1) * (y - 1)) - rx2 * ry2;
			while (y >= 0)
			{
				DrawHorizontalSpan(buffer, cx - x, cx + x, cy + y, ch, fg, bg, clip);
				DrawHorizontalSpan(buffer, cx - x, cx + x, cy - y, ch, fg, bg, clip);
				y--;
				dy -= 2 * rx2;
				if (d2 > 0)
				{
					d2 += rx2 - dy;
				}
				else
				{
					x++;
					dx += 2 * ry2;
					d2 += dx - dy + rx2;
				}
			}
		}

		private static void PlotEllipseQuadrants(CharacterBuffer buffer, int cx, int cy, int x, int y,
			char ch, Color fg, Color bg, LayoutRect clip)
		{
			PlotCell(buffer, cx + x, cy + y, ch, fg, bg, clip);
			PlotCell(buffer, cx - x, cy + y, ch, fg, bg, clip);
			PlotCell(buffer, cx + x, cy - y, ch, fg, bg, clip);
			PlotCell(buffer, cx - x, cy - y, ch, fg, bg, clip);
		}

		#endregion

		#region Arc Drawing

		/// <summary>
		/// Draws an arc (portion of a circle) using parametric sampling.
		/// Angles are in radians, measured counter-clockwise from the positive X axis.
		/// </summary>
		public static void DrawArc(this CharacterBuffer buffer, int cx, int cy, int radius,
			double startAngle, double endAngle, char ch, Color fg, Color bg, LayoutRect? clipRect = null)
		{
			if (radius < 0)
				return;

			var clip = ResolveClipRect(buffer, clipRect);
			double step = 2.0 * Math.PI / DrawingConstants.DefaultArcSegments;
			double angle = startAngle;

			int prevX = cx + (int)Math.Round(radius * Math.Cos(startAngle));
			int prevY = cy + (int)Math.Round(radius * Math.Sin(startAngle));
			PlotCell(buffer, prevX, prevY, ch, fg, bg, clip);

			while (angle < endAngle)
			{
				angle = Math.Min(angle + step, endAngle);
				int nx = cx + (int)Math.Round(radius * Math.Cos(angle));
				int ny = cy + (int)Math.Round(radius * Math.Sin(angle));

				if (nx != prevX || ny != prevY)
				{
					buffer.DrawLine(nx, ny, prevX, prevY, ch, fg, bg, clipRect);
					prevX = nx;
					prevY = ny;
				}
			}
		}

		#endregion

		#region Shared Helpers

		internal static void DrawHorizontalSpan(CharacterBuffer buffer, int x0, int x1, int y,
			char ch, Color fg, Color bg, LayoutRect clip)
		{
			if (y < clip.Y || y >= clip.Y + clip.Height)
				return;

			int left = Math.Max(x0, clip.X);
			int right = Math.Min(x1, clip.X + clip.Width - 1);

			for (int x = left; x <= right; x++)
			{
				buffer.SetNarrowCell(x, y, ch, fg, bg);
			}
		}

		#endregion
	}
}

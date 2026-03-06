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
	/// Drawing context that wraps a <see cref="CharacterBuffer"/> with local-coordinate translation.
	/// Used both for async painting (wrapping the internal canvas buffer at offset 0,0)
	/// and for event-driven painting (wrapping the window buffer with content-area offset).
	/// All drawing methods accept canvas-local coordinates and translate them automatically.
	/// </summary>
	public sealed class CanvasGraphics
	{
		private readonly CharacterBuffer _buffer;
		private readonly int _offsetX;
		private readonly int _offsetY;
		private readonly LayoutRect _clipRect;

		/// <summary>
		/// Canvas content width in local coordinates.
		/// </summary>
		public int Width { get; }

		/// <summary>
		/// Canvas content height in local coordinates.
		/// </summary>
		public int Height { get; }

		internal CanvasGraphics(CharacterBuffer buffer, int offsetX, int offsetY,
			int width, int height, LayoutRect clipRect)
		{
			_buffer = buffer;
			_offsetX = offsetX;
			_offsetY = offsetY;
			Width = width;
			Height = height;
			_clipRect = clipRect;
		}

		#region Core

		/// <summary>
		/// Sets a single cell at the specified canvas-local position.
		/// </summary>
		/// <param name="x">The X coordinate in canvas-local space.</param>
		/// <param name="y">The Y coordinate in canvas-local space.</param>
		/// <param name="ch">The character to display.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void SetCell(int x, int y, char ch, Color fg, Color bg)
		{
			int ax = x + _offsetX, ay = y + _offsetY;
			if (_clipRect.Contains(ax, ay))
				_buffer.SetCell(ax, ay, ch, fg, bg);
		}

		/// <summary>
		/// Gets the cell at the specified canvas-local position.
		/// </summary>
		/// <param name="x">The X coordinate in canvas-local space.</param>
		/// <param name="y">The Y coordinate in canvas-local space.</param>
		/// <returns>The cell at the specified position.</returns>
		public Cell GetCell(int x, int y)
			=> _buffer.GetCell(x + _offsetX, y + _offsetY);

		/// <summary>
		/// Clears the entire canvas area with the specified background color.
		/// </summary>
		/// <param name="bg">The background color to fill with.</param>
		public void Clear(Color bg)
		{
			var rect = new LayoutRect(_offsetX, _offsetY, Width, Height);
			_buffer.FillRect(rect, ' ', Color.White, bg);
		}

		/// <summary>
		/// Clears the entire canvas area with the specified character and colors.
		/// </summary>
		/// <param name="ch">The fill character.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void Clear(char ch, Color fg, Color bg)
		{
			var rect = new LayoutRect(_offsetX, _offsetY, Width, Height);
			_buffer.FillRect(rect, ch, fg, bg);
		}

		/// <summary>
		/// Fills a rectangle at canvas-local coordinates with the specified character and colors.
		/// </summary>
		/// <param name="x">The X coordinate of the rectangle's top-left corner.</param>
		/// <param name="y">The Y coordinate of the rectangle's top-left corner.</param>
		/// <param name="width">The width of the rectangle.</param>
		/// <param name="height">The height of the rectangle.</param>
		/// <param name="ch">The fill character.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void FillRect(int x, int y, int width, int height, char ch, Color fg, Color bg)
			=> _buffer.FillRect(new LayoutRect(x + _offsetX, y + _offsetY, width, height), ch, fg, bg);

		/// <summary>
		/// Fills a rectangle at canvas-local coordinates with spaces and the specified background.
		/// </summary>
		/// <param name="x">The X coordinate of the rectangle's top-left corner.</param>
		/// <param name="y">The Y coordinate of the rectangle's top-left corner.</param>
		/// <param name="width">The width of the rectangle.</param>
		/// <param name="height">The height of the rectangle.</param>
		/// <param name="bg">The background color.</param>
		public void FillRect(int x, int y, int width, int height, Color bg)
			=> _buffer.FillRect(new LayoutRect(x + _offsetX, y + _offsetY, width, height), ' ', Color.White, bg);

		#endregion

		#region Text

		/// <summary>
		/// Writes a string at the specified canvas-local position.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <param name="text">The text to write.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void WriteString(int x, int y, string text, Color fg, Color bg)
			=> _buffer.WriteStringClipped(x + _offsetX, y + _offsetY, text, fg, bg, _clipRect);

		/// <summary>
		/// Writes horizontally centered text within the canvas at the specified Y position.
		/// </summary>
		/// <param name="y">The Y coordinate in canvas-local space.</param>
		/// <param name="text">The text to center.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void WriteStringCentered(int y, string text, Color fg, Color bg)
		{
			var canvasBounds = new LayoutRect(_offsetX, _offsetY, Width, Height);
			var effectiveClip = canvasBounds.Intersect(_clipRect);
			_buffer.WriteStringCentered(y + _offsetY, text, fg, bg, effectiveClip);
		}

		/// <summary>
		/// Writes right-aligned text within the canvas at the specified Y position.
		/// </summary>
		/// <param name="y">The Y coordinate in canvas-local space.</param>
		/// <param name="text">The text to right-align.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void WriteStringRight(int y, string text, Color fg, Color bg)
		{
			var canvasBounds = new LayoutRect(_offsetX, _offsetY, Width, Height);
			var effectiveClip = canvasBounds.Intersect(_clipRect);
			_buffer.WriteStringRight(y + _offsetY, text, fg, bg, effectiveClip);
		}

		/// <summary>
		/// Draws a box border and writes centered text inside it.
		/// </summary>
		/// <param name="x">The X coordinate of the box.</param>
		/// <param name="y">The Y coordinate of the box.</param>
		/// <param name="width">The width of the box.</param>
		/// <param name="height">The height of the box.</param>
		/// <param name="text">The text to center inside the box.</param>
		/// <param name="boxChars">The box drawing character set.</param>
		/// <param name="fg">The text foreground color.</param>
		/// <param name="bg">The text background color.</param>
		/// <param name="boxFg">The box border foreground color.</param>
		/// <param name="boxBg">The box border background color.</param>
		public void WriteStringInBox(int x, int y, int width, int height, string text,
			BoxChars boxChars, Color fg, Color bg, Color boxFg, Color boxBg)
			=> _buffer.WriteStringInBox(
				new LayoutRect(x + _offsetX, y + _offsetY, width, height),
				text, boxChars, fg, bg, boxFg, boxBg, _clipRect);

		/// <summary>
		/// Writes word-wrapped text starting at the given position within the specified width.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <param name="width">The maximum line width for wrapping.</param>
		/// <param name="text">The text to wrap and write.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void WriteWrappedText(int x, int y, int width, string text, Color fg, Color bg)
			=> _buffer.WriteWrappedText(x + _offsetX, y + _offsetY, width, text, fg, bg, _clipRect);

		#endregion

		#region Lines and Boxes

		/// <summary>
		/// Draws a line between two points using Bresenham's algorithm.
		/// </summary>
		/// <param name="x0">The starting X coordinate.</param>
		/// <param name="y0">The starting Y coordinate.</param>
		/// <param name="x1">The ending X coordinate.</param>
		/// <param name="y1">The ending Y coordinate.</param>
		/// <param name="ch">The character to draw with.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void DrawLine(int x0, int y0, int x1, int y1, char ch, Color fg, Color bg)
			=> _buffer.DrawLine(x0 + _offsetX, y0 + _offsetY, x1 + _offsetX, y1 + _offsetY, ch, fg, bg, _clipRect);

		/// <summary>
		/// Draws a horizontal line starting at the given position.
		/// </summary>
		/// <param name="x">The starting X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <param name="length">The length of the line in characters.</param>
		/// <param name="ch">The character to draw with.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void DrawHorizontalLine(int x, int y, int length, char ch, Color fg, Color bg)
			=> _buffer.DrawHorizontalLine(x + _offsetX, y + _offsetY, length, ch, fg, bg);

		/// <summary>
		/// Draws a vertical line starting at the given position.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The starting Y coordinate.</param>
		/// <param name="length">The length of the line in characters.</param>
		/// <param name="ch">The character to draw with.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void DrawVerticalLine(int x, int y, int length, char ch, Color fg, Color bg)
			=> _buffer.DrawVerticalLine(x + _offsetX, y + _offsetY, length, ch, fg, bg);

		/// <summary>
		/// Draws a box border using the specified box drawing characters.
		/// </summary>
		/// <param name="x">The X coordinate of the box's top-left corner.</param>
		/// <param name="y">The Y coordinate of the box's top-left corner.</param>
		/// <param name="width">The width of the box.</param>
		/// <param name="height">The height of the box.</param>
		/// <param name="boxChars">The box drawing character set.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void DrawBox(int x, int y, int width, int height, BoxChars boxChars, Color fg, Color bg)
			=> _buffer.DrawBox(new LayoutRect(x + _offsetX, y + _offsetY, width, height), boxChars, fg, bg);

		#endregion

		#region Circles

		/// <summary>
		/// Draws a circle outline using the midpoint circle algorithm.
		/// </summary>
		/// <param name="cx">The center X coordinate.</param>
		/// <param name="cy">The center Y coordinate.</param>
		/// <param name="radius">The circle radius.</param>
		/// <param name="ch">The character to draw with.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void DrawCircle(int cx, int cy, int radius, char ch, Color fg, Color bg)
			=> _buffer.DrawCircle(cx + _offsetX, cy + _offsetY, radius, ch, fg, bg, _clipRect);

		/// <summary>
		/// Draws a filled circle using the midpoint algorithm with horizontal scanlines.
		/// </summary>
		/// <param name="cx">The center X coordinate.</param>
		/// <param name="cy">The center Y coordinate.</param>
		/// <param name="radius">The circle radius.</param>
		/// <param name="ch">The fill character.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void FillCircle(int cx, int cy, int radius, char ch, Color fg, Color bg)
			=> _buffer.FillCircle(cx + _offsetX, cy + _offsetY, radius, ch, fg, bg, _clipRect);

		/// <summary>
		/// Draws an ellipse outline using the midpoint ellipse algorithm.
		/// </summary>
		/// <param name="cx">The center X coordinate.</param>
		/// <param name="cy">The center Y coordinate.</param>
		/// <param name="rx">The horizontal radius.</param>
		/// <param name="ry">The vertical radius.</param>
		/// <param name="ch">The character to draw with.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void DrawEllipse(int cx, int cy, int rx, int ry, char ch, Color fg, Color bg)
			=> _buffer.DrawEllipse(cx + _offsetX, cy + _offsetY, rx, ry, ch, fg, bg, _clipRect);

		/// <summary>
		/// Draws a filled ellipse using the midpoint algorithm with horizontal scanlines.
		/// </summary>
		/// <param name="cx">The center X coordinate.</param>
		/// <param name="cy">The center Y coordinate.</param>
		/// <param name="rx">The horizontal radius.</param>
		/// <param name="ry">The vertical radius.</param>
		/// <param name="ch">The fill character.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void FillEllipse(int cx, int cy, int rx, int ry, char ch, Color fg, Color bg)
			=> _buffer.FillEllipse(cx + _offsetX, cy + _offsetY, rx, ry, ch, fg, bg, _clipRect);

		/// <summary>
		/// Draws an arc (portion of a circle) using parametric sampling.
		/// Angles are in radians, measured counter-clockwise from the positive X axis.
		/// </summary>
		/// <param name="cx">The center X coordinate.</param>
		/// <param name="cy">The center Y coordinate.</param>
		/// <param name="radius">The arc radius.</param>
		/// <param name="startAngle">The starting angle in radians.</param>
		/// <param name="endAngle">The ending angle in radians.</param>
		/// <param name="ch">The character to draw with.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void DrawArc(int cx, int cy, int radius, double startAngle, double endAngle,
			char ch, Color fg, Color bg)
			=> _buffer.DrawArc(cx + _offsetX, cy + _offsetY, radius, startAngle, endAngle, ch, fg, bg, _clipRect);

		#endregion

		#region Polygons

		/// <summary>
		/// Draws a triangle outline by connecting three vertices with lines.
		/// </summary>
		/// <param name="x0">First vertex X.</param>
		/// <param name="y0">First vertex Y.</param>
		/// <param name="x1">Second vertex X.</param>
		/// <param name="y1">Second vertex Y.</param>
		/// <param name="x2">Third vertex X.</param>
		/// <param name="y2">Third vertex Y.</param>
		/// <param name="ch">The character to draw with.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void DrawTriangle(int x0, int y0, int x1, int y1, int x2, int y2,
			char ch, Color fg, Color bg)
			=> _buffer.DrawTriangle(
				x0 + _offsetX, y0 + _offsetY,
				x1 + _offsetX, y1 + _offsetY,
				x2 + _offsetX, y2 + _offsetY,
				ch, fg, bg, _clipRect);

		/// <summary>
		/// Draws a filled triangle using scanline rasterization.
		/// </summary>
		/// <param name="x0">First vertex X.</param>
		/// <param name="y0">First vertex Y.</param>
		/// <param name="x1">Second vertex X.</param>
		/// <param name="y1">Second vertex Y.</param>
		/// <param name="x2">Third vertex X.</param>
		/// <param name="y2">Third vertex Y.</param>
		/// <param name="ch">The fill character.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void FillTriangle(int x0, int y0, int x1, int y1, int x2, int y2,
			char ch, Color fg, Color bg)
			=> _buffer.FillTriangle(
				x0 + _offsetX, y0 + _offsetY,
				x1 + _offsetX, y1 + _offsetY,
				x2 + _offsetX, y2 + _offsetY,
				ch, fg, bg, _clipRect);

		/// <summary>
		/// Draws a polygon outline by connecting consecutive points with lines.
		/// </summary>
		/// <param name="points">The polygon vertices in canvas-local coordinates.</param>
		/// <param name="ch">The character to draw with.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void DrawPolygon((int X, int Y)[] points, char ch, Color fg, Color bg)
		{
			var translated = new (int X, int Y)[points.Length];
			for (int i = 0; i < points.Length; i++)
				translated[i] = (points[i].X + _offsetX, points[i].Y + _offsetY);
			_buffer.DrawPolygon(translated, ch, fg, bg, _clipRect);
		}

		/// <summary>
		/// Draws a filled polygon using scanline rasterization with the even-odd rule.
		/// </summary>
		/// <param name="points">The polygon vertices in canvas-local coordinates.</param>
		/// <param name="ch">The fill character.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void FillPolygon((int X, int Y)[] points, char ch, Color fg, Color bg)
		{
			var translated = new (int X, int Y)[points.Length];
			for (int i = 0; i < points.Length; i++)
				translated[i] = (points[i].X + _offsetX, points[i].Y + _offsetY);
			_buffer.FillPolygon(translated, ch, fg, bg, _clipRect);
		}

		#endregion

		#region Gradients and Patterns

		/// <summary>
		/// Fills a rectangle with a horizontal foreground color gradient (left to right).
		/// </summary>
		/// <param name="x">The X coordinate of the rectangle.</param>
		/// <param name="y">The Y coordinate of the rectangle.</param>
		/// <param name="width">The width of the rectangle.</param>
		/// <param name="height">The height of the rectangle.</param>
		/// <param name="ch">The fill character.</param>
		/// <param name="fgStart">The starting foreground color (left).</param>
		/// <param name="fgEnd">The ending foreground color (right).</param>
		/// <param name="bg">The background color.</param>
		public void GradientFillHorizontal(int x, int y, int width, int height,
			char ch, Color fgStart, Color fgEnd, Color bg)
			=> _buffer.GradientFillHorizontal(
				new LayoutRect(x + _offsetX, y + _offsetY, width, height),
				ch, fgStart, fgEnd, bg, _clipRect);

		/// <summary>
		/// Fills a rectangle with a vertical foreground color gradient (top to bottom).
		/// </summary>
		/// <param name="x">The X coordinate of the rectangle.</param>
		/// <param name="y">The Y coordinate of the rectangle.</param>
		/// <param name="width">The width of the rectangle.</param>
		/// <param name="height">The height of the rectangle.</param>
		/// <param name="ch">The fill character.</param>
		/// <param name="fgStart">The starting foreground color (top).</param>
		/// <param name="fgEnd">The ending foreground color (bottom).</param>
		/// <param name="bg">The background color.</param>
		public void GradientFillVertical(int x, int y, int width, int height,
			char ch, Color fgStart, Color fgEnd, Color bg)
			=> _buffer.GradientFillVertical(
				new LayoutRect(x + _offsetX, y + _offsetY, width, height),
				ch, fgStart, fgEnd, bg, _clipRect);

		/// <summary>
		/// Fills a rectangle with a background color gradient.
		/// </summary>
		/// <param name="x">The X coordinate of the rectangle.</param>
		/// <param name="y">The Y coordinate of the rectangle.</param>
		/// <param name="width">The width of the rectangle.</param>
		/// <param name="height">The height of the rectangle.</param>
		/// <param name="bgStart">The starting background color.</param>
		/// <param name="bgEnd">The ending background color.</param>
		/// <param name="horizontal">True for left-to-right gradient, false for top-to-bottom.</param>
		public void GradientFillRect(int x, int y, int width, int height,
			Color bgStart, Color bgEnd, bool horizontal)
			=> _buffer.GradientFillRect(
				new LayoutRect(x + _offsetX, y + _offsetY, width, height),
				bgStart, bgEnd, horizontal, _clipRect);

		/// <summary>
		/// Fills a rectangle with a repeating 2D character pattern.
		/// </summary>
		/// <param name="x">The X coordinate of the rectangle.</param>
		/// <param name="y">The Y coordinate of the rectangle.</param>
		/// <param name="width">The width of the rectangle.</param>
		/// <param name="height">The height of the rectangle.</param>
		/// <param name="pattern">The pattern rows to tile.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void PatternFill(int x, int y, int width, int height,
			string[] pattern, Color fg, Color bg)
			=> _buffer.PatternFill(
				new LayoutRect(x + _offsetX, y + _offsetY, width, height),
				pattern, fg, bg, _clipRect);

		/// <summary>
		/// Fills a rectangle with alternating characters in a checkerboard pattern.
		/// </summary>
		/// <param name="x">The X coordinate of the rectangle.</param>
		/// <param name="y">The Y coordinate of the rectangle.</param>
		/// <param name="width">The width of the rectangle.</param>
		/// <param name="height">The height of the rectangle.</param>
		/// <param name="ch1">The character for even cells.</param>
		/// <param name="ch2">The character for odd cells.</param>
		/// <param name="fg1">The foreground color for even cells.</param>
		/// <param name="fg2">The foreground color for odd cells.</param>
		/// <param name="bg">The background color.</param>
		public void CheckerFill(int x, int y, int width, int height,
			char ch1, char ch2, Color fg1, Color fg2, Color bg)
			=> _buffer.CheckerFill(
				new LayoutRect(x + _offsetX, y + _offsetY, width, height),
				ch1, ch2, fg1, fg2, bg, _clipRect);

		/// <summary>
		/// Fills a rectangle with a density-based stipple pattern.
		/// Density ranges from 0.0 (empty) to 1.0 (fully filled).
		/// </summary>
		/// <param name="x">The X coordinate of the rectangle.</param>
		/// <param name="y">The Y coordinate of the rectangle.</param>
		/// <param name="width">The width of the rectangle.</param>
		/// <param name="height">The height of the rectangle.</param>
		/// <param name="density">The fill density from 0.0 to 1.0.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void StippleFill(int x, int y, int width, int height,
			double density, Color fg, Color bg)
			=> _buffer.StippleFill(
				new LayoutRect(x + _offsetX, y + _offsetY, width, height),
				density, fg, bg, _clipRect);

		#endregion
	}
}

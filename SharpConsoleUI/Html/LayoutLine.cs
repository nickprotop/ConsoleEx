// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Text alignment for layout lines.
	/// </summary>
	public enum TextAlignment
	{
		Left,
		Center,
		Right,
	}

	/// <summary>
	/// Represents a clickable link region within a layout line.
	/// </summary>
	public struct LinkRegion
	{
		public int StartX;
		public int EndX;
		public string Url;
		public string Text;

		public LinkRegion(int startX, int endX, string url, string text)
		{
			StartX = startX;
			EndX = endX;
			Url = url;
			Text = text;
		}
	}

	/// <summary>
	/// Represents a single rendered line of HTML content.
	/// </summary>
	public struct LayoutLine
	{
		public int Y;
		public int X;
		public int Width;
		public Cell[] Cells;
		public TextAlignment Alignment;
		public LinkRegion[]? Links;

		public LayoutLine(int y, int x, int width, Cell[] cells, TextAlignment alignment, LinkRegion[]? links = null)
		{
			Y = y;
			X = x;
			Width = width;
			Cells = cells;
			Alignment = alignment;
			Links = links;
		}
	}
}

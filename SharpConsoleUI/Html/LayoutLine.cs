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
		/// <summary>
		/// Stable identifier shared by all segments of the same &lt;a&gt; element.
		/// Used to group multi-line links as a single Tab stop.
		/// </summary>
		public int LinkId;

		public LinkRegion(int startX, int endX, string url, string text, int linkId = 0)
		{
			StartX = startX;
			EndX = endX;
			Url = url;
			Text = text;
			LinkId = linkId;
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

		/// <summary>
		/// Whether these cells are a rendered picture rather than text.
		/// </summary>
		/// <remarks>
		/// A half-block image carries one pixel in the glyph's foreground and
		/// another in its background, so anything that swaps the two is not a
		/// highlight but a scramble. The focused-link highlight does exactly
		/// that, and an image can now be inside a link, so a row says what it is
		/// and the highlight leaves its colours alone.
		/// </remarks>
		public bool IsImage;

		public LayoutLine(int y, int x, int width, Cell[] cells, TextAlignment alignment, LinkRegion[]? links = null,
			bool isImage = false)
		{
			Y = y;
			X = x;
			Width = width;
			Cells = cells;
			Alignment = alignment;
			Links = links;
			IsImage = isImage;
		}
	}
}

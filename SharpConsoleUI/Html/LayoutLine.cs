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

		/// <summary>
		/// Whether this line ended because the text ran out of room, with the
		/// rest of the same block continuing on the next line.
		/// </summary>
		/// <remarks>
		/// <para>
		/// False for a break the author wrote: a <c>&lt;br&gt;</c>, the end of a
		/// paragraph, a newline inside a code block, a table row, an image row.
		/// True only where the layout broke the line itself to make it fit.
		/// </para>
		/// <para>
		/// Recorded rather than inferred. A line that happens to end exactly at
		/// the margin is indistinguishable, by its length, from one that was
		/// broken to fit, and the difference decides whether copied text carries
		/// a newline the author never wrote. The inline flow knows which is which
		/// at the moment it finishes the line, so it says so.
		/// </para>
		/// </remarks>
		public bool ContinuesNextLine;

		/// <summary>
		/// Whether the break that ended this line fell between words, so the
		/// space it consumed belongs to the text.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Only meaningful when <see cref="ContinuesNextLine"/> is true, and it is
		/// the difference between rejoining with a space and rejoining with
		/// nothing. A paragraph broken at a word boundary needs the space back; a
		/// code line or a single long word broken mid-token does not, and giving
		/// it one corrupts what is pasted.
		/// </para>
		/// <para>
		/// Two bits rather than one because "the layout broke this line" and "the
		/// break was between words" are different facts, and only the code that
		/// performs the break knows the second.
		/// </para>
		/// </remarks>
		public bool BrokeBetweenWords;

		public LayoutLine(int y, int x, int width, Cell[] cells, TextAlignment alignment, LinkRegion[]? links = null,
			bool isImage = false, bool continuesNextLine = false, bool brokeBetweenWords = false)
		{
			Y = y;
			X = x;
			Width = width;
			Cells = cells;
			Alignment = alignment;
			Links = links;
			IsImage = isImage;
			ContinuesNextLine = continuesNextLine;
			BrokeBetweenWords = brokeBetweenWords;
		}
	}
}

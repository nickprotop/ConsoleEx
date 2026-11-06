// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Constants used for HTML-to-TUI rendering.
	/// </summary>
	public static class HtmlConstants
	{
		// List rendering
		public static readonly char[] BulletChars = { '•', '◦', '▪' };
		public const int ListIndent = 3;

		// Blockquote rendering
		public const int BlockquoteIndent = 3;
		public const char BlockquoteBar = '│';

		// Horizontal rule
		public const char HorizontalRuleChar = '─';

		// Link colors
		public static readonly Color DefaultLinkColor = Color.Cyan1;
		public static readonly Color DefaultVisitedLinkColor = Color.Purple;

		// Code background
		public static readonly Color DefaultCodeBackground = Color.Grey23;

		// Blockquote colors
		public static readonly Color DefaultBlockquoteBarColor = Color.Grey;
		public static readonly Color DefaultBlockquoteTextColor = Color.Grey70;

		// Table box-drawing characters
		public const char TableTopLeft = '┌';
		public const char TableTopRight = '┐';
		public const char TableBottomLeft = '└';
		public const char TableBottomRight = '┘';
		public const char TableHorizontal = '─';
		public const char TableVertical = '│';
		public const char TableCross = '┼';
		public const char TableTopTee = '┬';
		public const char TableBottomTee = '┴';
		public const char TableLeftTee = '├';
		public const char TableRightTee = '┤';

		// Table colors
		public static readonly Color DefaultTableBorderColor = Color.Grey;

		// Loading
		public const string DefaultLoadingText = "Loading...";

		// Unit conversion ratios
		public const double PxToCharRatio = 8.0;

		/// <summary>
		/// Pixels per terminal column, for an image whose width is declared in pixels.
		/// </summary>
		/// <remarks>
		/// Kept for compatibility: image width is no longer a fixed px-per-column
		/// ratio but a fraction of <see cref="CssViewportWidthPx"/>, because a
		/// declared pixel width means nothing in columns until you know how wide
		/// the page is. See <see cref="CssViewportWidthPx"/>.
		/// </remarks>
		[Obsolete("Image widths scale with the page; see CssViewportWidthPx. This value is no longer read.")]
		public const double ImagePxToCharRatio = 1.0;

		public const double EmToCharRatio = 2.0;

		/// <summary>
		/// The viewport width, in CSS pixels, that a page is treated as being laid out for.
		/// </summary>
		/// <remarks>
		/// <para>
		/// An image declares its width in pixels, and a terminal has no pixels, so
		/// the two have to be related by something. A fixed pixels-per-column ratio
		/// was tried twice and is wrong both ways round: at 1:1 every image on a
		/// real page is wider than the terminal and so every image fills it,
		/// thumbnail and hero alike; at 8:1 a 220px thumbnail lands on 28 columns
		/// whatever the window is doing, which is right on a wide terminal and most
		/// of a narrow one.
		/// </para>
		/// <para>
		/// So a declared width is read as a FRACTION of the viewport a page was
		/// written for - 220px of 1024px is a bit over a fifth of the page - and
		/// that fraction is spent on the columns actually available. Relative sizes
		/// survive, a thumbnail stays a thumbnail next to a hero image, and the
		/// whole page scales with the window the way the page's own design intends.
		/// </para>
		/// <para>
		/// A tablet-width viewport rather than a desktop one, because that is what a
		/// terminal is closer to. It also matches how text-first pages are authored:
		/// an encyclopedia serves its thumbnails at 250px for an article column
		/// several times narrower than a 1920px screen, so measuring them against
		/// 1920 rendered a readable thumbnail as a 16-column stamp.
		/// </para>
		/// </remarks>
		public const double CssViewportWidthPx = 1024.0;

		/// <summary>
		/// The width, in CSS pixels, of the content column a page's images are
		/// measured against.
		/// </summary>
		/// <remarks>
		/// <para>
		/// A declared width is a fraction of something, and the viewport is the
		/// wrong something. A browser window also holds a sidebar, margins and
		/// chrome; the text and pictures live in a narrower column inside it, and
		/// that column is what an author sizes a picture for. An encyclopedia
		/// serves its thumbnails at 250 pixels for a column around 700 wide - a
		/// bit over a third of the text beside them.
		/// </para>
		/// <para>
		/// A terminal renders no sidebar, so the text runs the full width and the
		/// column IS the line. Measuring those same 250 pixels against the whole
		/// 1024 made every picture about a quarter of a line that its own page
		/// gave over a third of, so pictures came out roughly half the size they
		/// read at next to the same words. Measuring against the column restores
		/// the proportion the reader actually sees.
		/// </para>
		/// <para>
		/// <see cref="CssViewportWidthPx"/> stays what it is and keeps its own
		/// job: it is what the page is TOLD about its viewport, for media queries
		/// and viewport-relative units. This is the narrower frame a picture is
		/// measured in. They are different questions and only coincidentally
		/// about the same page.
		/// </para>
		/// </remarks>
		public const double CssContentWidthPx = 700.0;

		/// <summary>
		/// The viewport height, in CSS pixels, a page is treated as being laid out for.
		/// </summary>
		/// <remarks>
		/// Only reaches media queries and viewport-relative units; nothing in the
		/// terminal layout is measured against it. Kept in the same 4:3 proportion
		/// as <see cref="CssViewportWidthPx"/> so a page that asks about its own
		/// shape gets a coherent answer.
		/// </remarks>
		public const double CssViewportHeightPx = 768.0;

		/// <summary>
		/// The root font size, in CSS pixels, that <c>em</c> and <c>rem</c> lengths resolve against.
		/// </summary>
		/// <remarks>
		/// The browser default, and the <c>FontSize</c> given to AngleSharp's render
		/// device, so a length computed by the stylesheet and one parsed here agree.
		/// </remarks>
		public const double CssRootFontSizePx = 16.0;

		// Image alt text
		public const string ImageAltPrefix = "[";
		public const string ImageAltSuffix = "]";

		// Image loading: parallel downloads per page (a browser uses ~6 per host; image hosts
		// such as Wikimedia answer 429 Too Many Requests when hit much harder than that)
		public const int MaxConcurrentImageFetches = 4;
	}
}

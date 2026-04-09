// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using Spectre.Console;

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
		public const double EmToCharRatio = 2.0;

		// Image alt text
		public const string ImageAltPrefix = "[";
		public const string ImageAltSuffix = "]";
	}
}

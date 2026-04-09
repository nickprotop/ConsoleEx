// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using SharpConsoleUI.Html;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Tests.Html
{
	public static class HtmlTestHelpers
	{
		/// <summary>
		/// Parses HTML into an AngleSharp IDocument with CSS support.
		/// </summary>
		public static IDocument ParseHtml(string html)
		{
			var config = AngleSharp.Configuration.Default.WithCss();
			var context = BrowsingContext.New(config);
			var parser = context.GetService<IHtmlParser>()!;
			return parser.ParseDocument(html);
		}

		/// <summary>
		/// Extracts text characters from a Cell array, skipping wide continuations.
		/// </summary>
		public static string CellsToText(Cell[] cells)
		{
			var sb = new System.Text.StringBuilder();
			foreach (var cell in cells)
			{
				if (!cell.IsWideContinuation)
					sb.Append(cell.Character.ToString());
			}
			return sb.ToString();
		}

		/// <summary>
		/// Joins multiple LayoutLines into text separated by newlines.
		/// </summary>
		public static string LinesToText(LayoutLine[] lines)
		{
			var parts = new string[lines.Length];
			for (int i = 0; i < lines.Length; i++)
				parts[i] = CellsToText(lines[i].Cells);
			return string.Join("\n", parts);
		}

		/// <summary>
		/// Checks if a cell at the given index has the specified decoration.
		/// </summary>
		public static bool HasDecoration(Cell[] cells, int index, TextDecoration decoration)
		{
			if (index < 0 || index >= cells.Length)
				return false;
			return cells[index].Decorations.HasFlag(decoration);
		}

		/// <summary>
		/// Finds the first index of the given character in a Cell array.
		/// Returns -1 if not found.
		/// </summary>
		public static int FindChar(Cell[] cells, char c)
		{
			for (int i = 0; i < cells.Length; i++)
			{
				if (cells[i].Character == new System.Text.Rune(c))
					return i;
			}
			return -1;
		}

		/// <summary>
		/// Collects all LinkRegions from an array of LayoutLines.
		/// </summary>
		public static List<LinkRegion> GetAllLinks(LayoutLine[] lines)
		{
			var result = new List<LinkRegion>();
			foreach (var line in lines)
			{
				if (line.Links != null)
					result.AddRange(line.Links);
			}
			return result;
		}
	}
}

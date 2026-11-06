// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using AngleSharp.Dom;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Column type for CSS Grid template definitions.
	/// </summary>
	public enum ColumnType
	{
		Fr,
		Fixed,
	}

	/// <summary>
	/// Represents a single column definition in a CSS Grid template.
	/// </summary>
	public struct ColumnDef
	{
		public ColumnType Type;
		public double Value;
	}

	/// <summary>
	/// Renders CSS Grid containers with grid-template-columns (fr/px/em units) and gap support.
	/// </summary>
	public static class HtmlGridLayout
	{
		/// <summary>
		/// Lays out a CSS Grid container element into LayoutLines.
		/// </summary>
		public static LayoutLine[] LayoutGrid(
			IElement gridElement,
			int maxWidth,
			string gridTemplateColumns,
			int gap,
			Color defaultFg,
			Color defaultBg,
			Color? linkColor = null)
		{
			// 1. Parse column definitions
			var colDefs = ParseColumnDefs(gridTemplateColumns);

			// A template we cannot read is a reason to stop arranging, never a
			// reason to drop the words. Returning nothing here deleted whole
			// sections of a page - the content was never laid out at all, so no
			// amount of scrolling found it. Stacking is what a browser falls back
			// to, and it keeps every word on the page.
			if (colDefs.Count == 0)
			{
				var fallback = new List<IElement>();
				foreach (var child in gridElement.Children)
					fallback.Add(child);

				return StackCells(fallback, maxWidth, defaultFg, defaultBg, linkColor);
			}

			int colCount = colDefs.Count;

			// 2. Calculate column widths
			int totalGap = gap * (colCount - 1);
			int availableForColumns = maxWidth - totalGap;
			if (availableForColumns < colCount)
				availableForColumns = colCount;

			var colWidths = CalculateColumnWidths(colDefs, availableForColumns);

			// 3. Collect direct children
			var children = new List<IElement>();
			foreach (var child in gridElement.Children)
				children.Add(child);

			if (children.Count == 0)
				return Array.Empty<LayoutLine>();

			// A page designs its grid for a screen hundreds of pixels wide. A
			// terminal has about a hundred columns in total, so a desktop grid of
			// twelve or fifteen tracks leaves each one a few characters, and text
			// wrapped into that prints one letter per line - a column of vertical
			// confetti where an article should be.
			//
			// A screen that cannot give a column room for a word is a screen where
			// the columns belong underneath each other, which is what a browser
			// does at its narrowest breakpoint. Stacking keeps every word readable
			// and in order; honouring the grid keeps neither.
			if (IsTooNarrowToHonour(colWidths, availableForColumns))
			{
				return StackCells(children, maxWidth, defaultFg, defaultBg, linkColor);
			}

			// 4. Assign children to rows
			var result = new List<LayoutLine>();
			int childIndex = 0;

			while (childIndex < children.Count)
			{
				// Render each cell in this row
				var cellLines = new List<LayoutLine[]>();
				int maxHeight = 0;

				for (int c = 0; c < colCount && childIndex < children.Count; c++, childIndex++)
				{
					var cellWidth = colWidths[c];
					if (cellWidth <= 0) cellWidth = 1;

					var lines = HtmlBlockFlow.FlowBlocks(
						children[childIndex],
						cellWidth,
						defaultFg,
						defaultBg,
						blockSpacing: 0,
						linkColor: linkColor);

					cellLines.Add(lines);
					if (lines.Length > maxHeight)
						maxHeight = lines.Length;
				}

				// Pad incomplete rows
				while (cellLines.Count < colCount)
					cellLines.Add(Array.Empty<LayoutLine>());

				if (maxHeight == 0)
					maxHeight = 1;

				// 5. Merge columns side-by-side
				for (int line = 0; line < maxHeight; line++)
				{
					var rowCells = new List<Cell>();

					for (int c = 0; c < colCount; c++)
					{
						if (c > 0)
						{
							// At least one space between columns, whatever the page
							// asked for. A page's grid-gap is often zero because on
							// a screen the tracks are already separated by position
							// and whitespace inside each cell. Here the tracks are
							// adjacent runs of characters, so a cell whose text
							// fills its own width touched the next one and two
							// words merged into a third that is in neither.
							for (int g = 0; g < Math.Max(1, gap); g++)
								rowCells.Add(new Cell(' ', defaultFg, defaultBg));
						}

						var width = colWidths[c];
						Cell[] sourceCells;

						if (line < cellLines[c].Length && cellLines[c][line].Cells.Length > 0)
						{
							sourceCells = cellLines[c][line].Cells;
						}
						else
						{
							sourceCells = Array.Empty<Cell>();
						}

						// Copy source cells up to column width
						int copied = 0;
						for (int i = 0; i < sourceCells.Length && copied < width; i++, copied++)
							rowCells.Add(sourceCells[i]);

						// Pad remaining
						while (copied < width)
						{
							rowCells.Add(new Cell(' ', defaultFg, defaultBg));
							copied++;
						}
					}

					result.Add(new LayoutLine(0, 0, rowCells.Count, rowCells.ToArray(), TextAlignment.Left));
				}
			}

			return result.ToArray();
		}

		/// <summary>
		/// Parses a CSS grid-template-columns string into column definitions.
		/// Supports fr, px, and em units. Plain integers are treated as fixed char widths.
		/// </summary>
		/// <summary>
		/// Rewrites <c>repeat(n, tracks)</c> into the tracks it stands for.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The template is parsed by splitting on spaces, and <c>repeat()</c>
		/// holds spaces of its own, so it arrived as the unparseable fragments
		/// <c>repeat(15,</c> and <c>1fr)</c>. Neither matched a unit, the parse
		/// produced no columns at all, and a grid with no columns returned no
		/// lines - so every word inside it vanished from the page rather than
		/// merely being laid out badly.
		/// </para>
		/// <para>
		/// That is the common spelling: a page writes <c>repeat(12, 1fr)</c>
		/// rather than twelve copies of <c>1fr</c>. Expanding it here keeps the
		/// rest of the parser working on the plain list it was written for.
		/// </para>
		/// <para>
		/// A count is clamped: <c>auto-fit</c> and <c>auto-fill</c> name no number
		/// and are left for the caller's narrow-grid handling rather than guessed
		/// at, and an absurd count cannot be used to allocate without bound.
		/// </para>
		/// </remarks>
		private static string ExpandRepeats(string templateColumns)
		{
			const string marker = "repeat(";
			const int maxRepeat = 64;

			if (templateColumns.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0)
				return templateColumns;

			var sb = new System.Text.StringBuilder(templateColumns.Length);
			int i = 0;

			while (i < templateColumns.Length)
			{
				int start = templateColumns.IndexOf(marker, i, StringComparison.OrdinalIgnoreCase);
				if (start < 0)
				{
					sb.Append(templateColumns, i, templateColumns.Length - i);
					break;
				}

				sb.Append(templateColumns, i, start - i);

				int open = start + marker.Length;
				int close = templateColumns.IndexOf(')', open);
				if (close < 0)
				{
					// Unbalanced: leave the rest as written rather than inventing
					// a shape for it.
					sb.Append(templateColumns, start, templateColumns.Length - start);
					break;
				}

				var inner = templateColumns.Substring(open, close - open);
				int comma = inner.IndexOf(',');

				if (comma > 0
					&& int.TryParse(inner.Substring(0, comma).Trim(),
						System.Globalization.NumberStyles.Integer,
						System.Globalization.CultureInfo.InvariantCulture, out var count)
					&& count > 0)
				{
					var tracks = inner.Substring(comma + 1).Trim();
					for (int n = 0; n < Math.Min(count, maxRepeat); n++)
					{
						sb.Append(' ').Append(tracks);
					}

					sb.Append(' ');
				}
				else
				{
					// A count we cannot read - auto-fit, auto-fill, something new.
					// One track stands in, so the grid still has a shape and its
					// content is still laid out.
					sb.Append(' ').Append(inner.Substring(comma + 1).Trim()).Append(' ');
				}

				i = close + 1;
			}

			return sb.ToString();
		}

		public static List<ColumnDef> ParseColumnDefs(string templateColumns)
		{
			var defs = new List<ColumnDef>();

			if (string.IsNullOrWhiteSpace(templateColumns))
				return defs;

			var parts = ExpandRepeats(templateColumns)
				.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			foreach (var part in parts)
			{
				var trimmed = part.Trim();

				if (trimmed.EndsWith("fr", StringComparison.OrdinalIgnoreCase))
				{
					var numStr = trimmed.Substring(0, trimmed.Length - 2);
					if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out var val))
					{
						defs.Add(new ColumnDef { Type = ColumnType.Fr, Value = val });
					}
				}
				else if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
				{
					var numStr = trimmed.Substring(0, trimmed.Length - 2);
					if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out var val))
					{
						var chars = Math.Max(1, (int)Math.Round(val / HtmlConstants.PxToCharRatio));
						defs.Add(new ColumnDef { Type = ColumnType.Fixed, Value = chars });
					}
				}
				else if (trimmed.EndsWith("em", StringComparison.OrdinalIgnoreCase))
				{
					var numStr = trimmed.Substring(0, trimmed.Length - 2);
					if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out var val))
					{
						var chars = Math.Max(1, (int)Math.Round(val * HtmlConstants.EmToCharRatio));
						defs.Add(new ColumnDef { Type = ColumnType.Fixed, Value = chars });
					}
				}
				else if (int.TryParse(trimmed, out var fixedVal))
				{
					defs.Add(new ColumnDef { Type = ColumnType.Fixed, Value = fixedVal });
				}
			}

			return defs;
		}

		/// <summary>
		/// The narrowest a column may be before a grid is not worth honouring.
		/// </summary>
		/// <remarks>
		/// A track has to hold a phrase, not a syllable. Sixteen columns is two or
		/// three words, which is the least that can be followed.
		/// <para>
		/// This was eight, chosen so a track could hold one English word. That
		/// was too generous: a page's desktop media query asks for eight or
		/// twelve tracks, which at terminal width are fifteen and ten columns -
		/// above the old floor, and unreadable. The number that matters is not
		/// whether a word fits but whether a reader can follow the text.
		/// </para>
		/// </remarks>
		private const int MinimumTrackWidth = 16;

		/// <summary>
		/// Whether the computed tracks are too narrow for the grid to mean anything.
		/// </summary>
		/// <remarks>
		/// One starved track is enough. A layout of twelve tracks where a single
		/// one is three columns wide still shreds whatever lands in it, and the
		/// page reads as broken however well its neighbours fared.
		/// </remarks>
		private static bool IsTooNarrowToHonour(int[] colWidths, int available)
		{
			// A single column is not a grid, and stacking it would change nothing.
			if (colWidths.Length < 2)
				return false;

			// Two measures, because neither alone is right.
			//
			// An absolute floor: below it a track holds a syllable rather than a
			// phrase, whatever the window is doing. A proportional floor as well,
			// because what makes a desktop grid unreadable here is not its width
			// in characters but how many ways it divides the page: eight tracks
			// is a set of ribbons at any window size a terminal has.
			//
			// The proportional test is what lets a two-column split of a narrow
			// window still work - half of forty is a legitimate column - while
			// rejecting the eight and twelve-track grids a page's desktop media
			// query asks for.
			int proportionalFloor = available / MaximumHonouredTracks;

			foreach (var width in colWidths)
			{
				if (width < MinimumTrackWidth || width < proportionalFloor)
					return true;
			}

			return false;
		}

		/// <summary>
		/// The most ways a page may divide the width before it stops being a
		/// layout and starts being a set of ribbons.
		/// </summary>
		/// <remarks>
		/// Three columns of a terminal is already narrow for prose; four is the
		/// point where a news headline stops being followable. A page's desktop
		/// grid asking for eight or twelve tracks is not describing something a
		/// terminal can show, so it is stacked instead.
		/// </remarks>
		private const int MaximumHonouredTracks = 4;

		/// <summary>
		/// Lays the cells out one under another, each at the full width.
		/// </summary>
		/// <remarks>
		/// What a browser does to a grid at its narrowest breakpoint, and the only
		/// arrangement a terminal has room for. Order is preserved, so the page
		/// still reads in the sequence it was written.
		/// </remarks>
		private static LayoutLine[] StackCells(
			List<IElement> children,
			int maxWidth,
			Color defaultFg,
			Color defaultBg,
			Color? linkColor)
		{
			if (maxWidth < 1)
				maxWidth = 1;

			var stacked = new List<LayoutLine>();

			foreach (var child in children)
			{
				var lines = HtmlBlockFlow.FlowBlocks(
					child,
					maxWidth,
					defaultFg,
					defaultBg,
					blockSpacing: 0,
					linkColor: linkColor);

				foreach (var line in lines)
				{
					var placed = line;
					placed.Y = stacked.Count;
					stacked.Add(placed);
				}
			}

			return stacked.ToArray();
		}

		private static int[] CalculateColumnWidths(List<ColumnDef> colDefs, int available)
		{
			int colCount = colDefs.Count;
			var widths = new int[colCount];

			// Allocate fixed columns first
			int usedByFixed = 0;
			for (int c = 0; c < colCount; c++)
			{
				if (colDefs[c].Type == ColumnType.Fixed)
				{
					widths[c] = (int)colDefs[c].Value;
					usedByFixed += widths[c];
				}
			}

			// Distribute remaining to fr columns
			int remainingForFr = available - usedByFixed;
			if (remainingForFr < 0) remainingForFr = 0;

			double totalFr = 0;
			for (int c = 0; c < colCount; c++)
			{
				if (colDefs[c].Type == ColumnType.Fr)
					totalFr += colDefs[c].Value;
			}

			if (totalFr > 0 && remainingForFr > 0)
			{
				int distributed = 0;
				int lastFrCol = -1;

				for (int c = 0; c < colCount; c++)
				{
					if (colDefs[c].Type == ColumnType.Fr)
					{
						widths[c] = (int)Math.Floor(colDefs[c].Value / totalFr * remainingForFr);
						distributed += widths[c];
						lastFrCol = c;
					}
				}

				// Give remainder to last fr column
				if (lastFrCol >= 0)
				{
					widths[lastFrCol] += remainingForFr - distributed;
				}
			}

			// Ensure minimum of 1 for all columns
			for (int c = 0; c < colCount; c++)
			{
				if (widths[c] < 1)
					widths[c] = 1;
			}

			return widths;
		}
	}
}

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
using Spectre.Console;

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
			if (colDefs.Count == 0)
				return Array.Empty<LayoutLine>();

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
							// Add gap spaces
							for (int g = 0; g < gap; g++)
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
		public static List<ColumnDef> ParseColumnDefs(string templateColumns)
		{
			var defs = new List<ColumnDef>();

			if (string.IsNullOrWhiteSpace(templateColumns))
				return defs;

			var parts = templateColumns.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

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

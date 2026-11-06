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
	/// Renders HTML table elements with box-drawing borders and proportional column widths.
	/// </summary>
	public static class HtmlTableLayout
	{
		/// <summary>
		/// One cell: the element it came from, plus the plain text used to measure it.
		/// </summary>
		/// <remarks>
		/// The element is kept rather than only its text because a cell's content
		/// is laid out properly once the column width is known - links keep their
		/// colour and their clickable region, bold keeps its weight, and block
		/// children keep their line breaks. Flattening to a string here was what
		/// turned a page built out of tables into an unbroken wall of characters
		/// with no reachable links.
		/// </remarks>
		private struct TableCell
		{
			/// <summary>The cell element, laid out once its width is known.</summary>
			public IElement? Element;

			/// <summary>
			/// The cell's visible text, for measuring only.
			/// </summary>
			/// <remarks>
			/// Column widths have to be chosen before any cell can be laid out, and
			/// laying every cell out at several candidate widths to find out how
			/// wide it wants to be would cost more than the measurement is worth.
			/// The text has the same characters as the rendered content, so it has
			/// the same natural width.
			/// </remarks>
			public string Content;

			public bool IsHeader;

			/// <summary>How many columns this cell occupies. One unless it spans.</summary>
			public int ColSpan;

			/// <summary>How many rows this cell occupies. One unless it spans.</summary>
			public int RowSpan;

			/// <summary>
			/// True for a column covered by a span that began elsewhere.
			/// </summary>
			/// <remarks>
			/// Spans are expanded into a full grid before anything is measured or
			/// drawn, so the renderer keeps its one-cell-per-column walk and the
			/// span exists only as "this column is already taken". A covered
			/// column contributes no content and no separator of its own.
			/// </remarks>
			public bool Covered;
		}

		/// <summary>
		/// What a cell needs in order to render a picture rather than its alt text.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Carried as one object rather than six more parameters: the table's
		/// entry point is public API with existing overloads, and widening every
		/// one of them for each field a picture happens to need would spread the
		/// decode's requirements across a signature that is about tables.
		/// </para>
		/// <para>
		/// Absent - which is what every existing caller passes - a cell behaves
		/// exactly as it always did, and its images arrive as alt text.
		/// </para>
		/// </remarks>
		public sealed class ImageContext
		{
			/// <summary>Pictures already decoded, keyed by source URL.</summary>
			public Dictionary<string, Imaging.PixelBuffer?>? ImageCache;

			/// <summary>The page's stylesheet, consulted for declared widths.</summary>
			public IDocument? CssDocument;

			/// <summary>The terminal's graphics protocol, when it has one.</summary>
			public Drivers.IGraphicsProtocol? GraphicsProtocol;

			/// <summary>The document's own URL, which decides local-file access.</summary>
			public string? DocumentUrl;

			/// <summary>Blank lines between blocks inside a cell.</summary>
			public int BlockSpacing = 1;
		}

		/// <summary>
		/// Lays out an HTML table element into LayoutLines with box-drawing borders.
		/// </summary>
		public static LayoutLine[] LayoutTable(
			IElement tableElement,
			int maxWidth,
			Color defaultFg,
			Color defaultBg,
			Color? linkColor = null)
			=> LayoutTable(tableElement, maxWidth, defaultFg, defaultBg, linkColor, null);

		/// <summary>
		/// Lays out an HTML table, colouring links the way the rest of the page does.
		/// </summary>
		/// <param name="tableElement">The <c>table</c> element.</param>
		/// <param name="maxWidth">Columns available to the whole table.</param>
		/// <param name="defaultFg">Foreground for ordinary cell text.</param>
		/// <param name="defaultBg">Background for the table.</param>
		/// <param name="linkColor">Colour for unvisited links inside cells.</param>
		/// <param name="visitedLinkColor">Colour for visited links inside cells.</param>
		/// <returns>The table's rendered lines, borders included.</returns>
		/// <remarks>
		/// An overload rather than an added default parameter, so an already-compiled
		/// caller keeps the behaviour it was built against.
		/// </remarks>
		public static LayoutLine[] LayoutTable(
			IElement tableElement,
			int maxWidth,
			Color defaultFg,
			Color defaultBg,
			Color? linkColor,
			Color? visitedLinkColor)
			=> LayoutTable(tableElement, maxWidth, defaultFg, defaultBg, linkColor, visitedLinkColor, null);

		/// <summary>
		/// Lays out an HTML table whose cells may hold pictures.
		/// </summary>
		/// <param name="tableElement">The <c>table</c> element.</param>
		/// <param name="maxWidth">Columns available to the whole table.</param>
		/// <param name="defaultFg">Foreground for ordinary cell text.</param>
		/// <param name="defaultBg">Background for the table.</param>
		/// <param name="linkColor">Colour for unvisited links inside cells.</param>
		/// <param name="visitedLinkColor">Colour for visited links inside cells.</param>
		/// <param name="images">
		/// What a cell needs to render a picture. Null - the default everywhere
		/// else - keeps the previous behaviour, where an image in a cell arrives
		/// as its alt text.
		/// </param>
		/// <returns>The table's rendered lines, borders included.</returns>
		public static LayoutLine[] LayoutTable(
			IElement tableElement,
			int maxWidth,
			Color defaultFg,
			Color defaultBg,
			Color? linkColor,
			Color? visitedLinkColor,
			ImageContext? images)
		{
			// 1. Extract rows and cells
			var rows = ExtractRows(tableElement);
			if (rows.Count == 0)
				return Array.Empty<LayoutLine>();

			// 2. Expand spans into a full grid, then normalize the column count.
			// Doing it here keeps every later step - measuring, wrapping, drawing -
			// on a plain rows x columns grid, with a span surviving only as the
			// width of the cell that starts it.
			int colCount = ExpandSpans(rows);

			if (colCount == 0)
				return Array.Empty<LayoutLine>();

			// 3. Calculate column widths
			int borderChars = colCount + 1; // │ at each edge and between columns
			int available = maxWidth - borderChars;
			if (available < colCount)
				available = colCount; // minimum 1 char per column

			var colWidths = CalculateColumnWidths(rows, colCount, available);

			// 4. Render
			var borderColor = HtmlConstants.DefaultTableBorderColor;
			var result = new List<LayoutLine>();

			// A table that declares border="0" is positioning things, not
			// presenting data, and its author has said so. Drawing it anyway
			// turned a page built out of layout tables into a grid of nested
			// boxes - one around each story and one around each empty spacer
			// cell - where none was asked for. The columns still do their work;
			// only the lines go.
			bool drawBorders = !DeclaresNoBorder(tableElement);

			// Top border: ┌─┬─┐
			if (drawBorders)
				result.Add(MakeBorderLine(colWidths, borderColor, defaultBg,
					HtmlConstants.TableTopLeft, HtmlConstants.TableHorizontal,
					HtmlConstants.TableTopTee, HtmlConstants.TableTopRight,
					dividedAbove: null, dividedBelow: DividedBoundaries(rows[0], colCount)));

			for (int r = 0; r < rows.Count; r++)
			{
				// Lay each cell out at its column's width. This is the whole point
				// of keeping the element: the result is CELLS, so a link arrives
				// already coloured and already carrying the region that makes it
				// clickable, and block children arrive on separate lines.
				var cellLines = new List<List<LayoutLine>>();
				int maxLines = 1;

				for (int c = 0; c < colCount; c++)
				{
					// A spanning cell is laid out at the width of every column it
					// covers, plus the separator between them that it swallows.
					var laid = LayoutCell(rows[r][c], SpanWidth(colWidths, c, rows[r][c].ColSpan),
						defaultFg, defaultBg, linkColor, visitedLinkColor, images);
					cellLines.Add(laid);
					if (laid.Count > maxLines)
						maxLines = laid.Count;
				}

				// Render each line of the row
				for (int line = 0; line < maxLines; line++)
				{
					var cells = new List<Cell>();
					List<LinkRegion>? rowLinks = null;

					for (int c = 0; c < colCount; c++)
					{
						// A column covered by a span. Which kind matters:
						//
						// Covered from the LEFT, by a colspan in this same row,
						// the cell was drawn as part of that span - skipping it
						// entirely is right, and drawing a separator would cut the
						// span in half.
						//
						// Covered from ABOVE, by a rowspan, nothing on THIS row has
						// drawn it, so the row still owes those columns their
						// width or it will close short of every other row.
						if (rows[r][c].Covered)
						{
							if (CoveredFromLeft(rows[r], c))
								continue;

							cells.Add(drawBorders
								? new Cell(HtmlConstants.TableVertical, borderColor, defaultBg)
								: new Cell(' ', defaultFg, defaultBg));
							for (int pad = 0; pad < colWidths[c]; pad++)
								cells.Add(new Cell(' ', defaultFg, defaultBg));
							continue;
						}

						int span = Math.Max(1, rows[r][c].ColSpan);
						int cellWidth = SpanWidth(colWidths, c, span);

						// Left border or separator. Without borders the columns are
						// still separated - by a space, so they stay legible as
						// columns rather than running into one another.
						cells.Add(drawBorders
							? new Cell(HtmlConstants.TableVertical, borderColor, defaultBg)
							: new Cell(' ', defaultFg, defaultBg));

						var source = line < cellLines[c].Count
							? cellLines[c][line]
							: default;

						// Where this cell's content begins in the row, so a link
						// region measured within the cell can be shifted to where
						// it actually lands. Without this every link in every
						// column but the first would be clickable at the wrong
						// place.
						int cellStart = cells.Count;

						int colsUsed = 0;
						if (source.Cells != null)
						{
							foreach (var cell in source.Cells)
							{
								// A continuation belongs to the wide rune before it and
								// is copied with it, never counted again.
								if (cell.IsWideContinuation)
								{
									if (colsUsed > 0) cells.Add(cell);
									continue;
								}

								int rw = Helpers.UnicodeWidth.GetRuneWidth(cell.Character);
								if (rw <= 0) continue;
								if (colsUsed + rw > cellWidth) break;

								cells.Add(cell);
								colsUsed += rw;
							}
						}

						// Pad remaining columns with spaces
						while (colsUsed < cellWidth)
						{
							cells.Add(new Cell(' ', defaultFg, defaultBg));
							colsUsed++;
						}

						if (source.Links != null)
						{
							rowLinks ??= new List<LinkRegion>();
							foreach (var link in source.Links)
							{
								int start = cellStart + link.StartX;
								int end = Math.Min(cellStart + link.EndX, cellStart + cellWidth);
								if (end <= start) continue;
								rowLinks.Add(new LinkRegion(start, end, link.Url, link.Text, link.LinkId));
							}
						}
					}

					// Right border
					cells.Add(drawBorders
						? new Cell(HtmlConstants.TableVertical, borderColor, defaultBg)
						: new Cell(' ', defaultFg, defaultBg));

					result.Add(new LayoutLine(0, 0, cells.Count, cells.ToArray(),
						TextAlignment.Left, rowLinks?.ToArray()));
				}

				// Row separator (between rows, not after last)
				if (drawBorders && r < rows.Count - 1)
				{
					result.Add(MakeBorderLine(colWidths, borderColor, defaultBg,
						HtmlConstants.TableLeftTee, HtmlConstants.TableHorizontal,
						HtmlConstants.TableCross, HtmlConstants.TableRightTee,
						dividedAbove: DividedBoundaries(rows[r], colCount),
						dividedBelow: DividedBoundaries(rows[r + 1], colCount)));
				}
			}

			// Bottom border: └─┴─┘
			if (drawBorders)
				result.Add(MakeBorderLine(colWidths, borderColor, defaultBg,
					HtmlConstants.TableBottomLeft, HtmlConstants.TableHorizontal,
					HtmlConstants.TableBottomTee, HtmlConstants.TableBottomRight,
					dividedAbove: DividedBoundaries(rows[rows.Count - 1], colCount),
					dividedBelow: null));

			return result.ToArray();
		}

		/// <summary>
		/// Lays a single cell's content out at the width its column was given.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Routed through the same inline flow the rest of the document uses, so a
		/// cell gets the same links, colours and decorations as the same markup
		/// would outside a table - rather than a second, poorer implementation
		/// that has to be taught each feature separately.
		/// </para>
		/// <para>
		/// A cell holding block children is flowed child by child, each starting on
		/// its own line. That is what stops a list of items - a Hacker News story
		/// and the next one - from arriving as a single run of characters.
		/// </para>
		/// </remarks>
		private static List<LayoutLine> LayoutCell(TableCell cell, int width,
			Color defaultFg, Color defaultBg, Color? linkColor, Color? visitedLinkColor,
			ImageContext? images)
		{
			if (cell.Element == null || width <= 0)
				return new List<LayoutLine>();

			// A header's weight is the cell's, not the markup's, so it is passed
			// down as an inherited decoration rather than wrapped around the
			// content.
			var inherited = cell.IsHeader ? TextDecoration.Bold : TextDecoration.None;

			var lines = new List<LayoutLine>();

			foreach (var part in SplitIntoBlocks(cell.Element))
			{
				// A table inside a cell is laid out AS a table, recursively. The
				// inline flow has no way to render one, so a nested table used to
				// come through as its text with every row and link lost - which is
				// exactly how a page that nests tables for layout, as Hacker News
				// does, lost its entire story list.
				if (part.Length == 1 && part[0] is IElement nested
					&& string.Equals(nested.LocalName, "table", StringComparison.OrdinalIgnoreCase))
				{
					lines.AddRange(LayoutTable(nested, width, defaultFg, defaultBg,
						linkColor, visitedLinkColor, images));
					continue;
				}

				// A picture in a cell is block content, the same as a picture in a
				// paragraph, and the inline flow cannot emit rows. The block flow
				// can, and it already knows how to interleave the two, so a part
				// holding one is handed to it whole rather than flowed as text.
				if (images != null && HoldsRenderableImage(part))
				{
					lines.AddRange(HtmlBlockFlow.FlowInlineRunWithImages(
						part, width, defaultFg, defaultBg, images.BlockSpacing,
						linkColor, visitedLinkColor, images.ImageCache,
						images.CssDocument, images.GraphicsProtocol, images.DocumentUrl,
						inherited));
					continue;
				}

				var flowed = HtmlInlineFlow.FlowInline(part, width, defaultFg, defaultBg,
					inherited, TextAlignment.Left, linkColor, visitedLinkColor);

				// A block that produced nothing still separates its neighbours; an
				// empty leading one would only indent the cell.
				if (flowed.Count == 0)
				{
					if (lines.Count > 0)
						lines.Add(new LayoutLine(0, 0, 0, Array.Empty<Cell>(), TextAlignment.Left));
					continue;
				}

				lines.AddRange(flowed);
			}

			// A trailing blank line is padding inside the cell: it adds a row to
			// the whole table and leaves a gap under the content. One appears
			// whenever the content ends on a break - a cell ending in <br>, or a
			// block whose flow closed its last line.
			while (lines.Count > 0 && IsBlank(lines[^1]))
				lines.RemoveAt(lines.Count - 1);

			return lines;
		}

		/// <summary>
		/// Whether this tag inside a cell starts a part of its own.
		/// </summary>
		/// <remarks>
		/// The block tags, minus <c>br</c>. A <c>br</c> counts as a block for the
		/// page's block flow, but within a cell it is a line break that the inline
		/// flow already produces - splitting on it as well would break the run in
		/// two and leave a blank line between the halves.
		/// </remarks>
		/// <summary>
		/// Whether a run of cell nodes holds an image that would render.
		/// </summary>
		/// <remarks>
		/// A picture is routinely wrapped - in a link, a figure, a span - so the
		/// test is "contains one", not "is one". An <c>img</c> with no source
		/// renders nothing, and must not divert the run away from the plain text
		/// path that lays a cell out exactly as it always did.
		/// </remarks>
		private static bool HoldsRenderableImage(INodeList nodes)
		{
			foreach (var node in nodes)
			{
				if (node is not IElement element)
					continue;

				if (element.LocalName.Equals("img", StringComparison.OrdinalIgnoreCase))
				{
					if (!string.IsNullOrEmpty(element.GetAttribute("src")))
						return true;
					continue;
				}

				if (element.QuerySelector("img[src]") != null)
					return true;
			}

			return false;
		}

		private static bool StartsItsOwnPart(string? tag) =>
			HtmlBlockFlow.IsBlockTag(tag)
			&& !string.Equals(tag, "br", StringComparison.OrdinalIgnoreCase);

		/// <summary>
		/// Whether this covered column was claimed from within its own row.
		/// </summary>
		/// <remarks>
		/// Walks left to the nearest cell that is not covered and asks whether its
		/// span reaches here. If it does, this column was drawn as part of that
		/// cell; if not, the cover came from a rowspan above and the column still
		/// needs its width on this row.
		/// </remarks>
		private static bool CoveredFromLeft(List<TableCell> row, int col)
		{
			for (int c = col - 1; c >= 0; c--)
			{
				if (row[c].Covered) continue;
				return c + Math.Max(1, row[c].ColSpan) > col;
			}

			return false;
		}

		/// <summary>
		/// The width of a cell covering <paramref name="span"/> columns from <paramref name="start"/>.
		/// </summary>
		/// <remarks>
		/// The columns themselves plus the separators between them, which a
		/// spanning cell occupies rather than drawing. Without that the cell would
		/// come up one character short per column crossed and the row would not
		/// close where every other row does.
		/// </remarks>
		private static int SpanWidth(int[] colWidths, int start, int span)
		{
			int width = 0;
			int count = Math.Min(Math.Max(1, span), colWidths.Length - start);

			for (int i = 0; i < count; i++)
				width += colWidths[start + i];

			// One separator swallowed for each extra column covered.
			width += count - 1;
			return width;
		}

		/// <summary>
		/// Reads a span attribute, clamped to something sane.
		/// </summary>
		/// <remarks>
		/// Real pages carry nonsense here. An unparseable or absent value is one
		/// column; a wild one is capped rather than trusted, since a span is
		/// clamped to the table's width later anyway and a huge number only risks
		/// a huge allocation on the way.
		/// </remarks>
		private static int SpanAttribute(IElement element, string name)
		{
			var raw = element.GetAttribute(name);
			if (raw == null || !int.TryParse(raw.Trim(), out int span) || span < 1)
				return 1;

			return Math.Min(span, MaxSpan);
		}

		/// <summary>The widest span honoured, before clamping to the table itself.</summary>
		private const int MaxSpan = 64;

		/// <summary>
		/// Expands colspan and rowspan into a rectangular grid of cells.
		/// </summary>
		/// <param name="rows">The rows as collected; rewritten in place.</param>
		/// <returns>The table's column count.</returns>
		/// <remarks>
		/// <para>
		/// Each cell is placed at the first column not already taken, then marks
		/// the columns and rows it covers so nothing else lands there. That is
		/// what makes a row beneath a span line up: it has fewer cells to place,
		/// and they go in the columns that are actually free.
		/// </para>
		/// <para>
		/// Ignoring the attributes put every cell in the next column along, so a
		/// Hacker News score - which sits under a <c>colspan="2"</c> spacer -
		/// landed beneath the vote arrow rather than beneath its own title.
		/// </para>
		/// </remarks>
		private static int ExpandSpans(List<List<TableCell>> rows)
		{
			// Which (row, column) slots are already claimed by a span from above
			// or to the left.
			var taken = new List<HashSet<int>>();
			for (int r = 0; r < rows.Count; r++)
				taken.Add(new HashSet<int>());

			var placed = new List<List<TableCell?>>();
			for (int r = 0; r < rows.Count; r++)
				placed.Add(new List<TableCell?>());

			int colCount = 0;

			for (int r = 0; r < rows.Count; r++)
			{
				int col = 0;
				foreach (var cell in rows[r])
				{
					while (taken[r].Contains(col))
						col++;

					int colSpan = Math.Max(1, cell.ColSpan);
					int rowSpan = Math.Max(1, cell.RowSpan);

					var placedCell = cell;
					placedCell.ColSpan = colSpan;

					Put(placed, r, col, placedCell);

					for (int c = 0; c < colSpan; c++)
					{
						for (int rr = 0; rr < rowSpan; rr++)
						{
							int targetRow = r + rr;
							if (targetRow >= rows.Count) break;
							// The cell's own slot is not "covered"; the rest are.
							if (rr == 0 && c == 0) continue;
							taken[targetRow].Add(col + c);
							Put(placed, targetRow, col + c,
								new TableCell { Covered = true, Content = "", ColSpan = 1, RowSpan = 1 });
						}
					}

					col += colSpan;
					if (col > colCount) colCount = col;
				}
			}

			// Rewrite the rows as a rectangle, filling any gap with an empty cell.
			for (int r = 0; r < rows.Count; r++)
			{
				var row = new List<TableCell>(colCount);
				for (int c = 0; c < colCount; c++)
				{
					var cell = c < placed[r].Count ? placed[r][c] : null;
					row.Add(cell ?? new TableCell { Content = "", ColSpan = 1, RowSpan = 1 });
				}
				rows[r] = row;
			}

			// A span may have claimed more columns than the table really has;
			// clamp each cell so no row can draw past the grid.
			foreach (var row in rows)
			{
				for (int c = 0; c < row.Count; c++)
				{
					var cell = row[c];
					if (cell.ColSpan > colCount - c)
					{
						cell.ColSpan = Math.Max(1, colCount - c);
						row[c] = cell;
					}
				}
			}

			return colCount;

			static void Put(List<List<TableCell?>> grid, int row, int col, TableCell cell)
			{
				while (grid[row].Count <= col)
					grid[row].Add(null);
				grid[row][col] = cell;
			}
		}

		/// <summary>
		/// Whether a table says it should be drawn without lines.
		/// </summary>
		/// <remarks>
		/// <c>border="0"</c>, and the CSS equivalents a page may use instead. Only
		/// an explicit "no" counts: a table that says nothing keeps its lines,
		/// because a data table written without the attribute still reads best
		/// with them.
		/// </remarks>
		private static bool DeclaresNoBorder(IElement tableElement)
		{
			var attribute = tableElement.GetAttribute("border");
			if (attribute != null && attribute.Trim() == "0")
				return true;

			var style = tableElement.GetAttribute("style");
			if (string.IsNullOrWhiteSpace(style))
				return false;

			return System.Text.RegularExpressions.Regex.IsMatch(
				style, @"border\s*:\s*(none|0)",
				System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		}

		/// <summary>True when a line holds nothing but blanks.</summary>
		private static bool IsBlank(LayoutLine line)
		{
			if (line.Cells == null || line.Cells.Length == 0)
				return true;

			foreach (var cell in line.Cells)
			{
				if (cell.Character.Value != ' ')
					return false;
			}

			return true;
		}

		/// <summary>
		/// Splits a cell into the runs of content that each deserve their own line.
		/// </summary>
		/// <remarks>
		/// Consecutive inline nodes flow together as one run; a block child breaks
		/// the run and takes a line of its own. A cell with no block children
		/// yields exactly one run, which is the ordinary case and costs nothing.
		/// </remarks>
		private static List<INodeList> SplitIntoBlocks(IElement cellElement)
		{
			var parts = new List<INodeList>();

			bool hasBlockChild = false;
			foreach (var child in cellElement.Children)
			{
				if (StartsItsOwnPart(child.LocalName))
				{
					hasBlockChild = true;
					break;
				}
			}

			if (!hasBlockChild)
			{
				parts.Add(cellElement.ChildNodes);
				return parts;
			}

			// Mixed content: gather inline runs between block children, and give
			// each block child a list of its own.
			var pending = new List<INode>();

			void FlushPending()
			{
				if (pending.Count == 0) return;
				parts.Add(new StaticNodeList(pending.ToArray()));
				pending = new List<INode>();
			}

			foreach (var child in cellElement.ChildNodes)
			{
				if (child is IElement el && StartsItsOwnPart(el.LocalName))
				{
					FlushPending();
					// A nested table is handed over whole, because LayoutCell lays it
					// out as a table. Any other block contributes its CHILDREN: the
					// inline flow skips a block element it is handed directly, so
					// passing <p> itself would silently drop the paragraph.
					if (string.Equals(el.LocalName, "table", StringComparison.OrdinalIgnoreCase))
						parts.Add(new StaticNodeList(new INode[] { el }));
					else
						parts.Add(el.ChildNodes);
					continue;
				}

				pending.Add(child);
			}

			FlushPending();
			return parts;
		}

		/// <summary>
		/// A fixed list of nodes, so a slice of a cell can be handed to the inline flow.
		/// </summary>
		/// <remarks>
		/// <see cref="HtmlInlineFlow.FlowInline"/> takes an <see cref="INodeList"/>,
		/// and AngleSharp only ever hands out live ones belonging to a parent. A run
		/// of children between two block siblings is not such a list, so it needs
		/// this wrapper to be flowed on its own.
		/// </remarks>
		private sealed class StaticNodeList : INodeList
		{
			private readonly INode[] _nodes;

			public StaticNodeList(INode[] nodes) => _nodes = nodes;

			public INode this[int index] => _nodes[index];

			public int Length => _nodes.Length;

			public IEnumerator<INode> GetEnumerator() =>
				((IEnumerable<INode>)_nodes).GetEnumerator();

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
				_nodes.GetEnumerator();

			public void ToHtml(System.IO.TextWriter writer, AngleSharp.IMarkupFormatter formatter)
			{
				foreach (var node in _nodes)
					node.ToHtml(writer, formatter);
			}
		}

		private static List<List<TableCell>> ExtractRows(IElement tableElement)
		{
			var rows = new List<List<TableCell>>();
			CollectRows(tableElement, rows, isRoot: true);
			return rows;
		}

		/// <summary>
		/// Recursively walks children of <paramref name="node"/> collecting &lt;tr&gt; rows that
		/// belong to this table, while skipping any descendant &lt;table&gt; (nested tables are
		/// rendered as text inside their parent cell instead of having their rows hoisted).
		/// </summary>
		private static void CollectRows(INode node, List<List<TableCell>> rows, bool isRoot)
		{
			foreach (var child in node.ChildNodes)
			{
				if (child is not IElement el)
					continue;

				var tag = el.LocalName.ToLowerInvariant();

				// Do not descend into a nested <table>; its rows belong to it, not us.
				if (!isRoot && tag == "table")
					continue;

				if (tag == "tr")
				{
					var row = new List<TableCell>();
					foreach (var cellEl in el.Children)
					{
						var cellTag = cellEl.LocalName.ToLowerInvariant();
						if (cellTag == "td" || cellTag == "th")
						{
							row.Add(new TableCell
							{
								Element = cellEl,
								Content = GetVisibleText(cellEl),
								IsHeader = cellTag == "th",
								ColSpan = SpanAttribute(cellEl, "colspan"),
								RowSpan = SpanAttribute(cellEl, "rowspan"),
							});
						}
					}
					rows.Add(row);
					continue;
				}

				// Recurse into structural wrappers (thead, tbody, tfoot, caption, colgroup, etc.)
				CollectRows(el, rows, isRoot: false);
			}
		}

		/// <summary>
		/// Extracts visible text from an element, excluding script/style content, and
		/// collapses all ASCII whitespace runs (spaces, tabs, newlines, carriage returns,
		/// form feeds) into a single space — mirroring HTML's whitespace-collapsing rules.
		/// Control characters must never be stored in Cells because they corrupt the
		/// terminal output stream.
		/// </summary>
		private static string GetVisibleText(IElement element)
		{
			var sb = new System.Text.StringBuilder();
			CollectVisibleText(element, sb);
			return CollapseWhitespace(sb.ToString());
		}

		private static void CollectVisibleText(INode node, System.Text.StringBuilder sb)
		{
			foreach (var child in node.ChildNodes)
			{
				if (child is IText text)
				{
					sb.Append(text.Data);
				}
				else if (child is IElement el)
				{
					var tag = el.LocalName.ToLowerInvariant();
					if (tag is "script" or "style" or "noscript")
						continue;
					// <br> introduces a break — use a space so adjacent words don't concatenate.
					if (tag == "br")
					{
						sb.Append(' ');
						continue;
					}
					CollectVisibleText(el, sb);
				}
			}
		}

		/// <summary>
		/// Collapses runs of ASCII whitespace into a single space and trims edges.
		/// </summary>
		private static string CollapseWhitespace(string s)
		{
			if (string.IsNullOrEmpty(s))
				return string.Empty;

			var sb = new System.Text.StringBuilder(s.Length);
			bool lastWasSpace = true; // trims leading whitespace
			foreach (var ch in s)
			{
				bool isWs = ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r' || ch == '\f' || ch == '\v';
				if (isWs)
				{
					if (!lastWasSpace)
					{
						sb.Append(' ');
						lastWasSpace = true;
					}
				}
				else
				{
					sb.Append(ch);
					lastWasSpace = false;
				}
			}
			// Trim trailing space
			if (sb.Length > 0 && sb[^1] == ' ')
				sb.Length -= 1;
			return sb.ToString();
		}

		private static int[] CalculateColumnWidths(
			List<List<TableCell>> rows,
			int colCount,
			int available)
		{
			var minWidths = new int[colCount];
			var prefWidths = new int[colCount];

			for (int c = 0; c < colCount; c++)
			{
				int minW = 1;
				int prefW = 1;

				foreach (var row in rows)
				{
					var content = row[c].Content;
					if (string.IsNullOrEmpty(content))
						continue;

					// Min width = longest word (in column width, not chars)
					var words = content.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (var word in words)
					{
						int wordCols = word.EnumerateRunes().Sum(r => Helpers.UnicodeWidth.GetRuneWidth(r));
						if (wordCols > minW)
							minW = wordCols;
					}

					// Preferred width = full content column width
					int contentCols = content.EnumerateRunes().Sum(r => Helpers.UnicodeWidth.GetRuneWidth(r));
					if (contentCols > prefW)
						prefW = contentCols;
				}

				minWidths[c] = minW;
				prefWidths[c] = prefW;
			}

			var widths = new int[colCount];
			int totalPref = 0;
			int totalMin = 0;
			for (int c = 0; c < colCount; c++)
			{
				totalPref += prefWidths[c];
				totalMin += minWidths[c];
			}

			if (totalPref <= available)
			{
				// Use preferred, distribute excess evenly
				int excess = available - totalPref;
				int perCol = excess / colCount;
				int remainder = excess % colCount;

				for (int c = 0; c < colCount; c++)
				{
					widths[c] = prefWidths[c] + perCol + (c < remainder ? 1 : 0);
				}
			}
			else if (totalMin >= available)
			{
				// Equal distribution
				int perCol = available / colCount;
				int remainder = available % colCount;
				for (int c = 0; c < colCount; c++)
				{
					widths[c] = perCol + (c < remainder ? 1 : 0);
				}
			}
			else
			{
				// Proportional from preferred, respecting minimums
				// First assign minimums
				for (int c = 0; c < colCount; c++)
					widths[c] = minWidths[c];

				int remaining = available - totalMin;

				// Distribute remaining proportionally based on (pref - min)
				int totalExcess = totalPref - totalMin;
				if (totalExcess > 0)
				{
					for (int c = 0; c < colCount; c++)
					{
						int extra = (int)Math.Round((double)(prefWidths[c] - minWidths[c]) / totalExcess * remaining);
						widths[c] += extra;
					}

					// Fix rounding: adjust to match available. Keep iterating until diff == 0
					// — a single pass can leave leftovers because the reduce path skips columns
					// already at their minimum, and the add path is capped by column count.
					int total = 0;
					for (int c = 0; c < colCount; c++)
						total += widths[c];

					int diff = available - total;
					int guard = colCount * 4; // safety: bounded iterations
					while (diff != 0 && guard-- > 0)
					{
						bool changed = false;
						for (int c = 0; c < colCount && diff != 0; c++)
						{
							if (diff > 0) { widths[c]++; diff--; changed = true; }
							else if (diff < 0 && widths[c] > minWidths[c]) { widths[c]--; diff++; changed = true; }
						}
						if (!changed) break; // cannot reduce further — all columns at min
					}
				}
			}

			// Ensure minimum of 1
			for (int c = 0; c < colCount; c++)
			{
				if (widths[c] < 1)
					widths[c] = 1;
			}

			return widths;
		}

		private static LayoutLine MakeBorderLine(
			int[] colWidths,
			Color borderColor,
			Color bg,
			char left,
			char horizontal,
			char junction,
			char right,
			bool[]? dividedAbove = null,
			bool[]? dividedBelow = null)
		{
			var cells = new List<Cell>();
			cells.Add(new Cell(left, borderColor, bg));

			for (int c = 0; c < colWidths.Length; c++)
			{
				for (int i = 0; i < colWidths[c]; i++)
					cells.Add(new Cell(horizontal, borderColor, bg));

				if (c < colWidths.Length - 1)
				{
					cells.Add(new Cell(
						JunctionFor(c, junction, dividedAbove, dividedBelow),
						borderColor, bg));
				}
			}

			cells.Add(new Cell(right, borderColor, bg));

			return new LayoutLine(0, 0, cells.Count, cells.ToArray(), TextAlignment.Left);
		}

		/// <summary>
		/// The glyph where a horizontal rule crosses one column boundary.
		/// </summary>
		/// <remarks>
		/// <para>
		/// A junction is where vertical rules meet a horizontal one, so which arms
		/// it needs depends on whether the rows on either side actually draw a
		/// divider at this boundary. A cell spanning the table draws none, and the
		/// rule beside it used to sprout a cross regardless - a stub of line
		/// pointing into open space, which is what made an infobox look broken.
		/// </para>
		/// <para>
		/// With neither neighbour divided the rule simply passes through. Callers
		/// that know nothing about their neighbours - the top and bottom edges of
		/// the table, which have rows on one side only - pass nothing and keep the
		/// junction they asked for.
		/// </para>
		/// </remarks>
		private static char JunctionFor(int boundary, char junction, bool[]? above, bool[]? below)
		{
			if (above == null && below == null)
				return junction;

			bool up = above != null && boundary < above.Length && above[boundary];
			bool down = below != null && boundary < below.Length && below[boundary];

			if (up && down) return HtmlConstants.TableCross;
			if (up) return HtmlConstants.TableBottomTee;
			if (down) return HtmlConstants.TableTopTee;

			// Neither side is divided here: the rule runs straight through, which
			// is what the eye expects under a cell that spans the boundary.
			return HtmlConstants.TableHorizontal;
		}

		/// <summary>
		/// Which column boundaries a row actually divides.
		/// </summary>
		/// <remarks>
		/// A boundary is divided when the cell to its left ends there. A cell that
		/// spans it swallows the boundary, so no vertical rule is drawn and the
		/// horizontal rules beside it must not pretend otherwise.
		/// </remarks>
		private static bool[] DividedBoundaries(List<TableCell> row, int colCount)
		{
			var divided = new bool[Math.Max(0, colCount - 1)];

			for (int c = 0; c < colCount - 1 && c < row.Count; c++)
			{
				// The boundary after column c survives unless the cell occupying
				// c reaches past it. A covered column belongs to a span that began
				// to its left, so it ends no cell of its own.
				if (row[c].Covered)
					continue;

				divided[c] = row[c].ColSpan <= 1;
			}

			return divided;
		}

		private static int StringColumnWidth(string s)
		{
			return s.EnumerateRunes().Sum(r => Helpers.UnicodeWidth.GetRuneWidth(r));
		}

		private static string[] WrapText(string text, int width)
		{
			if (string.IsNullOrEmpty(text))
				return new[] { "" };

			if (width <= 0)
				width = 1;

			var lines = new List<string>();
			var words = text.Split(' ');
			var currentLine = "";
			int currentCols = 0;

			foreach (var word in words)
			{
				int wordCols = StringColumnWidth(word);

				if (currentCols == 0)
				{
					if (wordCols > width)
					{
						// Break long word by column width
						var sb = new System.Text.StringBuilder();
						int cols = 0;
						foreach (var rune in word.EnumerateRunes())
						{
							int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
							if (cols + rw > width && sb.Length > 0)
							{
								lines.Add(sb.ToString());
								sb.Clear();
								cols = 0;
							}
							sb.Append(rune);
							cols += rw;
						}
						currentLine = sb.ToString();
						currentCols = cols;
					}
					else
					{
						currentLine = word;
						currentCols = wordCols;
					}
				}
				else if (currentCols + 1 + wordCols <= width)
				{
					currentLine += " " + word;
					currentCols += 1 + wordCols;
				}
				else
				{
					lines.Add(currentLine);
					if (wordCols > width)
					{
						var sb = new System.Text.StringBuilder();
						int cols = 0;
						foreach (var rune in word.EnumerateRunes())
						{
							int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
							if (cols + rw > width && sb.Length > 0)
							{
								lines.Add(sb.ToString());
								sb.Clear();
								cols = 0;
							}
							sb.Append(rune);
							cols += rw;
						}
						currentLine = sb.ToString();
						currentCols = cols;
					}
					else
					{
						currentLine = word;
						currentCols = wordCols;
					}
				}
			}

			if (currentLine.Length > 0)
				lines.Add(currentLine);

			if (lines.Count == 0)
				lines.Add("");

			return lines.ToArray();
		}

		private static string PadRight(string text, int width)
		{
			// Measure column width accounting for wide characters
			int colWidth = 0;
			int charCount = 0;
			foreach (var rune in text.EnumerateRunes())
			{
				int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
				if (colWidth + rw > width) break;
				colWidth += rw;
				charCount += rune.Utf16SequenceLength;
			}

			var truncated = charCount < text.Length ? text[..charCount] : text;
			int padding = width - colWidth;
			return padding > 0 ? truncated + new string(' ', padding) : truncated;
		}
	}
}

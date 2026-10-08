// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// Where the last paint of a <see cref="TableControl"/> put its columns, so a pointer position can be
/// turned back into a column and an offset within it.
/// </summary>
/// <remarks>
/// <para>
/// Positions are LOGICAL: counted along a row's full, unscrolled content from just inside the left
/// border, the same stream the painter draws by. A control-relative x maps to one by adding the
/// horizontal scroll, so every hit test agrees with what is on screen however far the table is
/// panned. Hit testing used to compare x with the unscrolled positions directly, so once scrolled a
/// click was attributed to the column that would have been there, not the one drawn there.
/// </para>
/// <para>
/// Recorded on every paint whether or not the header is drawn, and immutable once recorded, so a
/// paint on one thread cannot change the answers a hit test on another is in the middle of.
/// </para>
/// </remarks>
internal sealed class TableGeometry
{
	private readonly int[] _columnStarts;
	private readonly int[] _columnWidths;

	/// <summary>Records the column layout of one paint.</summary>
	/// <param name="contentLeft">Control-relative x where logical position 0 is drawn.</param>
	/// <param name="contentRight">Control-relative x just past the last cell a row can show.</param>
	/// <param name="horizontalScroll">The horizontal scroll the paint applied.</param>
	/// <param name="columnStarts">The logical start of each data column.</param>
	/// <param name="columnWidths">The width of each data column.</param>
	/// <param name="checkboxWidth">The checkbox column's width at logical 0, or 0 without one.</param>
	/// <param name="lines">Which lines the paint gave to the title, header, data rows and filter bar.</param>
	internal TableGeometry(int contentLeft, int contentRight, int horizontalScroll, int[] columnStarts, int[] columnWidths, int checkboxWidth,
		TableLineBands lines)
	{
		ContentLeft = contentLeft;
		ContentRight = contentRight;
		HorizontalScroll = horizontalScroll;
		_columnStarts = columnStarts;
		_columnWidths = columnWidths;
		CheckboxWidth = checkboxWidth;
		Lines = lines;
	}

	/// <summary>Which lines the paint gave to the title, header, data rows and filter bar.</summary>
	internal TableLineBands Lines { get; }

	/// <summary>Control-relative x where logical position 0 is drawn.</summary>
	internal int ContentLeft { get; }

	/// <summary>Control-relative x just past the last cell a row can show: the right border or the scrollbar.</summary>
	internal int ContentRight { get; }

	/// <summary>The horizontal scroll the paint applied.</summary>
	internal int HorizontalScroll { get; }

	/// <summary>The checkbox column's width, at logical 0, or 0 without one.</summary>
	internal int CheckboxWidth { get; }

	/// <summary>The number of data columns.</summary>
	internal int ColumnCount => _columnWidths.Length;

	/// <summary>The width of a data column.</summary>
	internal int GetColumnWidth(int column) => _columnWidths[column];

	/// <summary>The logical start of a data column.</summary>
	internal int GetColumnStart(int column) => _columnStarts[column];

	/// <summary>The logical position drawn at a control-relative x, or -1 left of the content.</summary>
	internal int ToLogical(int x) => x < ContentLeft ? -1 : x - ContentLeft + HorizontalScroll;

	/// <summary>The control-relative x where a logical position is drawn.</summary>
	internal int ToScreen(int logical) => logical - HorizontalScroll + ContentLeft;

	/// <summary>
	/// The data column drawn at a control-relative x, or -1 when none is, with the offset into it.
	/// </summary>
	internal int GetColumnAt(int x, out int cellOffset)
	{
		cellOffset = -1;
		int logical = ToLogical(x);
		if (logical < 0) return -1;

		for (int c = 0; c < _columnStarts.Length; c++)
		{
			int offset = logical - _columnStarts[c];
			if (offset >= 0 && offset < _columnWidths[c])
			{
				cellOffset = offset;
				return c;
			}
		}
		return -1;
	}

	/// <summary>Whether a control-relative x falls on the checkbox column.</summary>
	internal bool IsOnCheckbox(int x)
	{
		int logical = ToLogical(x);
		return logical >= 0 && logical < CheckboxWidth;
	}

	/// <summary>
	/// The first data column whose right edge is drawn within <paramref name="tolerance"/> cells of
	/// a control-relative x, or -1.
	/// </summary>
	internal int GetColumnBorderAt(int x, int tolerance)
	{
		for (int c = 0; c < _columnStarts.Length; c++)
		{
			int edge = ToScreen(_columnStarts[c] + _columnWidths[c]);
			if (Math.Abs(x - edge) <= tolerance)
				return c;
		}
		return -1;
	}
}

/// <summary>
/// The lines of a painted table, control-relative: each is -1 when that part was not drawn, and
/// each range is half-open.
/// </summary>
/// <param name="Title">The title line.</param>
/// <param name="Header">The header line.</param>
/// <param name="DataTop">The first line of the data area.</param>
/// <param name="DataBottom">The line after the data area, including the blank lines that pad it.</param>
/// <param name="FilterBarTop">The first line of the filter bar: the line above the status row.</param>
/// <param name="FilterBarBottom">The line after the status row.</param>
internal readonly record struct TableLineBands(
	int Title,
	int Header,
	int DataTop,
	int DataBottom,
	int FilterBarTop,
	int FilterBarBottom)
{
	/// <summary>Whether a line is in the data area.</summary>
	internal bool IsData(int y) => y >= DataTop && y < DataBottom;

	/// <summary>Whether a line is in the filter bar.</summary>
	internal bool IsFilterBar(int y) => y >= FilterBarTop && y < FilterBarBottom;
}

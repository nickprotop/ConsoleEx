// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace Cxdbg.Session;

/// <summary>
/// One row of the target's screen, as a table of its columns.
/// </summary>
/// <remarks>
/// <para>
/// A data source rather than rows, because the cells change several times a
/// second and building a row object per column each time would be work the
/// table can avoid: it asks for the cells it is showing and no others.
/// </para>
/// <para>
/// A table, not a list of lines. An earlier version wrote one line per column,
/// which made a tall thin list inside a wide shallow dock - the data read
/// vertically while the dock ran horizontally, which is backwards for a row of
/// a screen.
/// </para>
/// </remarks>
internal sealed class CellRowSource : ITableDataSource
{
	private IReadOnlyList<Cell> _cells = [];

	/// <summary>Raised when the row changes, which is how the table knows.</summary>
	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	/// <summary>The screen row these cells came from.</summary>
	internal int Row { get; private set; }

	/// <summary>Takes a row of the screen.</summary>
	internal void SetRow(int row, IReadOnlyList<Cell> cells)
	{
		Row = row;
		_cells = cells;

		// Reset rather than a per-item notification: a new row of the screen
		// is a different set of cells, not an edit of the old one.
		CollectionChanged?.Invoke(this,
			new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	/// <summary>One row per screen column.</summary>
	public int RowCount => _cells.Count;

	public int ColumnCount => 5;

	public string GetColumnHeader(int columnIndex) => columnIndex switch
	{
		0 => "col",
		1 => "glyph",
		2 => "width",
		3 => "marks",
		4 => "style",
		_ => "",
	};

	/// <summary>
	/// Narrow columns for the narrow facts.
	/// </summary>
	/// <remarks>
	/// The widths belong here rather than on the control: with a data source
	/// the source defines the columns, so AddColumn calls on the builder are
	/// discarded and the table divides the width evenly - a one-character
	/// glyph in twenty-three columns.
	/// </remarks>
	public int? GetColumnWidth(int columnIndex) => columnIndex switch
	{
		0 => 5,
		1 => 7,
		2 => 6,
		3 => 6,
		_ => null,
	};

	public string GetCellValue(int rowIndex, int columnIndex)
	{
		if (rowIndex < 0 || rowIndex >= _cells.Count) return "";

		var c = _cells[rowIndex];

		// A continuation cell has no glyph of its own: it is the second column
		// of the one before it. Saying so, rather than repeating that glyph, is
		// the whole reason to look here.
		if (c.IsWideContinuation)
		{
			return columnIndex switch
			{
				0 => rowIndex.ToString(),
				1 => "[dim]<-[/]",
				2 => "[dim]cont[/]",
				_ => "",
			};
		}

		int width = UnicodeWidth.GetRuneWidth(c.Character);

		return columnIndex switch
		{
			0 => rowIndex.ToString(),
			1 => Glyph(c),
			2 => width == 2 ? "[yellow]2[/]" : "1",
			3 => c.Combiners is { Length: > 0 } m ? $"[yellow]+{m.Length}[/]" : "",
			4 => c.Decorations == TextDecoration.None
				? ""
				: c.Decorations.ToString().ToLowerInvariant(),
			_ => "",
		};
	}

	/// <summary>A glyph that can be read on a table row.</summary>
	/// <remarks>
	/// A space is indistinguishable from the padding around it, and a control
	/// character would move the cursor of the terminal drawing the table.
	/// </remarks>
	private static string Glyph(Cell c)
	{
		if (c.Character.Value == ' ') return "[dim]space[/]";

		if (Rune.IsControl(c.Character))
			return $"[dim]U+{c.Character.Value:X4}[/]";

		return c.Character.ToString().Replace("[", "[[");
	}
}

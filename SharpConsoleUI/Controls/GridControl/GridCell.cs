// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A lightweight value-type handle to a single cell of a <see cref="GridControl"/>, addressed by its
	/// top-left row and column. It is not a control and is not stored in the grid: it holds only the grid
	/// reference plus the cell coordinate, and every member reads or writes the grid's cell store directly.
	/// Obtain one from the grid's indexer (<c>grid[row, col]</c>) or <see cref="GridControl.Cell"/>.
	/// </summary>
	/// <remarks>
	/// Because the struct is a transient handle, its setters mutate the grid (not the struct), so writing
	/// through a copied handle still affects the grid. Styling data lives on the cell's
	/// <see cref="GridPlacement"/>, which lets an empty cell be styled before it is filled.
	/// </remarks>
	public readonly struct GridCell
	{
		private readonly GridControl _grid;
		private readonly int _row;
		private readonly int _col;

		/// <summary>
		/// Initializes a handle to the cell at <paramref name="row"/>/<paramref name="col"/> of
		/// <paramref name="grid"/>. Constructed by <see cref="GridControl"/>; not intended for direct use.
		/// </summary>
		/// <param name="grid">The owning grid.</param>
		/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
		/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
		internal GridCell(GridControl grid, int row, int col)
		{
			_grid = grid;
			_row = row;
			_col = col;
		}

		/// <summary>
		/// Gets the zero-based row index of this cell's top-left corner.
		/// </summary>
		public int Row => _row;

		/// <summary>
		/// Gets the zero-based column index of this cell's top-left corner.
		/// </summary>
		public int Col => _col;

		/// <summary>
		/// Gets or sets the control placed in this cell. Getting returns the cell's control or <c>null</c>
		/// when the cell is empty (or styled-but-content-less). Setting to a control places/replaces it
		/// (keeping any per-cell styling); setting to <c>null</c> clears the content.
		/// </summary>
		public IWindowControl? Content
		{
			get => _grid.GetCellControl(_row, _col);
			set => _grid.SetCellContent(_row, _col, value);
		}

		/// <summary>
		/// Gets or sets this cell's background fill colour. <c>null</c> means no per-cell background (the
		/// cell shows through). Setting <c>null</c> via this property is a no-op (it leaves the current
		/// value unchanged); use <see cref="ResetStyle"/> to clear the cell's styling while keeping its
		/// content, or <see cref="Clear"/> to drop the cell's content entirely.
		/// </summary>
		public Color? Background
		{
			get => _grid.GetCellPlacement(_row, _col)?.Background;
			set => _grid.SetCellStyle(_row, _col, background: value);
		}

		/// <summary>
		/// Gets or sets this cell's border style. Defaults to <see cref="BorderStyle.None"/>. A non-None
		/// border draws a one-cell box around the cell and insets its content by one cell on every side.
		/// </summary>
		public BorderStyle Border
		{
			get => _grid.GetCellPlacement(_row, _col)?.Border ?? BorderStyle.None;
			set => _grid.SetCellStyle(_row, _col, border: value);
		}

		/// <summary>
		/// Gets or sets the padding that insets this cell's content from the cell edges (or from the inside
		/// of the border when <see cref="Border"/> is set). Defaults to <see cref="Padding.None"/>.
		/// </summary>
		public Padding Padding
		{
			get => _grid.GetCellPlacement(_row, _col)?.CellPadding ?? Padding.None;
			set => _grid.SetCellStyle(_row, _col, padding: value);
		}

		/// <summary>
		/// Gets the full <see cref="GridPlacement"/> of this cell (position, spans, and styling), or
		/// <c>null</c> when the cell has neither content nor styling.
		/// </summary>
		public GridPlacement? Placement => _grid.GetCellPlacement(_row, _col);

		/// <summary>
		/// Gets whether this cell currently holds no content. A styled-but-empty cell still reports
		/// <c>true</c> here (it has chrome but no control).
		/// </summary>
		public bool IsEmpty => _grid.GetCellControl(_row, _col) == null;

		/// <summary>
		/// Clears this cell's content. Equivalent to setting <see cref="Content"/> to <c>null</c>.
		/// </summary>
		public void Clear() => _grid.SetCellContent(_row, _col, null);

		/// <summary>
		/// Clears this cell's per-cell styling — background, border and padding — while keeping its
		/// content. This is the explicit way to remove styling, since setting <see cref="Background"/> to
		/// <c>null</c> is a no-op. When the cell is content-less (a styled empty cell), resetting leaves it
		/// with neither content nor style, so the cell is removed entirely.
		/// </summary>
		public void ResetStyle() => _grid.ResetCellStyle(_row, _col);
	}
}

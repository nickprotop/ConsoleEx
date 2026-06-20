// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;

namespace SharpConsoleUI.Layout;

/// <summary>
/// Describes where a single cell sits within a grid: its starting row and column
/// (both zero-based), how many rows and columns it spans, and its optional per-cell
/// styling (background fill, border, and content padding).
/// </summary>
/// <param name="Row">The zero-based row index of the cell's top-left corner.</param>
/// <param name="Col">The zero-based column index of the cell's top-left corner.</param>
/// <param name="RowSpan">The number of rows the cell occupies. Defaults to 1.</param>
/// <param name="ColSpan">The number of columns the cell occupies. Defaults to 1.</param>
public readonly record struct GridPlacement(int Row, int Col, int RowSpan = 1, int ColSpan = 1)
{
	/// <summary>
	/// Gets the per-cell background fill colour. When <c>null</c> (the default), the cell paints no
	/// background of its own and shows through to whatever is behind it (the grid background, or the
	/// content underneath the grid).
	/// </summary>
	public Color? Background { get; init; }

	private readonly BorderStyle? _border;

	/// <summary>
	/// Gets the per-cell border style. Defaults to <see cref="BorderStyle.None"/>. When set to anything
	/// other than <see cref="BorderStyle.None"/>, a one-cell-thick box is drawn around the cell and the
	/// cell's content is inset by one cell on every side so it sits inside the border.
	/// </summary>
	/// <remarks>
	/// Backed by a nullable field so the unset default is <see cref="BorderStyle.None"/> rather than the
	/// enum's zero value (<see cref="BorderStyle.DoubleLine"/>); a default-constructed placement therefore
	/// has no border.
	/// </remarks>
	public BorderStyle Border
	{
		get => _border ?? BorderStyle.None;
		init => _border = value;
	}

	/// <summary>
	/// Gets the padding, in cells, that insets the cell's content from the cell edges (or from the
	/// inside of the border when <see cref="Border"/> is set). Defaults to <see cref="Padding.None"/>.
	/// </summary>
	public Padding CellPadding { get; init; }
}

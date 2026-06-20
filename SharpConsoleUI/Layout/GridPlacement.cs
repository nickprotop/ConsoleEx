// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout;

/// <summary>
/// Describes where a single cell sits within a grid: its starting row and column
/// (both zero-based) and how many rows and columns it spans.
/// </summary>
/// <param name="Row">The zero-based row index of the cell's top-left corner.</param>
/// <param name="Col">The zero-based column index of the cell's top-left corner.</param>
/// <param name="RowSpan">The number of rows the cell occupies. Defaults to 1.</param>
/// <param name="ColSpan">The number of columns the cell occupies. Defaults to 1.</param>
public readonly record struct GridPlacement(int Row, int Col, int RowSpan = 1, int ColSpan = 1);

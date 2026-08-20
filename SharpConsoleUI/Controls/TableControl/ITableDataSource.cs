// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Sort direction for table columns.
/// </summary>
public enum SortDirection
{
	/// <summary>No sorting applied.</summary>
	None,
	/// <summary>Sort ascending (A-Z, 0-9).</summary>
	Ascending,
	/// <summary>Sort descending (Z-A, 9-0).</summary>
	Descending
}

/// <summary>
/// Filter operator for column-specific filtering.
/// </summary>
public enum FilterOperator
{
	/// <summary>Contains substring match.</summary>
	Contains,
	/// <summary>Greater than numeric comparison.</summary>
	GreaterThan,
	/// <summary>Less than numeric comparison.</summary>
	LessThan
}

/// <summary>
/// Interface for virtual/lazy data binding to a TableControl.
/// Enables large datasets (millions of rows) without memory overhead
/// by querying only visible rows on demand.
/// </summary>
public interface ITableDataSource : INotifyCollectionChanged
{
	/// <summary>
	/// Gets the total number of rows in the data source.
	/// </summary>
	int RowCount { get; }

	/// <summary>
	/// Gets the total number of columns in the data source.
	/// </summary>
	int ColumnCount { get; }

	/// <summary>
	/// Gets the header text for a column.
	/// </summary>
	string GetColumnHeader(int columnIndex);

	/// <summary>
	/// Gets the cell value at the specified row and column.
	/// </summary>
	string GetCellValue(int rowIndex, int columnIndex);

	/// <summary>
	/// Gets the text alignment for a column.
	/// </summary>
	TextJustification GetColumnAlignment(int columnIndex) => TextJustification.Left;

	/// <summary>
	/// Gets the fixed width for a column. Null means auto-width.
	/// </summary>
	int? GetColumnWidth(int columnIndex) => null;

	/// <summary>
	/// Gets a minimum width floor for a column when it is auto-width and gets shrunk to fit the
	/// available space. Null means no floor (the column can be shrunk down to 1 character). Ignored
	/// for columns with a fixed <see cref="GetColumnWidth"/>. When honoring this floor would require
	/// more space than is available, the table instead lets its total content width exceed the
	/// viewport so a horizontal scrollbar can be used to pan to it.
	/// </summary>
	int? GetColumnMinWidth(int columnIndex) => null;

	/// <summary>
	/// Gets the background color for a row. Null means use table default.
	/// </summary>
	Color? GetRowBackgroundColor(int rowIndex) => null;

	/// <summary>
	/// Gets the foreground color for a row. Null means use table default.
	/// </summary>
	Color? GetRowForegroundColor(int rowIndex) => null;

	/// <summary>
	/// Gets whether a row is enabled for interaction.
	/// </summary>
	bool IsRowEnabled(int rowIndex) => true;

	/// <summary>
	/// Gets an arbitrary tag object for a row.
	/// </summary>
	object? GetRowTag(int rowIndex) => null;

	/// <summary>
	/// Gets whether the data source supports sorting on the specified column.
	/// </summary>
	bool CanSort(int columnIndex) => false;

	/// <summary>
	/// Sorts the data source by the specified column and direction.
	/// </summary>
	void Sort(int columnIndex, SortDirection direction) { }

	/// <summary>
	/// Gets whether the data source supports server-side filtering.
	/// </summary>
	bool CanFilter => false;

	/// <summary>
	/// Applies a filter to the data source.
	/// </summary>
	void ApplyFilter(string filterText, string? columnName, FilterOperator op) { }

	/// <summary>
	/// Clears any active filter on the data source.
	/// </summary>
	void ClearFilter() { }
}

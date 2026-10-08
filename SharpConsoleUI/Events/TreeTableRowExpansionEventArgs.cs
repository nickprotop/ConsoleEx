// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Events;

/// <summary>
/// Event arguments for a row of a <see cref="TreeTableControl"/> that was expanded or collapsed.
/// </summary>
public class TreeTableRowExpansionEventArgs : EventArgs
{
	/// <summary>
	/// Creates new TreeTableRowExpansionEventArgs.
	/// </summary>
	/// <param name="row">The row expanded or collapsed.</param>
	/// <param name="isExpanded">Whether the row is open after the change.</param>
	/// <param name="isFilteredView">Whether the change is to the filtered view only.</param>
	public TreeTableRowExpansionEventArgs(TreeTableRow row, bool isExpanded, bool isFilteredView)
	{
		Row = row;
		IsExpanded = isExpanded;
		IsFilteredView = isFilteredView;
	}

	/// <summary>
	/// The row expanded or collapsed.
	/// </summary>
	public TreeTableRow Row { get; }

	/// <summary>
	/// Whether the row is open after the change.
	/// </summary>
	public bool IsExpanded { get; }

	/// <summary>
	/// Whether the change is to the filtered view only. While a filter is applied, opening and closing
	/// a row changes what the filter shows and leaves <see cref="TreeTableRow.IsExpanded"/> as it was,
	/// so clearing the filter brings the hierarchy back unchanged.
	/// </summary>
	public bool IsFilteredView { get; }
}

/// <summary>
/// Event arguments for a row of a <see cref="TreeTableControl"/> about to be expanded or collapsed.
/// </summary>
public class TreeTableRowExpansionChangingEventArgs : TreeTableRowExpansionEventArgs
{
	/// <summary>
	/// Creates new TreeTableRowExpansionChangingEventArgs.
	/// </summary>
	/// <param name="row">The row about to be expanded or collapsed.</param>
	/// <param name="isExpanded">Whether the row is to be open after the change.</param>
	/// <param name="isFilteredView">Whether the change is to the filtered view only.</param>
	public TreeTableRowExpansionChangingEventArgs(TreeTableRow row, bool isExpanded, bool isFilteredView)
		: base(row, isExpanded, isFilteredView)
	{
	}

	/// <summary>
	/// Set to true to cancel the change.
	/// </summary>
	public bool Cancel { get; set; }
}

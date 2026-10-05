// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// What a <see cref="TableControl"/> wants displayed: the filter and the sort it asks
/// <see cref="TableControl.ComputeDisplayRows"/> to apply.
/// </summary>
/// <remarks>
/// <para>
/// A struct rather than parameters, for the same reason <see cref="TableFilterResult"/> is one:
/// it is the growth seam. The table can tell an override more — a second sort key, say — by adding
/// a property, without changing the signature of a method subclasses have already overridden.
/// </para>
/// <para>
/// The filter mode is included because a derived table may want to show a filter differently while
/// it is still being typed, for instance expanding less eagerly than for a confirmed one.
/// </para>
/// </remarks>
public readonly struct TableDisplayQuery
{
	/// <summary>Creates a query.</summary>
	/// <param name="filter">The active filter, or null.</param>
	/// <param name="filterMode">Whether the filter is being typed or has been confirmed.</param>
	/// <param name="sortColumnIndex">The column to sort by, or -1.</param>
	/// <param name="sortDirection">The direction to sort in.</param>
	public TableDisplayQuery(CompoundFilterExpression? filter, FilterMode filterMode, int sortColumnIndex, SortDirection sortDirection)
	{
		Filter = filter;
		FilterMode = filterMode;
		SortColumnIndex = sortColumnIndex;
		SortDirection = sortDirection;
	}

	/// <summary>The active filter, or null when nothing is filtered.</summary>
	public CompoundFilterExpression? Filter { get; }

	/// <summary>Whether the filter is being typed or has been confirmed.</summary>
	public FilterMode FilterMode { get; }

	/// <summary>The column to sort by, or -1 when unsorted.</summary>
	public int SortColumnIndex { get; }

	/// <summary>The direction to sort in.</summary>
	public SortDirection SortDirection { get; }

	/// <summary>True when there is a filter to apply.</summary>
	public bool IsFiltered => Filter != null;

	/// <summary>True when there is a sort to apply: a column and a direction other than none.</summary>
	public bool IsSorted => SortDirection != SortDirection.None && SortColumnIndex >= 0;
}

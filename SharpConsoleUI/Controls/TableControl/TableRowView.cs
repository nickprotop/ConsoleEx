// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// Which data rows a <see cref="TableControl"/> displays, and in what order: the maps from a
/// display position to a data row, and back.
/// </summary>
/// <remarks>
/// <para>
/// The maps used to be loose fields on the control, assigned from fourteen places. Holding them
/// here means the rules that keep them consistent — above all that a map supplied by the data
/// source is flagged as such — are enforced by the type rather than remembered at every call site.
/// </para>
/// <para>
/// Two maps exist today: a sort map over every row, and a filter map that may also be sorted. A
/// filter map wins whenever one is present.
/// </para>
/// </remarks>
internal sealed class TableRowView
{
	/// <summary>
	/// Maps display index to data index while sorted without a filter, or null for data order.
	/// </summary>
	internal int[]? SortMap { get; private set; }

	/// <summary>
	/// Maps display index to data index while a filter is active, or null when rows are addressed
	/// by identity — either unfiltered, or filtered by a source that narrowed itself.
	/// </summary>
	internal int[]? FilterMap { get; private set; }

	/// <summary>
	/// True when <see cref="FilterMap"/> came from the data source rather than a client-side scan.
	/// The table cannot rebuild such a map: it encodes what the SOURCE decided to show, which is the
	/// point of <see cref="TableFilterOutcome.DisplayRowsSupplied"/>. Sorting therefore re-asks the
	/// source instead of rescanning, exactly as it already does for a source that narrowed itself.
	/// </summary>
	/// <remarks>
	/// Set only by <see cref="SetSourceFilterMap"/> and cleared by every other assignment of the
	/// filter map, so the flag cannot drift out of step with the map it describes. Any assignment
	/// forgetting to clear it would leave the table believing a client-side map came from the
	/// source, and silently skipping the rebuild that keeps sorting correct.
	/// </remarks>
	internal bool FilterMapFromSource { get; private set; }

	/// <summary>Installs or clears the sort map.</summary>
	internal void SetSortMap(int[]? map) => SortMap = map;

	/// <summary>Installs or clears a filter map the table built itself.</summary>
	internal void SetFilterMap(int[]? map)
	{
		FilterMap = map;
		FilterMapFromSource = false;
	}

	/// <summary>Installs a display map supplied by the data source, marking it as such.</summary>
	internal void SetSourceFilterMap(int[] map)
	{
		FilterMap = map;
		FilterMapFromSource = true;
	}

	/// <summary>
	/// Maps a display row index to the actual data row index, accounting for sorting.
	/// </summary>
	internal int MapDisplayToData(int displayIndex)
	{
		if (FilterMap != null && displayIndex >= 0 && displayIndex < FilterMap.Length)
			return FilterMap[displayIndex];
		if (SortMap != null && displayIndex >= 0 && displayIndex < SortMap.Length)
			return SortMap[displayIndex];
		return displayIndex;
	}

	/// <summary>
	/// Maps a data row index to the display row index, accounting for filtering and sorting.
	/// </summary>
	internal int MapDataToDisplay(int dataIndex)
	{
		if (FilterMap != null)
		{
			for (int i = 0; i < FilterMap.Length; i++)
			{
				if (FilterMap[i] == dataIndex) return i;
			}
			return dataIndex;
		}
		if (SortMap == null) return dataIndex;
		for (int i = 0; i < SortMap.Length; i++)
		{
			if (SortMap[i] == dataIndex) return i;
		}
		return dataIndex;
	}
}

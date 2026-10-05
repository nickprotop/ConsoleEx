// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// Which data rows a <see cref="TableControl"/> displays, and in what order: the map from a display
/// position to a data row, and back.
/// </summary>
/// <remarks>
/// <para>
/// ONE MAP. There used to be two, a sort map over every row and a filter map that might also be
/// sorted, with the filter map winning whenever it existed. Each could go stale while the other was
/// in use: sorting under a filter rebuilt only the filter map, so backspacing the filter away fell
/// back to a sort map from before, and the rows showed one order under the header's indicator for
/// another. A single map, rebuilt from the table's whole state, cannot disagree with itself.
/// </para>
/// <para>
/// The map records where it came from, because the table treats the two origins differently. A map
/// the table computed can be recomputed at any time. A map the data source supplied — or the
/// source's own narrowing, which leaves no map at all — encodes what the SOURCE decided to show, and
/// must never be rebuilt from a client-side scan. Every change goes through
/// <see cref="SetComputed"/>, <see cref="SetFromSource"/> or <see cref="Clear"/>, so the origin
/// cannot drift out of step with the map it describes.
/// </para>
/// </remarks>
internal sealed class TableRowView
{
	/// <summary>
	/// Maps display index to data index, or null when rows are addressed by identity: unsorted and
	/// unfiltered, or filtered by a source that narrowed itself.
	/// </summary>
	internal int[]? Map { get; private set; }

	/// <summary>
	/// True when the data source decided what shows: it either supplied <see cref="Map"/>, or
	/// narrowed itself and left the map null. The table must not rebuild either from its own scan.
	/// </summary>
	internal bool FromSource { get; private set; }

	/// <summary>True when the table computed <see cref="Map"/> with a filter applied.</summary>
	internal bool BuiltWithFilter { get; private set; }

	/// <summary>
	/// True when the map narrows the rows to a filter's matches, whoever computed it.
	/// </summary>
	/// <remarks>
	/// NOT the same as "a map exists". A sort map, or any other map a table computes without a
	/// filter, holds every row; mistaking one for a filter would, among other things, stop the
	/// table recording its unfiltered row count when filtering starts.
	/// </remarks>
	internal bool IsFilterMapActive => Map != null && (FromSource || BuiltWithFilter);

	/// <summary>True when the map is a filter's matches that the TABLE computed and can recompute.</summary>
	internal bool HasClientFilterMap => Map != null && BuiltWithFilter;

	/// <summary>Installs a map the table computed, or null for data order.</summary>
	/// <param name="map">The display map, or null.</param>
	/// <param name="builtWithFilter">Whether a filter decided which rows the map holds.</param>
	internal void SetComputed(int[]? map, bool builtWithFilter)
	{
		Map = map;
		FromSource = false;
		BuiltWithFilter = builtWithFilter && map != null;
	}

	/// <summary>
	/// Records the data source's answer to a filter: the display rows it supplied, or null when it
	/// narrowed itself.
	/// </summary>
	internal void SetFromSource(int[]? map)
	{
		Map = map;
		FromSource = true;
		BuiltWithFilter = false;
	}

	/// <summary>Returns to data order with no filter.</summary>
	internal void Clear() => SetComputed(null, builtWithFilter: false);

	/// <summary>
	/// Maps a display row index to the actual data row index, accounting for sorting and filtering.
	/// </summary>
	internal int MapDisplayToData(int displayIndex)
	{
		if (Map != null && displayIndex >= 0 && displayIndex < Map.Length)
			return Map[displayIndex];
		return displayIndex;
	}

	/// <summary>
	/// Maps a data row index to the display row index, accounting for filtering and sorting.
	/// </summary>
	internal int MapDataToDisplay(int dataIndex)
	{
		if (Map == null) return dataIndex;
		for (int i = 0; i < Map.Length; i++)
		{
			if (Map[i] == dataIndex) return i;
		}
		return dataIndex;
	}
}

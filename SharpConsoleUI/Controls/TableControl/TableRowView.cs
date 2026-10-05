// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;

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
	private InverseMap? _inverse;

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
		ForgetComputed();
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
		ForgetComputed();
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
	/// Maps a data row index to the display row index, accounting for filtering and sorting, or -1
	/// when the map does not display that row.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A hidden row used to map to its own data index, which is a display position belonging to some
	/// other row: anything locating a row through this landed on the wrong one instead of learning
	/// that the row is not shown.
	/// </para>
	/// <para>
	/// O(1) through an inverse map, built on first use after the map changes, where this used to scan
	/// the map on every call. Restoring a multi-selection after every change asks once per selected
	/// row. The inverse is sized by the largest index in the map, since every data row past it is
	/// hidden anyway; a data index the table does not have maps to -1 the same way.
	/// </para>
	/// </remarks>
	internal int MapDataToDisplay(int dataIndex)
	{
		var map = Map;
		if (map == null) return dataIndex;

		// Keyed by the map it inverts, so a map swapped in by another thread mid-call cannot be read
		// through the previous map's inverse.
		var cached = _inverse;
		if (cached == null || !ReferenceEquals(cached.Map, map))
			_inverse = cached = new InverseMap(map, BuildInverse(map));

		var inverse = cached.Inverse;
		return dataIndex >= 0 && dataIndex < inverse.Length ? inverse[dataIndex] : -1;
	}

	private static int[] BuildInverse(int[] map)
	{
		int length = 0;
		foreach (int dataIndex in map)
			length = Math.Max(length, dataIndex + 1);

		var inverse = new int[length];
		Array.Fill(inverse, -1);
		for (int display = 0; display < map.Length; display++)
			inverse[map[display]] = display;
		return inverse;
	}

	/// <summary>A data-to-display inverse, together with the map it was built from.</summary>
	private sealed record InverseMap(int[] Map, int[] Inverse);

	#region Incremental upkeep

	// WHY THIS EXISTS. Keeping the sort and filter when rows change means recomputing the display
	// rows on every change. Recomputed from scratch, a sorted table refilled row by row — ClearRows,
	// then AddRow in a loop, the most common way to load one — costs O(n log n) per row and
	// O(n² log n) in all: about a minute at ten thousand rows. So the table records each change
	// here, and when exactly one insert or removal separates two computes under the same filter and
	// sort, the last result is updated instead: a removal drops and shifts indices, an insert shifts
	// them and places each new matching row by binary search, which costs O(log n) comparisons.
	// Anything else — a replaced row set, an edited cell, a changed column — is a reset and gets a
	// full compute.

	private RowChange _pendingChange;
	private int _pendingChangeCount;
	private ComputedRows? _lastComputed;

	// The text each data row's sort column displays, markup stripped, kept across computes so an
	// insert strips only the rows its binary search visits. Follows inserts and removals like the
	// result does, and is dropped on a reset.
	private List<string?>? _sortText;
	private int _sortTextColumn = -1;

	/// <summary>Records that <paramref name="count"/> rows were inserted at data index <paramref name="index"/>.</summary>
	internal void RecordInsert(int index, int count)
	{
		Record(new RowChange(RowChangeKind.Insert, index, count));

		if (_sortText != null && index <= _sortText.Count)
			_sortText.InsertRange(index, new string?[count]);
		else
			_sortText = null;
	}

	/// <summary>Records that <paramref name="count"/> rows were removed at data index <paramref name="index"/>.</summary>
	internal void RecordRemove(int index, int count)
	{
		Record(new RowChange(RowChangeKind.Remove, index, count));

		if (_sortText != null && index + count <= _sortText.Count)
			_sortText.RemoveRange(index, count);
		else
			_sortText = null;
	}

	/// <summary>
	/// Records a change the last result cannot be updated across: the rows replaced, a cell edited,
	/// a column changed.
	/// </summary>
	internal void RecordReset()
	{
		Record(new RowChange(RowChangeKind.Reset, 0, 0));
		_sortText = null;
	}

	private void Record(RowChange change)
	{
		_pendingChange = change;
		_pendingChangeCount++;
	}

	/// <summary>
	/// Updates the last computed display rows across the one change recorded since, when that is
	/// possible and cheaper than computing them again.
	/// </summary>
	/// <param name="key">What the rows are computed from; the last result is only reused under the same key.</param>
	/// <param name="matches">Whether a data row passes the filter, or null when there is none.</param>
	/// <param name="order">The total order of the display rows, ties already broken.</param>
	/// <param name="rows">The updated display rows.</param>
	/// <returns>False when the rows have to be computed in full.</returns>
	internal bool TryUpdateComputed(in DisplayRowsKey key, Func<int, bool>? matches, Comparison<int> order, out int[] rows)
	{
		rows = Array.Empty<int>();
		var last = _lastComputed;
		if (last == null || _pendingChangeCount != 1 || !last.Key.Equals(key))
			return false;

		var change = _pendingChange;
		switch (change.Kind)
		{
			case RowChangeKind.Insert when change.Count <= ControlDefaults.TableIncrementalUpdateMaxRows:
				rows = InsertRows(last.Rows, change.Index, change.Count, matches, order);
				return true;

			case RowChangeKind.Remove:
				rows = RemoveRows(last.Rows, change.Index, change.Count);
				return true;

			default:
				return false;
		}
	}

	/// <summary>Remembers display rows just computed, so the next change can update them.</summary>
	internal void RememberComputed(in DisplayRowsKey key, int[] rows) => _lastComputed = new ComputedRows(key, rows);

	/// <summary>
	/// The markup-stripped text cache for a sort column, one entry per data row, filled lazily by
	/// the comparison.
	/// </summary>
	internal List<string?> GetSortTextCache(int column, int dataRowCount)
	{
		if (_sortText == null || _sortTextColumn != column || _sortText.Count != dataRowCount)
		{
			_sortText = new List<string?>(new string?[dataRowCount]);
			_sortTextColumn = column;
		}
		return _sortText;
	}

	private void ForgetComputed()
	{
		_lastComputed = null;
		_pendingChangeCount = 0;
	}

	private static int[] InsertRows(int[] previous, int index, int count, Func<int, bool>? matches, Comparison<int> order)
	{
		var rows = new List<int>(previous.Length + count);
		foreach (int dataIndex in previous)
			rows.Add(dataIndex >= index ? dataIndex + count : dataIndex);

		for (int dataIndex = index; dataIndex < index + count; dataIndex++)
		{
			if (matches != null && !matches(dataIndex)) continue;
			rows.Insert(FindInsertPosition(rows, dataIndex, order), dataIndex);
		}

		return rows.ToArray();
	}

	private static int FindInsertPosition(List<int> rows, int dataIndex, Comparison<int> order)
	{
		int low = 0;
		int high = rows.Count;
		while (low < high)
		{
			int middle = low + ((high - low) >> 1);
			if (order(rows[middle], dataIndex) < 0)
				low = middle + 1;
			else
				high = middle;
		}
		return low;
	}

	private static int[] RemoveRows(int[] previous, int index, int count)
	{
		var rows = new List<int>(previous.Length);
		foreach (int dataIndex in previous)
		{
			if (dataIndex < index)
				rows.Add(dataIndex);
			else if (dataIndex >= index + count)
				rows.Add(dataIndex - count);
		}
		return rows.ToArray();
	}

	private enum RowChangeKind { Insert, Remove, Reset }

	private readonly record struct RowChange(RowChangeKind Kind, int Index, int Count);

	private sealed record ComputedRows(DisplayRowsKey Key, int[] Rows);

	#endregion
}

/// <summary>
/// What a table's default display rows are computed from. Two computes under equal keys differ only
/// by the rows that changed between them, which is what lets <see cref="TableRowView"/> update the
/// last result instead of recomputing it.
/// </summary>
/// <param name="Filter">The active filter, compared by reference: a re-typed filter is a new filter.</param>
/// <param name="SortColumn">The sort column, or -1.</param>
/// <param name="Direction">The sort direction.</param>
/// <param name="RowComparer">The sort column's row comparer, if any.</param>
/// <param name="Comparer">The sort column's cell comparer, if any.</param>
/// <param name="FuzzyFilter">Whether fuzzy matching was on.</param>
internal readonly record struct DisplayRowsKey(
	object? Filter,
	int SortColumn,
	SortDirection Direction,
	object? RowComparer,
	object? Comparer,
	bool FuzzyFilter);

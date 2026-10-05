// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Filter mode for the table's inline filter.
/// </summary>
public enum FilterMode
{
	/// <summary>No filter active.</summary>
	None,
	/// <summary>User is typing a filter expression.</summary>
	Typing,
	/// <summary>Filter has been confirmed and is active.</summary>
	Confirmed
}

/// <summary>
/// Represents a parsed filter expression.
/// </summary>
public class FilterExpression
{
	/// <summary>The raw input text.</summary>
	public string RawText { get; init; } = string.Empty;
	/// <summary>Target column name, or null for all columns.</summary>
	public string? ColumnName { get; init; }
	/// <summary>The filter value to match against.</summary>
	public string Value { get; init; } = string.Empty;
	/// <summary>The comparison operator.</summary>
	public FilterOperator Operator { get; init; } = FilterOperator.Contains;
}

/// <summary>
/// A filter term: one or more alternative expressions combined with OR.
/// </summary>
public class FilterTerm
{
	/// <summary>Alternative filter expressions (OR relationship — any must match).</summary>
	public List<FilterExpression> Alternatives { get; init; } = new();
}

/// <summary>
/// A compound filter expression: one or more terms combined with AND.
/// Space-separated terms are AND; pipe-separated alternatives within a term are OR.
/// </summary>
public class CompoundFilterExpression
{
	/// <summary>The original raw input text.</summary>
	public string RawText { get; init; } = string.Empty;
	/// <summary>Filter terms (AND relationship — all must match).</summary>
	public List<FilterTerm> Terms { get; init; } = new();
}

public partial class TableControl
{
	#region Filter Properties

	/// <summary>
	/// Gets or sets whether filtering is enabled. Press '/' to enter filter mode.
	/// </summary>
	public bool FilteringEnabled
	{
		get => _filteringEnabled;
		set
		{
			_filteringEnabled = value;
			OnPropertyChanged();
			if (!value) ClearFilter();
			Invalidate(Invalidation.Relayout);
		}
	}

	/// <summary>
	/// Gets or sets whether fuzzy (character-subsequence) matching is enabled as a fallback.
	/// </summary>
	public bool FuzzyFilterEnabled
	{
		get => _fuzzyFilterEnabled;
		set { _fuzzyFilterEnabled = value; OnPropertyChanged(); }
	}

	/// <summary>
	/// Gets whether a filter is currently active (typing or confirmed).
	/// </summary>
	public bool IsFiltering => _filterMode != FilterMode.None;

	/// <summary>
	/// Gets the active filter text, or null if no filter is active.
	/// </summary>
	public string? ActiveFilterText => _activeFilter?.RawText;

	/// <summary>
	/// Gets the current filter mode.
	/// </summary>
	public FilterMode CurrentFilterMode => _filterMode;

	#endregion

	#region Filter Events

	/// <summary>
	/// Occurs when a filter is applied (confirmed).
	/// </summary>
	public event EventHandler<string>? FilterApplied;

	/// <summary>Async counterpart of <see cref="FilterApplied"/>.</summary>
	public event Core.AsyncEventHandler<string>? FilterAppliedAsync;

	/// <summary>
	/// Occurs when the filter is cleared.
	/// </summary>
	public event EventHandler? FilterCleared;

	/// <summary>Async counterpart of <see cref="FilterCleared"/>.</summary>
	public event Core.AsyncEventHandler<EventArgs>? FilterClearedAsync;

	/// <summary>
	/// Occurs when the filter text changes during typing.
	/// </summary>
	public event EventHandler<string>? FilterTextChanged;

	#endregion

	#region Programmatic Filter API

	/// <summary>
	/// Applies a filter programmatically. Sets mode to Confirmed.
	/// </summary>
	public void ApplyFilter(string filterText)
	{
		if (string.IsNullOrEmpty(filterText))
		{
			ClearFilter();
			return;
		}

		var compound = ParseCompoundFilterExpression(filterText);
		if (compound == null)
		{
			ClearFilter();
			return;
		}

		ApplyFilter(compound);
	}

	/// <summary>
	/// Applies a pre-built filter expression. Wraps it into a compound expression.
	/// </summary>
	public void ApplyFilter(FilterExpression expression)
	{
		var compound = new CompoundFilterExpression
		{
			RawText = expression.RawText,
			Terms = new List<FilterTerm>
			{
				new FilterTerm { Alternatives = new List<FilterExpression> { expression } }
			}
		};
		ApplyFilter(compound);
	}

	/// <summary>
	/// Applies a compound filter expression. Sets mode to Confirmed.
	/// </summary>
	public void ApplyFilter(CompoundFilterExpression compound)
	{
		_activeFilter = compound;
		_filterBuffer = compound.RawText;
		_filterMode = FilterMode.Confirmed;

		// Capture the total BEFORE anything narrows, delegated or not. A source that filters itself
		// changes its own RowCount, so asking afterwards reports the filtered set as the total and
		// the footer reads "1/1 rows". EnterFilterMode captures this too, but only the interactive
		// path goes through it — calling ApplyFilter directly must work the same way.
		if (!_rowView.IsFilterMapActive && _unfilteredRowCount == 0)
		{
			if (_dataSource != null)
				_unfilteredRowCount = _dataSource.RowCount;
			else
				lock (_tableLock) { _unfilteredRowCount = _rows.Count; }
		}

		// PUSH THE FILTER DOWN when the source can do it itself. Client-side filtering walks every
		// row calling GetCellValue per column, which pulls a virtualized source fully into memory —
		// the opposite of what ITableDataSource exists for. A source advertising CanFilter narrows
		// itself instead, so RowCount already reflects the filtered set and the display map stays
		// null (identity mapping).
		if (TryDelegateFilter(compound))
			return;

		RecomputeDisplayMap();

		_selectedRowIndex = RowCount > 0 ? 0 : -1;
		_scrollOffset = 0;
		_selectedRowIndices.Clear();

		EndRowGestures();
		Core.AsyncEvent.Raise(FilterApplied, FilterAppliedAsync, this, compound.RawText, Container?.GetConsoleWindowSystem?.LogService);
		InvalidateColumnWidths();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Offers a filter to the data source, which answers whether and how it applied it.
	/// </summary>
	/// <param name="dataSource">The source to offer the filter to. Never null.</param>
	/// <param name="compound">The parsed filter, which may hold several AND/OR terms.</param>
	/// <returns>
	/// <see cref="TableFilterResult.NotHandled"/> to filter client-side instead, or one of the
	/// handled outcomes. See <see cref="TableFilterResult"/> for what each one commits the source to.
	/// </returns>
	/// <remarks>
	/// <para>
	/// WHY THIS IS THE OVERRIDABLE PART. <see cref="ITableDataSource.ApplyFilter"/> takes a single
	/// (text, column, operator) triple, so only a filter reducing to ONE expression fits through it.
	/// The default therefore keeps every compound AND/OR filter client-side: pushing one down a
	/// single-expression hook would silently drop all but the first condition, which is worse than
	/// being slow.
	/// </para>
	/// <para>
	/// That restriction is the interface's, not the source's. A source that CAN evaluate the whole
	/// expression — a hierarchy that must keep a matching row's parent visible, a remote source that
	/// would rather not be pulled into memory — overrides this to receive the
	/// <see cref="CompoundFilterExpression"/> whole. Everything after the hand-off (display map,
	/// selection, scroll, the <see cref="FilterApplied"/> event) stays with the caller, so an
	/// override cannot forget it.
	/// </para>
	/// </remarks>
	protected virtual TableFilterResult TryApplyFilterToDataSource(
		ITableDataSource dataSource, CompoundFilterExpression compound)
	{
		if (!dataSource.CanFilter)
			return TableFilterResult.NotHandled;

		if (compound.Terms.Count != 1 || compound.Terms[0].Alternatives.Count != 1)
			return TableFilterResult.NotHandled;

		var expression = compound.Terms[0].Alternatives[0];
		dataSource.ApplyFilter(expression.Value, expression.ColumnName, expression.Operator);
		return TableFilterResult.SourceNarrowed;
	}

	/// <summary>
	/// Hands the filter to the data source when it can apply one itself, returning true if it did,
	/// and owns all the bookkeeping that has to follow either way.
	/// </summary>
	private bool TryDelegateFilter(CompoundFilterExpression compound)
	{
		if (_dataSource == null)
			return false;

		var result = TryApplyFilterToDataSource(_dataSource, compound);
		if (result.Outcome == TableFilterOutcome.NotHandled)
			return false;

		if (result.Outcome == TableFilterOutcome.DisplayRowsSupplied)
		{
			// The source keeps reporting every row and has named the ones to show, so the table
			// addresses rows through its map. Flagged as the source's so a later sort re-asks the
			// source instead of rebuilding the map from a client-side scan, which would throw away
			// the very knowledge the source overrode this to supply.
			_rowView.SetFromSource(BuildSourceDisplayMap(result.DisplayRows!));
		}
		else
		{
			// The source now reports only matching rows, so no display map is needed: RowCount reads
			// through to it and MapDisplayToData stays identity. Still recorded as the source's
			// answer, so nothing rebuilds a map over the narrowed rows from a client-side scan.
			_rowView.SetFromSource(null);
		}

		// _unfilteredRowCount is deliberately NOT reset here. It was captured before the source
		// narrowed and is the only remaining record of the pre-filter total — the source itself can
		// no longer report it. Clearing it made the footer read "1/1 rows" instead of "1/4 rows".
		// ClearFilter zeroes it when the filter actually goes away.

		_selectedRowIndex = RowCount > 0 ? 0 : -1;
		_scrollOffset = 0;
		_selectedRowIndices.Clear();

		EndRowGestures();
		Core.AsyncEvent.Raise(FilterApplied, FilterAppliedAsync, this, compound.RawText, Container?.GetConsoleWindowSystem?.LogService);
		InvalidateColumnWidths();
		Invalidate(Invalidation.Relayout);
		return true;
	}

	/// <summary>
	/// Copies the source's display rows into the array the table indexes by.
	/// </summary>
	/// <remarks>
	/// COPIED, not aliased, although the result doc says the list is held as given: the table
	/// stores an <c>int[]</c> and would otherwise re-index an <c>IReadOnlyList</c> on every cell
	/// read, which paint does per visible row per column.
	/// <para>
	/// Indices are validated in debug only. An out-of-range index would otherwise surface deep in
	/// paint, far from the override that produced it, and the assert names the real culprit; in
	/// release the hot path trusts the caller rather than re-checking every row.
	/// </para>
	/// </remarks>
	private int[] BuildSourceDisplayMap(IReadOnlyList<int> dataIndices)
	{
		var map = new int[dataIndices.Count];
		for (int i = 0; i < map.Length; i++)
			map[i] = dataIndices[i];

#if DEBUG
		int rowCount = _dataSource?.RowCount ?? 0;
		foreach (int index in map)
		{
			System.Diagnostics.Debug.Assert(index >= 0 && index < rowCount,
				$"TryApplyFilterToDataSource returned display row {index}, outside the source's 0..{rowCount - 1}.");
		}
#endif

		return map;
	}

	/// <summary>
	/// Filters by a specific column programmatically.
	/// </summary>
	public void FilterByColumn(int columnIndex, string value, FilterOperator op = FilterOperator.Contains)
	{
		string? columnName = null;
		if (_dataSource != null)
		{
			if (columnIndex >= 0 && columnIndex < _dataSource.ColumnCount)
				columnName = _dataSource.GetColumnHeader(columnIndex);
		}
		else
		{
			lock (_tableLock)
			{
				if (columnIndex >= 0 && columnIndex < _columns.Count)
					columnName = _columns[columnIndex].Header;
			}
		}

		if (columnName == null) return;

		var expression = new FilterExpression
		{
			RawText = $"{columnName}{(op == FilterOperator.GreaterThan ? ">" : op == FilterOperator.LessThan ? "<" : ":")}{value}",
			ColumnName = columnName,
			Value = value,
			Operator = op
		};

		// Wrap into compound and apply
		ApplyFilter(expression);
	}

	/// <summary>
	/// Clears any active filter, restoring all rows.
	/// </summary>
	public void ClearFilter()
	{
		// A DELEGATED filter may leave the filter map null (the source narrowed itself), so the
		// guard below cannot tell "no filter" from "filtered server-side" on its own — ask the
		// source to drop its filter first, then fall through to reset local state. A source that
		// supplied display rows instead leaves a map behind, and the same reset clears it.
		bool delegated = _dataSource != null && _dataSource.CanFilter && _activeFilter != null;
		if (delegated)
			_dataSource!.ClearFilter();

		if (!delegated && _filterMode == FilterMode.None && !_rowView.IsFilterMapActive) return;

		// Unlike applying a filter, which starts at its first match, clearing one keeps the row the
		// user was on: every row is shown again, so it is still there to keep.
		var selection = CaptureSelection();

		_filterMode = FilterMode.None;
		_filterBuffer = string.Empty;
		_filterCursorPosition = 0;
		_activeFilter = null;
		_unfilteredRowCount = 0;

		// A data source sorts itself and is read by identity again; the table's own rows get the
		// map rebuilt from the sort as it is NOW, which may have changed while the filter was on.
		if (_dataSource != null)
		{
			_rowView.Clear();
			if (_sortDirection != SortDirection.None)
				SortSource();
		}
		else
		{
			RebuildDisplayMap();
		}

		RestoreSelection(selection);
		Core.AsyncEvent.Raise(FilterCleared, FilterClearedAsync, this, EventArgs.Empty, Container?.GetConsoleWindowSystem?.LogService);
		InvalidateColumnWidths();
		Invalidate(Invalidation.Relayout);
	}

	#endregion

	#region Interactive Filter Methods

	/// <summary>
	/// Enters filter typing mode. Called when '/' is pressed.
	/// </summary>
	internal void EnterFilterMode()
	{
		if (!_filteringEnabled || _readOnly) return;

		_filterMode = FilterMode.Typing;
		_filterBuffer = string.Empty;
		_filterCursorPosition = 0;
		_activeFilter = null;

		// Store unfiltered count
		if (!_rowView.IsFilterMapActive)
		{
			if (_dataSource != null)
				_unfilteredRowCount = _dataSource.RowCount;
			else
				lock (_tableLock) { _unfilteredRowCount = _rows.Count; }
		}

		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Processes a key while in filter typing mode.
	/// </summary>
	internal bool ProcessFilterKey(ConsoleKeyInfo key)
	{
		switch (key.Key)
		{
			case ConsoleKey.Enter:
				if (!string.IsNullOrEmpty(_filterBuffer))
				{
					_filterMode = FilterMode.Confirmed;
					Core.AsyncEvent.Raise(FilterApplied, FilterAppliedAsync, this, _filterBuffer, Container?.GetConsoleWindowSystem?.LogService);
				}
				else
				{
					ClearFilter();
				}
				Invalidate(Invalidation.Relayout);
				return true;

			case ConsoleKey.Escape:
				ClearFilter();
				return true;

			case ConsoleKey.Backspace:
				if (_filterCursorPosition > 0)
				{
					_filterBuffer = _filterBuffer.Remove(_filterCursorPosition - 1, 1);
					_filterCursorPosition--;
					ApplyFilterLive();
				}
				else if (_filterBuffer.Length == 0)
				{
					ClearFilter();
				}
				return true;

			case ConsoleKey.Delete:
				if (_filterCursorPosition < _filterBuffer.Length)
				{
					_filterBuffer = _filterBuffer.Remove(_filterCursorPosition, 1);
					ApplyFilterLive();
				}
				return true;

			case ConsoleKey.LeftArrow:
				if (_filterCursorPosition > 0)
				{
					_filterCursorPosition--;
					Invalidate(Invalidation.Repaint);
				}
				return true;

			case ConsoleKey.RightArrow:
				if (_filterCursorPosition < _filterBuffer.Length)
				{
					_filterCursorPosition++;
					Invalidate(Invalidation.Repaint);
				}
				return true;

			case ConsoleKey.Home:
				_filterCursorPosition = 0;
				Invalidate(Invalidation.Repaint);
				return true;

			case ConsoleKey.End:
				_filterCursorPosition = _filterBuffer.Length;
				Invalidate(Invalidation.Repaint);
				return true;

			default:
				if (!char.IsControl(key.KeyChar))
				{
					_filterBuffer = _filterBuffer.Insert(_filterCursorPosition, key.KeyChar.ToString());
					_filterCursorPosition++;
					ApplyFilterLive();
					return true;
				}
				return true; // Consume all keys in filter mode
		}
	}

	/// <summary>
	/// Applies the current filter buffer as a live filter.
	/// </summary>
	internal void ApplyFilterLive()
	{
		if (string.IsNullOrEmpty(_filterBuffer))
		{
			// THE SOURCE HAS TO BE TOLD TOO. Backspacing the last character away ends the filter
			// just as Esc does, and a source that narrowed itself keeps reporting only the matches
			// until asked to stop — leaving the table showing a filtered set while believing
			// nothing is filtered, with no keystroke that puts the rows back.
			if (_dataSource != null && _dataSource.CanFilter && _activeFilter != null)
				_dataSource.ClearFilter();

			// The table's own rows keep the selected row, as Esc does; a data source, having no rows
			// to follow, starts again from its first row.
			var selection = CaptureSelection();
			_activeFilter = null;
			ShowUnfilteredRows();
			if (selection.IsTracked)
			{
				RestoreSelection(selection);
			}
			else
			{
				_selectedRowIndex = RowCount > 0 ? 0 : -1;
				_scrollOffset = 0;
			}
		}
		else
		{
			var compound = ParseCompoundFilterExpression(_filterBuffer);
			_activeFilter = compound;

			// Live typing goes through the same seam as the programmatic API, so a source that
			// filters itself is not scanned row-by-row on every keystroke. TryDelegateFilter already
			// resets selection and scroll, so only the client-side branch needs to do it here —
			// but FilterTextChanged below must still fire either way.
			if (compound == null || !TryDelegateFilter(compound))
			{
				if (compound != null)
					RecomputeDisplayMap();
				else
					ShowUnfilteredRows();

				_selectedRowIndex = RowCount > 0 ? 0 : -1;
				_scrollOffset = 0;
			}
		}

		_selectedRowIndices.Clear();
		EndRowGestures();
		FilterTextChanged?.Invoke(this, _filterBuffer);
		InvalidateColumnWidths();
		Invalidate(Invalidation.Relayout);
	}

	#endregion

	#region Filter Expression Parsing

	/// <summary>
	/// Parses a single atomic filter expression string into a FilterExpression.
	/// Supports: "text" (all columns), "col:value" (specific column), "col>value", "col&lt;value".
	/// </summary>
	internal FilterExpression? ParseSingleFilterExpression(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
			return null;

		// Check for operator patterns: col>value, col<value, col:value
		for (int i = 0; i < input.Length; i++)
		{
			char c = input[i];
			if (c == '>' || c == '<' || c == ':')
			{
				string colPart = input.Substring(0, i).Trim();
				string valPart = input.Substring(i + 1).Trim();

				if (!string.IsNullOrEmpty(colPart) && !string.IsNullOrEmpty(valPart))
				{
					// Verify column name exists
					if (ResolveColumnName(colPart) != null)
					{
						var op = c switch
						{
							'>' => FilterOperator.GreaterThan,
							'<' => FilterOperator.LessThan,
							_ => FilterOperator.Contains
						};

						return new FilterExpression
						{
							RawText = input,
							ColumnName = colPart,
							Value = valPart,
							Operator = op
						};
					}
				}
			}
		}

		// Plain text search across all columns
		return new FilterExpression
		{
			RawText = input,
			ColumnName = null,
			Value = input,
			Operator = FilterOperator.Contains
		};
	}

	/// <summary>
	/// Parses a compound filter expression from input text.
	/// Space-separated terms are AND; pipe-separated alternatives within a term are OR.
	/// Column prefix propagation: "category:electronics|clothing" → both alternatives target "category".
	/// </summary>
	internal CompoundFilterExpression? ParseCompoundFilterExpression(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
			return null;

		var rawTerms = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (rawTerms.Length == 0)
			return null;

		var terms = new List<FilterTerm>();

		foreach (var rawTerm in rawTerms)
		{
			var alternatives = rawTerm.Split('|', StringSplitOptions.RemoveEmptyEntries);
			if (alternatives.Length == 0) continue;

			var parsedAlternatives = new List<FilterExpression>();

			// Parse the first alternative to determine if it has a column prefix
			var firstExpr = ParseSingleFilterExpression(alternatives[0]);
			if (firstExpr == null) continue;
			parsedAlternatives.Add(firstExpr);

			// Parse remaining alternatives with column prefix propagation
			for (int i = 1; i < alternatives.Length; i++)
			{
				var alt = alternatives[i];
				var altExpr = ParseSingleFilterExpression(alt);
				if (altExpr == null) continue;

				// Inherit column and operator from first alternative if this one has no column prefix
				if (altExpr.ColumnName == null && firstExpr.ColumnName != null)
				{
					altExpr = new FilterExpression
					{
						RawText = alt,
						ColumnName = firstExpr.ColumnName,
						Value = altExpr.Value,
						Operator = firstExpr.Operator
					};
				}

				parsedAlternatives.Add(altExpr);
			}

			if (parsedAlternatives.Count > 0)
				terms.Add(new FilterTerm { Alternatives = parsedAlternatives });
		}

		if (terms.Count == 0)
			return null;

		return new CompoundFilterExpression
		{
			RawText = input,
			Terms = terms
		};
	}

	/// <summary>
	/// Backward-compatible wrapper: parses as compound but returns a single FilterExpression if possible.
	/// Used by tests that call ParseFilterExpression directly.
	/// </summary>
	internal FilterExpression? ParseFilterExpression(string input)
	{
		return ParseSingleFilterExpression(input);
	}

	/// <summary>
	/// Resolves a column name case-insensitively. Returns the canonical name or null if not found.
	/// </summary>
	private string? ResolveColumnName(string name)
	{
		if (_dataSource != null)
		{
			for (int c = 0; c < _dataSource.ColumnCount; c++)
			{
				if (string.Equals(_dataSource.GetColumnHeader(c), name, StringComparison.OrdinalIgnoreCase))
					return _dataSource.GetColumnHeader(c);
			}
		}
		else
		{
			lock (_tableLock)
			{
				for (int c = 0; c < _columns.Count; c++)
				{
					if (string.Equals(_columns[c].Header, name, StringComparison.OrdinalIgnoreCase))
						return _columns[c].Header;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Resolves a column name to its index. Returns -1 if not found.
	/// </summary>
	private int ResolveColumnIndex(string name)
	{
		if (_dataSource != null)
		{
			for (int c = 0; c < _dataSource.ColumnCount; c++)
			{
				if (string.Equals(_dataSource.GetColumnHeader(c), name, StringComparison.OrdinalIgnoreCase))
					return c;
			}
		}
		else
		{
			lock (_tableLock)
			{
				for (int c = 0; c < _columns.Count; c++)
				{
					if (string.Equals(_columns[c].Header, name, StringComparison.OrdinalIgnoreCase))
						return c;
				}
			}
		}
		return -1;
	}

	#endregion

	#region Filter Computation

	/// <summary>
	/// Computes the display map client-side, through <see cref="ComputeDisplayRows"/>.
	/// </summary>
	/// <remarks>
	/// Unconditional: the filter paths call it after the data source has declined the filter, when
	/// the map is the table's to build whatever the source answered before. Everything else goes
	/// through <see cref="RebuildDisplayMap"/>, which leaves a source's own answer alone.
	/// </remarks>
	internal void RecomputeDisplayMap()
	{
		ThrowIfComputingDisplayRows();

		var query = new TableDisplayQuery(_activeFilter, _filterMode, _sortColumnIndex, _sortDirection);
		int[]? rows;
		_displayRowsHookDepth++;
		try
		{
			rows = ComputeDisplayRows(query);
		}
		finally
		{
			_displayRowsHookDepth--;
		}

		AssertValidDisplayRows(rows);
		_rowView.SetComputed(rows, builtWithFilter: query.IsFiltered);
	}

	/// <summary>
	/// Decides which data rows the table displays, and in what order.
	/// </summary>
	/// <param name="query">The filter and the sort the table wants applied.</param>
	/// <returns>
	/// The data rows to display, in display order, or null for every row in data order. Each data
	/// index may appear at most once and must lie in [0, <see cref="DataRowCount"/>).
	/// </returns>
	/// <remarks>
	/// <para>
	/// WHY THIS IS OVERRIDABLE. The table can filter and sort a flat list of rows; it cannot know that
	/// a row is the child of another, that a collapsed parent hides its children, or that a parent
	/// must stay visible because one of its children matches. A derived table that knows such things
	/// decides here, and everything that follows — paint, the selection, scrolling, the row count,
	/// the column widths — follows from the rows it returns. It can still lean on the table's rules
	/// through <see cref="RowMatchesFilter(int, CompoundFilterExpression)"/> and <see cref="SortRowIndices"/>, or call this base
	/// method and work from its answer.
	/// </para>
	/// <para>
	/// WHEN IT IS CALLED. For the table's own rows, after every change that can change what is
	/// displayed — a row added, removed or replaced, a sort, a filter typed or cleared — and whenever
	/// a derived table calls <see cref="RefreshDisplayRows"/>; with nothing filtered or sorted too,
	/// since a derived table may hide rows of its own. With a data source, only while the table
	/// filters it client-side: otherwise the source decides what it reports. Always on the UI thread,
	/// and never while <see cref="SyncRoot"/> is held, so an override may take it to read rows
	/// consistently.
	/// </para>
	/// <para>
	/// WHY AN ARRAY. It runs on every change, and the table keeps the result as it is, without
	/// copying: ownership passes to the table, and the array must not be changed afterwards. An
	/// override must not change the rows either; doing so throws <see cref="InvalidOperationException"/>
	/// rather than recursing.
	/// </para>
	/// <para>
	/// THE DEFAULT applies the filter by <see cref="RowMatchesFilter(int, CompoundFilterExpression)"/> and sorts stably by
	/// <see cref="SortRowIndices"/>. It is kept up to date incrementally: when one insert or removal
	/// separates two calls under the same filter and sort, it updates its last answer instead of
	/// recomputing it, so refilling a sorted table row by row stays O(n log n) in all. An override
	/// that does not call it pays for its own computation on every change.
	/// </para>
	/// </remarks>
	protected virtual int[]? ComputeDisplayRows(TableDisplayQuery query)
	{
		var filter = query.Filter;
		bool sorted = query.IsSorted;
		if (filter == null && !sorted)
		{
			_rowView.ForgetComputed();
			return null;
		}

		if (_dataSource != null)
		{
			// Step 1: The filter's matches, or every row when only sorting
			int[] matches = filter != null
				? ComputeFilteredIndices(filter)
				: Enumerable.Range(0, GetUnfilteredRowCount()).ToArray();

			// Step 2: If sort is active, sort them
			if (sorted)
				SortRowIndices(matches, query.SortColumnIndex, query.SortDirection);

			return matches;
		}

		// The table's own rows: updated from the last result across a single insert or removal,
		// computed in full otherwise. See TableRowView's incremental upkeep for why.
		lock (_tableLock)
		{
			var sortColumn = _columns.ElementAtOrDefault(query.SortColumnIndex);
			var key = new DisplayRowsKey(filter, query.SortColumnIndex, query.SortDirection,
				sortColumn?.CustomRowComparer, sortColumn?.CustomComparer, _fuzzyFilterEnabled);
			Func<int, bool>? passes = filter != null ? dataIndex => RowMatchesFilter(dataIndex, filter) : null;
			Comparison<int> order = sorted
				? CreateRowComparison(query.SortColumnIndex, query.SortDirection)
				: (a, b) => a.CompareTo(b);

			if (!_rowView.TryUpdateComputed(key, passes, order, out int[] rows))
			{
				rows = filter != null
					? ComputeFilteredIndices(filter)
					: Enumerable.Range(0, _rows.Count).ToArray();
				if (sorted)
					Array.Sort(rows, order);
			}

			_rowView.RememberComputed(key, rows);
			return rows;
		}
	}

	/// <summary>
	/// Checks, in debug builds, that display rows name each data row at most once and only rows
	/// that exist.
	/// </summary>
	/// <remarks>
	/// A bad index would otherwise surface deep in paint, far from the override that produced it; in
	/// release the hot path trusts the override rather than re-checking every row on every change.
	/// </remarks>
	[System.Diagnostics.Conditional("DEBUG")]
	private void AssertValidDisplayRows(int[]? rows)
	{
		if (rows == null) return;

		int dataRowCount = DataRowCount;
		var seen = new HashSet<int>();
		foreach (int dataIndex in rows)
		{
			System.Diagnostics.Debug.Assert(dataIndex >= 0 && dataIndex < dataRowCount,
				$"ComputeDisplayRows returned data row {dataIndex}, outside 0..{dataRowCount - 1}.");
			System.Diagnostics.Debug.Assert(seen.Add(dataIndex),
				$"ComputeDisplayRows returned data row {dataIndex} more than once.");
		}
	}

	/// <summary>
	/// Shows every row again after the filter text was emptied or stopped parsing: a data source
	/// is read by identity, the table's own rows keep the current sort.
	/// </summary>
	private void ShowUnfilteredRows()
	{
		if (_dataSource != null)
			_rowView.Clear();
		else
			RebuildDisplayMap();
	}

	/// <summary>
	/// Collects data indices of rows matching a single filter expression.
	/// </summary>
	internal int[] ComputeFilteredIndices(FilterExpression filter)
	{
		var matches = new List<int>();
		int totalRows = GetUnfilteredRowCount();

		for (int i = 0; i < totalRows; i++)
		{
			if (RowMatchesFilter(i, filter))
				matches.Add(i);
		}

		return matches.ToArray();
	}

	/// <summary>
	/// Collects data indices of rows matching a compound filter expression.
	/// </summary>
	internal int[] ComputeFilteredIndices(CompoundFilterExpression filter)
	{
		var matches = new List<int>();
		int totalRows = GetUnfilteredRowCount();

		for (int i = 0; i < totalRows; i++)
		{
			if (RowMatchesFilter(i, filter))
				matches.Add(i);
		}

		return matches.ToArray();
	}

	/// <summary>
	/// Gets the total unfiltered row count.
	/// </summary>
	private int GetUnfilteredRowCount()
	{
		if (_dataSource != null)
			return _unfilteredRowCount > 0 ? _unfilteredRowCount : _dataSource.RowCount;

		lock (_tableLock)
		{
			return _rows.Count;
		}
	}

	/// <summary>
	/// Tests whether a single row matches the filter expression.
	/// </summary>
	internal bool RowMatchesFilter(int dataIndex, FilterExpression filter)
	{
		int colCount;
		if (_dataSource != null)
			colCount = _dataSource.ColumnCount;
		else
			lock (_tableLock) { colCount = _columns.Count; }

		if (filter.ColumnName != null)
		{
			// Column-specific filter
			int colIdx = ResolveColumnIndex(filter.ColumnName);
			if (colIdx < 0) return false;

			string cellValue = GetRawCellValue(dataIndex, colIdx);
			return CellMatchesFilter(cellValue, filter.Value, filter.Operator);
		}
		else
		{
			// All-columns filter
			for (int c = 0; c < colCount; c++)
			{
				string cellValue = GetRawCellValue(dataIndex, c);
				if (CellMatchesFilter(cellValue, filter.Value, filter.Operator))
					return true;
			}

			// Fuzzy fallback
			if (_fuzzyFilterEnabled)
			{
				for (int c = 0; c < colCount; c++)
				{
					string cellValue = GetRawCellValue(dataIndex, c);
					if (FuzzyMatch(cellValue, filter.Value))
						return true;
				}
			}

			return false;
		}
	}

	/// <summary>
	/// Tests whether a data row matches a filter by the table's own rules.
	/// </summary>
	/// <param name="dataIndex">The data row to test.</param>
	/// <param name="filter">The filter: every term must match (AND), and within a term any alternative (OR).</param>
	/// <returns>True when the row matches.</returns>
	/// <remarks>
	/// <para>
	/// THE RULES IN ONE PLACE: a column term matches that column's text, a plain term any column's,
	/// numbers compare as numbers for <c>&gt;</c> and <c>&lt;</c>, markup is ignored, and with
	/// <see cref="FuzzyFilterEnabled"/> a plain term also matches as a subsequence. A derived table
	/// that decides for itself which rows a filter shows — keeping the parent of a matching row, for
	/// instance — calls this for each row rather than reproducing the rules, so its filter means
	/// exactly what the table's does.
	/// </para>
	/// <para>
	/// Safe to call while the rows are being painted: the cells are read under <see cref="SyncRoot"/>.
	/// </para>
	/// </remarks>
	protected internal bool RowMatchesFilter(int dataIndex, CompoundFilterExpression filter)
	{
		foreach (var term in filter.Terms)
		{
			bool termMatches = false;
			foreach (var alt in term.Alternatives)
			{
				if (RowMatchesFilter(dataIndex, alt))
				{
					termMatches = true;
					break;
				}
			}
			if (!termMatches)
				return false;
		}
		return true;
	}

	/// <summary>
	/// Gets the raw (markup-stripped) cell value for a data row index.
	/// </summary>
	private string GetRawCellValue(int dataIndex, int colIndex)
	{
		string raw;
		if (_dataSource != null)
		{
			raw = _dataSource.GetCellValue(dataIndex, colIndex);
		}
		else
		{
			lock (_tableLock)
			{
				if (dataIndex < 0 || dataIndex >= _rows.Count) return string.Empty;
				if (colIndex < 0 || colIndex >= _rows[dataIndex].Cells.Count) return string.Empty;
				raw = _rows[dataIndex].Cells[colIndex];
			}
		}
		return MarkupParser.Remove(raw);
	}

	/// <summary>
	/// Tests whether a cell value matches a filter value with the given operator.
	/// </summary>
	private static bool CellMatchesFilter(string cellValue, string filterValue, FilterOperator op)
	{
		switch (op)
		{
			case FilterOperator.Contains:
				return cellValue.Contains(filterValue, StringComparison.OrdinalIgnoreCase);

			case FilterOperator.GreaterThan:
				// Strip common prefixes like $ for numeric comparison
				if (double.TryParse(StripNumericPrefix(cellValue), out double cellNum) &&
					double.TryParse(StripNumericPrefix(filterValue), out double filterNum))
					return cellNum > filterNum;
				return string.Compare(cellValue, filterValue, StringComparison.OrdinalIgnoreCase) > 0;

			case FilterOperator.LessThan:
				if (double.TryParse(StripNumericPrefix(cellValue), out double cellNum2) &&
					double.TryParse(StripNumericPrefix(filterValue), out double filterNum2))
					return cellNum2 < filterNum2;
				return string.Compare(cellValue, filterValue, StringComparison.OrdinalIgnoreCase) < 0;

			default:
				return false;
		}
	}

	/// <summary>
	/// Strips common numeric prefixes ($, etc.) for parsing.
	/// </summary>
	private static string StripNumericPrefix(string value)
	{
		if (value.Length > 0 && (value[0] == '$' || value[0] == '€' || value[0] == '£'))
			return value.Substring(1);
		// Also strip star ratings suffix
		return value.TrimEnd(' ', '\u2605', '\u2606');
	}

	/// <summary>
	/// Character-subsequence fuzzy match.
	/// </summary>
	private static bool FuzzyMatch(string text, string pattern)
	{
		int pi = 0;
		for (int ti = 0; ti < text.Length && pi < pattern.Length; ti++)
		{
			if (char.ToLowerInvariant(text[ti]) == char.ToLowerInvariant(pattern[pi]))
				pi++;
		}
		return pi == pattern.Length;
	}

	/// <summary>
	/// Sorts an array of data indices by the current sort column.
	/// </summary>
	private void SortIndices(int[] indices) => SortRowIndices(indices, _sortColumnIndex, _sortDirection);

	#endregion

	#region Match Highlighting

	/// <summary>
	/// Finds match positions within a row for highlighting (single expression).
	/// Returns (Column, StartIndex, Length) tuples.
	/// </summary>
	internal List<(int Column, int Start, int Length)> FindMatchPositions(int dataIndex, FilterExpression filter)
	{
		var positions = new List<(int Column, int Start, int Length)>();

		int colCount;
		if (_dataSource != null)
			colCount = _dataSource.ColumnCount;
		else
			lock (_tableLock) { colCount = _columns.Count; }

		if (filter.ColumnName != null)
		{
			int colIdx = ResolveColumnIndex(filter.ColumnName);
			if (colIdx >= 0)
				FindMatchesInCell(dataIndex, colIdx, filter.Value, filter.Operator, positions);
		}
		else
		{
			for (int c = 0; c < colCount; c++)
				FindMatchesInCell(dataIndex, c, filter.Value, filter.Operator, positions);
		}

		return positions;
	}

	/// <summary>
	/// Finds match positions within a row for highlighting (compound expression).
	/// Collects matches from all terms and all alternatives.
	/// </summary>
	internal List<(int Column, int Start, int Length)> FindMatchPositions(int dataIndex, CompoundFilterExpression filter)
	{
		var positions = new List<(int Column, int Start, int Length)>();

		foreach (var term in filter.Terms)
		{
			foreach (var alt in term.Alternatives)
			{
				var altPositions = FindMatchPositions(dataIndex, alt);
				positions.AddRange(altPositions);
			}
		}

		return positions;
	}

	private void FindMatchesInCell(int dataIndex, int colIndex, string filterValue, FilterOperator op,
		List<(int Column, int Start, int Length)> positions)
	{
		if (op != FilterOperator.Contains) return;

		string cellValue = GetRawCellValue(dataIndex, colIndex);
		int idx = 0;
		while (idx < cellValue.Length)
		{
			int found = cellValue.IndexOf(filterValue, idx, StringComparison.OrdinalIgnoreCase);
			if (found < 0) break;
			positions.Add((colIndex, found, filterValue.Length));
			idx = found + 1;
		}
	}

	#endregion

	#region Status Bar Rendering

	/// <summary>
	/// Draws the filter status bar row with colored segments.
	/// </summary>
	internal void DrawFilterStatusBar(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect,
		Color fgColor, Color bgColor, BoxChars box, Color borderColor, bool hasBorder)
	{
		var status = new TableFilterStatus(
			Mode: _filterMode,
			Buffer: _filterBuffer,
			CursorPosition: _filterCursorPosition,
			FilterText: _activeFilter?.RawText ?? _filterBuffer,
			RowCount: RowCount,
			TotalRows: _unfilteredRowCount > 0 ? _unfilteredRowCount : GetUnfilteredRowCount(),
			SelectedRowIndex: _selectedRowIndex);

		TableRowPainter.DrawFilterStatusBar(buffer, x, y, width, clipRect, status,
			fgColor, bgColor, box, borderColor, hasBorder);
	}

	#endregion
}

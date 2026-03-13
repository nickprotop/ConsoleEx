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
			Container?.Invalidate(true);
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

	/// <summary>
	/// Occurs when the filter is cleared.
	/// </summary>
	public event EventHandler? FilterCleared;

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

		// Store unfiltered count before filtering
		if (_filterIndexMap == null)
		{
			if (_dataSource != null)
				_unfilteredRowCount = _dataSource.RowCount;
			else
				lock (_tableLock) { _unfilteredRowCount = _rows.Count; }
		}

		RecomputeDisplayMap();

		_selectedRowIndex = RowCount > 0 ? 0 : -1;
		_scrollOffset = 0;
		_selectedRowIndices.Clear();

		FilterApplied?.Invoke(this, compound.RawText);
		InvalidateColumnWidths();
		Container?.Invalidate(true);
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
		if (_filterMode == FilterMode.None && _filterIndexMap == null) return;

		_filterMode = FilterMode.None;
		_filterBuffer = string.Empty;
		_filterCursorPosition = 0;
		_activeFilter = null;
		_filterIndexMap = null;
		_unfilteredRowCount = 0;

		// If sort is still active, ensure sort map is intact
		if (_sortDirection != SortDirection.None)
		{
			// Re-apply sort without filter
			ApplySort();
		}

		FilterCleared?.Invoke(this, EventArgs.Empty);
		InvalidateColumnWidths();
		Container?.Invalidate(true);
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
		if (_filterIndexMap == null)
		{
			if (_dataSource != null)
				_unfilteredRowCount = _dataSource.RowCount;
			else
				lock (_tableLock) { _unfilteredRowCount = _rows.Count; }
		}

		Container?.Invalidate(true);
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
					FilterApplied?.Invoke(this, _filterBuffer);
				}
				else
				{
					ClearFilter();
				}
				Container?.Invalidate(true);
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
					Container?.Invalidate(true);
				}
				return true;

			case ConsoleKey.RightArrow:
				if (_filterCursorPosition < _filterBuffer.Length)
				{
					_filterCursorPosition++;
					Container?.Invalidate(true);
				}
				return true;

			case ConsoleKey.Home:
				_filterCursorPosition = 0;
				Container?.Invalidate(true);
				return true;

			case ConsoleKey.End:
				_filterCursorPosition = _filterBuffer.Length;
				Container?.Invalidate(true);
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
			_filterIndexMap = null;
			_activeFilter = null;
			_selectedRowIndex = RowCount > 0 ? 0 : -1;
			_scrollOffset = 0;
		}
		else
		{
			var compound = ParseCompoundFilterExpression(_filterBuffer);
			_activeFilter = compound;

			if (compound != null)
				RecomputeDisplayMap();
			else
				_filterIndexMap = null;

			_selectedRowIndex = RowCount > 0 ? 0 : -1;
			_scrollOffset = 0;
		}

		_selectedRowIndices.Clear();
		FilterTextChanged?.Invoke(this, _filterBuffer);
		InvalidateColumnWidths();
		Container?.Invalidate(true);
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
	/// Recomputes the combined filter+sort display map.
	/// </summary>
	internal void RecomputeDisplayMap()
	{
		if (_activeFilter == null)
		{
			_filterIndexMap = null;
			return;
		}

		// Step 1: Compute filtered indices (compound filter)
		var filtered = ComputeFilteredIndices(_activeFilter);

		// Step 2: If sort is active, sort the filtered indices
		if (_sortDirection != SortDirection.None && _sortColumnIndex >= 0)
		{
			SortIndices(filtered);
		}

		_filterIndexMap = filtered;
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
			if (RowMatchesCompoundFilter(i, filter))
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
	/// Tests whether a single row matches a compound filter expression.
	/// All terms must match (AND), and within each term any alternative must match (OR).
	/// </summary>
	internal bool RowMatchesCompoundFilter(int dataIndex, CompoundFilterExpression filter)
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
	private void SortIndices(int[] indices)
	{
		if (_dataSource != null)
		{
			// For DataSource, sort by raw cell values
			int col = _sortColumnIndex;
			Array.Sort(indices, (a, b) =>
			{
				string valA = _dataSource.GetCellValue(a, col);
				string valB = _dataSource.GetCellValue(b, col);
				int result = string.Compare(MarkupParser.Remove(valA), MarkupParser.Remove(valB), StringComparison.OrdinalIgnoreCase);
				return _sortDirection == SortDirection.Descending ? -result : result;
			});
		}
		else
		{
			lock (_tableLock)
			{
				int col = _sortColumnIndex;
				IComparer<string>? customComparer = null;
				if (col >= 0 && col < _columns.Count)
					customComparer = _columns[col].CustomComparer;

				Array.Sort(indices, (a, b) =>
				{
					string valA = col < _rows[a].Cells.Count ? _rows[a].Cells[col] : string.Empty;
					string valB = col < _rows[b].Cells.Count ? _rows[b].Cells[col] : string.Empty;

					int result;
					if (customComparer != null)
						result = customComparer.Compare(valA, valB);
					else
						result = string.Compare(valA, valB, StringComparison.OrdinalIgnoreCase);

					return _sortDirection == SortDirection.Descending ? -result : result;
				});
			}
		}
	}

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
		Color fgColor, Color bgColor, BoxChars box, Color borderColor, bool hasBorder, bool preserveBg)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) return;

		int totalRows = _unfilteredRowCount > 0 ? _unfilteredRowCount : GetUnfilteredRowCount();

		// Build segments: (text, foreground color)
		var segments = new List<(string Text, Color Fg)>();

		switch (_filterMode)
		{
			case FilterMode.Typing:
				segments.Add((" \u2315 ", Color.Cyan1));                          // ⌕ filter icon
				segments.Add((_filterBuffer, Color.White));
				segments.Add(("\u2581", Color.White));                             // cursor block
				segments.Add(("  ", fgColor));
				segments.Add(("Enter", Color.Yellow));
				segments.Add((" confirm  ", Color.Grey));
				segments.Add(("Esc", Color.Yellow));
				segments.Add((" cancel", Color.Grey));
				break;

			case FilterMode.Confirmed:
				int filteredCount = _filterIndexMap?.Length ?? 0;
				string filterText = _activeFilter?.RawText ?? _filterBuffer;
				segments.Add((" \u2315 ", Color.Cyan1));
				segments.Add((filterText, Color.White));
				segments.Add(("  ", fgColor));
				if (filteredCount == 0)
				{
					segments.Add(("No matches", Color.Red));
				}
				else
				{
					segments.Add(($"{filteredCount}", Color.Green));
					segments.Add(($"/{totalRows} rows", Color.Grey));
				}
				segments.Add(("  ", fgColor));
				segments.Add(("Esc", Color.Yellow));
				segments.Add((" clear", Color.Grey));
				break;

			default:
				int selRow = _selectedRowIndex >= 0 ? _selectedRowIndex + 1 : 0;
				int rowCount = RowCount;
				segments.Add(($" Row {selRow}/{rowCount}", Color.Grey50));
				segments.Add(("  ", fgColor));
				segments.Add(("/", Color.Yellow));
				segments.Add((" filter", Color.Grey50));
				break;
		}

		// Render: left border, content, right border
		int writeX = x;

		if (hasBorder)
		{
			if (writeX >= clipRect.X && writeX < clipRect.Right)
			{
				Color bg = preserveBg ? buffer.GetCell(writeX, y).Background : bgColor;
				buffer.SetNarrowCell(writeX, y, box.Vertical, borderColor, bg);
			}
			writeX++;
		}

		// Content area
		int contentWidth = width - (hasBorder ? 2 : 0);
		int charPos = 0;

		// Track cursor position for typing mode highlight
		int cursorCharPos = _filterMode == FilterMode.Typing ? UnicodeWidth.GetStringWidth(" \u2315 ") + _filterCursorPosition : -1;

		foreach (var (text, fg) in segments)
		{
			foreach (var rune in text.EnumerateRunes())
			{
				int runeWidth = UnicodeWidth.GetRuneWidth(rune);
				if (runeWidth == 0) continue; // skip zero-width characters
				if (charPos >= contentWidth) break;
				if (writeX >= clipRect.X && writeX < clipRect.Right)
				{
					Color cellFg = fg;
					Color cellBg = preserveBg ? buffer.GetCell(writeX, y).Background : bgColor;

					// Highlight cursor position in typing mode
					if (charPos == cursorCharPos)
					{
						cellFg = Color.Black;
						cellBg = Color.White;
					}

					buffer.SetNarrowCell(writeX, y, rune, cellFg, cellBg);

					// Wide character: mark continuation cell
					if (runeWidth == 2 && charPos + 1 < contentWidth)
					{
						var cont = new Cell(' ', cellFg, cellBg) { IsWideContinuation = true };
						if (writeX + 1 >= clipRect.X && writeX + 1 < clipRect.Right)
							buffer.SetCell(writeX + 1, y, cont);
						writeX++;
						charPos++;
					}
				}
				writeX++;
				charPos++;
			}
			if (charPos >= contentWidth) break;
		}

		// Fill remaining space
		while (charPos < contentWidth)
		{
			if (writeX >= clipRect.X && writeX < clipRect.Right)
			{
				Color bg = preserveBg ? buffer.GetCell(writeX, y).Background : bgColor;
				buffer.SetNarrowCell(writeX, y, ' ', fgColor, bg);
			}
			writeX++;
			charPos++;
		}

		if (hasBorder)
		{
			if (writeX >= clipRect.X && writeX < clipRect.Right)
			{
				Color bg = preserveBg ? buffer.GetCell(writeX, y).Background : bgColor;
				buffer.SetNarrowCell(writeX, y, box.Vertical, borderColor, bg);
			}
		}
	}

	#endregion
}

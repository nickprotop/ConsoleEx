// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Tests for the <see cref="ITableDataSource"/> hooks that were declared on the interface but never
/// called by the control: <c>CanFilter</c> / <c>ApplyFilter</c> / <c>ClearFilter</c> (the server-side
/// filtering seam) and <c>GetRowTag</c>.
///
/// <para>
/// The filtering seam is the one that mattered. <c>TableControl.ApplyFilter</c> always filtered
/// client-side through <c>RecomputeDisplayMap</c>, which walks EVERY row calling
/// <c>GetCellValue</c> per column — so filtering a virtualized source pulled the whole set into
/// memory, defeating the point of <see cref="ITableDataSource"/>. A source advertising
/// <c>CanFilter</c> now has the filter pushed down to it instead.
/// </para>
/// </summary>
public class TableDataSourceSeamTests
{
	/// <summary>
	/// A data source that records what the control asks of it, so the tests can assert on
	/// DELEGATION (what was pushed down) rather than on rendering.
	/// </summary>
	private sealed class RecordingDataSource : ITableDataSource
	{
		private readonly List<string[]> _all;
		private List<string[]> _visible;

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public RecordingDataSource(bool canFilter, params string[][] rows)
		{
			CanFilter = canFilter;
			_all = new List<string[]>(rows);
			_visible = new List<string[]>(rows);
		}

		public bool CanFilter { get; }

		// --- recorded calls -------------------------------------------------
		public int ApplyFilterCalls { get; private set; }
		public int ClearFilterCalls { get; private set; }
		public string? LastFilterText { get; private set; }
		public string? LastColumnName { get; private set; }
		public FilterOperator LastOperator { get; private set; }
		/// <summary>Counts cell reads, to prove the control did NOT scan every row itself.</summary>
		public int GetCellValueCalls { get; private set; }

		public int RowCount => _visible.Count;
		public int ColumnCount => 2;
		public string GetColumnHeader(int c) => c == 0 ? "Name" : "City";

		public string GetCellValue(int r, int c)
		{
			GetCellValueCalls++;
			return _visible[r][c];
		}

		public object? GetRowTag(int rowIndex) => $"tag-{_visible[rowIndex][0]}";

		public void ApplyFilter(string filterText, string? columnName, FilterOperator op)
		{
			ApplyFilterCalls++;
			LastFilterText = filterText;
			LastColumnName = columnName;
			LastOperator = op;

			// Stand in for a server-side WHERE: only matching rows remain visible.
			_visible = _all.FindAll(row =>
			{
				foreach (var cell in row)
					if (cell.Contains(filterText, StringComparison.OrdinalIgnoreCase)) return true;
				return false;
			});
			CollectionChanged?.Invoke(this,
				new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		public void ClearFilter()
		{
			ClearFilterCalls++;
			_visible = new List<string[]>(_all);
			CollectionChanged?.Invoke(this,
				new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
	}

	private static RecordingDataSource MakeSource(bool canFilter) => new(
		canFilter,
		new[] { "alice", "berlin" },
		new[] { "bob", "paris" },
		new[] { "carol", "berlin" });

	// ── Server-side filtering seam ──────────────────────────────────────────────────────────────

	[Fact]
	public void FilteringSource_GetsTheFilterPushedDown()
	{
		var src = MakeSource(canFilter: true);
		var table = new TableControl { DataSource = src, FilteringEnabled = true };

		table.ApplyFilter("berlin");

		Assert.Equal(1, src.ApplyFilterCalls);
		Assert.Equal("berlin", src.LastFilterText);
		Assert.Equal(2, table.RowCount);   // the source narrowed itself; control reports its count
	}

	[Fact]
	public void FilteringSource_IsNotScannedRowByRow()
	{
		// The point of the seam: the control must NOT walk every row calling GetCellValue to
		// filter. Before the fix this was exactly what happened.
		var src = MakeSource(canFilter: true);
		var table = new TableControl { DataSource = src, FilteringEnabled = true };

		int before = src.GetCellValueCalls;
		table.ApplyFilter("berlin");
		int scanned = src.GetCellValueCalls - before;

		Assert.Equal(0, scanned);
	}

	[Fact]
	public void NonFilteringSource_StillFiltersClientSide()
	{
		// A source that does NOT advertise CanFilter keeps the existing behavior — this is the
		// backward-compatibility guard.
		var src = MakeSource(canFilter: false);
		var table = new TableControl { DataSource = src, FilteringEnabled = true };

		table.ApplyFilter("berlin");

		Assert.Equal(0, src.ApplyFilterCalls);
		Assert.Equal(2, table.RowCount);   // client-side filter found the same two rows
	}

	[Fact]
	public void ClearingAFilter_IsPushedDownToo()
	{
		var src = MakeSource(canFilter: true);
		var table = new TableControl { DataSource = src, FilteringEnabled = true };

		table.ApplyFilter("berlin");
		Assert.Equal(2, table.RowCount);

		table.ClearFilter();

		Assert.Equal(1, src.ClearFilterCalls);
		Assert.Equal(3, table.RowCount);   // all rows restored
	}

	[Fact]
	public void ColumnFilter_PassesTheColumnAndOperator()
	{
		var src = MakeSource(canFilter: true);
		var table = new TableControl { DataSource = src, FilteringEnabled = true };

		table.FilterByColumn(1, "berlin");

		Assert.Equal(1, src.ApplyFilterCalls);
		Assert.Equal("City", src.LastColumnName);
		Assert.Equal("berlin", src.LastFilterText);
		Assert.Equal(FilterOperator.Contains, src.LastOperator);
	}

	[Fact]
	public void CompoundFilter_FallsBackToClientSide()
	{
		// The interface hook takes ONE (text, column, operator). A compound AND/OR filter cannot be
		// expressed through it, so delegating would silently drop conditions — it must filter
		// client-side instead of lying to the source.
		var src = MakeSource(canFilter: true);
		var table = new TableControl { DataSource = src, FilteringEnabled = true };

		table.ApplyFilter("berlin alice");   // two AND terms

		Assert.Equal(0, src.ApplyFilterCalls);
		Assert.Equal(1, table.RowCount);     // only alice/berlin matches both
	}

	[Fact]
	public void LiveTyping_DelegatesAndStillRaisesFilterTextChanged()
	{
		// The as-you-type ('/') path must use the same seam, and must not lose FilterTextChanged
		// when it delegates. Driven through EnterFilterMode/ApplyFilterLive — the same methods the
		// '/' key handler calls — rather than synthesising keystrokes.
		var src = MakeSource(canFilter: true);
		var table = new TableControl { DataSource = src, FilteringEnabled = true };

		string? raised = null;
		table.FilterTextChanged += (_, text) => raised = text;

		table.EnterFilterMode();
		foreach (char ch in "berlin")
			table.ProcessFilterKey(new ConsoleKeyInfo(ch, ConsoleKey.NoName, false, false, false));

		Assert.True(src.ApplyFilterCalls > 0, "live typing did not reach the data source");
		Assert.Equal("berlin", raised);          // event still fires on the delegated path
		Assert.Equal(2, table.RowCount);
	}

	// ── GetRowTag ───────────────────────────────────────────────────────────────────────────────

	[Fact]
	public void GetRowTag_IsReadFromTheDataSource()
	{
		var src = MakeSource(canFilter: false);
		var table = new TableControl { DataSource = src };

		Assert.Equal("tag-alice", table.GetRowTagAt(0));
		Assert.Equal("tag-bob", table.GetRowTagAt(1));
	}

	[Fact]
	public void GetRowTag_FallsBackToTheRowTag_WithoutADataSource()
	{
		var table = new TableControl();
		table.AddRow(new TableRow(new List<string> { "x" }) { Tag = "row-tag" });

		Assert.Equal("row-tag", table.GetRowTagAt(0));
	}

	[Fact]
	public void GetRowTag_OutOfRange_ReturnsNull()
	{
		var table = new TableControl { DataSource = MakeSource(canFilter: false) };

		Assert.Null(table.GetRowTagAt(-1));
		Assert.Null(table.GetRowTagAt(99));
	}
}

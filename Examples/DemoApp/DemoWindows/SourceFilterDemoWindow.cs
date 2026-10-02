using System.Collections.Specialized;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

/// <summary>
/// Shows the two ways an <see cref="ITableDataSource"/> can take a filter the table would
/// otherwise apply itself, through <c>TableControl.TryApplyFilterToDataSource</c> (issue #88).
///
/// <para>Tab 1 — a flattened hierarchy supplying DISPLAY ROWS: filtering a tree has to keep a
/// matching task's story on screen, which the table cannot work out from flat rows.</para>
///
/// <para>Tab 2 — a pretend remote source that NARROWS ITSELF: the table holds no map at all and
/// the source reports only matches, which is what keeps a big source lazy.</para>
/// </summary>
public static class SourceFilterDemoWindow
{
	private const int WindowWidth = 96;
	private const int WindowHeight = 30;

	public static Window Create(ConsoleWindowSystem ws)
	{
		var tabs = Controls.TabControl()
			.AddTab("Tree (display rows)", BuildTreeTab(ws))
			.AddTab("Remote (narrows itself)", BuildRemoteTab(ws))
			.Fill()
			.Build();

		return new WindowBuilder(ws)
			.WithTitle("Data-source Filtering")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControl(tabs)
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)sender!);
					e.Handled = true;
				}
			})
			.BuildAndShow();
	}

	#region Tab 1 — a tree that supplies display rows

	private static IWindowControl BuildTreeTab(ConsoleWindowSystem ws)
	{
		var header = Controls.Markup()
			.AddLine("[bold]A hierarchy filtered by its own source[/]")
			.AddEmptyLine()
			.AddLine("Press [yellow]/[/] and type [yellow]task login[/] — two terms, so the filter is compound.")
			.AddLine("The matching task stays, [bold]and so does its story[/], although the story itself")
			.AddLine("does not match. A table filtering flat rows cannot know that.")
			.AddEmptyLine()
			.AddLine("[dim]Terms match anywhere in a cell, and a space means AND.[/]")
			.AddLine("[dim]Try [/][yellow]export[/][dim] too. Esc closes the window.[/]")
			.StickyTop()
			.Build();

		var table = new TreeTable
		{
			DataSource = new StorySource(),
			FilteringEnabled = true,
			ReadOnly = false,
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};

		return Controls.ScrollablePanel()
			.AddControl(header)
			.AddControl(table)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
	}

	/// <summary>Stories with their tasks, flattened into the rows a tree table reports.</summary>
	private sealed class StorySource : ITableDataSource
	{
		private readonly (string Item, string Owner, bool IsTask)[] _rows =
		[
			("Story: Authentication", "team-a", false),
			("    Task: login form", "maria", true),
			("    Task: password reset", "nikos", true),
			("Story: Reporting", "team-b", false),
			("    Task: export CSV", "maria", true),
			("    Task: nightly digest", "petros", true),
			("Story: Billing", "team-a", false),
			("    Task: invoice PDF", "nikos", true),
		];

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public bool CanFilter => true;
		// NEVER narrows: every row is still reported, and the supplied map decides what shows.
		public int RowCount => _rows.Length;
		public int ColumnCount => 2;
		public string GetColumnHeader(int c) => c == 0 ? "Item" : "Owner";
		public string GetCellValue(int r, int c) => c == 0 ? _rows[r].Item : _rows[r].Owner;
		public object? GetRowTag(int rowIndex) => null;

		// Unused: a compound filter arrives through the table's override instead, and this source
		// routes single expressions the same way so both behave identically.
		public void ApplyFilter(string filterText, string? columnName, FilterOperator op) { }
		public void ClearFilter() { }

		/// <summary>Rows matching every needle, plus the story above any matching task.</summary>
		public List<int> MatchesKeepingParents(IEnumerable<string> needles)
		{
			var keep = new SortedSet<int>();
			var wanted = needles.ToList();

			for (int i = 0; i < _rows.Length; i++)
			{
				bool matches = wanted.All(n =>
					_rows[i].Item.Contains(n, StringComparison.OrdinalIgnoreCase) ||
					_rows[i].Owner.Contains(n, StringComparison.OrdinalIgnoreCase));
				if (!matches) continue;

				keep.Add(i);
				if (_rows[i].IsTask)
				{
					for (int p = i - 1; p >= 0; p--)
						if (!_rows[p].IsTask) { keep.Add(p); break; }
				}
			}
			return keep.ToList();
		}
	}

	private sealed class TreeTable : TableControl
	{
		protected override TableFilterResult TryApplyFilterToDataSource(
			ITableDataSource dataSource, CompoundFilterExpression compound)
		{
			if (dataSource is not StorySource stories)
				return base.TryApplyFilterToDataSource(dataSource, compound);

			var needles = compound.Terms.Select(t => t.Alternatives[0].Value);
			return TableFilterResult.WithDisplayRows(stories.MatchesKeepingParents(needles));
		}
	}

	#endregion

	#region Tab 2 — a remote source that narrows itself

	private static IWindowControl BuildRemoteTab(ConsoleWindowSystem ws)
	{
		var source = new RemoteSource();

		var status = Controls.Markup("[dim]No query run yet.[/]")
			.StickyBottom()
			.Build();

		source.QueryRan += (_, text) => status.SetContent(
		[
			$"[dim]Source ran[/] [yellow]WHERE {text}[/][dim] and now reports[/] [green]{source.RowCount}[/]" +
			$"[dim] {(source.RowCount == 1 ? "row" : "rows")}. The table holds no map.[/]"
		]);

		// Without this the line keeps describing the last query after the filter is gone, which
		// reads as the table having failed to reset when in fact it has.
		source.QueryCleared += (_, _) => status.SetContent(
		[
			$"[dim]Filter cleared. The source reports all[/] [green]{source.RowCount}[/][dim] rows again.[/]"
		]);

		var header = Controls.Markup()
			.AddLine("[bold]A remote source filtering itself[/]")
			.AddEmptyLine()
			.AddLine("Press [yellow]/[/] and type [yellow]maria athens[/] — two terms again, but this")
			.AddLine("source answers by [bold]narrowing itself[/] instead of naming rows.")
			.AddEmptyLine()
			.AddLine("[dim]Nothing is materialised: no map is allocated and the table reads cells only[/]")
			.AddLine("[dim]for rows it is about to paint. This is what to do for a big source.[/]")
			.AddEmptyLine()
			.AddLine("[dim]Terms match anywhere in a cell, so[/] [yellow]ns[/] [dim]finds \"Athens\". Space = AND.[/]")
			.AddLine("[dim]Scope one to a column with[/] [yellow]city:athens[/][dim], which the source turns into[/]")
			.AddLine("[dim]a condition on that column alone.[/]")
			.StickyTop()
			.Build();

		var table = new RemoteTable
		{
			DataSource = source,
			FilteringEnabled = true,
			ReadOnly = false,
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};

		return Controls.ScrollablePanel()
			.AddControl(header)
			.AddControl(table)
			.AddControl(status)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
	}

	/// <summary>Stands in for a source whose filter becomes a server-side WHERE.</summary>
	private sealed class RemoteSource : ITableDataSource
	{
		// Chosen so each typed character visibly narrows: 'm' matches three rows (Amsterdam among
		// them), 'ma' only the two marias. Data where 'm' and 'ma' matched the same rows made live
		// filtering look broken, because the first keystroke appeared to do nothing.
		private static readonly string[][] AllRows =
		[
			["maria", "Athens", "active"],
			["nikos", "Berlin", "active"],
			["petros", "Amsterdam", "paused"],
			["anna", "Cairo", "active"],
			["maria", "Dublin", "paused"],
			["kostas", "Athens", "active"],
		];

		private List<string[]> _visible = [.. AllRows];

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		/// <summary>Raised with the query text so the demo can show what the source did.</summary>
		public event EventHandler<string>? QueryRan;

		/// <summary>Raised when the filter goes away, so the status line stops describing it.</summary>
		public event EventHandler? QueryCleared;

		public bool CanFilter => true;
		// NARROWED: after a filter this reports only matches, so the table needs no map.
		public int RowCount => _visible.Count;
		public int ColumnCount => 3;
		public string GetColumnHeader(int c) => c switch { 0 => "User", 1 => "City", _ => "State" };
		public string GetCellValue(int r, int c) => _visible[r][c];
		public object? GetRowTag(int rowIndex) => null;

		public void ApplyFilter(string filterText, string? columnName, FilterOperator op)
			=> RunQuery([new Condition(filterText, columnName)]);

		/// <summary>One condition of the query: a value, optionally scoped to a column.</summary>
		public readonly record struct Condition(string Value, string? ColumnName);

		/// <summary>
		/// Every condition ANDed together, the way a WHERE clause would. A condition naming a
		/// column is tested against that column alone, which is what makes "city:athens" mean
		/// something different from a bare "athens".
		/// </summary>
		public void RunQuery(IReadOnlyList<Condition> conditions)
		{
			_visible = AllRows.Where(row => conditions.All(c => Matches(row, c))).ToList();

			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			QueryRan?.Invoke(this, string.Join(" AND ", conditions.Select(Describe)));
		}

		private bool Matches(string[] row, Condition condition)
		{
			if (condition.ColumnName == null)
				return row.Any(cell => cell.Contains(condition.Value, StringComparison.OrdinalIgnoreCase));

			int column = IndexOfColumn(condition.ColumnName);
			// An unknown column matches nothing, rather than silently widening to every column.
			if (column < 0) return false;

			return row[column].Contains(condition.Value, StringComparison.OrdinalIgnoreCase);
		}

		private int IndexOfColumn(string name)
		{
			for (int c = 0; c < ColumnCount; c++)
				if (string.Equals(GetColumnHeader(c), name, StringComparison.OrdinalIgnoreCase))
					return c;
			return -1;
		}

		private static string Describe(Condition c)
			=> c.ColumnName == null ? $"any LIKE '{c.Value}'" : $"{c.ColumnName} LIKE '{c.Value}'";

		public void ClearFilter()
		{
			_visible = [.. AllRows];
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			QueryCleared?.Invoke(this, EventArgs.Empty);
		}
	}

	private sealed class RemoteTable : TableControl
	{
		protected override TableFilterResult TryApplyFilterToDataSource(
			ITableDataSource dataSource, CompoundFilterExpression compound)
		{
			if (dataSource is not RemoteSource remote)
				return base.TryApplyFilterToDataSource(dataSource, compound);

			// The whole compound expression goes to the source as one query, instead of the
			// default keeping everything but a single term client-side.
			// Each term keeps its column prefix, so "city:athens" reaches the source as a
			// condition on City rather than a bare text search.
			remote.RunQuery([.. compound.Terms.Select(t =>
				new RemoteSource.Condition(t.Alternatives[0].Value, t.Alternatives[0].ColumnName))]);
			return TableFilterResult.SourceNarrowed;
		}
	}

	#endregion
}

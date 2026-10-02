// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// What a data source did with a filter it was offered, returned from
/// <see cref="TableControl.TryApplyFilterToDataSource"/>.
/// </summary>
/// <remarks>
/// <para>
/// A struct with named factories rather than a bool, because the table has to know not only
/// WHETHER the source applied the filter but HOW the result is addressed afterwards: a source that
/// narrowed itself is read by identity, while one that supplied display rows is read through a map.
/// Those are different contracts and a bool cannot carry the difference.
/// </para>
/// <para>
/// It is also the growth seam. The outcome set can gain a case without changing the signature of
/// an overridable method that subclasses already implement.
/// </para>
/// </remarks>
public readonly struct TableFilterResult
{
	private readonly IReadOnlyList<int>? _displayRows;

	private TableFilterResult(TableFilterOutcome outcome, IReadOnlyList<int>? displayRows)
	{
		Outcome = outcome;
		_displayRows = displayRows;
	}

	/// <summary>Which of the three outcomes this result represents.</summary>
	public TableFilterOutcome Outcome { get; }

	/// <summary>
	/// The data-row indices to display, in display order. Non-null only when
	/// <see cref="Outcome"/> is <see cref="TableFilterOutcome.DisplayRowsSupplied"/>.
	/// </summary>
	public IReadOnlyList<int>? DisplayRows => _displayRows;

	/// <summary>
	/// The source did not apply the filter, so the table filters client-side: it walks every row
	/// calling <c>GetCellValue</c> and builds the display map itself. This is the default and is
	/// always correct, merely not always cheap.
	/// </summary>
	public static TableFilterResult NotHandled { get; } = new(TableFilterOutcome.NotHandled, null);

	/// <summary>
	/// The source applied the filter by narrowing ITSELF: its <c>RowCount</c> now reports only
	/// matching rows, and row <c>n</c> means the <c>n</c>th matching row.
	/// </summary>
	/// <remarks>
	/// The option to prefer for large, remote or virtualized sources. Nothing is materialised: the
	/// table holds no map, asks the source for its count, and reads cells only for the rows it is
	/// about to paint. A source that keeps reporting every row must NOT return this — the table
	/// would address rows by identity and show the unfiltered set with no error anywhere.
	/// </remarks>
	public static TableFilterResult SourceNarrowed { get; } = new(TableFilterOutcome.SourceNarrowed, null);

	/// <summary>
	/// The source applied the filter and is naming exactly which rows to show, in which order,
	/// while continuing to report all of them from <c>RowCount</c>.
	/// </summary>
	/// <param name="dataIndices">
	/// Data-row indices in display order. Held as given, not copied, so it must not be mutated
	/// afterwards. May be empty, which means the filter matched nothing.
	/// </param>
	/// <remarks>
	/// <para>
	/// For sources whose visible set is not a subset its own <c>RowCount</c> can express — a
	/// hierarchy keeping a non-matching parent visible because a child matched, say, or any source
	/// wanting a display order of its own.
	/// </para>
	/// <para>
	/// COSTS, AND WHY <see cref="SourceNarrowed"/> IS PREFERRED FOR BIG SOURCES. This is the one
	/// allocation in an interface built to avoid them: one <c>int</c> per matching row, held for
	/// as long as the filter is active. The source must also enumerate its COMPLETE match set
	/// before the table paints anything, because the table takes its row count from the length of
	/// this list — so a remote source cannot answer with a first page and fetch the rest later.
	/// Cell reads do stay lazy: the table still calls <c>GetCellValue</c> only for visible rows.
	/// Suited to bounded in-memory sources; for millions of rows, narrow instead.
	/// </para>
	/// </remarks>
	public static TableFilterResult WithDisplayRows(IReadOnlyList<int> dataIndices)
	{
		ArgumentNullException.ThrowIfNull(dataIndices);
		return new TableFilterResult(TableFilterOutcome.DisplayRowsSupplied, dataIndices);
	}
}

/// <summary>
/// The outcomes a <see cref="TableFilterResult"/> can carry.
/// </summary>
public enum TableFilterOutcome
{
	/// <summary>The source declined; the table filters client-side.</summary>
	NotHandled,

	/// <summary>The source narrowed its own row set; the table addresses rows by identity.</summary>
	SourceNarrowed,

	/// <summary>The source supplied the display rows; the table addresses rows through that map.</summary>
	DisplayRowsSupplied,
}

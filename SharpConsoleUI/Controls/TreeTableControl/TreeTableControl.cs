// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// A table whose rows nest: each row can hold rows of its own, shown indented under it in the tree
/// column and expanded or collapsed in place, while every column keeps the table's layout, sorting,
/// filtering, selection and editing.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="TableControl"/>, built only on the table's public and protected members, so code
/// written against a table works on a tree table too. Its inherited members keep their meaning, read
/// in terms of the hierarchy:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="TableControl.Rows"/>, <see cref="TableControl.GetRow"/>, the cell
/// accessors and <see cref="TableControl.GetRowTagAt"/> address every row, depth-first in sibling
/// order, whatever is expanded or filtered.</description></item>
/// <item><description><see cref="TableControl.RowCount"/>, the selection and the row events count the
/// rows displayed.</description></item>
/// <item><description>The add and insert overloads add roots; a <see cref="TreeTableRow"/> brings the
/// rows nested under it, and a plain <see cref="TableRow"/> is a root without children. An insert
/// lands at the first root at or after the index, so a flat tree table inserts exactly as a table
/// does.</description></item>
/// <item><description><see cref="TableControl.RemoveRow(int)"/> removes the row at that position
/// together with the rows nested under it; <see cref="TableControl.ClearRows"/> and
/// <see cref="TableControl.SetData"/> clear and replace the roots.</description></item>
/// <item><description>A <see cref="TableControl.DataSource"/> makes it a flat virtual table, exactly as
/// a table; the tree members throw <see cref="InvalidOperationException"/> while one is set.</description></item>
/// </list>
/// <para>
/// A row is in one place at a time: adding a row that already has a parent or is already in a table
/// throws, as does nesting a row under itself.
/// </para>
/// </remarks>
public partial class TreeTableControl : TableControl
{
	/// <summary>Initializes a new instance of the <see cref="TreeTableControl"/> class.</summary>
	public TreeTableControl()
	{
	}

	/// <summary>
	/// The rows at the top of the hierarchy, in order: <see cref="TreeTableRow"/>s, and any plain
	/// <see cref="TableRow"/>s added as rows without children.
	/// </summary>
	public IReadOnlyList<TableRow> RootRows
	{
		get { lock (SyncRoot) { return _roots.ToList().AsReadOnly(); } }
	}

	/// <summary>The binding to items made by <c>BindItems</c>, replaced by the next one.</summary>
	internal DataBinding.ITreeTableItemsBinding? ItemsBinding { get; set; }

	/// <summary>
	/// Creates a new TreeTableControlBuilder for fluent configuration.
	/// </summary>
	public static new Builders.TreeTableControlBuilder Create() => new Builders.TreeTableControlBuilder();

	/// <inheritdoc/>
	protected override void OnDisposing()
	{
		base.OnDisposing();

		RowExpansionChanging = null;
		RowExpansionChanged = null;
		RowExpansionChangedAsync = null;
	}
}

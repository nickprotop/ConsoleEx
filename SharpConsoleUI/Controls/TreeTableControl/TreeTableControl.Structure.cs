// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

public partial class TreeTableControl
{
	#region Fields

	// The hierarchy: the roots, each a TreeTableRow or a plain TableRow without children. Guarded by
	// SyncRoot, like the rows the table stores.
	private readonly List<TableRow> _roots = new();

	// Plain TableRows in the table, which carry no parent or table of their own to check against.
	private readonly HashSet<TableRow> _plainRows = new(ReferenceEqualityComparer.Instance);

	// What the table was last given: every row, depth-first in sibling order. Data indices refer to
	// this list until the next change to the hierarchy is handed over.
	private List<TableRow> _flattened = new();

	// Batching: nested BatchUpdate calls, whether the hierarchy changed since it was handed over, and
	// whether only what is displayed of it changed — a row expanded, say.
	private int _batchDepth;
	private bool _structureDirty;
	private bool _viewDirty;

	#endregion

	#region Building the Hierarchy

	/// <summary>Adds a root row with the given cells, created by <see cref="CreateTreeRow"/>.</summary>
	/// <param name="cells">The new row's cell values.</param>
	/// <returns>The new row.</returns>
	public TreeTableRow AddRootRow(params string[] cells)
	{
		var row = CreateRowFor(cells);
		InsertRootRow(int.MaxValue, row);
		return row;
	}

	/// <summary>Inserts a row, with the rows nested under it, among the roots.</summary>
	/// <param name="rootIndex">The root position, clamped to [0, the number of roots].</param>
	/// <param name="row">A row without a parent and not in a table.</param>
	/// <returns><paramref name="row"/>.</returns>
	public TreeTableRow InsertRootRow(int rootIndex, TreeTableRow row)
	{
		ThrowIfDataSource();
		ThrowIfCannotAddRoot(row);

		ChangeStructure(() =>
		{
			_roots.Insert(Math.Clamp(rootIndex, 0, _roots.Count), row);
			Attach(row);
		});
		return row;
	}

	/// <summary>Removes a row, at any depth, together with the rows nested under it.</summary>
	/// <param name="row">The row to remove.</param>
	/// <returns>False when the row is not in this table.</returns>
	public bool RemoveRow(TableRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		ThrowIfDataSource();
		if (!Contains(row)) return false;

		RemoveTreeRow(row);
		return true;
	}

	/// <summary>
	/// Moves a row, with the rows nested under it, to another place in the hierarchy, keeping it the
	/// same row: its selection, expansion and tag go with it.
	/// </summary>
	/// <param name="row">A row in this table.</param>
	/// <param name="newParent">The row to nest it under, or null to make it a root.</param>
	/// <param name="index">The position among its new siblings, clamped, counted without the row itself.</param>
	/// <remarks>
	/// A plain <see cref="TableRow"/> has no place for children and can only move among the roots.
	/// Moving a row under itself or one of its own descendants throws <see cref="InvalidOperationException"/>.
	/// </remarks>
	public void MoveRow(TableRow row, TreeTableRow? newParent, int index)
	{
		ArgumentNullException.ThrowIfNull(row);
		ThrowIfDataSource();
		if (!Contains(row))
			throw new InvalidOperationException("The row is not in this table.");
		if (newParent != null && newParent.Table != this)
			throw new InvalidOperationException("The new parent is not in this table.");
		if (newParent != null && row is not TreeTableRow)
			throw new InvalidOperationException("A plain TableRow can only be a root; use a TreeTableRow to nest it.");

		if (newParent != null)
		{
			for (var ancestor = newParent; ancestor != null; ancestor = ancestor.Parent)
			{
				if (ReferenceEquals(ancestor, row))
					throw new InvalidOperationException("A row cannot be moved under itself or one of its own descendants.");
			}
		}

		ChangeStructure(() =>
		{
			Unlink(row);
			if (newParent == null)
				_roots.Insert(Math.Clamp(index, 0, _roots.Count), row);
			else
				newParent.AttachChild(Math.Clamp(index, 0, newParent.ChildList.Count), (TreeTableRow)row);
		});
	}

	/// <summary>
	/// Makes changes to the hierarchy and rows as one: the table recomputes and redisplays once, at
	/// the end, however many rows were added, removed or moved.
	/// </summary>
	/// <param name="update">The changes to make.</param>
	/// <remarks>
	/// <para>
	/// Every change to the hierarchy otherwise recomputes the displayed rows on its own, which is
	/// right for one change and wasteful for a thousand: loading a hierarchy row by row inside a batch
	/// costs one recompute instead of one per row. Building a subtree detached and adding it in one
	/// step is cheaper still.
	/// </para>
	/// <para>
	/// Batches nest; only the outermost one ends the batch. Until it ends, <see cref="TableControl.Rows"/>,
	/// <see cref="TableControl.RowCount"/> and the display show the table as it was before the batch
	/// began. If <paramref name="update"/> throws, the changes made so far are still applied.
	/// </para>
	/// </remarks>
	public void BatchUpdate(Action update)
	{
		ArgumentNullException.ThrowIfNull(update);

		_batchDepth++;
		try
		{
			update();
		}
		finally
		{
			if (--_batchDepth == 0)
			{
				FlushStructure();
				if (_viewDirty)
				{
					_viewDirty = false;
					RefreshDisplayRows();
				}
			}
		}
	}

	#endregion

	#region Asking About the Hierarchy

	/// <summary>The row a row is nested under, or null for a root or a row not in this table.</summary>
	public TreeTableRow? GetParentRow(TableRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		return row is TreeTableRow treeRow && treeRow.Table == this ? treeRow.Parent : null;
	}

	/// <summary>How many rows a row is nested under: 0 for a root, -1 for a row not in this table.</summary>
	public int GetDepth(TableRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		if (!Contains(row)) return -1;
		return row is TreeTableRow treeRow ? treeRow.Depth : 0;
	}

	/// <summary>
	/// The first row, depth-first in sibling order, whose <see cref="TableRow.Tag"/> equals
	/// <paramref name="tag"/>, collapsed or filtered out or not; null when there is none.
	/// </summary>
	public TableRow? FindRowByTag(object tag)
	{
		ArgumentNullException.ThrowIfNull(tag);
		lock (SyncRoot)
		{
			foreach (var row in Flatten())
			{
				if (tag.Equals(row.Tag))
					return row;
			}
		}
		return null;
	}

	#endregion

	#region The Inherited Row API

	// The table's own mutators — every AddRow, InsertRow, RemoveRow, ClearRows and SetData overload —
	// arrive here. They are read in terms of the hierarchy, and the hierarchy is then handed to the
	// table as one depth-first list.

	/// <summary>Creates the rows the table's text overloads add, through <see cref="CreateTreeRow"/>.</summary>
	protected sealed override TableRow CreateRow(string[] cells) => CreateRowFor(cells);

	/// <summary>
	/// Creates the <see cref="TreeTableRow"/> for cell text added through this table: by
	/// <see cref="AddRootRow"/>, <see cref="TreeTableRow.AddChild(string[])"/> on a row in this table,
	/// and the inherited <c>AddRow(string[])</c> and <c>InsertRow(int, string[])</c>.
	/// </summary>
	/// <param name="cells">The new row's cell values.</param>
	/// <returns>A new, detached row.</returns>
	/// <remarks>
	/// Override to give the table's rows a type of your own, carrying more than cells.
	/// </remarks>
	protected virtual TreeTableRow CreateTreeRow(IReadOnlyList<string> cells) => new TreeTableRow(cells);

	/// <summary>Adds the rows as roots, at the first root at or after <paramref name="index"/> in <see cref="TableControl.Rows"/>.</summary>
	protected override void InsertRowsCore(int index, IReadOnlyList<TableRow> rows)
	{
		ThrowIfDataSource();
		var adding = new HashSet<TableRow>(ReferenceEqualityComparer.Instance);
		foreach (var row in rows)
		{
			ThrowIfCannotAddRoot(row);
			if (!adding.Add(row))
				throw new InvalidOperationException("The same row cannot be added twice.");
		}

		ChangeStructure(() =>
		{
			int rootIndex = GetRootIndexAtOrAfter(index);
			foreach (var row in rows)
			{
				_roots.Insert(rootIndex++, row);
				Attach(row);
			}
		});
	}

	/// <summary>Removes the rows at those positions in <see cref="TableControl.Rows"/>, with the rows nested under them.</summary>
	protected override void RemoveRowsCore(int index, int count)
	{
		var doomed = _flattened.Skip(index).Take(count).ToList();
		ChangeStructure(() =>
		{
			foreach (var row in doomed)
			{
				// A row nested under one removed before it has gone with it.
				if (Contains(row))
				{
					Unlink(row);
					Detach(row);
				}
			}
		});
	}

	/// <summary>Replaces the roots with <paramref name="rows"/>, each bringing the rows nested under it.</summary>
	protected override void SetDataCore(IReadOnlyList<TableRow> rows)
	{
		var adding = new HashSet<TableRow>(ReferenceEqualityComparer.Instance);
		foreach (var row in rows)
		{
			if (!adding.Add(row))
				throw new InvalidOperationException("The same row cannot be added twice.");
			if (!Contains(row))
				ThrowIfCannotAddRoot(row);
		}

		ChangeStructure(() =>
		{
			foreach (var root in _roots.ToList())
			{
				if (!adding.Contains(root))
					Detach(root);
			}
			_roots.Clear();

			foreach (var row in rows)
			{
				if (row is TreeTableRow { Parent: not null } nested)
					nested.Parent!.DetachChild(nested);
				_roots.Add(row);
				Attach(row);
			}
		});
	}

	#endregion

	#region Changing the Hierarchy

	/// <summary>Creates a row for cell text, through <see cref="CreateTreeRow"/>.</summary>
	internal TreeTableRow CreateRowFor(string[] cells) => CreateTreeRow(cells);

	/// <summary>Nests <paramref name="child"/> under <paramref name="parent"/>, a row in this table.</summary>
	internal void InsertChildRow(TreeTableRow parent, int index, TreeTableRow child)
	{
		ThrowIfDataSource();
		ChangeStructure(() =>
		{
			parent.AttachChild(Math.Clamp(index, 0, parent.ChildList.Count), child);
			Attach(child);
		});
	}

	/// <summary>Removes a row in this table, with the rows nested under it.</summary>
	internal void RemoveTreeRow(TableRow row)
	{
		ThrowIfDataSource();
		ChangeStructure(() =>
		{
			Unlink(row);
			Detach(row);
		});
	}

	/// <summary>Removes every child of a row in this table.</summary>
	internal void ClearChildRows(TreeTableRow parent)
	{
		ThrowIfDataSource();
		ChangeStructure(() =>
		{
			foreach (var child in parent.ChildList)
				Detach(child);
			parent.DetachChildren();
		});
	}

	/// <summary>
	/// Something that decides what is displayed of the hierarchy, but not the hierarchy itself,
	/// changed: whether a row has children not loaded yet, say.
	/// </summary>
	internal void OnRowShapeChanged() => RefreshView();

	/// <summary>
	/// Recomputes what is displayed of the hierarchy — at once, or when the outermost batch ends.
	/// </summary>
	private void RefreshView()
	{
		if (_batchDepth > 0)
		{
			_viewDirty = true;
			return;
		}

		RefreshDisplayRows();
	}

	/// <summary>
	/// Changes the hierarchy under the lock, then hands it to the table — at once, or when the
	/// outermost batch ends.
	/// </summary>
	private void ChangeStructure(Action change)
	{
		lock (SyncRoot)
		{
			change();
			_structureDirty = true;
		}

		if (_batchDepth == 0)
			FlushStructure();
	}

	/// <summary>
	/// Hands the changed hierarchy to the table: every row, depth-first in sibling order.
	/// </summary>
	/// <remarks>
	/// Through the table's own <see cref="TableControl.SetDataCore"/>, so a whole change, however
	/// large, is one recompute, and the selection follows its rows by identity the way the table keeps
	/// it across any replacement of its rows.
	/// </remarks>
	private void FlushStructure()
	{
		List<TableRow> flattened;
		lock (SyncRoot)
		{
			if (!_structureDirty) return;

			_shape = TreeTableShape.Capture(_roots);
			flattened = _shape.Rows.ToList();
			_flattened = flattened;
			_structureDirty = false;
			_viewDirty = false;
		}

		_replacedView = _view;
		try
		{
			base.SetDataCore(flattened);
		}
		finally
		{
			_replacedView = null;
		}
	}

	/// <summary>Every row, depth-first in sibling order. Callers hold <see cref="TableControl.SyncRoot"/>.</summary>
	private List<TableRow> Flatten()
	{
		var rows = new List<TableRow>();
		foreach (var root in _roots)
			AddWithDescendants(root, rows);
		return rows;
	}

	private static void AddWithDescendants(TableRow row, List<TableRow> rows)
	{
		rows.Add(row);
		if (row is TreeTableRow treeRow)
		{
			foreach (var child in treeRow.ChildList)
				AddWithDescendants(child, rows);
		}
	}

	/// <summary>
	/// The root position of the first root whose row is at or after <paramref name="dataIndex"/> in
	/// the depth-first list, or the end. Callers hold <see cref="TableControl.SyncRoot"/>.
	/// </summary>
	private int GetRootIndexAtOrAfter(int dataIndex)
	{
		int position = 0;
		for (int root = 0; root < _roots.Count; root++)
		{
			if (position >= dataIndex)
				return root;
			position += CountWithDescendants(_roots[root]);
		}
		return _roots.Count;
	}

	private static int CountWithDescendants(TableRow row)
	{
		int count = 1;
		if (row is TreeTableRow treeRow)
		{
			foreach (var child in treeRow.ChildList)
				count += CountWithDescendants(child);
		}
		return count;
	}

	/// <summary>Whether a row is in this table, at any depth.</summary>
	private bool Contains(TableRow row)
		=> row is TreeTableRow treeRow ? treeRow.Table == this : _plainRows.Contains(row);

	/// <summary>Takes a row out of its parent's children or the roots, leaving it attached.</summary>
	private void Unlink(TableRow row)
	{
		if (row is TreeTableRow { Parent: { } parent } nested)
			parent.DetachChild(nested);
		else
			_roots.Remove(row);
	}

	/// <summary>Marks a row and the rows nested under it as in this table.</summary>
	private void Attach(TableRow row)
	{
		if (row is TreeTableRow treeRow)
		{
			treeRow.Table = this;
			foreach (var child in treeRow.ChildList)
				Attach(child);
		}
		else
		{
			_plainRows.Add(row);
		}
	}

	/// <summary>Marks a row and the rows nested under it as no longer in this table.</summary>
	private void Detach(TableRow row)
	{
		if (row is TreeTableRow treeRow)
		{
			treeRow.Table = null;
			foreach (var child in treeRow.ChildList)
				Detach(child);
		}
		else
		{
			_plainRows.Remove(row);
		}
	}

	/// <summary>Throws unless a row can become a root: not nested, and not in a table.</summary>
	private void ThrowIfCannotAddRoot(TableRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		bool attached = row is TreeTableRow treeRow
			? treeRow.Parent != null || treeRow.Table != null
			: _plainRows.Contains(row);
		if (attached)
			throw new InvalidOperationException("The row already belongs to a parent or a table; remove it from there first.");
	}

	/// <summary>Refuses the tree members while a data source makes the table flat.</summary>
	private void ThrowIfDataSource()
	{
		if (DataSource != null)
			throw new InvalidOperationException("The hierarchy cannot be used while DataSource is set; a data source makes the table flat.");
	}

	#endregion
}

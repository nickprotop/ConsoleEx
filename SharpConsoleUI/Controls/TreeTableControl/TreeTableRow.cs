// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// A row of a <see cref="TreeTableControl"/> that can hold rows nested under it.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="TableRow"/> with a parent and children: everything a table row has — cells, colours,
/// enabled state, tag — plus its place in a hierarchy and whether that hierarchy is open beneath it.
/// Rows can be built up detached, a whole subtree at a time, and added to a table in one step.
/// </para>
/// <para>
/// A row belongs to one parent and one table at a time. Adding a row that already has a parent, or
/// that is already in a table, throws <see cref="InvalidOperationException"/>, and so does nesting a
/// row under itself or one of its own descendants. Once in a table, changes to the hierarchy made
/// through the row — adding, inserting or removing children, expanding — are applied to the table
/// at once.
/// </para>
/// </remarks>
public class TreeTableRow : TableRow
{
	private readonly List<TreeTableRow> _children = new();
	private bool _isExpanded = true;
	private bool _hasUnrealizedChildren;

	/// <summary>
	/// Initializes a new instance of the <see cref="TreeTableRow"/> class with no cells.
	/// </summary>
	public TreeTableRow()
	{
		Children = _children.AsReadOnly();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TreeTableRow"/> class with the specified cells.
	/// </summary>
	public TreeTableRow(params string[] cells) : base(cells)
	{
		Children = _children.AsReadOnly();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TreeTableRow"/> class with the specified cells.
	/// </summary>
	public TreeTableRow(IEnumerable<string> cells) : base(cells)
	{
		Children = _children.AsReadOnly();
	}

	/// <summary>
	/// The rows nested directly under this one, in order. A live, read-only view: it reflects later
	/// changes, which are made through <see cref="AddChild(TreeTableRow)"/> and its siblings.
	/// </summary>
	public IReadOnlyList<TreeTableRow> Children { get; }

	/// <summary>The row this one is nested under, or null for a root or a detached row.</summary>
	public TreeTableRow? Parent { get; private set; }

	/// <summary>How many rows this one is nested under: 0 for a root.</summary>
	public int Depth
	{
		get
		{
			int depth = 0;
			for (var parent = Parent; parent != null; parent = parent.Parent)
				depth++;
			return depth;
		}
	}

	/// <summary>
	/// Gets or sets whether the rows nested under this one are shown. Default true, as for
	/// <see cref="TreeNode.IsExpanded"/>.
	/// </summary>
	/// <remarks>
	/// The row's own, lasting state. Setting it on a row in a table expands or collapses the row there,
	/// raising <see cref="TreeTableControl.RowExpansionChanging"/>, which can cancel it, and
	/// <see cref="TreeTableControl.RowExpansionChanged"/>. While the table is filtered, what is
	/// displayed is the filtered view's, see <see cref="TreeTableControl.IsRowExpanded"/>; this state
	/// shows again when the filter is cleared.
	/// </remarks>
	public bool IsExpanded
	{
		get => _isExpanded;
		set
		{
			if (_isExpanded == value) return;

			if (Table != null)
				Table.SetPersistentExpansion(this, value);
			else
				_isExpanded = value;
		}
	}

	/// <summary>
	/// Gets or sets whether the row has children that have not been loaded yet, so it shows an
	/// expander although <see cref="Children"/> is empty.
	/// </summary>
	/// <remarks>
	/// For loading a hierarchy on demand: set it on a row whose children are expensive to fetch, add
	/// them from <see cref="TreeTableControl.RowExpansionChanging"/> when the row is first expanded,
	/// and set it back to false. Children added there are displayed together with the expansion.
	/// </remarks>
	public bool HasUnrealizedChildren
	{
		get => _hasUnrealizedChildren;
		set
		{
			if (_hasUnrealizedChildren == value) return;

			_hasUnrealizedChildren = value;
			Table?.OnRowShapeChanged();
		}
	}

	/// <summary>The table this row is in, or null while detached.</summary>
	internal TreeTableControl? Table { get; set; }

	/// <summary>The live children, for the table's own bookkeeping.</summary>
	internal List<TreeTableRow> ChildList => _children;

	/// <summary>Adds a child row with the given cells, created by the table's row factory when in one.</summary>
	/// <param name="cells">The new row's cell values.</param>
	/// <returns>The new row.</returns>
	public TreeTableRow AddChild(params string[] cells)
		=> AddChild(Table != null ? Table.CreateRowFor(cells) : new TreeTableRow(cells));

	/// <summary>Adds a row, with any rows nested under it, as this row's last child.</summary>
	/// <param name="child">A row without a parent and not in a table.</param>
	/// <returns><paramref name="child"/>.</returns>
	public TreeTableRow AddChild(TreeTableRow child) => InsertChild(_children.Count, child);

	/// <summary>Inserts a row, with any rows nested under it, among this row's children.</summary>
	/// <param name="index">The child position, clamped to [0, the number of children].</param>
	/// <param name="child">A row without a parent and not in a table.</param>
	/// <returns><paramref name="child"/>.</returns>
	public TreeTableRow InsertChild(int index, TreeTableRow child)
	{
		ThrowIfCannotAdopt(child);

		if (Table != null)
			Table.InsertChildRow(this, index, child);
		else
			AttachChild(Math.Clamp(index, 0, _children.Count), child);

		return child;
	}

	/// <summary>Removes one of this row's children, with the rows nested under it.</summary>
	/// <param name="child">The child to remove.</param>
	/// <returns>False when <paramref name="child"/> is not a child of this row.</returns>
	public bool RemoveChild(TreeTableRow child)
	{
		ArgumentNullException.ThrowIfNull(child);
		if (child.Parent != this) return false;

		if (Table != null)
			Table.RemoveTreeRow(child);
		else
			DetachChild(child);

		return true;
	}

	/// <summary>Removes every child of this row, with the rows nested under them.</summary>
	public void ClearChildren()
	{
		if (_children.Count == 0) return;

		if (Table != null)
			Table.ClearChildRows(this);
		else
			DetachChildren();
	}

	/// <summary>
	/// Throws unless <paramref name="child"/> may be nested under this row: it has no parent, is in no
	/// table, and is neither this row nor one of its ancestors.
	/// </summary>
	internal void ThrowIfCannotAdopt(TreeTableRow child)
	{
		ArgumentNullException.ThrowIfNull(child);
		if (child.Parent != null || child.Table != null)
			throw new InvalidOperationException("The row already belongs to a parent or a table; remove it from there first.");

		for (var ancestor = this; ancestor != null; ancestor = ancestor.Parent)
		{
			if (ReferenceEquals(ancestor, child))
				throw new InvalidOperationException("A row cannot be nested under itself or one of its own descendants.");
		}
	}

	/// <summary>Links a child in, without telling any table.</summary>
	internal void AttachChild(int index, TreeTableRow child)
	{
		_children.Insert(index, child);
		child.Parent = this;
	}

	/// <summary>Unlinks a child, without telling any table.</summary>
	internal void DetachChild(TreeTableRow child)
	{
		_children.Remove(child);
		child.Parent = null;
	}

	/// <summary>Unlinks every child, without telling any table.</summary>
	internal void DetachChildren()
	{
		foreach (var child in _children)
			child.Parent = null;
		_children.Clear();
	}

	/// <summary>Sets the lasting expansion state, without telling any table.</summary>
	internal void SetExpandedState(bool isExpanded) => _isExpanded = isExpanded;
}

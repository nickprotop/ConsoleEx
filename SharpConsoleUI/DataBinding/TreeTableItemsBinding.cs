// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;

namespace SharpConsoleUI.DataBinding;

/// <summary>
/// A binding of a <see cref="TreeTableControl"/> to a hierarchy of items, whatever the item type.
/// </summary>
internal interface ITreeTableItemsBinding : IDisposable
{
	/// <summary>Stops following the items and removes the rows the binding created.</summary>
	void Release();
}

/// <summary>
/// Keeps a <see cref="TreeTableControl"/>'s rows in step with a hierarchy of items: one row per
/// item, created and removed as the item collections change, its cells updated as the item does.
/// </summary>
/// <remarks>
/// <para>
/// Every bound collection — the roots, and each item's children — is followed by a list of the rows
/// created for it, in the collection's order, so a change at an index is applied to the row at that
/// index without searching. A collection that raises
/// <see cref="INotifyCollectionChanged.CollectionChanged"/> is applied change by change: an add
/// inserts rows, a remove removes them, a move moves the same row, keeping its selection and
/// expansion, and a replace keeps the row when the new item equals the old. A reset, or a change the
/// list cannot follow, is applied by comparing the collection with the rows: rows whose items are
/// still there are kept and moved into place, the rest are removed, and new items get new rows.
/// Each change is one batch, so one recompute.
/// </para>
/// <para>
/// Only the rows the binding created are touched: rows added to the table in other ways keep their
/// places among them. The binding marshals nothing, as the library's other bindings do not: raise
/// the changes on the UI thread.
/// </para>
/// </remarks>
internal sealed class TreeTableItemsBinding<T> : ITreeTableItemsBinding where T : class
{
	private readonly TreeTableControl _table;
	private readonly Func<T, IEnumerable<T>?> _childrenOf;
	private readonly Func<T, IEnumerable<string>> _cellsOf;
	private readonly TreeTableItemsOptions<T> _options;
	private readonly IEqualityComparer<T> _comparer;
	private readonly Siblings _roots;
	private readonly Dictionary<TreeTableRow, Node> _nodes = new(ReferenceEqualityComparer.Instance);
	private bool _isDisposed;

	public TreeTableItemsBinding(
		TreeTableControl table,
		IEnumerable<T> roots,
		Func<T, IEnumerable<T>?> childrenOf,
		Func<T, IEnumerable<string>> cellsOf,
		TreeTableItemsOptions<T> options)
	{
		_table = table;
		_childrenOf = childrenOf;
		_cellsOf = cellsOf;
		_options = options;
		_comparer = options.ItemComparer ?? EqualityComparer<T>.Default;
		_roots = new Siblings(null, roots);

		_table.BatchUpdate(() =>
		{
			foreach (var item in roots)
			{
				var node = CreateNode(item);
				_roots.Nodes.Add(node);
				_table.AddRootRow(node.Row);
			}
		});
		Follow(_roots);

		_table.RowExpansionChanging += OnRowExpansionChanging;
		_table.RowExpansionChanged += OnRowExpansionChanged;
	}

	#region Lifetime

	public void Release()
	{
		if (_isDisposed) return;

		var rows = _roots.Nodes.Select(node => node.Row).ToList();
		Dispose();
		_table.BatchUpdate(() =>
		{
			foreach (var row in rows)
				_table.RemoveRow(row);
		});
	}

	public void Dispose()
	{
		if (_isDisposed) return;
		_isDisposed = true;

		_table.RowExpansionChanging -= OnRowExpansionChanging;
		_table.RowExpansionChanged -= OnRowExpansionChanged;
		Unfollow(_roots);
		foreach (var node in _roots.Nodes)
			Forget(node);
	}

	#endregion

	#region Rows for Items

	/// <summary>A row the binding created, and the item it shows.</summary>
	private sealed class Node
	{
		public Node(T item, TreeTableRow row)
		{
			Item = item;
			Row = row;
		}

		public T Item { get; set; }

		public TreeTableRow Row { get; }

		/// <summary>The item's children, once asked for.</summary>
		public Siblings? Children { get; set; }

		public PropertyChangedEventHandler? ItemHandler { get; set; }
	}

	/// <summary>A bound collection, and the rows created for it in its order.</summary>
	private sealed class Siblings
	{
		public Siblings(TreeTableRow? parent, IEnumerable<T> source)
		{
			Parent = parent;
			Source = source;
		}

		/// <summary>The row the collection's rows are nested under, or null for the roots.</summary>
		public TreeTableRow? Parent { get; }

		public IEnumerable<T> Source { get; set; }

		public List<Node> Nodes { get; } = new();

		public NotifyCollectionChangedEventHandler? Handler { get; set; }
	}

	/// <summary>
	/// Creates the row for an item, detached, with the rows of its children unless they load on
	/// demand, and starts following the item and its children.
	/// </summary>
	private Node CreateNode(T item)
	{
		var row = new TreeTableRow(_cellsOf(item)) { Tag = item };
		if (_options.IsExpanded != null)
			row.IsExpanded = _options.IsExpanded(item);

		var node = new Node(item, row);
		_nodes[row] = node;

		// A row whose children load on demand starts closed, unless told otherwise: opening it is
		// what loads them, and one that starts open loads them now.
		bool loadsOnDemand = _options.HasUnrealizedChildren?.Invoke(item) == true;
		if (loadsOnDemand && _options.IsExpanded == null)
			row.IsExpanded = false;

		if (loadsOnDemand && !row.IsExpanded)
			row.HasUnrealizedChildren = true;
		else
			RealizeChildren(node);

		_options.UpdateRow?.Invoke(row, item);
		FollowItem(node);
		return node;
	}

	/// <summary>Asks for an item's children and adds their rows under its row.</summary>
	private void RealizeChildren(Node node)
	{
		var children = new Siblings(node.Row, _childrenOf(node.Item) ?? Array.Empty<T>());
		node.Children = children;
		foreach (var child in children.Source)
		{
			var childNode = CreateNode(child);
			children.Nodes.Add(childNode);
			node.Row.AddChild(childNode.Row);
		}
		Follow(children);
	}

	/// <summary>Stops following an item and everything under it.</summary>
	private void Forget(Node node)
	{
		if (node.ItemHandler != null && node.Item is INotifyPropertyChanged notifying)
			notifying.PropertyChanged -= node.ItemHandler;
		node.ItemHandler = null;

		if (node.Children != null)
		{
			Unfollow(node.Children);
			foreach (var child in node.Children.Nodes)
				Forget(child);
		}
		_nodes.Remove(node.Row);
	}

	#endregion

	#region Following Collections

	private void Follow(Siblings siblings)
	{
		if (siblings.Source is not INotifyCollectionChanged notifying) return;

		siblings.Handler = (_, e) => OnCollectionChanged(siblings, e);
		notifying.CollectionChanged += siblings.Handler;
	}

	private static void Unfollow(Siblings siblings)
	{
		if (siblings.Handler != null && siblings.Source is INotifyCollectionChanged notifying)
			notifying.CollectionChanged -= siblings.Handler;
		siblings.Handler = null;
	}

	private void OnCollectionChanged(Siblings siblings, NotifyCollectionChangedEventArgs e)
	{
		if (_isDisposed) return;

		_table.BatchUpdate(() =>
		{
			if (!TryApplyChange(siblings, e))
				Resynchronize(siblings);
		});
	}

	/// <summary>Applies one change at its index; false when the index cannot be trusted.</summary>
	private bool TryApplyChange(Siblings siblings, NotifyCollectionChangedEventArgs e)
	{
		var nodes = siblings.Nodes;
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add when e.NewItems != null && e.NewStartingIndex >= 0 && e.NewStartingIndex <= nodes.Count:
				for (int i = 0; i < e.NewItems.Count; i++)
					Insert(siblings, e.NewStartingIndex + i, (T)e.NewItems[i]!);
				return true;

			case NotifyCollectionChangedAction.Remove when e.OldItems != null && e.OldStartingIndex >= 0 && e.OldStartingIndex + e.OldItems.Count <= nodes.Count:
				for (int i = 0; i < e.OldItems.Count; i++)
					Remove(siblings, e.OldStartingIndex);
				return true;

			case NotifyCollectionChangedAction.Replace when e.NewItems != null && e.NewStartingIndex >= 0 && e.NewStartingIndex + e.NewItems.Count <= nodes.Count:
				for (int i = 0; i < e.NewItems.Count; i++)
					Replace(siblings, e.NewStartingIndex + i, (T)e.NewItems[i]!);
				return true;

			case NotifyCollectionChangedAction.Move when e.OldItems is { Count: 1 } && e.OldStartingIndex >= 0 && e.OldStartingIndex < nodes.Count
				&& e.NewStartingIndex >= 0 && e.NewStartingIndex < nodes.Count:
				Move(siblings, e.OldStartingIndex, e.NewStartingIndex);
				return true;

			default:
				return false;
		}
	}

	private void Insert(Siblings siblings, int index, T item)
	{
		var node = CreateNode(item);
		siblings.Nodes.Insert(index, node);
		int position = PositionFor(siblings, index, node.Row);
		if (siblings.Parent == null)
			_table.InsertRootRow(position, node.Row);
		else
			siblings.Parent.InsertChild(position, node.Row);
	}

	private void Remove(Siblings siblings, int index)
	{
		var node = siblings.Nodes[index];
		siblings.Nodes.RemoveAt(index);
		Forget(node);
		_table.RemoveRow(node.Row);
	}

	private void Replace(Siblings siblings, int index, T item)
	{
		var node = siblings.Nodes[index];
		if (_comparer.Equals(node.Item, item))
		{
			Repoint(node, item);
			return;
		}

		Remove(siblings, index);
		Insert(siblings, index, item);
	}

	private void Move(Siblings siblings, int from, int to)
	{
		var node = siblings.Nodes[from];
		siblings.Nodes.RemoveAt(from);
		siblings.Nodes.Insert(to, node);
		_table.MoveRow(node.Row, siblings.Parent, PositionFor(siblings, to, node.Row));
	}

	/// <summary>
	/// Brings the rows in line with the collection as it is now: rows whose items are still there
	/// are kept, in the collection's order; the others are removed; new items get new rows.
	/// </summary>
	private void Resynchronize(Siblings siblings)
	{
		var items = siblings.Source.ToList();

		var unclaimed = new Dictionary<T, Queue<Node>>(_comparer);
		foreach (var node in siblings.Nodes)
		{
			if (!unclaimed.TryGetValue(node.Item, out var queue))
				unclaimed[node.Item] = queue = new Queue<Node>();
			queue.Enqueue(node);
		}

		var kept = new Node?[items.Count];
		for (int i = 0; i < items.Count; i++)
		{
			if (unclaimed.TryGetValue(items[i], out var queue) && queue.Count > 0)
				kept[i] = queue.Dequeue();
		}

		foreach (var leftOver in unclaimed.Values.SelectMany(queue => queue))
		{
			Forget(leftOver);
			_table.RemoveRow(leftOver.Row);
		}

		siblings.Nodes.Clear();

		// Nothing new and nothing out of order, as after a reset that only removed or re-sent items:
		// the rows stay where they are.
		if (Array.TrueForAll(kept, node => node != null) && IsInOrder(siblings, kept!))
		{
			for (int i = 0; i < items.Count; i++)
			{
				siblings.Nodes.Add(kept[i]!);
				if (!ReferenceEquals(kept[i]!.Item, items[i]))
					Repoint(kept[i]!, items[i]);
			}
			return;
		}

		for (int i = 0; i < items.Count; i++)
		{
			if (kept[i] is not { } node)
			{
				Insert(siblings, i, items[i]);
				continue;
			}

			siblings.Nodes.Add(node);
			if (!ReferenceEquals(node.Item, items[i]))
				Repoint(node, items[i]);
			_table.MoveRow(node.Row, siblings.Parent, PositionFor(siblings, i, node.Row));
		}
	}

	/// <summary>Whether the rows already stand in this order among their siblings.</summary>
	private bool IsInOrder(Siblings siblings, Node[] nodes)
	{
		var position = new Dictionary<TableRow, int>(ReferenceEqualityComparer.Instance);
		int index = 0;
		foreach (var row in SiblingRowsOf(siblings))
			position[row] = index++;

		int last = -1;
		foreach (var node in nodes)
		{
			if (!position.TryGetValue(node.Row, out int current) || current <= last)
				return false;
			last = current;
		}
		return true;
	}

	/// <summary>
	/// Where the row for <c>siblings.Nodes[index]</c> goes among all of its parent's children,
	/// counted without the row itself: right after the bound row before it; for the first, right
	/// before the first bound row there is; else last. Rows the binding did not create keep their
	/// places.
	/// </summary>
	private int PositionFor(Siblings siblings, int index, TreeTableRow row)
	{
		var others = SiblingRowsOf(siblings).Where(other => !ReferenceEquals(other, row)).ToList();

		if (index > 0)
		{
			int previous = others.IndexOf(siblings.Nodes[index - 1].Row);
			if (previous >= 0) return previous + 1;
		}

		int firstBound = others.FindIndex(other => other is TreeTableRow treeRow && _nodes.ContainsKey(treeRow));
		return firstBound >= 0 ? firstBound : others.Count;
	}

	/// <summary>Every row under the collection's parent, bound or not, in order.</summary>
	private IEnumerable<TableRow> SiblingRowsOf(Siblings siblings)
		=> siblings.Parent != null ? siblings.Parent.Children : _table.RootRows;

	#endregion

	#region Following Items

	private void FollowItem(Node node)
	{
		if (node.Item is not INotifyPropertyChanged notifying) return;

		node.ItemHandler = (_, _) => OnItemChanged(node);
		notifying.PropertyChanged += node.ItemHandler;
	}

	/// <summary>Shows a different, equal item in a row, keeping the row.</summary>
	private void Repoint(Node node, T item)
	{
		if (node.ItemHandler != null && node.Item is INotifyPropertyChanged old)
			old.PropertyChanged -= node.ItemHandler;
		node.ItemHandler = null;

		node.Item = item;
		node.Row.Tag = item;
		FollowItem(node);
		Refresh(node);
	}

	private void OnItemChanged(Node node)
	{
		if (_isDisposed || !_nodes.ContainsKey(node.Row)) return;

		_table.BatchUpdate(() => Refresh(node));
	}

	/// <summary>Updates a row from its item: cells, expansion, and children if the collection was replaced.</summary>
	private void Refresh(Node node)
	{
		var row = node.Row;
		var cells = _cellsOf(node.Item).ToList();
		if (!row.Cells.SequenceEqual(cells))
			row.Cells = new ObservableCollection<string>(cells);

		if (_options.IsExpanded != null)
			row.IsExpanded = _options.IsExpanded(node.Item);

		if (node.Children != null)
		{
			var source = _childrenOf(node.Item) ?? Array.Empty<T>();
			if (!ReferenceEquals(source, node.Children.Source))
			{
				Unfollow(node.Children);
				node.Children.Source = source;
				Resynchronize(node.Children);
				Follow(node.Children);
			}
		}

		_options.UpdateRow?.Invoke(row, node.Item);
	}

	#endregion

	#region Following the Table

	/// <summary>Loads the children of an item whose row opens for the first time.</summary>
	private void OnRowExpansionChanging(object? sender, TreeTableRowExpansionChangingEventArgs e)
	{
		if (!e.IsExpanded || !_nodes.TryGetValue(e.Row, out var node) || node.Children != null)
			return;

		RealizeChildren(node);
		node.Row.HasUnrealizedChildren = false;
	}

	private void OnRowExpansionChanged(object? sender, TreeTableRowExpansionEventArgs e)
	{
		if (!e.IsFilteredView && _nodes.TryGetValue(e.Row, out var node))
			_options.IsExpandedChanged?.Invoke(node.Item, e.IsExpanded);
	}

	#endregion
}

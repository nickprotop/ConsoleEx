// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// The hierarchy of a <see cref="TreeTableControl"/> as it was last handed to the table: every row
/// by data index, with its parent and children by data index too.
/// </summary>
/// <remarks>
/// A snapshot, taken under the table's lock whenever the hierarchy is handed over and never changed
/// afterwards. The display is always computed from it, so a batch that has changed the live
/// hierarchy but not yet handed it over cannot make the display disagree with the rows the table
/// stores.
/// </remarks>
internal sealed class TreeTableShape
{
	/// <summary>A hierarchy without rows.</summary>
	internal static readonly TreeTableShape Empty = Capture(Array.Empty<TableRow>());

	private readonly Dictionary<TableRow, int> _dataIndexOf;

	private TreeTableShape(TableRow[] rows, int[] parents, int[][] children, int[] roots, int[] depths)
	{
		Rows = rows;
		Parents = parents;
		Children = children;
		Roots = roots;
		Depths = depths;
		_dataIndexOf = new Dictionary<TableRow, int>(rows.Length, ReferenceEqualityComparer.Instance);
		for (int i = 0; i < rows.Length; i++)
			_dataIndexOf[rows[i]] = i;
	}

	/// <summary>Every row, depth-first in sibling order: index i is data row i.</summary>
	internal TableRow[] Rows { get; }

	/// <summary>Each row's parent, or -1 for a root.</summary>
	internal int[] Parents { get; }

	/// <summary>Each row's children, in sibling order; empty for a leaf.</summary>
	internal int[][] Children { get; }

	/// <summary>The roots, in order.</summary>
	internal int[] Roots { get; }

	/// <summary>Each row's depth: 0 for a root.</summary>
	internal int[] Depths { get; }

	/// <summary>The number of rows.</summary>
	internal int Count => Rows.Length;

	/// <summary>A row's data index, or -1 when it is not in this hierarchy.</summary>
	internal int IndexOf(TableRow row) => _dataIndexOf.TryGetValue(row, out int index) ? index : -1;

	/// <summary>Takes a snapshot of a hierarchy. Callers hold the table's lock.</summary>
	/// <param name="roots">The roots, each a <see cref="TreeTableRow"/> or a plain <see cref="TableRow"/>.</param>
	internal static TreeTableShape Capture(IReadOnlyList<TableRow> roots)
	{
		var rows = new List<TableRow>();
		var parents = new List<int>();
		var depths = new List<int>();
		var children = new List<List<int>>();
		var rootIndices = new int[roots.Count];

		void Visit(TableRow row, int parent, int depth)
		{
			int index = rows.Count;
			rows.Add(row);
			parents.Add(parent);
			depths.Add(depth);
			children.Add(new List<int>());
			if (parent >= 0)
				children[parent].Add(index);

			if (row is TreeTableRow treeRow)
			{
				foreach (var child in treeRow.ChildList)
					Visit(child, index, depth + 1);
			}
		}

		for (int r = 0; r < roots.Count; r++)
		{
			rootIndices[r] = rows.Count;
			Visit(roots[r], -1, 0);
		}

		return new TreeTableShape(rows.ToArray(), parents.ToArray(),
			children.Select(list => list.Count == 0 ? Array.Empty<int>() : list.ToArray()).ToArray(),
			rootIndices, depths.ToArray());
	}
}

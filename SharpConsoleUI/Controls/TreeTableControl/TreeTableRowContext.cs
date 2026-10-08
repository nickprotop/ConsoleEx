// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// Where a row stands in what a <see cref="TreeTableControl"/> displays, for drawing the guide lines
/// and the expander in front of it.
/// </summary>
/// <remarks>
/// <para>
/// Describes the row as DISPLAYED, not as stored: whether it shows an expander and whether it is
/// open account for the filter, and whether it is the last of its siblings is counted among the
/// siblings displayed, in their sorted order. That is what guide lines must follow, or a line would
/// run on to a sibling the filter hid.
/// </para>
/// <para>
/// A struct passed by reference, so a decoration hook called for every painted row allocates
/// nothing to receive it.
/// </para>
/// </remarks>
public readonly struct TreeTableRowContext
{
	private readonly TreeTableProjection _view;

	internal TreeTableRowContext(TreeTableProjection view, int dataRowIndex, TreeGuide guide, string indent)
	{
		_view = view;
		DataRowIndex = dataRowIndex;
		Guide = guide;
		Indent = indent;
	}

	/// <summary>The row.</summary>
	public TableRow Row => _view.Shape.Rows[DataRowIndex];

	/// <summary>The row's data index, as <see cref="TableControl.GetRow"/> counts rows.</summary>
	public int DataRowIndex { get; }

	/// <summary>How many rows the row is nested under: 0 for a root.</summary>
	public int Depth => _view.Shape.Depths[DataRowIndex];

	/// <summary>Whether the row shows an expander: it has children to show, or children not loaded yet.</summary>
	public bool HasChildren => _view.HasChildren[DataRowIndex];

	/// <summary>Whether the row is open in what is displayed.</summary>
	public bool IsExpanded => _view.IsExpanded[DataRowIndex];

	/// <summary>Whether the row is the last of the siblings displayed with it.</summary>
	public bool IsLastSibling => _view.IsLastSibling[DataRowIndex];

	/// <summary>
	/// Whether some displayed row shows an expander, so that a row without one should leave the
	/// expander's width blank for its value to line up with its siblings'.
	/// </summary>
	public bool ShowsExpanderGutter => _view.AnyExpandable;

	/// <summary>The table's guide style.</summary>
	public TreeGuide Guide { get; }

	/// <summary>The table's indent, drawn after each ancestor level's guide line.</summary>
	public string Indent { get; }

	/// <summary>
	/// Whether the row's ancestor at <paramref name="depth"/> — or the row itself, at its own
	/// <see cref="Depth"/> — is the last of the siblings displayed with it. A guide line continues past
	/// an ancestor only when it is not.
	/// </summary>
	/// <param name="depth">A depth from 1 to <see cref="Depth"/>.</param>
	public bool IsLastSiblingAtDepth(int depth)
	{
		if (depth < 1 || depth > Depth)
			throw new ArgumentOutOfRangeException(nameof(depth), depth, "Expected a depth from 1 to the row's own depth.");

		int row = DataRowIndex;
		for (int level = Depth; level > depth; level--)
			row = _view.Shape.Parents[row];
		return _view.IsLastSibling[row];
	}
}

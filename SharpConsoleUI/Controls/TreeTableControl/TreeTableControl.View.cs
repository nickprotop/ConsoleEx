// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls;

public partial class TreeTableControl
{
	#region Fields

	// The hierarchy as last handed to the table, and the display computed from it. The display is
	// replaced whole on every recompute, so painting on another thread always reads a consistent one;
	// null while the table shows its rows flat.
	private TreeTableShape _shape = TreeTableShape.Empty;
	private TreeTableProjection? _view;

	private TreeGuide _guide = TreeGuide.Line;
	private string _indent = ControlDefaults.DefaultTreeIndent;
	private int _treeColumnIndex;

	#endregion

	#region Properties

	/// <summary>
	/// Gets or sets the line style of the guides connecting rows to their parents. Default
	/// <see cref="TreeGuide.Line"/>.
	/// </summary>
	public TreeGuide Guide
	{
		get => _guide;
		set
		{
			if (_guide == value) return;
			_guide = value;
			OnPropertyChanged();
			RedrawDecorations();
		}
	}

	/// <summary>
	/// Gets or sets what follows each ancestor level's guide line, widening every level of nesting.
	/// Default <see cref="ControlDefaults.DefaultTreeIndent"/>, two spaces. Drawn as text, not markup.
	/// </summary>
	public string Indent
	{
		get => _indent;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			if (_indent == value) return;
			_indent = value;
			OnPropertyChanged();
			RedrawDecorations();
		}
	}

	/// <summary>
	/// Gets or sets the column the guides and expanders are drawn in. Default 0, the first column.
	/// </summary>
	/// <remarks>
	/// A column index that does not exist draws the hierarchy nowhere; the rows still nest and
	/// expand.
	/// </remarks>
	public int TreeColumnIndex
	{
		get => _treeColumnIndex;
		set
		{
			if (value < 0)
				throw new ArgumentOutOfRangeException(nameof(value), value, "The tree column cannot be negative.");
			if (_treeColumnIndex == value) return;
			_treeColumnIndex = value;
			OnPropertyChanged();
			RedrawDecorations();
		}
	}

	#endregion

	#region Computing the Display

	/// <summary>
	/// Displays the roots, and under each open row its children: what a tree shows. A sort orders
	/// each row's children among themselves.
	/// </summary>
	/// <remarks>
	/// Sorting a hierarchy as a flat list would tear rows away from their parents, so siblings are
	/// compared only with each other, by the table's own rules through
	/// <see cref="TableControl.CompareRows"/>; rows that compare equal keep the order they were added
	/// in, in either direction.
	/// </remarks>
	protected override int[]? ComputeDisplayRows(TableDisplayQuery query)
	{
		// A data source makes the table flat; so, for now, does a filter.
		if (DataSource != null || query.IsFiltered)
		{
			_view = null;
			return base.ComputeDisplayRows(query);
		}

		TreeTableShape shape;
		lock (SyncRoot) { shape = _shape; }

		Comparison<int>? siblingOrder = null;
		if (query.IsSorted)
		{
			int column = query.SortColumnIndex;
			var direction = query.SortDirection;
			siblingOrder = (a, b) =>
			{
				int result = CompareRows(a, b, column, direction);
				return result != 0 ? result : a.CompareTo(b);
			};
		}

		var view = TreeTableProjection.Compute(shape, row => shape.Rows[row] is TreeTableRow { IsExpanded: true }, siblingOrder);
		_view = view;
		return view.DisplayRows;
	}

	/// <summary>
	/// Moves a cursor whose row was hidden, by collapsing one of its ancestors, to the nearest
	/// ancestor still displayed.
	/// </summary>
	protected override int ResolveHiddenSelectedRow(int dataIndex)
	{
		var view = _view;
		return view != null && dataIndex < view.Shape.Count ? view.FindDisplayedAncestor(dataIndex) : -1;
	}

	#endregion

	#region Drawing the Hierarchy

	/// <summary>Draws the guides and the expander in front of the tree column's value.</summary>
	protected override string? GetCellPrefixMarkup(int dataRowIndex, int columnIndex)
	{
		var view = _view;
		if (view == null || columnIndex != _treeColumnIndex || dataRowIndex < 0 || dataRowIndex >= view.Shape.Count)
			return null;

		string prefix = view.PrefixMarkup[dataRowIndex] ??= BuildPrefixMarkup(view, dataRowIndex);
		return prefix.Length == 0 ? null : prefix;
	}

	/// <summary>
	/// The guide lines in front of a row, as markup: for each ancestor level a continuation line or a
	/// blank, then the connector to the row. Empty for a root.
	/// </summary>
	/// <param name="context">Where the row stands in what is displayed.</param>
	/// <returns>The markup, or an empty string for none.</returns>
	/// <remarks>
	/// Override to draw the hierarchy differently. The markup is drawn in front of the value without
	/// being part of it, and the expander follows it; the width of what this returns is where a click
	/// starts counting as a click on the expander.
	/// </remarks>
	protected virtual string GetGuideMarkup(in TreeTableRowContext context)
	{
		if (context.Depth == 0) return string.Empty;

		var isLastAtLevel = new bool[context.Depth];
		for (int depth = 1; depth <= context.Depth; depth++)
			isLastAtLevel[depth - 1] = context.IsLastSiblingAtDepth(depth);

		var builder = new StringBuilder();
		TreeGuideHelper.AppendPrefix(builder, isLastAtLevel, TreeGuideHelper.GetGuideChars(context.Guide), context.Indent);
		return MarkupParser.Escape(builder.ToString());
	}

	/// <summary>
	/// The expander after a row's guides, as markup: open or closed for a row with children, blank
	/// of the same width for one without while another row shows an expander, otherwise nothing.
	/// </summary>
	/// <param name="context">Where the row stands in what is displayed.</param>
	/// <returns>The markup, or an empty string for none.</returns>
	/// <remarks>
	/// Override to draw expanders differently. The width of what this returns is the span a click
	/// toggles the row on, so a wider or narrower glyph keeps working with the mouse.
	/// </remarks>
	protected virtual string GetExpanderMarkup(in TreeTableRowContext context)
	{
		if (context.HasChildren)
			return MarkupParser.Escape(TreeGuideHelper.GetExpanderText(context.IsExpanded));

		return context.ShowsExpanderGutter ? new string(' ', TreeGuideHelper.ExpanderWidth) : string.Empty;
	}

	/// <summary>Builds and keeps a row's guide, expander and the two together.</summary>
	private string BuildPrefixMarkup(TreeTableProjection view, int dataRowIndex)
	{
		var context = new TreeTableRowContext(view, dataRowIndex, _guide, _indent);
		string guide = view.GuideMarkup[dataRowIndex] ??= GetGuideMarkup(in context) ?? string.Empty;
		string expander = view.ExpanderMarkup[dataRowIndex] ??= GetExpanderMarkup(in context) ?? string.Empty;
		return guide + expander;
	}

	/// <summary>Drops the drawn guides and expanders, after a setting that changes them.</summary>
	private void RedrawDecorations()
	{
		_view?.ForgetMarkup();
		InvalidateColumnWidths();
		Invalidate(Invalidation.Relayout);
	}

	#endregion
}

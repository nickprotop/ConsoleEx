// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Events;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating TreeTableControl instances.
/// </summary>
/// <remarks>
/// Every table setting comes from <see cref="TableControlBuilderBase{TSelf}"/>, exactly as for
/// <see cref="TableControlBuilder"/>; the rows it adds are roots, a <see cref="TreeTableRow"/>
/// bringing the rows nested under it. This builder adds the hierarchy's own settings and events.
/// </remarks>
public sealed class TreeTableControlBuilder : TableControlBuilderBase<TreeTableControlBuilder>, IControlBuilder<TreeTableControl>
{
	private TreeGuide _guide = TreeGuide.Line;
	private string _indent = ControlDefaults.DefaultTreeIndent;
	private int _treeColumnIndex;
	private bool _filterIncludesDescendants;
	private EventHandler<TreeTableRowExpansionChangingEventArgs>? _onRowExpansionChanging;
	private EventHandler<TreeTableRowExpansionEventArgs>? _onRowExpansionChanged;

	#region Hierarchy

	/// <summary>
	/// Adds a root row, with any rows nested under it.
	/// </summary>
	/// <param name="row">The row to add.</param>
	/// <returns>The builder for chaining</returns>
	public TreeTableControlBuilder AddRootRow(TreeTableRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		return AddRow(row);
	}

	/// <summary>
	/// Adds a root row with the specified cells.
	/// </summary>
	/// <param name="cells">The new row's cell values.</param>
	/// <returns>The newly created row, to add children to.</returns>
	/// <remarks>
	/// The row is a plain <see cref="TreeTableRow"/>: the table that will hold it does not exist
	/// yet, so <c>CreateTreeRow</c> is not asked.
	/// </remarks>
	public TreeTableRow AddRootRow(params string[] cells)
	{
		var row = new TreeTableRow(cells);
		AddRow(row);
		return row;
	}

	/// <summary>
	/// Sets the line style of the guides connecting rows to their parents.
	/// </summary>
	/// <param name="guide">The guide style.</param>
	/// <returns>The builder for chaining</returns>
	public TreeTableControlBuilder WithGuide(TreeGuide guide)
	{
		_guide = guide;
		return this;
	}

	/// <summary>
	/// Sets what follows each ancestor level's guide line.
	/// </summary>
	/// <param name="indent">The indentation text.</param>
	/// <returns>The builder for chaining</returns>
	public TreeTableControlBuilder WithIndent(string indent)
	{
		ArgumentNullException.ThrowIfNull(indent);
		_indent = indent;
		return this;
	}

	/// <summary>
	/// Sets the column the guides and expanders are drawn in.
	/// </summary>
	/// <param name="columnIndex">The column index; 0 by default.</param>
	/// <returns>The builder for chaining</returns>
	public TreeTableControlBuilder WithTreeColumn(int columnIndex)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
		_treeColumnIndex = columnIndex;
		return this;
	}

	/// <summary>
	/// Makes a filter show every row under a match, matching or not.
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public TreeTableControlBuilder WithFilterIncludingDescendants()
	{
		_filterIncludesDescendants = true;
		return this;
	}

	#endregion

	#region Events

	/// <summary>
	/// Wires the RowExpansionChanging event handler, which can cancel a change or load children on demand.
	/// </summary>
	public TreeTableControlBuilder OnRowExpansionChanging(EventHandler<TreeTableRowExpansionChangingEventArgs> handler)
	{
		_onRowExpansionChanging = handler;
		return this;
	}

	/// <summary>
	/// Wires the RowExpansionChanged event handler.
	/// </summary>
	public TreeTableControlBuilder OnRowExpansionChanged(EventHandler<TreeTableRowExpansionEventArgs> handler)
	{
		_onRowExpansionChanged = handler;
		return this;
	}

	#endregion

	#region Build

	/// <summary>
	/// Builds the TreeTableControl with all configured options.
	/// </summary>
	public TreeTableControl Build()
	{
		var table = new TreeTableControl
		{
			Guide = _guide,
			Indent = _indent,
			TreeColumnIndex = _treeColumnIndex,
			FilterIncludesDescendants = _filterIncludesDescendants,
		};

		if (_onRowExpansionChanging != null)
			table.RowExpansionChanging += _onRowExpansionChanging;
		if (_onRowExpansionChanged != null)
			table.RowExpansionChanged += _onRowExpansionChanged;

		return Apply(table);
	}

	/// <summary>
	/// Implicit conversion to TreeTableControl.
	/// </summary>
	public static implicit operator TreeTableControl(TreeTableControlBuilder builder) => builder.Build();

	#endregion
}

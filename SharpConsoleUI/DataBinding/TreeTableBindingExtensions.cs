// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.DataBinding;

/// <summary>
/// Extension methods for binding a <see cref="TreeTableControl"/> to a hierarchy of items.
/// </summary>
public static class TreeTableBindingExtensions
{
	/// <summary>
	/// Shows a hierarchy of items in the table, one row per item, and keeps the rows in step as the
	/// items and their collections change.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="table">The table.</param>
	/// <param name="roots">The items at the top of the hierarchy.</param>
	/// <param name="childrenOf">An item's children, or null for none.</param>
	/// <param name="cellsOf">An item's cell values, as markup, in column order.</param>
	/// <param name="configure">Optional settings: item identity, expansion, loading on demand, row styling.</param>
	/// <returns>The table, for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Each row's <see cref="TableRow.Tag"/> is its item, so <see cref="TableControl.SelectedRow"/>
	/// and <see cref="TreeTableControl.FindRowByTag"/> lead back to the items. A collection that
	/// implements <see cref="System.Collections.Specialized.INotifyCollectionChanged"/>, such as
	/// <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>, is followed change by
	/// change: an added item gets a row in its place, a removed one loses it, a moved one keeps its
	/// row, with its selection and expansion. An item that implements
	/// <see cref="System.ComponentModel.INotifyPropertyChanged"/> updates its row's cells when it
	/// changes, and its children when <paramref name="childrenOf"/> returns another collection.
	/// </para>
	/// <para>
	/// Delegates rather than expressions, so nothing is compiled at run time and the binding works
	/// under NativeAOT. The binding is kept in <see cref="BaseControl.Bindings"/> and ends with the
	/// control; binding again replaces it, removing the rows it created. Rows added to the table in
	/// other ways are left alone. As with the library's other bindings, nothing is marshalled: change
	/// the items and collections on the UI thread.
	/// </para>
	/// </remarks>
	public static TreeTableControl BindItems<T>(
		this TreeTableControl table,
		IEnumerable<T> roots,
		Func<T, IEnumerable<T>?> childrenOf,
		Func<T, IEnumerable<string>> cellsOf,
		Action<TreeTableItemsOptions<T>>? configure = null)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(table);
		ArgumentNullException.ThrowIfNull(roots);
		ArgumentNullException.ThrowIfNull(childrenOf);
		ArgumentNullException.ThrowIfNull(cellsOf);

		var options = new TreeTableItemsOptions<T>();
		configure?.Invoke(options);

		if (table.ItemsBinding is { } previous)
		{
			previous.Release();
			table.Bindings.Remove(previous);
		}

		var binding = new TreeTableItemsBinding<T>(table, roots, childrenOf, cellsOf, options);
		table.ItemsBinding = binding;
		table.Bindings.Add(binding);
		return table;
	}

	/// <summary>
	/// Binds the table the builder builds to a hierarchy of items, once it is built.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="builder">The builder.</param>
	/// <param name="roots">The items at the top of the hierarchy.</param>
	/// <param name="childrenOf">An item's children, or null for none.</param>
	/// <param name="cellsOf">An item's cell values, as markup, in column order.</param>
	/// <param name="configure">Optional settings: item identity, expansion, loading on demand, row styling.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <remarks>
	/// See <see cref="BindItems{T}(TreeTableControl, IEnumerable{T}, Func{T, IEnumerable{T}?}, Func{T, IEnumerable{string}}, Action{TreeTableItemsOptions{T}}?)"/>.
	/// The bound rows follow any rows the builder adds.
	/// </remarks>
	public static TreeTableControlBuilder BindItems<T>(
		this TreeTableControlBuilder builder,
		IEnumerable<T> roots,
		Func<T, IEnumerable<T>?> childrenOf,
		Func<T, IEnumerable<string>> cellsOf,
		Action<TreeTableItemsOptions<T>>? configure = null)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(roots);
		ArgumentNullException.ThrowIfNull(childrenOf);
		ArgumentNullException.ThrowIfNull(cellsOf);

		BindingHelper.AddDeferredBinding(builder, control =>
			((TreeTableControl)control).BindItems(roots, childrenOf, cellsOf, configure));
		return builder;
	}
}

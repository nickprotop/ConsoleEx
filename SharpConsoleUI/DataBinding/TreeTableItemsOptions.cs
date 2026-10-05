// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.DataBinding;

/// <summary>
/// Optional settings for binding a <see cref="TreeTableControl"/> to a hierarchy of items through
/// <see cref="TreeTableBindingExtensions.BindItems{T}(TreeTableControl, IEnumerable{T}, Func{T, IEnumerable{T}?}, Func{T, IEnumerable{string}}, Action{TreeTableItemsOptions{T}}?)"/>.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed class TreeTableItemsOptions<T> where T : class
{
	/// <summary>
	/// Gets or sets how items are told apart when a collection is reset or an item replaced: an item
	/// equal to one already shown keeps its row, with its selection and expansion. Default
	/// <see cref="EqualityComparer{T}.Default"/>.
	/// </summary>
	public IEqualityComparer<T>? ItemComparer { get; set; }

	/// <summary>
	/// Gets or sets whether an item's row is expanded: asked when the row is created and again when
	/// the item raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>.
	/// Default null: rows start expanded and only the table changes them.
	/// </summary>
	public Func<T, bool>? IsExpanded { get; set; }

	/// <summary>
	/// Gets or sets what to call when a row's own expansion changed in the table, to store it on the
	/// item. Not called for changes to the filtered view only.
	/// </summary>
	/// <remarks>
	/// Called from the table's <see cref="TreeTableControl.RowExpansionChanged"/>, so a handler of that
	/// event subscribed before the binding was made runs before the item is updated.
	/// </remarks>
	public Action<T, bool>? IsExpandedChanged { get; set; }

	/// <summary>
	/// Gets or sets whether an item's children are loaded only when its row is first expanded. Such a
	/// row starts collapsed, unless <see cref="IsExpanded"/> says otherwise, and shows an expander
	/// without asking for its children; they are asked for, and their rows added, as the row opens.
	/// Default null: every item's children are asked for at once.
	/// </summary>
	public Func<T, bool>? HasUnrealizedChildren { get; set; }

	/// <summary>
	/// Gets or sets what to call after an item's row was created or its cells updated: to colour it,
	/// disable it, or set anything else a row has.
	/// </summary>
	public Action<TreeTableRow, T>? UpdateRow { get; set; }
}

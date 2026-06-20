// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A list of <see cref="GridLength"/> track definitions that notifies its owning grid whenever it
	/// is mutated. This closes the gap where a runtime mutation of a grid's row/column definitions
	/// (e.g. <c>grid.ColumnDefinitions.Add(GridLength.Star(1))</c>) needs to rebuild and invalidate the
	/// grid so the change is reflected on the next render.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It derives from <see cref="Collection{T}"/> so existing callers that use <c>Add</c>, <c>Insert</c>,
	/// <c>RemoveAt</c>, <c>Clear</c>, the indexer, and <c>Count</c> continue to compile unchanged.
	/// <see cref="Collection{T}"/> also implements <see cref="IReadOnlyList{T}"/>, which satisfies the
	/// <see cref="IGridSource"/> seam.
	/// </para>
	/// <para>
	/// Mutations and reads are serialised by an internal lock so a background thread doing
	/// <c>ColumnDefinitions.Add(...)</c> cannot corrupt a concurrent read on the layout (UI) thread. Use
	/// <see cref="Snapshot"/> for a stable point-in-time copy. This is defensive against corruption only;
	/// callers should still marshal grid mutations to the UI thread for semantic consistency.
	/// </para>
	/// <para>
	/// The change callback runs on every structural mutation; it is the same work the grid does after a
	/// <c>Place</c>/<c>AddControl</c>: null the ordered-cells cache, force a layout rebuild, and
	/// invalidate. The callback is invoked outside both the internal sync lock and the grid's cell lock,
	/// so it must not re-enter list mutation.
	/// </para>
	/// </remarks>
	internal sealed class GridDefinitionList : Collection<GridLength>, IReadOnlyList<GridLength>
	{
		private readonly Action _onChanged;
		private readonly object _sync = new();

		/// <summary>
		/// Creates a definition list that invokes <paramref name="onChanged"/> after each mutation.
		/// </summary>
		/// <param name="onChanged">The callback to run after any insert, set, remove, or clear.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="onChanged"/> is <c>null</c>.</exception>
		public GridDefinitionList(Action onChanged)
		{
			ArgumentNullException.ThrowIfNull(onChanged);
			_onChanged = onChanged;
		}

		/// <summary>
		/// Returns a stable snapshot of the definitions taken under the internal lock, so a consumer
		/// reading it cannot observe a partially-applied concurrent mutation or throw a
		/// "Collection was modified" enumeration error.
		/// </summary>
		public IReadOnlyList<GridLength> Snapshot()
		{
			lock (_sync) { return new List<GridLength>(Items); }
		}

		/// <summary>Gets the element at the given index. The read is taken under the internal lock.</summary>
		public new GridLength this[int index]
		{
			get { lock (_sync) { return Items[index]; } }
			set { base[index] = value; }
		}

		/// <summary>Gets the number of definitions. The read is taken under the internal lock.</summary>
		public new int Count
		{
			get { lock (_sync) { return Items.Count; } }
		}

		/// <summary>
		/// Returns an enumerator over a snapshot taken under the internal lock, so enumeration cannot
		/// throw if another thread mutates the list concurrently.
		/// </summary>
		public new IEnumerator<GridLength> GetEnumerator() => Snapshot().GetEnumerator();

		/// <inheritdoc/>
		protected override void InsertItem(int index, GridLength item)
		{
			lock (_sync) { base.InsertItem(index, item); }
			_onChanged();
		}

		/// <inheritdoc/>
		protected override void SetItem(int index, GridLength item)
		{
			lock (_sync) { base.SetItem(index, item); }
			_onChanged();
		}

		/// <inheritdoc/>
		protected override void RemoveItem(int index)
		{
			lock (_sync) { base.RemoveItem(index); }
			_onChanged();
		}

		/// <inheritdoc/>
		protected override void ClearItems()
		{
			lock (_sync) { base.ClearItems(); }
			_onChanged();
		}
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Per-cell accessor surface for <see cref="GridControl"/>: the internal read/write primitives that
	/// back the value-type <see cref="GridCell"/> handle (and the indexer / <see cref="GridControl.Cell"/>).
	/// These mutate the same <c>_cells</c> store under <c>_cellsLock</c> as the rest of the grid; styling
	/// data lives on the <see cref="GridPlacement"/> record, so an empty cell can be styled before it is
	/// filled by parking a content-less entry (a <c>null</c> Control) in the store.
	/// </summary>
	public partial class GridControl
	{
		/// <summary>
		/// Returns the control whose placement starts at <paramref name="row"/>/<paramref name="col"/>,
		/// or <c>null</c> when no entry starts there or the entry is content-less (a styled empty cell).
		/// </summary>
		/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
		/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
		internal IWindowControl? GetCellControl(int row, int col)
		{
			lock (_cellsLock)
			{
				int index = FindEntryIndex(row, col);
				return index < 0 ? null : _cells[index].Control;
			}
		}

		/// <summary>
		/// Returns the <see cref="GridPlacement"/> of the entry starting at
		/// <paramref name="row"/>/<paramref name="col"/> (content-bearing or content-less), or
		/// <c>null</c> when no entry starts there.
		/// </summary>
		/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
		/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
		internal GridPlacement? GetCellPlacement(int row, int col)
		{
			lock (_cellsLock)
			{
				int index = FindEntryIndex(row, col);
				return index < 0 ? null : _cells[index].Placement;
			}
		}

		/// <summary>
		/// Sets (or clears) the content of the cell starting at <paramref name="row"/>/<paramref name="col"/>:
		/// a <c>null</c> <paramref name="control"/> removes a content-bearing entry (a content-less styled
		/// entry is left in place so styling survives); a non-<c>null</c> control replaces an existing
		/// entry's control (keeping its styling placement) or, if no entry exists, adds a new one with
		/// default styling. Range is validated like <see cref="Place"/> when definitions exist.
		/// </summary>
		/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
		/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
		/// <param name="control">The control to place, or <c>null</c> to clear the cell's content.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Thrown when <paramref name="row"/> or <paramref name="col"/> is negative, or falls outside the
		/// declared track definitions when any exist.
		/// </exception>
		internal void SetCellContent(int row, int col, IWindowControl? control)
		{
			ValidateCoord(row, col);

			IWindowControl? detached = null;
			IWindowControl? attached = null;
			lock (_cellsLock)
			{
				int index = FindEntryIndex(row, col);

				if (control == null)
				{
					if (index < 0) return; // nothing to clear

					var existing = _cells[index];
					if (existing.Control == null) return; // already content-less; keep its styling

					// Clearing content but the cell carries styling — keep a content-less styled entry
					// so the chrome survives; otherwise drop the entry entirely.
					bool hasStyle = existing.Placement.Background != null
						|| existing.Placement.Border != BorderStyle.None
						|| existing.Placement.CellPadding != Padding.None;
					detached = existing.Control;
					if (hasStyle)
						_cells[index] = (null, existing.Placement);
					else
						_cells.RemoveAt(index);
				}
				else if (index >= 0)
				{
					// Replace the control, preserving the existing styling placement.
					var existing = _cells[index];
					detached = existing.Control; // may be null for a content-less entry
					_cells[index] = (control, existing.Placement);
					attached = control;
				}
				else
				{
					_cells.Add((control, new GridPlacement(row, col)));
					attached = control;
				}

				_orderedCellsCache = null;
				_allOrderedCellsCache = null;
			}

			if (detached != null && !ReferenceEquals(detached, attached))
				detached.Container = null;
			if (attached != null)
				attached.Container = this;
			RebuildAndInvalidate();
		}

		/// <summary>
		/// Applies per-cell styling to the cell starting at <paramref name="row"/>/<paramref name="col"/>.
		/// Each parameter uses "if provided, set it" semantics: a non-<c>null</c> argument overrides that
		/// facet, a <c>null</c> argument leaves it unchanged. When no entry exists at the cell a
		/// content-less styled entry is created so an empty cell can be styled before it is filled.
		/// </summary>
		/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
		/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
		/// <param name="background">The new background fill, or <c>null</c> to leave it unchanged.</param>
		/// <param name="border">The new border style, or <c>null</c> to leave it unchanged.</param>
		/// <param name="padding">The new content padding, or <c>null</c> to leave it unchanged.</param>
		/// <remarks>
		/// Because <c>null</c> means "leave unchanged", this method cannot reset <see cref="GridCell.Background"/>
		/// back to <c>null</c> (no background). Use <see cref="GridCell.Clear"/> to drop the whole cell.
		/// </remarks>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Thrown when <paramref name="row"/> or <paramref name="col"/> is negative, or falls outside the
		/// declared track definitions when any exist.
		/// </exception>
		internal void SetCellStyle(int row, int col, Color? background = null, BorderStyle? border = null, Padding? padding = null)
		{
			ValidateCoord(row, col);

			lock (_cellsLock)
			{
				int index = FindEntryIndex(row, col);
				if (index >= 0)
				{
					var existing = _cells[index];
					var updated = existing.Placement with
					{
						Background = background ?? existing.Placement.Background,
						Border = border ?? existing.Placement.Border,
						CellPadding = padding ?? existing.Placement.CellPadding
					};
					_cells[index] = (existing.Control, updated);
				}
				else
				{
					// No entry yet — park a content-less styled entry so an empty cell can carry chrome.
					var placement = new GridPlacement(row, col)
					{
						Background = background,
						Border = border ?? BorderStyle.None,
						CellPadding = padding ?? Padding.None
					};
					_cells.Add((null, placement));
				}

				_orderedCellsCache = null;
				_allOrderedCellsCache = null;
			}

			RebuildAndInvalidate();
		}

		/// <summary>
		/// Clears all per-cell styling (background, border, padding) from the cell starting at
		/// <paramref name="row"/>/<paramref name="col"/>, keeping its content. Unlike
		/// <see cref="SetCellStyle"/> — whose <c>null</c> arguments mean "leave unchanged" — this is the
		/// explicit way to remove a cell's styling. When the entry is content-less (a styled empty cell),
		/// resetting leaves it with neither content nor style, so it is removed entirely. A no-op when no
		/// entry starts at the cell.
		/// </summary>
		/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
		/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Thrown when <paramref name="row"/> or <paramref name="col"/> is negative, or falls outside the
		/// declared track definitions when any exist.
		/// </exception>
		internal void ResetCellStyle(int row, int col)
		{
			ValidateCoord(row, col);

			bool changed = false;
			lock (_cellsLock)
			{
				int index = FindEntryIndex(row, col);
				if (index >= 0)
				{
					var existing = _cells[index];
					if (existing.Control == null)
					{
						// Content-less styled cell stripped of style serves no purpose — drop it.
						_cells.RemoveAt(index);
					}
					else
					{
						_cells[index] = (existing.Control, existing.Placement with
						{
							Background = null,
							Border = BorderStyle.None,
							CellPadding = new Padding(0)
						});
					}

					_orderedCellsCache = null;
					_allOrderedCellsCache = null;
					changed = true;
				}
			}

			if (changed)
				RebuildAndInvalidate();
		}

		/// <summary>
		/// Finds the index of the entry whose placement starts exactly at
		/// <paramref name="row"/>/<paramref name="col"/>, or -1. Callers must hold <c>_cellsLock</c>.
		/// </summary>
		private int FindEntryIndex(int row, int col) =>
			_cells.FindIndex(c => c.Placement.Row == row && c.Placement.Col == col);

		/// <summary>
		/// Range-validates a cell coordinate, mirroring <see cref="Place"/>: negatives are always invalid,
		/// and when track definitions exist the start cell must fall inside them.
		/// </summary>
		private void ValidateCoord(int row, int col)
		{
			if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
			if (col < 0) throw new ArgumentOutOfRangeException(nameof(col));
			if (RowDefinitions.Count > 0 && row >= RowDefinitions.Count)
				throw new ArgumentOutOfRangeException(nameof(row));
			if (ColumnDefinitions.Count > 0 && col >= ColumnDefinitions.Count)
				throw new ArgumentOutOfRangeException(nameof(col));
		}
	}
}

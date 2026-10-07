// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

public partial class TableControl
{
	/// <summary>
	/// Tracks a single row or cell animation overlay, on the row it was started on.
	/// </summary>
	/// <remarks>
	/// THE ROW, NOT ITS POSITION. An animation runs for hundreds of milliseconds, during which the
	/// table can be sorted, filtered or changed, and a position taken at the start then names another
	/// row. The table's own rows are therefore followed by reference, and found again whenever the
	/// overlay is drawn or the row removed; a data source has no row objects, so its rows are followed
	/// by data index, adjusted as the source announces rows added, removed and moved. A class, not a
	/// record: the animation's callbacks hold this very instance, and value equality would let two
	/// alike entries be mistaken for each other.
	/// </remarks>
	internal sealed class RowAnimationEntry
	{
		public RowAnimationEntry(TableRow? row, int dataIndex, int columnIndex, Color overlayColor, float intensity)
		{
			Row = row;
			DataIndex = dataIndex;
			ColumnIndex = columnIndex;
			OverlayColor = overlayColor;
			Intensity = intensity;
		}

		/// <summary>The table's own row being animated, or null for a data source's row.</summary>
		public TableRow? Row { get; }

		/// <summary>
		/// The row's data index: for a data source, the row itself; for the table's own rows, where
		/// the row was last found, checked first the next time it is looked for.
		/// </summary>
		public int DataIndex { get; set; }

		/// <summary>The column of a cell animation, or -1 for the whole row.</summary>
		public int ColumnIndex { get; }

		/// <summary>The overlay colour.</summary>
		public Color OverlayColor { get; }

		/// <summary>Current overlay intensity, updated as the animation runs.</summary>
		public float Intensity { get; set; }

		/// <summary>Whether only one cell is overlaid.</summary>
		public bool CellOnly => ColumnIndex >= 0;
	}

	private readonly List<RowAnimationEntry> _rowAnimationEntries = new();
	private AnimationManager? _testAnimationManager;

	/// <summary>
	/// Whether any row-level animations are currently active.
	/// </summary>
	public bool HasActiveRowAnimations => _rowAnimationEntries.Count > 0;

	/// <summary>
	/// Sets the animation manager for testing purposes (bypasses window lookup).
	/// </summary>
	internal void SetAnimationManagerForTesting(AnimationManager manager)
	{
		_testAnimationManager = manager;
	}

	/// <summary>
	/// Gets the AnimationManager from the parent window, or the test override.
	/// </summary>
	private AnimationManager? GetAnimationManager()
	{
		if (_testAnimationManager != null)
			return _testAnimationManager;

		return (this as IWindowControl).GetParentWindow()?.GetConsoleWindowSystem?.Animations;
	}

	/// <summary>
	/// Applies a color overlay to an entire row using SinePulse easing by default.
	/// The overlay peaks at the midpoint then decays back to zero.
	/// </summary>
	/// <param name="rowIndex">
	/// The row's display position, as <see cref="SelectedRowIndex"/> counts rows. The overlay stays on
	/// that row while it runs, wherever a sort, a filter or a change to the rows moves it, and is not
	/// drawn while the row is hidden.
	/// </param>
	/// <param name="color">The overlay color.</param>
	/// <param name="duration">How long the flash lasts.</param>
	/// <param name="easing">Easing function. Defaults to SinePulse.</param>
	/// <returns>The animation, or null if rowIndex is invalid or no AnimationManager.</returns>
	public IAnimation? FlashRow(int rowIndex, Color color, TimeSpan duration, EasingFunction? easing = null)
		=> AnimateRow(rowIndex, -1, color, 0f, 1f, easing ?? EasingFunctions.SinePulse, duration);

	/// <summary>
	/// Applies a color overlay to a single cell using SinePulse easing by default.
	/// </summary>
	/// <param name="rowIndex">The row's display position, followed as for <see cref="FlashRow"/>.</param>
	/// <param name="columnIndex">The column index.</param>
	/// <param name="color">The overlay color.</param>
	/// <param name="duration">How long the flash lasts.</param>
	/// <param name="easing">Easing function. Defaults to SinePulse.</param>
	/// <returns>The animation, or null if indices are invalid or no AnimationManager.</returns>
	public IAnimation? FlashCell(int rowIndex, int columnIndex, Color color, TimeSpan duration, EasingFunction? easing = null)
	{
		if (columnIndex < 0 || columnIndex >= ColumnCount) return null;

		return AnimateRow(rowIndex, columnIndex, color, 0f, 1f, easing ?? EasingFunctions.SinePulse, duration);
	}

	/// <summary>
	/// Highlights a row starting at full overlay intensity, decaying to zero.
	/// Useful for new row insertion highlights. Uses EaseOut by default.
	/// </summary>
	/// <param name="rowIndex">The row's display position, followed as for <see cref="FlashRow"/>.</param>
	/// <param name="color">The overlay color.</param>
	/// <param name="duration">How long the highlight lasts.</param>
	/// <param name="easing">Easing function. Defaults to EaseOut.</param>
	/// <returns>The animation, or null if rowIndex is invalid or no AnimationManager.</returns>
	public IAnimation? HighlightRow(int rowIndex, Color color, TimeSpan duration, EasingFunction? easing = null)
		=> AnimateRow(rowIndex, -1, color, 1f, 0f, easing ?? EasingFunctions.EaseOut, duration);

	/// <summary>
	/// Animates a row fading to black, then removes it from the table.
	/// </summary>
	/// <param name="rowIndex">
	/// The row's display position, as <see cref="SelectedRowIndex"/> counts rows. The row that fades is
	/// the row removed, wherever the table has moved it by then; a row already removed meanwhile is
	/// not replaced by another.
	/// </param>
	/// <param name="duration">Duration of the fade-out animation.</param>
	/// <param name="easing">Easing function. Defaults to EaseOut.</param>
	/// <returns>
	/// The animation, or null if rowIndex is invalid, no AnimationManager, or a data source is set,
	/// whose rows the table cannot remove.
	/// </returns>
	public IAnimation? AnimateRowRemoval(int rowIndex, TimeSpan duration, EasingFunction? easing = null)
		=> AnimateRowsRemoval(new[] { rowIndex }, duration, easing);

	/// <summary>
	/// Bulk variant: all specified rows fade to black simultaneously, then are all removed
	/// in one frame.
	/// </summary>
	/// <param name="rowIndices">The rows' display positions, followed as for <see cref="AnimateRowRemoval"/>.</param>
	/// <param name="duration">Duration of the fade-out animation.</param>
	/// <param name="easing">Easing function. Defaults to EaseOut.</param>
	/// <returns>
	/// The animation, or null if no valid indices, no AnimationManager, or a data source is set.
	/// </returns>
	public IAnimation? AnimateRowsRemoval(int[] rowIndices, TimeSpan duration, EasingFunction? easing = null)
	{
		if (rowIndices == null || rowIndices.Length == 0 || _dataSource != null) return null;

		var rows = rowIndices
			.Where(position => position >= 0 && position < RowCount)
			.Distinct()
			.Select(position => RowAtDisplayPosition(position))
			.OfType<TableRow>()
			.ToList();
		if (rows.Count == 0) return null;

		var manager = GetAnimationManager();
		if (manager == null) return null;

		var entries = rows.Select(row => new RowAnimationEntry(row, -1, -1, Color.Black, 0f)).ToList();
		_rowAnimationEntries.AddRange(entries);

		return manager.Animate(
			from: 0f,
			to: 1f,
			duration: duration,
			easing: easing ?? EasingFunctions.EaseOut,
			onUpdate: intensity =>
			{
				foreach (var entry in entries)
					entry.Intensity = intensity;
				Invalidate(Invalidation.Repaint);
			},
			onComplete: () =>
			{
				foreach (var entry in entries)
					_rowAnimationEntries.Remove(entry);

				// Each row is found again now, by reference, so whatever moved meanwhile, only the
				// rows that faded are removed; highest index first, so the others stay where found.
				var indices = entries.Select(FindDataIndex).Where(index => index >= 0).OrderByDescending(index => index);
				foreach (int index in indices)
					RemoveRow(index);

				Invalidate(Invalidation.Relayout);
			});
	}

	/// <summary>
	/// Iterates active row animations and applies color overlays to the buffer.
	/// Should be called from a PostBufferPaint handler externally.
	/// </summary>
	/// <param name="buffer">The character buffer to apply overlays to.</param>
	public void ApplyRowAnimationOverlays(CharacterBuffer buffer)
	{
		if (_rowAnimationEntries.Count == 0) return;

		int visibleRows = GetVisibleRowCount();
		foreach (var entry in _rowAnimationEntries)
		{
			if (entry.Intensity <= 0f) continue;

			// Where the row is displayed now; a row removed or hidden is not drawn.
			int dataIndex = FindDataIndex(entry);
			int position = dataIndex >= 0 ? GetDisplayRowIndex(dataIndex) : -1;
			if (position < _scrollOffset || position >= _scrollOffset + visibleRows) continue;

			int rowY = GetRenderedRowY(position);
			if (rowY < 0) continue;

			if (entry.CellOnly)
			{
				var cellBounds = GetCellBounds(entry.ColumnIndex, rowY);
				if (cellBounds.Width > 0)
				{
					ColorBlendHelper.ApplyColorOverlay(
						buffer, entry.OverlayColor,
						entry.Intensity * 0.5f,
						entry.Intensity * 0.3f,
						cellBounds);
				}
			}
			else
			{
				var rowBounds = new LayoutRect(ActualX, rowY, ActualWidth, 1);
				ColorBlendHelper.ApplyColorOverlay(
					buffer, entry.OverlayColor,
					entry.Intensity * 0.4f,
					entry.Intensity * 0.25f,
					rowBounds);
			}
		}
	}

	/// <summary>Starts an overlay on the row displayed at a position, fading between two intensities.</summary>
	private IAnimation? AnimateRow(int rowIndex, int columnIndex, Color color, float from, float to, EasingFunction easing, TimeSpan duration)
	{
		if (rowIndex < 0 || rowIndex >= RowCount) return null;

		var manager = GetAnimationManager();
		if (manager == null) return null;

		int dataIndex = MapDisplayToData(rowIndex);
		var row = _dataSource == null ? RowAtDisplayPosition(rowIndex) : null;
		if (_dataSource == null && row == null) return null;

		var entry = new RowAnimationEntry(row, dataIndex, columnIndex, color, from);
		_rowAnimationEntries.Add(entry);

		return manager.Animate(
			from: from,
			to: to,
			duration: duration,
			easing: easing,
			onUpdate: intensity =>
			{
				entry.Intensity = intensity;
				Invalidate(Invalidation.Repaint);
			},
			onComplete: () =>
			{
				_rowAnimationEntries.Remove(entry);
				Invalidate(Invalidation.Repaint);
			});
	}

	/// <summary>The table's own row displayed at a position, or null.</summary>
	private TableRow? RowAtDisplayPosition(int position)
	{
		int dataIndex = MapDisplayToData(position);
		lock (_tableLock)
		{
			return dataIndex >= 0 && dataIndex < _rows.Count ? _rows[dataIndex] : null;
		}
	}

	/// <summary>
	/// The data index of an animated row now, or -1 when it is no longer in the table. The table's
	/// own rows are found by reference, starting where the row was last found.
	/// </summary>
	private int FindDataIndex(RowAnimationEntry entry)
	{
		if (entry.Row == null)
			return entry.DataIndex < DataRowCount ? entry.DataIndex : -1;

		lock (_tableLock)
		{
			int last = entry.DataIndex;
			if (last >= 0 && last < _rows.Count && ReferenceEquals(_rows[last], entry.Row))
				return last;

			for (int i = 0; i < _rows.Count; i++)
			{
				if (ReferenceEquals(_rows[i], entry.Row))
				{
					entry.DataIndex = i;
					return i;
				}
			}
		}
		return -1;
	}

	/// <summary>
	/// Keeps the animations on a data source's rows on their rows as the source adds, removes and
	/// moves rows; after a reset, or a change it cannot follow, no position can be trusted, and they
	/// are dropped rather than drawn on whatever row now sits there.
	/// </summary>
	private void FollowSourceAnimations(NotifyCollectionChangedEventArgs e)
	{
		if (_rowAnimationEntries.Count == 0) return;

		for (int i = _rowAnimationEntries.Count - 1; i >= 0; i--)
		{
			var entry = _rowAnimationEntries[i];
			if (entry.Row != null) continue;

			int index = ShiftDataIndex(entry.DataIndex, e);
			if (index < 0)
				_rowAnimationEntries.RemoveAt(i);
			else
				entry.DataIndex = index;
		}
	}

	/// <summary>Where a source row is after a change, or -1 when it is gone or cannot be told.</summary>
	private static int ShiftDataIndex(int index, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add when e.NewStartingIndex >= 0 && e.NewItems != null:
				return index >= e.NewStartingIndex ? index + e.NewItems.Count : index;

			case NotifyCollectionChangedAction.Remove when e.OldStartingIndex >= 0 && e.OldItems != null:
				if (index < e.OldStartingIndex) return index;
				return index < e.OldStartingIndex + e.OldItems.Count ? -1 : index - e.OldItems.Count;

			case NotifyCollectionChangedAction.Replace:
				return index;

			case NotifyCollectionChangedAction.Move when e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && e.OldItems is { Count: 1 }:
				int from = e.OldStartingIndex, to = e.NewStartingIndex;
				if (index == from) return to;
				if (from < to && index > from && index <= to) return index - 1;
				if (to < from && index >= to && index < from) return index + 1;
				return index;

			default:
				return -1;
		}
	}

	/// <summary>
	/// The screen bounds of a cell on a row line, where the last paint drew its column: past the
	/// checkbox column, scrolled sideways, and cut to the part that is visible.
	/// </summary>
	private LayoutRect GetCellBounds(int columnIndex, int rowY)
	{
		var geometry = _geometry;
		if (geometry == null || columnIndex < 0 || columnIndex >= geometry.ColumnCount)
			return LayoutRect.Empty;

		int left = geometry.ToScreen(geometry.GetColumnStart(columnIndex));
		int right = left + geometry.GetColumnWidth(columnIndex);
		left = Math.Max(left, geometry.ContentLeft);
		right = Math.Min(right, geometry.ContentRight);
		if (right <= left)
			return LayoutRect.Empty;

		return new LayoutRect(ActualX + left, rowY, right - left, 1);
	}
}

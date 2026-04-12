// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Animation;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

public partial class TableControl
{
    /// <summary>
    /// Tracks a single row or cell animation overlay.
    /// </summary>
    internal record RowAnimationEntry(
        int RowIndex,
        int ColumnIndex,
        Color OverlayColor,
        float Intensity,
        bool IsRemoval,
        bool CellOnly)
    {
        /// <summary>Current overlay intensity (mutable for animation updates).</summary>
        public float Intensity { get; set; } = Intensity;
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
    /// <param name="rowIndex">The data row index to flash.</param>
    /// <param name="color">The overlay color.</param>
    /// <param name="duration">How long the flash lasts.</param>
    /// <param name="easing">Easing function. Defaults to SinePulse.</param>
    /// <returns>The animation, or null if rowIndex is invalid or no AnimationManager.</returns>
    public IAnimation? FlashRow(int rowIndex, Color color, TimeSpan duration, EasingFunction? easing = null)
    {
        if (rowIndex < 0 || rowIndex >= RowCount) return null;

        var manager = GetAnimationManager();
        if (manager == null) return null;

        var entry = new RowAnimationEntry(rowIndex, -1, color, 0f, false, false);
        _rowAnimationEntries.Add(entry);

        return manager.Animate(
            from: 0f,
            to: 1f,
            duration: duration,
            easing: easing ?? EasingFunctions.SinePulse,
            onUpdate: intensity =>
            {
                entry.Intensity = intensity;
                Invalidate();
            },
            onComplete: () =>
            {
                _rowAnimationEntries.Remove(entry);
                Invalidate();
            });
    }

    /// <summary>
    /// Applies a color overlay to a single cell using SinePulse easing by default.
    /// </summary>
    /// <param name="rowIndex">The data row index.</param>
    /// <param name="columnIndex">The column index.</param>
    /// <param name="color">The overlay color.</param>
    /// <param name="duration">How long the flash lasts.</param>
    /// <param name="easing">Easing function. Defaults to SinePulse.</param>
    /// <returns>The animation, or null if indices are invalid or no AnimationManager.</returns>
    public IAnimation? FlashCell(int rowIndex, int columnIndex, Color color, TimeSpan duration, EasingFunction? easing = null)
    {
        if (rowIndex < 0 || rowIndex >= RowCount) return null;
        if (columnIndex < 0 || columnIndex >= ColumnCount) return null;

        var manager = GetAnimationManager();
        if (manager == null) return null;

        var entry = new RowAnimationEntry(rowIndex, columnIndex, color, 0f, false, true);
        _rowAnimationEntries.Add(entry);

        return manager.Animate(
            from: 0f,
            to: 1f,
            duration: duration,
            easing: easing ?? EasingFunctions.SinePulse,
            onUpdate: intensity =>
            {
                entry.Intensity = intensity;
                Invalidate();
            },
            onComplete: () =>
            {
                _rowAnimationEntries.Remove(entry);
                Invalidate();
            });
    }

    /// <summary>
    /// Highlights a row starting at full overlay intensity, decaying to zero.
    /// Useful for new row insertion highlights. Uses EaseOut by default.
    /// </summary>
    /// <param name="rowIndex">The data row index to highlight.</param>
    /// <param name="color">The overlay color.</param>
    /// <param name="duration">How long the highlight lasts.</param>
    /// <param name="easing">Easing function. Defaults to EaseOut.</param>
    /// <returns>The animation, or null if rowIndex is invalid or no AnimationManager.</returns>
    public IAnimation? HighlightRow(int rowIndex, Color color, TimeSpan duration, EasingFunction? easing = null)
    {
        if (rowIndex < 0 || rowIndex >= RowCount) return null;

        var manager = GetAnimationManager();
        if (manager == null) return null;

        var entry = new RowAnimationEntry(rowIndex, -1, color, 1f, false, false);
        _rowAnimationEntries.Add(entry);

        return manager.Animate(
            from: 1f,
            to: 0f,
            duration: duration,
            easing: easing ?? EasingFunctions.EaseOut,
            onUpdate: intensity =>
            {
                entry.Intensity = intensity;
                Invalidate();
            },
            onComplete: () =>
            {
                _rowAnimationEntries.Remove(entry);
                Invalidate();
            });
    }

    /// <summary>
    /// Animates a row fading to black, then removes it from the table.
    /// </summary>
    /// <param name="rowIndex">The data row index to remove.</param>
    /// <param name="duration">Duration of the fade-out animation.</param>
    /// <param name="easing">Easing function. Defaults to EaseOut.</param>
    /// <returns>The animation, or null if rowIndex is invalid or no AnimationManager.</returns>
    public IAnimation? AnimateRowRemoval(int rowIndex, TimeSpan duration, EasingFunction? easing = null)
    {
        if (rowIndex < 0 || rowIndex >= RowCount) return null;

        var manager = GetAnimationManager();
        if (manager == null) return null;

        // Capture row identity — if rows shift during animation (background sync,
        // undo, etc.), we verify before removing to avoid deleting the wrong row.
        var capturedTag = GetRow(rowIndex).Tag;

        var entry = new RowAnimationEntry(rowIndex, -1, Color.Black, 0f, true, false);
        _rowAnimationEntries.Add(entry);

        return manager.Animate(
            from: 0f,
            to: 1f,
            duration: duration,
            easing: easing ?? EasingFunctions.EaseOut,
            onUpdate: intensity =>
            {
                entry.Intensity = intensity;
                Invalidate();
            },
            onComplete: () =>
            {
                _rowAnimationEntries.Remove(entry);
                // Verify row identity before removing
                if (rowIndex < RowCount && GetRow(rowIndex).Tag == capturedTag)
                {
                    RemoveRow(rowIndex);
                    AdjustAnimationIndicesAfterRemoval(rowIndex);
                }
                Invalidate();
            });
    }

    /// <summary>
    /// Bulk variant: all specified rows fade to black simultaneously, then are all removed
    /// in one frame (in reverse index order to preserve indices during removal).
    /// </summary>
    /// <param name="rowIndices">The data row indices to remove.</param>
    /// <param name="duration">Duration of the fade-out animation.</param>
    /// <param name="easing">Easing function. Defaults to EaseOut.</param>
    /// <returns>The animation, or null if no valid indices or no AnimationManager.</returns>
    public IAnimation? AnimateRowsRemoval(int[] rowIndices, TimeSpan duration, EasingFunction? easing = null)
    {
        if (rowIndices == null || rowIndices.Length == 0) return null;

        // Filter to valid indices only
        var validIndices = rowIndices.Where(i => i >= 0 && i < RowCount).Distinct().ToArray();
        if (validIndices.Length == 0) return null;

        var manager = GetAnimationManager();
        if (manager == null) return null;

        // Capture row identity at animation start for verification in onComplete
        var capturedTags = new Dictionary<int, object?>();
        foreach (var idx in validIndices)
            capturedTags[idx] = GetRow(idx).Tag;

        var entries = new List<RowAnimationEntry>();
        foreach (var idx in validIndices)
        {
            var entry = new RowAnimationEntry(idx, -1, Color.Black, 0f, true, false);
            _rowAnimationEntries.Add(entry);
            entries.Add(entry);
        }

        return manager.Animate(
            from: 0f,
            to: 1f,
            duration: duration,
            easing: easing ?? EasingFunctions.EaseOut,
            onUpdate: intensity =>
            {
                foreach (var entry in entries)
                    entry.Intensity = intensity;
                Invalidate();
            },
            onComplete: () =>
            {
                foreach (var entry in entries)
                    _rowAnimationEntries.Remove(entry);

                // Remove rows in reverse order, verifying identity to avoid removing wrong rows
                // if the table was modified during the animation (background sync, undo, etc.)
                var sorted = validIndices.OrderByDescending(i => i).ToArray();
                foreach (var idx in sorted)
                {
                    if (idx < RowCount && capturedTags.TryGetValue(idx, out var tag) && GetRow(idx).Tag == tag)
                        RemoveRow(idx);
                }

                foreach (var idx in sorted)
                    AdjustAnimationIndicesAfterRemoval(idx);

                Invalidate();
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

        foreach (var entry in _rowAnimationEntries)
        {
            if (entry.Intensity <= 0f) continue;

            int rowY = GetRenderedRowY(entry.RowIndex);
            if (rowY < 0) continue; // Row not visible

            int visibleRows = GetVisibleRowCount();
            int rowOffset = entry.RowIndex - _scrollOffset;
            if (rowOffset < 0 || rowOffset >= visibleRows) continue;

            if (entry.CellOnly)
            {
                var cellBounds = GetCellBounds(entry.RowIndex, entry.ColumnIndex, rowY);
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

    /// <summary>
    /// Calculates the screen bounds of a specific cell.
    /// </summary>
    private LayoutRect GetCellBounds(int rowIndex, int columnIndex, int rowY)
    {
        var columns = Columns;
        if (columnIndex < 0 || columnIndex >= columns.Count)
            return LayoutRect.Empty;

        // Compute column widths to find cell X position
        int[] colWidths;
        lock (_tableLock)
        {
            colWidths = ComputeColumnWidths(ActualWidth, _columns, _rows, _scrollOffset, GetVisibleRowCount());
        }

        if (columnIndex >= colWidths.Length)
            return LayoutRect.Empty;

        bool hasBorder = _borderStyle != BorderStyle.None;
        int cellX = ActualX + (hasBorder ? 1 : 0);
        for (int c = 0; c < columnIndex; c++)
        {
            cellX += colWidths[c];
            if (hasBorder) cellX++; // column separator
        }

        return new LayoutRect(cellX, rowY, colWidths[columnIndex], 1);
    }

    /// <summary>
    /// Adjusts row indices for remaining animations after a row is removed.
    /// </summary>
    private void AdjustAnimationIndicesAfterRemoval(int removedIndex)
    {
        for (int i = _rowAnimationEntries.Count - 1; i >= 0; i--)
        {
            var entry = _rowAnimationEntries[i];
            if (entry.RowIndex > removedIndex)
            {
                _rowAnimationEntries[i] = entry with { RowIndex = entry.RowIndex - 1 };
            }
            else if (entry.RowIndex == removedIndex)
            {
                // Animation on the removed row is invalid now, remove it
                _rowAnimationEntries.RemoveAt(i);
            }
        }
    }
}

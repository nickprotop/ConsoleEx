// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers.Scrollbar;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

public partial class TableControl
{
	#region Scroll Properties

	/// <summary>
	/// Gets or sets whether the table keeps the newest (last) row visible.
	/// Disables automatically when the user scrolls up, re-enables when the user scrolls back to
	/// the bottom. Mirrors <see cref="ScrollablePanelControl.AutoScroll"/>.
	/// </summary>
	/// <remarks>
	/// Defaults to <c>false</c>. Only movement through the <see cref="ScrollOffset"/> setter
	/// re-derives this flag; direct internal offset writes (selection-follow, filter and structural
	/// resets) deliberately leave it untouched, because they are not user scrolls.
	/// </remarks>
	public bool AutoScroll
	{
		get => _autoScroll;
		set { if (_autoScroll == value) return; _autoScroll = value; OnPropertyChanged(); }
	}

	/// <summary>
	/// Gets or sets the vertical scroll offset (first visible row index).
	/// </summary>
	public int ScrollOffset
	{
		get => _scrollOffset;
		set
		{
			int maxOffset = Math.Max(0, RowCount - GetVisibleRowCount());
			int clamped = Math.Clamp(value, 0, maxOffset);
			if (clamped != _scrollOffset)
			{
				bool movedUp = clamped < _scrollOffset;
				_scrollOffset = clamped;

				// Tail-follow intent, re-derived from the scroll the caller just performed. Lives here
				// (not in a wrapper's input handler) so it is correct no matter who scrolls: wheel,
				// scrollbar, keyboard or programmatic. Only UPWARD movement detaches; landing on
				// maxOffset re-attaches, which is why a tail pin (ScrollOffset = maxOffset) can never
				// detach the control from itself.
				if (_autoScroll && movedUp)
					_autoScroll = false;
				else if (!_autoScroll && clamped >= maxOffset)
					_autoScroll = true;

				_hoveredRowIndex = -1;
				OnPropertyChanged();
				Invalidate(Invalidation.Relayout);
			}
		}
	}

	/// <summary>
	/// Gets or sets the horizontal scroll offset (in character columns).
	/// </summary>
	public int HorizontalScrollOffset
	{
		get => _horizontalScrollOffset;
		set
		{
			_horizontalScrollOffset = Math.Max(0, value);
			OnPropertyChanged();
			Invalidate(Invalidation.Relayout);
		}
	}

	/// <summary>
	/// Gets or sets when the vertical scrollbar should be displayed.
	/// </summary>
	public ScrollbarVisibility VerticalScrollbarVisibility
	{
		get => _verticalScrollbarVisibility;
		set { _verticalScrollbarVisibility = value; OnPropertyChanged(); Invalidate(Invalidation.Relayout); }
	}

	/// <summary>
	/// Gets or sets when the horizontal scrollbar should be displayed.
	/// </summary>
	public ScrollbarVisibility HorizontalScrollbarVisibility
	{
		get => _horizontalScrollbarVisibility;
		set { _horizontalScrollbarVisibility = value; OnPropertyChanged(); Invalidate(Invalidation.Relayout); }
	}

	/// <summary>
	/// Gets or sets the minimum vertical scrollbar thumb height, in rows.
	/// </summary>
	public int MinScrollbarThumbSize
	{
		get => _minScrollbarThumbSize;
		set { if (_minScrollbarThumbSize == value) return; _minScrollbarThumbSize = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
	}

	/// <summary>
	/// Gets or sets the number of rows (or columns, horizontally) scrolled per mouse wheel notch.
	/// Values below 1 are clamped to 1.
	/// Default: <see cref="ControlDefaults.DefaultScrollWheelLines"/>.
	/// </summary>
	public int MouseWheelScrollSpeed
	{
		get => _mouseWheelScrollSpeed;
		set { _mouseWheelScrollSpeed = Math.Max(1, value); OnPropertyChanged(); }
	}

	#endregion

	#region Scroll State

	/// <summary>
	/// Gets the number of visible rows based on the current rendering area.
	/// </summary>
	public int GetVisibleRowCount()
	{
		// A wrapping column makes this a walk rather than a division: rows differ in height, so
		// how many fit depends on WHICH rows, starting from the current scroll offset.
		if (HasWrappingColumns)
		{
			int lines = AvailableDataLines();
			return lines <= 0 ? 1 : CountVisibleRowsFrom(_scrollOffset, lines);
		}

		// Use actual rendered height if available (set during PaintDOM)
		if (ActualHeight > 0)
			return CalculateVisibleRowsFromHeight(ActualHeight);

		// Use explicit Height property if set
		if (Height.HasValue)
			return CalculateVisibleRowsFromHeight(Height.Value);

		// Try container-provided height
		int? containerHeight = Container?.GetVisibleHeightForControl(this);
		if (containerHeight.HasValue && containerHeight.Value > 0)
			return CalculateVisibleRowsFromHeight(containerHeight.Value);

		// No height constraint - all rows are visible (no scrollbar needed)
		return RowCount;
	}

	/// <summary>
	/// The lines left for data rows after the title, borders, header, scrollbar and filter bar,
	/// at whichever height this control currently knows about.
	/// </summary>
	/// <remarks>
	/// The chrome subtraction is the half of CalculateVisibleRowsFromHeight that does not assume
	/// a uniform row height, so the wrapping path reuses it rather than keeping a second copy
	/// that would drift the first time a piece of chrome changed.
	/// </remarks>
	private int AvailableDataLines()
	{
		int totalHeight =
			ActualHeight > 0 ? ActualHeight
			: Height ?? Container?.GetVisibleHeightForControl(this) ?? 0;

		return totalHeight <= 0 ? 0 : Math.Max(0, totalHeight - ChromeHeight());
	}

	/// <summary>Lines this table spends on everything that is not a data row.</summary>
	private int ChromeHeight()
	{
		int usedHeight = 0;
		bool hasBorder = _borderStyle != BorderStyle.None;

		usedHeight += Margin.Top + Margin.Bottom;
		if (!string.IsNullOrEmpty(_title)) usedHeight++;
		if (hasBorder) usedHeight++;                        // top border
		if (_showHeader) usedHeight++;
		if (_showHeader && hasBorder) usedHeight++;         // header separator
		if (hasBorder) usedHeight++;                        // bottom border
		if (ShouldShowHorizontalScrollbar()) usedHeight++;
		if (_filteringEnabled && !_readOnly && hasBorder) usedHeight += 2;

		return usedHeight;
	}

	/// <summary>
	/// How many rows starting at <paramref name="firstRow"/> fit in <paramref name="availableLines"/>,
	/// counting each row's real height.
	/// </summary>
	/// <remarks>
	/// Always at least 1. A row taller than the whole viewport cannot fit, but reporting 0 rows
	/// visible would leave nothing to draw, nothing to select and no way to scroll off it; one
	/// partly shown row is the honest answer and the only usable one.
	/// </remarks>
	internal int CountVisibleRowsFrom(int firstRow, int availableLines)
	{
		int rowCount = RowCount;
		if (rowCount == 0) return 0;

		int used = 0;
		int rows = 0;
		bool separators = _showRowSeparators && _borderStyle != BorderStyle.None;

		for (int r = Math.Max(0, firstRow); r < rowCount; r++)
		{
			int h = GetRowHeight(r) + (separators ? 1 : 0);
			if (used + h > availableLines) break;
			used += h;
			rows++;
		}

		return Math.Max(1, rows);
	}

	private int CalculateVisibleRowsFromHeight(int totalHeight)
	{
		int usedHeight = 0;
		bool hasBorder = _borderStyle != BorderStyle.None;

		usedHeight += Margin.Top + Margin.Bottom;
		if (!string.IsNullOrEmpty(_title)) usedHeight++;
		if (hasBorder) usedHeight++; // top border
		if (_showHeader) usedHeight++;
		if (_showHeader && hasBorder) usedHeight++; // header separator
		if (hasBorder) usedHeight++; // bottom border

		// Reserve space for horizontal scrollbar if visible
		if (ShouldShowHorizontalScrollbar())
			usedHeight++;

		// Reserve space for filter status bar (separator + status row) — only rendered with borders
		if (_filteringEnabled && !_readOnly && hasBorder)
			usedHeight += 2;

		int availableLines = Math.Max(0, totalHeight - usedHeight);

		// Account for row separators: each row except the last has a separator line
		if (_showRowSeparators && hasBorder && availableLines > 1)
			return Math.Max(1, (availableLines + 1) / 2);

		return Math.Max(1, availableLines);
	}

	/// <summary>
	/// Ensures the selected row is visible by adjusting scroll offset.
	/// </summary>
	internal void EnsureSelectedRowVisible()
	{
		if (_selectedRowIndex < 0) return;

		int visibleRows = GetVisibleRowCount();
		int oldOffset = _scrollOffset;

		if (_selectedRowIndex < _scrollOffset)
		{
			_scrollOffset = _selectedRowIndex;
		}
		else if (_selectedRowIndex >= _scrollOffset + visibleRows)
		{
			if (HasWrappingColumns)
			{
				// With rows of different heights, "the selection minus a row count" can land
				// anywhere. Walk back from the selected row instead, taking rows while they fit,
				// so the selected row is fully shown with as much above it as there is room for.
				// A row taller than the viewport takes the offset to itself and no further, which
				// shows its first lines rather than scrolling past it.
				int lines = AvailableDataLines();
				int used = 0;
				int offset = _selectedRowIndex;
				bool separators = _showRowSeparators && _borderStyle != BorderStyle.None;

				for (int r = _selectedRowIndex; r >= 0; r--)
				{
					int h = GetRowHeight(r) + (separators ? 1 : 0);
					if (r < _selectedRowIndex && used + h > lines) break;
					used += h;
					offset = r;
				}

				_scrollOffset = offset;
			}
			else
			{
				_scrollOffset = _selectedRowIndex - visibleRows + 1;
			}
		}

		if (_scrollOffset != oldOffset)
			_hoveredRowIndex = -1;

		// Clamp
		int maxOffset = Math.Max(0, RowCount - visibleRows);
		_scrollOffset = Math.Clamp(_scrollOffset, 0, maxOffset);
	}

	/// <summary>
	/// Whether the vertical scrollbar should be shown based on current state.
	/// </summary>
	internal bool ShouldShowVerticalScrollbar()
	{
		return _verticalScrollbarVisibility switch
		{
			ScrollbarVisibility.Always => true,
			ScrollbarVisibility.Never => false,
			_ => RowCount > GetVisibleRowCount()
		};
	}

	/// <summary>
	/// Whether the horizontal scrollbar should be shown based on current state.
	/// </summary>
	internal bool ShouldShowHorizontalScrollbar()
	{
		// Will be determined during rendering based on total column width vs viewport
		return _horizontalScrollbarVisibility == ScrollbarVisibility.Always;
	}

	/// <summary>
	/// Whether the horizontal scrollbar should be shown based on actual column widths.
	/// </summary>
	internal bool ShouldShowHorizontalScrollbar(int totalColumnsWidth, int viewportWidth)
	{
		return _horizontalScrollbarVisibility switch
		{
			ScrollbarVisibility.Always => true,
			ScrollbarVisibility.Never => false,
			_ => totalColumnsWidth > viewportWidth
		};
	}

	#endregion

	#region Scrollbar Rectangle

	/// <summary>
	/// The vertical scrollbar's rectangle, control-relative: the column it paints on, the row its
	/// track starts at, and the track's height. Single source of truth for both the render path
	/// (<c>TableControl.Rendering.cs</c>) and hit testing (<c>TableControl.Mouse.cs</c>) — before this
	/// unification the two independently walked title/border/header/separator rows to derive the same
	/// numbers, which is exactly the kind of duplication that drifts into an off-by-one.
	/// </summary>
	/// <remarks>
	/// The Y math mirrors the render path's top-chrome walk (title, top border, header, header
	/// separator) exactly; the height counts the same rows the render path fills before the bottom
	/// border (including the filter status bar), reserving the horizontal scrollbar's row when shown.
	/// </remarks>
	internal (int x, int y, int height) GetVerticalScrollbarRect()
	{
		int dataStartY = Margin.Top;
		if (!string.IsNullOrEmpty(_title)) dataStartY++;
		if (_borderStyle != BorderStyle.None) dataStartY++;
		if (_showHeader) dataStartY++;
		if (_showHeader && _borderStyle != BorderStyle.None) dataStartY++;

		int height = ActualHeight - dataStartY - Margin.Bottom;
		if (_borderStyle != BorderStyle.None) height--; // bottom border
		if (ShouldShowHorizontalScrollbar()) height--;
		height = Math.Max(0, height);

		int x = ActualWidth - Margin.Right - 1;
		return (x, dataStartY, height);
	}

	/// <summary>
	/// The horizontal scrollbar's rectangle, control-relative: the row it paints on, the column its
	/// track starts at, and the track's width. Mirrors <see cref="GetVerticalScrollbarRect"/> for the
	/// horizontal axis; single source of truth for render and hit testing.
	/// </summary>
	internal (int x, int y, int width) GetHorizontalScrollbarRect()
	{
		int y = ActualHeight - Margin.Bottom - 1;

		int width = ActualWidth - Margin.Left - Margin.Right;
		if (_borderStyle != BorderStyle.None) width -= 2; // left + right border
		if (ShouldShowVerticalScrollbar()) width--;
		width = Math.Max(0, width);

		int x = Margin.Left + (_borderStyle != BorderStyle.None ? 1 : 0);
		return (x, y, width);
	}

	#endregion

	#region Scrollbar Geometry

	/// <summary>
	/// The shared engine's view of the vertical scrollbar's geometry inputs. <see cref="MinScrollbarThumbSize"/>
	/// applies to this axis only — the horizontal metrics below deliberately omit it, preserving the
	/// original asymmetry (the horizontal thumb's minimum has always been the engine default of 1).
	/// </summary>
	private ScrollbarMetrics VerticalScrollbarMetrics(int contentAreaHeight) =>
		new(contentAreaHeight, RowCount, GetVisibleRowCount(), _scrollOffset, _minScrollbarThumbSize);

	/// <summary>The shared engine's view of the horizontal scrollbar's geometry inputs.</summary>
	private ScrollbarMetrics HorizontalScrollbarMetrics(int contentAreaWidth, int totalColumnsWidth) =>
		new(contentAreaWidth, totalColumnsWidth, contentAreaWidth, _horizontalScrollOffset);

	/// <summary>
	/// Calculates the vertical scrollbar geometry (relative to content area).
	/// </summary>
	internal (int trackTop, int trackHeight, int thumbY, int thumbHeight) GetVerticalScrollbarGeometry(int contentAreaHeight)
	{
		if (contentAreaHeight <= 0) return (0, 0, 0, 0);

		var metrics = VerticalScrollbarMetrics(contentAreaHeight);
		if (RowCount <= GetVisibleRowCount()) return (0, contentAreaHeight, 0, contentAreaHeight);

		return (0, contentAreaHeight,
			ScrollbarGeometry.ThumbPosForOffset(metrics),
			ScrollbarGeometry.ThumbLength(metrics));
	}

	/// <summary>
	/// Calculates the horizontal scrollbar geometry (relative to content area).
	/// </summary>
	internal (int trackLeft, int trackWidth, int thumbX, int thumbWidth) GetHorizontalScrollbarGeometry(int contentAreaWidth, int totalColumnsWidth)
	{
		if (contentAreaWidth <= 0 || totalColumnsWidth <= contentAreaWidth)
			return (0, contentAreaWidth, 0, contentAreaWidth);

		var metrics = HorizontalScrollbarMetrics(contentAreaWidth, totalColumnsWidth);
		return (0, contentAreaWidth,
			ScrollbarGeometry.ThumbPosForOffset(metrics),
			ScrollbarGeometry.ThumbLength(metrics));
	}

	#endregion

	#region Scrollbar Drawing

	/// <summary>
	/// Draws the vertical scrollbar.
	/// </summary>
	internal void DrawVerticalScrollbar(CharacterBuffer buffer, int x, int startY, int height, Color bgColor)
	{
		if (height <= 0) return;

		var palette = ResolveScrollbarPalette(bgColor);
		ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Vertical, x, startY,
			VerticalScrollbarMetrics(height), palette);
	}

	/// <summary>
	/// Draws the horizontal scrollbar.
	/// </summary>
	internal void DrawHorizontalScrollbar(CharacterBuffer buffer, int startX, int y, int width, int totalColumnsWidth, Color bgColor)
	{
		if (width <= 0) return;

		var palette = ResolveScrollbarPalette(bgColor);
		ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Horizontal, startX, y,
			HorizontalScrollbarMetrics(width, totalColumnsWidth), palette);
	}

	#endregion
}

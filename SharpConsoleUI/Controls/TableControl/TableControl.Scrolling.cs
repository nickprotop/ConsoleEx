// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

public partial class TableControl
{
	#region Scroll Properties

	/// <summary>
	/// Gets or sets the vertical scroll offset (first visible row index).
	/// </summary>
	public int ScrollOffset
	{
		get => _scrollOffset;
		set
		{
			int maxOffset = Math.Max(0, RowCount - GetVisibleRowCount());
			_scrollOffset = Math.Clamp(value, 0, maxOffset);
			Container?.Invalidate(true);
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
			Container?.Invalidate(true);
		}
	}

	/// <summary>
	/// Gets or sets when the vertical scrollbar should be displayed.
	/// </summary>
	public ScrollbarVisibility VerticalScrollbarVisibility
	{
		get => _verticalScrollbarVisibility;
		set { _verticalScrollbarVisibility = value; Container?.Invalidate(true); }
	}

	/// <summary>
	/// Gets or sets when the horizontal scrollbar should be displayed.
	/// </summary>
	public ScrollbarVisibility HorizontalScrollbarVisibility
	{
		get => _horizontalScrollbarVisibility;
		set { _horizontalScrollbarVisibility = value; Container?.Invalidate(true); }
	}

	#endregion

	#region Scroll State

	/// <summary>
	/// Gets the number of visible rows based on the current rendering area.
	/// </summary>
	public int GetVisibleRowCount()
	{
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

		// Reserve space for filter status bar (separator + status row)
		if (_filteringEnabled && !_readOnly)
			usedHeight += 2;

		return Math.Max(1, totalHeight - usedHeight);
	}

	/// <summary>
	/// Ensures the selected row is visible by adjusting scroll offset.
	/// </summary>
	internal void EnsureSelectedRowVisible()
	{
		if (_selectedRowIndex < 0) return;

		int visibleRows = GetVisibleRowCount();

		if (_selectedRowIndex < _scrollOffset)
		{
			_scrollOffset = _selectedRowIndex;
		}
		else if (_selectedRowIndex >= _scrollOffset + visibleRows)
		{
			_scrollOffset = _selectedRowIndex - visibleRows + 1;
		}

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

	#region Scrollbar Geometry

	/// <summary>
	/// Calculates the vertical scrollbar geometry (relative to content area).
	/// </summary>
	internal (int trackTop, int trackHeight, int thumbY, int thumbHeight) GetVerticalScrollbarGeometry(int contentAreaHeight)
	{
		int trackTop = 0;
		int trackHeight = contentAreaHeight;
		if (trackHeight <= 0) return (0, 0, 0, 0);

		int totalRows = RowCount;
		int visibleRows = GetVisibleRowCount();
		if (totalRows <= visibleRows) return (trackTop, trackHeight, 0, trackHeight);

		// Reserve first and last positions for arrows
		int arrowSlots = trackHeight >= 3 ? 2 : 0;
		int thumbTrackHeight = trackHeight - arrowSlots;
		if (thumbTrackHeight <= 0) return (trackTop, trackHeight, 0, trackHeight);

		double viewportRatio = (double)visibleRows / totalRows;
		int thumbHeight = Math.Max(1, (int)(thumbTrackHeight * viewportRatio));
		double scrollRatio = (double)_scrollOffset / Math.Max(1, totalRows - visibleRows);
		int thumbY = arrowSlots > 0 ? 1 : 0; // start after top arrow
		thumbY += (int)((thumbTrackHeight - thumbHeight) * scrollRatio);

		return (trackTop, trackHeight, thumbY, thumbHeight);
	}

	/// <summary>
	/// Calculates the horizontal scrollbar geometry (relative to content area).
	/// </summary>
	internal (int trackLeft, int trackWidth, int thumbX, int thumbWidth) GetHorizontalScrollbarGeometry(int contentAreaWidth, int totalColumnsWidth)
	{
		int trackLeft = 0;
		int trackWidth = contentAreaWidth;
		if (trackWidth <= 0 || totalColumnsWidth <= contentAreaWidth)
			return (trackLeft, trackWidth, 0, trackWidth);

		// Reserve first and last positions for arrows
		int arrowSlots = trackWidth >= 3 ? 2 : 0;
		int thumbTrackWidth = trackWidth - arrowSlots;
		if (thumbTrackWidth <= 0) return (trackLeft, trackWidth, 0, trackWidth);

		double viewportRatio = (double)contentAreaWidth / totalColumnsWidth;
		int thumbWidth = Math.Max(1, (int)(thumbTrackWidth * viewportRatio));
		int maxHScroll = totalColumnsWidth - contentAreaWidth;
		double scrollRatio = maxHScroll > 0 ? (double)_horizontalScrollOffset / maxHScroll : 0;
		int thumbX = arrowSlots > 0 ? 1 : 0; // start after left arrow
		thumbX += (int)((thumbTrackWidth - thumbWidth) * scrollRatio);

		return (trackLeft, trackWidth, thumbX, thumbWidth);
	}

	#endregion

	#region Scrollbar Drawing

	/// <summary>
	/// Draws the vertical scrollbar.
	/// </summary>
	internal void DrawVerticalScrollbar(CharacterBuffer buffer, int x, int startY, int height, Color bgColor)
	{
		var (trackTop, trackHeight, thumbY, thumbHeight) = GetVerticalScrollbarGeometry(height);
		if (trackHeight <= 0) return;

		Color thumbColor = ResolveScrollbarThumbColor();
		Color trackColor = ResolveScrollbarTrackColor();
		bool hasArrows = trackHeight >= 3;

		for (int y = 0; y < trackHeight; y++)
		{
			int absY = startY + trackTop + y;
			if (y >= thumbY && y < thumbY + thumbHeight)
			{
				buffer.SetCell(x, absY, '\u2588', thumbColor, bgColor); // █ thumb
			}
			else
			{
				buffer.SetCell(x, absY, '\u2502', trackColor, bgColor); // │ track
			}
		}

		// Arrow indicators at fixed positions (first and last)
		if (hasArrows)
		{
			buffer.SetCell(x, startY + trackTop, '\u25b2', thumbColor, bgColor); // ▲
			buffer.SetCell(x, startY + trackTop + trackHeight - 1, '\u25bc', thumbColor, bgColor); // ▼
		}
	}

	/// <summary>
	/// Draws the horizontal scrollbar.
	/// </summary>
	internal void DrawHorizontalScrollbar(CharacterBuffer buffer, int startX, int y, int width, int totalColumnsWidth, Color bgColor)
	{
		var (trackLeft, trackWidth, thumbX, thumbWidth) = GetHorizontalScrollbarGeometry(width, totalColumnsWidth);
		if (trackWidth <= 0) return;

		Color thumbColor = ResolveScrollbarThumbColor();
		Color trackColor = ResolveScrollbarTrackColor();
		bool hasArrows = trackWidth >= 3;

		for (int xOff = 0; xOff < trackWidth; xOff++)
		{
			int absX = startX + trackLeft + xOff;
			if (xOff >= thumbX && xOff < thumbX + thumbWidth)
			{
				buffer.SetCell(absX, y, '\u25ac', thumbColor, bgColor); // ▬ thumb
			}
			else
			{
				buffer.SetCell(absX, y, '\u2500', trackColor, bgColor); // ─ track
			}
		}

		// Arrow indicators at fixed positions (first and last)
		if (hasArrows)
		{
			buffer.SetCell(startX + trackLeft, y, '\u25c4', thumbColor, bgColor); // ◄
			buffer.SetCell(startX + trackLeft + trackWidth - 1, y, '\u25ba', thumbColor, bgColor); // ►
		}
	}

	#endregion
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class CommandPaletteControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width = _paletteWidth;
			int visibleItems = Math.Min(_maxVisibleItems, _filteredItems.Count);
			int height = ControlDefaults.CommandPaletteSearchBarHeight
				+ visibleItems * ControlDefaults.CommandPaletteItemHeight;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);
			if (!_isVisible) return;

			var theme = Container?.GetConsoleWindowSystem?.Theme;
			Color bgColor = theme?.WindowBackgroundColor ?? defaultBg;
			Color fgColor = theme?.WindowForegroundColor ?? defaultFg;
			Color highlightBg = theme?.ListUnfocusedHighlightBackgroundColor ?? Color.Blue;
			Color highlightFg = theme?.ListUnfocusedHighlightForegroundColor ?? Color.White;
			Color dimColor = theme?.InactiveBorderForegroundColor ?? Color.Grey;

			int startX = bounds.X;
			int startY = bounds.Y;
			int width = bounds.Width;

			// Paint search bar
			PaintSearchBar(buffer, startX, startY, width, clipRect, fgColor, bgColor, dimColor);

			// Paint filtered items
			int itemStartY = startY + ControlDefaults.CommandPaletteSearchBarHeight;
			PaintItems(buffer, startX, itemStartY, width, clipRect,
				fgColor, bgColor, highlightFg, highlightBg, dimColor);
		}

		#endregion

		#region Rendering Helpers

		private void PaintSearchBar(CharacterBuffer buffer, int x, int y, int width,
			LayoutRect clipRect, Color fgColor, Color bgColor, Color dimColor)
		{
			// Top border line
			if (y >= clipRect.Y && y < clipRect.Bottom)
			{
				string topBorder = "\u250c" + new string('\u2500', Math.Max(0, width - 2)) + "\u2510";
				var topCells = Parsing.MarkupParser.Parse(topBorder, fgColor, bgColor);
				buffer.WriteCellsClipped(x, y, topCells, clipRect);
			}

			// Search text line
			int searchY = y + 1;
			if (searchY >= clipRect.Y && searchY < clipRect.Bottom)
			{
				int innerWidth = Math.Max(0, width - 2);
				int prefixLen = 4; // "| X " where X is search icon

				string displayText;
				Color textColor;
				if (_searchText.Length > 0)
				{
					displayText = _searchText;
					textColor = fgColor;
				}
				else
				{
					displayText = _placeholder;
					textColor = dimColor;
				}

				int maxTextLen = Math.Max(0, innerWidth - prefixLen);
				if (displayText.Length > maxTextLen)
					displayText = displayText.Substring(0, maxTextLen);

				int paddingLen = Math.Max(0, innerWidth - prefixLen - displayText.Length);

				// Build the search line manually with cells
				string lineContent = "\u2502 > " + displayText + new string(' ', paddingLen) + "\u2502";
				var searchCells = Parsing.MarkupParser.Parse(lineContent, textColor, bgColor);
				buffer.WriteCellsClipped(x, searchY, searchCells, clipRect);
			}

			// Bottom border of search bar
			int borderY = y + 2;
			if (borderY >= clipRect.Y && borderY < clipRect.Bottom)
			{
				string midBorder = "\u251c" + new string('\u2500', Math.Max(0, width - 2)) + "\u2524";
				var midCells = Parsing.MarkupParser.Parse(midBorder, fgColor, bgColor);
				buffer.WriteCellsClipped(x, borderY, midCells, clipRect);
			}
		}

		private void PaintItems(CharacterBuffer buffer, int x, int y, int width,
			LayoutRect clipRect, Color fgColor, Color bgColor,
			Color highlightFg, Color highlightBg, Color dimColor)
		{
			int visibleCount = Math.Min(_maxVisibleItems, _filteredItems.Count - _scrollOffset);
			int innerWidth = Math.Max(0, width - 2);
			string? lastCategory = null;
			int paintY = y;

			for (int i = 0; i < visibleCount; i++)
			{
				if (paintY >= clipRect.Bottom)
					break;

				int itemIndex = i + _scrollOffset;
				if (itemIndex >= _filteredItems.Count)
					break;

				var (item, matchResult) = _filteredItems[itemIndex];

				// Category header
				if (_showCategories && !string.IsNullOrEmpty(item.Category) && item.Category != lastCategory)
				{
					lastCategory = item.Category;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
					{
						PaintCategoryHeader(buffer, x, paintY, width, clipRect, item.Category, dimColor, bgColor);
					}
					paintY++;
					if (paintY >= clipRect.Bottom)
						break;
				}

				if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
				{
					bool isSelected = (itemIndex == _selectedIndex);
					Color itemBg = isSelected ? highlightBg : bgColor;
					Color itemFg = isSelected ? highlightFg : fgColor;
					Color itemDim = isSelected ? highlightFg : dimColor;

					PaintSingleItem(buffer, x, paintY, width, innerWidth, clipRect,
						item, matchResult, itemFg, itemBg, itemDim);
				}

				paintY++;
			}

			// Fill remaining rows with empty lines (border continuation)
			int totalHeight = _maxVisibleItems;
			for (int row = visibleCount; row < totalHeight; row++)
			{
				if (paintY >= clipRect.Bottom)
					break;
				if (paintY >= clipRect.Y)
				{
					string emptyLine = "\u2502" + new string(' ', innerWidth) + "\u2502";
					var emptyCells = Parsing.MarkupParser.Parse(emptyLine, fgColor, bgColor);
					buffer.WriteCellsClipped(x, paintY, emptyCells, clipRect);
				}
				paintY++;
			}

			// Bottom border
			if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
			{
				// Scroll indicator
				bool hasMore = _filteredItems.Count > _scrollOffset + _maxVisibleItems;
				bool hasAbove = _scrollOffset > 0;
				string statusText = _filteredItems.Count + " items";
				if (hasAbove || hasMore)
					statusText = (hasAbove ? "\u25b2" : " ") + " " + statusText + " " + (hasMore ? "\u25bc" : " ");

				int statusLen = statusText.Length;
				int borderFillLen = Math.Max(0, Math.Max(0, width - 2) - statusLen);
				string bottomBorder = "\u2514" + new string('\u2500', borderFillLen / 2)
					+ statusText + new string('\u2500', borderFillLen - borderFillLen / 2) + "\u2518";

				var bottomCells = Parsing.MarkupParser.Parse(bottomBorder, dimColor, bgColor);
				buffer.WriteCellsClipped(x, paintY, bottomCells, clipRect);
			}
		}

		private void PaintCategoryHeader(CharacterBuffer buffer, int x, int y, int width,
			LayoutRect clipRect, string category, Color dimColor, Color bgColor)
		{
			int innerWidth = Math.Max(0, width - 2);
			string categoryText = " " + category + " ";
			int paddingLen = Math.Max(0, innerWidth - categoryText.Length);
			string line = "\u2502" + categoryText + new string('\u2500', paddingLen) + "\u2502";

			var cells = Parsing.MarkupParser.Parse(line, dimColor, bgColor);
			buffer.WriteCellsClipped(x, y, cells, clipRect);
		}

		private void PaintSingleItem(CharacterBuffer buffer, int x, int y, int width, int innerWidth,
			LayoutRect clipRect, CommandPaletteItem item, FuzzyMatchResult matchResult,
			Color fgColor, Color bgColor, Color dimColor)
		{
			// Build item text: "| icon label          shortcut |"
			string iconText = !string.IsNullOrEmpty(item.Icon) ? item.Icon + " " : "  ";
			string labelText = item.Label;

			string shortcutText = string.Empty;
			if (_showShortcuts && !string.IsNullOrEmpty(item.Shortcut))
				shortcutText = " " + item.Shortcut;

			int iconLen = GetCachedTextLength(iconText);
			int shortcutLen = GetCachedTextLength(shortcutText);
			int availableForLabel = Math.Max(0, innerWidth - iconLen - shortcutLen);

			if (GetCachedTextLength(labelText) > availableForLabel)
			{
				int truncLen = Math.Max(0, availableForLabel - ControlDefaults.DefaultEllipsisLength);
				labelText = labelText.Substring(0, Math.Min(truncLen, labelText.Length)) + "...";
			}

			int labelLen = GetCachedTextLength(labelText);
			int paddingLen = Math.Max(0, availableForLabel - labelLen);
			string content = iconText + labelText + new string(' ', paddingLen) + shortcutText;

			// Ensure content fits inner width exactly
			int contentLen = GetCachedTextLength(content);
			if (contentLen < innerWidth)
				content += new string(' ', innerWidth - contentLen);

			string line = "\u2502" + content + "\u2502";

			if (!item.IsEnabled)
			{
				fgColor = dimColor;
			}

			var cells = Parsing.MarkupParser.Parse(line, fgColor, bgColor);
			buffer.WriteCellsClipped(x, y, cells, clipRect);
		}

		#endregion

		#region Mouse Handling

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!_isVisible || !_isEnabled)
				return false;

			// Mouse click on items area
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				int itemAreaStartY = ActualY + ControlDefaults.CommandPaletteSearchBarHeight;
				int clickedRow = args.Position.Y - itemAreaStartY;

				if (clickedRow >= 0 && clickedRow < _maxVisibleItems)
				{
					int clickedIndex = clickedRow + _scrollOffset;
					if (clickedIndex >= 0 && clickedIndex < _filteredItems.Count)
					{
						_selectedIndex = clickedIndex;
						SelectCurrentItem();
						return true;
					}
				}
			}
			else if (args.HasFlag(MouseFlags.WheeledUp))
			{
				MoveSelection(-ControlDefaults.DefaultScrollStep);
				return true;
			}
			else if (args.HasFlag(MouseFlags.WheeledDown))
			{
				MoveSelection(ControlDefaults.DefaultScrollStep);
				return true;
			}

			return true; // Absorb all mouse events when visible
		}

		#endregion
	}
}

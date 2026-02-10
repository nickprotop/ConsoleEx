// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	public partial class ListControl
	{
		/// <summary>
		/// Gets the actual rendered height in lines.
		/// </summary>
		public int? ContentHeight
		{
			get
			{
				// Calculate based on content
				bool hasTitle = !string.IsNullOrEmpty(_title);
				int titleHeight = hasTitle ? 1 : 0;
				int visibleItems = _calculatedMaxVisibleItems ?? _maxVisibleItems ?? Math.Min(10, _items.Count);
				int itemsHeight = 0;
				int scrollOffset = CurrentScrollOffset;
				for (int i = 0; i < Math.Min(visibleItems, _items.Count - scrollOffset); i++)
				{
					int itemIndex = i + scrollOffset;
					if (itemIndex < _items.Count)
						itemsHeight += _items[itemIndex].Lines.Count;
				}
				bool hasScrollIndicator = scrollOffset > 0 || scrollOffset + visibleItems < _items.Count;
				return titleHeight + itemsHeight + (hasScrollIndicator ? 1 : 0) + _margin.Top + _margin.Bottom;
			}
		}

		/// <summary>
		/// Gets the actual rendered width in characters.
		/// </summary>
		public int? ContentWidth
		{
			get
			{
				// Calculate based on content
				int maxItemWidth = 0;
				foreach (var item in _items)
				{
					int itemLength = GetCachedTextLength(item.Text + "    ");
					if (itemLength > maxItemWidth) maxItemWidth = itemLength;
				}

				// Calculate indicator space: only needed in Complex mode with markers
				int indicatorSpace = (_isSelectable && _selectionMode == ListSelectionMode.Complex && _showSelectionMarkers) ? 5 : 0;
				int titleLength = string.IsNullOrEmpty(_title) ? 0 : GetCachedTextLength(_title) + 5;

				int width = _width ?? Math.Max(maxItemWidth + indicatorSpace + 4, titleLength);
				return width + _margin.Left + _margin.Right;
			}
		}

		/// <summary>
		/// Gets or sets whether the control automatically adjusts its width to fit content.
		/// </summary>
		public bool AutoAdjustWidth
		{
			get => _autoAdjustWidth;
			set
			{
				if (_autoAdjustWidth == value) return;
				_autoAdjustWidth = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets a custom formatter for rendering list items.
		/// </summary>
		public ItemFormatterEvent? ItemFormatter
		{
			get => _itemFormatter;
			set
			{
				_itemFormatter = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			// Calculate content size directly
			bool hasTitle = !string.IsNullOrEmpty(_title);
			int titleHeight = hasTitle ? 1 : 0;
			int visibleItems = _calculatedMaxVisibleItems ?? _maxVisibleItems ?? Math.Min(10, _items.Count);
			int itemsHeight = 0;
			int scrollOffset = CurrentScrollOffset;

			for (int i = 0; i < Math.Min(visibleItems, _items.Count - scrollOffset); i++)
			{
				int itemIndex = i + scrollOffset;
				if (itemIndex < _items.Count)
					itemsHeight += _items[itemIndex].Lines.Count;
			}

			bool hasScrollIndicator = scrollOffset > 0 || scrollOffset + visibleItems < _items.Count;
			int height = titleHeight + itemsHeight + (hasScrollIndicator ? 1 : 0) + _margin.Top + _margin.Bottom;

			// Calculate indicator space: only needed in Complex mode with markers
			int indicatorSpace = (_isSelectable && _selectionMode == ListSelectionMode.Complex && _showSelectionMarkers) ? 5 : 0;
			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				int itemLength = GetCachedTextLength(item.Text + "    ");
				if (itemLength > maxItemWidth) maxItemWidth = itemLength;
			}

			int titleLength = string.IsNullOrEmpty(_title) ? 0 : GetCachedTextLength(_title) + 5;
			int width = _width ?? Math.Max(maxItemWidth + indicatorSpace + 4, titleLength);
			width += _margin.Left + _margin.Right;

			return new System.Drawing.Size(width, height);
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// Calculate indicator space: only needed in Complex mode with markers
			int indicatorSpace = (_isSelectable && _selectionMode == ListSelectionMode.Complex && _showSelectionMarkers) ? 5 : 0;

			// Calculate max item width
			int maxItemWidth = 0;
			foreach (var item in _items)
			{
				int itemLength = GetCachedTextLength(item.Text + "    ");
				if (itemLength > maxItemWidth) maxItemWidth = itemLength;
			}

			// Calculate list width
			int listWidth;
			if (_width.HasValue)
			{
				listWidth = _width.Value;
			}
			else if (_horizontalAlignment == HorizontalAlignment.Stretch)
			{
				listWidth = constraints.MaxWidth - _margin.Left - _margin.Right;
			}
			else
			{
				int titleLength = string.IsNullOrEmpty(_title) ? 0 : GetCachedTextLength(_title) + 5;
				listWidth = Math.Max(maxItemWidth + indicatorSpace + 4, titleLength);
				listWidth = Math.Max(listWidth, 40);
			}

			if (_autoAdjustWidth)
			{
				int contentWidth = 0;
				foreach (var item in _items)
				{
					int itemLength = GetCachedTextLength(item.Text + "    ");
					contentWidth = Math.Max(contentWidth, itemLength);
				}
				listWidth = Math.Max(listWidth, contentWidth + indicatorSpace + 4);
			}

			int width = listWidth + _margin.Left + _margin.Right;

			// Calculate height
			bool hasTitle = !string.IsNullOrEmpty(_title);
			int titleHeight = hasTitle ? 1 : 0;
			int scrollOffset = CurrentScrollOffset;

			int effectiveMaxVisibleItems;
			if (_maxVisibleItems.HasValue)
			{
				effectiveMaxVisibleItems = _maxVisibleItems.Value;
			}
			else if (_verticalAlignment == VerticalAlignment.Fill)
			{
				// Check if we have unbounded constraints
				bool isUnbounded = constraints.MaxHeight >= int.MaxValue / 2;

				if (isUnbounded)
				{
					// With unbounded constraints and Fill alignment, return a reasonable default
					// instead of trying to fit all items. The parent container should provide
					// proper bounded constraints during the arrange phase.
					effectiveMaxVisibleItems = Math.Min(10, _items.Count);
				}
				else
				{
					// When VerticalAlignment.Fill with bounded constraints, use available height
					int availableContentHeight = constraints.MaxHeight - titleHeight - _margin.Top - _margin.Bottom - 1;
					effectiveMaxVisibleItems = 0;
					int heightUsed = 0;
					for (int i = scrollOffset; i < _items.Count; i++)
					{
						int itemHeight = _items[i].Lines.Count;
						if (heightUsed + itemHeight <= availableContentHeight)
						{
							effectiveMaxVisibleItems++;
							heightUsed += itemHeight;
						}
						else break;
					}
					effectiveMaxVisibleItems = Math.Max(1, effectiveMaxVisibleItems);
				}
			}
			else
			{
				effectiveMaxVisibleItems = Math.Min(10, _items.Count);
			}

			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;

			int itemsHeight = 0;
			int itemsToShow = Math.Min(effectiveMaxVisibleItems, _items.Count - scrollOffset);
			for (int i = 0; i < itemsToShow; i++)
			{
				int itemIndex = i + scrollOffset;
				if (itemIndex < _items.Count)
					itemsHeight += _items[itemIndex].Lines.Count;
			}

			bool hasScrollIndicator = scrollOffset > 0 || scrollOffset + itemsToShow < _items.Count;
			int height = titleHeight + itemsHeight + (hasScrollIndicator ? 1 : 0) + _margin.Top + _margin.Bottom;

			// VerticalAlignment.Fill is handled during arrangement, not measurement.
			// Measurement should return actual content height, not constraints.MaxHeight.
			// This prevents integer overflow when measured with unbounded height.

			var result = new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);

			return result;
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			Color backgroundColor;
			Color foregroundColor;
			Color windowBackground = Container?.BackgroundColor ?? defaultBg;

			// Determine colors based on enabled/focused state
			if (!_isEnabled)
			{
				backgroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor ?? Color.Grey;
				foregroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor ?? Color.DarkSlateGray1;
			}
			else if (_hasFocus)
			{
				backgroundColor = FocusedBackgroundColor;
				foregroundColor = FocusedForegroundColor;
			}
			else
			{
				backgroundColor = BackgroundColor;
				foregroundColor = ForegroundColor;
			}

			// Calculate indicator space: only needed in Complex mode with markers
			int indicatorSpace = (_isSelectable && _selectionMode == ListSelectionMode.Complex && _showSelectionMarkers) ? 5 : 0;
			int listWidth = bounds.Width - _margin.Left - _margin.Right;
			if (listWidth <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;
			int currentY = startY;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, foregroundColor, windowBackground);

			bool hasTitle = !string.IsNullOrEmpty(_title);
			int scrollOffset = CurrentScrollOffset;
			int selectedIndex = CurrentSelectedIndex;
			int highlightedIndex = CurrentHighlightedIndex;

			// Note: Highlight initialization moved to SetFocus to avoid firing events during render

			// Render title
			if (hasTitle && currentY < bounds.Bottom)
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
				{
					// Fill left margin
					if (_margin.Left > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', foregroundColor, windowBackground);
					}

					string titleBarContent = _title;
					int titleLen = GetCachedTextLength(titleBarContent);
					if (titleLen < listWidth)
					{
						titleBarContent += new string(' ', listWidth - titleLen);
					}

					var titleAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(titleBarContent, listWidth, 1, false, backgroundColor, foregroundColor).FirstOrDefault() ?? "";
					var titleCells = AnsiParser.Parse(titleAnsi, foregroundColor, backgroundColor);
					buffer.WriteCellsClipped(startX, currentY, titleCells, clipRect);

					// Fill right margin
					if (_margin.Right > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', foregroundColor, windowBackground);
					}
				}
				currentY++;
			}

			// Calculate effective visible items
			int availableContentHeight = bounds.Height - _margin.Top - _margin.Bottom - (hasTitle ? 1 : 0) - 1;
			int effectiveMaxVisibleItems;

			if (_maxVisibleItems.HasValue)
			{
				effectiveMaxVisibleItems = _maxVisibleItems.Value;
			}
			else
			{
				effectiveMaxVisibleItems = 0;
				int heightUsed = 0;
				for (int i = scrollOffset; i < _items.Count; i++)
				{
					int itemHeight = _items[i].Lines.Count;
					if (heightUsed + itemHeight <= availableContentHeight)
					{
						effectiveMaxVisibleItems++;
						heightUsed += itemHeight;
					}
					else break;
				}
				effectiveMaxVisibleItems = Math.Max(1, effectiveMaxVisibleItems);
			}

			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;

			int itemsToShow = Math.Min(effectiveMaxVisibleItems, _items.Count - scrollOffset);

			// Render each visible item
			for (int i = 0; i < itemsToShow && currentY < bounds.Bottom - _margin.Bottom - 1; i++)
			{
				int itemIndex = i + scrollOffset;
				if (itemIndex >= _items.Count) break;

				List<string> itemLines = _items[itemIndex].Lines;

				for (int lineIndex = 0; lineIndex < itemLines.Count && currentY < bounds.Bottom - _margin.Bottom - 1; lineIndex++)
				{
					if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
					{
						// Fill left margin
						if (_margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', foregroundColor, windowBackground);
						}

						string lineText = itemLines[lineIndex];
						if (lineIndex == 0 && _itemFormatter != null)
						{
							lineText = _itemFormatter(_items[itemIndex], itemIndex == selectedIndex, _hasFocus);
						}

						// Truncate if necessary
						int maxTextWidth = listWidth - (indicatorSpace + 2);
						if (maxTextWidth > ControlDefaults.DefaultMinTextWidth)
						{
							lineText = TextTruncationHelper.Truncate(lineText, maxTextWidth, cache: _textMeasurementCache);
						}

						// Determine colors for this item
						// Priority: Disabled > Hovered > Highlighted > Selected > Normal
						Color itemBg, itemFg;
						bool isHovered = (itemIndex == _hoveredIndex);

						if (!IsEnabled)
						{
							itemBg = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor ?? Color.Grey;
							itemFg = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor ?? Color.DarkSlateGray1;
						}
						else if (isHovered && _hoverHighlightsItems && _hasFocus)
						{
							// Hover takes precedence when control has focus
							// Use theme hover colors if available, otherwise fall back to highlight colors
							var theme = Container?.GetConsoleWindowSystem?.Theme;
							itemBg = theme?.ListHoverBackgroundColor ?? HighlightBackgroundColor;
							itemFg = theme?.ListHoverForegroundColor ?? HighlightForegroundColor;
						}
						else if (_isSelectable && itemIndex == highlightedIndex && _hasFocus)
						{
							itemBg = HighlightBackgroundColor;
							itemFg = HighlightForegroundColor;
						}
						else if (_isSelectable && itemIndex == highlightedIndex && !_hasFocus)
						{
							itemBg = Container?.GetConsoleWindowSystem?.Theme?.ListUnfocusedHighlightBackgroundColor ?? HighlightBackgroundColor;
							itemFg = Container?.GetConsoleWindowSystem?.Theme?.ListUnfocusedHighlightForegroundColor ?? Color.Grey;
						}
						else
						{
							itemBg = backgroundColor;
							itemFg = foregroundColor;
						}

						// Build item content with selection markers
						string selectionIndicator = "";
						if (_isSelectable && lineIndex == 0)
						{
							// Show markers only in Complex mode
							if (_selectionMode == ListSelectionMode.Complex && _showSelectionMarkers)
							{
								if (itemIndex == selectedIndex)
									selectionIndicator = "[ x ] ";
								else if (itemIndex == highlightedIndex && _hasFocus)
									selectionIndicator = "[ > ] ";
								else
									selectionIndicator = "     ";
							}
							// In Simple mode, no markers
						}
						else if (_isSelectable && lineIndex > 0)
						{
							// Continuation lines: add spacing if Complex mode
							if (_selectionMode == ListSelectionMode.Complex && _showSelectionMarkers)
							{
								selectionIndicator = "     ";
							}
						}

						string itemContent;
						if (lineIndex == 0 && _items[itemIndex].Icon != null)
						{
							string iconText = _items[itemIndex].Icon!;
							Color iconColor = _items[itemIndex].IconColor ?? itemFg;
							string iconMarkup = $"[{iconColor.ToMarkup()}]{iconText}[/] ";
							int iconVisibleLength = GetCachedTextLength(iconText) + 1;
							itemContent = selectionIndicator + iconMarkup + lineText;
							int visibleTextLength = selectionIndicator.Length + iconVisibleLength + GetCachedTextLength(lineText);
							int paddingNeeded = Math.Max(0, listWidth - visibleTextLength);
							if (paddingNeeded > 0) itemContent += new string(' ', paddingNeeded);
						}
						else
						{
							string indent = "";
							if (lineIndex > 0 && _items[itemIndex].Icon != null)
							{
								string iconText = _items[itemIndex].Icon!;
								int iconWidth = GetCachedTextLength(iconText) + 1;
								indent = new string(' ', iconWidth);
							}
							itemContent = selectionIndicator + indent + lineText;
							int visibleTextLength = selectionIndicator.Length + indent.Length + GetCachedTextLength(lineText);
							int paddingNeeded = Math.Max(0, listWidth - visibleTextLength);
							if (paddingNeeded > 0) itemContent += new string(' ', paddingNeeded);
						}

						var itemAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(itemContent, listWidth, 1, false, itemBg, itemFg).FirstOrDefault() ?? "";
						var itemCells = AnsiParser.Parse(itemAnsi, itemFg, itemBg);
						buffer.WriteCellsClipped(startX, currentY, itemCells, clipRect);

						// Fill right margin
						if (_margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', foregroundColor, windowBackground);
						}
					}
					currentY++;
				}
			}

			// Fill empty lines if VerticalAlignment.Fill
			if (_verticalAlignment == VerticalAlignment.Fill)
			{
				int scrollIndicatorY = bounds.Bottom - _margin.Bottom - 1;
				while (currentY < scrollIndicatorY)
				{
					if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
					{
						if (_margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', foregroundColor, windowBackground);
						}
						buffer.FillRect(new LayoutRect(startX, currentY, listWidth, 1), ' ', foregroundColor, backgroundColor);
						if (_margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', foregroundColor, windowBackground);
						}
					}
					currentY++;
				}
			}

			// Render scroll indicators
			bool hasScrollIndicator = scrollOffset > 0 || scrollOffset + itemsToShow < _items.Count;
			if (hasScrollIndicator && currentY < bounds.Bottom - _margin.Bottom)
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
				{
					if (_margin.Left > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.X, currentY, _margin.Left, 1), ' ', foregroundColor, windowBackground);
					}

					string scrollIndicator = "";
					scrollIndicator += scrollOffset > 0 ? "▲" : " ";
					int scrollPadding = listWidth - 2;
					if (scrollPadding > 0) scrollIndicator += new string(' ', scrollPadding);
					scrollIndicator += (scrollOffset + itemsToShow < _items.Count) ? "▼" : " ";

					var scrollAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(scrollIndicator, listWidth, 1, false, backgroundColor, foregroundColor).FirstOrDefault() ?? "";
					var scrollCells = AnsiParser.Parse(scrollAnsi, foregroundColor, backgroundColor);
					buffer.WriteCellsClipped(startX, currentY, scrollCells, clipRect);

					if (_margin.Right > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, currentY, _margin.Right, 1), ' ', foregroundColor, windowBackground);
					}
				}
				currentY++;
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, currentY, foregroundColor, windowBackground);
		}

		#endregion
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using System.Drawing;
using Size = System.Drawing.Size;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class DropdownControl
	{
		#region Portal Methods

		/// <summary>
		/// Opens the dropdown and creates the portal overlay.
		/// </summary>
		private void OpenDropdown()
		{
			if (_isDropdownOpen) return;

			_isDropdownOpen = true;

			// Calculate portal bounds
			CalculatePortalBounds();

			// Create portal content and add to window
			var window = Container as Window ?? FindContainingWindow();
			if (window != null)
			{
				_portalContent = new DropdownPortalContent(this);
				_portalContent.DismissOnOutsideClick = true;
				_portalContent.DismissRequested += (s, e) =>
				{
					_dismissedByOutsideClick = true;
					CloseDropdown();
				};
				_dropdownPortal = window.CreatePortal(this, _portalContent);
			}

			Container?.Invalidate(true);
		}

		/// <summary>
		/// Closes the dropdown and removes the portal overlay.
		/// </summary>
		private void CloseDropdown()
		{
			if (!_isDropdownOpen) return;

			// Remove portal
			if (_dropdownPortal != null)
			{
				var window = Container as Window ?? FindContainingWindow();
				if (window != null)
				{
					window.RemovePortal(this, _dropdownPortal);
				}
				_dropdownPortal = null;
				_portalContent = null;
			}

			_isDropdownOpen = false;
			_mouseHoveredIndex = -1;

			Container?.Invalidate(true);
		}

		/// <summary>
		/// Calculates the portal bounds with auto-flip logic.
		/// If the dropdown would extend past the window bottom, it flips upward.
		/// </summary>
		private void CalculatePortalBounds()
		{
			// Get screen dimensions
			int screenWidth = 160;
			int screenHeight = 40;

			var window = Container as Window ?? FindContainingWindow();
			if (window != null)
			{
				screenWidth = window.Width;
				screenHeight = window.Height;
			}

			// Calculate dropdown dimensions
			int dropdownWidth = CalculateDropdownWidth();
			int effectiveMaxVisibleItems = _maxVisibleItems;
			int visibleItems = Math.Min(effectiveMaxVisibleItems, _items.Count);
			int hasScrollIndicator = (_items.Count > visibleItems) ? 1 : 0;
			int dropdownHeight = visibleItems + hasScrollIndicator;

			// Calculate header position from last layout bounds
			int headerX = _lastLayoutBounds.X + Margin.Left;
			int headerY = _lastLayoutBounds.Y + Margin.Top;

			// Calculate alignment offset (same logic as PaintDOM)
			int targetWidth = _lastLayoutBounds.Width - Margin.Left - Margin.Right;
			int alignOffset = 0;
			if (dropdownWidth < targetWidth)
			{
				switch (HorizontalAlignment)
				{
					case HorizontalAlignment.Center:
						alignOffset = (targetWidth - dropdownWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						alignOffset = targetWidth - dropdownWidth;
						break;
				}
			}

			// Use PortalPositioner for placement with auto-flip
			var request = new PortalPositionRequest(
				Anchor: new Rectangle(headerX + alignOffset, headerY, dropdownWidth, 1),
				ContentSize: new Size(dropdownWidth, dropdownHeight),
				ScreenBounds: new Rectangle(0, 0, screenWidth, screenHeight),
				Placement: PortalPlacement.BelowOrAbove
			);

			var result = PortalPositioner.Calculate(request);
			_opensUpward = (result.ActualPlacement == PortalPlacement.Above);
			_dropdownBounds = result.Bounds;
		}

		/// <summary>
		/// Calculates the optimal dropdown portal width based on all items.
		/// </summary>
		private int CalculateDropdownWidth()
		{
			int targetWidth = _lastLayoutBounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) targetWidth = 20;

			int dropdownWidth = calculatePortalWidth(targetWidth);

			return dropdownWidth;
		}

		/// <summary>
		/// Gets the portal bounds for positioning the dropdown overlay.
		/// </summary>
		internal Rectangle GetPortalBounds()
		{
			return _dropdownBounds;
		}

		/// <summary>
		/// Paints the dropdown list items (called by portal content).
		/// </summary>
		internal void PaintDropdownListInternal(CharacterBuffer buffer, LayoutRect clipRect)
		{
			List<DropdownItem> items;
			lock (_dropdownLock) { items = _items.ToList(); }
			if (items.Count == 0) return;

			// Get colors for dropdown list (uses dedicated dropdown theme colors)
			Color backgroundColor;
			Color foregroundColor;

			if (!_isEnabled)
			{
				backgroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor ?? Color.Grey;
				foregroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor ?? Color.DarkSlateGray1;
			}
			else
			{
				backgroundColor = Container?.GetConsoleWindowSystem?.Theme?.DropdownBackgroundColor ?? Color.Grey23;
				foregroundColor = Container?.GetConsoleWindowSystem?.Theme?.DropdownForegroundColor ?? Color.White;
			}

			int selectedIdx = CurrentSelectedIndex;
			int highlightedIdx = CurrentHighlightedIndex;
			int dropdownScroll = CurrentDropdownScrollOffset;

			// Use mouse hover index if active, otherwise keyboard highlight
			int effectiveHighlight = _mouseHoveredIndex >= 0 ? _mouseHoveredIndex : highlightedIdx;

			int dropdownWidth = _dropdownBounds.Width;
			int startX = _dropdownBounds.X;
			int paintY = _dropdownBounds.Y;

			int effectiveMaxVisibleItems =  _maxVisibleItems;
			int itemsToShow = Math.Min(effectiveMaxVisibleItems, items.Count - dropdownScroll);

			// Clamp to available height in bounds
			int availableHeight = _dropdownBounds.Height;
			bool hasScrollIndicator = (items.Count > effectiveMaxVisibleItems);
			int maxItemsInBounds = hasScrollIndicator ? availableHeight - 1 : availableHeight;
			itemsToShow = Math.Min(itemsToShow, maxItemsInBounds);

			for (int i = 0; i < itemsToShow; i++)
			{
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
				{
					int itemIndex = i + dropdownScroll;
					if (itemIndex >= items.Count) break;

					string itemText = _itemFormatter != null
						? _itemFormatter(items[itemIndex], itemIndex == selectedIdx, HasFocus)
						: items[itemIndex].Text;

					if (Parsing.MarkupParser.StripLength(itemText) > dropdownWidth - 4)
						itemText = Helpers.TextTruncationHelper.Truncate(itemText, dropdownWidth - 4);

					Color itemBg = (itemIndex == selectedIdx) ? HighlightBackgroundColor : backgroundColor;
					Color itemFg = (itemIndex == selectedIdx) ? HighlightForegroundColor : foregroundColor;

					// Override with hover highlight
					if (itemIndex == effectiveHighlight)
					{
						itemBg = HighlightBackgroundColor;
						itemFg = HighlightForegroundColor;
					}

					string selectionIndicator = itemIndex == highlightedIdx ? "● " : "  ";
					string itemContent = selectionIndicator + itemText;
					int visibleTextLength = 2 + Parsing.MarkupParser.StripLength(itemText);
					int paddingNeeded = Math.Max(0, dropdownWidth - visibleTextLength);
					if (paddingNeeded > 0)
						itemContent += new string(' ', paddingNeeded);

					var itemCells = Parsing.MarkupParser.Parse(itemContent, itemFg, itemBg);
					buffer.WriteCellsClipped(startX, paintY, itemCells, clipRect);
				}
				paintY++;
			}

			// Render scroll indicators if needed
			if (hasScrollIndicator && paintY < _dropdownBounds.Bottom)
			{
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
				{
					var scrollBuilder = new System.Text.StringBuilder();
					scrollBuilder.Append(dropdownScroll > 0 ? ControlDefaults.DropdownScrollUpArrow : " ");
					int scrollPadding = dropdownWidth - 2;
					if (scrollPadding > 0)
						scrollBuilder.Append(' ', scrollPadding);
					scrollBuilder.Append(dropdownScroll + itemsToShow < items.Count ? ControlDefaults.DropdownScrollDownArrow : " ");
					string scrollIndicator = scrollBuilder.ToString();

					var scrollCells = Parsing.MarkupParser.Parse(scrollIndicator, foregroundColor, backgroundColor);
					buffer.WriteCellsClipped(startX, paintY, scrollCells, clipRect);
				}
			}
		}

		#endregion

		// Helper method to traverse up the container hierarchy until finding a Window instance
		private Window? FindContainingWindow()
		{
			// Start with the immediate container
			IContainer? currentContainer = Container;

			// Maximum number of levels to prevent infinite loops in case of circular references
			const int MaxLevels = 10;
			int level = 0;

			// Continue traversing up until we find a Window or reach the top
			while (currentContainer != null && level < MaxLevels)
			{
				// If the current container is a Window, return it
				if (currentContainer is Window window)
				{
					return window;
				}

				// If the current container is an IWindowControl, move up to its container
				if (currentContainer is IWindowControl control)
				{
					currentContainer = control.Container;
				}
				else
				{
					if (currentContainer is ColumnContainer columnContainer)
					{
						currentContainer = columnContainer.HorizontalGridContent.Container;
					}
					else
					{
						break;
					}
				}

				level++;
			}

			// If we didn't find a Window in the hierarchy, return null
			return null;
		}

		private void RestoreContainerScrollOffset(Window containerWindow)
		{
			// Use reflection to set the private _scrollOffset field in the Window class
			// since there's no public method to set it directly
			var scrollOffsetField = typeof(Window).GetField("_scrollOffset",
				System.Reflection.BindingFlags.NonPublic |
				System.Reflection.BindingFlags.Instance);

			if (scrollOffsetField != null)
			{
				scrollOffsetField.SetValue(containerWindow, _containerScrollOffsetBeforeDrop);
				containerWindow.Invalidate(true);
			}
			else
			{
				// Fallback if reflection doesn't work - simulate key presses
				containerWindow.GoToTop();
				for (int i = 0; i < _containerScrollOffsetBeforeDrop; i++)
				{
					containerWindow.EventDispatcher?.ProcessInput(new ConsoleKeyInfo(
						'\0', ConsoleKey.DownArrow, false, false, false));
				}
			}
		}
	}
}

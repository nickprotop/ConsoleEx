// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	public partial class ListControl
	{
		// IMouseAwareControl properties

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <summary>
		/// Gets the index of the currently hovered item (mouse cursor). -1 if no item is hovered.
		/// </summary>
		public int HoveredIndex
		{
			get => _hoveredIndex;
		}

		/// <summary>
		/// Gets or sets whether mouse hover highlights items visually.
		/// Default: true.
		/// </summary>
		public bool HoverHighlightsItems
		{
			get => _hoverHighlightsItems;
			set => SetProperty(ref _hoverHighlightsItems, value);
		}

		/// <summary>
		/// Gets or sets the number of lines to scroll with mouse wheel.
		/// Default: 3.
		/// </summary>
		public int MouseWheelScrollSpeed
		{
			get => _mouseWheelScrollSpeed;
			set { _mouseWheelScrollSpeed = Math.Max(1, value); OnPropertyChanged(); }
		}

		/// <summary>
		/// Gets or sets whether a right-click selects the item under the mouse cursor
		/// before firing the <see cref="MouseRightClick"/> event.
		/// Default: false (preserves backward compatibility).
		/// </summary>
		public bool SelectOnRightClick
		{
			get => _selectOnRightClick;
			set { _selectOnRightClick = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Gets or sets whether double-click activates items.
		/// Default: true.
		/// </summary>
		public bool DoubleClickActivates
		{
			get => _doubleClickActivates;
			set { _doubleClickActivates = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Gets or sets the double-click threshold in milliseconds.
		/// Default: 500.
		/// </summary>
		public int DoubleClickThresholdMs
		{
			get => _doubleClickThresholdMs;
			set { _doubleClickThresholdMs = Math.Max(100, value); OnPropertyChanged(); }
		}

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Don't process if already handled
			if (args.Handled)
				return false;

			// Handle mouse leave - clear hover state
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				if (_hoveredIndex != -1)
				{
					_hoveredIndex = -1;
					ItemHovered?.Invoke(this, -1);
					Container?.Invalidate(true);
				}
				MouseLeave?.Invoke(this, args);
				return true;
			}

			// Handle scrollbar drag-in-progress (must be checked early)
			if (args.HasAnyFlag(MouseFlags.Button1Dragged, MouseFlags.Button1Pressed))
			{
				if (_isScrollbarDragging)
				{
					HandleScrollbarDrag(args);
					return true;
				}
			}

			// Handle scrollbar drag end
			if (args.HasFlag(MouseFlags.Button1Released) && _isScrollbarDragging)
			{
				_isScrollbarDragging = false;
				return true;
			}

			// Calculate which item the mouse is over
			// args.Position.Y is control-relative (includes margin), so subtract both margin and title
			int titleOffset = string.IsNullOrEmpty(_title) ? 0 : 1;
			int relativeY = args.Position.Y - Margin.Top - titleOffset;
			int hoveredIndex = -1;

			// Check if mouse is on the scrollbar column
			bool mouseOnScrollbar = IsClickOnScrollbar(args);

			// Get visible height to properly calculate item index
			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			int totalVisibleLines = CalculateTotalVisibleItemsHeight();
			if (!mouseOnScrollbar && relativeY >= 0 && relativeY < totalVisibleLines)
			{
				hoveredIndex = GetItemIndexAtRelativeY(relativeY);
			}

			// Update hover state (visual feedback only, doesn't change highlight/selection)
			if (_hoverHighlightsItems && hoveredIndex != _hoveredIndex)
			{
				_hoveredIndex = hoveredIndex;
				ItemHovered?.Invoke(this, hoveredIndex);
				Container?.Invalidate(true);
			}

			// Handle mouse wheel scrolling (no impact on selection/highlight)
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				if (_scrollOffset > 0)
				{
					_scrollOffset = Math.Max(0, _scrollOffset - _mouseWheelScrollSpeed);
					Container?.Invalidate(true);
					args.Handled = true;
					return true; // Consumed
				}
				else
				{
					return false; // Allow parent to handle
				}
			}
			else if (args.HasFlag(MouseFlags.WheeledDown))
			{
				int maxScroll = Math.Max(0, _items.Count - effectiveMaxVisibleItems);
				if (_scrollOffset < maxScroll)
				{
					_scrollOffset = Math.Min(maxScroll, _scrollOffset + _mouseWheelScrollSpeed);
					Container?.Invalidate(true);
					args.Handled = true;
					return true; // Consumed
				}
				else
				{
					return false; // Allow parent to handle
				}
			}

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				if (_selectOnRightClick && hoveredIndex >= 0 && hoveredIndex < _items.Count)
				{
					SelectedIndex = hoveredIndex;
					Container?.Invalidate(true);
				}
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Handle double-click event from driver (preferred method)
			if (!mouseOnScrollbar && args.HasFlag(MouseFlags.Button1DoubleClicked) && _doubleClickActivates)
			{
				int clickedIndex = GetItemIndexAtRelativeY(relativeY);
				if (clickedIndex >= 0)
				{
					// Reset tracking state since driver handled the gesture
					_lastClickTime = DateTime.MinValue;
					_lastClickIndex = -1;

					// Commit highlight to selection
					if (_selectedIndex != clickedIndex)
					{
						SelectedIndex = clickedIndex;
					}

					MouseDoubleClick?.Invoke(this, args);

					// Fire ItemActivated
					var item = _items[clickedIndex];
					if (item.IsEnabled)
					{
						ItemActivated?.Invoke(this, item);
					}

					Container?.Invalidate(true);
					args.Handled = true;
					return true;
				}
			}

			// Handle scrollbar thumb drag initiation (needs Button1Pressed for responsive dragging)
			if (mouseOnScrollbar && args.HasFlag(MouseFlags.Button1Pressed))
			{
				if (!HasFocus && CanFocusWithMouse)
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				HandleScrollbarThumbPress(args);
				args.Handled = true;
				return true;
			}

			// Handle scrollbar arrow/track clicks (Button1Clicked only to avoid double-firing)
			if (mouseOnScrollbar && args.HasFlag(MouseFlags.Button1Clicked))
			{
				if (!HasFocus && CanFocusWithMouse)
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				HandleScrollbarClick(args);
				args.Handled = true;
				return true;
			}

			// Handle mouse clicks - set focus, select item, detect double-click
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				// Set focus on click
				if (!HasFocus && CanFocusWithMouse)
				{
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				}

				{
					int clickedIndex = GetItemIndexAtRelativeY(relativeY);
					if (clickedIndex >= 0)
					{
						// Checkbox mode: clicking the [ ]/[x] prefix (first 5 columns) toggles without selecting
						if (_checkboxMode && _items[clickedIndex].IsEnabled)
						{
							int relativeX = args.Position.X - Margin.Left;
							if (relativeX >= 0 && relativeX < 5)
							{
								_items[clickedIndex].IsChecked = !_items[clickedIndex].IsChecked;
								CheckedItemsChanged?.Invoke(this, EventArgs.Empty);
								Container?.Invalidate(true);
								args.Handled = true;
								return true;
							}
						}

						// Detect double-click (ProcessMouseEvent is always called on UI thread)
						var now = DateTime.UtcNow;
						var timeSince = (now - _lastClickTime).TotalMilliseconds;
						bool isDoubleClick = _doubleClickActivates &&
										clickedIndex == _lastClickIndex &&
										timeSince <= _doubleClickThresholdMs;

						_lastClickTime = now;
						_lastClickIndex = clickedIndex;

						// Set selection directly
						SelectedIndex = clickedIndex;

						// Double click: Commit to selection and activate
						if (isDoubleClick)
						{
							// Commit highlight to selection
							if (_selectedIndex != clickedIndex)
							{
								SelectedIndex = clickedIndex;
							}

							MouseDoubleClick?.Invoke(this, args);

							// Fire ItemActivated (like Enter key)
							var item = _items[clickedIndex];
							if (item.IsEnabled)
							{
								ItemActivated?.Invoke(this, item);
							}
						}
						else
						{
							// Fire mouse click event
							MouseClick?.Invoke(this, args);
						}

						Container?.Invalidate(true);
					}
				}

				args.Handled = true;
				return true;
			}

			// Handle mouse movement
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
			}

			return false;
		}

		/// <summary>
		/// Maps a relative Y line position (within the item area) to an item index,
		/// accounting for multi-line items. Returns -1 if the position is out of bounds.
		/// </summary>
		private int GetItemIndexAtRelativeY(int relativeY)
		{
			if (relativeY < 0) return -1;

			int scrollOffset = CurrentScrollOffset;
			int linesSoFar = 0;

			lock (_itemsLock)
			{
				for (int i = scrollOffset; i < _items.Count; i++)
				{
					int itemHeight = _items[i].Lines.Count;
					if (relativeY < linesSoFar + itemHeight)
						return i;
					linesSoFar += itemHeight;
				}
			}

			return -1;
		}

		private int CalculateTotalVisibleItemsHeight()
		{
			int totalHeight = 0;
			int scrollOffset = CurrentScrollOffset;

			lock (_itemsLock)
			{
				int itemsToCount = Math.Min(_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1, _items.Count - scrollOffset);

				for (int i = 0; i < itemsToCount; i++)
				{
					int itemIndex = i + scrollOffset;
					if (itemIndex < _items.Count)
					{
						totalHeight += _items[itemIndex].Lines.Count;
					}
				}
			}

			return totalHeight;
		}

		#region Scrollbar Interaction

		private bool IsClickOnScrollbar(MouseEventArgs args)
		{
			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			if (!ShouldShowScrollbar(_items.Count, effectiveMaxVisibleItems))
				return false;

			int fullListWidth = ActualWidth - Margin.Left - Margin.Right;
			int scrollbarX = fullListWidth - 1; // last column of content area
			int relativeX = args.Position.X - Margin.Left;
			return relativeX == scrollbarX;
		}

		private (int scrollbarStartY, int scrollbarHeight) GetScrollbarLayout()
		{
			bool hasTitle = !string.IsNullOrEmpty(_title);
			int scrollbarStartY = Margin.Top + (hasTitle ? 1 : 0);
			int scrollbarHeight = ActualHeight - Margin.Top - Margin.Bottom - (hasTitle ? 1 : 0);
			return (scrollbarStartY, Math.Max(0, scrollbarHeight));
		}

		private void HandleScrollbarThumbPress(MouseEventArgs args)
		{
			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			var (scrollbarStartY, scrollbarHeight) = GetScrollbarLayout();
			if (scrollbarHeight <= 0) return;

			var (_, trackHeight, thumbY, thumbHeight) =
				ScrollbarHelper.GetVerticalGeometry(scrollbarHeight, _items.Count, effectiveMaxVisibleItems, _scrollOffset);

			int relY = args.Position.Y - scrollbarStartY;
			var zone = ScrollbarHelper.HitTest(relY, trackHeight, thumbY, thumbHeight);
			if (zone == ScrollbarHitZone.Thumb)
			{
				_isScrollbarDragging = true;
				_scrollbarDragStartY = args.Position.Y;
				_scrollbarDragStartOffset = _scrollOffset;
			}
		}

		private void HandleScrollbarClick(MouseEventArgs args)
		{
			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			var (scrollbarStartY, scrollbarHeight) = GetScrollbarLayout();
			if (scrollbarHeight <= 0) return;

			var (_, trackHeight, thumbY, thumbHeight) =
				ScrollbarHelper.GetVerticalGeometry(scrollbarHeight, _items.Count, effectiveMaxVisibleItems, _scrollOffset);

			int relY = args.Position.Y - scrollbarStartY;
			int maxOffset = Math.Max(0, _items.Count - effectiveMaxVisibleItems);

			var zone = ScrollbarHelper.HitTest(relY, trackHeight, thumbY, thumbHeight);
			switch (zone)
			{
				case ScrollbarHitZone.UpArrow:
					_scrollOffset = Math.Max(0, _scrollOffset - 1);
					break;
				case ScrollbarHitZone.DownArrow:
					_scrollOffset = Math.Min(maxOffset, _scrollOffset + 1);
					break;
				case ScrollbarHitZone.TrackAbove:
					_scrollOffset = Math.Max(0, _scrollOffset - effectiveMaxVisibleItems);
					break;
				case ScrollbarHitZone.TrackBelow:
					_scrollOffset = Math.Min(maxOffset, _scrollOffset + effectiveMaxVisibleItems);
					break;
			}

			Container?.Invalidate(true);
		}

		private void HandleScrollbarDrag(MouseEventArgs args)
		{
			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			var (_, scrollbarHeight) = GetScrollbarLayout();
			if (scrollbarHeight <= 0) return;

			var (_, _, _, thumbHeight) =
				ScrollbarHelper.GetVerticalGeometry(scrollbarHeight, _items.Count, effectiveMaxVisibleItems, _scrollOffset);

			int deltaY = args.Position.Y - _scrollbarDragStartY;
			_scrollOffset = ScrollbarHelper.CalculateDragOffset(
				deltaY, _scrollbarDragStartOffset,
				scrollbarHeight, thumbHeight,
				_items.Count, effectiveMaxVisibleItems);

			Container?.Invalidate(true);
		}

		#endregion
	}
}

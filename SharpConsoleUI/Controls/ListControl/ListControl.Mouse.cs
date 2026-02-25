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
			set
			{
				_hoverHighlightsItems = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the number of lines to scroll with mouse wheel.
		/// Default: 3.
		/// </summary>
		public int MouseWheelScrollSpeed
		{
			get => _mouseWheelScrollSpeed;
			set
			{
				_mouseWheelScrollSpeed = Math.Max(1, value);
			}
		}

		/// <summary>
		/// Gets or sets whether double-click activates items.
		/// Default: true.
		/// </summary>
		public bool DoubleClickActivates
		{
			get => _doubleClickActivates;
			set
			{
				_doubleClickActivates = value;
			}
		}

		/// <summary>
		/// Gets or sets the double-click threshold in milliseconds.
		/// Default: 500.
		/// </summary>
		public int DoubleClickThresholdMs
		{
			get => _doubleClickThresholdMs;
			set
			{
				_doubleClickThresholdMs = Math.Max(100, value);
			}
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

			// Calculate which item the mouse is over
			// args.Position.Y is control-relative (includes margin), so subtract both margin and title
			int titleOffset = string.IsNullOrEmpty(_title) ? 0 : 1;
			int relativeY = args.Position.Y - Margin.Top - titleOffset;
			int hoveredIndex = -1;

			// Get visible height to properly calculate item index
			int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
			int totalVisibleLines = CalculateTotalVisibleItemsHeight();
			if (relativeY >= 0 && relativeY < totalVisibleLines)
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
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Handle double-click event from driver (preferred method)
			if (args.HasFlag(MouseFlags.Button1DoubleClicked) && _doubleClickActivates)
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

			// Handle mouse clicks - set focus, select item, detect double-click
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				// Set focus on click
				if (!HasFocus && CanFocusWithMouse)
				{
					SetFocus(true, FocusReason.Mouse);
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

						// Detect double-click (thread-safe)
						bool isDoubleClick;
						lock (_clickLock)
						{
							var now = DateTime.UtcNow;
							var timeSince = (now - _lastClickTime).TotalMilliseconds;
							isDoubleClick = _doubleClickActivates &&
											clickedIndex == _lastClickIndex &&
											timeSince <= _doubleClickThresholdMs;

							_lastClickTime = now;
							_lastClickIndex = clickedIndex;
						}

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

			for (int i = scrollOffset; i < _items.Count; i++)
			{
				int itemHeight = _items[i].Lines.Count;
				if (relativeY < linesSoFar + itemHeight)
					return i;
				linesSoFar += itemHeight;
			}

			return -1;
		}

		private int CalculateTotalVisibleItemsHeight()
		{
			int totalHeight = 0;
			int scrollOffset = CurrentScrollOffset;
			int itemsToCount = Math.Min(_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1, _items.Count - scrollOffset);

			for (int i = 0; i < itemsToCount; i++)
			{
				int itemIndex = i + scrollOffset;
				if (itemIndex < _items.Count)
				{
					totalHeight += _items[itemIndex].Lines.Count;
				}
			}

			return totalHeight;
		}
	}
}

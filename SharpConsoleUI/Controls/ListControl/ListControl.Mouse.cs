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
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
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
		/// Gets or sets the number of lines scrolled per mouse wheel notch.
		/// Values below 1 are clamped to 1.
		/// Default: <see cref="ControlDefaults.DefaultScrollWheelLines"/>.
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
					Invalidate(Invalidation.Relayout);
				}
				MouseLeave?.Invoke(this, args);
				return true;
			}

			// Scrollbar gesture capture: once a press lands on the scrollbar, subsequent resent
			// press/drag events route to it without re-hit-testing, even if the pointer wanders off
			// the bar into the content column - this is what stops a thumb drag from leaking into
			// content selection. Content presses are captured too (region Content) so the capture is
			// always released on Up, but Content gestures fall through to the existing handling below
			// (this preserves the exact pre-existing content click/double-click/checkbox behavior).
			if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Dragged,
				MouseFlags.Button1Released, MouseFlags.Button1Clicked))
			{
				var route = _gesture.Route(args, HitTestRegion);
				if (route.Phase != GesturePhase.None && route.Region == ListGestureRegion.Scrollbar)
				{
					switch (route.Phase)
					{
						case GesturePhase.Down:
							if (!HasFocus && CanFocusWithMouse)
								this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
							_thumbDragging = false;
							HandleScrollbarThumbPress(args);
							if (!_thumbDragging)
								HandleScrollbarClick(args);
							args.Handled = true;
							return true;

						case GesturePhase.Move:
							if (_thumbDragging)
								HandleScrollbarDrag(args);
							args.Handled = true;
							return true;

						case GesturePhase.Up:
							_thumbDragging = false;
							args.Handled = true;
							return true;
					}
				}

				// A bare Button1Clicked with no prior captured press (some drivers/tests deliver a click
				// without a separate Button1Pressed) landing on the scrollbar: synthesize a full click by
				// hit-testing fresh and dispatching Down then Up, so arrow/track clicks still fire.
				if (route.Phase == GesturePhase.None && args.HasFlag(MouseFlags.Button1Clicked)
					&& HitTestRegion(args) == ListGestureRegion.Scrollbar)
				{
					if (!HasFocus && CanFocusWithMouse)
						this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
					_thumbDragging = false;
					HandleScrollbarThumbPress(args);
					if (!_thumbDragging)
						HandleScrollbarClick(args);
					_thumbDragging = false;
					args.Handled = true;
					return true;
				}
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
				Invalidate(Invalidation.Relayout);
			}

			// Handle mouse wheel scrolling (no impact on selection/highlight)
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				if (_scrollOffset > 0)
				{
					_scrollOffset = Math.Max(0, _scrollOffset - _mouseWheelScrollSpeed);
					Invalidate(Invalidation.Relayout);
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
					Invalidate(Invalidation.Relayout);
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
					Invalidate(Invalidation.Relayout);
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

					Invalidate(Invalidation.Relayout);
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
								Invalidate(Invalidation.Relayout);
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

						Invalidate(Invalidation.Relayout);
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

		/// <summary>
		/// Maps a fresh Button1 press position to the sub-region that should own the resulting gesture.
		/// Called ONLY on a fresh press by <see cref="MouseGestureCapture{TRegion}"/>; never re-invoked
		/// mid-gesture, which is what stops a resent-press-on-motion from re-hit-testing a drag that has
		/// wandered off the scrollbar column into the item area (or vice versa).
		/// </summary>
		private ListGestureRegion HitTestRegion(MouseEventArgs args) =>
			IsClickOnScrollbar(args) ? ListGestureRegion.Scrollbar : ListGestureRegion.Content;

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
				_thumbDragging = true;
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

			Invalidate(Invalidation.Relayout);
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

			Invalidate(Invalidation.Relayout);
		}

		#endregion
	}
}

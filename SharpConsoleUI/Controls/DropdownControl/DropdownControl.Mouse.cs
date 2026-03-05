// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;

namespace SharpConsoleUI.Controls
{
	public partial class DropdownControl
	{
		#region IMouseAwareControl Implementation

		/// <summary>
		/// Processes mouse events for the dropdown header.
		/// Note: When the dropdown list is open, it's rendered as a portal and
		/// receives events via ProcessPortalMouseEvent instead.
		/// </summary>
		/// <param name="args">Mouse event arguments with control-relative coordinates.</param>
		/// <returns>True if the event was handled.</returns>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled)
				return false;

			// Handle mouse leave
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				_isHeaderPressed = false;
				if (!_hasFocus)
				{
					_mouseHoveredIndex = -1;
				}
				MouseLeave?.Invoke(this, args);
				Container?.Invalidate(true);
				return true;
			}

			// Handle mouse enter
			if (args.HasFlag(MouseFlags.MouseEnter))
			{
				MouseEnter?.Invoke(this, args);
				return true;
			}

			// Validate click is within content area (not in margins)
			// Margins are non-interactive spacing around the control
			int contentHeight = (_lastLayoutBounds.Height > 0 ? _lastLayoutBounds.Height : 1);
			if (args.Position.Y < Margin.Top ||
			    args.Position.Y >= contentHeight - Margin.Bottom ||
			    args.Position.X < Margin.Left ||
			    args.Position.X >= (_lastLayoutBounds.Width - Margin.Right))
			{
				// Click is in margin area - not interactive
				return false;
			}

			// Check if click is on header row (accounts for top margin)
			// The header is painted at margin.Top, not at Y=0
			bool isOnHeader = args.Position.Y == Margin.Top;

			// Handle mouse move
			if (args.HasAnyFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return true;
			}

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Handle mouse down
			if (args.HasAnyFlag(MouseFlags.Button1Pressed))
			{
				if (isOnHeader)
				{
					_isHeaderPressed = true;

					// Capture focus on mouse down
					if (!_hasFocus)
					{
						SetFocus(true, FocusReason.Mouse);
					}

					Container?.Invalidate(true);
					return true;
				}

				// Click outside header area
				return false;
			}

			// Handle mouse up
			if (args.HasFlag(MouseFlags.Button1Released))
			{
				bool wasHeaderPressed = _isHeaderPressed;
				_isHeaderPressed = false;

				if (wasHeaderPressed && isOnHeader)
				{
					// Toggle dropdown
					IsDropdownOpen = !_isDropdownOpen;

					MouseClick?.Invoke(this, args);
					Container?.Invalidate(true);
					return true;
				}

				return true;
			}

			return false;
		}

		/// <summary>
		/// Processes mouse events from the dropdown portal (when dropdown list is open).
		/// Called by DropdownPortalContent when the portal receives mouse events.
		/// </summary>
		/// <param name="args">Mouse event arguments with portal-relative coordinates.</param>
		/// <returns>True if the event was handled.</returns>
		internal bool ProcessPortalMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled || !_isDropdownOpen)
				return false;

			// Portal coordinates are relative to portal bounds (no border, starts at 0,0)
			int contentY = args.Position.Y;
			int contentX = args.Position.X;

			// Calculate visible structure for hit testing
			int effectiveMaxVisibleItems =  _maxVisibleItems;
			bool hasScrollIndicator = (_items.Count > effectiveMaxVisibleItems);
			int visibleItemCount = Math.Min(effectiveMaxVisibleItems, _items.Count - _dropdownScrollOffset);
			int scrollIndicatorRow = hasScrollIndicator ? visibleItemCount : -1;

			// Check if click is on scroll indicator row
			bool isOnScrollIndicatorRow = hasScrollIndicator && contentY == scrollIndicatorRow;

			// Find which item is at this Y position (accounting for scroll offset)
			int hitItemIndex = -1;
			if (contentY >= 0 && !isOnScrollIndicatorRow)
			{
				int itemIndex = contentY + _dropdownScrollOffset;
				if (itemIndex >= 0 && itemIndex < _items.Count)
				{
					hitItemIndex = itemIndex;
				}
			}

			// Handle mouse wheel scrolling
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				if (_dropdownScrollOffset > 0)
				{
					SetDropdownScrollOffset(_dropdownScrollOffset - 1);
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
				if (_dropdownScrollOffset < maxScroll)
				{
					SetDropdownScrollOffset(_dropdownScrollOffset + 1);
					Container?.Invalidate(true);
					args.Handled = true;
					return true; // Consumed
				}
				else
				{
					return false; // Allow parent to handle
				}
			}

			// Handle mouse leave
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				if (!_hasFocus)
				{
					_mouseHoveredIndex = -1;
				}
				Container?.Invalidate(true);
				return true;
			}

			// Handle mouse move (hover)
			if (args.HasAnyFlag(MouseFlags.ReportMousePosition))
			{
				if (hitItemIndex != _mouseHoveredIndex)
				{
					_mouseHoveredIndex = hitItemIndex;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Handle scroll indicator clicks
			if (isOnScrollIndicatorRow)
			{
				// Only act on mouse release to avoid double-scroll
				if (args.HasFlag(MouseFlags.Button1Released))
				{
					int dropdownWidth = _dropdownBounds.Width;

					// Small click areas: ▲ at positions 0-2, ▼ at last 3 positions
					if (contentX <= 2 && _dropdownScrollOffset > 0)
					{
						// Click on ▲ area - scroll up
						SetDropdownScrollOffset(_dropdownScrollOffset - 1);
						Container?.Invalidate(true);
					}
					else if (contentX >= dropdownWidth - 3)
					{
						// Click on ▼ area - scroll down
						int maxScroll = Math.Max(0, _items.Count - effectiveMaxVisibleItems);
						if (_dropdownScrollOffset < maxScroll)
						{
							SetDropdownScrollOffset(_dropdownScrollOffset + 1);
							Container?.Invalidate(true);
						}
					}
				}
				return true; // Consume all events on scroll indicator row
			}

			// Handle mouse down on items
			if (args.HasAnyFlag(MouseFlags.Button1Pressed))
			{
				if (hitItemIndex >= 0)
				{
					_mouseHoveredIndex = hitItemIndex;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Handle mouse up - select item
			if (args.HasFlag(MouseFlags.Button1Released))
			{
				if (hitItemIndex >= 0 && hitItemIndex == _mouseHoveredIndex)
				{
					// Select the item and close dropdown
					SelectedIndex = hitItemIndex;
					IsDropdownOpen = false;
					_mouseHoveredIndex = -1;

					MouseClick?.Invoke(this, args);
					Container?.Invalidate(true);
				}
				return true;
			}

			return false;
		}

		#endregion
	}
}

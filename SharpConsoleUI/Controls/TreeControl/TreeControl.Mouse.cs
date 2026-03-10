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

namespace SharpConsoleUI.Controls
{
	public partial class TreeControl
	{
		// IMouseAwareControl properties

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		// IMouseAwareControl events

		/// <summary>
		/// Occurs when the mouse is clicked on the control.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <summary>
		/// Occurs when the mouse is double-clicked on the control.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <summary>
		/// Occurs when the control is right-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		#pragma warning disable CS0067 // Event never raised (interface requirement)
		/// <summary>
		/// Occurs when the mouse enters the control area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseEnter;
		#pragma warning restore CS0067

		/// <summary>
		/// Occurs when the mouse leaves the control area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <summary>
		/// Occurs when the mouse moves over the control.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <summary>
		/// Selects a node by index without adjusting the scroll offset.
		/// Used for mouse-click selection where the item is already visible on screen.
		/// Captures deferred selection event instead of firing directly.
		/// </summary>
		private void SelectNodeNoScroll(int nodeIndex)
		{
			if (_selectedIndex == nodeIndex) return;
			if (nodeIndex < 0 || nodeIndex >= _flattenedNodes.Count) return;
			_selectedIndex = nodeIndex;
			// Defer selection changed event
			var selectedNode = nodeIndex >= 0 && nodeIndex < _flattenedNodes.Count ? _flattenedNodes[nodeIndex] : null;
			_deferredSelectionChanged = new TreeNodeEventArgs(selectedNode);
		}

		private bool IsClickOnScrollbar(int mouseX, int contentWidth)
		{
			int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;
			if (!ShouldShowScrollbar(_flattenedNodes.Count, effectiveMaxVisibleItems))
				return false;
			int scrollbarX = contentWidth - 1;
			return mouseX - Margin.Left == scrollbarX;
		}

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			if (args.Handled)
				return false;

			// Handle mouse leave - clear hover state
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				if (_hoveredIndex != -1)
				{
					_hoveredIndex = -1;
					Container?.Invalidate(true);
				}
				MouseLeave?.Invoke(this, args);
				return true;
			}

			// Handle drag-in-progress (must be checked early, before lock)
			if (args.HasAnyFlag(MouseFlags.Button1Dragged, MouseFlags.Button1Pressed) && _isScrollbarDragging)
			{
				lock (_treeLock)
				{
					int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;
					int sbContentHeight = (ActualHeight > 0 ? ActualHeight : 20) - Margin.Top - Margin.Bottom;
					var (_, _, _, thumbHeight) = ScrollbarHelper.GetVerticalGeometry(
						sbContentHeight, _flattenedNodes.Count, effectiveMaxVisibleItems, _scrollOffset);
					int maxOffset = Math.Max(0, _flattenedNodes.Count - effectiveMaxVisibleItems);
					int newOffset = ScrollbarHelper.CalculateDragOffset(
						args.Position.Y - _scrollbarDragStartY, _scrollbarDragStartOffset,
						sbContentHeight, thumbHeight, _flattenedNodes.Count, effectiveMaxVisibleItems);
					_scrollOffset = Math.Clamp(newOffset, 0, maxOffset);
				}
				Container?.Invalidate(true);
				return true;
			}

			// Handle scrollbar drag end
			if (args.HasFlag(MouseFlags.Button1Released) && _isScrollbarDragging)
			{
				_isScrollbarDragging = false;
				return true;
			}

			// Reset deferred events
			_deferredSelectionChanged = null;
			_deferredExpandCollapse = null;
			_deferredNodeActivated = null;

			// Collect mouse events to fire outside the lock
			MouseEventArgs? fireMouseRightClick = null;
			MouseEventArgs? fireMouseDoubleClick = null;
			MouseEventArgs? fireMouseClick = null;
			MouseEventArgs? fireMouseMove = null;
			bool result = false;

			lock (_treeLock)
			{
				// Calculate which node the mouse is over
				int nodeIndex = GetNodeIndexAtPosition(args.Position.Y);

				// Check if mouse is on the scrollbar column — skip hover update if so
				int sbContentWidth = (ActualWidth > 0 ? ActualWidth : 40) - Margin.Left - Margin.Right;
				bool mouseOnScrollbar = IsClickOnScrollbar(args.Position.X, sbContentWidth);

				// Update hover state (only when mouse is over content, not scrollbar)
				if (!mouseOnScrollbar && nodeIndex != _hoveredIndex)
				{
					_hoveredIndex = nodeIndex;
					Container?.Invalidate(true);
				}
				else if (mouseOnScrollbar && _hoveredIndex != -1)
				{
					_hoveredIndex = -1;
					Container?.Invalidate(true);
				}

				// Handle mouse wheel scrolling
				if (args.HasFlag(MouseFlags.WheeledUp))
				{
					if (_scrollOffset > 0)
					{
						_scrollOffset = Math.Max(0, _scrollOffset - ControlDefaults.DefaultScrollWheelLines);
						Container?.Invalidate(true);
						args.Handled = true;
						return true;
					}
					else
					{
						return false; // Allow parent to handle
					}
				}
				else if (args.HasFlag(MouseFlags.WheeledDown))
				{
					int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;
					int maxScroll = Math.Max(0, _flattenedNodes.Count - effectiveMaxVisibleItems);
					if (_scrollOffset < maxScroll)
					{
						_scrollOffset = Math.Min(maxScroll, _scrollOffset + ControlDefaults.DefaultScrollWheelLines);
						Container?.Invalidate(true);
						args.Handled = true;
						return true;
					}
					else
					{
						return false; // Allow parent to handle
					}
				}

				// Handle right-click
				if (args.HasFlag(MouseFlags.Button3Clicked))
				{
					if (!mouseOnScrollbar && _selectOnRightClick && nodeIndex >= 0 && nodeIndex < _flattenedNodes.Count)
					{
						SelectNodeNoScroll(nodeIndex);
						Container?.Invalidate(true);
					}
					fireMouseRightClick = args;
					result = true;
				}
				// Handle double-click - toggle expand/collapse or activate leaf
				else if (!mouseOnScrollbar && args.HasFlag(MouseFlags.Button1DoubleClicked))
				{
					if (nodeIndex >= 0 && nodeIndex < _flattenedNodes.Count)
					{
						// Reset tracking state since driver handled the gesture
						lock (_clickLock)
						{
							_lastClickTime = DateTime.MinValue;
							_lastClickIndex = -1;
						}

						// Select without scrolling — item is already visible
						SelectNodeNoScroll(nodeIndex);

						var node = _flattenedNodes[nodeIndex];
						if (node.Children.Count > 0)
						{
							// Toggle expand/collapse on directories
							node.IsExpanded = !node.IsExpanded;
							_deferredExpandCollapse = new TreeNodeEventArgs(node);
							UpdateFlattenedNodes();
						}
						else
						{
							// Activate leaf node (open file)
							_deferredNodeActivated = new TreeNodeEventArgs(node);
						}

						fireMouseDoubleClick = args;
						Container?.Invalidate(true);
						args.Handled = true;
						result = true;
					}
				}
				// Handle mouse click - select node
				else if (args.HasFlag(MouseFlags.Button1Clicked) || args.HasFlag(MouseFlags.Button1Pressed))
				{
					// Set focus on click
					if (!HasFocus && CanFocusWithMouse)
					{
						SetFocus(true, FocusReason.Mouse);
					}

					// Check if clicking on scrollbar (reuse mouseOnScrollbar computed above)
					if (mouseOnScrollbar)
					{
						int sbContentHeight = (ActualHeight > 0 ? ActualHeight : 20) - Margin.Top - Margin.Bottom;
						int effectiveVis = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;
						var (_, trackHeight, thumbY, thumbHeight) = ScrollbarHelper.GetVerticalGeometry(
							sbContentHeight, _flattenedNodes.Count, effectiveVis, _scrollOffset);
						int relY = args.Position.Y - Margin.Top;
						int maxOffset = Math.Max(0, _flattenedNodes.Count - effectiveVis);
						var zone = ScrollbarHelper.HitTest(relY, trackHeight, thumbY, thumbHeight);
						switch (zone)
						{
							case ScrollbarHitZone.UpArrow:
								_scrollOffset = Math.Max(0, _scrollOffset - 1);
								break;
							case ScrollbarHitZone.DownArrow:
								_scrollOffset = Math.Min(maxOffset, _scrollOffset + 1);
								break;
							case ScrollbarHitZone.Thumb:
								_isScrollbarDragging = true;
								_scrollbarDragStartY = args.Position.Y;
								_scrollbarDragStartOffset = _scrollOffset;
								break;
							case ScrollbarHitZone.TrackAbove:
								_scrollOffset = Math.Max(0, _scrollOffset - effectiveVis);
								break;
							case ScrollbarHitZone.TrackBelow:
								_scrollOffset = Math.Min(maxOffset, _scrollOffset + effectiveVis);
								break;
						}
						Container?.Invalidate(true);
						args.Handled = true;
						result = true;
					}
					else if (nodeIndex >= 0 && nodeIndex < _flattenedNodes.Count)
					{
						// Check if click was on the expand/collapse indicator
						int indicatorStart = GetIndicatorStartColumn(nodeIndex);
						bool clickedIndicator = indicatorStart >= 0 &&
							args.Position.X >= indicatorStart &&
							args.Position.X < indicatorStart + 4; // "[-] " / "[+] " is 4 chars

						// Select without scrolling — item is already visible (user just clicked it)
						SelectNodeNoScroll(nodeIndex);

						if (clickedIndicator)
						{
							// Single click on indicator: toggle expand/collapse
							var node = _flattenedNodes[nodeIndex];
							node.IsExpanded = !node.IsExpanded;
							_deferredExpandCollapse = new TreeNodeEventArgs(node);
							UpdateFlattenedNodes();
							// Reset double-click tracking so next click isn't misdetected
							lock (_clickLock)
							{
								_lastClickTime = DateTime.MinValue;
								_lastClickIndex = -1;
							}
						}
						else
						{
							// Detect double-click on non-indicator area (thread-safe)
							bool isDoubleClick;
							lock (_clickLock)
							{
								var now = DateTime.UtcNow;
								var timeSince = (now - _lastClickTime).TotalMilliseconds;
								isDoubleClick = nodeIndex == _lastClickIndex &&
												timeSince <= ControlDefaults.DefaultDoubleClickThresholdMs;

								_lastClickTime = now;
								_lastClickIndex = nodeIndex;
							}

							if (isDoubleClick)
							{
								var node = _flattenedNodes[nodeIndex];
								if (node.Children.Count > 0)
								{
									// Double-click on directory: toggle expand/collapse
									node.IsExpanded = !node.IsExpanded;
									_deferredExpandCollapse = new TreeNodeEventArgs(node);
									UpdateFlattenedNodes();
								}
								else
								{
									// Double-click on leaf: activate (open file)
									_deferredNodeActivated = new TreeNodeEventArgs(node);
								}

								fireMouseDoubleClick = args;
							}
							else
							{
								fireMouseClick = args;
							}
						}

						Container?.Invalidate(true);
					}

					args.Handled = true;
					result = true;
				}
				// Handle mouse movement
				else if (args.HasFlag(MouseFlags.ReportMousePosition))
				{
					fireMouseMove = args;
				}
			}

			// Fire all deferred events outside the lock
			if (_deferredSelectionChanged != null)
				SelectedNodeChanged?.Invoke(this, _deferredSelectionChanged);
			if (_deferredExpandCollapse != null)
				NodeExpandCollapse?.Invoke(this, _deferredExpandCollapse);
			if (_deferredNodeActivated != null)
				NodeActivated?.Invoke(this, _deferredNodeActivated);

			// Fire mouse events outside the lock
			if (fireMouseRightClick != null)
				MouseRightClick?.Invoke(this, fireMouseRightClick);
			if (fireMouseDoubleClick != null)
				MouseDoubleClick?.Invoke(this, fireMouseDoubleClick);
			if (fireMouseClick != null)
				MouseClick?.Invoke(this, fireMouseClick);
			if (fireMouseMove != null)
				MouseMove?.Invoke(this, fireMouseMove);

			return result;
		}

		/// <summary>
		/// Converts a control-relative Y position to a flattened node index.
		/// </summary>
		/// <param name="mouseY">The Y position relative to the control.</param>
		/// <returns>The node index, or -1 if the position is outside the node area.</returns>
		private int GetNodeIndexAtPosition(int mouseY)
		{
			int relativeY = mouseY - Margin.Top;
			int effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? 10;

			if (relativeY < 0 || relativeY >= effectiveMaxVisibleItems)
				return -1;

			int index = _scrollOffset + relativeY;
			if (index >= _flattenedNodes.Count)
				return -1;

			return index;
		}
	}
}

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

public partial class MenuControl
{
    #region IMouseAwareControl Implementation

    /// <inheritdoc/>
    public bool WantsMouseEvents => _enabled;
    /// <inheritdoc/>
    public bool CanFocusWithMouse => _enabled;

    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseClick;
    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseEnter;
    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseLeave;
    #pragma warning disable CS0067  // Event never raised (interface requirement)
    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseDoubleClick;
    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseMove;
    #pragma warning restore CS0067

    /// <inheritdoc/>
    public bool ProcessMouseEvent(MouseEventArgs args)
    {
        if (!_enabled)
            return false;

        // Handle mouse leave event - clear hover if unfocused
        if (args.HasFlag(MouseFlags.MouseLeave))
        {
            if (!HasFocus)
            {
                _hoveredItem = null;
                Container?.Invalidate(true);
            }
            MouseLeave?.Invoke(this, args);
            return true;
        }

        // Handle mouse enter event
        if (args.HasFlag(MouseFlags.MouseEnter))
        {
            MouseEnter?.Invoke(this, args);
            return true;
        }

        // Mouse move - update hover state
        if (args.HasAnyFlag(MouseFlags.ReportMousePosition))
        {
            var hitItem = HitTest(args.Position.X, args.Position.Y);

            if (hitItem != _hoveredItem)
            {
                _hoveredItem = hitItem;

                // If any dropdown is open and we hover a different top-level item
                if (_openDropdowns.Count > 0 && hitItem != null && IsTopLevelItem(hitItem))
                {
                    // Switch dropdown immediately
                    _pendingSubmenuItem = null;
                    CloseAllMenus();
                    // Restore hover after CloseAllMenus cleared it (we're switching, not closing)
                    _hoveredItem = hitItem;
                    if (hitItem.HasChildren)
                        OpenDropdownInternal(hitItem);
                }
                // If hovering item with children in open dropdown, start hover delay
                else if (_openDropdowns.Count > 0 && hitItem?.HasChildren == true && !hitItem.IsOpen)
                {
                    _pendingSubmenuItem = hitItem;
                    _hoverStartTime = DateTime.Now;
                }
                else
                {
                    _pendingSubmenuItem = null;
                }

                if (hitItem != null)
                {
                    ItemHovered?.Invoke(this, hitItem);
                }
                Container?.Invalidate(true);
            }
            // Same item as before — check if pending submenu delay has elapsed
            else if (_pendingSubmenuItem != null && hitItem == _pendingSubmenuItem)
            {
                var elapsed = (DateTime.Now - _hoverStartTime).TotalMilliseconds;
                if (elapsed >= SubmenuHoverDelayMs)
                {
                    OpenSubmenu(_pendingSubmenuItem);
                    _pendingSubmenuItem = null;
                }
            }

            return true;
        }

        // Mouse down - track pressed state for visual feedback
        if (args.HasAnyFlag(MouseFlags.Button1Pressed))
        {
            var hitItem = HitTest(args.Position.X, args.Position.Y);

            if (hitItem == null)
            {
                // Clicked outside menu - close all
                CloseAllMenus();
                return false;
            }

            _pressedItem = hitItem;

            // Set focus on mouse down
            bool wasFocused = HasFocus;
            if (!HasFocus)
            {
                SetFocus(true, FocusReason.Mouse);
            }

            // If we just gained focus and clicked a top-level item with children, open it immediately
            // This allows single-click to open menus instead of requiring click-to-focus then click-to-open
            if (!wasFocused && IsTopLevelItem(hitItem) && hitItem.HasChildren && hitItem.IsEnabled && !hitItem.IsSeparator)
            {
                CloseAllMenus();
                OpenDropdownInternal(hitItem);
                _focusedItem = hitItem;
                _hoveredItem = null;
            }

            Container?.Invalidate(true);
            return true;
        }

        // Mouse up - execute action (only handle Released, not Clicked to avoid duplicate processing)
        if (args.HasFlag(MouseFlags.Button1Released))
        {
            var hitItem = HitTest(args.Position.X, args.Position.Y);
            var pressedItem = _pressedItem; // Save before clearing

            // Only process if we have a pressedItem - prevents duplicate processing
            if (pressedItem == null)
            {
                return true;
            }

            _pressedItem = null;

            if (hitItem == null)
            {
                CloseAllMenus();
                return false;
            }

            if (!hitItem.IsEnabled || hitItem.IsSeparator)
                return true;

            // Always update focus to clicked item and clear hover
            // (click "commits" the hover to a focused state)
            _focusedItem = hitItem;
            _hoveredItem = null;

            // Top-level item clicked
            if (IsTopLevelItem(hitItem))
            {
                // Check if we just opened this menu in the Button1Pressed handler
                // (happens when menu wasn't focused and we clicked it)
                bool justOpenedInMouseDown = (pressedItem == hitItem && hitItem.IsOpen && hitItem.HasChildren);

                if (justOpenedInMouseDown)
                {
                    // Skip - already opened in mouse down, don't toggle it closed
                }
                else if (hitItem.IsOpen)
                {
                    // Close if already open from a previous click
                    CloseAllMenus();
                }
                else
                {
                    // Open dropdown or execute action
                    CloseAllMenus();
                    if (hitItem.HasChildren)
                    {
                        OpenDropdownInternal(hitItem);
                    }
                    else
                    {
                        // Top-level item without children - execute action
                        ExecuteMenuItem(hitItem);
                    }
                }
            }
            // Submenu item with children
            else if (hitItem.HasChildren)
            {
                OpenSubmenu(hitItem);
            }
            // Leaf item - execute action
            else
            {
                ExecuteMenuItem(hitItem);
                CloseAllMenus();
            }

            MouseClick?.Invoke(this, args);
            Container?.Invalidate(true);
            return true;
        }

        return false;
    }

    #endregion

    #region Dropdown Mouse Handling

    /// <summary>
    /// Processes mouse events for a dropdown portal.
    /// Called by MenuPortalContent when the portal receives mouse events.
    /// </summary>
    internal bool ProcessDropdownMouseEvent(MenuDropdown dropdown, MouseEventArgs args)
    {
        if (!_enabled)
            return false;

        // Handle mouse wheel scrolling
        if (args.HasFlag(MouseFlags.WheeledUp))
        {
            dropdown.ScrollUp();
            Container?.Invalidate(true);
            return true;
        }

        if (args.HasFlag(MouseFlags.WheeledDown))
        {
            dropdown.ScrollDown();
            Container?.Invalidate(true);
            return true;
        }

        // Convert portal-relative coordinates to item index
        // Portal bounds include border (1 char each side), so content starts at (1, 1)
        int contentX = args.Position.X - 1;
        int contentY = args.Position.Y - 1;

        // Find which item is at this Y position (accounting for scroll offset)
        MenuItem? hitItem = null;
        if (contentY >= 0 && contentY < dropdown.VisibleItems.Count)
        {
            int itemIndex = contentY + dropdown.ScrollOffset;
            if (itemIndex >= 0 && itemIndex < dropdown.VisibleItems.Count)
            {
                hitItem = dropdown.VisibleItems[itemIndex];
            }
        }

        // Handle mouse leave
        if (args.HasFlag(MouseFlags.MouseLeave))
        {
            if (!HasFocus)
            {
                _hoveredItem = null;
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

        // Handle mouse move (hover)
        if (args.HasAnyFlag(MouseFlags.ReportMousePosition))
        {
            if (hitItem != _hoveredItem)
            {
                _hoveredItem = hitItem;

                if (hitItem != null && hitItem.HasChildren && !hitItem.IsOpen)
                {
                    // Item with children - open its submenu (closes sibling submenus automatically)
                    OpenSubmenu(hitItem);
                }
                else if (hitItem != null && !hitItem.HasChildren)
                {
                    // Leaf item - close any sibling submenus that are deeper than this dropdown
                    CloseSiblingSubmenus(dropdown);
                }

                if (hitItem != null)
                {
                    ItemHovered?.Invoke(this, hitItem);
                }
                Container?.Invalidate(true);
            }
            return true;
        }

        // Handle mouse down
        if (args.HasAnyFlag(MouseFlags.Button1Pressed))
        {
            if (hitItem == null || !hitItem.IsEnabled || hitItem.IsSeparator)
            {
                return true;
            }

            _pressedItem = hitItem;
            Container?.Invalidate(true);
            return true;
        }

        // Handle mouse up - execute action
        if (args.HasFlag(MouseFlags.Button1Released))
        {
            var pressedItem = _pressedItem;
            _pressedItem = null;

            if (pressedItem == null || hitItem == null || hitItem != pressedItem)
            {
                Container?.Invalidate(true);
                return true;
            }

            if (!hitItem.IsEnabled || hitItem.IsSeparator)
                return true;

            // Update focus and clear hover
            _focusedItem = hitItem;
            _hoveredItem = null;

            // Item with children - open submenu
            if (hitItem.HasChildren)
            {
                OpenSubmenu(hitItem);
            }
            // Leaf item - execute action
            else
            {
                ExecuteMenuItem(hitItem);
                CloseAllMenus();
            }

            MouseClick?.Invoke(this, args);
            Container?.Invalidate(true);
            return true;
        }

        return false;
    }

    #endregion

    #region Hit Testing

    private MenuItem? HitTest(int controlRelativeX, int controlRelativeY)
    {
        // Convert control-relative coordinates to bounds-relative coordinates
        int boundsX = controlRelativeX + _lastBounds.X;
        int boundsY = controlRelativeY + _lastBounds.Y;

        // Check top-level items
        foreach (var item in _items)
        {
            if (item.Bounds.Contains(boundsX, boundsY))
            {
                return item;
            }
        }

        // Check open dropdowns in reverse order (topmost first)
        for (int i = _openDropdowns.Count - 1; i >= 0; i--)
        {
            var dropdown = _openDropdowns[i];

            // Check if click is in dropdown bounds
            if (dropdown.Contains(boundsX, boundsY))
            {
                // Check each item in dropdown
                foreach (var item in dropdown.VisibleItems)
                {
                    if (item.Bounds.Contains(boundsX, boundsY))
                        return item;
                }

                // Click inside dropdown but not on item (border/padding)
                return null;
            }
        }

        return null;
    }

    private bool IsTopLevelItem(MenuItem item)
    {
        return _items.Contains(item);
    }

    #endregion
}

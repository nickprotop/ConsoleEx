using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;

using SharpConsoleUI.Extensions;
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

    /// <summary>
    /// Occurs when the control is right-clicked with the mouse.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseRightClick;
    /// <inheritdoc/>
    public event EventHandler<MouseEventArgs>? MouseMove;
    #pragma warning restore CS0067

    /// <inheritdoc/>
    public bool ProcessMouseEvent(MouseEventArgs args)
    {
        if (!_enabled)
            return false;


        // Handle right-click
        if (args.HasFlag(MouseFlags.Button3Clicked))
        {
            CloseAllMenus();
            MouseRightClick?.Invoke(this, args);
            return true;
        }

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

                // Cancel pending switch if mouse moved to a different item
                if (_pendingSwitchItem != null && hitItem != _pendingSwitchItem)
                {
                    _pendingSwitchItem = null;
                }

                // If any dropdown is open and we hover a different top-level item
                if (_openDropdowns.Count > 0 && hitItem != null && IsTopLevelItem(hitItem))
                {
                    bool hasOpenSubmenu = _openDropdowns.Count > 1; // submenu is open (not just a dropdown)
                    if (hasOpenSubmenu && _pendingSwitchItem != hitItem)
                    {
                        // Start delay — user might be moving toward the open submenu
                        _pendingSwitchItem = hitItem;
                        _switchStartTime = DateTime.Now;
                    }
                    else if (!hasOpenSubmenu)
                    {
                        // No submenu open — switch immediately (just top-level dropdown switching)
                        _pendingSubmenuItem = null;
                        _pendingSwitchItem = null;
                        CloseAllMenus();
                        _hoveredItem = hitItem;
                        if (hitItem.HasChildren)
                            OpenDropdownInternal(hitItem);
                    }
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
            else if (_pendingSwitchItem != null && hitItem == _pendingSwitchItem)
            {
                var elapsed = (DateTime.Now - _switchStartTime).TotalMilliseconds;
                if (elapsed >= Configuration.ControlDefaults.MenuAimDelayMs)
                {
                    // Delay elapsed — perform the switch
                    _pendingSubmenuItem = null;
                    _pendingSwitchItem = null;
                    CloseAllMenus();
                    _hoveredItem = hitItem;
                    if (hitItem.HasChildren)
                        OpenDropdownInternal(hitItem);
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
                this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
            }

            // If we just gained focus and clicked a top-level item with children, open it immediately
            // This allows single-click to open menus instead of requiring click-to-focus then click-to-open
            // Skip if this item's dropdown is already open (prevents re-open on double-click)
            if (IsTopLevelItem(hitItem) && hitItem.HasChildren && hitItem.IsEnabled && !hitItem.IsSeparator)
            {
                bool alreadyOpen = _openDropdowns.Any(d => d.ParentItem == hitItem);
                if (!wasFocused && !alreadyOpen)
                {
                    CloseAllMenus();
                    OpenDropdownInternal(hitItem);
                    _focusedItem = hitItem;
                    _hoveredItem = null;
                }
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
        var (visStart, visEnd) = dropdown.GetVisibleRange();
        int visibleCount = visEnd - visStart;
        if (contentY >= 0 && contentY < visibleCount)
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

                // Check if a deeper submenu is open (sibling of this dropdown's items)
                bool hasDeeperSubmenu = _openDropdowns.Count > 0 &&
                    _openDropdowns[_openDropdowns.Count - 1] != dropdown;

                if (hitItem != null && hitItem.HasChildren && !hitItem.IsOpen)
                {
                    if (hasDeeperSubmenu)
                    {
                        // Delay before switching submenu — user might be moving toward the open one
                        _pendingSubmenuItem = hitItem;
                        _hoverStartTime = DateTime.Now;
                    }
                    else
                    {
                        // No deeper submenu open — open immediately
                        OpenSubmenu(hitItem);
                    }
                }
                else if (hitItem != null && !hitItem.HasChildren)
                {
                    if (hasDeeperSubmenu)
                    {
                        // Delay before closing submenu — user might be moving toward it
                        _pendingSubmenuItem = null; // Not opening a submenu, just delaying close
                        _pendingCloseDropdown = dropdown;
                        _hoverStartTime = DateTime.Now;
                    }
                    else
                    {
                        // No deeper submenu open — close sibling submenus immediately
                        CloseSiblingSubmenus(dropdown);
                    }
                }
                else
                {
                    _pendingSubmenuItem = null;
                    _pendingCloseDropdown = null;
                }

                if (hitItem != null)
                {
                    ItemHovered?.Invoke(this, hitItem);
                }
                Container?.Invalidate(true);
            }
            // Same item — check pending delays
            else if (_pendingSubmenuItem != null && hitItem == _pendingSubmenuItem)
            {
                var elapsed = (DateTime.Now - _hoverStartTime).TotalMilliseconds;
                if (elapsed >= Configuration.ControlDefaults.MenuAimDelayMs)
                {
                    OpenSubmenu(_pendingSubmenuItem);
                    _pendingSubmenuItem = null;
                    _pendingCloseDropdown = null;
                }
            }
            else if (_pendingCloseDropdown != null)
            {
                var elapsed = (DateTime.Now - _hoverStartTime).TotalMilliseconds;
                if (elapsed >= Configuration.ControlDefaults.MenuAimDelayMs)
                {
                    CloseSiblingSubmenus(_pendingCloseDropdown);
                    _pendingCloseDropdown = null;
                }
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

    #region Window Background Click Handler

    private void OnWindowUnhandledMouseClick(object? sender, MouseEventArgs args)
    {
        if (_openDropdowns.Count > 0)
            CloseAllMenus();
    }

    private void OnWindowDeactivated(object? sender, EventArgs args)
    {
        if (_openDropdowns.Count > 0)
            CloseAllMenus();
    }

    #endregion

    #region Hit Testing

    private MenuItem? HitTest(int controlRelativeX, int controlRelativeY)
    {
        // Convert control-relative coordinates to bounds-relative coordinates
        int boundsX = controlRelativeX + _lastBounds.X;
        int boundsY = controlRelativeY + _lastBounds.Y;

        // Check top-level items
        List<MenuItem> snapshot;
        lock (_menuLock) { snapshot = _items.ToList(); }
        foreach (var item in snapshot)
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
                // Check only items in visible range (scrolled dropdowns have stale bounds for off-screen items)
                var (start, end) = dropdown.GetVisibleRange();
                for (int j = start; j < end; j++)
                {
                    var item = dropdown.VisibleItems[j];
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
        lock (_menuLock) { return _items.Contains(item); }
    }

    #endregion
}

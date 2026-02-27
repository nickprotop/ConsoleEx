namespace SharpConsoleUI.Controls;

public partial class MenuControl
{
    #region IInteractiveControl Implementation

    /// <inheritdoc/>
    public bool ProcessKey(ConsoleKeyInfo key)
    {
        if (!_enabled || !_hasFocus)
            return false;

        bool isInSubmenu = _openDropdowns.Count > 1;
        bool hasOpenDropdown = _openDropdowns.Count > 0;

        if (_orientation == MenuOrientation.Horizontal)
        {
            return ProcessKeyHorizontal(key, isInSubmenu, hasOpenDropdown);
        }
        else
        {
            return ProcessKeyVertical(key, isInSubmenu, hasOpenDropdown);
        }
    }

    #endregion

    #region Keyboard - Horizontal Menu

    private bool ProcessKeyHorizontal(ConsoleKeyInfo key, bool isInSubmenu, bool hasOpenDropdown)
    {
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                if (hasOpenDropdown)
                {
                    if (isInSubmenu)
                    {
                        // Close current submenu, return to parent
                        CloseLastOpenMenu();
                        UpdateFocusToLastDropdown();
                    }
                    else
                    {
                        // Move to previous top-level item (works from any dropdown item)
                        MoveToPreviousTopLevel();
                    }
                    return true;
                }
                else
                {
                    // Navigate top-level items without opening
                    MoveToPreviousTopLevel();
                    return true;
                }

            case ConsoleKey.RightArrow:
                if (hasOpenDropdown)
                {
                    // Check if focused item already has its dropdown open
                    bool isParentOfOpenDropdown = _openDropdowns.Count > 0 &&
                                                   _openDropdowns[0].ParentItem == _focusedItem;

                    if (_focusedItem?.HasChildren == true && !isParentOfOpenDropdown)
                    {
                        // Open submenu (only if not already the parent of current dropdown)
                        OpenSubmenu(_focusedItem);
                    }
                    else if (!isInSubmenu)
                    {
                        // Move to next top-level item (from dropdown item or top-level item)
                        MoveToNextTopLevel();
                    }
                    return true;
                }
                else
                {
                    // Navigate top-level items without opening
                    MoveToNextTopLevel();
                    return true;
                }

            case ConsoleKey.DownArrow:
                if (hasOpenDropdown)
                {
                    // Navigate within dropdown
                    MoveToNextItem();
                }
                else
                {
                    // Open dropdown of focused top-level item
                    if (_focusedItem != null)
                    {
                        if (_focusedItem.HasChildren)
                            OpenDropdownInternal(_focusedItem);
                    }
                }
                return true;

            case ConsoleKey.UpArrow:
                if (hasOpenDropdown)
                {
                    MoveToPreviousItem();
                }
                return true;

            case ConsoleKey.Enter:
                return HandleEnterKey();

            case ConsoleKey.Escape:
                if (hasOpenDropdown)
                {
                    if (isInSubmenu)
                        CloseLastOpenMenu();
                    else
                        CloseAllMenus();
                    return true;
                }
                else
                {
                    // Unfocus menu
                    SetFocus(false, FocusReason.Keyboard);
                    return true;
                }

            case ConsoleKey.Home:
                MoveToFirstItem();
                return true;

            case ConsoleKey.End:
                MoveToLastItem();
                return true;

            default:
                // Letter key navigation
                if (!char.IsControl(key.KeyChar))
                {
                    return JumpToItemStartingWith(key.KeyChar);
                }
                break;
        }

        return false;
    }

    #endregion

    #region Keyboard - Vertical Menu

    private bool ProcessKeyVertical(ConsoleKeyInfo key, bool isInSubmenu, bool hasOpenDropdown)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (hasOpenDropdown)
                {
                    MoveToPreviousItem();
                }
                else
                {
                    MoveToPreviousTopLevel();
                }
                return true;

            case ConsoleKey.DownArrow:
                if (hasOpenDropdown)
                {
                    MoveToNextItem();
                }
                else
                {
                    MoveToNextTopLevel();
                }
                return true;

            case ConsoleKey.RightArrow:
                if (_focusedItem?.HasChildren == true)
                {
                    OpenSubmenu(_focusedItem);
                    return true;
                }
                break;

            case ConsoleKey.LeftArrow:
                if (isInSubmenu)
                {
                    CloseLastOpenMenu();
                    UpdateFocusToLastDropdown();
                    return true;
                }
                break;

            case ConsoleKey.Enter:
                return HandleEnterKey();

            case ConsoleKey.Escape:
                if (hasOpenDropdown)
                {
                    if (isInSubmenu)
                        CloseLastOpenMenu();
                    else
                        CloseAllMenus();
                    return true;
                }
                else
                {
                    SetFocus(false, FocusReason.Keyboard);
                    return true;
                }

            case ConsoleKey.Home:
                MoveToFirstItem();
                return true;

            case ConsoleKey.End:
                MoveToLastItem();
                return true;

            default:
                // Letter key navigation
                if (!char.IsControl(key.KeyChar))
                {
                    return JumpToItemStartingWith(key.KeyChar);
                }
                break;
        }

        return false;
    }

    #endregion

    #region Keyboard - Navigation

    private bool HandleEnterKey()
    {
        if (_focusedItem == null || !_focusedItem.IsEnabled)
            return false;

        if (_focusedItem.HasChildren)
        {
            // Open submenu
            OpenSubmenu(_focusedItem);
        }
        else
        {
            // Execute action
            ExecuteMenuItem(_focusedItem);
            CloseAllMenus();
        }

        return true;
    }

    private void MoveToPreviousTopLevel()
    {
        List<MenuItem> snapshot;
        lock (_menuLock) { snapshot = _items.ToList(); }
        if (snapshot.Count == 0)
            return;

        // Find starting point: if focused item is top-level, use it; otherwise use current dropdown parent
        int currentIndex;
        if (_focusedItem != null && snapshot.Contains(_focusedItem))
        {
            currentIndex = snapshot.IndexOf(_focusedItem);
        }
        else if (_openDropdowns.Count > 0)
        {
            var parentItem = _openDropdowns[0].ParentItem;
            if (parentItem != null)
            {
                currentIndex = snapshot.IndexOf(parentItem);
            }
            else
            {
                currentIndex = 0;
            }
        }
        else
        {
            currentIndex = 0;
        }

        int startIndex = currentIndex;

        do
        {
            currentIndex--;
            if (currentIndex < 0)
                currentIndex = snapshot.Count - 1;

            var item = snapshot[currentIndex];
            if (!item.IsSeparator && item.IsEnabled)
            {
                _focusedItem = item;

                // If a dropdown was open, switch to the new one
                if (_openDropdowns.Count > 0 && _openDropdowns[0].ParentItem != null)
                {
                    CloseAllMenus();
                    if (_focusedItem.HasChildren)
                        OpenDropdownInternal(_focusedItem);
                }

                Container?.Invalidate(true);
                return;
            }
        }
        while (currentIndex != startIndex);
    }

    private void MoveToNextTopLevel()
    {
        List<MenuItem> snapshot;
        lock (_menuLock) { snapshot = _items.ToList(); }
        if (snapshot.Count == 0)
            return;

        // Find starting point: if focused item is top-level, use it; otherwise use current dropdown parent
        int currentIndex;
        if (_focusedItem != null && snapshot.Contains(_focusedItem))
        {
            currentIndex = snapshot.IndexOf(_focusedItem);
        }
        else if (_openDropdowns.Count > 0)
        {
            var parentItem = _openDropdowns[0].ParentItem;
            if (parentItem != null)
            {
                currentIndex = snapshot.IndexOf(parentItem);
            }
            else
            {
                currentIndex = 0;
            }
        }
        else
        {
            currentIndex = -1;
        }

        int startIndex = currentIndex;

        do
        {
            currentIndex++;
            if (currentIndex >= snapshot.Count)
                currentIndex = 0;

            var item = snapshot[currentIndex];
            if (!item.IsSeparator && item.IsEnabled)
            {
                _focusedItem = item;

                // If a dropdown was open, switch to the new one
                if (_openDropdowns.Count > 0 && _openDropdowns[0].ParentItem != null)
                {
                    CloseAllMenus();
                    if (_focusedItem.HasChildren)
                        OpenDropdownInternal(_focusedItem);
                }

                Container?.Invalidate(true);
                return;
            }
        }
        while (currentIndex != startIndex);
    }

    private void MoveToNextItem()
    {
        if (_openDropdowns.Count == 0)
            return;

        // Clear hover state when using keyboard navigation
        _hoveredItem = null;

        var currentDropdown = _openDropdowns[_openDropdowns.Count - 1];
        var items = currentDropdown.VisibleItems;

        int currentIndex = _focusedItem != null ? items.IndexOf(_focusedItem) : -1;
        int startIndex = currentIndex;

        do
        {
            currentIndex++;
            if (currentIndex >= items.Count)
                currentIndex = 0;

            var item = items[currentIndex];
            if (!item.IsSeparator && item.IsEnabled)
            {
                _focusedItem = item;
                currentDropdown.EnsureItemVisible(item);
                Container?.Invalidate(true);
                return;
            }
        }
        while (currentIndex != startIndex);
    }

    private void MoveToPreviousItem()
    {
        if (_openDropdowns.Count == 0)
            return;

        // Clear hover state when using keyboard navigation
        _hoveredItem = null;

        var currentDropdown = _openDropdowns[_openDropdowns.Count - 1];
        var items = currentDropdown.VisibleItems;

        int currentIndex = _focusedItem != null ? items.IndexOf(_focusedItem) : 0;
        int startIndex = currentIndex;

        do
        {
            currentIndex--;
            if (currentIndex < 0)
                currentIndex = items.Count - 1;

            var item = items[currentIndex];
            if (!item.IsSeparator && item.IsEnabled)
            {
                _focusedItem = item;
                currentDropdown.EnsureItemVisible(item);
                Container?.Invalidate(true);
                return;
            }
        }
        while (currentIndex != startIndex);
    }

    private void MoveToFirstItem()
    {
        List<MenuItem> items;

        if (_openDropdowns.Count > 0)
        {
            var currentDropdown = _openDropdowns[_openDropdowns.Count - 1];
            items = currentDropdown.VisibleItems;
        }
        else
        {
            lock (_menuLock) { items = _items.ToList(); }
        }

        var firstItem = items.FirstOrDefault(i => !i.IsSeparator && i.IsEnabled);
        if (firstItem != null)
        {
            _focusedItem = firstItem;

            if (_openDropdowns.Count > 0)
            {
                _openDropdowns[_openDropdowns.Count - 1].EnsureItemVisible(firstItem);
            }

            Container?.Invalidate(true);
        }
    }

    private void MoveToLastItem()
    {
        List<MenuItem> items;

        if (_openDropdowns.Count > 0)
        {
            var currentDropdown = _openDropdowns[_openDropdowns.Count - 1];
            items = currentDropdown.VisibleItems;
        }
        else
        {
            lock (_menuLock) { items = _items.ToList(); }
        }

        var lastItem = items.LastOrDefault(i => !i.IsSeparator && i.IsEnabled);
        if (lastItem != null)
        {
            _focusedItem = lastItem;

            if (_openDropdowns.Count > 0)
            {
                _openDropdowns[_openDropdowns.Count - 1].EnsureItemVisible(lastItem);
            }

            Container?.Invalidate(true);
        }
    }

    private bool JumpToItemStartingWith(char letter)
    {
        List<MenuItem> items;

        if (_openDropdowns.Count > 0)
        {
            var currentDropdown = _openDropdowns[_openDropdowns.Count - 1];
            items = currentDropdown.VisibleItems;
        }
        else
        {
            lock (_menuLock) { items = _items.ToList(); }
        }

        // Find first item starting with this letter (case-insensitive)
        var targetItem = items.FirstOrDefault(i =>
            !i.IsSeparator &&
            i.IsEnabled &&
            !string.IsNullOrEmpty(i.Text) &&
            char.ToLowerInvariant(i.Text[0]) == char.ToLowerInvariant(letter));

        if (targetItem != null)
        {
            _focusedItem = targetItem;

            if (_openDropdowns.Count > 0)
            {
                _openDropdowns[_openDropdowns.Count - 1].EnsureItemVisible(targetItem);
            }

            Container?.Invalidate(true);
            return true;
        }

        return false;
    }

    private void UpdateFocusToLastDropdown()
    {
        if (_openDropdowns.Count > 0)
        {
            var lastDropdown = _openDropdowns[_openDropdowns.Count - 1];
            _focusedItem = lastDropdown.VisibleItems.FirstOrDefault(i => !i.IsSeparator && i.IsEnabled)
                         ?? lastDropdown.ParentItem;
        }
        else
        {
            lock (_menuLock)
            {
                if (_items.Count > 0)
                    _focusedItem = _items.FirstOrDefault(i => !i.IsSeparator && i.IsEnabled);
            }
        }

        Container?.Invalidate(true);
    }

    #endregion
}

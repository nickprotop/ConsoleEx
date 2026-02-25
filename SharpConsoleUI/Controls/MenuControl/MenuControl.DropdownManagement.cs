using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using System.Drawing;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Controls;

public partial class MenuControl
{
    #region Dropdown Lifecycle

    private void OpenDropdownInternal(MenuItem item)
    {
        if (!item.HasChildren)
            return;

        // Check if already open - prevent duplicate portals
        if (item.IsOpen && _openDropdowns.Any(d => d.ParentItem == item))
        {
            return;
        }

        item.IsOpen = true;

        var dropdown = new MenuDropdown
        {
            ParentItem = item,
            VisibleItems = item.Children.ToList(),
            MaxVisibleItems = MaxDropdownHeight
        };

        // Calculate dropdown bounds
        dropdown.Bounds = CalculateDropdownBounds(item);

        _openDropdowns.Add(dropdown);

        // Subscribe to dismiss events when first dropdown opens
        if (_openDropdowns.Count == 1)
        {
            var parentWindow = this.GetParentWindow();
            if (parentWindow != null)
            {
                parentWindow.UnhandledMouseClick += OnWindowUnhandledMouseClick;
                parentWindow.Deactivated += OnWindowDeactivated;
            }
        }

        // Create portal for dropdown overlay
        var portalContent = new MenuPortalContent(this, dropdown);
        var window = this.GetParentWindow();
        if (window != null)
        {
            var portalNode = window.CreatePortal(this, portalContent);
            if (portalNode != null)
            {
                _dropdownPortals[dropdown] = portalNode;
            }
        }

        Container?.Invalidate(true);
    }

    private void OpenSubmenu(MenuItem item)
    {
        if (!item.HasChildren)
            return;

        // Close any existing submenu at this level or deeper
        var window = this.GetParentWindow();
        while (_openDropdowns.Count > 0)
        {
            var last = _openDropdowns[_openDropdowns.Count - 1];
            if (last.ParentItem != null && last.ParentItem.GetDepth() >= item.GetDepth())
            {
                last.ParentItem.IsOpen = false;

                // Remove portal before removing dropdown from list
                if (_dropdownPortals.TryGetValue(last, out var portalNode) && window != null)
                {
                    window.RemovePortal(this, portalNode);
                    _dropdownPortals.Remove(last);
                }

                _openDropdowns.RemoveAt(_openDropdowns.Count - 1);
            }
            else
            {
                break;
            }
        }

        OpenDropdownInternal(item);

        // Focus first item in new submenu
        var firstItem = item.Children.FirstOrDefault(i => !i.IsSeparator && i.IsEnabled);
        if (firstItem != null)
        {
            _focusedItem = firstItem;
        }

        Container?.Invalidate(true);
    }

    private void CloseLastOpenMenu()
    {
        if (_openDropdowns.Count > 0)
        {
            var last = _openDropdowns[_openDropdowns.Count - 1];
            if (last.ParentItem != null)
                last.ParentItem.IsOpen = false;

            // Remove portal if it exists
            if (_dropdownPortals.TryGetValue(last, out var portalNode))
            {
                var window = this.GetParentWindow();
                if (window != null)
                {
                    window.RemovePortal(this, portalNode);
                }
                _dropdownPortals.Remove(last);
            }

            _openDropdowns.RemoveAt(_openDropdowns.Count - 1);
            Container?.Invalidate(true);
        }
    }

    /// <summary>
    /// Closes any submenus that are deeper than the specified dropdown.
    /// Called when hovering over a leaf item to close sibling submenus.
    /// </summary>
    private void CloseSiblingSubmenus(MenuDropdown currentDropdown)
    {
        var window = this.GetParentWindow();
        int currentIndex = _openDropdowns.IndexOf(currentDropdown);
        if (currentIndex < 0) return;

        // Close all dropdowns after the current one
        while (_openDropdowns.Count > currentIndex + 1)
        {
            var last = _openDropdowns[_openDropdowns.Count - 1];
            if (last.ParentItem != null)
            {
                last.ParentItem.IsOpen = false;
            }

            if (_dropdownPortals.TryGetValue(last, out var portalNode) && window != null)
            {
                window.RemovePortal(this, portalNode);
                _dropdownPortals.Remove(last);
            }

            _openDropdowns.RemoveAt(_openDropdowns.Count - 1);
        }
    }

    #endregion

    #region Dropdown Positioning

    private Rectangle CalculateDropdownBounds(MenuItem item)
    {
        if (!item.HasChildren)
            return new Rectangle(0, 0, 0, 0);

        // Calculate dropdown dimensions
        int maxTextWidth = 0;
        int maxShortcutWidth = 0;
        int itemCount = 0;

        foreach (var child in item.Children)
        {
            if (child.IsSeparator)
            {
                itemCount++;
                continue;
            }

            int textWidth = MeasureText(child.Text);
            maxTextWidth = Math.Max(maxTextWidth, textWidth);

            if (!string.IsNullOrEmpty(child.Shortcut))
            {
                int shortcutWidth = MeasureText(child.Shortcut);
                maxShortcutWidth = Math.Max(maxShortcutWidth, shortcutWidth);
            }

            itemCount++;
        }

        // Calculate dropdown size
        int dropdownWidth = maxTextWidth + maxShortcutWidth + Configuration.ControlDefaults.MenuItemDropdownPadding;
        dropdownWidth = Math.Max(dropdownWidth, Configuration.ControlDefaults.MenuDropdownMinWidth);
        dropdownWidth = Math.Min(dropdownWidth, Configuration.ControlDefaults.MenuDropdownMaxWidth);

        int dropdownHeight = Math.Min(itemCount + 2, MaxDropdownHeight + 2);

        // Get screen dimensions from window system or parent window
        Window? parentWindow = this.GetParentWindow();
        int screenWidth = parentWindow?.Width ?? 80;
        int screenHeight = parentWindow?.Height ?? 24;

        if (Container?.GetConsoleWindowSystem != null)
        {
            var ws = Container.GetConsoleWindowSystem;
            var dimensions = ws.DesktopDimensions;
            screenWidth = dimensions.Width;
            screenHeight = dimensions.Height;
        }

        // Determine screen bounds for clamping
        // Use window buffer bounds if available (portals use window-relative coordinates)
        Rectangle screenBounds;
        if (parentWindow != null)
        {
            int bufferWidth = parentWindow.Width - 2;
            int bufferHeight = parentWindow.Height - 2;
            screenBounds = new Rectangle(0, 0, bufferWidth, bufferHeight);
        }
        else
        {
            screenBounds = new Rectangle(0, 0, screenWidth, screenHeight);
        }

        bool isTopLevel = (item.Parent == null);
        var itemBounds = item.Bounds;

        // Determine placement based on context
        PortalPlacement placement;
        if (isTopLevel && _orientation == MenuOrientation.Horizontal)
        {
            // Horizontal menu bar - top-level dropdowns open Below/Above
            // Use screen coordinates for direction check
            int contentTop = 0;
            if (parentWindow != null)
            {
                contentTop = parentWindow.Top + (parentWindow.ShowTitle ? 2 : 1);
            }
            int screenBottom = contentTop + itemBounds.Bottom;
            int screenTop = contentTop + itemBounds.Top;

            bool fitsBelow = (screenBottom + dropdownHeight <= screenHeight);
            bool fitsAbove = (screenTop - dropdownHeight >= 0);

            placement = (!fitsBelow && fitsAbove) ? PortalPlacement.Above : PortalPlacement.Below;
        }
        else
        {
            // Vertical menu OR nested submenu - opens Right/Left
            int contentLeft = 0;
            if (parentWindow != null)
            {
                contentLeft = parentWindow.Left + 1;
            }
            int screenRight = contentLeft + itemBounds.Right;
            int screenLeft = contentLeft + itemBounds.Left;

            bool fitsRight = (screenRight + dropdownWidth <= screenWidth);
            bool fitsLeft = (screenLeft - dropdownWidth >= 0);

            placement = (!fitsRight && fitsLeft) ? PortalPlacement.Left : PortalPlacement.Right;
        }

        // Use PortalPositioner for final placement and clamping
        var request = new PortalPositionRequest(
            Anchor: new Rectangle(itemBounds.X, itemBounds.Y, itemBounds.Width, itemBounds.Height),
            ContentSize: new Size(dropdownWidth, dropdownHeight),
            ScreenBounds: screenBounds,
            Placement: placement
        );

        var result = PortalPositioner.Calculate(request);
        return result.Bounds;
    }

    #endregion

    #region Dropdown Helpers

    private void ExecuteMenuItem(MenuItem item)
    {
        if (!item.IsEnabled || item.HasChildren)
            return;

        item.Action?.Invoke();
        ItemSelected?.Invoke(this, item);
    }

    #endregion
}

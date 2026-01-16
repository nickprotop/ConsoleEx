using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Drivers;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;
using Size = System.Drawing.Size;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls;

/// <summary>
/// A full-featured menu control supporting horizontal (menu bar) and vertical (sidebar) orientations,
/// unlimited submenu nesting, keyboard and mouse navigation, and overlay rendering.
/// </summary>
public class MenuControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IDOMPaintable, IContainer
{
    #region Fields

    // Configuration
    private MenuOrientation _orientation = MenuOrientation.Horizontal;
    private bool _isSticky;
    private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
    private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
    private Margin _margin = new Margin(0, 0, 0, 0);
    private StickyPosition _stickyPosition = StickyPosition.None;
    private bool _visible = true;
    private bool _enabled = true;
    private int? _width;

    // Menu items
    private readonly List<MenuItem> _items = new();

    // State tracking
    private MenuItem? _focusedItem;              // Keyboard focus
    private MenuItem? _hoveredItem;              // Mouse hover
    private MenuItem? _pressedItem;              // Mouse pressed (visual feedback)
    private bool _isMouseInside;                 // Track if mouse is currently inside control
    private readonly List<MenuDropdown> _openDropdowns = new();
    private readonly Dictionary<MenuDropdown, LayoutNode> _dropdownPortals = new();
    private DateTime _hoverStartTime = DateTime.MinValue;

    // Focus state
    private bool _hasFocus;

    // Mouse behavior constants
    private const int SubmenuHoverDelayMs = 150;
    private const int MaxDropdownHeight = 20;    // Max items before scrolling

    // Cached layout data
    private LayoutRect _lastBounds;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the orientation of the menu (Horizontal for menu bar, Vertical for sidebar).
    /// </summary>
    public MenuOrientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            Container?.Invalidate(true);
        }
    }

    /// <summary>
    /// Gets or sets whether the menu keeps focus when a dropdown is open (sticky mode).
    /// </summary>
    public bool IsSticky
    {
        get => _isSticky;
        set => _isSticky = value;
    }

    /// <summary>
    /// Gets the list of top-level menu items.
    /// </summary>
    public IReadOnlyList<MenuItem> Items => _items.AsReadOnly();

    // Menu bar colors (nullable for theme fallback)
    private Color? _menuBarBackgroundColor;
    private Color? _menuBarForegroundColor;
    private Color? _menuBarHighlightBackgroundColor;
    private Color? _menuBarHighlightForegroundColor;

    // Dropdown colors (nullable for theme fallback)
    private Color? _dropdownBackgroundColor;
    private Color? _dropdownForegroundColor;
    private Color? _dropdownHighlightBackgroundColor;
    private Color? _dropdownHighlightForegroundColor;

    /// <summary>
    /// Gets or sets the background color for the menu bar. Null uses theme default.
    /// </summary>
    public Color? MenuBarBackgroundColor
    {
        get => _menuBarBackgroundColor;
        set { _menuBarBackgroundColor = value; Container?.Invalidate(false); }
    }

    /// <summary>
    /// Gets or sets the foreground color for the menu bar. Null uses theme default.
    /// </summary>
    public Color? MenuBarForegroundColor
    {
        get => _menuBarForegroundColor;
        set { _menuBarForegroundColor = value; Container?.Invalidate(false); }
    }

    /// <summary>
    /// Gets or sets the background color for highlighted menu bar items. Null uses theme default.
    /// </summary>
    public Color? MenuBarHighlightBackgroundColor
    {
        get => _menuBarHighlightBackgroundColor;
        set { _menuBarHighlightBackgroundColor = value; Container?.Invalidate(false); }
    }

    /// <summary>
    /// Gets or sets the foreground color for highlighted menu bar items. Null uses theme default.
    /// </summary>
    public Color? MenuBarHighlightForegroundColor
    {
        get => _menuBarHighlightForegroundColor;
        set { _menuBarHighlightForegroundColor = value; Container?.Invalidate(false); }
    }

    /// <summary>
    /// Gets or sets the background color for dropdowns. Null uses theme default.
    /// </summary>
    public Color? DropdownBackgroundColor
    {
        get => _dropdownBackgroundColor;
        set { _dropdownBackgroundColor = value; Container?.Invalidate(false); }
    }

    /// <summary>
    /// Gets or sets the foreground color for dropdown items. Null uses theme default.
    /// </summary>
    public Color? DropdownForegroundColor
    {
        get => _dropdownForegroundColor;
        set { _dropdownForegroundColor = value; Container?.Invalidate(false); }
    }

    /// <summary>
    /// Gets or sets the background color for highlighted dropdown items. Null uses theme default.
    /// </summary>
    public Color? DropdownHighlightBackgroundColor
    {
        get => _dropdownHighlightBackgroundColor;
        set { _dropdownHighlightBackgroundColor = value; Container?.Invalidate(false); }
    }

    /// <summary>
    /// Gets or sets the foreground color for highlighted dropdown items. Null uses theme default.
    /// </summary>
    public Color? DropdownHighlightForegroundColor
    {
        get => _dropdownHighlightForegroundColor;
        set { _dropdownHighlightForegroundColor = value; Container?.Invalidate(false); }
    }

    // Resolved colors with theme fallback
    private Color ResolvedMenuBarBackground => ColorResolver.ResolveMenuBarBackground(_menuBarBackgroundColor, Container);
    private Color ResolvedMenuBarForeground => ColorResolver.ResolveMenuBarForeground(_menuBarForegroundColor, Container);
    private Color ResolvedMenuBarHighlightBackground => _menuBarHighlightBackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.MenuBarHighlightBackgroundColor ?? Color.Blue;
    private Color ResolvedMenuBarHighlightForeground => _menuBarHighlightForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.MenuBarHighlightForegroundColor ?? Color.White;
    private Color ResolvedDropdownBackground => _dropdownBackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.MenuDropdownBackgroundColor ?? Color.White;
    private Color ResolvedDropdownForeground => _dropdownForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.MenuDropdownForegroundColor ?? Color.Black;
    private Color ResolvedDropdownHighlightBackground => _dropdownHighlightBackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.MenuDropdownHighlightBackgroundColor ?? Color.Blue;
    private Color ResolvedDropdownHighlightForeground => _dropdownHighlightForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.MenuDropdownHighlightForegroundColor ?? Color.White;

    // Legacy property aliases for backward compatibility
    /// <summary>
    /// Gets or sets the background color for dropdowns. Alias for DropdownBackgroundColor.
    /// </summary>
    [Obsolete("Use DropdownBackgroundColor instead")]
    public Color BackgroundColor
    {
        get => ResolvedDropdownBackground;
        set => _dropdownBackgroundColor = value;
    }

    /// <summary>
    /// Gets or sets the foreground color for dropdown items. Alias for DropdownForegroundColor.
    /// </summary>
    [Obsolete("Use DropdownForegroundColor instead")]
    public Color ForegroundColor
    {
        get => ResolvedDropdownForeground;
        set => _dropdownForegroundColor = value;
    }

    /// <summary>
    /// Gets or sets the background color for highlighted items. Alias for DropdownHighlightBackgroundColor.
    /// </summary>
    [Obsolete("Use DropdownHighlightBackgroundColor instead")]
    public Color HighlightColor
    {
        get => ResolvedDropdownHighlightBackground;
        set => _dropdownHighlightBackgroundColor = value;
    }

    /// <summary>
    /// Gets or sets the foreground color for highlighted items. Alias for DropdownHighlightForegroundColor.
    /// </summary>
    [Obsolete("Use DropdownHighlightForegroundColor instead")]
    public Color HighlightForeground
    {
        get => ResolvedDropdownHighlightForeground;
        set => _dropdownHighlightForegroundColor = value;
    }

    #endregion

    #region IWindowControl Implementation

    public IContainer? Container { get; set; }
    public string? Name { get; set; }
    public object? Tag { get; set; }

    public HorizontalAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set { _horizontalAlignment = value; Container?.Invalidate(true); }
    }

    public VerticalAlignment VerticalAlignment
    {
        get => _verticalAlignment;
        set { _verticalAlignment = value; Container?.Invalidate(true); }
    }

    public Margin Margin
    {
        get => _margin;
        set { _margin = value; Container?.Invalidate(true); }
    }

    public StickyPosition StickyPosition
    {
        get => _stickyPosition;
        set { _stickyPosition = value; Container?.Invalidate(true); }
    }

    public bool Visible
    {
        get => _visible;
        set { _visible = value; Container?.Invalidate(true); }
    }

    public bool IsEnabled
    {
        get => _enabled;
        set { _enabled = value; Container?.Invalidate(true); }
    }

    public int? Width
    {
        get => _width;
        set
        {
            var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
            if (_width != validatedValue)
            {
                _width = validatedValue;
                Container?.Invalidate(true);
            }
        }
    }

    public int? ActualWidth => null;

    public void Dispose()
    {
        CloseAllMenus();
        _items.Clear();
        Container = null;
    }

    public Size GetLogicalContentSize()
    {
        if (_orientation == MenuOrientation.Horizontal)
        {
            int totalWidth = 0;
            foreach (var item in _items)
            {
                if (!item.IsSeparator)
                    totalWidth += AnsiConsoleHelper.StripSpectreLength(item.Text) + 4; // Padding
            }
            return new Size(totalWidth + _margin.Left + _margin.Right, 1 + _margin.Top + _margin.Bottom);
        }
        else
        {
            int maxWidth = 0;
            foreach (var item in _items)
            {
                if (!item.IsSeparator)
                    maxWidth = Math.Max(maxWidth, AnsiConsoleHelper.StripSpectreLength(item.Text));
            }
            return new Size(maxWidth + 4 + _margin.Left + _margin.Right, _items.Count + _margin.Top + _margin.Bottom);
        }
    }

    public void Invalidate()
    {
        Container?.Invalidate(true);
    }

    #endregion

    #region IFocusableControl Implementation

    public bool HasFocus
    {
        get => _hasFocus;
        set
        {
            _hasFocus = value;
            Container?.Invalidate(true);
        }
    }

    public bool CanReceiveFocus => _enabled;

    public event EventHandler? GotFocus;
    public event EventHandler? LostFocus;

    public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
    {
        var hadFocus = HasFocus;
        HasFocus = focus;

        if (focus && !hadFocus)
        {
            // Clear hover state when gaining focus via keyboard
            // (mouse focus will set _focusedItem properly in click handler)
            if (reason == FocusReason.Keyboard || reason == FocusReason.Programmatic)
            {
                _hoveredItem = null;
            }

            // When gaining focus, focus first item if nothing focused
            if (_focusedItem == null && _items.Count > 0)
            {
                _focusedItem = _items.FirstOrDefault(i => !i.IsSeparator && i.IsEnabled);
            }
            GotFocus?.Invoke(this, EventArgs.Empty);
        }
        else if (!focus && hadFocus)
        {
            // When losing focus, clear all item states and close menus
            // Sticky only prevents closing on keyboard/programmatic focus loss, not mouse clicks
            _hoveredItem = null;
            _focusedItem = null;
            _pressedItem = null;
            if (!_isSticky || reason == FocusReason.Mouse)
            {
                CloseAllMenus();
            }
            LostFocus?.Invoke(this, EventArgs.Empty);
        }

        Container?.Invalidate(true);

        // Notify parent Window if focus state actually changed
        if (hadFocus != focus)
        {
            this.NotifyParentWindowOfFocusChange(focus);
        }
    }

    #endregion

    #region IMouseAwareControl Implementation

    public bool WantsMouseEvents => _enabled;
    public bool CanFocusWithMouse => _enabled;

    public event EventHandler<MouseEventArgs>? MouseClick;
    public event EventHandler<MouseEventArgs>? MouseEnter;
    public event EventHandler<MouseEventArgs>? MouseLeave;
    public event EventHandler<MouseEventArgs>? MouseMove;

    public bool ProcessMouseEvent(MouseEventArgs args)
    {
        if (!_enabled)
            return false;

        // Handle mouse leave event - clear hover if unfocused
        if (args.HasFlag(MouseFlags.MouseLeave))
        {
            _isMouseInside = false;
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
            _isMouseInside = true;
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
                _hoverStartTime = DateTime.Now;

                // If any dropdown is open and we hover a different top-level item
                if (_openDropdowns.Count > 0 && hitItem != null && IsTopLevelItem(hitItem))
                {
                    // Switch dropdown immediately
                    CloseAllMenus();
                    // Restore hover after CloseAllMenus cleared it (we're switching, not closing)
                    _hoveredItem = hitItem;
                    if (hitItem.HasChildren)
                        OpenDropdownInternal(hitItem);
                }
                // If hovering item with children in open dropdown
                else if (_openDropdowns.Count > 0 && hitItem?.HasChildren == true && !hitItem.IsOpen)
                {
                    // Check if enough time has elapsed for delayed submenu open
                    var elapsed = (DateTime.Now - _hoverStartTime).TotalMilliseconds;
                    if (elapsed >= SubmenuHoverDelayMs)
                    {
                        OpenSubmenu(hitItem);
                    }
                }

                ItemHovered?.Invoke(this, hitItem);
                Container?.Invalidate(true);
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
                    // Open dropdown
                    CloseAllMenus();
                    if (hitItem.HasChildren)
                        OpenDropdownInternal(hitItem);
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

    /// <summary>
    /// Processes mouse events for a dropdown portal.
    /// Called by MenuPortalContent when the portal receives mouse events.
    /// </summary>
    /// <param name="dropdown">The dropdown that received the event.</param>
    /// <param name="args">Mouse event arguments with portal-relative coordinates.</param>
    /// <returns>True if the event was handled.</returns>
    internal bool ProcessDropdownMouseEvent(MenuDropdown dropdown, MouseEventArgs args)
    {
        if (!_enabled)
            return false;

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

                ItemHovered?.Invoke(this, hitItem);
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

    #region IInteractiveControl Implementation

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

    #region IDOMPaintable Implementation

    public LayoutSize MeasureDOM(LayoutConstraints constraints)
    {
        int width, height;

        if (_orientation == MenuOrientation.Horizontal)
        {
            // Calculate total width of all menu items
            int totalWidth = 0;
            foreach (var item in _items)
            {
                if (!item.IsSeparator)
                    totalWidth += AnsiConsoleHelper.StripSpectreLength(item.Text) + 4; // Add padding
            }

            width = _width ?? totalWidth;
            height = 1;
        }
        else // Vertical
        {
            // Calculate max width and total height
            int maxWidth = 0;
            foreach (var item in _items)
            {
                if (!item.IsSeparator)
                    maxWidth = Math.Max(maxWidth, AnsiConsoleHelper.StripSpectreLength(item.Text));
            }

            width = _width ?? (maxWidth + 4);
            height = _items.Count;
        }

        // Add margins
        width += _margin.Left + _margin.Right;
        height += _margin.Top + _margin.Bottom;

        return new LayoutSize(
            Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
        );
    }

    public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
    {
        _lastBounds = bounds;


        Color windowBg = Container?.BackgroundColor ?? defaultBg;
        Color windowFg = Container?.ForegroundColor ?? defaultFg;

        // 1. Paint top-level menu items
        if (_orientation == MenuOrientation.Horizontal)
        {
            PaintHorizontalMenuBar(buffer, bounds, clipRect, windowFg, windowBg);
        }
        else
        {
            PaintVerticalMenu(buffer, bounds, clipRect, windowFg, windowBg);
        }

        // Note: Dropdowns now render via portal system, not here
    }

    #endregion

    #region IContainer Implementation

    Color IContainer.BackgroundColor
    {
        get => Container?.BackgroundColor ?? Color.Black;
        set { /* Menu doesn't support changing background color directly */ }
    }

    Color IContainer.ForegroundColor
    {
        get => Container?.ForegroundColor ?? Color.White;
        set { /* Menu doesn't support changing foreground color directly */ }
    }

    ConsoleWindowSystem? IContainer.GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;

    bool IContainer.IsDirty
    {
        get => false; // Menu doesn't track dirty state
        set { /* Menu doesn't track dirty state */ }
    }

    void IContainer.Invalidate(bool redrawAll, IWindowControl? callerControl)
    {
        Container?.Invalidate(redrawAll, callerControl);
    }

    int? IContainer.GetVisibleHeightForControl(IWindowControl control)
    {
        // Menu doesn't host child controls in the traditional sense
        return null;
    }

    public void Invalidate(bool fullRender)
    {
        Container?.Invalidate(fullRender);
    }

    #endregion

    #region Public Methods - Menu Management

    /// <summary>
    /// Adds a menu item to the menu.
    /// </summary>
    public void AddItem(MenuItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        _items.Add(item);
        Container?.Invalidate(true);
    }

    /// <summary>
    /// Removes a menu item from the menu.
    /// </summary>
    public void RemoveItem(MenuItem item)
    {
        if (_items.Remove(item))
        {
            if (_focusedItem == item)
                _focusedItem = null;
            if (_hoveredItem == item)
                _hoveredItem = null;
            if (_pressedItem == item)
                _pressedItem = null;

            Container?.Invalidate(true);
        }
    }

    /// <summary>
    /// Removes all menu items.
    /// </summary>
    public void ClearItems()
    {
        _items.Clear();
        _focusedItem = null;
        _hoveredItem = null;
        _pressedItem = null;
        CloseAllMenus();
        Container?.Invalidate(true);
    }

    /// <summary>
    /// Finds a menu item by its path (e.g., "File/Recent/File1.txt").
    /// </summary>
    public MenuItem? FindItemByPath(string path)
    {
        var parts = path.Split('/');
        MenuItem? current = null;
        var searchList = _items;

        foreach (var part in parts)
        {
            current = searchList.FirstOrDefault(i => i.Text == part);
            if (current == null)
                return null;
            searchList = current.Children;
        }

        return current;
    }

    /// <summary>
    /// Sets whether a menu item is enabled by its path.
    /// </summary>
    public void SetItemEnabled(string path, bool enabled)
    {
        var item = FindItemByPath(path);
        if (item != null)
        {
            item.IsEnabled = enabled;
            Container?.Invalidate(true);
        }
    }

    /// <summary>
    /// Opens the dropdown for a top-level menu item.
    /// </summary>
    public void OpenDropdown(string itemText)
    {
        var item = _items.FirstOrDefault(i => i.Text == itemText);
        if (item != null && item.HasChildren)
        {
            CloseAllMenus();
            OpenDropdownInternal(item);
        }
    }

    /// <summary>
    /// Closes all open dropdowns and submenus.
    /// </summary>
    public void CloseAllMenus()
    {
        var window = Container as Window ?? FindContainingWindow();

        foreach (var dropdown in _openDropdowns)
        {
            if (dropdown.ParentItem != null)
                dropdown.ParentItem.IsOpen = false;

            // Remove portal if it exists
            if (_dropdownPortals.TryGetValue(dropdown, out var portalNode) && window != null)
            {
                window.RemovePortal(this, portalNode);
            }
        }

        _openDropdowns.Clear();
        _dropdownPortals.Clear();

        // Clear hover state - dropdown items are no longer visible
        _hoveredItem = null;

        Container?.Invalidate(true);
    }

    /// <summary>
    /// Sets focus to the menu control.
    /// </summary>
    public void Focus()
    {
        SetFocus(true, FocusReason.Programmatic);
    }

    #endregion

    #region Public Events

    /// <summary>
    /// Event fired when a menu item is selected (executed).
    /// </summary>
    public event EventHandler<MenuItem>? ItemSelected;

    /// <summary>
    /// Event fired when a menu item is hovered.
    /// </summary>
    public event EventHandler<MenuItem>? ItemHovered;

    #endregion

    #region Private Helper Methods - Dropdown Management

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

        // Calculate dropdown bounds (will be implemented in rendering phase)
        dropdown.Bounds = CalculateDropdownBounds(item);
        dropdown.Direction = CalculateSubmenuDirection(item);


        _openDropdowns.Add(dropdown);

        // Create portal for dropdown overlay
        var portalContent = new MenuPortalContent(this, dropdown);
        var window = Container as Window ?? FindContainingWindow();
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
                var window = Container as Window ?? FindContainingWindow();
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

    private Window? FindContainingWindow()
    {
        // Start with the immediate container
        IContainer? currentContainer = Container;

        // Maximum number of levels to prevent infinite loops
        const int MaxLevels = 10;
        int level = 0;

        // Continue traversing up until we find a Window or reach the top
        while (currentContainer != null && level < MaxLevels)
        {
            // If the current container is a Window, return it
            if (currentContainer is Window window)
                return window;

            // If the current container is an IWindowControl, move up to its container
            if (currentContainer is IWindowControl control)
            {
                currentContainer = control.Container;
            }
            else if (currentContainer is ColumnContainer columnContainer)
            {
                currentContainer = columnContainer.HorizontalGridContent.Container;
            }
            else
            {
                break;
            }

            level++;
        }

        return null;
    }

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

            int textWidth = AnsiConsoleHelper.StripSpectreLength(child.Text);
            maxTextWidth = Math.Max(maxTextWidth, textWidth);

            if (!string.IsNullOrEmpty(child.Shortcut))
            {
                int shortcutWidth = AnsiConsoleHelper.StripSpectreLength(child.Shortcut);
                maxShortcutWidth = Math.Max(maxShortcutWidth, shortcutWidth);
            }

            itemCount++;
        }

        // Calculate dropdown size
        int dropdownWidth = maxTextWidth + maxShortcutWidth + 8; // Padding + indicator + margins
        dropdownWidth = Math.Max(dropdownWidth, 15); // Minimum width
        dropdownWidth = Math.Min(dropdownWidth, 50); // Maximum width

        int dropdownHeight = Math.Min(itemCount + 2, MaxDropdownHeight + 2); // +2 for borders

        // Get screen size
        int screenWidth = 160; // Default, will get from console driver if available
        int screenHeight = 40;

        if (Container?.GetConsoleWindowSystem != null)
        {
            // Try to get actual screen size
            screenWidth = 160; // Placeholder - would get from driver
            screenHeight = 40;
        }

        // Calculate position based on direction
        var direction = CalculateSubmenuDirection(item);
        int x, y;

        if (item.Parent == null)
        {
            // Top-level dropdown
            if (direction == SubmenuDirection.Below)
            {
                x = item.Bounds.X;
                y = item.Bounds.Bottom;
            }
            else // Above
            {
                x = item.Bounds.X;
                y = item.Bounds.Y - dropdownHeight;
            }
        }
        else
        {
            // Submenu
            if (direction == SubmenuDirection.Right)
            {
                x = item.Bounds.Right;
                y = item.Bounds.Y;
            }
            else // Left
            {
                x = item.Bounds.X - dropdownWidth;
                y = item.Bounds.Y;
            }
        }

        // Clamp to screen bounds
        x = Math.Max(0, Math.Min(x, screenWidth - dropdownWidth));
        y = Math.Max(0, Math.Min(y, screenHeight - dropdownHeight));

        return new Rectangle(x, y, dropdownWidth, dropdownHeight);
    }

    private SubmenuDirection CalculateSubmenuDirection(MenuItem item)
    {
        // Get screen size
        int screenWidth = 160;
        int screenHeight = 40;

        if (Container?.GetConsoleWindowSystem != null)
        {
            screenWidth = 160;
            screenHeight = 40;
        }

        // Calculate submenu dimensions
        int submenuWidth = 20; // Approximate
        int submenuHeight = Math.Min(item.Children.Count + 2, MaxDropdownHeight + 2);

        var itemBounds = item.Bounds;

        // For top-level dropdown
        if (item.Parent == null)
        {
            // Try below first
            int belowY = itemBounds.Bottom;
            if (belowY + submenuHeight <= screenHeight)
                return SubmenuDirection.Below;

            // Try above
            int aboveY = itemBounds.Top - submenuHeight;
            if (aboveY >= 0)
                return SubmenuDirection.Above;

            return SubmenuDirection.Below; // Best effort
        }

        // For nested submenu
        // Try right first
        int rightX = itemBounds.Right;
        if (rightX + submenuWidth <= screenWidth)
            return SubmenuDirection.Right;

        // Try left
        int leftX = itemBounds.Left - submenuWidth;
        if (leftX >= 0)
            return SubmenuDirection.Left;

        return SubmenuDirection.Right; // Best effort
    }

    private bool IsTopLevelItem(MenuItem item)
    {
        return _items.Contains(item);
    }

    private MenuItem? HitTest(int screenX, int screenY)
    {
        // Check top-level items
        foreach (var item in _items)
        {
            if (item.Bounds.Contains(screenX, screenY))
                return item;
        }

        // Check open dropdowns in reverse order (topmost first)
        for (int i = _openDropdowns.Count - 1; i >= 0; i--)
        {
            var dropdown = _openDropdowns[i];

            // Check if click is in dropdown bounds
            if (dropdown.Contains(screenX, screenY))
            {
                // Check each item in dropdown
                foreach (var item in dropdown.VisibleItems)
                {
                    if (item.Bounds.Contains(screenX, screenY))
                        return item;
                }

                // Click inside dropdown but not on item (border/padding)
                return null;
            }
        }

        return null;
    }

    private void ExecuteMenuItem(MenuItem item)
    {
        if (!item.IsEnabled || item.HasChildren)
            return;

        item.Action?.Invoke();
        ItemSelected?.Invoke(this, item);
    }

    #endregion

    #region Private Helper Methods - Rendering

    private void PaintHorizontalMenuBar(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color fg, Color bg)
    {
        int x = bounds.X + _margin.Left;
        int y = bounds.Y + _margin.Top;

        foreach (var item in _items)
        {
            if (item.IsSeparator)
                continue;

            int itemWidth = AnsiConsoleHelper.StripSpectreLength(item.Text) + 4;
            var itemBounds = new Rectangle(x, y, itemWidth, 1);
            item.Bounds = itemBounds;

            var state = GetItemState(item);
            PaintMenuItem(buffer, item, x, y, itemWidth, state, true);

            x += itemWidth;
        }
    }

    private void PaintVerticalMenu(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color fg, Color bg)
    {
        int x = bounds.X + _margin.Left;
        int y = bounds.Y + _margin.Top;
        int width = bounds.Width - _margin.Left - _margin.Right;

        foreach (var item in _items)
        {
            if (item.IsSeparator)
            {
                // Draw separator line
                if (y >= clipRect.Y && y < clipRect.Bottom)
                {
                    buffer.FillRect(new LayoutRect(x, y, width, 1), '─', fg, bg);
                }
            }
            else
            {
                var itemBounds = new Rectangle(x, y, width, 1);
                item.Bounds = itemBounds;

                var state = GetItemState(item);
                PaintMenuItem(buffer, item, x, y, width, state, false);
            }

            y++;
        }
    }

    /// <summary>
    /// Internal method for portal content to paint dropdowns.
    /// Called by MenuPortalContent.PaintDOM() to render dropdown overlays.
    /// </summary>
    internal void PaintDropdownInternal(CharacterBuffer buffer, MenuDropdown dropdown, LayoutRect clipRect)
    {
        PaintDropdown(buffer, dropdown, clipRect);
    }

    private void PaintDropdown(CharacterBuffer buffer, MenuDropdown dropdown, LayoutRect clipRect)
    {
        var bounds = dropdown.Bounds;


        // Draw border (rounded box)
        DrawBox(buffer, bounds);

        // Draw items
        int y = bounds.Y + 1;
        var (start, end) = dropdown.GetVisibleRange();

        for (int i = start; i < end; i++)
        {
            var item = dropdown.VisibleItems[i];
            int x = bounds.X + 1;
            int width = bounds.Width - 2;

            if (item.IsSeparator)
            {
                // Draw separator line
                buffer.FillRect(new LayoutRect(x, y, width, 1), '─', Color.Grey, ResolvedDropdownBackground);
            }
            else
            {
                var itemBounds = new Rectangle(x, y, width, 1);
                item.Bounds = itemBounds;

                var state = GetItemState(item);
                PaintMenuItem(buffer, item, x, y, width, state, false);
            }

            y++;
        }

        // Draw scroll indicators if needed
        if (dropdown.CanScrollUp)
        {
            buffer.SetCell(bounds.X + bounds.Width / 2, bounds.Y, '▲', ResolvedDropdownForeground, ResolvedDropdownBackground);
        }

        if (dropdown.CanScrollDown)
        {
            buffer.SetCell(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height - 1, '▼', ResolvedDropdownForeground, ResolvedDropdownBackground);
        }

        // Debug: Verify box character is still there after complete dropdown paint
        var cellAfterPaint = buffer.GetCell(bounds.X, bounds.Y);
    }

    private void PaintMenuItem(CharacterBuffer buffer, MenuItem item, int x, int y, int width, MenuItemState state, bool isTopLevel)
    {
        // Determine colors based on state and whether this is a top-level item or dropdown item
        Color bg, fg;
        if (isTopLevel)
        {
            (bg, fg) = state switch
            {
                MenuItemState.Disabled => (ResolvedMenuBarBackground, Color.Grey),
                MenuItemState.Pressed => (Color.DarkBlue, Color.White),
                MenuItemState.Highlighted => (ResolvedMenuBarHighlightBackground, ResolvedMenuBarHighlightForeground),
                MenuItemState.Open => (ResolvedMenuBarHighlightBackground, ResolvedMenuBarHighlightForeground),
                _ => (ResolvedMenuBarBackground, ResolvedMenuBarForeground)
            };
        }
        else
        {
            (bg, fg) = state switch
            {
                MenuItemState.Disabled => (ResolvedDropdownBackground, Color.Grey),
                MenuItemState.Pressed => (Color.DarkBlue, Color.White),
                MenuItemState.Highlighted => (ResolvedDropdownHighlightBackground, ResolvedDropdownHighlightForeground),
                MenuItemState.Open => (ResolvedDropdownHighlightBackground, ResolvedDropdownHighlightForeground),
                _ => (ResolvedDropdownBackground, ResolvedDropdownForeground)
            };
        }

        // Build item text
        string text = item.Text;

        // Render
        if (isTopLevel)
        {
            // Top-level items: simple text with padding (no truncation needed)
            string displayText = $" {text} ";
            buffer.WriteString(x, y, displayText, fg, bg);
        }
        else
        {
            // Dropdown items: full width with alignment
            string shortcut = item.Shortcut ?? "";
            int shortcutWidth = AnsiConsoleHelper.StripSpectreLength(shortcut);
            int indicatorWidth = item.HasChildren ? 2 : 0;
            int textWidth = AnsiConsoleHelper.StripSpectreLength(text);
            int availableForText = width - shortcutWidth - indicatorWidth - 4; // Padding

            // Truncate text if needed
            if (textWidth > availableForText && availableForText > 0)
            {
                text = AnsiConsoleHelper.TruncateSpectre(text, Math.Max(0, availableForText - 3)) + "...";
            }

            buffer.FillRect(new LayoutRect(x, y, width, 1), ' ', fg, bg);
            buffer.WriteString(x + 2, y, text, fg, bg);

            if (!string.IsNullOrEmpty(shortcut))
            {
                int shortcutX = x + width - shortcutWidth - indicatorWidth - 2;
                buffer.WriteString(shortcutX, y, shortcut, Color.Grey, bg);
            }

            if (item.HasChildren)
            {
                buffer.WriteString(x + width - 2, y, "►", fg, bg);
            }
        }
    }

    private enum MenuItemState
    {
        Normal,
        Highlighted,  // Hovered or keyboard focused
        Pressed,      // Mouse down
        Open,         // Has open dropdown/submenu
        Disabled
    }

    private MenuItemState GetItemState(MenuItem item)
    {
        if (!item.IsEnabled)
            return MenuItemState.Disabled;
        if (item == _pressedItem)
            return MenuItemState.Pressed;

        // Priority: hover takes precedence when mouse is active
        if (_hoveredItem != null)
        {
            if (item == _hoveredItem)
                return MenuItemState.Highlighted;
        }
        else if (item == _focusedItem)
        {
            return MenuItemState.Highlighted;
        }

        if (item.IsOpen)
            return MenuItemState.Open;
        return MenuItemState.Normal;
    }

    private void DrawBox(CharacterBuffer buffer, Rectangle bounds)
    {
        var fg = ResolvedDropdownForeground;
        var bg = ResolvedDropdownBackground;

        // Draw rounded box using box-drawing characters
        // Corners
        buffer.SetCell(bounds.X, bounds.Y, '╭', fg, bg);
        var cell = buffer.GetCell(bounds.X, bounds.Y);
        buffer.SetCell(bounds.Right - 1, bounds.Y, '╮', fg, bg);
        buffer.SetCell(bounds.X, bounds.Bottom - 1, '╰', fg, bg);
        buffer.SetCell(bounds.Right - 1, bounds.Bottom - 1, '╯', fg, bg);

        // Horizontal lines
        for (int x = bounds.X + 1; x < bounds.Right - 1; x++)
        {
            buffer.SetCell(x, bounds.Y, '─', fg, bg);
            buffer.SetCell(x, bounds.Bottom - 1, '─', fg, bg);
        }

        // Vertical lines
        for (int y = bounds.Y + 1; y < bounds.Bottom - 1; y++)
        {
            buffer.SetCell(bounds.X, y, '│', fg, bg);
            buffer.SetCell(bounds.Right - 1, y, '│', fg, bg);
        }

        // Fill interior
        for (int y = bounds.Y + 1; y < bounds.Bottom - 1; y++)
        {
            buffer.FillRect(new LayoutRect(bounds.X + 1, y, bounds.Width - 2, 1), ' ', fg, bg);
        }
    }

    #endregion

    #region Private Helper Methods - Keyboard Navigation

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
        if (_items.Count == 0)
            return;

        // Find starting point: if focused item is top-level, use it; otherwise use current dropdown parent
        int currentIndex;
        if (_focusedItem != null && _items.Contains(_focusedItem))
        {
            currentIndex = _items.IndexOf(_focusedItem);
        }
        else if (_openDropdowns.Count > 0 && _openDropdowns[0].ParentItem != null)
        {
            currentIndex = _items.IndexOf(_openDropdowns[0].ParentItem);
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
                currentIndex = _items.Count - 1;

            var item = _items[currentIndex];
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
        if (_items.Count == 0)
            return;

        // Find starting point: if focused item is top-level, use it; otherwise use current dropdown parent
        int currentIndex;
        if (_focusedItem != null && _items.Contains(_focusedItem))
        {
            currentIndex = _items.IndexOf(_focusedItem);
        }
        else if (_openDropdowns.Count > 0 && _openDropdowns[0].ParentItem != null)
        {
            currentIndex = _items.IndexOf(_openDropdowns[0].ParentItem);
        }
        else
        {
            currentIndex = -1;
        }

        int startIndex = currentIndex;

        do
        {
            currentIndex++;
            if (currentIndex >= _items.Count)
                currentIndex = 0;

            var item = _items[currentIndex];
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
            items = _items;
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
            items = _items;
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
            items = _items;
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

    private void OpenSubmenu(MenuItem item)
    {
        if (!item.HasChildren)
            return;

        // Close any existing submenu at this level or deeper
        var window = Container as Window ?? FindContainingWindow();
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

    /// <summary>
    /// Closes any submenus that are deeper than the specified dropdown.
    /// Called when hovering over a leaf item to close sibling submenus.
    /// </summary>
    private void CloseSiblingSubmenus(MenuDropdown currentDropdown)
    {
        var window = Container as Window ?? FindContainingWindow();
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

    private void UpdateFocusToLastDropdown()
    {
        if (_openDropdowns.Count > 0)
        {
            var lastDropdown = _openDropdowns[_openDropdowns.Count - 1];
            _focusedItem = lastDropdown.VisibleItems.FirstOrDefault(i => !i.IsSeparator && i.IsEnabled)
                         ?? lastDropdown.ParentItem;
        }
        else if (_items.Count > 0)
        {
            _focusedItem = _items.FirstOrDefault(i => !i.IsSeparator && i.IsEnabled);
        }

        Container?.Invalidate(true);
    }

    #endregion
}

/// <summary>
/// Internal portal content control for rendering menu dropdowns as overlays.
/// This control is created by MenuControl when a dropdown opens and is added as a portal child
/// to render outside the normal bounds of the menu control.
/// </summary>
internal class MenuPortalContent : IWindowControl, IDOMPaintable, IMouseAwareControl
{
    private readonly MenuControl _owner;
    private readonly MenuDropdown _dropdown;

    public MenuPortalContent(MenuControl owner, MenuDropdown dropdown)
    {
        _owner = owner;
        _dropdown = dropdown;
    }

    #region IMouseAwareControl Implementation

    /// <summary>
    /// Whether this control wants to receive mouse events.
    /// </summary>
    public bool WantsMouseEvents => true;

    /// <summary>
    /// Whether this control can receive focus via mouse clicks.
    /// Portal content should not steal focus from owner.
    /// </summary>
    public bool CanFocusWithMouse => false;

    /// <summary>
    /// Event fired when the control is clicked.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseClick;

    /// <summary>
    /// Event fired when the mouse enters the control area.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseEnter;

    /// <summary>
    /// Event fired when the mouse leaves the control area.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseLeave;

    /// <summary>
    /// Event fired when the mouse moves over the control.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseMove;

    /// <summary>
    /// Processes mouse events for this portal content and delegates to owner MenuControl.
    /// </summary>
    /// <param name="args">Mouse event arguments with portal-relative coordinates.</param>
    /// <returns>True if the event was handled.</returns>
    public bool ProcessMouseEvent(MouseEventArgs args)
    {
        // Delegate to owner MenuControl for handling
        return _owner.ProcessDropdownMouseEvent(_dropdown, args);
    }

    #endregion

    // IWindowControl minimal implementation
    public int? ActualWidth => _dropdown.Bounds.Width;
    public int? ActualHeight => _dropdown.Bounds.Height;
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;
    public IContainer? Container { get; set; }
    public Margin Margin { get; set; } = new Margin(0, 0, 0, 0);
    public StickyPosition StickyPosition { get; set; } = StickyPosition.None;
    public string? Name { get; set; }
    public object? Tag { get; set; }
    public bool Visible { get; set; } = true;
    public int? Width { get; set; }

    public Size GetLogicalContentSize()
    {
        return new Size(_dropdown.Bounds.Width, _dropdown.Bounds.Height);
    }

    /// <summary>
    /// Gets the absolute bounds for portal positioning.
    /// </summary>
    public Rectangle GetPortalBounds()
    {
        return _dropdown.Bounds;
    }

    public void Invalidate()
    {
        Container?.Invalidate(true);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    // IDOMPaintable implementation
    public LayoutSize MeasureDOM(LayoutConstraints constraints)
    {
        return new LayoutSize(_dropdown.Bounds.Width, _dropdown.Bounds.Height);
    }

    public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
                         Color defaultFg, Color defaultBg)
    {
        // Delegate painting to the owner MenuControl
        _owner.PaintDropdownInternal(buffer, _dropdown, clipRect);

        // Debug: Verify box character after PaintDropdownInternal returns
        var cell = buffer.GetCell(_dropdown.Bounds.X, _dropdown.Bounds.Y);
    }
}

/// <summary>
/// Specifies the orientation of a menu control.
/// </summary>
public enum MenuOrientation
{
    /// <summary>
    /// Horizontal menu bar (File, Edit, View).
    /// </summary>
    Horizontal,

    /// <summary>
    /// Vertical sidebar menu.
    /// </summary>
    Vertical
}

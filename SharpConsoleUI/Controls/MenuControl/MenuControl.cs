using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;
using Size = System.Drawing.Size;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls;

/// <summary>
/// A full-featured menu control supporting horizontal (menu bar) and vertical (sidebar) orientations,
/// unlimited submenu nesting, keyboard and mouse navigation, and overlay rendering.
/// </summary>
public partial class MenuControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IContainer
{
    #region Fields

    // Configuration
    private MenuOrientation _orientation = MenuOrientation.Horizontal;
    private bool _isSticky;
    private bool _enabled = true;

    // Menu items
    private readonly List<MenuItem> _items = new();

    // State tracking
    private MenuItem? _focusedItem;              // Keyboard focus
    private MenuItem? _hoveredItem;              // Mouse hover
    private MenuItem? _pressedItem;              // Mouse pressed (visual feedback)
    private readonly List<MenuDropdown> _openDropdowns = new();
    private readonly Dictionary<MenuDropdown, LayoutNode> _dropdownPortals = new();
    private DateTime _hoverStartTime = DateTime.MinValue;
    private MenuItem? _pendingSubmenuItem;         // Item awaiting hover delay to open submenu

    // Focus state
    private bool _hasFocus;

    // Mouse behavior constants (values in Configuration.ControlDefaults)
    private const int SubmenuHoverDelayMs = Configuration.ControlDefaults.MenuSubmenuHoverDelayMs;
    private const int MaxDropdownHeight = Configuration.ControlDefaults.MenuMaxDropdownHeight;

    // Cached layout data
    private LayoutRect _lastBounds;

    // Measurement cache (avoid repeated StripSpectreLength calls per frame)
    private readonly Dictionary<string, int> _measurementCache = new();

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
        set { _dropdownBackgroundColor = value; Container?.Invalidate(true); }
    }

    /// <summary>
    /// Gets or sets the foreground color for dropdown items. Alias for DropdownForegroundColor.
    /// </summary>
    [Obsolete("Use DropdownForegroundColor instead")]
    public Color ForegroundColor
    {
        get => ResolvedDropdownForeground;
        set { _dropdownForegroundColor = value; Container?.Invalidate(true); }
    }

    /// <summary>
    /// Gets or sets the background color for highlighted items. Alias for DropdownHighlightBackgroundColor.
    /// </summary>
    [Obsolete("Use DropdownHighlightBackgroundColor instead")]
    public Color HighlightColor
    {
        get => ResolvedDropdownHighlightBackground;
        set { _dropdownHighlightBackgroundColor = value; Container?.Invalidate(true); }
    }

    /// <summary>
    /// Gets or sets the foreground color for highlighted items. Alias for DropdownHighlightForegroundColor.
    /// </summary>
    [Obsolete("Use DropdownHighlightForegroundColor instead")]
    public Color HighlightForeground
    {
        get => ResolvedDropdownHighlightForeground;
        set { _dropdownHighlightForegroundColor = value; Container?.Invalidate(true); }
    }

    #endregion

    #region Measurement Cache

    private int MeasureText(string text)
    {
        if (_measurementCache.TryGetValue(text, out int cached))
            return cached;

        int width = AnsiConsoleHelper.StripSpectreLength(text);
        _measurementCache[text] = width;
        return width;
    }

    private void InvalidateMeasurementCache()
    {
        _measurementCache.Clear();
    }

    #endregion

    #region IWindowControl Implementation

    /// <inheritdoc/>
    public bool IsEnabled
    {
        get => _enabled;
        set { _enabled = value; Container?.Invalidate(true); }
    }

    /// <inheritdoc/>
    public override int? ContentWidth => null;

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        // Safety unsubscribe in case CloseAllMenus doesn't cover it
        if (_openDropdowns.Count > 0)
        {
            var parentWindow = this.GetParentWindow();
            if (parentWindow != null)
            {
                parentWindow.UnhandledMouseClick -= OnWindowUnhandledMouseClick;
                parentWindow.Deactivated -= OnWindowDeactivated;
            }
        }

        CloseAllMenus();
        _items.Clear();
    }

    /// <inheritdoc/>
    public override Size GetLogicalContentSize()
    {
        if (_orientation == MenuOrientation.Horizontal)
        {
            int totalWidth = 0;
            foreach (var item in _items)
            {
                if (!item.IsSeparator)
                    totalWidth += MeasureText(item.Text) + Configuration.ControlDefaults.MenuItemHorizontalPadding;
            }
            return new Size(totalWidth + Margin.Left + Margin.Right, 1 + Margin.Top + Margin.Bottom);
        }
        else
        {
            int maxWidth = 0;
            foreach (var item in _items)
            {
                if (!item.IsSeparator)
                    maxWidth = Math.Max(maxWidth, MeasureText(item.Text));
            }
            return new Size(maxWidth + Configuration.ControlDefaults.MenuItemHorizontalPadding + Margin.Left + Margin.Right, _items.Count + Margin.Top + Margin.Bottom);
        }
    }

    #endregion

    #region IFocusableControl Implementation

    /// <inheritdoc/>
    public bool HasFocus
    {
        get => _hasFocus;
        set
        {
            _hasFocus = value;
            Container?.Invalidate(true);
        }
    }

    /// <inheritdoc/>
    public bool CanReceiveFocus => _enabled;

    /// <inheritdoc/>
    public event EventHandler? GotFocus;
    /// <inheritdoc/>
    public event EventHandler? LostFocus;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
        InvalidateMeasurementCache();
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

            InvalidateMeasurementCache();
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
        InvalidateMeasurementCache();
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
        var window = this.GetParentWindow();

        // Unsubscribe from dismiss events
        if (_openDropdowns.Count > 0 && window != null)
        {
            window.UnhandledMouseClick -= OnWindowUnhandledMouseClick;
            window.Deactivated -= OnWindowDeactivated;
        }

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

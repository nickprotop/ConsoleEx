using SharpConsoleUI.Layout;
using System.Drawing;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Represents an open dropdown or submenu with its rendering state.
/// Used internally by MenuControl to track and render active menus.
/// </summary>
internal class MenuDropdown
{
    /// <summary>
    /// Gets or sets the parent menu item that owns this dropdown.
    /// Null for top-level dropdown (menu bar dropdown).
    /// </summary>
    public MenuItem? ParentItem { get; set; }

    /// <summary>
    /// Gets or sets the list of visible menu items in this dropdown.
    /// </summary>
    public List<MenuItem> VisibleItems { get; set; } = new();

    /// <summary>
    /// Gets or sets the screen-space bounds of this dropdown.
    /// </summary>
    public Rectangle Bounds { get; set; }

    /// <summary>
    /// Gets or sets the direction this dropdown/submenu opens relative to its parent.
    /// </summary>
    public SubmenuDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the scroll offset for dropdowns taller than the available screen space.
    /// </summary>
    public int ScrollOffset { get; set; }

    /// <summary>
    /// Gets the maximum number of items that can be displayed without scrolling.
    /// </summary>
    public int MaxVisibleItems { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets whether this dropdown requires scrolling.
    /// </summary>
    public bool RequiresScrolling => VisibleItems.Count > MaxVisibleItems;

    /// <summary>
    /// Gets whether the scroll offset can be decreased (scroll up).
    /// </summary>
    public bool CanScrollUp => ScrollOffset > 0;

    /// <summary>
    /// Gets whether the scroll offset can be increased (scroll down).
    /// </summary>
    public bool CanScrollDown => ScrollOffset + MaxVisibleItems < VisibleItems.Count;

    /// <summary>
    /// Scrolls the dropdown up by one item if possible.
    /// </summary>
    public void ScrollUp()
    {
        if (CanScrollUp)
            ScrollOffset--;
    }

    /// <summary>
    /// Scrolls the dropdown down by one item if possible.
    /// </summary>
    public void ScrollDown()
    {
        if (CanScrollDown)
            ScrollOffset++;
    }

    /// <summary>
    /// Ensures the specified item is visible by adjusting the scroll offset.
    /// </summary>
    /// <param name="item">The menu item to make visible.</param>
    public void EnsureItemVisible(MenuItem item)
    {
        int index = VisibleItems.IndexOf(item);
        if (index < 0)
            return;

        // Item is above visible range
        if (index < ScrollOffset)
        {
            ScrollOffset = index;
        }
        // Item is below visible range
        else if (index >= ScrollOffset + MaxVisibleItems)
        {
            ScrollOffset = index - MaxVisibleItems + 1;
        }
    }

    /// <summary>
    /// Gets the range of currently visible item indices.
    /// </summary>
    public (int start, int end) GetVisibleRange()
    {
        int start = ScrollOffset;
        int end = Math.Min(ScrollOffset + MaxVisibleItems, VisibleItems.Count);
        return (start, end);
    }

    /// <summary>
    /// Determines whether a screen coordinate is within this dropdown's bounds.
    /// </summary>
    public bool Contains(int screenX, int screenY)
    {
        return Bounds.Contains(screenX, screenY);
    }
}

/// <summary>
/// Specifies the direction a dropdown or submenu opens relative to its parent.
/// </summary>
internal enum SubmenuDirection
{
    /// <summary>
    /// Opens above the parent item (top-level dropdown).
    /// </summary>
    Above,

    /// <summary>
    /// Opens below the parent item (top-level dropdown).
    /// </summary>
    Below,

    /// <summary>
    /// Opens to the left of the parent item (submenu).
    /// </summary>
    Left,

    /// <summary>
    /// Opens to the right of the parent item (submenu).
    /// </summary>
    Right
}

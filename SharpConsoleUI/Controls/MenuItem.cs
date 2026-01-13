using SharpConsoleUI.Layout;
using System.Drawing;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Represents a menu item with support for hierarchical menu structures.
/// </summary>
public class MenuItem
{
    /// <summary>
    /// Gets or sets the display text for this menu item.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the keyboard shortcut text displayed on the right (display only, not handled by MenuControl).
    /// Example: "Ctrl+S", "Alt+F4"
    /// </summary>
    public string? Shortcut { get; set; }

    /// <summary>
    /// Gets or sets whether this menu item is enabled. Disabled items are shown but cannot be selected.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this menu item is a separator (horizontal line).
    /// </summary>
    public bool IsSeparator { get; set; }

    /// <summary>
    /// Gets or sets user-defined data associated with this menu item.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Gets or sets the parent menu item. Null for top-level items.
    /// </summary>
    public MenuItem? Parent { get; internal set; }

    /// <summary>
    /// Gets the list of child menu items (submenu).
    /// </summary>
    public List<MenuItem> Children { get; } = new();

    /// <summary>
    /// Gets whether this menu item has any children (is a submenu).
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// Gets or sets whether this menu item's dropdown/submenu is currently open.
    /// Managed internally by MenuControl.
    /// </summary>
    public bool IsOpen { get; internal set; }

    /// <summary>
    /// Gets or sets the screen-space bounds of this menu item for hit testing.
    /// Managed internally by MenuControl.
    /// </summary>
    public Rectangle Bounds { get; internal set; }

    /// <summary>
    /// Gets or sets the action to execute when this menu item is selected.
    /// Not called for items with children (submenus).
    /// </summary>
    public Action? Action { get; set; }

    /// <summary>
    /// Adds a child menu item to this item's submenu.
    /// </summary>
    /// <param name="item">The menu item to add as a child.</param>
    public void AddChild(MenuItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        item.Parent = this;
        Children.Add(item);
    }

    /// <summary>
    /// Gets the full hierarchical path of this menu item.
    /// Example: "File/Recent/Document1.txt"
    /// </summary>
    /// <returns>A forward-slash separated path string.</returns>
    public string GetPath()
    {
        var path = new List<string>();
        var current = this;

        while (current != null)
        {
            if (!string.IsNullOrEmpty(current.Text))
                path.Insert(0, current.Text);
            current = current.Parent;
        }

        return string.Join("/", path);
    }

    /// <summary>
    /// Gets the depth level of this menu item in the hierarchy (0 for top-level).
    /// </summary>
    public int GetDepth()
    {
        int depth = 0;
        var current = Parent;

        while (current != null)
        {
            depth++;
            current = current.Parent;
        }

        return depth;
    }

    /// <summary>
    /// Returns a string representation of this menu item for debugging.
    /// </summary>
    public override string ToString()
    {
        if (IsSeparator)
            return "[Separator]";

        var shortcut = string.IsNullOrEmpty(Shortcut) ? "" : $" ({Shortcut})";
        var children = HasChildren ? $" [{Children.Count} children]" : "";
        var enabled = IsEnabled ? "" : " [Disabled]";

        return $"{Text}{shortcut}{children}{enabled}";
    }
}

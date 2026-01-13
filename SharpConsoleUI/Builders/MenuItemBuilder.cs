using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating MenuItem instances with a hierarchical structure.
/// </summary>
public class MenuItemBuilder
{
    private readonly MenuItem _item;

    /// <summary>
    /// Initializes a new MenuItemBuilder with the specified text.
    /// </summary>
    /// <param name="text">The text to display for this menu item.</param>
    public MenuItemBuilder(string text)
    {
        _item = new MenuItem { Text = text };
    }

    /// <summary>
    /// Adds a child menu item with text and action.
    /// </summary>
    /// <param name="text">The text to display for the child item.</param>
    /// <param name="action">The action to execute when selected.</param>
    public MenuItemBuilder AddItem(string text, Action action)
    {
        _item.AddChild(new MenuItem
        {
            Text = text,
            Action = action
        });
        return this;
    }

    /// <summary>
    /// Adds a child menu item with text, shortcut, and action.
    /// </summary>
    /// <param name="text">The text to display for the child item.</param>
    /// <param name="shortcut">The keyboard shortcut text (display only).</param>
    /// <param name="action">The action to execute when selected.</param>
    public MenuItemBuilder AddItem(string text, string shortcut, Action action)
    {
        _item.AddChild(new MenuItem
        {
            Text = text,
            Shortcut = shortcut,
            Action = action
        });
        return this;
    }

    /// <summary>
    /// Adds a child menu item with nested subitems using a configuration action.
    /// </summary>
    /// <param name="text">The text to display for the child item.</param>
    /// <param name="configure">Action to configure the child item's submenu.</param>
    public MenuItemBuilder AddItem(string text, Action<MenuItemBuilder> configure)
    {
        var builder = new MenuItemBuilder(text);
        configure(builder);
        _item.AddChild(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a separator line to the submenu.
    /// </summary>
    public MenuItemBuilder AddSeparator()
    {
        _item.AddChild(new MenuItem { IsSeparator = true });
        return this;
    }

    /// <summary>
    /// Marks this menu item as disabled.
    /// </summary>
    public MenuItemBuilder Disabled()
    {
        _item.IsEnabled = false;
        return this;
    }

    /// <summary>
    /// Sets the keyboard shortcut text for this menu item (display only).
    /// </summary>
    /// <param name="shortcut">The shortcut text to display (e.g., "Ctrl+S").</param>
    public MenuItemBuilder WithShortcut(string shortcut)
    {
        _item.Shortcut = shortcut;
        return this;
    }

    /// <summary>
    /// Sets the action to execute when this menu item is selected.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public MenuItemBuilder WithAction(Action action)
    {
        _item.Action = action;
        return this;
    }

    /// <summary>
    /// Sets user-defined data associated with this menu item.
    /// </summary>
    /// <param name="tag">The user data to associate.</param>
    public MenuItemBuilder WithTag(object tag)
    {
        _item.Tag = tag;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured MenuItem instance.
    /// </summary>
    internal MenuItem Build() => _item;
}

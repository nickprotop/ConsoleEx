using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating MenuControl instances with a clean, chainable API.
/// </summary>
public class MenuBuilder
{
    private MenuOrientation _orientation = MenuOrientation.Horizontal;
    private readonly List<MenuItem> _items = new();
    private string? _name;
    private bool _isSticky;
    private EventHandler<MenuItem>? _itemSelectedHandler;
    private EventHandler<MenuItem>? _itemHoveredHandler;
    private WindowEventHandler<MenuItem>? _itemSelectedWithWindowHandler;

    /// <summary>
    /// Sets the menu orientation to horizontal (menu bar style).
    /// </summary>
    public MenuBuilder Horizontal()
    {
        _orientation = MenuOrientation.Horizontal;
        return this;
    }

    /// <summary>
    /// Sets the menu orientation to vertical (sidebar style).
    /// </summary>
    public MenuBuilder Vertical()
    {
        _orientation = MenuOrientation.Vertical;
        return this;
    }

    /// <summary>
    /// Sets the name of the menu control for identification.
    /// </summary>
    public MenuBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Enables sticky mode, where the menu keeps focus when dropdowns are open.
    /// </summary>
    public MenuBuilder Sticky()
    {
        _isSticky = true;
        return this;
    }

    /// <summary>
    /// Adds a menu item with subitems using a configuration action.
    /// </summary>
    /// <param name="text">The text to display for this menu item.</param>
    /// <param name="configure">Action to configure the menu item's children.</param>
    public MenuBuilder AddItem(string text, Action<MenuItemBuilder> configure)
    {
        var builder = new MenuItemBuilder(text);
        configure(builder);
        _items.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a simple menu item with text, shortcut, and action.
    /// </summary>
    /// <param name="text">The text to display for this menu item.</param>
    /// <param name="shortcut">The keyboard shortcut text (display only).</param>
    /// <param name="action">The action to execute when selected.</param>
    public MenuBuilder AddItem(string text, string shortcut, Action action)
    {
        _items.Add(new MenuItem
        {
            Text = text,
            Shortcut = shortcut,
            Action = action
        });
        return this;
    }

    /// <summary>
    /// Adds a simple menu item with text and action (no shortcut).
    /// </summary>
    /// <param name="text">The text to display for this menu item.</param>
    /// <param name="action">The action to execute when selected.</param>
    public MenuBuilder AddItem(string text, Action action)
    {
        _items.Add(new MenuItem
        {
            Text = text,
            Action = action
        });
        return this;
    }

    /// <summary>
    /// Adds a separator line to the menu.
    /// </summary>
    public MenuBuilder AddSeparator()
    {
        _items.Add(new MenuItem { IsSeparator = true });
        return this;
    }

    /// <summary>
    /// Registers an event handler for when a menu item is selected.
    /// </summary>
    public MenuBuilder OnItemSelected(EventHandler<MenuItem> handler)
    {
        _itemSelectedHandler = handler;
        return this;
    }

    /// <summary>
    /// Registers an event handler for when a menu item is selected, with access to the parent window.
    /// </summary>
    public MenuBuilder OnItemSelected(WindowEventHandler<MenuItem> handler)
    {
        _itemSelectedWithWindowHandler = handler;
        return this;
    }

    /// <summary>
    /// Registers an event handler for when a menu item is hovered.
    /// </summary>
    public MenuBuilder OnItemHovered(EventHandler<MenuItem> handler)
    {
        _itemHoveredHandler = handler;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured MenuControl instance.
    /// </summary>
    public MenuControl Build()
    {
        var menu = new MenuControl
        {
            Orientation = _orientation,
            Name = _name,
            IsSticky = _isSticky
        };

        foreach (var item in _items)
            menu.AddItem(item);

        if (_itemSelectedHandler != null)
            menu.ItemSelected += _itemSelectedHandler;

        if (_itemSelectedWithWindowHandler != null)
        {
            menu.ItemSelected += (sender, item) =>
            {
                // Get the parent window from the control's container chain
                var window = GetParentWindow(sender as MenuControl);
                if (window != null)
                    _itemSelectedWithWindowHandler(sender, item, window);
            };
        }

        if (_itemHoveredHandler != null)
            menu.ItemHovered += _itemHoveredHandler;

        return menu;
    }

    /// <summary>
    /// Allows implicit conversion from MenuBuilder to MenuControl.
    /// </summary>
    public static implicit operator MenuControl(MenuBuilder builder) => builder.Build();

    private Window? GetParentWindow(MenuControl? control)
    {
        if (control == null)
            return null;

        var container = control.Container;
        while (container != null)
        {
            if (container is Window window)
                return window;

            if (container is IWindowControl windowControl)
                container = windowControl.Container;
            else
                break;
        }

        return null;
    }
}

/// <summary>
/// Delegate for event handlers that receive both the event arguments and the parent window.
/// </summary>
public delegate void WindowEventHandler<T>(object? sender, T args, Window window);

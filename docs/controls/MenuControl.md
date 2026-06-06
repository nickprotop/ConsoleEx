# MenuControl

Full-featured menu control supporting horizontal (menu bar) and vertical (sidebar) orientations, unlimited submenu nesting, keyboard and mouse navigation, and overlay-rendered dropdowns.

## Overview

`MenuControl` renders either a horizontal menu bar (File, Edit, View) or a vertical sidebar of menu items. Each top-level item can have an unlimited hierarchy of child items, separators, keyboard-shortcut hints, and disabled states. Dropdowns and submenus are rendered as portal overlays that float above other content and are automatically positioned and clamped to the available screen space (opening below/above for horizontal bars, right/left for submenus).

Items are modeled by the `MenuItem` class, which supports display text, a (display-only) shortcut hint, a per-item foreground color, an `Action` to invoke on selection, a `Tag` for user data, and a live `Children` collection for nested submenus. Because `MenuItem` implements `INotifyPropertyChanged`, display properties such as `Text`, `Shortcut`, `ForegroundColor`, and `IsEnabled` can be data-bound from a view model.

Navigation is fully keyboard- and mouse-driven: arrow keys move between items and open/close submenus, letter keys jump to matching items, hover opens submenus after a short aim delay, and clicking outside or deactivating the window dismisses open menus. Colors for the menu bar and dropdowns (including highlight colors) can be customized independently, and any unset color falls back to the active theme.

See also: [ToolbarControl](ToolbarControl.md) — for a horizontal strip of action buttons

## Quick Start

```csharp
MenuControl menu = Controls.Menu()
    .Horizontal()
    .WithName("mainMenu")
    .Sticky()
    .AddItem("File", m => m
        .AddItem("New", "Ctrl+N", () => NewFile())
        .AddItem("Open", "Ctrl+O", () => OpenFile())
        .AddSeparator()
        .AddItem("Exit", "Alt+F4", () => window.Close()))
    .AddItem("Edit", m => m
        .AddItem("Undo", "Ctrl+Z", () => Undo())
        .AddItem("Redo", "Ctrl+Y", () => Redo()))
    .Build();

menu.StickyPosition = StickyPosition.Top;
window.AddControl(menu);
```

## Builder API

Create a `MenuBuilder` through the `Controls` factory:

```csharp
var builder = Controls.Menu();
```

### Orientation and Behavior

```csharp
.Horizontal()                               // Menu bar style (default)
.Vertical()                                 // Sidebar style
.Sticky()                                   // Keep focus while a dropdown is open
.WithName(string name)                      // Identify the control
```

### Adding Items

```csharp
.AddItem(string text, Action<MenuItemBuilder> configure)        // Item with a submenu
.AddItem(string text, Action action)                            // Leaf item
.AddItem(string text, Action action, Color foregroundColor)
.AddItem(string text, string shortcut, Action action)
.AddItem(string text, string shortcut, Action action, Color foregroundColor)
.AddSeparator()                             // Horizontal separator line
```

### Menu Bar Colors

```csharp
.WithMenuBarBackgroundColor(Color color)
.WithMenuBarForegroundColor(Color color)
.WithMenuBarHighlightBackgroundColor(Color color)
.WithMenuBarHighlightForegroundColor(Color color)
.WithMenuBarColors(Color background, Color foreground, Color highlightBackground, Color highlightForeground)
```

### Dropdown Colors

```csharp
.WithDropdownBackgroundColor(Color color)
.WithDropdownForegroundColor(Color color)
.WithDropdownHighlightBackgroundColor(Color color)
.WithDropdownHighlightForegroundColor(Color color)
.WithDropdownColors(Color background, Color foreground, Color highlightBackground, Color highlightForeground)
```

### Events

```csharp
.OnItemSelected(EventHandler<MenuItem> handler)
.OnItemSelected(WindowEventHandler<MenuItem> handler)       // Handler also receives the parent window
.OnItemHovered(EventHandler<MenuItem> handler)
```

### Building

```csharp
MenuControl control = builder.Build();

// Implicit conversion is also supported:
MenuControl control = Controls.Menu().AddItem("File", () => { });
```

### MenuItemBuilder

Submenus are configured with a nested `MenuItemBuilder` (the argument to `AddItem(text, configure)`):

```csharp
.AddItem(string text, Action action)                            // Child leaf item
.AddItem(string text, Action action, Color foregroundColor)
.AddItem(string text, string shortcut, Action action)
.AddItem(string text, string shortcut, Action action, Color foregroundColor)
.AddItem(string text, Action<MenuItemBuilder> configure)        // Nested submenu
.AddSeparator()
.Disabled()                                 // Mark this item disabled
.WithShortcut(string shortcut)              // Shortcut hint (display only)
.WithAction(Action action)
.WithTag(object tag)
.WithForegroundColor(Color color)
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Orientation` | `MenuOrientation` | `Horizontal` | Menu bar (`Horizontal`) or sidebar (`Vertical`) layout |
| `IsSticky` | `bool` | `false` | Keep menu focus while a dropdown is open |
| `Items` | `MenuItemCollection` | empty | Live, observable collection of top-level items |
| `IsEnabled` | `bool` | `true` | Enable/disable all menu interaction |
| `HasFocus` | `bool` | `false` | Whether the menu currently has keyboard focus |
| `CanReceiveFocus` | `bool` | `true` | Whether the menu can receive focus (mirrors `IsEnabled`) |
| `MenuBarBackgroundColor` | `Color?` | `null` | Menu bar background (null = theme default) |
| `MenuBarForegroundColor` | `Color?` | `null` | Menu bar foreground (null = theme default) |
| `MenuBarHighlightBackgroundColor` | `Color?` | `null` | Highlighted menu bar item background (null = theme) |
| `MenuBarHighlightForegroundColor` | `Color?` | `null` | Highlighted menu bar item foreground (null = theme) |
| `DropdownBackgroundColor` | `Color?` | `null` | Dropdown background (null = theme default) |
| `DropdownForegroundColor` | `Color?` | `null` | Dropdown item foreground (null = theme default) |
| `DropdownHighlightBackgroundColor` | `Color?` | `null` | Highlighted dropdown item background (null = theme) |
| `DropdownHighlightForegroundColor` | `Color?` | `null` | Highlighted dropdown item foreground (null = theme) |
| `WantsMouseEvents` | `bool` | `true` | Whether the control receives mouse events (mirrors `IsEnabled`) |
| `CanFocusWithMouse` | `bool` | `true` | Whether mouse clicks can focus the menu (mirrors `IsEnabled`) |

> The legacy aliases `BackgroundColor`, `ForegroundColor`, `HighlightColor`, and `HighlightForeground` are `[Obsolete]` and map to the corresponding `Dropdown*` colors. Use the named color properties above instead.

### MenuItem Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | `""` | Display text (supports markup) |
| `Shortcut` | `string?` | `null` | Right-aligned shortcut hint (display only, not handled) |
| `ForegroundColor` | `Color?` | `null` | Custom foreground (ignored when disabled or highlighted) |
| `IsEnabled` | `bool` | `true` | Whether the item can be selected |
| `IsSeparator` | `bool` | `false` | Render this item as a separator line |
| `Tag` | `object?` | `null` | User-defined data |
| `Parent` | `MenuItem?` | `null` | Parent item (null for top-level) |
| `Children` | `MenuItemCollection` | empty | Observable child submenu items |
| `HasChildren` | `bool` | — | Whether this item has any children |
| `Action` | `Action?` | `null` | Invoked when selected (not called for items with children) |
| `IsOpen` | `bool` | `false` | Whether this item's dropdown is open (managed internally) |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `ItemSelected` | `MenuItem` | Fired when a leaf item is selected/executed |
| `ItemSelectedAsync` | `MenuItem` | Async counterpart of `ItemSelected` |
| `ItemHovered` | `MenuItem` | Fired when an item is hovered |
| `MouseClick` | `MouseEventArgs` | Fired when a menu item is clicked |
| `MouseRightClick` | `MouseEventArgs` | Fired on right-click (also closes open menus) |
| `MouseEnter` | `MouseEventArgs` | Fired when the mouse enters the control |
| `MouseLeave` | `MouseEventArgs` | Fired when the mouse leaves the control |

## Keyboard Support

### Horizontal (menu bar)

| Key | Action |
|-----|--------|
| **Left Arrow** | Previous top-level item (closes/switches dropdown; in a submenu, returns to parent) |
| **Right Arrow** | Open focused item's submenu, or move to next top-level item |
| **Down Arrow** | Open the focused item's dropdown; navigate down within an open dropdown |
| **Up Arrow** | Navigate up within an open dropdown |
| **Enter** | Open submenu (if item has children) or execute the focused item |
| **Escape** | Close current submenu/dropdown, or unfocus the menu (unless sticky) |
| **Home** | Jump to first item |
| **End** | Jump to last item |
| **Letter key** | Jump to the next item whose text starts with that letter |

### Vertical (sidebar)

| Key | Action |
|-----|--------|
| **Up Arrow** | Previous item (or navigate up within an open dropdown) |
| **Down Arrow** | Next item (or navigate down within an open dropdown) |
| **Right Arrow** | Open the focused item's submenu |
| **Left Arrow** | Close the current submenu and return to parent |
| **Enter** | Open submenu (if item has children) or execute the focused item |
| **Escape** | Close current submenu/dropdown, or unfocus the menu (unless sticky) |
| **Home** | Jump to first item |
| **End** | Jump to last item |
| **Letter key** | Jump to the next item whose text starts with that letter |

## Mouse Support

| Action | Result |
|--------|--------|
| **Click on top-level item** | Focus the menu and open its dropdown (or execute if it has no children) |
| **Click on item with children** | Open its submenu |
| **Click on leaf item** | Execute the item's action and close all menus |
| **Hover over item with children** | Open the submenu after a short aim delay |
| **Hover over top-level item (dropdown open)** | Switch to that item's dropdown |
| **Mouse wheel over dropdown** | Scroll the dropdown when it exceeds the visible height |
| **Right-click** | Close all open menus and fire `MouseRightClick` |
| **Click outside / window deactivated** | Dismiss all open menus |

## Examples

### Horizontal Menu Bar (IDE-style)

```csharp
MenuControl menu = Controls.Menu()
    .Horizontal()
    .WithName("mainMenu")
    .Sticky()
    .AddItem("File", m => m
        .AddItem("New", "Ctrl+N", HandleNewFile)
        .AddItem("Open", "Ctrl+O", () => FileDialogs.ShowFilePickerAsync(ws))
        .AddSeparator()
        .AddItem("Save", "Ctrl+S", HandleSaveFile)
        .AddItem("Save As...", "Ctrl+Shift+S", () => FileDialogs.ShowSaveFileAsync(ws))
        .AddSeparator()
        .AddItem("Exit", "Alt+F4", () => ws.CloseWindow(window)))
    .AddItem("Edit", m => m
        .AddItem("Undo", "Ctrl+Z", () => Undo())
        .AddItem("Redo", "Ctrl+Y", () => Redo())
        .AddSeparator()
        .AddItem("Cut", "Ctrl+X", () => Cut())
        .AddItem("Copy", "Ctrl+C", () => Copy())
        .AddItem("Paste", "Ctrl+V", () => Paste()))
    .AddItem("Help", m => m
        .AddItem("Documentation", "F1", () => ShowDocs())
        .AddItem("About", () => AboutDialog.Show(ws)))
    .Build();

menu.StickyPosition = StickyPosition.Top;
window.AddControl(menu);
```

### Vertical Sidebar Menu

```csharp
MenuControl sidebar = Controls.Menu()
    .Vertical()
    .AddItem("Dashboard", () => ShowDashboard())
    .AddItem("Reports", m => m
        .AddItem("Daily", () => ShowDaily())
        .AddItem("Weekly", () => ShowWeekly())
        .AddItem("Monthly", () => ShowMonthly()))
    .AddItem("Settings", () => ShowSettings())
    .Build();

window.AddControl(sidebar);
```

### Nested Submenus

```csharp
MenuControl menu = Controls.Menu()
    .AddItem("Insert", m => m
        .AddItem("Image", () => InsertImage())
        .AddItem("Table", t => t
            .AddItem("2x2", () => InsertTable(2, 2))
            .AddItem("3x3", () => InsertTable(3, 3))
            .AddItem("Custom...", () => InsertCustomTable()))
        .AddSeparator()
        .AddItem("Page Break", () => InsertPageBreak()))
    .Build();
```

### Disabled Items, Colors, and Tags via MenuItemBuilder

```csharp
MenuControl menu = Controls.Menu()
    .AddItem("Account", m => m
        .AddItem("Profile", () => ShowProfile())
        .AddSeparator()
        .AddItem("Delete Account", b => b
            .WithAction(() => DeleteAccount())
            .WithForegroundColor(Color.Red)
            .WithTag("danger"))
        .AddItem("Premium (Locked)", b => b
            .WithAction(() => { })
            .Disabled()))
    .Build();
```

### Customizing Colors

```csharp
MenuControl menu = Controls.Menu()
    .Horizontal()
    .WithMenuBarColors(
        background: Color.Grey15,
        foreground: Color.White,
        highlightBackground: Color.Blue,
        highlightForeground: Color.White)
    .WithDropdownColors(
        background: Color.Grey11,
        foreground: Color.Silver,
        highlightBackground: Color.Blue,
        highlightForeground: Color.White)
    .AddItem("File", m => m.AddItem("New", () => NewFile()))
    .Build();
```

### Handling Selection with Window Access

```csharp
MenuControl menu = Controls.Menu()
    .AddItem("File", m => m
        .AddItem("Close", () => { }))
    .OnItemSelected((sender, item, window) =>
    {
        windowSystem.NotificationStateService.ShowNotification(
            "Menu",
            $"Selected: {item.GetPath()}",
            NotificationSeverity.Info);
    })
    .Build();
```

### Runtime Manipulation by Path

```csharp
// Find and update items after the menu is built
var saveItem = menu.FindItemByPath("File/Save");

// Enable/disable by path
menu.SetItemEnabled("File/Save", isDirty);

// Open a dropdown programmatically
menu.OpenDropdown("File");

// Add and remove items via the live Items collection
menu.AddItem(new MenuItem { Text = "Recent", Action = () => ShowRecent() });
menu.ClearItems();
```

## Best Practices

1. **Use `.Horizontal()` for menu bars and `.Vertical()` for sidebars** — set `StickyPosition.Top` on a horizontal bar so it stays pinned.
2. **Provide shortcut hints** with the `shortcut` overloads (`"Ctrl+S"`); these are display-only — wire the real keybindings via the window's key handling.
3. **Group related items with `.AddSeparator()`** to keep long dropdowns scannable.
4. **Disable rather than hide** unavailable actions using `MenuItem.IsEnabled` or `.Disabled()`, and update them via `SetItemEnabled(path, enabled)`.
5. **Use `.Sticky()`** when you want the menu to retain focus while dropdowns are open (common for keyboard-driven menu bars).
6. **Mutate menus from the UI thread** — modify `Items`/`Children` or call `OpenDropdown`/`CloseAllMenus` on the UI thread (use `windowSystem.EnqueueOnUIThread` from background work).

## See Also

- [ToolbarControl](ToolbarControl.md) - Horizontal strip of action buttons
- [DropdownControl](DropdownControl.md) - Single-selection dropdown / combo box
- [ListControl](ListControl.md) - Scrollable selectable list

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

# DropdownControl

A dropdown/combobox control that displays a list of selectable items with keyboard navigation, type-ahead search, and custom item formatting.

## Overview

The `DropdownControl` renders a single-line header showing the prompt and the currently selected item, followed by a triangle indicator. Activating the control expands a scrollable list of items rendered as a portal overlay, so the list can extend beyond the bounds of the parent container without affecting the control's own height (which stays constant at one line plus margins).

The dropdown supports full keyboard navigation (arrow keys, Home/End, Page Up/Down), type-ahead search (type the first letters of an item to jump to it), and mouse interaction including hover highlighting, click-to-select, and wheel scrolling. The open list auto-flips upward when there is not enough room below the header. Items can carry an optional icon, an icon color, a programmatic `Value`, and an arbitrary `Tag` for associating user data.

Selection is exposed three ways — by index (`SelectedIndex`), by item (`SelectedItem`), and by text/value (`SelectedValue`) — each with its own change event. A custom `ItemFormatter` delegate lets you control how each item is rendered in the open list.

See also: [ListControl](ListControl.md)

## Quick Start

```csharp
var dropdown = Controls.Dropdown("Choose a fruit:")
    .AddItems("Apple", "Banana", "Cherry")
    .SelectedIndex(0)
    .OnSelectionChanged((s, index) =>
    {
        // index is the newly selected index
    })
    .Build();

window.AddControl(dropdown);
```

## Builder API

Create a `DropdownBuilder` through the `Controls` factory:

```csharp
var builder = Controls.Dropdown("Select...");
```

### Item Methods

```csharp
.WithPrompt(string prompt)                        // Set the header prompt text
.AddItem(string text, string? value = null, Color? color = null)  // Add an item with optional value and icon color
.AddItem(DropdownItem item)                       // Add a pre-built DropdownItem
.AddItems(params string[] items)                  // Add multiple string items
.AddItems(IEnumerable<DropdownItem> items)        // Add multiple DropdownItems
.SelectedIndex(int index)                         // Set the initially selected index
```

### Layout Methods

```csharp
.WithAlignment(HorizontalAlignment align)         // Horizontal alignment
.WithVerticalAlignment(VerticalAlignment align)   // Vertical alignment
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int margin)                           // Uniform margin on all sides
.WithWidth(int width)                             // Fixed width
.Visible(bool visible = true)                     // Initial visibility
```

### Identity Methods

```csharp
.WithName(string name)                            // Name for FindControl<T>() lookups
.WithTag(object tag)                              // Arbitrary user data
.WithStickyPosition(StickyPosition pos)           // Sticky positioning
.StickyTop()                                      // Shorthand for StickyPosition.Top
.StickyBottom()                                   // Shorthand for StickyPosition.Bottom
```

### Event Methods

```csharp
.OnSelectionChanged(EventHandler<int> handler)
.OnSelectionChanged(WindowEventHandler<int> handler)              // includes Window reference
.OnSelectedItemChanged(EventHandler<DropdownItem?> handler)
.OnSelectedItemChanged(WindowEventHandler<DropdownItem?> handler) // includes Window reference
.OnSelectedValueChanged(EventHandler<string?> handler)
.OnSelectedValueChanged(WindowEventHandler<string?> handler)      // includes Window reference
.OnGotFocus(EventHandler handler)
.OnGotFocus(WindowEventHandler<EventArgs> handler)
.OnLostFocus(EventHandler handler)
.OnLostFocus(WindowEventHandler<EventArgs> handler)
```

### Building

```csharp
DropdownControl control = builder.Build();

// Implicit conversion is also supported:
DropdownControl control = Controls.Dropdown("Pick one:")
    .AddItems("A", "B", "C");
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Prompt` | `string` | `"Select an item:"` | Prompt text displayed in the header before the selected item. |
| `Items` | `List<DropdownItem>` | empty | The list of dropdown items. |
| `StringItems` | `List<string>` | empty | The items as plain strings; getting projects `Text`, setting replaces all items. |
| `SelectedIndex` | `int` | `-1` | Index of the selected item, or `-1` if none. |
| `SelectedItem` | `DropdownItem?` | `null` | The selected item, or `null` if none. |
| `SelectedValue` | `string?` | `null` | Text or `Value` of the selected item, or `null` if none. |
| `MaxVisibleItems` | `int` | `5` | Maximum items shown in the open list before scrolling (clamped to a minimum of 1). |
| `IsDropdownOpen` | `bool` | `false` | Whether the list is currently expanded. Setting it opens/closes the portal. |
| `IsEnabled` | `bool` | `true` | Whether the control accepts input. |
| `AutoAdjustWidth` | `bool` | `true` | Whether the control auto-sizes its width to fit content. |
| `ItemFormatter` | `ItemFormatterEvent?` | `null` | Delegate to customize how each item is rendered in the open list. |
| `BackgroundColor` | `Color?` | `null` | Header background (unfocused); inherits from container/theme when `null`. |
| `ForegroundColor` | `Color` | theme `ButtonForegroundColor` (`Color.White`) | Header foreground (unfocused). |
| `FocusedBackgroundColor` | `Color` | theme `ButtonFocusedBackgroundColor` (`Color.Blue`) | Header background when focused. |
| `FocusedForegroundColor` | `Color` | theme `ButtonFocusedForegroundColor` (`Color.White`) | Header foreground when focused. |
| `HighlightBackgroundColor` | `Color` | theme `DropdownHighlightBackgroundColor` (`Color.Blue`) | Background of the highlighted/selected item in the list. |
| `HighlightForegroundColor` | `Color` | theme `DropdownHighlightForegroundColor` (`Color.White`) | Foreground of the highlighted/selected item in the list. |
| `HasFocus` | `bool` | — | Read-only. Whether the control currently has focus. |
| `CanReceiveFocus` | `bool` | — | Read-only. True when `IsEnabled`. |
| `ContentHeight` | `int?` | — | Read-only. Rendered height (one line plus top/bottom margins). |
| `WantsMouseEvents` | `bool` | — | Read-only. True when `IsEnabled`. |
| `CanFocusWithMouse` | `bool` | — | Read-only. True when `IsEnabled`. |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `SelectedIndexChanged` | `EventHandler<int>` | Fires when the selected index changes. |
| `SelectedIndexChangedAsync` | `AsyncEventHandler<int>` | Async counterpart of `SelectedIndexChanged`. |
| `SelectedItemChanged` | `EventHandler<DropdownItem?>` | Fires when the selected item changes. |
| `SelectedItemChangedAsync` | `AsyncEventHandler<DropdownItem?>` | Async counterpart of `SelectedItemChanged`. |
| `SelectedValueChanged` | `EventHandler<string?>` | Fires when the selected value (text or `Value`) changes. |
| `SelectedValueChangedAsync` | `AsyncEventHandler<string?>` | Async counterpart of `SelectedValueChanged`. |
| `GotFocus` | `EventHandler` | Fires when the control receives focus. |
| `LostFocus` | `EventHandler` | Fires when the control loses focus (also closes the list). |
| `MouseClick` | `EventHandler<MouseEventArgs>` | Fires on a header or item click. |
| `MouseRightClick` | `EventHandler<MouseEventArgs>` | Fires when the header is right-clicked. |
| `MouseEnter` | `EventHandler<MouseEventArgs>` | Fires when the mouse enters the control. |
| `MouseLeave` | `EventHandler<MouseEventArgs>` | Fires when the mouse leaves the control. |
| `MouseMove` | `EventHandler<MouseEventArgs>` | Fires when the mouse moves over the control. |
| `MouseDoubleClick` | `EventHandler<MouseEventArgs>` | Declared for the `IMouseAwareControl` contract. |

## Keyboard Support

The dropdown ignores keys combined with Shift, Alt, or Ctrl.

| Key | Action |
|-----|--------|
| Enter | When closed, opens the list; when open, selects the highlighted item and closes |
| Escape | Closes the list without changing the selection |
| Down Arrow | When closed, opens the list; when open, moves the highlight down one item |
| Up Arrow | When open, moves the highlight up one item |
| Home | When open, highlights the first item |
| End | When open, highlights the last item |
| Page Up | When open, moves the highlight up by `MaxVisibleItems` |
| Page Down | When open, moves the highlight down by `MaxVisibleItems` |
| Letters / digits | When open, type-ahead: jumps to the first item whose text starts with the typed sequence (resets after ~1.5 s of inactivity) |

## Mouse Support

- **Click the header** toggles the list open or closed and focuses the control.
- **Hover over an item** highlights it (`HighlightBackgroundColor`/`HighlightForegroundColor`).
- **Click an item** selects it and closes the list.
- **Mouse wheel over the open list** scrolls items up or down when there are more than `MaxVisibleItems`.
- **Click the scroll-indicator row** (the `▴`/`▾` arrows) scrolls the list up or down.
- **Click outside the open list** dismisses it.
- **Right-click the header** raises `MouseRightClick`.

## DropdownItem

Each item in the list is a `DropdownItem`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | (constructor) | Display text for the item. |
| `Icon` | `string?` | `null` | Optional icon character/string shown before the text. |
| `IconColor` | `Color?` | `null` | Optional color for the icon. |
| `Value` | `string?` | `null` | Programmatic value returned by `SelectedValue` instead of `Text` when set. |
| `Tag` | `object?` | `null` | Arbitrary user data. |
| `IsEnabled` | `bool` | `true` | Whether the item can be selected. |

A `string` converts implicitly to a `DropdownItem`, so `dropdown.AddItem("Apple")` and `dropdown.Items.Add("Apple")` both work.

```csharp
var item = new DropdownItem("Japanese", icon: "●", iconColor: Color.Red)
{
    Value = "jp",
    Tag = someUserObject
};
```

## Examples

### Simple Dropdown

```csharp
var dropdown = Controls.Dropdown("Pick a color:")
    .AddItems("Red", "Green", "Blue")
    .SelectedIndex(0)
    .OnSelectionChanged((s, index) =>
    {
        logService.LogInfo($"Selected index {index}");
    })
    .Build();

window.AddControl(dropdown);
```

### Items with Values and Icons

```csharp
var cuisine = Controls.Dropdown("Choose cuisine...")
    .AddItem(new DropdownItem("Italian", "●", Color.Green) { Value = "it" })
    .AddItem(new DropdownItem("Mexican", "●", Color.Orange1) { Value = "mx" })
    .AddItem(new DropdownItem("Thai", "●", Color.Magenta1) { Value = "th" })
    .SelectedIndex(0)
    .OnSelectedValueChanged((s, value) =>
    {
        // value is "it", "mx", or "th"
    })
    .Build();

window.AddControl(cuisine);
```

### Reacting to the Selected Item

```csharp
var dropdown = Controls.Dropdown("Choose diet...")
    .AddItems("None", "Vegetarian", "Vegan", "Gluten-Free", "Keto")
    .SelectedIndex(0)
    .OnSelectedItemChanged((s, item) =>
    {
        string text = item?.Text ?? "None";
        // Use item.Tag, item.Value, etc.
    })
    .Build();

window.AddControl(dropdown);
```

### Limiting Visible Items and Scrolling

```csharp
var dropdown = Controls.Dropdown("Choose servings...")
    .AddItems("1 person", "2 people", "3 people", "4 people",
              "6 people", "8 people")
    .SelectedIndex(2)
    .Build();

// Show only 3 items at a time; the rest scroll
dropdown.MaxVisibleItems = 3;

window.AddControl(dropdown);
```

### Custom Item Formatting

```csharp
var dropdown = Controls.Dropdown("Status:")
    .AddItems("Active", "Pending", "Closed")
    .Build();

dropdown.ItemFormatter = (item, isSelected, hasFocus) =>
    isSelected ? $"[bold]{item.Text}[/]" : item.Text;

window.AddControl(dropdown);
```

### Linked Dropdowns with a Live Summary

```csharp
var summary = Controls.Markup("[dim]No selection[/]").Build();

var cuisine = Controls.Dropdown("Cuisine...")
    .AddItems("Japanese", "Italian", "Mexican")
    .SelectedIndex(0)
    .Build();

var spice = Controls.Dropdown("Spice...")
    .AddItems("Mild", "Medium", "Hot")
    .SelectedIndex(0)
    .Build();

void Update(object? s, int _)
{
    summary.SetContent(new List<string>
    {
        $"Cuisine: {cuisine.SelectedItem?.Text}",
        $"Spice:   {spice.SelectedItem?.Text}",
    });
}

cuisine.SelectedIndexChanged += Update;
spice.SelectedIndexChanged += Update;

window.AddControl(cuisine);
window.AddControl(spice);
window.AddControl(summary);
```

## Best Practices

1. **Set an initial selection** with `.SelectedIndex(0)` so the header shows a meaningful value instead of `(None)`.
2. **Use `Value` for stable keys** when item display text may change or be localized — read it back through `SelectedValue` or `OnSelectedValueChanged`.
3. **Tune `MaxVisibleItems`** for long lists to keep the overlay compact; the control scrolls automatically once items exceed the limit.
4. **Pick the right change event**: use `OnSelectionChanged` for index-based logic, `OnSelectedItemChanged` to access `Tag`/`Icon`, and `OnSelectedValueChanged` for value-keyed handling.
5. **Mutate items and selection on the UI thread** — wrap changes from background work in `windowSystem.EnqueueOnUIThread(...)`.
6. **Use the `WindowEventHandler` overloads** when a handler needs access to the parent `Window` without capturing it manually.

## See Also

- [ListControl](ListControl.md) - For larger, always-visible selectable lists
- [MenuControl](MenuControl.md) - For menu-based command selection
- [NavigationView](NavigationView.md) - For sidebar/page navigation

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

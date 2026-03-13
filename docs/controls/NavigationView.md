# NavigationView

A WinUI-inspired navigation control with a left navigation pane and a right content area, encapsulating item selection, content switching, and header management into a single reusable control.

## Overview

NavigationView provides the common "sidebar navigation + content area" pattern found in modern desktop applications. It eliminates the manual wiring typically needed for this layout — click handlers, selection state, header updates, and content switching are all handled internally.

The control composes a `HorizontalGridControl` internally with two columns: a fixed-width nav pane and a flexible content area. The content area includes an optional header (title + subtitle) and a `ScrollablePanelControl` for the active section's content.

**Key feature**: NavigationView is gradient-transparent — when placed in a window with a gradient background, the gradient shows through the nav pane and header areas while the content panel can have its own opaque background.

## Properties

### Navigation Pane

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `NavPaneWidth` | `int` | `26` | Width of the left navigation column (minimum 10) |
| `PaneHeader` | `string?` | `null` | Markup text shown as the nav pane header |
| `SelectedItemBackground` | `Color` | `rgb(40,50,80)` | Background color for the selected nav item |
| `SelectedItemForeground` | `Color` | `White` | Foreground color for the selected nav item |
| `ItemForeground` | `Color` | `Grey` | Default foreground color for unselected items |
| `SelectionIndicator` | `char` | `'▸'` | Character used as the selection indicator prefix |

### Content Area

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ContentBorderStyle` | `BorderStyle` | `Rounded` | Border style for the content panel |
| `ContentBorderColor` | `Color?` | `null` | Border color for the content panel |
| `ContentBackgroundColor` | `Color?` | `null` | Background color for the content panel |
| `ContentPadding` | `Padding` | `(1,0,1,0)` | Padding inside the content panel |
| `ShowContentHeader` | `bool` | `true` | Whether to show the title + subtitle header |

### Selection

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SelectedIndex` | `int` | `-1` | Index of the currently selected item |
| `SelectedItem` | `NavigationItem?` | `null` | The currently selected item (read-only) |
| `Items` | `IReadOnlyList<NavigationItem>` | empty | Read-only collection of all items |

### Standard

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BackgroundColor` | `Color` | inherited | Background color (cascades from parent) |
| `ForegroundColor` | `Color` | `White` | Foreground color |
| `ContentPanel` | `ScrollablePanelControl` | - | Direct access to the content panel (read-only) |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `SelectedItemChanging` | `NavigationItemChangingEventArgs` | Raised before selection changes (cancelable) |
| `SelectedItemChanged` | `NavigationItemChangedEventArgs` | Raised after selection has changed |
| `GotFocus` | `EventArgs` | Raised when the control receives focus |
| `LostFocus` | `EventArgs` | Raised when the control loses focus |

### Event Args

**NavigationItemChangedEventArgs:**
- `OldIndex` / `NewIndex` — indices of the old and new selection
- `OldItem` / `NewItem` — the NavigationItem instances

**NavigationItemChangingEventArgs:**
- Same as above, plus `Cancel` — set to `true` to prevent the selection change

## Methods

### Item Management

| Method | Description |
|--------|-------------|
| `AddItem(NavigationItem item)` | Add a navigation item |
| `AddItem(string text, string? icon, string? subtitle)` | Add an item with properties, returns the created NavigationItem |
| `RemoveItem(int index)` | Remove item by index |
| `RemoveItem(NavigationItem item)` | Remove a specific item |
| `ClearItems()` | Remove all items |

### Content Management

| Method | Description |
|--------|-------------|
| `SetItemContent(NavigationItem item, Action<ScrollablePanelControl> populate)` | Register a content factory for an item |
| `SetItemContent(int index, Action<ScrollablePanelControl> populate)` | Register a content factory by index |

## NavigationItem

```csharp
public class NavigationItem
{
    public string Text { get; set; }
    public string? Icon { get; set; }        // emoji/symbol prefix
    public string? Subtitle { get; set; }    // shown in content header
    public object? Tag { get; set; }
    public bool IsEnabled { get; set; } = true;

    // Implicit conversion from string
    NavigationItem item = "Home";
}
```

## Creating NavigationView

### Using Builder (Recommended)

```csharp
var nav = Controls.NavigationView()
    .WithNavWidth(26)
    .WithPaneHeader("[bold white]  ⚙  Settings[/]")
    .WithContentBorder(BorderStyle.Rounded)
    .WithContentBorderColor(Color.Grey37)
    .WithContentBackground(new Color(30, 30, 40))
    .WithContentPadding(1, 0, 1, 0)
    .AddItem("Home", subtitle: "Configure your preferences", content: panel =>
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold cyan]Welcome[/]")
            .AddLine("This is the home section.")
            .Build());
    })
    .AddItem("Settings", subtitle: "General application settings", content: panel =>
    {
        panel.AddControl(Controls.Checkbox("Enable notifications")
            .Checked(true).Build());
        panel.AddControl(Controls.Checkbox("Auto-save on exit")
            .Checked(true).Build());
    })
    .AddItem("About", subtitle: "Application information", content: panel =>
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold]MyApp[/] v1.0")
            .AddLine("[dim]License: MIT[/]")
            .Build());
    })
    .WithAlignment(HorizontalAlignment.Stretch)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .Build();

window.AddControl(nav);
```

### Using Constructor

```csharp
var nav = new NavigationView();
nav.NavPaneWidth = 30;
nav.PaneHeader = "[bold white]  Menu[/]";
nav.ContentBorderStyle = BorderStyle.Rounded;

var homeItem = nav.AddItem("Home", subtitle: "Welcome page");
nav.SetItemContent(homeItem, panel =>
{
    panel.AddControl(Controls.Label("Welcome!"));
});

var settingsItem = nav.AddItem("Settings", subtitle: "App settings");
nav.SetItemContent(settingsItem, panel =>
{
    panel.AddControl(Controls.Checkbox("Dark mode").Build());
});

window.AddControl(nav);
```

## Keyboard & Mouse Support

| Input | Action |
|-------|--------|
| **Mouse Click** | Click a nav item to select it |
| **Tab** | Navigate between controls in the content area |
| **Mouse Wheel** | Scroll within the content panel |
| **Shift+Tab** | Navigate backward through controls |

Navigation between the nav pane and content area uses Tab, handled by the internal `HorizontalGridControl`.

## Architecture

NavigationView internally composes:
- `HorizontalGridControl` — the two-column layout
- `ColumnContainer` (left) — nav pane header + item markup controls
- `ColumnContainer` (right) — content header + scrollable panel
- `MarkupControl` per nav item — with click handlers for selection
- `ScrollablePanelControl` — bordered content area

The control implements `IContainer` and propagates `HasGradientBackground` from its parent, allowing gradient backgrounds to show through transparent areas.

### Content Factory Pattern

Unlike TabControl (which keeps all tab content in the DOM), NavigationView uses **content factories** — delegates that populate the content panel on demand:

```csharp
nav.SetItemContent(item, panel =>
{
    // Called each time this item is selected
    // panel.ClearContents() is called automatically before this
    panel.AddControl(Controls.Label("Fresh content"));
});
```

This means content is rebuilt each time an item is selected. For content that should preserve state across selections, store state externally and restore it in the factory.

## Visual Layout

```
┌────────────────────────┬─────────────────────────────────┐
│  ⚙  Settings           │  Home                           │
│                        │  Configure your preferences     │
│  ▸ Home                │ ╭─────────────────────────────╮ │
│    Settings             │ │                             │ │
│    Appearance           │ │  Welcome                    │ │
│    Privacy              │ │                             │ │
│    About                │ │  This demo showcases a      │ │
│                        │ │  WinUI-inspired layout...   │ │
│                        │ │                             │ │
│                        │ ╰─────────────────────────────╯ │
└────────────────────────┴─────────────────────────────────┘
```

## Examples

### Settings Panel with Gradient Background

```csharp
var gradient = ColorGradient.FromColors(
    new Color(15, 25, 60),
    new Color(5, 5, 15));

var nav = Controls.NavigationView()
    .WithPaneHeader("[bold white]  ⚙  Settings[/]")
    .WithContentBorder(BorderStyle.Rounded)
    .WithContentBorderColor(Color.Grey37)
    .WithContentBackground(new Color(30, 30, 40))
    .AddItem("General", subtitle: "General settings", content: panel =>
    {
        panel.AddControl(Controls.Checkbox("Notifications").Checked(true).Build());
        panel.AddControl(Controls.Checkbox("Auto-update").Build());
    })
    .AddItem("Display", subtitle: "Display settings", content: panel =>
    {
        panel.AddControl(Controls.Markup().AddLine("[bold cyan]Theme[/]").Build());
        panel.AddControl(Controls.Checkbox("Dark mode").Checked(true).Build());
    })
    .WithAlignment(HorizontalAlignment.Stretch)
    .Fill()
    .Build();

var window = new WindowBuilder(ws)
    .WithTitle("Settings")
    .WithSize(80, 30)
    .Centered()
    .WithBackgroundGradient(gradient, GradientDirection.Vertical)
    .AddControl(nav)
    .BuildAndShow();
```

### Cancelable Navigation

```csharp
var nav = Controls.NavigationView()
    .AddItem("Editor", content: panel => { /* ... */ })
    .AddItem("Preview", content: panel => { /* ... */ })
    .OnSelectedItemChanging((sender, e) =>
    {
        if (e.OldItem?.Text == "Editor" && HasUnsavedChanges())
        {
            e.Cancel = true;
            ShowSaveDialog();
        }
    })
    .Build();
```

### Dynamic Items

```csharp
var nav = new NavigationView();
nav.PaneHeader = "[bold]  Projects[/]";

foreach (var project in projects)
{
    var item = nav.AddItem(project.Name, subtitle: project.Description);
    item.Tag = project;
    nav.SetItemContent(item, panel =>
    {
        var p = (Project)item.Tag!;
        panel.AddControl(Controls.Markup()
            .AddLine($"[bold]{p.Name}[/]")
            .AddLine($"[dim]{p.Description}[/]")
            .AddLine($"Status: {p.Status}")
            .Build());
    });
}

window.AddControl(nav);
```

### No Content Header

```csharp
var nav = Controls.NavigationView()
    .WithContentHeader(false)  // Hide the title + subtitle header
    .AddItem("Dashboard", content: panel =>
    {
        panel.AddControl(Controls.Header("Dashboard"));
        // Content manages its own header
    })
    .Build();
```

## Builder Reference

### NavigationViewBuilder Methods

| Category | Method | Description |
|----------|--------|-------------|
| **Items** | `AddItem(text, icon?, subtitle?, content?)` | Add a nav item with optional content factory |
| | `AddItem(NavigationItem, content?)` | Add an existing NavigationItem |
| | `WithSelectedIndex(int)` | Set initially selected item |
| **Nav Pane** | `WithNavWidth(int)` | Set nav pane width |
| | `WithPaneHeader(string)` | Set pane header markup |
| | `WithSelectedColors(fg, bg)` | Set selected item colors |
| | `WithSelectionIndicator(char)` | Set selection indicator character |
| **Content** | `WithContentBorder(BorderStyle)` | Set content panel border |
| | `WithContentBorderColor(Color)` | Set content panel border color |
| | `WithContentBackground(Color)` | Set content panel background |
| | `WithContentPadding(l, t, r, b)` | Set content panel padding |
| | `WithContentHeader(bool)` | Show/hide content header |
| **Events** | `OnSelectedItemChanged(handler)` | Attach changed event handler |
| | `OnSelectedItemChanging(handler)` | Attach changing event handler |
| **Layout** | `WithAlignment(HorizontalAlignment)` | Set horizontal alignment |
| | `WithVerticalAlignment(VerticalAlignment)` | Set vertical alignment |
| | `Fill()` | Fill available vertical space |
| | `WithMargin(l, t, r, b)` | Set margins |
| | `WithWidth(int)` | Set explicit width |
| | `WithName(string)` | Set control name |
| | `WithTag(object)` | Set tag data |

## Comparison with TabControl

| Feature | NavigationView | TabControl |
|---------|---------------|------------|
| **Layout** | Side-by-side (left nav + right content) | Stacked (top header + content below) |
| **Content model** | Content factories (rebuild on select) | Persistent DOM (visibility toggle) |
| **State preservation** | External (rebuild each time) | Automatic (controls stay in tree) |
| **Best for** | Settings panels, app navigation | Document tabs, multi-view editors |
| **Gradient support** | Transparent nav pane | Opaque header |

## Best Practices

1. **Use content factories**: Register content via `SetItemContent` or builder's `content:` parameter — don't add controls directly to the content panel
2. **Set explicit size**: Use `WithAlignment(Stretch)` and `Fill()` for full-area navigation
3. **Keep nav items short**: 1-2 words work best in the nav pane
4. **Use subtitles**: They provide context in the content header when an item is selected
5. **Gradient backgrounds**: NavigationView is gradient-transparent by default — pair with `WithBackgroundGradient` for modern looks
6. **External state**: Since content is rebuilt on each selection, store stateful data (checkbox values, text input) outside the factory and restore it

## See Also

- [TabControl](TabControl.md) — For tabbed multi-page interfaces
- [Fluent Builders](../BUILDERS.md) — Builder API reference
- [Gradients](../GRADIENTS.md) — Background gradient system

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

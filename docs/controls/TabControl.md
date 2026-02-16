# TabControl

Multi-page container with tab headers for organizing content into switchable views.

## Overview

TabControl is a container control that displays multiple pages of content (tabs) with a header bar for switching between them. Only one tab is visible at a time, making it perfect for organizing complex interfaces with multiple sections. Tab switching is supported via mouse clicks on headers and keyboard shortcuts (Ctrl+Tab).

**Important**: TabControl does not automatically provide scrolling. Wrap tab content in `ScrollablePanelBuilder` to enable scrolling for content that exceeds the visible area.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ActiveTabIndex` | `int` | `0` | Index of currently active tab (0-based) |
| `ActiveTab` | `TabPage?` | - | Currently active tab page (read-only) |
| `TabPages` | `IReadOnlyList<TabPage>` | empty | Read-only collection of all tabs |
| `TabCount` | `int` | `0` | Number of tabs in the control (read-only) |
| `HasTabs` | `bool` | `false` | Whether control has any tabs (read-only) |
| `TabTitles` | `IEnumerable<string>` | - | All tab titles (read-only) |
| `Height` | `int?` | `null` | Fixed height (minimum 2: 1 header + 1 content) |
| `Width` | `int?` | `null` | Fixed width (auto-sized if null) |
| `BackgroundColor` | `Color` | `Color.Black` | Background color for control |
| `ForegroundColor` | `Color` | `Color.White` | Foreground color for control |
| `IsEnabled` | `bool` | `true` | Enable/disable tab control |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `TabChanging` | `TabChangingEventArgs` | Raised before tab changes (cancelable) |
| `TabChanged` | `TabChangedEventArgs` | Raised after tab has changed |
| `TabAdded` | `TabEventArgs` | Raised when a tab is added |
| `TabRemoved` | `TabEventArgs` | Raised when a tab is removed |

## Methods

### Tab Management

| Method | Description |
|--------|-------------|
| `AddTab(string title, IWindowControl content)` | Add a new tab |
| `RemoveTab(int index)` | Remove tab by index |
| `RemoveTab(string title)` | Remove first tab with matching title |
| `RemoveTabAt(int index)` | Remove tab by index (alias) |
| `ClearTabs()` | Remove all tabs |
| `InsertTab(int index, string title, IWindowControl content)` | Insert tab at position |

### Navigation

| Method | Description |
|--------|-------------|
| `NextTab()` | Switch to next tab (wraps around) |
| `PreviousTab()` | Switch to previous tab (wraps around) |
| `SwitchToTab(string title)` | Switch to tab by title |

### Query

| Method | Description |
|--------|-------------|
| `FindTab(string title)` | Find tab by title (returns TabPage?) |
| `GetTab(int index)` | Get tab by index (returns TabPage?) |
| `HasTab(string title)` | Check if tab exists |

### Modification

| Method | Description |
|--------|-------------|
| `SetTabTitle(int index, string newTitle)` | Change tab title |
| `SetTabContent(int index, IWindowControl newContent)` | Replace tab content |

## Creating TabControl

### Using Builder (Recommended)

```csharp
var tabControl = Controls.TabControl()
    .AddTab("Overview", new ScrollablePanelBuilder()
        .AddControl(Controls.Markup()
            .AddLine("[bold]Welcome![/]")
            .Build())
        .Build())
    .AddTab("Settings", new ScrollablePanelBuilder()
        .AddControl(settingsContent)
        .Build())
    .WithHeight(20)
    .Build();

window.AddControl(tabControl);
```

### Multiple Controls in a Tab

Use `ScrollablePanelBuilder` with multiple `.AddControl()` calls to stack controls vertically:

```csharp
var interactiveTab = new ScrollablePanelBuilder()
    .AddControl(Controls.Header("Interactive Demo"))
    .AddControl(Controls.Prompt("Name:"))
    .AddControl(Controls.Button("Submit"))
    .Build();

var tabControl = Controls.TabControl()
    .AddTab("Form", interactiveTab)
    .WithHeight(20)
    .Build();
```

### Using Constructor

```csharp
var tabControl = new TabControl();
tabControl.AddTab("Tab 1", content1);
tabControl.AddTab("Tab 2", content2);
tabControl.Height = 15;

window.AddControl(tabControl);
```

## Keyboard Support

| Key | Action |
|-----|--------|
| **Ctrl+Tab** | Switch to next tab (wraps around) |
| **Ctrl+Shift+Tab** | Switch to previous tab (wraps around) |
| **Tab** | Navigate between controls within active tab |
| **Mouse Click** | Click tab header to switch tabs |

## Architecture

TabControl uses a hybrid layout approach:
- All tab content remains in the DOM tree
- Active tab determined by `Visible` property
- Custom `TabLayout` reserves 1 line for headers
- Only active tab is rendered (performance optimization)
- Event routing and state preservation work automatically

## Visual Layout

```
┌─ Overview │ Settings │ Help ────────────────┐
│                                               │
│  Tab content area (only active tab visible)  │
│                                               │
│                                               │
└───────────────────────────────────────────────┘
```

## Examples

### Simple Multi-Tab Interface

```csharp
var tabControl = Controls.TabControl()
    .AddTab("Home", new ScrollablePanelBuilder()
        .AddControl(Controls.Markup()
            .AddLine("[bold cyan]Welcome![/]")
            .AddLine("This is the home tab.")
            .Build())
        .Build())
    .AddTab("About", new ScrollablePanelBuilder()
        .AddControl(Controls.Markup()
            .AddLine("[yellow]About This App[/]")
            .AddLine("Version 1.0")
            .Build())
        .Build())
    .WithHeight(15)
    .Build();

window.AddControl(tabControl);
```

### Tab with Long Scrollable Content

```csharp
var markup = new MarkupBuilder();
for (int i = 1; i <= 50; i++)
{
    markup.AddLine($"[green]Line {i}:[/] Scrollable content");
}

var tabControl = Controls.TabControl()
    .AddTab("Long Content", new ScrollablePanelBuilder()
        .AddControl(markup.Build())
        .WithVerticalScroll()
        .Build())
    .WithHeight(20)
    .Build();
```

### Tab with Interactive Controls and State

```csharp
var clickCount = 0;
var statusLabel = new MarkupBuilder()
    .AddLine("[yellow]Click the button below![/]")
    .Build();

var button = Controls.Button("Click Me!")
    .OnClick((sender, btn) =>
    {
        clickCount++;
        (statusLabel as MarkupControl)?.SetContent(new List<string>
        {
            $"[green]Button clicked {clickCount} time(s)![/]"
        });
    })
    .Build();

var interactiveTab = new ScrollablePanelBuilder()
    .AddControl(Controls.Markup()
        .AddLine("[bold cyan]Interactive Demo[/]")
        .Build())
    .AddControl(new RuleControl { Title = "Try it out" })
    .AddControl(statusLabel)
    .AddControl(button)
    .Build();

var tabControl = Controls.TabControl()
    .AddTab("Interactive", interactiveTab)
    .AddTab("Static", new ScrollablePanelBuilder()
        .AddControl(Controls.Label("Static content"))
        .Build())
    .WithHeight(20)
    .Build();
```

### Tab with Data Table

```csharp
var table = TableControl.Create()
    .AddColumn("ID", Justify.Right, 8)
    .AddColumn("Name", Justify.Left, 20)
    .AddColumn("Status", Justify.Center, 12)
    .AddRow("001", "Alice", "[green]Active[/]")
    .AddRow("002", "Bob", "[yellow]Pending[/]")
    .AddRow("003", "Charlie", "[green]Active[/]")
    .WithTitle("Users")
    .Build();

var dataTab = new ScrollablePanelBuilder()
    .AddControl(Controls.Header("User Data"))
    .AddControl(table)
    .AddControl(Controls.Label("[dim]Scroll for more rows[/]"))
    .Build();

var tabControl = Controls.TabControl()
    .AddTab("Overview", new ScrollablePanelBuilder()
        .AddControl(Controls.Label("Summary"))
        .Build())
    .AddTab("Data", dataTab)
    .WithHeight(20)
    .Build();
```

### Settings Interface

```csharp
var generalSettings = new ScrollablePanelBuilder()
    .AddControl(Controls.Header("General Settings"))
    .AddControl(Controls.Prompt("Username:").WithName("username").Build())
    .AddControl(Controls.Checkbox("Enable notifications"))
    .AddControl(Controls.Checkbox("Auto-save"))
    .Build();

var displaySettings = new ScrollablePanelBuilder()
    .AddControl(Controls.Header("Display Settings"))
    .AddControl(Controls.Checkbox("Dark mode"))
    .AddControl(Controls.Checkbox("Show line numbers"))
    .Build();

var tabControl = Controls.TabControl()
    .AddTab("General", generalSettings)
    .AddTab("Display", displaySettings)
    .AddTab("Advanced", advancedSettings)
    .WithHeight(25)
    .Fill()
    .Build();

window.AddControl(tabControl);
```

### Programmatic Tab Switching

```csharp
var tabControl = Controls.TabControl()
    .AddTab("Step 1", step1Content)
    .AddTab("Step 2", step2Content)
    .AddTab("Step 3", step3Content)
    .WithName("wizard")
    .WithHeight(20)
    .Build();

window.AddControl(tabControl);

// Navigation buttons
window.AddControl(Controls.HorizontalGrid()
    .Column(col => col.Add(
        Controls.Button("Previous")
            .OnClick((s, e, w) =>
            {
                var tabs = w.FindControl<TabControl>("wizard");
                if (tabs != null && tabs.ActiveTabIndex > 0)
                    tabs.ActiveTabIndex--;
            })
            .Build()))
    .Column(col => col.Add(
        Controls.Button("Next")
            .OnClick((s, e, w) =>
            {
                var tabs = w.FindControl<TabControl>("wizard");
                if (tabs != null && tabs.ActiveTabIndex < tabs.TabPages.Count - 1)
                    tabs.ActiveTabIndex++;
            })
            .Build()))
    .Build());
```

### Using Events

```csharp
var tabControl = Controls.TabControl()
    .AddTab("Tab 1", content1)
    .AddTab("Tab 2", content2)
    .Build();

// TabChanged - after tab switches
tabControl.TabChanged += (sender, e) =>
{
    Console.WriteLine($"Switched from {e.OldTab?.Title} to {e.NewTab?.Title}");
};

// TabChanging - before tab switches (cancelable)
tabControl.TabChanging += (sender, e) =>
{
    if (HasUnsavedChanges())
    {
        e.Cancel = true; // Prevent tab switch
        ShowSaveDialog();
    }
};

// Tab collection events
tabControl.TabAdded += (sender, e) =>
    Console.WriteLine($"Added: {e.TabPage.Title} at index {e.Index}");

tabControl.TabRemoved += (sender, e) =>
    Console.WriteLine($"Removed: {e.TabPage.Title}");
```

### Helper Methods - Navigation

```csharp
// Next/Previous with wrapping
tabControl.NextTab(); //Goes to next tab (wraps to first from last)
tabControl.PreviousTab(); // Goes to previous tab (wraps to last from first)

// Switch by title
if (tabControl.SwitchToTab("Settings"))
{
    Console.WriteLine("Switched to Settings");
}

// Access active tab
var currentTab = tabControl.ActiveTab;
Console.WriteLine($"Current: {currentTab?.Title}");
```

### Helper Methods - Tab Management

```csharp
// Remove tabs
tabControl.RemoveTab(0); // By index
tabControl.RemoveTab("Obsolete Tab"); // By title
tabControl.RemoveTabAt(2); // Alias for RemoveTab(int)

// Clear all tabs
tabControl.ClearTabs();

// Insert at position
tabControl.InsertTab(1, "New Tab", newContent);

// Query tabs
if (tabControl.HasTab("Settings"))
{
    var tab = tabControl.FindTab("Settings");
    // Modify tab...
}

var tab = tabControl.GetTab(2); // Safe get by index

// Modify existing tabs
tabControl.SetTabTitle(0, "Overview (Updated)");
tabControl.SetTabContent(1, newContent);

// Use convenience properties
Console.WriteLine($"Total tabs: {tabControl.TabCount}");
Console.WriteLine($"Has tabs: {tabControl.HasTabs}");
foreach (var title in tabControl.TabTitles)
{
    Console.WriteLine($"- {title}");
}
```

### Builder Enhancements - Batch Add

```csharp
var tabControl = Controls.TabControl()
    .AddTabs(
        ("Overview", overviewPanel),
        ("Settings", settingsPanel),
        ("Help", helpPanel)
    )
    .WithHeight(25)
    .Build();
```

### Builder Enhancements - Conditional Tabs

```csharp
var isAdmin = CheckAdminStatus();
var hasDebugMode = Config.DebugEnabled;

var tabControl = Controls.TabControl()
    .AddTab("Home", homeContent)
    .AddTab("Data", dataContent)
    .AddTabIf(isAdmin, "Admin", adminPanel)
    .AddTabIf(hasDebugMode, "Debug", () => CreateDebugPanel())
    .WithHeight(25)
    .Build();
```

### Dynamic Tab Management

```csharp
// Add tabs based on user data
foreach (var project in userProjects)
{
    var panel = CreateProjectPanel(project);
    tabControl.AddTab(project.Name, panel);
}

// Remove tab on user action
window.AddControl(Controls.Button("Close Tab")
    .OnClick((s, e, w) =>
    {
        if (tabControl.TabCount > 1)
        {
            tabControl.RemoveTab(tabControl.ActiveTabIndex);
        }
    }));

// Navigate with buttons
window.AddControl(Controls.HorizontalGrid()
    .Column(col => col.Add(Controls.Button("< Prev")
        .OnClick((s, e, w) => tabControl.PreviousTab())))
    .Column(col => col.Add(Controls.Button("Next >")
        .OnClick((s, e, w) => tabControl.NextTab()))));
```

## Best Practices

1. **Wrap content in ScrollablePanel**: Always use `ScrollablePanelBuilder` for tab content to enable scrolling
2. **Set explicit height**: Use `WithHeight()` for consistent tab sizing across all tabs
3. **Use descriptive tab titles**: Keep titles short (1-2 words) for better header layout
4. **Group related content**: Organize logically related sections into tabs
5. **Multiple controls per tab**: Use `ScrollablePanelBuilder` with multiple `.AddControl()` calls
6. **Limit tab count**: 3-7 tabs is optimal; more than 10 becomes hard to navigate

## Common Patterns

### Wizard/Multi-Step Form

```csharp
var step1 = new ScrollablePanelBuilder()
    .AddControl(Controls.Header("Step 1: Basic Info"))
    .AddControl(Controls.Prompt("Name:"))
    .AddControl(Controls.Prompt("Email:"))
    .Build();

var step2 = new ScrollablePanelBuilder()
    .AddControl(Controls.Header("Step 2: Preferences"))
    .AddControl(Controls.Checkbox("Subscribe to newsletter"))
    .Build();

var wizard = Controls.TabControl()
    .AddTab("Step 1", step1)
    .AddTab("Step 2", step2)
    .AddTab("Step 3", step3)
    .WithActiveTab(0)
    .WithHeight(25)
    .Build();
```

### Application with Multiple Views

```csharp
var dashboard = new ScrollablePanelBuilder()
    .AddControl(CreateDashboardWidgets())
    .Build();

var dataView = new ScrollablePanelBuilder()
    .AddControl(CreateDataTable())
    .Build();

var mainTabs = Controls.TabControl()
    .AddTab("Dashboard", dashboard)
    .AddTab("Data", dataView)
    .AddTab("Reports", reportsView)
    .AddTab("Settings", settingsView)
    .WithHeight(30)
    .Fill()
    .Build();
```

### Help System

```csharp
var overview = new ScrollablePanelBuilder()
    .AddControl(Controls.Markup()
        .AddLine("[bold cyan]Help Overview[/]")
        .AddLine("")
        .AddLine("Welcome to the help system...")
        .Build())
    .Build();

var helpTabs = Controls.TabControl()
    .AddTab("Overview", overview)
    .AddTab("Getting Started", gettingStarted)
    .AddTab("Keyboard Shortcuts", keyboardHelp)
    .AddTab("FAQ", faqContent)
    .WithHeight(25)
    .Build();
```

## Technical Details

### Layout System Integration

TabControl uses a custom `TabLayout` class that implements `ILayoutContainer`:
- Reserves 1 line at Y=0 for tab headers
- Positions active tab content below headers
- Collapses inactive tabs to (0,0,0,0) bounds
- Tab content can be any `IWindowControl` (ScrollablePanel recommended)

### Rendering Pipeline

1. **Measure Phase**: TabLayout measures all children, returns max size + header height
2. **Arrange Phase**: Active tab positioned below header, inactive tabs collapsed
3. **Paint Phase**: TabControl paints headers, layout system paints active tab content

### Event Routing

- Mouse clicks on headers (Y=0) handled by TabControl
- Mouse/keyboard events in content area routed to active tab's children
- Focus navigation works automatically through standard control tree

### State Preservation

All tab content controls remain in the DOM tree with their state intact. Only the `Visible` property changes when switching tabs, ensuring:
- Form input values preserved
- Scroll positions maintained (within ScrollablePanel)
- Event handlers remain attached
- Child control state unchanged

## Performance Considerations

- **Efficient rendering**: Only active tab content is painted
- **Fast switching**: Visibility toggle is instantaneous
- **Memory**: All tabs remain in memory (consider lazy loading for many tabs)
- **Measurement**: Inactive tabs still measured (minimal performance impact)

## See Also

- [ScrollablePanelControl](ScrollablePanelControl.md) - Recommended container for tab content
- [HorizontalGridControl](HorizontalGridControl.md) - For multi-column layouts
- [ToolbarControl](ToolbarControl.md) - For tab-like button bars
- [MarkupControl](MarkupControl.md) - For formatted text in tabs

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

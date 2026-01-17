# ListControl

Scrollable list control with single selection and keyboard/mouse navigation.

## Overview

ListControl displays a scrollable list of items with selection support. Users can navigate with keyboard or mouse, select items, and activate items (double-click or Enter).

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Items` | `ObservableCollection<ListItem>` | Empty | List of items to display |
| `SelectedIndex` | `int` | `-1` | Index of selected item (-1 = none) |
| `SelectedItem` | `ListItem?` | `null` | Currently selected item |
| `ViewportHeight` | `int` | `10` | Number of visible items |
| `IsEnabled` | `bool` | `true` | Enable/disable list |
| `BackgroundColor` | `Color?` | `null` | Background color (uses theme if null) |
| `ForegroundColor` | `Color?` | `null` | Text color (uses theme if null) |
| `HasFocus` | `bool` | `false` | Whether list has keyboard focus |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `SelectedIndexChanged` | `EventHandler<int>` | Fired when selection index changes |
| `SelectedItemChanged` | `EventHandler<ListItem?>` | Fired when selected item changes |
| `ItemActivated` | `EventHandler<ListItem>` | Fired when item is double-clicked or Enter pressed |
| `GotFocus` | `EventHandler` | Fired when list receives focus |
| `LostFocus` | `EventHandler` | Fired when list loses focus |

## Creating Lists

### Using Builder (Recommended)

```csharp
var list = Controls.List()
    .AddItem("Apple")
    .AddItem("Banana")
    .AddItem("Cherry")
    .AddItem("Date")
    .AddItem("Elderberry")
    .WithHeight(10)
    .WithColors(Color.Grey15, Color.White)
    .WithName("fruitList")
    .OnItemActivated((sender, item, window) =>
    {
        windowSystem.NotificationStateService.ShowNotification(
            "Item Activated",
            $"You selected: {item.Text}",
            NotificationSeverity.Info
        );
    })
    .OnSelectionChanged((sender, index, window) =>
    {
        var status = window.FindControl<MarkupControl>("status");
        if (index >= 0)
        {
            status?.SetContent($"[dim]Selected: {sender.SelectedItem?.Text}[/]");
        }
    })
    .Build();

window.AddControl(list);
```

### Using Constructor

```csharp
var list = new ListControl
{
    ViewportHeight = 10,
    BackgroundColor = Color.Grey15,
    ForegroundColor = Color.White
};

list.Items.Add(new ListItem("Item 1"));
list.Items.Add(new ListItem("Item 2"));
list.Items.Add(new ListItem("Item 3"));

list.ItemActivated += (sender, item) =>
{
    // Handle activation
};

list.SelectedIndexChanged += (sender, index) =>
{
    // Handle selection change
};

window.AddControl(list);
```

## Keyboard Support

| Key | Action |
|-----|--------|
| **Up Arrow** | Move selection up |
| **Down Arrow** | Move selection down |
| **Page Up** | Move selection up by viewport height |
| **Page Down** | Move selection down by viewport height |
| **Home** | Select first item |
| **End** | Select last item |
| **Enter** | Activate selected item (fire ItemActivated) |
| **Tab** | Move focus to next control |
| **Shift+Tab** | Move focus to previous control |

## Mouse Support

| Action | Result |
|--------|--------|
| **Left Click** | Select item and give focus |
| **Double Click** | Activate item (fire ItemActivated) |
| **Scroll Wheel** | Scroll list up/down |

## ListItem

Items in the list are `ListItem` objects:

```csharp
public class ListItem
{
    public string Text { get; set; }
    public object? Tag { get; set; }  // Store custom data
}
```

## Examples

### Simple List

```csharp
var list = Controls.List()
    .AddItem("Option 1")
    .AddItem("Option 2")
    .AddItem("Option 3")
    .WithHeight(5)
    .OnItemActivated((s, item, w) =>
    {
        windowSystem.NotificationStateService.ShowNotification(
            "Selected", item.Text, NotificationSeverity.Success);
    })
    .Build();

window.AddControl(list);
```

### List with Custom Data

```csharp
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}

var people = new[]
{
    new Person { Name = "Alice", Age = 30 },
    new Person { Name = "Bob", Age = 25 },
    new Person { Name = "Charlie", Age = 35 }
};

var list = new ListControl { ViewportHeight = 10 };

foreach (var person in people)
{
    list.Items.Add(new ListItem(person.Name)
    {
        Tag = person  // Store custom data
    });
}

list.ItemActivated += (sender, item) =>
{
    var person = item.Tag as Person;
    if (person != null)
    {
        windowSystem.NotificationStateService.ShowNotification(
            "Person Details",
            $"{person.Name} is {person.Age} years old",
            NotificationSeverity.Info
        );
    }
};

window.AddControl(list);
```

### Master-Detail Pattern

```csharp
var list = Controls.List()
    .AddItem("Item 1")
    .AddItem("Item 2")
    .AddItem("Item 3")
    .WithName("masterList")
    .OnSelectionChanged((sender, index, window) =>
    {
        var detail = window.FindControl<MarkupControl>("detailView");
        if (index >= 0)
        {
            var item = sender.SelectedItem;
            detail?.SetContent(new List<string>
            {
                $"[bold yellow]{item?.Text}[/]",
                "",
                "Detailed information about this item...",
                $"Selected index: {index}"
            });
        }
    })
    .Build();

window.AddControl(list);

window.AddControl(
    Controls.Markup()
        .AddLine("[dim]Select an item to see details[/]")
        .WithName("detailView")
        .Build()
);
```

### Searchable List

```csharp
var allItems = new[] { "Apple", "Apricot", "Banana", "Blueberry", "Cherry", "Date" };

var list = Controls.List()
    .WithHeight(10)
    .WithName("searchableList")
    .Build();

// Initialize with all items
foreach (var item in allItems)
{
    list.Items.Add(new ListItem(item));
}

// Add search box
window.AddControl(
    Controls.Prompt("Search:")
        .OnInputChanged((sender, text, window) =>
        {
            var list = window.FindControl<ListControl>("searchableList");
            if (list != null)
            {
                list.Items.Clear();

                var filtered = string.IsNullOrWhiteSpace(text)
                    ? allItems
                    : allItems.Where(i => i.Contains(text, StringComparison.OrdinalIgnoreCase));

                foreach (var item in filtered)
                {
                    list.Items.Add(new ListItem(item));
                }

                list.Invalidate();
            }
        })
        .Build()
);

window.AddControl(list);
```

### Dynamic List Updates

```csharp
var list = Controls.List()
    .WithName("dynamicList")
    .WithHeight(10)
    .Build();

window.AddControl(list);

// Add button to add items
window.AddControl(
    Controls.Button("Add Item")
        .OnClick((s, e, w) =>
        {
            var list = w.FindControl<ListControl>("dynamicList");
            if (list != null)
            {
                var count = list.Items.Count + 1;
                list.Items.Add(new ListItem($"Item {count}"));
                list.Invalidate();
            }
        })
        .Build()
);

// Add button to remove selected
window.AddControl(
    Controls.Button("Remove Selected")
        .OnClick((s, e, w) =>
        {
            var list = w.FindControl<ListControl>("dynamicList");
            if (list != null && list.SelectedIndex >= 0)
            {
                list.Items.RemoveAt(list.SelectedIndex);
                list.Invalidate();
            }
        })
        .Build()
);
```

### Formatted List Items

```csharp
var list = Controls.List()
    .AddItem("[green]Available[/] - Service Running")
    .AddItem("[yellow]Warning[/] - High CPU Usage")
    .AddItem("[red]Error[/] - Service Stopped")
    .AddItem("[blue]Info[/] - Update Available")
    .WithHeight(10)
    .Build();

window.AddControl(list);
```

### File Browser

```csharp
public void ShowDirectoryContents(string path, ListControl list)
{
    list.Items.Clear();

    try
    {
        // Add parent directory option
        if (Directory.GetParent(path) != null)
        {
            list.Items.Add(new ListItem("[..] Parent Directory")
            {
                Tag = Directory.GetParent(path)?.FullName
            });
        }

        // Add directories
        foreach (var dir in Directory.GetDirectories(path))
        {
            var name = Path.GetFileName(dir);
            list.Items.Add(new ListItem($"[DIR] {name}")
            {
                Tag = dir
            });
        }

        // Add files
        foreach (var file in Directory.GetFiles(path))
        {
            var name = Path.GetFileName(file);
            list.Items.Add(new ListItem($"      {name}")
            {
                Tag = file
            });
        }
    }
    catch (Exception ex)
    {
        windowSystem.NotificationStateService.ShowNotification(
            "Error", ex.Message, NotificationSeverity.Danger);
    }

    list.Invalidate();
}

var list = Controls.List()
    .WithHeight(15)
    .OnItemActivated((sender, item, window) =>
    {
        var path = item.Tag as string;
        if (path != null && Directory.Exists(path))
        {
            ShowDirectoryContents(path, sender);
        }
    })
    .Build();

ShowDirectoryContents(Environment.CurrentDirectory, list);
window.AddControl(list);
```

## Helper Methods

### Select Item Programmatically

```csharp
var list = window.FindControl<ListControl>("myList");
if (list != null)
{
    list.SelectedIndex = 0;  // Select first item
    list.Invalidate();
}
```

### Clear Selection

```csharp
var list = window.FindControl<ListControl>("myList");
if (list != null)
{
    list.SelectedIndex = -1;  // No selection
    list.Invalidate();
}
```

### Get Selected Item

```csharp
var list = window.FindControl<ListControl>("myList");
if (list != null && list.SelectedItem != null)
{
    string text = list.SelectedItem.Text;
    object? data = list.SelectedItem.Tag;
}
```

## Best Practices

1. **Set appropriate height**: Use WithHeight() to control visible items
2. **Use Tag for data**: Store custom objects in ListItem.Tag
3. **Handle ItemActivated**: Respond to double-click/Enter
4. **Update on selection**: Use OnSelectionChanged for master-detail patterns
5. **Clear and rebuild**: For filtering, clear Items and rebuild list
6. **Call Invalidate**: After modifying Items collection
7. **Check selection**: Always check SelectedIndex >= 0 before accessing SelectedItem

## Common Patterns

### Action on Selection

```csharp
var list = Controls.List()
    .AddItem("View Details")
    .AddItem("Edit Item")
    .AddItem("Delete Item")
    .OnItemActivated((s, item, w) =>
    {
        switch (item.Text)
        {
            case "View Details":
                ShowDetailsDialog();
                break;
            case "Edit Item":
                ShowEditDialog();
                break;
            case "Delete Item":
                ConfirmDelete();
                break;
        }
    })
    .Build();
```

### List with Status Bar

```csharp
var list = Controls.List()
    .WithName("itemList")
    .WithHeight(10)
    .OnSelectionChanged((s, index, w) =>
    {
        var status = w.FindControl<MarkupControl>("statusBar");
        status?.SetContent($"Item {index + 1} of {s.Items.Count}");
    })
    .Build();

window.AddControl(list);
window.AddControl(
    Controls.Label("No selection")
        .WithName("statusBar")
        .StickyBottom()
        .Build()
);
```

## See Also

- [TreeControl](TreeControl.md) - For hierarchical data
- [DropdownControl](DropdownControl.md) - For compact selection
- [MenuControl](MenuControl.md) - For menu-based navigation

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

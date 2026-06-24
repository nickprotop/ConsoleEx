# TreeControl

Hierarchical tree control that displays nodes in a collapsible structure with keyboard and mouse navigation.

## Overview

TreeControl displays data as an expandable, collapsible tree of `TreeNode` objects. Each node has text, an optional color, an optional `Tag` for custom data, and a list of child nodes. Parent nodes can be expanded or collapsed; leaf nodes can be activated.

The control flattens visible nodes into a scrollable list and supports a vertical scrollbar, mouse-wheel scrolling, hover highlighting, and configurable guide-line styles (`Line`, `Ascii`, `DoubleLine`, `BoldLine`). Tree structure can be built up front via the builder or modified at runtime through `AddRootNode`, `RemoveRootNode`, `TreeNode.AddChild`, and related methods.

Three primary events report interaction: `SelectedNodeChanged` (selection moved), `NodeExpandCollapse` (a parent toggled), and `NodeActivated` (a leaf was activated via Enter/Space or double-click). Each has an async counterpart. The control supports lazy loading by adding placeholder children and populating them in the `NodeExpandCollapse` handler.

See also: [ListControl](ListControl.md)

## Quick Start

```csharp
var tree = Controls.Tree()
    .WithGuide(TreeGuide.Line)
    .WithName("fileTree")
    .OnSelectedNodeChanged((sender, args, window) =>
    {
        if (args.Node != null)
        {
            var status = window.FindControl<MarkupControl>("status");
            status?.SetContent($"Selected: {args.Node.Text}");
        }
    })
    .Build();

var root = tree.AddRootNode("Project");
var src = root.AddChild("src");
src.AddChild("Program.cs");
src.AddChild("Window.cs");
root.AddChild("README.md");

window.AddControl(tree);
```

## Builder API

Create a builder with `Controls.Tree()`.

### Nodes

```csharp
.AddRootNode(TreeNode node)              // Add an existing node (returns builder)
.AddRootNode("text")                     // Create + add a node (returns the TreeNode)
.AddRootNodes(params TreeNode[] nodes)   // Add multiple nodes
.AddRootNodes(IEnumerable<TreeNode> nodes)
```

### Appearance

```csharp
.WithGuide(TreeGuide.Line)               // Guide line style
.WithIndent("  ")                        // Indent string per level
.WithColors(foreground, background)
.WithBackgroundColor(color)
.WithForegroundColor(color)
.WithHighlightColors(foreground, background)  // Selected/highlighted item colors
.WithScrollbarVisibility(ScrollbarVisibility.Auto)
```

### Layout

```csharp
.WithMaxVisibleItems(count)              // Cap visible rows
.WithWidth(width)
.WithHeight(height)
.WithAlignment(HorizontalAlignment.Left)
.Centered()
.WithVerticalAlignment(VerticalAlignment.Top)
.WithMargin(left, top, right, bottom)
.WithMargin(uniform)
.WithStickyPosition(StickyPosition.None)
```

### State and Identity

```csharp
.Visible(true)
.Enabled(true)
.Disabled()
.WithName("treeName")                    // For window.FindControl lookup
.WithTag(object)
```

### Events

```csharp
.OnSelectedNodeChanged((sender, args) => { ... })
.OnSelectedNodeChanged((sender, args, window) => { ... })
.OnNodeExpandCollapse((sender, args) => { ... })
.OnNodeExpandCollapse((sender, args, window) => { ... })
.OnGotFocus((sender, e) => { ... })
.OnGotFocus((sender, e, window) => { ... })
.OnLostFocus((sender, e) => { ... })
.OnLostFocus((sender, e, window) => { ... })
```

> Note: the builder wires up `SelectedNodeChanged`, `NodeExpandCollapse`, `GotFocus`, and `LostFocus`. To handle `NodeActivated`, subscribe to the event on the built control directly.

## Properties

### TreeControl

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RootNodes` | `ReadOnlyCollection<TreeNode>` | Empty | Snapshot of the root nodes (read-only) |
| `SelectedIndex` | `int` | `0` | Index of the selected node in the flattened list |
| `SelectedNode` | `TreeNode?` | `null` | Currently selected node (null if none) |
| `Guide` | `TreeGuide` | `Line` | Guide line style (`Line`, `Ascii`, `DoubleLine`, `BoldLine`) |
| `Indent` | `string` | `"  "` | Indent string applied per level |
| `MaxVisibleItems` | `int?` | `null` | Max rows shown at once (fits available height if null) |
| `Height` | `int?` | `null` | Explicit control height (content-based if null) |
| `BackgroundColor` | `Color?` | `null` | Background color (uses theme if null) |
| `ForegroundColor` | `Color` | Theme / `White` | Text color |
| `HighlightBackgroundColor` | `Color?` | `null` | Background for the highlighted/selected item |
| `HighlightForegroundColor` | `Color` | `White` | Foreground for the highlighted/selected item |
| `ScrollbarVisibility` | `ScrollbarVisibility` | `Auto` | Scrollbar display mode (`Auto`, `Always`, `Never`) |
| `HoverEnabled` | `bool` | `true` | Enable mouse hover highlighting |
| `SelectOnRightClick` | `bool` | `false` | Select the node under the cursor before `MouseRightClick` fires |
| `LastRightClickedNode` | `TreeNode?` | `null` | Node under the cursor at the most recent right-click |
| `IsEnabled` | `bool` | `true` | Enable/disable interaction |
| `HasFocus` | `bool` | `false` | Whether the tree has keyboard focus |
| `CanReceiveFocus` | `bool` | `IsEnabled` | Whether the tree can receive focus |
| `ContentWidth` | `int?` | computed | Rendered width in characters |

### TreeNode

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | (constructor) | Node label (supports markup) |
| `Children` | `ReadOnlyCollection<TreeNode>` | Empty | Child nodes (read-only view) |
| `IsExpanded` | `bool` | `true` | Whether children are shown |
| `Tag` | `object?` | `null` | Custom data associated with the node |
| `TextColor` | `Color?` | `null` | Optional per-node text color |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `SelectedNodeChanged` | `EventHandler<TreeNodeEventArgs>` | Fired when the selected node changes |
| `SelectedNodeChangedAsync` | `AsyncEventHandler<TreeNodeEventArgs>` | Async counterpart of `SelectedNodeChanged` |
| `NodeExpandCollapse` | `EventHandler<TreeNodeEventArgs>` | Fired when a node is expanded or collapsed |
| `NodeExpandCollapseAsync` | `AsyncEventHandler<TreeNodeEventArgs>` | Async counterpart of `NodeExpandCollapse` |
| `NodeActivated` | `EventHandler<TreeNodeEventArgs>` | Fired when a leaf node is activated (Enter/Space or double-click) |
| `NodeActivatedAsync` | `AsyncEventHandler<TreeNodeEventArgs>` | Async counterpart of `NodeActivated` |
| `MouseClick` | `EventHandler<MouseEventArgs>` | Fired on a single left-click |
| `MouseDoubleClick` | `EventHandler<MouseEventArgs>` | Fired on a double-click |
| `MouseRightClick` | `EventHandler<MouseEventArgs>` | Fired on a right-click |
| `MouseLeave` | `EventHandler<MouseEventArgs>` | Fired when the mouse leaves the control |
| `MouseMove` | `EventHandler<MouseEventArgs>` | Fired when the mouse moves over the control |
| `GotFocus` | `EventHandler` | Fired when the tree receives focus |
| `LostFocus` | `EventHandler` | Fired when the tree loses focus |

`TreeNodeEventArgs` exposes a single `Node` property (`TreeNode?`) identifying the affected node.

## Keyboard Support

| Key | Action |
|-----|--------|
| **Up Arrow** | Move selection up |
| **Down Arrow** | Move selection down |
| **Page Up** | Move selection up by one page |
| **Page Down** | Move selection down by one page |
| **Home** | Select first node |
| **End** | Select last node |
| **Right Arrow** | Expand the selected node (if collapsed) |
| **Left Arrow** | Collapse the selected node (if expanded with children) |
| **Enter** | Toggle expand/collapse on a parent; activate a leaf node |
| **Space** | Toggle expand/collapse on a parent; activate a leaf node |

> Keys combined with Shift, Alt, or Control are ignored, allowing focus navigation (Tab/Shift+Tab) to pass through.

## Mouse Support

| Action | Result |
|--------|--------|
| **Left Click (node)** | Select node and give focus |
| **Left Click (expand indicator)** | Toggle expand/collapse |
| **Double Click (parent)** | Toggle expand/collapse |
| **Double Click (leaf)** | Activate node (fire `NodeActivated`) |
| **Right Click** | Fire `MouseRightClick`; sets `LastRightClickedNode` (selects node if `SelectOnRightClick` is true) |
| **Scroll Wheel** | Scroll the tree up/down |
| **Hover** | Highlight node under cursor (when `HoverEnabled`) |
| **Scrollbar drag / arrows / track** | Scroll the tree |

## Methods

### TreeControl

| Method | Description |
|--------|-------------|
| `AddRootNode(TreeNode)` | Adds an existing root node |
| `AddRootNode(string)` | Creates and adds a root node, returning it |
| `RemoveRootNode(TreeNode)` | Removes a root node; returns true if found |
| `Clear()` | Removes all nodes |
| `ExpandAll()` | Expands every node |
| `CollapseAll()` | Collapses every node |
| `SelectNode(TreeNode)` | Selects a node, expanding parents if needed; returns true if found |
| `EnsureNodeVisible(TreeNode)` | Expands all ancestors so the node is visible; returns true if found |
| `FindNodeByTag(object)` | Returns the first node with a matching `Tag`, or null |
| `FindNodeByText(string, TreeNode? searchRoot = null)` | Returns the first node with matching text, or null |
| `PulseNode(TreeNode, Color, int pulseCount, TimeSpan pulseDuration)` | Animates a node's text color as a pulse; returns the animation or null |

### TreeNode

| Method | Description |
|--------|-------------|
| `AddChild(TreeNode)` | Adds a child node, returning it |
| `AddChild(string)` | Creates and adds a child node, returning it |
| `RemoveChild(TreeNode)` | Removes a child; returns true if found |
| `ClearChildren()` | Removes all child nodes |

## Examples

### Simple Tree

```csharp
var tree = Controls.Tree()
    .WithName("tree")
    .Build();

var fruits = tree.AddRootNode("Fruits");
fruits.AddChild("Apple");
fruits.AddChild("Banana");

var veggies = tree.AddRootNode("Vegetables");
veggies.AddChild("Carrot");
veggies.AddChild("Potato");

window.AddControl(tree);
```

### Nodes with Custom Data (Tag)

```csharp
var tree = Controls.Tree()
    .WithName("tree")
    .Build();

foreach (var person in people)
{
    var node = tree.AddRootNode(person.Name);
    node.Tag = person;  // Store the domain object
}

tree.SelectedNodeChanged += (sender, args) =>
{
    if (args.Node?.Tag is Person p)
    {
        windowSystem.NotificationStateService.ShowNotification(
            "Selected", $"{p.Name} ({p.Age})", NotificationSeverity.Info);
    }
};

window.AddControl(tree);
```

### Activating Leaf Nodes

```csharp
var tree = Controls.Tree()
    .WithName("menuTree")
    .Build();

var actions = tree.AddRootNode("Actions");
actions.AddChild("Open");
actions.AddChild("Save");
actions.AddChild("Close");

// NodeActivated is not exposed on the builder — subscribe directly
tree.NodeActivated += (sender, args) =>
{
    switch (args.Node?.Text)
    {
        case "Open": OpenFile(); break;
        case "Save": SaveFile(); break;
        case "Close": window.Close(); break;
    }
};

window.AddControl(tree);
```

### Lazy-Loading Directory Tree

```csharp
const string Placeholder = "...";

var tree = Controls.Tree()
    .WithGuide(TreeGuide.Line)
    .WithName("dirTree")
    .Build();

void AddChildren(TreeNode parent, string dirPath)
{
    foreach (var dir in Directory.GetDirectories(dirPath))
    {
        var node = parent.AddChild($"[cyan]{Path.GetFileName(dir)}[/]");
        node.Tag = dir;
        node.IsExpanded = false;
        // Add a placeholder so the node shows as expandable
        node.AddChild(Placeholder);
    }
}

var root = tree.AddRootNode("[cyan]Root[/]");
root.Tag = rootPath;
AddChildren(root, rootPath);

// Populate children only when the node is expanded
tree.NodeExpandCollapse += (sender, args) =>
{
    if (args.Node is { IsExpanded: true, Tag: string path })
    {
        if (args.Node.Children.Count == 1 && args.Node.Children[0].Text == Placeholder)
        {
            args.Node.ClearChildren();
            AddChildren(args.Node, path);
        }
    }
};

window.AddControl(tree);
```

### Master-Detail with Selection

```csharp
var tree = Controls.Tree()
    .WithName("nav")
    .OnSelectedNodeChanged((sender, args, window) =>
    {
        var detail = window.FindControl<MarkupControl>("detail");
        detail?.SetContent($"[bold]{args.Node?.Text}[/]");
    })
    .Build();

window.AddControl(tree);
window.AddControl(
    Controls.Markup()
        .AddLine("[dim]Select a node[/]")
        .WithName("detail")
        .Build()
);
```

### Styling and Highlight Colors

```csharp
var tree = Controls.Tree()
    .WithGuide(TreeGuide.BoldLine)
    .WithColors(Color.White, Color.Grey15)
    .WithHighlightColors(Color.Black, Color.Aqua)
    .WithMaxVisibleItems(15)
    .WithScrollbarVisibility(ScrollbarVisibility.Auto)
    .Build();

var root = tree.AddRootNode("Settings");
root.TextColor = Color.Yellow;  // Per-node color
root.AddChild("Display");
root.AddChild("Audio");

window.AddControl(tree);
```

## Best Practices

1. **Use Tag for data**: Store domain objects in `TreeNode.Tag` and read them in event handlers via `args.Node.Tag`.
2. **Lazy-load large trees**: Add a placeholder child and populate real children in `NodeExpandCollapse` to avoid building the whole tree up front.
3. **Subscribe to NodeActivated directly**: It is not exposed on the builder; attach it to the built control for leaf activation.
4. **Choose a guide style**: Use `TreeGuide.Ascii` for terminals with limited box-drawing support.
5. **Cap visible rows**: Use `WithMaxVisibleItems` so the tree fits its layout and gets a scrollbar.
6. **Marshal background updates**: When modifying nodes from background threads, use `windowSystem.EnqueueOnUIThread` for UI state and `Container?.Invalidate(Invalidation.Relayout)` to refresh (safe to call directly from a background thread).

## See Also

- [ListControl](ListControl.md) - For flat, single-selection lists
- [DropdownControl](DropdownControl.md) - For compact selection
- [MenuControl](MenuControl.md) - For menu-based navigation
- [NavigationView](NavigationView.md) - For sidebar navigation

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

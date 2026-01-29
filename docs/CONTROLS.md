# Controls Reference

SharpConsoleUI provides 25+ built-in UI controls for building rich console applications.

## Table of Contents

- [Overview](#overview)
- [Basic Input Controls](#basic-input-controls)
- [Selection Controls](#selection-controls)
- [Display Controls](#display-controls)
- [Layout Controls](#layout-controls)
- [Utility Controls](#utility-controls)
- [Interfaces](#interfaces)

## Overview

All controls implement the `IWindowControl` interface and can be added to windows. Interactive controls implement additional interfaces like `IInteractiveControl`, `IFocusableControl`, and `IMouseAwareControl`.

### Common Control Properties

All controls support:
- `Name` - Unique identifier for `FindControl<T>(name)` lookups
- `Visible` - Show/hide control
- `Container` - Reference to parent container
- `Tag` - Store custom data
- Layout properties (Width, Margin, Alignment, StickyPosition)

## Basic Input Controls

Controls for user input and interaction.

| Control | Description | Details |
|---------|-------------|---------|
| **[ButtonControl](controls/ButtonControl.md)** | Clickable button with text | Click events, keyboard/mouse support |
| **[CheckboxControl](controls/CheckboxControl.md)** | Toggle checkbox with label | Checked/unchecked state, change events |
| **[PromptControl](controls/PromptControl.md)** | Single-line text input | Enter key events, input validation, max length |
| **[MultilineEditControl](controls/MultilineEditControl.md)** | Multi-line text editor | Scrolling, word wrap, text selection |

## Selection Controls

Controls for selecting items from lists or hierarchies.

| Control | Description | Details |
|---------|-------------|---------|
| **[ListControl](controls/ListControl.md)** | Scrollable list with selection | Single selection, item activation, keyboard navigation |
| **[TreeControl](controls/TreeControl.md)** | Hierarchical tree view | Expand/collapse nodes, selection, keyboard navigation |
| **[DropdownControl](controls/DropdownControl.md)** | Dropdown selection list | Click to expand, keyboard navigation, portal rendering |
| **[MenuControl](controls/MenuControl.md)** | Menu bar with dropdowns | Horizontal/vertical menus, submenus, separators, keyboard shortcuts |

## Display Controls

Controls for displaying formatted content.

| Control | Description | Details |
|---------|-------------|---------|
| **[MarkupControl](controls/MarkupControl.md)** | Rich text with Spectre markup | Colors, bold, italic, links using `[markup]` syntax |
| **[FigleControl](controls/FigleControl.md)** | ASCII art text (Figlet) | Large stylized text, multiple fonts |
| **[LogViewerControl](controls/LogViewerControl.md)** | Log message viewer | Auto-scroll, filtering, severity colors |
| **[SpectreRenderableControl](controls/SpectreRenderableControl.md)** | Wrapper for Spectre widgets | Display Tables, Trees, Panels, Charts, etc. |
| **PanelControl** | Bordered content panel | Headers, multiple border styles, padding, mouse support |
| **[RuleControl](controls/RuleControl.md)** | Horizontal rule/separator | Optional title, colors, horizontal line |
| **SparklineControl** | Time-series sparkline graph | Block, braille, or bidirectional modes; borders; titles |
| **BarGraphControl** | Horizontal bar graph | Gradient color thresholds, labels, value display |

## Layout Controls

Controls for organizing other controls.

| Control | Description | Details |
|---------|-------------|---------|
| **[ColumnContainer](controls/ColumnContainer.md)** | Vertical stack container | Stack controls vertically, padding, alignment |
| **[ScrollablePanelControl](controls/ScrollablePanelControl.md)** | Scrollable content area | Vertical scrolling, contains multiple controls |
| **[HorizontalGridControl](controls/HorizontalGridControl.md)** | Multi-column layout | Variable-width columns, alignment, splitters |
| **[SplitterControl](controls/SplitterControl.md)** | Resizable divider | Drag to resize adjacent columns |
| **[ToolbarControl](controls/ToolbarControl.md)** | Horizontal button toolbar | Quick access buttons, separators |
| **[SeparatorControl](controls/SeparatorControl.md)** | Visual separator | Simple horizontal line |

## Utility Controls

Special-purpose controls.

| Control | Description | Details |
|---------|-------------|---------|
| **RuleControl** | Titled horizontal rule | Section dividers with optional title |
| **SeparatorControl** | Plain horizontal line | Visual spacing |

## Interfaces

Controls implement these interfaces based on their capabilities:

### IWindowControl (Base Interface)

All controls implement this interface:

```csharp
public interface IWindowControl : IDisposable
{
    IContainer? Container { get; set; }
    string? Name { get; set; }
    object? Tag { get; set; }
    bool Visible { get; set; }

    void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect);
    Size MeasureDOM(int availableWidth);
    void Invalidate(bool recursive = false);
}
```

### IInteractiveControl

Controls that handle keyboard input:

```csharp
public interface IInteractiveControl : IWindowControl
{
    bool IsEnabled { get; set; }
    bool ProcessKey(KeyEventArgs e);
}
```

Implemented by: Button, Checkbox, Prompt, MultilineEdit, List, Tree, Dropdown, Menu

### IFocusableControl

Controls that can receive keyboard focus:

```csharp
public interface IFocusableControl : IInteractiveControl
{
    bool HasFocus { get; }
    bool CanReceiveFocus { get; }
    event EventHandler? GotFocus;
    event EventHandler? LostFocus;
    void SetFocus();
}
```

Implemented by: Button, Checkbox, Prompt, MultilineEdit, List, Tree, Dropdown

### IMouseAwareControl

Controls that handle mouse events:

```csharp
public interface IMouseAwareControl : IWindowControl
{
    bool WantsMouseEvents { get; }
    bool CanFocusWithMouse { get; }
    event EventHandler<MouseEventArgs>? MouseClick;
    event EventHandler<MouseEventArgs>? MouseEnter;
    event EventHandler<MouseEventArgs>? MouseLeave;
    event EventHandler<MouseEventArgs>? MouseMove;
    void ProcessMouseEvent(MouseEventArgs e);
}
```

Implemented by: Button, List, Tree, Dropdown, Menu, Toolbar, ScrollablePanel

### IContainer

Controls that can contain other controls:

```csharp
public interface IContainer : IWindowControl
{
    void AddControl(IWindowControl control);
    void RemoveControl(IWindowControl control);
    IReadOnlyList<IWindowControl> GetControls();
    T? FindControl<T>(string name) where T : IWindowControl;
}
```

Implemented by: ColumnContainer, ScrollablePanelControl, HorizontalGridControl

## Quick Reference

### Creating Controls

```csharp
// Using builders (recommended)
var button = Controls.Button("Click Me")
    .WithWidth(20)
    .OnClick((s, e, w) => { })
    .Build();

// Using constructors
var button = new ButtonControl
{
    Text = "Click Me",
    Width = 20
};
button.OnClick += (s, e) => { };

// Using static helpers
var label = Controls.Label("Simple text");
var header = Controls.Header("Title");
```

### Adding to Windows

```csharp
window.AddControl(control);
```

### Finding Controls by Name

```csharp
// Name a control
window.AddControl(
    Controls.Prompt("Name:")
        .WithName("nameInput")
        .Build()
);

// Find it later
var input = window.FindControl<PromptControl>("nameInput");
if (input != null)
{
    string text = input.Text;
}
```

### Common Patterns

#### Input Form

```csharp
window.AddControl(Controls.Header("Contact Form"));
window.AddControl(Controls.Prompt("Name:").WithName("name").Build());
window.AddControl(Controls.Prompt("Email:").WithName("email").Build());
window.AddControl(Controls.Button("Submit")
    .OnClick((s, e, w) =>
    {
        var name = w.FindControl<PromptControl>("name")?.Text;
        var email = w.FindControl<PromptControl>("email")?.Text;
        // Process form...
    })
    .Build());
```

#### Data Display

```csharp
var list = Controls.List()
    .AddItem("Item 1")
    .AddItem("Item 2")
    .AddItem("Item 3")
    .OnItemActivated((s, item, w) =>
    {
        windowSystem.NotificationStateService.ShowNotification(
            "Selected", item.Text, NotificationSeverity.Info);
    })
    .Build();

window.AddControl(list);
```

#### Layout

```csharp
var grid = Controls.HorizontalGrid()
    .WithAlignment(HorizontalAlignment.Stretch)
    .Column(col => col.Add(Controls.Label("Left")))
    .Column(col => col.Add(Controls.Label("Center")))
    .Column(col => col.Add(Controls.Label("Right")))
    .Build();

window.AddControl(grid);
```

## Next Steps

Browse detailed documentation for specific controls:

### Essential Controls
- [ButtonControl](controls/ButtonControl.md) - Interactive buttons
- [PromptControl](controls/PromptControl.md) - Text input
- [ListControl](controls/ListControl.md) - Item lists
- [MarkupControl](controls/MarkupControl.md) - Formatted text

### Advanced Controls
- [MenuControl](controls/MenuControl.md) - Menu systems
- [TreeControl](controls/TreeControl.md) - Hierarchical data
- [HorizontalGridControl](controls/HorizontalGridControl.md) - Multi-column layouts
- [SpectreRenderableControl](controls/SpectreRenderableControl.md) - Spectre.Console widgets

---

[Back to Main Documentation](../README.md)

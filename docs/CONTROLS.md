# Controls Reference

SharpConsoleUI provides 30+ built-in UI controls for building rich console applications.

> **New to SharpConsoleUI?** Start with the [Tutorials](tutorials/01-hello-window.md) — step-by-step guides that build real apps from scratch.

## Table of Contents

- [Overview](#overview)
- [Basic Input Controls](#basic-input-controls)
- [Selection Controls](#selection-controls)
- [Display Controls](#display-controls)
- [Drawing Controls](#drawing-controls)
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
| **CheckboxControl** | Toggle checkbox with label | Checked/unchecked state, change events |
| **[DatePickerControl](controls/DatePickerControl.md)** | Locale-aware date picker | Segmented editing, calendar popup, min/max dates |
| **[PromptControl](controls/PromptControl.md)** | Single-line text input | Enter key events, input validation, max length |
| **[TimePickerControl](controls/TimePickerControl.md)** | Locale-aware time picker | 12h/24h modes, seconds toggle, min/max times |
| **[MultilineEditControl](controls/MultilineEditControl.md)** | Multi-line text editor | Syntax highlighting, pluggable gutter, find/replace, undo/redo, word wrap |
| **[SliderControl](controls/SliderControl.md)** | Value slider with thumb | Horizontal/vertical, step/large-step, keyboard/mouse drag |
| **[RangeSliderControl](controls/RangeSliderControl.md)** | Dual-thumb range slider | MinRange enforcement, tab to switch thumbs, range events |

## Selection Controls

Controls for selecting items from lists or hierarchies.

| Control | Description | Details |
|---------|-------------|---------|
| **[ListControl](controls/ListControl.md)** | Scrollable list with selection | Single selection, item activation, keyboard navigation |
| **[TableControl](controls/TableControl.md)** | Interactive data grid | Virtual data, sorting, filtering (AND/OR), inline editing, multi-select, cell navigation, scrollbars |
| **TreeControl** | Hierarchical tree view | Expand/collapse nodes, selection, keyboard navigation |
| **DropdownControl** | Dropdown selection list | Click to expand, keyboard navigation, portal rendering |
| **MenuControl** | Menu bar with dropdowns | Horizontal/vertical menus, submenus, separators, keyboard shortcuts |

## Display Controls

Controls for displaying formatted content.

| Control | Description | Details |
|---------|-------------|---------|
| **[MarkupControl](controls/MarkupControl.md)** | Rich text with markup | Colors, bold, italic, links using `[markup]` syntax |
| **FigletControl** | ASCII art text (Figlet) | Large stylized text, multiple fonts |
| **LogViewerControl** | Log message viewer | Auto-scroll, filtering, severity colors |
| **SpectreRenderableControl** | Wrapper for Spectre widgets | Display Tables, Trees, Panels, Charts, etc. |
| **PanelControl** | Bordered content panel | Headers, multiple border styles, padding, mouse support |
| **RuleControl** | Horizontal rule/separator | Optional title, colors, horizontal line |
| **SparklineControl** | Time-series sparkline graph | Block, braille, or bidirectional modes; borders; titles |
| **[LineGraphControl](controls/LineGraphControl.md)** | Multi-series line graph | Braille and ASCII rendering modes, gradients, Y-axis labels, live updates |
| **BarGraphControl** | Horizontal bar graph | Gradient color thresholds, labels, value display |

## Drawing Controls

Controls for custom graphics and free-form drawing.

| Control | Description | Details |
|---------|-------------|---------|
| **[CanvasControl](controls/CanvasControl.md)** | Free-form drawing surface | 30+ drawing primitives, retained & immediate modes, thread-safe async painting |
| **[ImageControl](IMAGE_RENDERING.md)** | Half-block image renderer | Load PNG/JPEG/BMP/GIF/WebP/TIFF files or PixelBuffer; Fit/Fill/Stretch/None scaling |

## Layout Controls

Controls for organizing other controls.

| Control | Description | Details |
|---------|-------------|---------|
| **ColumnContainer** | Vertical stack container | Stack controls vertically, padding, alignment |
| **ScrollablePanelControl** | Scrollable content area | Vertical scrolling, contains multiple controls |
| **HorizontalGridControl** | Multi-column layout | Variable-width columns, alignment, splitters |
| **SplitterControl** | Resizable divider | Drag to resize adjacent columns |
| **[TabControl](controls/TabControl.md)** | Multi-page tab container | Tab headers, keyboard/mouse switching, state preservation |
| **[NavigationView](controls/NavigationView.md)** | Sidebar navigation + content area | WinUI-inspired nav pane, responsive display modes (Expanded/Compact/Minimal), content factories, gradient-transparent |
| **[ToolbarControl](controls/ToolbarControl.md)** | Horizontal button toolbar | Auto-height, wrapping, separator lines, content padding, Tab navigation |
| **[StatusBarControl](controls/StatusBarControl.md)** | Three-zone status bar | Left/center/right zones, clickable items, shortcut hints, above line separator |
| **SeparatorControl** | Visual separator | Simple horizontal line |
| **PortalContentContainer** | Portal overlay container | Host child controls in [portal overlays](PORTAL_SYSTEM.md), mouse/keyboard routing, focus tracking |

## Utility Controls

Special-purpose controls.

| Control | Description | Details |
|---------|-------------|---------|
| **RuleControl** | Titled horizontal rule | Section dividers with optional title |
| **SeparatorControl** | Plain horizontal line | Visual spacing |
| **[TerminalControl](controls/TerminalControl.md)** | PTY-backed terminal emulator | Full xterm-256color, keyboard/mouse passthrough, auto-resize. **Linux only.** |

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

Implemented by: Button, Checkbox, Prompt, MultilineEdit, List, Tree, Table, Dropdown, Menu, Canvas

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

Implemented by: Button, Checkbox, Prompt, MultilineEdit, List, Tree, Table, Dropdown, Canvas

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

Implemented by: Button, List, Tree, Table, Dropdown, Menu, Toolbar, ScrollablePanel, Canvas

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

Implemented by: ColumnContainer, ScrollablePanelControl, HorizontalGridControl, NavigationView

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

// Canvas control
var canvas = new CanvasControl(80, 24);
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

### Input Controls
- [DatePickerControl](controls/DatePickerControl.md) - Date picker with calendar popup
- [TimePickerControl](controls/TimePickerControl.md) - Time picker with 12h/24h modes

### Layout & Status Controls
- [ToolbarControl](controls/ToolbarControl.md) - Horizontal button toolbar with wrapping
- [StatusBarControl](controls/StatusBarControl.md) - Three-zone status bar with clickable items

### Advanced Controls
- [TableControl](controls/TableControl.md) - Interactive data grid with virtual data
- [TabControl](controls/TabControl.md) - Multi-page tab container
- [NavigationView](controls/NavigationView.md) - Sidebar navigation with content area
- [CanvasControl](controls/CanvasControl.md) - Free-form drawing surface
- [TerminalControl](controls/TerminalControl.md) - Embedded PTY terminal (Linux)

---

[Back to Main Documentation](../README.md)

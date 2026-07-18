# Controls Reference

SharpConsoleUI provides 41 built-in UI controls for building rich console applications.

> **New to SharpConsoleUI?** Start with the [Tutorials](tutorials/README.md) — step-by-step guides that build real apps from scratch.

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
- `Role` / `Outline` - Available on controls implementing `IRoleableControl`. Apply a semantic [Control Role](THEMES.md#control-roles) (Primary, Success, Danger, …) so the control's colors come from the theme's palette instead of being set by hand. `Role.Default` (the default) leaves color resolution unchanged. Most controls implement `IRoleableControl`; a few non-themed ones (Canvas, Image, Video, Html, …) do not and have no `Role`/`Outline`.

### Creating Your Own Control

Need something the built-in controls don't cover? There are two ways to build one, and a step-by-step tutorial for each. **Reach for composition first** — it's simpler and covers most cases.

- **Compose existing controls** — wrap a grid, labels, buttons, etc. into a reusable unit. No custom rendering, no `IWindowControl` plumbing. Start here.
  → [Contributor Tutorial 1: Composite Controls](tutorials/contributing/01-composite-controls.md)

- **Write a primitive from scratch** — a control that paints its own cells (a gauge, a badge, a sparkline). You derive from `BaseControl` and override its four abstract/virtual layout members: `ContentWidth`, `GetLogicalContentSize()`, `MeasureDOM(LayoutConstraints) → LayoutSize`, and `PaintDOM(...)`. The tutorial builds a `BadgeControl` end to end.
  → [Contributor Tutorial 2: Adding a Control](tutorials/contributing/02-adding-a-control.md)

Before hand-rolling a primitive, know these two things — they save the most time:

- **Don't parse markup yourself.** The framework already turns `[red]…[/]` tags into colored runs. Host or compose a [`MarkupControl`](controls/MarkupControl.md) for text, or call `SharpConsoleUI.Parsing.MarkupParser` if you paint directly. Writing raw markup strings straight into buffer cells renders the literal `[red]` tags on screen — the tags must be parsed to runs first. See [Markup Syntax](MARKUP_SYNTAX.md).
- **`MeasureDOM` and `PaintDOM` are not on `IWindowControl`.** Both come from the `IDOMPaintable` contract, which `BaseControl` implements — that's why deriving from `BaseControl` is the normal path. See the [Interfaces](#interfaces) section below for the exact signatures.

For the bigger picture of how paints, layout, and the character buffer fit together, see the [DOM Layout System](DOM_LAYOUT_SYSTEM.md) and [Rendering Pipeline](RENDERING_PIPELINE.md).

## Basic Input Controls

Controls for user input and interaction.

| Control | Description | Details |
|---------|-------------|---------|
| **[ButtonControl](controls/ButtonControl.md)** | Clickable button with text | Click events, keyboard/mouse support |
| **[CheckboxControl](controls/CheckboxControl.md)** | Toggle checkbox with label | Checked/unchecked state, change events |
| **[DatePickerControl](controls/DatePickerControl.md)** | Locale-aware date picker | Segmented editing, calendar popup, min/max dates |
| **[PromptControl](controls/PromptControl.md)** | Single-line text input | Enter key events, input validation, max length |
| **[TimePickerControl](controls/TimePickerControl.md)** | Locale-aware time picker | 12h/24h modes, seconds toggle, min/max times |
| **[MultilineEditControl](controls/MultilineEditControl.md)** | Multi-line text editor | [Syntax highlighting](SYNTAX_HIGHLIGHTING.md) (13 built-in languages via `SyntaxHighlighters.For(...)`), pluggable gutter, find/replace, undo/redo, word wrap |
| **[SliderControl](controls/SliderControl.md)** | Value slider with thumb | Horizontal/vertical, step/large-step, keyboard/mouse drag |
| **[RangeSliderControl](controls/RangeSliderControl.md)** | Dual-thumb range slider | MinRange enforcement, tab to switch thumbs, range events |
| **[FormControl](controls/FormControl.md)** | Labeled-input form | Two-column grid (label \| editor), typed field overloads (text/multiline/checkbox/dropdown/radio/slider), custom `AddField`, sections with collapse, row packing, validation, `GetValues`/`Submit`/`Submitted`, declarative [XML loader](FORM_XML.md) |

## Selection Controls

Controls for selecting items from lists or hierarchies.

| Control | Description | Details |
|---------|-------------|---------|
| **[ListControl](controls/ListControl.md)** | Scrollable list with selection | Single selection, item activation, keyboard navigation |
| **[TableControl](controls/TableControl.md)** | Interactive data grid | Virtual data, sorting, filtering (AND/OR), inline editing, multi-select, cell navigation, scrollbars |
| **[TreeControl](controls/TreeControl.md)** | Hierarchical tree view | Expand/collapse nodes, selection, keyboard navigation |
| **[DropdownControl](controls/DropdownControl.md)** | Dropdown selection list | Click to expand, keyboard navigation, portal rendering |
| **[RadioControl](controls/RadioControl.md)** | Single-select radio group | Typed `RadioGroup<T>` coordination, `Required`/`AllowDeselect` policies, label wrap, cross-layout grouping |
| **[MenuControl](controls/MenuControl.md)** | Menu bar with dropdowns | Horizontal/vertical menus, submenus, separators, keyboard shortcuts |

## Display Controls

Controls for displaying formatted content.

| Control | Description | Details |
|---------|-------------|---------|
| **[MarkupControl](controls/MarkupControl.md)** | Rich text with markup | Colors, bold, italic, **clickable & keyboard-navigable links** (`[link=url]`); renders Markdown (incl. links) via the [`[markdown]` tag](MARKUP_SYNTAX.md#markdown), optional border/header |
| **[HtmlControl](controls/HtmlControl.md)** | HTML content renderer | Parse & render HTML with images, links, tables, keyboard navigation |
| **[FigleControl](controls/FigleControl.md)** | ASCII art text (Figlet) | Large stylized text, multiple fonts |
| **[ChatTranscriptControl](controls/ChatTranscriptControl.md)** | Agent/chat transcript | Role-tagged messages (User/Assistant/System/Tool/Error), token-by-token streaming, collapsible tool messages, thinking indicator, gradient headers, alpha bubbles |
| **[LogViewerControl](controls/LogViewerControl.md)** | Log message viewer | Auto-scroll, filtering, severity colors |
| **[SpectreRenderableControl](controls/SpectreRenderableControl.md)** | Wrapper for Spectre widgets | Display Tables, Trees, Panels, Charts, etc. |
| **PanelControl** | Bordered container panel | Hosts child controls (a non-collapsible CollapsiblePanel); headers, border styles, padding, mouse. For bordered *text*, use [MarkupControl](controls/MarkupControl.md) with `.WithBorder()`. |
| **RuleControl** | Horizontal rule/separator | Optional title, colors, horizontal line |
| **SparklineControl** | Time-series sparkline graph | Block, braille, or bidirectional modes; borders; titles |
| **[LineGraphControl](controls/LineGraphControl.md)** | Multi-series line graph | Braille and ASCII rendering modes, gradients, Y-axis labels, live updates |
| **BarGraphControl** | Horizontal bar graph | Gradient color thresholds, labels, value display |
| **[SpinnerControl](controls/SpinnerControl.md)** | Animated indeterminate-progress spinner | Preset styles + custom frames, per-frame markup, auto-animates via the animation manager. Also available as the inline `[spinner]` markup tag. |

## Drawing Controls

Controls for custom graphics and free-form drawing.

| Control | Description | Details |
|---------|-------------|---------|
| **[CanvasControl](controls/CanvasControl.md)** | Free-form drawing surface | 30+ drawing primitives, retained & immediate modes, thread-safe async painting |
| **[ImageControl](IMAGE_RENDERING.md)** | Image display with Kitty graphics | Full-resolution via Kitty/WezTerm/Ghostty with half-block fallback; PNG/JPEG/BMP/GIF/WebP/TIFF; async loading |
| **[VideoControl](VIDEO_PLAYBACK.md)** | Terminal video player | Kitty graphics + half-block/ASCII/braille fallbacks (auto-detected); FFmpeg decode; overlay bar; dynamic resize; looping |

## Layout Controls

Controls for organizing other controls.

| Control | Description | Details |
|---------|-------------|---------|
| **ColumnContainer** | Vertical stack container | Stack controls vertically, padding, alignment |
| **[ScrollablePanelControl](controls/ScrollablePanelControl.md)** | Scrollable content area | Vertical scrolling, contains multiple controls |
| **[HorizontalGridControl](controls/HorizontalGridControl.md)** | Multi-column layout | Variable-width columns, alignment, splitters |
| **[GridControl](controls/GridControl.md)** | WinUI-style 2D grid | Fixed/Auto/Star rows & columns, row/col spans, gaps, per-cell styling, any control per cell |
| **[FlowControl](controls/FlowControl.md)** | Renders a flow inline in a region | Embeds `Flow.Run`/`Flow.Wizard` in a pane (vs. a modal); idle/done placeholder; normal focus scope |
| **[WizardControl](controls/WizardControl.md)** | Runs a multi-step wizard inline | A `FlowControl` named/presetted for wizards (the discoverable door); `wizard.Run(Flow.Wizard<T>()...)`; inline, not modal |
| **SplitterControl** | Resizable divider | Drag to resize adjacent columns |
| **[TabControl](controls/TabControl.md)** | Multi-page tab container | Tab headers, keyboard/mouse switching, state preservation |
| **[CollapsiblePanel](controls/CollapsiblePanel.md)** | Click-to-expand container | Borderless/bordered header, markup title, custom icons, MaxContentHeight, IControlHost. Can also serve as a plain, non-collapsible panel hosting any control via `.NonCollapsible()` / `.HideHeader()` |
| **[NavigationView](controls/NavigationView.md)** | Sidebar navigation + content area | WinUI-inspired nav pane, responsive display modes (Expanded/Compact/Minimal), content factories, gradient-transparent |
| **[ToolbarControl](controls/ToolbarControl.md)** | Horizontal button toolbar | Auto-height, wrapping, separator lines, content padding, Tab navigation |
| **[StatusBarControl](controls/StatusBarControl.md)** | Three-zone status bar | Left/center/right zones, clickable items, shortcut hints, above line separator |
| **SeparatorControl** | Visual separator | Simple horizontal line |
| **PortalContentContainer** | Portal overlay container | Host child controls in [portal overlays](PORTAL_SYSTEM.md), mouse/keyboard routing, focus tracking |

## Utility Controls

Special-purpose controls.

| Control | Description | Details |
|---------|-------------|---------|
| **[TerminalControl](controls/TerminalControl.md)** | PTY-backed terminal emulator | Full xterm-256color, keyboard/mouse passthrough, auto-resize. **Linux only.** |

## Interfaces

Controls implement these interfaces based on their capabilities:

### IWindowControl (Base Interface)

All controls implement this interface. It defines identity, layout, and sizing — **not** painting (see the note below):

```csharp
public interface IWindowControl : IDisposable
{
    // Identity
    IContainer? Container { get; set; }
    string? Name { get; set; }
    object? Tag { get; set; }
    bool Visible { get; set; }

    // Layout & sizing
    int? ContentWidth { get; }                    // intrinsic width of the content, or null
    HorizontalAlignment HorizontalAlignment { get; set; }
    VerticalAlignment VerticalAlignment { get; set; }
    Margin Margin { get; set; }
    StickyPosition StickyPosition { get; set; }
    int? Width { get; set; }
    int? Height { get; set; }
    int ActualX { get; }                          // where it was last rendered
    int ActualY { get; }
    int ActualWidth { get; }
    int ActualHeight { get; }

    // Intrinsic content size — returns System.Drawing.Size
    Size GetLogicalContentSize();

    // Invalidation (Repaint vs Relayout)
    void Invalidate(Invalidation work);
    void Invalidate();                            // default impl → Invalidate(Relayout)
    void Invalidate(bool redrawAll);              // default impl → Relayout / Repaint
}
```

> **Where are measure and paint?** `IWindowControl` has neither. Both come from the
> `IDOMPaintable` contract (`SharpConsoleUI/Layout/IDOMPaintable.cs`), which `BaseControl`
> implements and re-declares as abstract members you override:
> ```csharp
> public interface IDOMPaintable
> {
>     LayoutSize MeasureDOM(LayoutConstraints constraints);
>     void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
>                   Color defaultForeground, Color defaultBackground);
> }
> ```
> (`IDOMMeasurable` declares `MeasureDOM` alone, for controls that measure but don't paint.)
> This is why the normal way to write a custom primitive is to **derive from `BaseControl`**
> and override `MeasureDOM` and `PaintDOM` — see
> [Creating Your Own Control](#creating-your-own-control) and
> [Contributor Tutorial 2](tutorials/contributing/02-adding-a-control.md).
>
> **Namespaces:** `IWindowControl`, `IContainer`, `Margin`, `StickyPosition`, `BaseControl` →
> `SharpConsoleUI.Controls`; `HorizontalAlignment`, `VerticalAlignment`, `CharacterBuffer`,
> `LayoutRect`, `LayoutSize`, `LayoutConstraints`, `IDOMPaintable`, `IDOMMeasurable` →
> `SharpConsoleUI.Layout`; `Invalidation`, `Color` → `SharpConsoleUI`;
> `MarkupParser` → `SharpConsoleUI.Parsing`; **`Size` is `System.Drawing.Size`** (not a
> SharpConsoleUI type).

### IInteractiveControl

Controls that handle keyboard input:

```csharp
public interface IInteractiveControl : IWindowControl
{
    bool IsEnabled { get; set; }
    bool ProcessKey(KeyEventArgs e);
}
```

Implemented by: Button, Checkbox, Prompt, MultilineEdit, List, Tree, Table, Dropdown, Menu, Canvas, Grid

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

Implemented by: Button, Checkbox, Prompt, MultilineEdit, List, Tree, Table, Dropdown, Canvas, Grid

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

Implemented by: Button, List, Tree, Table, Dropdown, Menu, Toolbar, ScrollablePanel, Canvas, Grid

### IContainer

The container surface used by child controls for rendering — colors, dirty tracking, and invalidation. It is *not* a child-mutation API; for that, see `IControlHost` below.

```csharp
public interface IContainer
{
    Color BackgroundColor { get; set; }
    Color ForegroundColor { get; set; }
    ConsoleWindowSystem? GetConsoleWindowSystem { get; }
    void Invalidate(Invalidation work, IWindowControl? callerControl = null);
    int? GetVisibleHeightForControl(IWindowControl control);
}
```

Implemented by: ColumnContainer, ScrollablePanelControl, HorizontalGridControl, GridControl, NavigationView, Window, and other containers.

### IControlHost

Capability interface for containers whose children are a flat list of `IWindowControl`. Lets you add, remove, clear, and enumerate children without binding to a concrete container type.

```csharp
public interface IControlHost
{
    void AddControl(IWindowControl control);
    void RemoveControl(IWindowControl control);
    void ClearControls();
    IReadOnlyList<IWindowControl> Children { get; }
}
```

Implemented by: `ScrollablePanelControl`, `ColumnContainer`, `CollapsiblePanel`, `GridControl`, `Window`.

Not implemented by `TabControl` (tabs are title-keyed pages), `MenuControl` (children are `MenuItem`, not `IWindowControl`), `ToolbarControl`, or `NavigationView` — their child models are not a flat `IWindowControl` list, so forcing the contract would mean throwing `NotSupportedException`.

```csharp
// Write once against the capability, reuse anywhere.
void PopulateForm(IControlHost host)
{
    host.AddControl(Controls.Label("Name:"));
    host.AddControl(Controls.Prompt().Build());
}

PopulateForm(scrollablePanel);
PopulateForm(column);
PopulateForm(window);
```

`ColumnContainer` and `Window` keep their existing native names (`AddContent`/`Contents` on `ColumnContainer`; `RemoveContent`/`GetControls()` on `Window`); the `IControlHost` members are implemented via explicit interface forwarding so the public APIs are unchanged.

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
- [CollapsiblePanel](controls/CollapsiblePanel.md) - Click-to-expand container for progressive disclosure
- [NavigationView](controls/NavigationView.md) - Sidebar navigation with content area
- [CanvasControl](controls/CanvasControl.md) - Free-form drawing surface
- [TerminalControl](controls/TerminalControl.md) - Embedded PTY terminal (Linux)

### Cross-Cutting Features
- [Syntax Highlighting](SYNTAX_HIGHLIGHTING.md) - 13 built-in highlighters + registry, used by MultilineEditControl and Markdown code blocks
- [Markup Syntax](MARKUP_SYNTAX.md) - Colors, decorations, spinners, and the `[markdown]` tag

---

[Back to Main Documentation](../README.md)

# CollapsiblePanel

Click-to-expand container with a header that toggles a body of child controls.

## Overview

CollapsiblePanel is a container control with a clickable header. Clicking the header (or pressing Enter/Space when focused) toggles a body that hosts any number of child controls, collapsing or expanding it vertically. When collapsed, the body's children are hidden and skipped in Tab traversal — only the header row remains visible.

It is an `IControlHost` container, so its body is a flat list of `IWindowControl` children, just like `ScrollablePanelControl` or `ColumnContainer`.

**Important**: CollapsiblePanel does **not** provide scrolling on its own. Use `MaxContentHeight` to cap the body height, and wrap the content in a `ScrollablePanelControl` if you need the capped body to scroll (see [MaxContentHeight](#maxcontentheight)).

## When to Use

CollapsiblePanel is ideal for progressive disclosure — hiding secondary content until the user asks for it:

- **FAQ / Q&A panels** — each question is a header; the answer lives in the collapsible body.
- **Collapsing AI-agent secondary content** — fold away reasoning steps, sub-agent calls, and tool-call transcripts so the main answer stays front and center, while still letting the user expand the detail on demand.
- **Settings sections** — group related options under collapsible category headers.
- **Detail-on-demand** — keep dense logs, stack traces, or raw payloads out of the way until needed.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Title` | `string?` | `null` | Header title text (supports `[markup]` syntax) |
| `IsExpanded` | `bool` | `true` | Whether the body is expanded |
| `Collapsible` | `bool` | `true` | When `false`, the panel is locked expanded with no toggle affordance and is not a Tab stop (see [Panel mode](#panel-mode-non-collapsible)) |
| `ShowHeader` | `bool` | `true` | When `false`, the header row is suppressed (see [Panel mode](#panel-mode-non-collapsible)) |
| `HeaderStyle` | `CollapsibleHeaderStyle` | `Borderless` | `Borderless` or `Bordered` header rendering |
| `HeaderAlignment` | `HorizontalAlignment` | `Left` | Horizontal alignment of the header content |
| `ExpandedIcon` | `string?` | `▾` | Indicator shown when expanded |
| `CollapsedIcon` | `string?` | `▸` | Indicator shown when collapsed |
| `ShowHeaderSeparator` | `bool` | `false` | Draw a separator under a borderless header |
| `MaxContentHeight` | `int?` | `null` | Caps the visible body height (in rows); clips overflow |
| `AnimationMode` | `CollapsibleAnimationMode` | `None` | `None` (instant) or `Height` (tweened body) |
| `BorderColor` | `Color?` | `null` | Border color (uses theme if null) |
| `BackgroundColor` | `Color` | theme | Background color |
| `ForegroundColor` | `Color` | theme | Foreground/text color |
| `IsEnabled` | `bool` | `true` | Enable/disable the panel |
| `HasFocus` | `bool` | `false` | Whether the header has keyboard focus |
| `Children` | `IReadOnlyList<IWindowControl>` | empty | Body child controls (read-only) |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `ExpandedChanged` | `EventHandler<bool>` | Fired when expanded state changes; `bool` is the new `IsExpanded` value |

## Methods

| Method | Description |
|--------|-------------|
| `Expand()` | Expand the body |
| `Collapse()` | Collapse the body |
| `Toggle()` | Toggle the expanded state |
| `AddControl(IWindowControl control)` | Add a child to the body |
| `RemoveControl(IWindowControl control)` | Remove a child from the body |
| `ClearControls()` | Remove all body children |
| `GetChildren()` | Get the body child controls |

## Quick Start

### Using Builder (Recommended)

```csharp
var panel = Controls.CollapsiblePanel("Reasoning steps")
    .AddControl(Controls.Markup()
        .AddLine("[dim]1. Parsed the request[/]")
        .AddLine("[dim]2. Queried the index[/]")
        .AddLine("[dim]3. Ranked the results[/]")
        .Build())
    .Collapsed()
    .Build();

window.AddControl(panel);
```

### Using Constructor

```csharp
var panel = new CollapsiblePanel
{
    Title = "Details",
    IsExpanded = false
};

panel.AddControl(Controls.Label("Hidden until expanded"));

panel.ExpandedChanged += (sender, expanded) =>
{
    // React to expand/collapse
};

window.AddControl(panel);
```

## Header Styles

### Borderless (default)

A single clickable header row: an indicator icon followed by the title. Optionally followed by a separator line (`WithHeaderSeparator()`).

```
▸ Tool calls          (collapsed)

▾ Tool calls          (expanded)
  search("foo")
  read("bar.txt")
```

```csharp
Controls.CollapsiblePanel("Tool calls")
    .WithHeaderStyle(CollapsibleHeaderStyle.Borderless)
    .WithHeaderSeparator()
    .AddControl(Controls.Label("search(\"foo\")"))
    .AddControl(Controls.Label("read(\"bar.txt\")"))
    .Build();
```

### Bordered

A box (PanelControl-style) with the title embedded in the top border. The indicator icon sits next to the title in the border.

```
┌─ ▾ Sub-agent calls ──────────────┐
│  agent: researcher               │
│  agent: writer                   │
└──────────────────────────────────┘
```

```csharp
Controls.CollapsiblePanel("Sub-agent calls")
    .WithHeaderStyle(CollapsibleHeaderStyle.Bordered)
    .WithBorderColor(Color.Grey37)
    .AddControl(Controls.Label("agent: researcher"))
    .AddControl(Controls.Label("agent: writer"))
    .Build();
```

## Panel mode (non-collapsible)

Two opt-in flags let a CollapsiblePanel double as a plain panel — a titled (or titleless) container that hosts any control and never collapses.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Collapsible` | `bool` | `true` | When `false`, the panel is locked expanded, draws **no** ▾/▸ indicator, ignores header clicks and Enter/Space, and is **not** a Tab stop — focus passes straight through to its focusable body children. Setting it to `false` at runtime on a collapsed panel snaps it back to expanded. |
| `ShowHeader` | `bool` | `true` | When `false`, the header row is suppressed. Borderless → body only; Bordered → a clean titleless box framing the body. |

### Mutual-exclusion rule

A collapsible panel **always** shows its header — the header is the only toggle affordance. The effective header visibility is therefore:

```
effective header shown = ShowHeader || Collapsible
```

This means `Collapsible = true, ShowHeader = false` resolves gracefully to "header shown" (collapsibility wins — **no exception is thrown**). A headerless panel is consequently always non-collapsible.

### Pass-through focus

A non-collapsible panel (`Collapsible = false`) is not itself a Tab stop. Tab traversal skips the panel header and lands directly on the panel's focusable body children, exactly as if they were added to a transparent container. This makes panel mode safe to drop into an existing layout without introducing an extra stop.

### Combinations

| Collapsible | ShowHeader | HeaderStyle | Result |
|---|---|---|---|
| `true` | `true` | Borderless | Default collapsible panel |
| `true` | `true` | Bordered | Default bordered collapsible panel |
| `false` | `true` | Borderless | Static header (title, no indicator), body below; pass-through focus |
| `false` | `true` | Bordered | Titled bordered box, no collapse; pass-through focus |
| `false` | `false` | Borderless | Bare container, no header, no frame; pass-through focus |
| `false` | `false` | Bordered | Clean bordered box, no title — a true "panel" |
| `true` | `false` | * | Invalid → resolves to header shown (collapsibility wins) |

### Example: a bordered, headerless panel

```csharp
var panel = Controls.CollapsiblePanel()
    .NonCollapsible()
    .HideHeader()
    .WithHeaderStyle(CollapsibleHeaderStyle.Bordered)
    .AddControl(Controls.Markup("[bold]Status[/]").Build())
    .AddControl(Controls.Button("Refresh")
        .OnClick((_, _, _) => { /* refresh */ })
        .Build())
    .Build();

window.AddControl(panel);
```

The builder methods are `.Collapsible(bool)`, `.NonCollapsible()`, `.ShowHeader(bool)`, and `.HideHeader()` (see the [Builder Reference](#builder-reference)).

## Custom Icons

Override the expand/collapse indicators with any string. Use `WithIcons(expanded, collapsed)` to set both at once, or `WithExpandedIcon`/`WithCollapsedIcon` individually.

```csharp
// ASCII-style indicators
Controls.CollapsiblePanel("Advanced options")
    .WithIcons("[-]", "[+]")
    .AddControl(advancedOptionsPanel)
    .Build();

// Or set them separately
Controls.CollapsiblePanel("Logs")
    .WithExpandedIcon("v")
    .WithCollapsedIcon(">")
    .Build();
```

## Header Separator

For borderless headers, draw a thin separator line under the header with `WithHeaderSeparator()`:

```csharp
Controls.CollapsiblePanel("Section")
    .WithHeaderSeparator()          // defaults to true
    .AddControl(content)
    .Build();
```

## MaxContentHeight

`WithMaxContentHeight(rows)` caps the body to a fixed number of rows. Content taller than the cap is clipped. CollapsiblePanel does not scroll on its own — to get a **scrollable capped body**, wrap the content in a `ScrollablePanelControl`:

```csharp
// A long transcript, capped at 8 rows and scrollable
var transcript = Controls.ScrollablePanel()
    .WithVerticalScroll()
    .Build();

for (int i = 1; i <= 100; i++)
{
    transcript.AddControl(Controls.Label($"[dim]log line {i}[/]"));
}

var panel = Controls.CollapsiblePanel("Full transcript")
    .Collapsed()
    .WithMaxContentHeight(8)        // body is at most 8 rows tall
    .AddControl(transcript)         // the inner panel scrolls within those 8 rows
    .Build();

window.AddControl(panel);
```

## Activation

There are several ways to toggle the panel:

| Mechanism | Action |
|-----------|--------|
| **Header click** | Click the header to toggle |
| **Enter / Space** | Toggle when the header has keyboard focus |
| **Tab** | Move focus to the next control (body children are skipped while collapsed) |
| Programmatic | Set `IsExpanded`, or call `Toggle()` / `Expand()` / `Collapse()` |

### ExpandedChanged Event

Subscribe to react when the state changes — for example to lazily populate the body the first time it opens:

```csharp
var panel = Controls.CollapsiblePanel("Tool output")
    .Collapsed()
    .OnExpandedChanged((sender, expanded) =>
    {
        if (expanded)
        {
            windowSystem.LogService.LogInfo("Tool output panel expanded");
        }
    })
    .Build();
```

Or on the control directly:

```csharp
panel.ExpandedChanged += (sender, expanded) =>
{
    var caption = expanded ? "Showing details" : "Details hidden";
    statusLabel?.SetContent($"[dim]{caption}[/]");
};
```

### Programmatic Toggle

```csharp
var panel = window.FindControl<CollapsiblePanel>("details");
if (panel != null)
{
    panel.Toggle();          // flip state
    // or
    panel.IsExpanded = true; // expand explicitly
}
```

## Sizing and Alignment

```csharp
Controls.CollapsiblePanel("Centered header")
    .WithWidth(50)                              // fixed width
    .WithHeaderAlignment(HorizontalAlignment.Center)
    .AddControl(content)
    .Build();
```

- `WithWidth(int)` sets a fixed panel width (auto-sized if not set).
- `WithHeaderAlignment(HorizontalAlignment)` aligns the header content (`Left`, `Center`, `Right`).

## Animation

By default, expand/collapse re-layouts instantly. Opt into a height tween with `Animated()` (or `WithAnimation(CollapsibleAnimationMode.Height)`):

```csharp
Controls.CollapsiblePanel("Animated section")
    .Animated()                                 // body height tweens open/closed
    .AddControl(content)
    .Build();
```

## Colors

CollapsiblePanel uses **PanelControl-style theme resolution** plus optional overrides. It does **not** inherit `TabControl` colors — if you nest a CollapsiblePanel inside a tab, set its colors explicitly when you want them to differ from the theme.

```csharp
Controls.CollapsiblePanel("Styled")
    .WithBorderColor(Color.Grey37)
    .WithBackgroundColor(new Color(30, 30, 40))
    .WithForegroundColor(Color.White)
    .Build();
```

Unset colors fall back to the active theme. The title accepts markup, so you can color individual parts of the header:

```csharp
Controls.CollapsiblePanel("[bold cyan]Reasoning[/] [dim](3 steps)[/]")
    .Collapsed()
    .Build();
```

## Container Behavior

CollapsiblePanel implements `IControlHost`, so its body can host **any** `IWindowControl` — labels, markup, lists, tables, nested panels, even another CollapsiblePanel. You can write reusable code against the capability interface:

```csharp
void PopulateBody(IControlHost host)
{
    host.AddControl(Controls.Header("Settings"));
    host.AddControl(Controls.Checkbox("Enable telemetry").Build());
}

var panel = Controls.CollapsiblePanel("Preferences").Build();
PopulateBody(panel);
window.AddControl(panel);
```

## Builder Reference

| Method | Description |
|--------|-------------|
| `Controls.CollapsiblePanel(string? title = null)` | Create a builder, optionally seeding the title |
| `.WithTitle(string)` | Set the header title (supports markup) |
| `.Collapsed()` / `.Expanded()` | Set the initial expanded state |
| `.WithHeaderStyle(CollapsibleHeaderStyle)` | `Borderless` or `Bordered` |
| `.WithHeaderAlignment(HorizontalAlignment)` | Align the header content |
| `.WithExpandedIcon(string)` / `.WithCollapsedIcon(string)` | Set indicator icons individually |
| `.WithIcons(expanded, collapsed)` | Set both indicator icons |
| `.WithHeaderSeparator(bool = true)` | Draw a separator under a borderless header |
| `.Collapsible(bool = true)` | Toggle collapsibility; `false` = panel mode (locked expanded, pass-through focus) |
| `.NonCollapsible()` | Shorthand for `.Collapsible(false)` |
| `.ShowHeader(bool = true)` | Show or suppress the header row |
| `.HideHeader()` | Shorthand for `.ShowHeader(false)` |
| `.WithMaxContentHeight(int)` | Cap the visible body height |
| `.WithAnimation(CollapsibleAnimationMode)` / `.Animated()` | Configure expand/collapse animation |
| `.WithWidth(int)` | Set fixed panel width |
| `.WithBorderColor(Color)` | Set border color |
| `.WithBackgroundColor(Color)` | Set background color |
| `.WithForegroundColor(Color)` | Set foreground/text color |
| `.WithName(string)` | Set control name for `FindControl<T>` |
| `.AddControl(IWindowControl)` | Add a child to the body |
| `.OnExpandedChanged(EventHandler<bool>)` | Subscribe to the `ExpandedChanged` event |
| `.Build()` | Build the `CollapsiblePanel` |

## Best Practices

1. **Start collapsed for secondary content**: Use `.Collapsed()` for reasoning, tool calls, and logs so the primary content stays visible.
2. **Cap long bodies**: Use `WithMaxContentHeight()` to keep large content from dominating the window.
3. **Wrap to scroll**: Compose with `ScrollablePanelControl` when a capped body needs scrolling.
4. **Use markup titles**: Color or annotate header titles (e.g. step counts) with `[markup]` syntax.
5. **Set colors explicitly inside tabs**: CollapsiblePanel does not inherit TabControl colors.

## See Also

- [ScrollablePanelControl](ScrollablePanelControl.md) - Wrap a capped body to make it scrollable
- [TabControl](TabControl.md) - Switchable multi-page container
- [MarkupControl](MarkupControl.md) - Formatted text for headers and bodies

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

# Fluent Builders Reference

SharpConsoleUI provides fluent builder APIs for creating windows and controls with clean, chainable syntax.

## Table of Contents

- [WindowBuilder](#windowbuilder)
- [Control Builders](#control-builders)
- [Controls Static Factory](#controls-static-factory)
- [Builder Patterns](#builder-patterns)

## WindowBuilder

The `WindowBuilder` class provides a fluent API for creating and configuring windows.

### Basic Usage

```csharp
using SharpConsoleUI.Builders;

var window = new WindowBuilder(windowSystem)
    .WithTitle("My Window")
    .WithSize(80, 25)
    .Centered()
    .Build();
```

### Configuration Methods

#### Size and Position

```csharp
.WithSize(width, height)           // Set window dimensions
.AtPosition(x, y)                  // Set window position
.Centered()                        // Center on screen
.WithMinimumSize(width, height)    // Set minimum size constraints
```

#### Appearance

```csharp
.WithTitle(title)                                // Set window title
.WithColors(foreground, background)              // Set colors
.Borderless()                                    // Remove border
```

#### Behavior

```csharp
.Resizable(bool)                   // Enable/disable resizing (default: true)
.Movable(bool)                     // Enable/disable moving (default: true)
.Closable(bool)                    // Enable/disable closing (default: true)
.Minimizable(bool)                 // Enable/disable minimizing (default: true)
.Maximizable(bool)                 // Enable/disable maximizing (default: true)
.AsModal()                         // Make window modal
```

#### State

```csharp
.Maximized()                       // Start maximized
.Minimized()                       // Start minimized
```

#### Independent Window Thread

```csharp
.WithAsyncWindowThread(asyncMethod)   // Attach async update loop
```

The async method receives `Window` and `CancellationToken`:

```csharp
private async Task UpdateWindowAsync(Window window, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        // Update window content
        await Task.Delay(1000, ct);
    }
}
```

#### Event Handlers

```csharp
.OnKeyPressed((sender, e) => { })        // Key press handler
.OnClosed((sender, e) => { })            // Window closed handler
```

### Templates

```csharp
// Dialog template (title, size, centered, modal)
.WithTemplate(new DialogTemplate("Title", width, height))

// Tool window template (title, position, size)
.WithTemplate(new ToolWindowTemplate("Tools", new Point(x, y), new Size(w, h)))
```

### Complete Example

```csharp
var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("Task Manager")
    .WithSize(80, 25)
    .Centered()
    .WithColors(Color.White, Color.DarkBlue)
    .Resizable()
    .Movable()
    .WithMinimumSize(60, 20)
    .OnKeyPressed((sender, e) =>
    {
        if (e.KeyInfo.Key == ConsoleKey.Escape)
        {
            windowSystem.CloseWindow(sender as Window);
            e.Handled = true;
        }
    })
    .Build();
```

## Control Builders

Control builders provide fluent APIs for configuring controls before adding them to windows.

### ButtonBuilder

```csharp
Controls.Button("Click Me")
    .WithWidth(20)
    .WithAlignment(HorizontalAlignment.Center)
    .WithMargin(0, 1, 0, 0)
    .WithName("submitButton")
    .OnClick((sender, e, window) =>
    {
        // Button click handler - window parameter available
    })
    .Build();
```

### ListBuilder

```csharp
Controls.List()
    .AddItem("Item 1")
    .AddItem("Item 2")
    .AddItem("Item 3")
    .WithHeight(10)
    .WithColors(Color.White, Color.Grey15)
    .OnItemActivated((sender, item, window) =>
    {
        // Item double-clicked or Enter pressed
    })
    .OnSelectionChanged((sender, index, window) =>
    {
        // Selection changed
    })
    .Build();
```

### CheckboxBuilder

```csharp
Controls.Checkbox("Enable feature", initialChecked: false)
    .OnCheckedChanged((sender, isChecked, window) =>
    {
        // Checkbox state changed
    })
    .Build();
```

### DropdownBuilder

```csharp
Controls.Dropdown()
    .AddItem("Option 1")
    .AddItem("Option 2")
    .AddItem("Option 3")
    .WithWidth(30)
    .OnSelectionChanged((sender, index, window) =>
    {
        // Selection changed
    })
    .Build();
```

### PromptBuilder

```csharp
Controls.Prompt("Enter name:")
    .WithInitialText("")
    .WithMaxLength(50)
    .WithName("nameInput")
    .OnEntered((sender, text, window) =>
    {
        // Enter key pressed
    })
    .OnInputChanged((sender, text, window) =>
    {
        // Text changed
    })
    .Build();
```

### MarkupBuilder

```csharp
Controls.Markup()
    .AddLine("[bold yellow]Title[/]")
    .AddLine("")
    .AddLine("Regular text")
    .AddLine("[green]Success message[/]")
    .WithAlignment(HorizontalAlignment.Center)
    .Build();
```

### TreeControlBuilder

```csharp
Controls.Tree()
    .AddNode("Root", children =>
    {
        children.AddNode("Child 1");
        children.AddNode("Child 2", grandchildren =>
        {
            grandchildren.AddNode("Grandchild");
        });
    })
    .WithHeight(15)
    .WithColors(Color.Grey15, Color.White)
    .Build();
```

### HorizontalGridBuilder

```csharp
Controls.HorizontalGrid()
    .WithAlignment(HorizontalAlignment.Stretch)
    .Column(col => col.Add(Controls.Label("Left")))
    .Column(col => col.Add(Controls.Label("Middle")))
    .Column(col => col.Add(Controls.Label("Right")))
    .Build();
```

### ScrollablePanelBuilder

```csharp
Controls.ScrollablePanel()
    .AddControl(Controls.Label("Line 1"))
    .AddControl(Controls.Label("Line 2"))
    .AddControl(Controls.Label("Line 3"))
    .WithHeight(5)
    .Build();
```

### MenuBuilder

```csharp
Controls.Menu()
    .AddItem(item => item
        .WithText("File")
        .AddSubmenu(submenu => submenu
            .AddItem(i => i.WithText("New"))
            .AddItem(i => i.WithText("Open"))
            .AddSeparator()
            .AddItem(i => i.WithText("Exit"))
        )
    )
    .AddItem(item => item.WithText("Edit"))
    .Build();
```

### ToolbarBuilder

```csharp
Controls.Toolbar()
    .AddButton("New", onClick: (s, e, w) => { })
    .AddButton("Open", onClick: (s, e, w) => { })
    .AddSeparator()
    .AddButton("Save", onClick: (s, e, w) => { })
    .Build();
```

### MultilineEditControlBuilder

```csharp
Controls.MultilineEdit()
    .WithViewportHeight(10)
    .WithWrapMode(WrapMode.Wrap)
    .WithInitialText("Initial content")
    .Build();
```

### FigleControlBuilder

```csharp
Controls.Figle("BIG TEXT")
    .WithFont(FigletFont.Load("standard"))
    .WithAlignment(HorizontalAlignment.Center)
    .WithColor(Color.Yellow)
    .Build();
```

### SparklineBuilder

```csharp
// Basic sparkline with block characters (default)
new SparklineBuilder()
    .WithHeight(6)
    .WithMaxValue(100)
    .WithBarColor(Color.Cyan1)
    .WithBorder(BorderStyle.Rounded, Color.Grey50)
    .WithTitle("CPU Usage %", Color.Cyan1)
    .WithData(cpuHistory)
    .Build();

// Braille mode for smoother appearance
new SparklineBuilder()
    .WithHeight(6)
    .WithMode(SparklineMode.Braille)  // Use braille patterns (5 levels)
    .WithMaxValue(100)
    .WithBarColor(Color.Green)
    .WithTitle("Memory %")
    .Build();

// Bidirectional mode for network upload/download visualization
new SparklineBuilder()
    .WithHeight(10)
    .WithMode(SparklineMode.BidirectionalBraille)  // Upload up, download down
    .WithMaxValue(100)                    // Primary (upload) max
    .WithSecondaryMaxValue(100)           // Secondary (download) max
    .WithBarColor(Color.Cyan1)            // Upload color (goes up from center)
    .WithSecondaryBarColor(Color.Green)   // Download color (goes down from center)
    .WithTitle("↑ Upload  ↓ Download", Color.Grey70)
    .WithBidirectionalData(uploadHistory, downloadHistory)
    .Build();
```

**SparklineMode Options:**
- `Block` (default) - Uses 9-level block characters (▁▂▃▄▅▆▇█)
- `Braille` - Uses 5-level braille patterns for smoother, denser appearance
- `Bidirectional` - Two series: primary goes up from center, secondary goes down (block chars)
- `BidirectionalBraille` - Same as Bidirectional but with braille patterns

**Bidirectional Mode Methods:**
- `WithSecondaryData(IEnumerable<double>)` - Set secondary data series
- `WithSecondaryBarColor(Color)` - Color for secondary (downward) bars
- `WithSecondaryMaxValue(double)` - Max scale for secondary series
- `WithBidirectionalData(primary, secondary)` - Set both series at once

### BarGraphBuilder

```csharp
// Basic bar graph with fixed color
new BarGraphBuilder()
    .WithLabel("CPU")
    .WithValue(65.5)
    .WithMaxValue(100)
    .WithBarWidth(30)
    .WithFilledColor(Color.Cyan1)
    .WithUnfilledColor(Color.Grey35)
    .ShowLabel()
    .ShowValue()
    .WithValueFormat("F1")
    .Build();

// Bar graph with gradient thresholds
new BarGraphBuilder()
    .WithLabel("RAM Used")
    .WithValue(75.0)
    .WithMaxValue(100)
    .WithBarWidth(30)
    .WithStandardGradient()  // Green (0-49%), Yellow (50-79%), Red (80%+)
    .Build();

// Custom gradient thresholds
new BarGraphBuilder()
    .WithLabel("Swap")
    .WithValue(30.0)
    .WithMaxValue(100)
    .WithGradient(
        new ColorThreshold(0, Color.Green),
        new ColorThreshold(25, Color.Yellow),
        new ColorThreshold(50, Color.Red)
    )
    .Build();

// Aligned bar graphs with fixed label width
new BarGraphBuilder()
    .WithLabel("RAM Used")
    .WithLabelWidth(12)  // All bars align at position 12 + ": "
    .WithValue(75.0)
    .WithMaxValue(100)
    .WithBarWidth(30)
    .Build();
```

**Key Methods:**
- `WithLabelWidth(int)` - Fixed label column width for vertical alignment
- `WithGradient(params ColorThreshold[])` - Set custom color thresholds
- `WithStandardGradient()` - Convenience method for green/yellow/red at 0/50/80%

### TerminalBuilder

> **Platform:** Linux only. Requires `PtyShim.RunIfShim(args)` at the start of `Main` — see [TerminalControl](controls/TerminalControl.md#critical-setup-requirement).

```csharp
// Quick open — auto-sized window
Controls.Terminal().Open(ws);

// Explicit size (cols × rows)
Controls.Terminal().Open(ws, width: 120, height: 40);

// Different executable
Controls.Terminal("/usr/bin/htop").Open(ws);

// With arguments, manual window
var terminal = Controls.Terminal("/usr/bin/vim")
    .WithArgs("/etc/hosts")
    .Build();

var window = new WindowBuilder(ws)
    .WithTitle(terminal.Title)
    .WithSize(102, 42)
    .Centered()
    .Closable(true)
    .AddControl(terminal)
    .Build();

ws.AddWindow(window);
```

**Methods:**
- `WithExe(string)` — Executable to launch (default: `/bin/bash`)
- `WithArgs(params string[])` — Arguments passed to the executable
- `Build()` — Opens the PTY and returns a `TerminalControl`
- `Open(ws, width?, height?)` — Build and open in a new centered window

### SpectreRenderableBuilder

```csharp
var table = new Table()
    .AddColumn("Name")
    .AddColumn("Value")
    .AddRow("Item 1", "100")
    .AddRow("Item 2", "200");

Controls.SpectreRenderable(table)
    .WithAlignment(HorizontalAlignment.Center)
    .Build();
```

### RuleBuilder

```csharp
Controls.RuleBuilder()
    .WithTitle("Section Title")
    .WithColor(Color.Blue)
    .StickyTop()  // Stick to top of window
    .Build();
```

### PanelBuilder

```csharp
// Basic panel with content
Controls.Panel()
    .WithContent("[yellow bold]Panel Title[/]")
    .WithHeader("Header Text")
    .Rounded()  // Use rounded border
    .WithBorderColor(Color.Cyan1)
    .WithPadding(2, 1)
    .Build();

// Different border styles
Controls.Panel()
    .WithContent("Double line border")
    .DoubleLine()
    .Build();

Controls.Panel()
    .WithContent("Single line border")
    .SingleLine()
    .Build();

Controls.Panel()
    .WithContent("No border")
    .NoBorder()
    .Build();

// Panel that fills available height
Controls.Panel()
    .WithContent("Stretches vertically")
    .FillVertical()
    .Build();

// With header alignment
Controls.Panel()
    .WithContent("Content here")
    .WithHeader("Centered Header")
    .HeaderCenter()
    .Build();
```

**Border Styles:**
- `Rounded()` - Rounded corners (╭─╮│╰╯)
- `DoubleLine()` - Double-line border (╔═╗║╚╝)
- `SingleLine()` - Single-line border (┌─┐│└┘)
- `NoBorder()` - No visible border

**Key Methods:**
- `WithContent(string)` - Set text content (supports Spectre markup)
- `WithContent(IRenderable)` - Set any Spectre renderable as content
- `WithHeader(string)` - Add header text to border
- `HeaderLeft()` / `HeaderCenter()` / `HeaderRight()` - Header alignment
- `WithPadding(int)` or `WithPadding(left, top, right, bottom)` - Inner padding
- `WithBorderColor(Color)` - Border color
- `FillVertical()` - Stretch panel to fill available height
- `WithHeight(int)` - Set explicit height

## Controls Static Factory

The `Controls` static class provides convenient factory methods.

### Quick Creation Methods

```csharp
// Simple controls (no builder needed)
Controls.Label("Text")              // Plain text label
Controls.Header("Title")            // Bold yellow header
Controls.Info("Info message")       // Blue info text
Controls.Warning("Warning")         // Orange warning text
Controls.Error("Error message")     // Red error text
Controls.Success("Success!")        // Green success text
Controls.Rule("Section")            // Horizontal rule with title
Controls.Separator()                // Plain horizontal line
Controls.Space()                    // Empty space/padding
```

### Builder Methods

```csharp
// Returns builders for fluent configuration
Controls.Button(text)               // ButtonBuilder
Controls.Markup(line)               // MarkupBuilder
Controls.List()                     // ListBuilder
Controls.Checkbox(label, checked)   // CheckboxBuilder
Controls.Dropdown()                 // DropdownBuilder
Controls.Prompt(prompt)             // PromptBuilder
Controls.Tree()                     // TreeControlBuilder
Controls.HorizontalGrid()           // HorizontalGridBuilder
Controls.ScrollablePanel()          // ScrollablePanelBuilder
Controls.Menu()                     // MenuBuilder
Controls.Toolbar()                  // ToolbarBuilder
Controls.MultilineEdit()            // MultilineEditControlBuilder
Controls.Figle(text)                // FigleControlBuilder
Controls.SpectreRenderable(widget)  // SpectreRenderableBuilder
Controls.RuleBuilder()              // RuleBuilder
Controls.Panel()                    // PanelBuilder (for bordered content panels)
Controls.Panel(text)                // PanelControl directly with text content
Controls.Terminal(exe?)             // TerminalBuilder — Linux only, requires PtyShim setup
new SparklineBuilder()              // SparklineBuilder (for time-series graphs)
new BarGraphBuilder()               // BarGraphBuilder (for horizontal bar graphs)
```

## Builder Patterns

### Common Pattern: Name and Find

Name controls to find them later:

```csharp
window.AddControl(
    Controls.Prompt("Enter name:")
        .WithName("nameInput")
        .Build()
);

window.AddControl(
    Controls.Label("Status: Ready")
        .WithName("statusLabel")
        .Build()
);

// Later, find by name
var nameInput = window.FindControl<PromptControl>("nameInput");
var statusLabel = window.FindControl<MarkupControl>("statusLabel");
```

### Common Pattern: Event Handlers with Window Access

All event handlers include a `window` parameter:

```csharp
window.AddControl(
    Controls.Button("Submit")
        .OnClick((sender, e, window) =>
        {
            // Access other controls through window parameter
            var input = window.FindControl<PromptControl>("nameInput");
            var status = window.FindControl<MarkupControl>("status");

            if (!string.IsNullOrWhiteSpace(input?.Text))
            {
                status?.SetContent($"[green]Hello, {input.Text}![/]");
            }
        })
        .Build()
);
```

### Common Pattern: Layout with Alignment

```csharp
window.AddControl(
    Controls.Header("Welcome")
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);

window.AddControl(
    Controls.Button("Submit")
        .WithWidth(20)
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);
```

### Common Pattern: Sticky Positioning

```csharp
// Stick to top
window.AddControl(
    Controls.RuleBuilder()
        .WithTitle("Header")
        .StickyTop()
        .Build()
);

// Stick to bottom
window.AddControl(
    Controls.Label("Press ESC to close")
        .WithAlignment(HorizontalAlignment.Right)
        .StickyBottom()
        .Build()
);
```

### Common Pattern: Margins

```csharp
window.AddControl(
    Controls.Label("Text with margins")
        .WithMargin(left: 2, top: 1, right: 2, bottom: 1)
        .Build()
);
```

## Complete Application Example

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Drivers;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("Contact Form")
    .WithSize(60, 20)
    .Centered()
    .WithColors(Color.White, Color.DarkBlue)
    .Build();

// Header
mainWindow.AddControl(
    Controls.Header("Contact Information")
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);

mainWindow.AddControl(Controls.Space());

// Name input
mainWindow.AddControl(
    Controls.Prompt("Name:")
        .WithName("nameInput")
        .WithMaxLength(50)
        .OnInputChanged((sender, text, window) =>
        {
            var status = window.FindControl<MarkupControl>("statusLabel");
            status?.SetContent($"[dim]Typing... ({text.Length} chars)[/]");
        })
        .Build()
);

// Email input
mainWindow.AddControl(
    Controls.Prompt("Email:")
        .WithName("emailInput")
        .WithMaxLength(100)
        .Build()
);

mainWindow.AddControl(Controls.Space());

// Message input
mainWindow.AddControl(Controls.Label("Message:"));
mainWindow.AddControl(
    Controls.MultilineEdit()
        .WithName("messageInput")
        .WithViewportHeight(5)
        .WithWrapMode(WrapMode.Wrap)
        .Build()
);

mainWindow.AddControl(Controls.Space());

// Buttons
mainWindow.AddControl(
    Controls.HorizontalGrid()
        .Column(col => col.Add(
            Controls.Button("Submit")
                .OnClick((sender, e, window) =>
                {
                    var name = window.FindControl<PromptControl>("nameInput");
                    var email = window.FindControl<PromptControl>("emailInput");
                    var message = window.FindControl<MultilineEditControl>("messageInput");

                    if (string.IsNullOrWhiteSpace(name?.Text))
                    {
                        windowSystem.NotificationStateService.ShowNotification(
                            "Validation Error",
                            "Name is required",
                            NotificationSeverity.Warning
                        );
                        return;
                    }

                    // Process form...
                    windowSystem.NotificationStateService.ShowNotification(
                        "Success",
                        "Message sent successfully!",
                        NotificationSeverity.Success
                    );
                })
                .Build()
        ))
        .Column(col => col.Add(
            Controls.Button("Clear")
                .OnClick((sender, e, window) =>
                {
                    window.FindControl<PromptControl>("nameInput")?.Clear();
                    window.FindControl<PromptControl>("emailInput")?.Clear();
                    window.FindControl<MultilineEditControl>("messageInput")?.Clear();
                })
                .Build()
        ))
        .Build()
);

// Status label
mainWindow.AddControl(
    Controls.Label("Ready")
        .WithName("statusLabel")
        .WithAlignment(HorizontalAlignment.Center)
        .StickyBottom()
        .Build()
);

windowSystem.AddWindow(mainWindow);
windowSystem.Run();
```

---

[Back to Main Documentation](../README.md)

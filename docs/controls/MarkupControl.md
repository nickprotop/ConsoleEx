# MarkupControl

Display rich formatted text using Spectre.Console markup syntax.

## Overview

MarkupControl displays multi-line text with rich formatting using Spectre.Console's markup syntax. Supports colors, styles (bold, italic, underline), and more.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Lines` | `List<string>` | Empty | List of text lines to display |

## Creating Markup

### Using Builder (Recommended)

```csharp
var markup = Controls.Markup()
    .AddLine("[bold yellow]Welcome to SharpConsoleUI![/]")
    .AddLine("")
    .AddLine("Features:")
    .AddLine("  [green]• Modern UI controls[/]")
    .AddLine("  [green]• Async support[/]")
    .AddLine("  [green]• Rich formatting[/]")
    .AddLine("")
    .AddLine("[dim]Press any key to continue...[/]")
    .WithAlignment(HorizontalAlignment.Center)
    .Build();

window.AddControl(markup);
```

### Using Constructor

```csharp
var markup = new MarkupControl(new List<string>
{
    "[bold yellow]Title[/]",
    "",
    "Regular text",
    "[green]Success message[/]",
    "[red]Error message[/]"
});

window.AddControl(markup);
```

### Using Static Helpers

```csharp
// Simple text
window.AddControl(Controls.Label("Plain text"));

// Formatted shortcuts
window.AddControl(Controls.Header("Section Title"));  // Bold yellow
window.AddControl(Controls.Info("Information"));      // Blue
window.AddControl(Controls.Warning("Warning"));       // Orange
window.AddControl(Controls.Error("Error"));           // Red
window.AddControl(Controls.Success("Success"));       // Green
```

## Markup Syntax

SharpConsoleUI uses Spectre.Console markup syntax:

### Colors

```csharp
"[red]Red text[/]"
"[green]Green text[/]"
"[blue]Blue text[/]"
"[yellow]Yellow text[/]"
"[cyan]Cyan text[/]"
"[magenta]Magenta text[/]"
"[white]White text[/]"
"[black]Black text[/]"

// Extended colors
"[orange3]Orange text[/]"
"[purple]Purple text[/]"
"[grey]Grey text[/]"

// RGB colors
"[rgb(255,128,0)]Custom color[/]"
"[#FF8000]Hex color[/]"
```

### Styles

```csharp
"[bold]Bold text[/]"
"[italic]Italic text[/]"
"[underline]Underlined text[/]"
"[strikethrough]Strikethrough text[/]"
"[dim]Dimmed text[/]"
"[reverse]Reversed text[/]"
```

### Combined Markup

```csharp
"[bold red]Bold red text[/]"
"[italic blue]Italic blue text[/]"
"[bold yellow underline]Multiple styles[/]"
```

### Escaping

```csharp
"Use [[double brackets]] to display literal brackets"
"This shows [red]colored[/] and [[red]] literal markup"
```

## Examples

### Welcome Screen

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("")
        .AddLine("[bold yellow]╔═══════════════════════════════╗[/]")
        .AddLine("[bold yellow]║   Welcome to My Application   ║[/]")
        .AddLine("[bold yellow]╚═══════════════════════════════╝[/]")
        .AddLine("")
        .AddLine("[dim]Version 1.0.0[/]")
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);
```

### Status Messages

```csharp
// Success
window.AddControl(Controls.Success("File saved successfully!"));

// Error
window.AddControl(Controls.Error("Failed to load file"));

// Warning
window.AddControl(Controls.Warning("Disk space running low"));

// Info
window.AddControl(Controls.Info("Checking for updates..."));
```

### Formatted List

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("[bold yellow]Available Commands:[/]")
        .AddLine("")
        .AddLine("  [green]/help[/]     - Show this help message")
        .AddLine("  [green]/quit[/]     - Exit the application")
        .AddLine("  [green]/clear[/]    - Clear the screen")
        .AddLine("  [green]/settings[/] - Open settings")
        .Build()
);
```

### Dynamic Content Updates

```csharp
var status = Controls.Markup()
    .AddLine("[dim]Status: Ready[/]")
    .WithName("statusDisplay")
    .Build();

window.AddControl(status);

// Later, update the content
var statusControl = window.FindControl<MarkupControl>("statusDisplay");
if (statusControl != null)
{
    statusControl.SetContent(new List<string>
    {
        "[green]Status: Connected[/]",
        "[dim]Last updated: " + DateTime.Now.ToString("HH:mm:ss") + "[/]"
    });
}
```

### Progress Indicator

```csharp
void UpdateProgress(int percent, MarkupControl control)
{
    int barWidth = 30;
    int filled = (int)((percent / 100.0) * barWidth);
    string bar = new string('█', filled) + new string('░', barWidth - filled);

    control.SetContent(new List<string>
    {
        $"[bold]Progress:[/] {percent}%",
        $"[cyan]{bar}[/]"
    });
}

var progress = Controls.Markup()
    .WithName("progress")
    .Build();

window.AddControl(progress);

// Update progress
for (int i = 0; i <= 100; i += 10)
{
    var progressControl = window.FindControl<MarkupControl>("progress");
    if (progressControl != null)
    {
        UpdateProgress(i, progressControl);
    }
    await Task.Delay(100);
}
```

### Color-coded Logs

```csharp
var logDisplay = Controls.Markup()
    .WithName("logs")
    .Build();

window.AddControl(logDisplay);

// Add log entries
void AddLog(string level, string message)
{
    var logs = window.FindControl<MarkupControl>("logs");
    if (logs == null) return;

    var color = level switch
    {
        "ERROR" => "red",
        "WARN" => "yellow",
        "INFO" => "blue",
        "DEBUG" => "grey",
        _ => "white"
    };

    var timestamp = DateTime.Now.ToString("HH:mm:ss");
    var lines = logs.Lines.ToList();
    lines.Add($"[dim]{timestamp}[/] [{color}]{level}[/] {message}");

    // Keep only last 20 lines
    if (lines.Count > 20)
        lines.RemoveAt(0);

    logs.SetContent(lines);
}

AddLog("INFO", "Application started");
AddLog("WARN", "Configuration file not found");
AddLog("ERROR", "Failed to connect to database");
```

### Table-like Display

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("[bold]Name          Age    City[/]")
        .AddLine("[dim]─────────────────────────────────[/]")
        .AddLine("Alice         30     New York")
        .AddLine("Bob           25     London")
        .AddLine("Charlie       35     Tokyo")
        .Build()
);
```

### Banner/Header

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("[bold cyan on blue]                                        [/]")
        .AddLine("[bold cyan on blue]     MY APPLICATION - v2.0.0             [/]")
        .AddLine("[bold cyan on blue]                                        [/]")
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);
```

### Multi-column Text

```csharp
window.AddControl(
    Controls.HorizontalGrid()
        .Column(col => col.Add(
            Controls.Markup()
                .AddLine("[bold yellow]Left Column[/]")
                .AddLine("Content on")
                .AddLine("the left side")
                .Build()
        ))
        .Column(col => col.Add(
            Controls.Markup()
                .AddLine("[bold yellow]Middle Column[/]")
                .AddLine("Content in")
                .AddLine("the middle")
                .Build()
        ))
        .Column(col => col.Add(
            Controls.Markup()
                .AddLine("[bold yellow]Right Column[/]")
                .AddLine("Content on")
                .AddLine("the right side")
                .Build()
        ))
        .Build()
);
```

### Links/References

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("[bold]Documentation:[/]")
        .AddLine("")
        .AddLine("  [link]https://github.com/example/docs[/]")
        .AddLine("  [blue underline]User Guide[/]")
        .AddLine("  [blue underline]API Reference[/]")
        .Build()
);
```

## Helper Methods

### Set Content

```csharp
var markup = window.FindControl<MarkupControl>("myMarkup");
if (markup != null)
{
    markup.SetContent(new List<string>
    {
        "[green]New content[/]",
        "Line 2"
    });
}
```

### Set Single Line Content

```csharp
var markup = window.FindControl<MarkupControl>("myMarkup");
if (markup != null)
{
    markup.SetContent("[yellow]Single line update[/]");
}
```

### Append Content

```csharp
var markup = window.FindControl<MarkupControl>("myMarkup");
if (markup != null)
{
    var lines = markup.Lines.ToList();
    lines.Add("[green]New line appended[/]");
    markup.SetContent(lines);
}
```

### Clear Content

```csharp
var markup = window.FindControl<MarkupControl>("myMarkup");
if (markup != null)
{
    markup.SetContent(new List<string>());
}
```

## Best Practices

1. **Use semantic helpers**: Prefer `Controls.Header()`, `Controls.Info()`, etc. for common patterns
2. **Don't over-format**: Too many colors/styles can be distracting
3. **Consistent colors**: Use same colors for same meaning (red = error, green = success)
4. **Test readability**: Ensure text is readable with different themes
5. **Escape brackets**: Use `[[` and `]]` to display literal brackets
6. **Keep lines reasonable**: Very long lines may cause wrapping issues
7. **Update efficiently**: Use `SetContent()` instead of recreating controls

## Common Patterns

### Title and Description

```csharp
window.AddControl(
    Controls.Header("Application Settings")
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);

window.AddControl(
    Controls.Label("Configure your preferences below")
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);
```

### Status Bar

```csharp
window.AddControl(
    Controls.Label("[dim]Ready | ESC: Exit | F1: Help[/]")
        .StickyBottom()
        .WithAlignment(HorizontalAlignment.Center)
        .Build()
);
```

### Error Display

```csharp
window.AddControl(
    Controls.Markup()
        .AddLine("[bold red]Error:[/]")
        .AddLine("")
        .AddLine("Failed to connect to server.")
        .AddLine("[dim]Please check your network connection.[/]")
        .Build()
);
```

## Color Reference

Common colors available in Spectre.Console:

- **Basic**: red, green, blue, yellow, cyan, magenta, white, black
- **Grays**: grey, grey0-grey100 (0=black, 100=white)
- **Extended**: orange, orange3, purple, lime, aqua, fuchsia, maroon, navy, olive, teal, silver
- **Custom**: rgb(r,g,b) or #RRGGBB

## See Also

- [FigleControl](FigleControl.md) - For large ASCII art text
- [SpectreRenderableControl](SpectreRenderableControl.md) - For Spectre.Console widgets
- [LogViewerControl](LogViewerControl.md) - For log display with filtering

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

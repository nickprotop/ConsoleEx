# PromptControl

Single-line text input with readline-style editing, history, selection, clipboard, and tab completion.

## Overview

PromptControl provides a labeled text input field with rich editing capabilities including cursor navigation, word-level operations, text selection, clipboard support, command history, and tab completion. Supports password masking, horizontal scrolling, and mouse click-to-cursor.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Prompt` | `string?` | `"> "` | Label text displayed before the input area (supports markup) |
| `Input` | `string` | `""` | Current input text |
| `MaskCharacter` | `char?` | `null` | Display character for password fields |
| `InputWidth` | `int?` | `null` | Input field width (auto-computed from available space if null) |
| `UnfocusOnEnter` | `bool` | `true` | Whether focus leaves the control on Enter |
| `HistoryEnabled` | `bool` | `false` | Enable Up/Down arrow command recall |
| `TabCompleter` | `Func<string, int, IEnumerable<string>?>?` | `null` | Tab completion delegate |
| `InputBackgroundColor` | `Color?` | Theme | Background when unfocused |
| `InputFocusedBackgroundColor` | `Color?` | Theme | Background when focused |
| `InputForegroundColor` | `Color?` | Theme | Foreground when unfocused |
| `InputFocusedForegroundColor` | `Color?` | Theme | Foreground when focused |
| `IsEnabled` | `bool` | `true` | Whether the control accepts input |
| `HasSelection` | `bool` | (read-only) | Whether text is currently selected |
| `SelectedText` | `string?` | (read-only) | The selected text, or null |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `Entered` | `string` | Enter key pressed — provides the input text |
| `InputChanged` | `string` | Input text changed (typing, paste, delete) |

## Creating PromptControl

### Using Builder (Recommended)

```csharp
var prompt = Controls.Prompt("Search: ")
    .WithHistory()
    .WithMaskCharacter('*')  // password field
    .OnEntered((sender, text) => Console.WriteLine($"You entered: {text}"))
    .Build();
```

### Using Constructor

```csharp
var prompt = new PromptControl
{
    Prompt = "Enter name: ",
    UnfocusOnEnter = false,
    HistoryEnabled = true
};
prompt.Entered += (sender, text) => ProcessInput(text);
```

## Keyboard Support

### Navigation

| Key | Action |
|-----|--------|
| `Left Arrow` | Move cursor left |
| `Right Arrow` | Move cursor right |
| `Home` / `Ctrl+A` | Move to start (Ctrl+A selects all) |
| `End` / `Ctrl+E` | Move to end |
| `Ctrl+Left` | Move word left |
| `Ctrl+Right` | Move word right |

### Editing

| Key | Action |
|-----|--------|
| `Backspace` | Delete character left (or delete selection) |
| `Delete` | Delete character right (or delete selection) |
| `Ctrl+K` | Kill from cursor to end of line |
| `Ctrl+U` | Kill from start to cursor |
| `Ctrl+W` | Kill word backward |

### Selection & Clipboard

| Key | Action |
|-----|--------|
| `Shift+Left/Right` | Extend selection |
| `Shift+Home/End` | Extend selection to start/end |
| `Ctrl+A` | Select all |
| `Ctrl+C` | Copy selection to clipboard |
| `Ctrl+V` | Paste from clipboard |
| `Ctrl+X` | Cut selection to clipboard |

### History & Completion

| Key | Action |
|-----|--------|
| `Up Arrow` | Previous history entry (when HistoryEnabled) |
| `Down Arrow` | Next history entry |
| `Tab` | Trigger tab completion (when TabCompleter is set) |
| `Enter` | Submit input (fires Entered, adds to history) |
| `Escape` | Clear focus |

## Mouse Support

| Action | Result |
|--------|--------|
| Click | Focus control and position cursor at clicked character |

## Tab Completion

Set a `TabCompleter` delegate that returns completion candidates:

```csharp
var prompt = Controls.Prompt("$ ")
    .WithTabCompleter((input, cursorPos) =>
    {
        var commands = new[] { "help", "exit", "clear", "history" };
        return commands.Where(c => c.StartsWith(input));
    })
    .WithHistory()
    .Build();
```

When Tab is pressed:
- **Single match**: auto-completes the input
- **Multiple matches**: inserts the longest common prefix
- **No matches**: Tab passes through to focus traversal (no trap)

## Examples

### Password Input

```csharp
var password = Controls.Prompt("Password: ")
    .WithMaskCharacter('●')
    .OnEntered((_, pwd) => Authenticate(pwd))
    .Build();
```

### Command Line with History

```csharp
var cli = Controls.Prompt("$ ")
    .WithHistory()
    .UnfocusOnEnter(false)
    .OnEntered((sender, cmd) =>
    {
        ExecuteCommand(cmd);
        ((PromptControl)sender!).SetInput("");
    })
    .Build();
```

### URL Bar

```csharp
var addressBar = Controls.Prompt($"{icon} ")
    .UnfocusOnEnter(false)
    .WithAlignment(HorizontalAlignment.Stretch)
    .OnEntered(async (sender, url) =>
    {
        await htmlControl.LoadUrlAsync(url);
    })
    .Build();
```

## Best Practices

- Use `UnfocusOnEnter(false)` for controls where the user types multiple inputs (command lines, search bars)
- Use `WithHistory()` for command-line interfaces
- Use `WithMaskCharacter('●')` for password fields
- Tab completion returns `false` (passes through) when no matches — the user is never trapped
- `Ctrl+A` selects all text; typing with a selection replaces it

## See Also

- [MultilineEditControl](MultilineEditControl.md) — for multi-line text editing
- [Controls Reference](../CONTROLS.md) — complete control listing

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

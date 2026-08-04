# PromptControl

Text input with readline-style editing, history, selection, clipboard, and tab completion.
Single-line by default; optionally wraps and grows.

## Overview

PromptControl provides a labeled text input field with rich editing capabilities including cursor
navigation, word-level operations, text selection, clipboard support, command history, and tab
completion. Supports password masking, placeholder text, length limits, and mouse selection.

By default it is a **single-line** field: it measures one row and scrolls horizontally when the text
outgrows it. Set [`Multiline`](#multiline) and it soft-wraps instead, growing between `MinRows` and
`MaxRows` — a command line, chat composer, or comment box without leaving the control. The value is a
single string in both modes, so code that binds to `Input` does not care which mode it is in.

> **Upgrading:** every property below defaults to the behaviour PromptControl had before multiline
> existed. A prompt you already ship renders identically until you opt in.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Prompt` | `string?` | `"> "` | Label text displayed before the input area (supports markup) |
| `Input` | `string` | `""` | Current input text |
| `Placeholder` | `string?` | `null` | Hint shown while the value is empty; never part of `Input` |
| `MaxLength` | `int?` | `null` | Maximum value length in characters; `null` is unlimited |
| `ReadOnly` | `bool` | `false` | Value cannot be edited, but is still focusable, navigable and copyable |
| `Multiline` | `bool` | `false` | Allow newlines, wrap the text, and grow the box |
| `MinRows` | `int` | `1` | Minimum measured rows when `Multiline` (ignored otherwise) |
| `MaxRows` | `int` | `6` | Maximum measured rows when `Multiline`; taller content scrolls |
| `EnterBehavior` | `EnterBehavior` | `Submit` | Whether Enter submits or inserts a newline |
| `MaskCharacter` | `char?` | `null` | Display character for password fields |
| `InputWidth` | `int?` | `null` | Input field width (auto-computed from available space if null) |
| `UnfocusOnEnter` | `bool` | `true` | Whether focus leaves the control on Enter |
| `HistoryEnabled` | `bool` | `false` | Enable Up/Down arrow command recall |
| `MaxHistoryEntries` | `int` | `500` | History cap; oldest entries are dropped past it |
| `TabCompleter` | `Func<string, int, IEnumerable<string>?>?` | `null` | Tab completion delegate |
| `InputBackgroundColor` | `Color?` | Theme | Background when unfocused |
| `InputFocusedBackgroundColor` | `Color?` | Theme | Background when focused |
| `InputForegroundColor` | `Color?` | Theme | Foreground when unfocused |
| `InputFocusedForegroundColor` | `Color?` | Theme | Foreground when focused |
| `IsEnabled` | `bool` | `true` | Whether the control accepts input at all |
| `HasSelection` | `bool` | (read-only) | Whether text is currently selected |
| `SelectedText` | `string?` | (read-only) | The selected text, or null |

`ReadOnly` and `IsEnabled` are different refusals: a read-only prompt looks normal and lets the user
move through and copy the text, while a disabled one refuses everything and paints disabled.

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `Entered` | `string` | Enter pressed (per `EnterBehavior`) — provides the input text |
| `InputChanged` | `string` | Input text changed (typing, paste, delete) |
| `MouseClick` | `MouseEventArgs` | Primary button clicked |
| `MouseDoubleClick` | `MouseEventArgs` | Double click (also selects the word under the pointer) |
| `MouseRightClick` | `MouseEventArgs` | Secondary button clicked |
| `MouseEnter` / `MouseLeave` | `MouseEventArgs` | Pointer entered or left the control |
| `MouseMove` | `MouseEventArgs` | Pointer moved over the control |

## Creating PromptControl

### Using Builder (Recommended)

```csharp
var prompt = Controls.Prompt("Search: ")
    .WithHistory()
    .WithPlaceholder("type to search")
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

## Multiline

Turn it on and the value may contain newlines, wraps at the field width, and the control measures
between `MinRows` and `MaxRows` rows. Content taller than `MaxRows` scrolls vertically, keeping the
caret in view. Every wrapped row carries a hanging indent under the prompt, so the block has one
straight left edge.

```csharp
var composer = Controls.Prompt("> ")
    .Multiline()
    .WithRows(1, 8)              // start at one row, grow to eight
    .UnfocusOnEnter(false)
    .OnEntered((sender, text) =>
    {
        Send(text);
        ((PromptControl)sender!).SetInput("");
    })
    .Build();
```

Wrapping is shared with [MultilineEditControl](MultilineEditControl.md) — both measure in display
columns, so CJK and emoji break where they actually render.

### What Enter does

`EnterBehavior` decides, and it defaults to `Submit` so turning on `Multiline` never silently
changes what the key means.

| `EnterBehavior` | `Enter` | Newline chord | Submit chord |
|---|---|---|---|
| `Submit` (default) | Submits | `Alt+Enter`, `Ctrl+L` | `Enter` |
| `InsertNewline` | Inserts a newline | `Enter` | `Ctrl+Enter` |

> **On Unix, `Shift+Enter` and `Ctrl+Enter` are indistinguishable from `Enter`.** No terminal reports
> them distinctly without CSI-u or `modifyOtherKeys`, neither of which the Unix input parser enables.
> They are accepted where the host does report them (the Windows console does), but never required —
> which is why the newline chord is `Alt+Enter`, with `Ctrl+L` as a spelling that needs no
> reassembly. It also means `EnterBehavior.InsertNewline` leaves a control with no keyboard submit on
> Linux; submit from a button there, or keep the default.

## Keyboard Support

### Navigation

| Key | Action |
|-----|--------|
| `Left Arrow` | Move cursor left |
| `Right Arrow` | Move cursor right |
| `Up` / `Down` | Move between wrapped rows (multiline); history recall at the first/last row |
| `Home` / `End` | Start/end of the row (multiline) or of the value (single-line) |
| `Ctrl+Home` / `Ctrl+End` | Start/end of the whole value |
| `Ctrl+A` | Select all |
| `Ctrl+E` | Move to end |
| `Ctrl+Left` | Move word left |
| `Ctrl+Right` | Move word right |

### Editing

| Key | Action |
|-----|--------|
| `Backspace` | Delete character left (or delete selection) |
| `Delete` | Delete character right (or delete selection) |
| `Ctrl+K` | Kill from cursor to end of line |
| `Ctrl+U` | Kill from start of line to cursor |
| `Ctrl+W` | Kill word backward |
| `Alt+Enter` / `Ctrl+L` | Insert a newline (multiline, `EnterBehavior.Submit`) |

`Ctrl+K` and `Ctrl+U` act on the current logical line in multiline mode, and on the whole value in
single-line mode.

### Selection & Clipboard

| Key | Action |
|-----|--------|
| `Shift+Left/Right` | Extend selection |
| `Shift+Home/End` | Extend selection to start/end |
| `Shift+Ctrl+Left/Right` | Extend selection by word |
| `Ctrl+A` | Select all |
| `Ctrl+C` | Copy selection to clipboard |
| `Ctrl+V` | Paste from clipboard |
| `Ctrl+X` | Cut selection to clipboard |

> Copy/paste works locally and over SSH. The control implements `IPasteTarget`. Paste is
> **mode-dependent**: single-line flattens newlines to spaces, multiline inserts them verbatim.
> Pasted text longer than `MaxLength` is truncated to fit rather than rejected. See
> [Clipboard, Copy & Paste](../CLIPBOARD.md) for the OSC 52 remote-clipboard behavior.

### History & Completion

| Key | Action |
|-----|--------|
| `Up Arrow` | Previous history entry (when HistoryEnabled) |
| `Down Arrow` | Next history entry |
| `Tab` | Trigger tab completion (when TabCompleter is set) |
| `Enter` | Submit input (fires Entered, adds to history) |
| `Escape` | Clear focus |

History skips consecutive duplicates and is capped at `MaxHistoryEntries`, so a long-running command
line does not accumulate every line ever typed.

## Mouse Support

| Action | Result |
|--------|--------|
| Click | Focus control and position cursor at clicked character |
| Click and drag | Select a range |
| Double click | Select the word under the pointer |
| Right click | Raises `MouseRightClick` |

Positions map in **display columns**, so clicking the right half of a wide character lands on that
character rather than past it.

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
    .WithMaxLength(64)
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

### Chat Composer That Grows

```csharp
var composer = Controls.Prompt("> ")
    .Multiline()
    .WithRows(1, 6)
    .WithPlaceholder("Message… (Alt+Enter for a new line)")
    .WithMaxLength(4000)
    .UnfocusOnEnter(false)
    .OnEntered((sender, text) =>
    {
        if (!string.IsNullOrWhiteSpace(text)) Send(text);
        ((PromptControl)sender!).SetInput("");
    })
    .Build();
```

### URL Bar

```csharp
var addressBar = Controls.Prompt($"{icon} ")
    .UnfocusOnEnter(false)
    .WithPlaceholder("Enter a URL")
    .WithAlignment(HorizontalAlignment.Stretch)
    .OnEntered(async (sender, url) =>
    {
        await htmlControl.LoadUrlAsync(url);
    })
    .Build();
```

### Read-only Field

```csharp
var apiKey = Controls.Prompt("API key: ")
    .WithInput(key)
    .ReadOnly()
    .Build();   // selectable and copyable, not editable
```

## Best Practices

- Use `UnfocusOnEnter(false)` for controls where the user types multiple inputs (command lines, search bars)
- Use `Multiline()` with `WithRows(1, n)` for composers that should start small and grow
- Keep `EnterBehavior.Submit` unless the control is genuinely a text area — it is the only mode with a
  keyboard submit on Linux
- Use `WithHistory()` for command-line interfaces
- Use `WithMaskCharacter('●')` for password fields
- Use `WithPlaceholder(...)` rather than pre-filling `Input` with hint text, which the user would have
  to delete and which would be submitted if they did not
- Tab completion returns `false` (passes through) when no matches — the user is never trapped
- `Ctrl+A` selects all text; typing with a selection replaces it

## See Also

- [MultilineEditControl](MultilineEditControl.md) — full text editor: line numbers, find/replace, undo
- [Controls Reference](../CONTROLS.md) — complete control listing

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)

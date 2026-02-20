# TerminalControl

PTY-backed terminal emulator control that embeds a fully interactive shell or process inside any window.

> **Platform:** Linux only. The control throws `PlatformNotSupportedException` on other operating systems.

## Overview

`TerminalControl` opens a real pseudo-terminal (PTY), spawns a child process inside it, and renders the VT100/xterm-256color screen directly into the SharpConsoleUI buffer. Keyboard and mouse input are forwarded to the process. The terminal resizes automatically when the window is resized.

## Critical Setup Requirement

`TerminalControl` relies on an in-process PTY shim — the host executable re-launches itself as the slave-side process. **You must add the following as the very first line of your `Main` method**, before any UI initialisation:

```csharp
// Program.cs
if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;
```

Without this line the PTY will open but the child process will never start, leaving the terminal blank.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Title` | `string` | `"  Terminal — <exe>"` | Window title derived from the launched executable |
| `HasFocus` | `bool` | `true` | Whether the control has keyboard focus |
| `IsEnabled` | `bool` | `true` | Enable/disable keyboard and mouse input forwarding |
| `Visible` | `bool` | `true` | Show/hide the control |
| `Container` | `IContainer?` | `null` | Set by the window when the control is added |
| `Margin` | `Margin` | `0,0,0,0` | Layout margin around the control |
| `HorizontalAlignment` | `HorizontalAlignment` | `Stretch` | Fills available width |
| `VerticalAlignment` | `VerticalAlignment` | `Fill` | Fills available height |

## Creating a Terminal

### Using the Builder — Quick Open (Recommended)

```csharp
// Open a bash terminal in a new auto-sized window
Controls.Terminal().Open(ws);

// Specify an explicit size (cols × rows)
Controls.Terminal().Open(ws, width: 120, height: 40);

// Open a different program
Controls.Terminal("/usr/bin/htop").Open(ws);

// Pass arguments
Controls.Terminal("/usr/bin/vim")
    .WithArgs("/etc/hosts")
    .Open(ws, width: 100, height: 35);
```

`Open` creates the `TerminalControl`, wraps it in a centered closable window, and registers it with the window system. When the child process exits the window closes automatically.

### Using the Builder — Manual Window Wiring

Use `Build()` when you need full control over the window configuration:

```csharp
var terminal = Controls.Terminal("/bin/bash").Build();

var window = new WindowBuilder(ws)
    .WithTitle(terminal.Title)
    .WithSize(82, 26)
    .Centered()
    .Closable(true)
    .Resizable(true)
    .AddControl(terminal)
    .Build();

ws.AddWindow(window);
```

### Using TerminalBuilder Directly

```csharp
var terminal = new TerminalBuilder()
    .WithExe("/bin/bash")
    .Build();
```

## TerminalBuilder Methods

| Method | Description |
|--------|-------------|
| `WithExe(string exe)` | Set the executable to launch (default: `/bin/bash`) |
| `WithArgs(params string[] args)` | Pass arguments to the executable |
| `Build()` | Create the `TerminalControl` (PTY is opened immediately) |
| `Open(ConsoleWindowSystem ws, int? width, int? height)` | Build and open in a new centered window |

### Open Method Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ws` | `ConsoleWindowSystem` | required | The window system to open the terminal in |
| `width` | `int?` | `null` | Terminal columns. When null: desktop width − 6, minimum 60 |
| `height` | `int?` | `null` | Terminal rows. When null: desktop height − 6, minimum 20 |

## Keyboard Support

All standard xterm-256color key sequences are encoded and forwarded to the PTY.

| Key | Sequence sent |
|-----|---------------|
| **Printable chars** | UTF-8 bytes |
| **Enter** | `CR` (0x0D) |
| **Backspace** | DEL (0x7F) |
| **Tab** | `HT` (0x09) |
| **Escape** | `ESC` (0x1B) |
| **Arrow keys** | `ESC[A/B/C/D` or `ESCO A/B/C/D` (application cursor mode) |
| **Home / End** | `ESCOH` / `ESCOF` |
| **Page Up / Down** | `ESC[5~` / `ESC[6~` |
| **Delete / Insert** | `ESC[3~` / `ESC[2~` |
| **F1–F12** | Standard xterm sequences |
| **Ctrl+C/D/Z/L/A/E/U/W/K/R** | Corresponding control bytes |

## Mouse Support

Mouse events are forwarded when the running application enables mouse reporting (e.g. `vim`, `htop`, `ncurses` apps).

| Event | Description |
|-------|-------------|
| Button press/release | Button 1–3 press and release |
| Scroll wheel | Wheel up/down forwarded as buttons 64/65 |
| Drag | Button drag in button-event (1002) and any-event (1003) modes |
| Mouse move | Position reporting in any-event mode (1003) |

Both X10 and SGR (1006) mouse encoding protocols are supported, selected by what the child process requests.

## Lifecycle

1. **Constructor** — PTY is opened, shim process is started, read thread begins.
2. **PaintDOM** — Terminal is resized to match the layout bounds on the first paint and on every subsequent resize.
3. **Child process exits** — Read thread detects EOF, closes the master fd, waits for the shim to exit, then closes the containing window automatically.
4. **Dispose** — Closes the master fd, which signals the child process to terminate.

## Examples

### Default Bash Terminal

```csharp
// Program.cs — REQUIRED before any UI code
if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

// ...

Controls.Terminal().Open(ws);
```

### Specific Program

```csharp
Controls.Terminal("/usr/bin/htop").Open(ws);
```

### Custom Size

```csharp
Controls.Terminal().Open(ws, width: 132, height: 43);
```

### Vim Editing a File

```csharp
Controls.Terminal("/usr/bin/vim")
    .WithArgs("/etc/hosts")
    .Open(ws, width: 100, height: 40);
```

### Terminal Alongside Other Controls

```csharp
var terminal = Controls.Terminal("/bin/bash").Build();

var window = new WindowBuilder(ws)
    .WithTitle("Debug Console")
    .WithSize(100, 35)
    .AtPosition(2, 2)
    .Resizable(true)
    .Closable(true)
    .AddControl(terminal)
    .Build();

ws.AddWindow(window);
```

### Keyboard Shortcut to Open Terminal

```csharp
ws.GlobalKeyPressed += (sender, e) =>
{
    if (e.KeyInfo.Key == ConsoleKey.F7)
    {
        Controls.Terminal().Open(ws);
        e.Handled = true;
    }
};
```

## How It Works

`TerminalControl` uses a **in-process PTY shim** pattern to avoid the need for a separate helper binary:

1. The host process calls `PtyNative.Open()` to create a PTY master/slave pair.
2. It re-launches **itself** (`Environment.ProcessPath`) with `--pty-shim <slaveFd> <exe> [args]` as arguments.
3. `PtyShim.RunIfShim` detects these arguments, calls `setsid`/`ioctl(TIOCSCTTY)`, redirects stdin/stdout/stderr to the slave fd, and `exec`s the target executable — replacing the shim process entirely.
4. The original process reads from the master fd in a background thread, feeds bytes to the `VT100Machine`, and calls `Invalidate` after each read so the UI repaints.
5. Keyboard/mouse input is written back to the master fd as escape sequences.

This design requires no external binaries and works with any process that supports a TTY.

## Best Practices

- **Always add `PtyShim.RunIfShim(args)` first** — it must run before any console or UI initialisation.
- **Let the window close itself** — when the child exits, the window closes automatically; do not force-close it from outside.
- **Prefer `Open()`** for simple cases; use `Build()` only when you need custom window configuration.
- **Do not set `HasFocus = false`** while the terminal is the only control — keyboard input will stop being forwarded.

## See Also

- [TerminalBuilder](#terminalbuilder-methods) — Fluent API for creating terminals
- [WindowBuilder](../BUILDERS.md#windowbuilder) — Manual window configuration
- [Controls Static Factory](../BUILDERS.md#controls-static-factory) — `Controls.Terminal()`

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

# Shell Scripting & Pipeline Usage

SharpConsoleUI apps work correctly inside shell pipelines. You can pipe data in through stdin, read it before showing the UI, then write results to stdout after the UI closes — all while the interactive window renders on the real terminal.

This makes SharpConsoleUI a drop-in choice for interactive pickers, confirmations, and wizards in scripts, similar to tools like `fzf`, `gum`, and `dialog`.

> **Platform scope: Linux and macOS.** Piped stdin/stdout is not supported on Windows — see
> [Windows: not supported](#windows-not-supported).

## How It Works

On Unix, when `NetConsoleDriver` starts up it calls `isatty(0)` and `isatty(1)` to detect whether stdin and stdout are real terminals.

| stdin | stdout | What the driver does |
|---|---|---|
| TTY | TTY | Uses fd 0 and fd 1 directly (fast path, no overhead) |
| pipe/file | TTY | Opens `/dev/tty` as RDWR, routes all UI I/O through it |
| TTY | pipe/file | Same — opens `/dev/tty` for UI I/O |
| pipe/file | pipe/file | Same — pipes stay free for the script's data |

When `/dev/tty` is used, the script's stdin and stdout remain untouched — they carry your data, not terminal escape codes. Keyboard input is read from `/dev/tty`, and UI frames are written to `/dev/tty`. The pipeline sees only the data you explicitly write with `Console.Out.WriteLine(...)` after the UI closes.

If no controlling terminal is available (e.g., a systemd service with no TTY allocated, or `setsid` with redirected streams), `EnterRawMode()` returns `false` and the driver falls back to ConsolePal. A TUI cannot render in that environment — this is an expected and graceful failure.

### Windows: not supported

**Pipelines are a Unix and macOS feature.** On Windows, `NetConsoleDriver` throws
`PlatformNotSupportedException` at construction when stdin or stdout is redirected.

The reason is structural rather than an oversight. On Unix the driver opens `/dev/tty` and writes UI
frames to that file descriptor directly, leaving the standard streams free — which is what makes the
whole pattern on this page work. On Windows it renders and reads input through the managed
`Console` APIs (`ReadKey`, `KeyAvailable`, `Write`, the cursor and buffer APIs), and every one of
those acts on the process's *standard* handles. Redirect them and they refer to a pipe or a file:
input cannot be read, and UI output would be written into your data stream.

Supporting it properly means giving Windows its own device-level I/O path — reading via
`ReadConsoleInput` on `CONIN$` and writing to `CONOUT$` — which is a separate backend rather than a
patch. Until that exists the driver refuses immediately, with a message naming the workarounds,
instead of failing later from an unrelated-looking cursor call or silently painting escape sequences
into your output file.

Redirecting **stderr** is fine on every platform: `2> log.txt` works, and the driver only ever
writes there.

If you need to consume piped data in a Windows TUI, read stdin yourself *before* constructing the
window system, and run the UI without redirection.

## Writing a Pipeline-Friendly App

The golden rule: **read piped stdin before `windowSystem.Run()`, write results after it**.

`ConsoleWindowSystem` does this automatically — piped stdin is captured from construction onward and available via `PipedInput` / `PipedLines` throughout the app lifecycle:

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

// Piped stdin is automatically captured at construction.
var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// system.PipedInput  — full text (null if stdin is a TTY)
// system.PipedLines  — split into lines (null if stdin is a TTY)

string? selection = null;
var items = system.PipedLines ?? Array.Empty<string>();
var window = new WindowBuilder(system)
    .WithTitle("Pick an item")
    .WithSize(60, 15)
    .AddControl(Controls.List()
        .AddItems(items)
        .OnItemActivated((sender, item, win) =>
        {
            selection = item.Text;
            system.Shutdown();
        })
        .Build())
    .Build();

system.AddWindow(window);
system.Run();

// 3. Write the result to stdout after shutdown.
if (selection != null)
{
    Console.Out.WriteLine(selection);
    Environment.Exit(0);
}
else
{
    Environment.Exit(1); // user cancelled
}
```

### PipedInput / PipedLines Properties

| Property | Type | Description |
|----------|------|-------------|
| `PipedInput` | `string?` | The full text piped via stdin. `null` when stdin is a TTY (normal interactive run), and **always `null` on Windows**, where redirected stdio is not supported. |
| `PipedLines` | `string[]?` | `PipedInput` split by newline. Same `null` rules. |

Piped stdin is captured automatically, starting at construction — before the driver takes over the terminal. The data is available throughout the entire app lifecycle, including inside event handlers, async threads, and after `Run()` returns.

### Slow and never-ending stdin

The capture runs in the background, so **constructing the system never blocks**. This matters because redirected stdin has no single correct deadline: `echo x | app` ends immediately, but `tail -f log | app` is designed never to end, and a parent process that spawns your app with an open stdin pipe may never close it.

For an ordinary finite pipe the capture finishes in well under a millisecond, so nothing below is ever observable — `PipedInput` returns the complete text just as it always has.

When input is still arriving, two things bound the wait:

- **Before `Run()`** — reading `PipedInput` waits up to `PreUiTimeoutMs` (default 2s), then returns the text received so far. No UI exists yet to report a longer wait, so the wait is bounded and partial data is returned rather than blocking startup.
- **After the UI is up** — if the capture is still running, a cancellable **"Reading input"** dialog appears (after a short `DialogDelayMs` grace period, so it never flashes for fast input). The user can wait or press Cancel; cancelling leaves `PipedInput` holding whatever arrived.

Tune or disable any of this via `PipedInputOptions`:

```csharp
var options = new ConsoleWindowSystemOptions() with
{
    PipedInput = new PipedInputOptions(
        PreUiTimeoutMs: 500,      // shorter pre-UI bound
        ShowDialog: false)        // capture silently in the background
};
var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer), options: options);
```

Set `Enabled: false` to leave stdin entirely to your application, in which case `PipedInput` is always `null`.

You can also read stdin manually before constructing the system if you prefer:

```csharp
string? input = null;
if (Console.IsInputRedirected)
    input = Console.In.ReadToEnd();

var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
// 'input' has the piped data, system.PipedInput also has it (captured independently)
```

### I/O Contract

Follow these conventions so your app composes cleanly with other shell tools:

- **stdin:** plain lines, or JSON — read it *before* `windowSystem.Run()`
- **stdout:** the result, one line or a JSON blob — written *after* `windowSystem.Run()` returns
- **stderr:** error messages only, never mixed with stdout
- **Exit codes:**
  - `0` — user confirmed / made a selection
  - `1` — user cancelled (Esc, Ctrl+C, empty selection)
  - `2` — invalid input / validation error
  - `>2` — unexpected error

These conventions match `fzf`, `gum`, and similar tools, so shell users get predictable behavior.

## Shell Examples

### Bash / Zsh

```bash
# Pipe a list into a picker, capture the selection
selected=$(ls /etc | my-picker)
if [ $? -eq 0 ]; then
    echo "User picked: $selected"
fi

# Multi-stage pipeline
git branch --list | my-picker | xargs git checkout
```

### PowerShell

```powershell
# Pipe objects via JSON
$service = Get-Service |
    ConvertTo-Json |
    dotnet run my-table-picker.cs |
    ConvertFrom-Json

if ($service) {
    Restart-Service $service.Name
}
```

### Fish

```fish
set branch (git branch --list | my-picker)
test $status -eq 0; and git checkout $branch
```

### Nushell

```nu
ls | to json | my-table-picker | from json
```

## .NET 10 File-Based Apps

Starting with .NET 10, you can write a single-file C# script with a `#:package` directive and run it via `dotnet run script.cs` — no project file needed. Combined with `/dev/tty` support, this makes SharpConsoleUI ideal for one-off interactive scripts:

```csharp
#!/usr/bin/env dotnet
#:package SharpConsoleUI@2.4.54

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

var items = Console.IsInputRedirected
    ? Console.In.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
    : args;

var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
string? choice = null;

var window = new WindowBuilder(system)
    .WithTitle("Pick")
    .WithSize(50, Math.Min(20, items.Length + 4))
    .AddControl(Controls.List()
        .AddItems(items)
        .OnItemActivated((s, item, w) => { choice = item.Text; system.Shutdown(); })
        .Build())
    .Build();

system.AddWindow(window);
system.Run();

if (choice != null) { Console.Out.WriteLine(choice); return 0; }
return 1;
```

Save as `pick.cs`, then:

```bash
chmod +x pick.cs   # Unix only
echo -e "alpha\nbeta\ngamma" | ./pick.cs
# or
echo -e "alpha\nbeta\ngamma" | dotnet run pick.cs
```

## Ready-to-Use Templates

Instead of writing a script from scratch, you can copy one of the templates from [`docs/scripting/templates/`](https://github.com/nickprotop/ConsoleEx/blob/master/docs/scripting/templates/). Each template is a self-contained `.cs` file that follows the I/O contract above.

| Template | Purpose |
|---|---|
| [picker.cs](https://github.com/nickprotop/ConsoleEx/blob/master/docs/scripting/templates/picker.cs) | Single-select list picker (stdin lines → stdout line) |
| [multi-picker.cs](https://github.com/nickprotop/ConsoleEx/blob/master/docs/scripting/templates/multi-picker.cs) | Multi-select checklist |
| [confirm.cs](https://github.com/nickprotop/ConsoleEx/blob/master/docs/scripting/templates/confirm.cs) | Yes/no dialog (exit code only) |
| [prompt.cs](https://github.com/nickprotop/ConsoleEx/blob/master/docs/scripting/templates/prompt.cs) | Text input with optional password masking |
| [table-select.cs](https://github.com/nickprotop/ConsoleEx/blob/master/docs/scripting/templates/table-select.cs) | JSON array row picker |
| [progress.cs](https://github.com/nickprotop/ConsoleEx/blob/master/docs/scripting/templates/progress.cs) | Wraps a subprocess with an indeterminate progress bar |

See the [scripting guide](scripting/README.md) for usage details and per-shell recipes (bash, PowerShell, fish, nushell).

## Troubleshooting

**UI bytes leak into the pipe output**
Something in your app wrote to `Console.Out` while the TUI was running. Move all data output to *after* `windowSystem.Run()` returns.

**Terminal is stuck in raw mode after script exits**
`RestoreTerminal()` did not run. Make sure exceptions propagate through `windowSystem.Run()` so the driver's cleanup path fires. Running `reset` will restore your shell.

**App hangs when invoked via `setsid` or `cron`**
No controlling terminal — `/dev/tty` cannot be opened. This is expected. The driver falls back to ConsolePal but a TUI cannot render without a terminal. Detect this case in your app and exit early:

```csharp
if (!Environment.UserInteractive || Console.IsInputRedirected && Console.IsOutputRedirected)
{
    // headless mode — consider a non-interactive fallback
}
```

**Pipeline works on Linux/macOS but not Windows**
It is not supported on Windows — see *Windows: not supported* above. The driver throws
`PlatformNotSupportedException` when stdin or stdout is redirected. Read stdin yourself before
constructing the window system, and run the UI without redirection.

Note also that PowerShell's `|` and `>` operate on its own object pipeline rather than the
process's stdio handles, so they do not redirect the way `cmd.exe` and Unix shells do.

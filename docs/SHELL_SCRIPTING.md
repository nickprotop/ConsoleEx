# Shell Scripting & Pipeline Usage

SharpConsoleUI apps work correctly inside shell pipelines. You can pipe data in through stdin, read it before showing the UI, then write results to stdout after the UI closes — all while the interactive window renders on the real terminal.

This makes SharpConsoleUI a drop-in choice for interactive pickers, confirmations, and wizards in scripts, similar to tools like `fzf`, `gum`, and `dialog`.

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

On Windows, the standard Win32 console APIs handle redirection themselves, so no special code path is needed.

## Writing a Pipeline-Friendly App

The golden rule: **read piped stdin before `windowSystem.Run()`, write results after it**.

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

// 1. Read piped data before taking over the terminal.
string? input = null;
if (Console.IsInputRedirected)
    input = Console.In.ReadToEnd();

// 2. Build and run the UI.
var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

string? selection = null;
var window = new WindowBuilder(system)
    .WithTitle("Pick an item")
    .WithSize(60, 15)
    .AddControl(Controls.List()
        .AddItems((input ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
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
Windows file-based apps don't honor the `#!/usr/bin/env dotnet` shebang. Invoke via `dotnet run script.cs` explicitly in PowerShell.

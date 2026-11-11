# Logging

A TUI cannot print. The screen is the output, so `Console.WriteLine` writes over the thing
it is reporting on, and `builder.AddConsole()` does it on every entry. That is the whole
reason this library carries a log service of its own.

It does four things: it records what the framework is doing, it holds those entries in a
buffer a control can display, it writes them to a file when asked, and it bridges to
`Microsoft.Extensions.Logging` in both directions so an application's existing pipeline and
this one are not two separate worlds.

```csharp
windowSystem.LogService.LogInfo("Application started");
```

## The three filters, and why entries go missing

Almost every "my logs are not showing" question is one of these three being confused for
another. They apply in this order, and an entry has to pass all of them.

| | What it decides | Where it lives | Default |
|---|---|---|---|
| **Capture** | Whether the entry is RECORDED at all | `ILogService.MinimumLevel` | `Warning` |
| **View** | Whether a recorded entry is PROJECTED into the viewer | `LogViewerControl.FilterLevel`, `.FilterCategory` | `Trace`, null |
| **Grammar** | Whether a projected row MATCHES what you typed | the table's filter, `/` or `ApplyFilterText` | none |

**The capture level is the one that catches people.** It defaults to `Warning`, so an
application that logs `Information` and sees nothing has not found a bug: it has found the
default. Below the capture level an entry is never created, so no view filter and no
grammar can bring it back, and it is not in the file either.

```csharp
// Record everything from here on. Settable at any time, including while running.
windowSystem.LogService.MinimumLevel = LogLevel.Debug;
```

The other two are non-destructive: they hide rows that are still in the buffer, and relaxing
them brings the rows back.

## Reading the log

### In the application: LogViewerControl

```csharp
var viewer = Controls.LogViewer(windowSystem.LogService)
    .WithTitle("Application log")
    .Build();
```

A live, virtualized table over the buffer: Time, Level, Category, Message. It follows the
tail, detaches when you scroll up, re-attaches at the bottom, and survives buffer eviction
without yanking a reader back down.

What it gives you interactively:

| | |
|---|---|
| `/` | the filter grammar: plain text, `category:Order`, `level:ERROR`, AND/OR terms, fuzzy fallback |
| click a header | sort by that column, cycling ascending, descending, then back to arrival order |
| drag a column border | resize, which is how you see a full type name in the narrow Category column |
| select a row | the detail pane, with the full message and the exception |
| Pause | freeze the tail; entries keep buffering |
| Export | write what is displayed to a file |

Sorting is on the underlying values, not the rendered text, so Level orders by severity
rather than alphabetically (where `ERROR` would sort before `WARNING`) and Time by the full
timestamp rather than its `HH:mm:ss` rendering. Entries with equal keys keep their arrival
order.

`GlobalFilterHotkey` makes `/` work from anywhere on the window rather than only when the
viewer has focus. It is off by default and should stay off on a page where another control
legitimately wants `/`, such as a terminal or a text editor; even when on, it declines while
another interactive control holds focus, so a typed slash stays a typed slash.

Full reference: **[LogViewerControl](controls/LogViewerControl.md)**.

### Outside the application: cxdbg and the diagnostics socket

The buffer is readable from another process while the app runs, which is the only way to
read the log of a program that is busy drawing. Start the app with a socket:

```bash
SHARPCONSOLEUI_DIAGNOSTICS_SOCKET=/tmp/myapp.sock dotnet run
```

It asks once, on its own screen, before opening the socket: another process will be able to
read what the app displays and to drive it. Then:

```bash
cxdbg logs -s /tmp/myapp.sock                                   # the last 50
cxdbg logs --count 200 -s /tmp/myapp.sock
cxdbg logs --level Warning -s /tmp/myapp.sock                   # this level and above
cxdbg logs --category Focus -s /tmp/myapp.sock
cxdbg logs --contains "payment" -s /tmp/myapp.sock              # substring, on the message
cxdbg logs --level Error --contains timeout -s /tmp/myapp.sock   # fields combine
```

The output is a table of time, level, message, category and exception:

```
    time          level  message                     category                         exception
 1  14:15:16.822  Error  order 10023 payment failed  DemoApp.Services.OrderProcessor  InvalidOperationException: card declined
```

Two things that read better here than in the viewer: the category is shown in FULL, where
the viewer's column is narrow enough to clip a type name to its tail, and the exception gets
its own column with the type and message rather than being appended to the text.

| Option | Default | Matches |
|---|---|---|
| `--count` | 50 | how many entries, newest last |
| `--level` | all | that level and above |
| `--category` | all | the category EXACTLY, so a full type name is needed |
| `--contains` | all | a substring of the message |

`--category` is an exact match: `--category OrderProcessor` finds nothing when the entries
say `DemoApp.Services.OrderProcessor`. Use `--contains` to match loosely.

The capture level is changeable from outside too, which is the cheapest way to investigate
something you cannot reproduce on demand, since restarting loses the state you attached to
look at:

```bash
cxdbg set --logLevel Trace -s /tmp/myapp.sock   # raise the floor, no restart
# reproduce the problem
cxdbg logs --count 200 --category Focus -s /tmp/myapp.sock
cxdbg set --logLevel Warning -s /tmp/myapp.sock # put it back
cxdbg set --clearLogs -s /tmp/myapp.sock        # empty the buffer
```

`cxdbg watch logs` repeats any of this in place, so the log can be followed live, including
for a headless app that has no terminal to show it on.

See **[Debugging](DEBUGGING.md)** for the socket and the full verb set, and
**[cxdbg](CXDBG.md)** for the tool.

### To a file

```csharp
windowSystem.LogService.EnableFileLogging("/tmp/myapp.log");
```

Or without touching the code, which is the point when a bug only happens on someone else's
machine:

```bash
export SHARPCONSOLEUI_DEBUG_LOG=/tmp/consoleui.log
export SHARPCONSOLEUI_DEBUG_LEVEL=Debug
```

`SHARPCONSOLEUI_DEBUG_LOG` turns file logging on at startup and `SHARPCONSOLEUI_DEBUG_LEVEL`
sets the capture level (`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`).
Paths under `/etc`, `/usr`, `/bin`, `/sbin`, `/proc`, `/sys` and the filesystem root are
refused.

### In your own code

```csharp
windowSystem.LogService.LogAdded += (_, entry) =>
{
    // entry.Timestamp, .Level, .Message, .Category, .Exception
};
```

Raised synchronously, on whichever thread logged, inside a `try/catch` that swallows handler
exceptions so a broken subscriber cannot break logging. A slow handler slows down the thread
that logged, which for the library's own entries can be the render loop.

## Microsoft.Extensions.Logging, both ways

Most of the .NET ecosystem logs through `ILogger`. Two adapters connect it to this library,
and neither needs a dependency-injection container: you construct them.

### Their ILogger into the viewer

```csharp
var bridge = new SharpConsoleUILoggerProvider(windowSystem.LogService);
ILogger logger = bridge.CreateLogger("MyApp.Startup");
logger.LogInformation("ready");          // appears in LogViewerControl
```

With a host builder, the same object is an ordinary `ILoggerProvider`:

```csharp
builder.Logging.AddProvider(new SharpConsoleUILoggerProvider(windowSystem.LogService));
```

Remember the capture level. The provider deliberately does **not** change
`MinimumLevel`, because that property belongs to the application; wire the bridge, log
`Information`, see nothing, and the fix is one line:

```csharp
windowSystem.LogService.MinimumLevel = LogLevel.Information;
```

What crosses, and how:

- **Level** maps one to one. `None` is dropped.
- **Category** is stored verbatim, so `ILogger<OrderProcessor>`'s full type name is kept and
  stays matchable by the filter grammar even though the Category column is narrow enough to
  show only its tail.
- **Exceptions** are carried on `Error` and `Critical`, and appended to the message text at
  the lower levels, which have no exception parameter on `ILogService`.
- **Scopes and event ids** have no field on `LogEntry`, so they are folded into the message
  rather than dropped: `[Order=42 => Attempt=2] [7:Submitted] retrying submission`.

### This library's own entries out to their pipeline

```csharp
var forwarder = new LogServiceForwarder(windowSystem.LogService, loggerFactory);
// ...
forwarder.Dispose();   // unsubscribes; it does not dispose the factory or the service
```

Window, focus, render and input diagnostics then reach whatever the application already has
configured, Seq or Serilog or a file. Categories are emitted prefixed, `SharpConsoleUI.Focus`,
so they are attributable and filterable as a group in a shared sink; pass
`prefixCategories: false` to forward them verbatim.

Forwarding is synchronous, on the thread that logged. A pipeline normally batches its own
writes; one that blocks instead blocks that thread.

### Both at once

Wiring both directions over one log service is safe, including with the provider registered
in the very pipeline the forwarder sends to. Each entry crosses exactly once: a re-entry
guard marks the call while a bridge is carrying an entry across, and the other bridge drops
what it sees marked. The guard is thread-static rather than `AsyncLocal`, deliberately, so
an entry logged from a thread the pipeline started is treated as new rather than suppressed.

> **Why no `AddSharpConsoleUI()` extension method?** This library has no DI container
> anywhere, by design, and its services are properties on `ConsoleWindowSystem`. An adapter
> you construct fits that; a registration idiom would suggest a wiring story the library
> does not have. If you have a `builder`, `AddProvider` takes the same object.

## Levels and categories

`SharpConsoleUI.Logging.LogLevel` mirrors `Microsoft.Extensions.Logging.LogLevel` exactly,
member for member and value for value, so the two convert by a cast. It is declared
separately because the library's core avoids depending on things it does not need; the
bridges above do the conversion for you.

`Trace` (0), `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None` (6).

> Two names collide if you import both namespaces in one file: `LogLevel` then needs
> qualifying or aliasing (`using OurLevel = SharpConsoleUI.Logging.LogLevel;`). The bridges
> themselves never put either enum in a signature you call, so this only comes up where your
> own code names a level.

The framework logs under these categories:

| Category | What |
|---|---|
| `System` | lifecycle: startup, the main loop, unhandled exceptions |
| `Window` | window add, close, move, resize |
| `WindowState` | state transitions (normal, minimized, maximized) |
| `Focus` | focus moves and the focus path |
| `Modal` | the modal stack |
| `Interaction` | drag and resize gestures |
| `Input` | keyboard and mouse |
| `Render` / `Renderer` | layout and paint passes |
| `Theme` | theme changes and colour resolution |
| `Notification` | toasts and notifications |
| `Flows` | multi-step flows and wizards |
| `Diagnostics` | the diagnostics socket itself |
| `Plugin`, `Layout` | plugin lifecycle, layout internals |

Both `Render` and `Renderer` appear in the framework's own calls. Filter on the prefix if
you want every rendering entry.

Your own categories are whatever string you pass; they share the namespace with these, so
something distinctive is worth using.

## Never log to the console

```csharp
Console.WriteLine("debug");          // corrupts the UI
builder.AddConsole();                // corrupts the UI, on every entry
builder.AddSimpleConsole();          // the same
```

Any provider that writes to stdout or stderr draws over the frame. Use `LogService`, a file,
the debug output, or the bridges above into a non-console sink. If a UI is garbled with no
obvious cause, this is the first thing to rule out.

## See also

- **[LogViewerControl](controls/LogViewerControl.md)** - the control's full reference
- **[Debugging](DEBUGGING.md)** - reading and driving a running app over a socket
- **[Configuration](CONFIGURATION.md)** - every environment variable
- **[Threading & Async](THREADING_AND_ASYNC.md)** - which thread a handler runs on

# cxdbg

A debugger for SharpConsoleUI applications. It attaches to a running app over
its diagnostics socket and asks it questions, or acts on it.

A TUI is the one program you cannot printf-debug: the screen *is* the output.
cxdbg is the way in.

```bash
dotnet tool install -g SharpConsoleUI.Debugger
```

## Starting an app under it

```bash
cxdbg run ./MyApp -- --headless --size 100x30
```

Everything after `--` belongs to the application, passed through unchanged.
cxdbg names the socket before the app exists, so there is nothing to hunt for
afterwards. `--again` starts the last command again.

cxdbg does not supervise what it starts: the app is still running when cxdbg
exits.

An app with a real terminal gets its own window. Where there is no terminal
emulator - over SSH, in a container, in CI - cxdbg prints the two lines to
paste instead of guessing.

## Asking

Three verbs answer the same question three ways, and the disagreement between
them is usually the bug:

| | |
|---|---|
| `cxdbg show tree` | what layout BELIEVES, desired against arranged |
| `cxdbg snapshot` | what the screen SHOWS, as text |
| `cxdbg snapshot --format cells` | what is in each COLUMN, one entry per cell |
| `cxdbg image` | what it LOOKS LIKE, with real glyphs |

A control with a zero-width arranged rect is in `tree`, reachable by focus, and
invisible in `snapshot`. That pattern is obvious here and invisible to tmux.

When the columns do not add up, ask for cells:

```bash
cxdbg snapshot --format cells --arg y=7 --arg h=1
```

```
row 7
    2   日   width 2
    3        <- the column before this one is two wide
    4   A    width 1
```

Text cannot answer this. It folds a wide glyph into one character and drops the
second column it occupies, which is exactly what you are looking for.

## Explaining

```bash
cxdbg why size ChartControl#b679e5     # which container narrowed it, and where
cxdbg why color Danger --state Disabled
```

`why` answers the questions whose answer is a cascade: why is this forty wide,
why is this green. The answer is usually a container several levels up, or a
contrast adjustment.

## Acting

```bash
cxdbg send key Tab
cxdbg send click 20 10
cxdbg send resize 80 24       # headless apps only; a terminal owns its own size
cxdbg set logLevel Trace      # bare `cxdbg set` reads them all
```

Input is asynchronous: the app has the keystroke when the command returns, not
necessarily the result. Poll `cxdbg frame` until the counter moves.

## Every protocol verb is a command

```bash
cxdbg commands              # what this app answers
cxdbg tree --depth 2        # protocol fields are options, with the protocol's names
cxdbg metrics --json
```

There is nothing to learn twice: a verb that gains a field gains an option. A
verb cxdbg has never heard of is passed through, because the app can be newer
than the debugger attached to it.

`--help` works at every level.

## Exit codes

| | |
|---|---|
| 0 | answered |
| 1 | the application refused, and said why |
| 2 | nothing was listening |
| 3 | cxdbg rejected the command line; nothing was sent |

Only 1 says anything about the application.

## Consent

An app opens a socket when `SHARPCONSOLEUI_DIAGNOSTICS_SOCKET` is set, after
asking on the console - the variable alone is not consent, since it can be
inherited from a shell profile or left over in a terminal. A headless app has
nobody to ask and opens it without prompting.

There is no verb that stops an application. A debugger that could kill its
subject would be a worse thing than a leaked process.

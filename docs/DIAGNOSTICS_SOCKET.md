# The Diagnostics Socket

A TUI is the one kind of program you cannot print debug output from. The screen *is* the
output, so a stray `Console.WriteLine` corrupts the thing you are trying to inspect, and a
breakpoint freezes the render loop you are trying to watch.

The diagnostics socket is the way out. It opens a local Unix socket that another program --
a shell, a script, an AI coding agent -- can use to **read what the app is showing** and
**drive it**, while it runs normally in its own terminal.

```
$ echo '{"cmd":"windows"}' | socat - UNIX-CONNECT:/tmp/myapp.sock
{"hello":"sharpconsoleui","protocol":1,"pid":12345}
{"ok":true,"windows":[{"title":"Settings","bounds":{"x":10,"y":4,"w":60,"h":20},"active":true}]}
```

---

## Quick start

```bash
# 1. Start your app with the socket requested.
SHARPCONSOLEUI_DIAGNOSTICS_SOCKET=/tmp/myapp.sock dotnet run

# 2. The app asks permission, on the plain console, before it paints. Answer y.

# 3. Talk to it. One JSON object per line, one reply per line.
echo '{"cmd":"snapshot"}' | socat - UNIX-CONNECT:/tmp/myapp.sock
echo '{"cmd":"key","spec":"Down"}' | socat - UNIX-CONNECT:/tmp/myapp.sock
echo '{"cmd":"png","path":"/tmp/shot.png"}' | socat - UNIX-CONNECT:/tmp/myapp.sock
```

`SHARPCONSOLEUI_DIAGNOSTICS_SOCKET=1` picks a path for you, under `$XDG_RUNTIME_DIR` with
the pid in the name.

In code, which never prompts:

```csharp
var options = ConsoleWindowSystemOptions.Create() with
{
    DiagnosticsSocket = "/tmp/myapp.sock",
};
```

---

## Consent, and why it is not optional

**An environment variable is not consent.** It can be inherited from a shell profile, baked
into a `.desktop` file or a systemd unit, or left over in a terminal from hours ago. Any of
those would otherwise silently open a channel that exposes the user's screen and hands over
control of the application.

So the environment route **asks**, before the driver initialises and before anything is
painted:

```
  SharpConsoleUI diagnostics

  This program was started with SHARPCONSOLEUI_DIAGNOSTICS_SOCKET set.

  It will open a local socket. For as long as this program runs,
  another program on this machine can:

    - READ what this application displays, plus its window layout,
      focus state and render metrics
    - CONTROL it: press keys, click, resize, switch windows and
      invoke actions, exactly as if you were doing it yourself

  Allow this only if you started a debugger yourself.

    socket:  /tmp/myapp.sock   (owner-only)

  Allow?  [y/N]
```

The rules behind that prompt:

- **Bare Enter is No.** Someone who really started a debugger types `y` without resentment;
  someone who inherited a stray variable gets the safe outcome from the key they were going
  to press anyway.
- **No TTY means no.** If stdin is redirected there is nobody to ask, and "nobody is
  watching" must mean no, never "assume yes". That is exactly the case a hostile variable
  would exploit.
- **The socket is `srw-------`** -- owner only.
- **The option in code does not prompt.** A property cannot be inherited from a shell
  profile or a desktop file; it can only be set by someone writing it into the program.

---

## The verbs

Every request is one JSON object on one line. Every reply is one JSON object on one line,
carrying `"ok": true` or `"ok": false` with an `"error"`.

**A verb that cannot do its job returns `ok: false`, never an empty success.** If an app is
configured such that a verb cannot work -- asking for `metrics` with diagnostics off, say --
the reply is an error naming the switch to change, not a reply with the numbers missing.
"No metrics" and "zero metrics" are different answers, and a probe must not blur them.

Verbs that change something accept `"settle": true`, which waits for the UI to stop moving
before replying -- so `key` followed by `snapshot` is coherent rather than racing.

### Seeing

| Verb | What it answers |
|------|-----------------|
| `snapshot` | What is on the screen, as text |
| `png` | What is on the screen, as an image |
| `windows` | What windows exist, in z-order |
| `tree` | What the layout engine believes |
| `focus` | Where keyboard input will go |
| `metrics` | Frame number, bytes written, characters changed |
| `logs` | The log buffer a TUI can never print to itself |

### Driving

| Verb | What it does |
|------|--------------|
| `key` | Sends keystrokes |
| `mouse` | Clicks, moves, scrolls |
| `focusctl` | Moves focus to a named control |
| `invoke` | Focuses a named control and activates it |
| `window` | Activates or closes a window |
| `theme` | Switches theme |
| `size` | Reports the screen size |
| `settle` | Waits for the UI to go quiet |

### Housekeeping

| Verb | What it does |
|------|--------------|
| `set` | Reads, and optionally changes, runtime switches |
| `ping` | Liveness |
| `commands` | Lists the verbs |

---

## snapshot

```json
{"cmd":"snapshot"}
{"cmd":"snapshot","window":"Settings"}
{"cmd":"snapshot","composed":true,"ansi":true}
{"cmd":"snapshot","y":20,"h":3}
```

| Field | Meaning |
|-------|---------|
| `window` | Window title or GUID. Omitted, the active window. |
| `composed` | The whole desktop as the user sees it, instead of one window. |
| `ansi` | Include the colour escapes. Pipe it to a terminal to see it in colour. |
| `x`,`y`,`w`,`h` | A region. Omitted fields mean "to the edge". |

A region matters because the interesting part of a 200x60 terminal is usually six rows of
it. The reply echoes the **resolved** origin and size, so a request for `h:3` at the bottom
edge may answer `height: 2` -- and you can map a hit back to real coordinates without
guessing.

A region entirely outside the buffer is an **error**, not an empty result: silently
returning nothing would read as "that area is blank", which is a different answer.

---

## png

A screenshot, with real text.

```json
{"cmd":"png"}                                    → inline base64url
{"cmd":"png","path":"/tmp/shot.png"}             → written to disk
{"cmd":"png","composed":true,"cellHeight":24}    → the whole desktop, larger
{"cmd":"png","glyphs":false}                     → colour blocks only, no font needed
```

| Field | Default | Meaning |
|-------|---------|---------|
| `path` | *(none)* | Write to a file instead of returning bytes. |
| `cellHeight` | `16` | Pixels per cell vertically. |
| `cellWidth` | *(font advance)* | Pixels per cell horizontally. |
| `glyphs` | `true` | Draw text. `false` gives a colour map with no font. |
| `font` | *(auto)* | Path to a monospace `.ttf`. |
| `composed` | `false` | The whole desktop instead of one window. |
| `x`,`y`,`w`,`h` | *(all)* | A region, as for `snapshot`. |

**Bytes are the default, and a path is the opt-in.** A full 120x40 desktop is around 130 KB
of PNG -- smaller than a verbose `tree` reply. Returning it inline means the reply *is* the
screenshot: no second step, nothing left behind on a crash, and it works across a container
or user boundary where a path would not. Writing a file is also the one thing the consent
prompt does not already cover, which is why you have to ask for it.

The inline field is **base64url** (`-` and `_`). Plain base64 would be escaped by
`System.Text.Json` -- `+` becomes `+` -- which inflates the reply and breaks a client
that slices the field out by hand.

### Fonts

With `glyphs: true` (the default) the renderer looks for a monospace TrueType font in the
usual places, preferring a Nerd Font because it covers the powerline and icon glyphs a TUI
actually draws. Pass `font` to choose one. If none is found the reply says so in
`fontNote` and falls back to colour blocks rather than failing.

The glyph is composited over whatever is already in the image, not over its own cell's
background. Box-drawing characters are designed to overhang their cell -- `─` is drawn 10px
wide at an 8px pitch precisely so neighbours fuse -- and compositing per-cell would chop a
continuous rule into dashes.

---

## key

tmux/emacs notation, because it is what anyone reaching for this already knows.

```json
{"cmd":"key","spec":"Down"}
{"cmd":"key","spec":"C-c"}
{"cmd":"key","spec":"S-Tab"}
{"cmd":"key","spec":"F5"}
{"cmd":"key","spec":"\"hello\"",  "settle":true}
```

`C-` is ctrl, `M-` is alt, `S-` is shift, in any order. A quoted string is typed literally,
one keystroke per character.

An unrecognised name is an **error**, not typed literally. Silently typing `Dwon` into the
app would be a confusing way to discover the typo.

---

## mouse

```json
{"cmd":"mouse","x":40,"y":25}
{"cmd":"mouse","x":40,"y":25,"button":"right","action":"click","settle":true}
{"cmd":"mouse","x":40,"y":25,"action":"wheelup"}
```

| Field | Values |
|-------|--------|
| `button` | `left`, `right`, `middle` |
| `action` | `click`, `press`, `release`, `move`, `wheelup`, `wheeldown` |

This routes into the same method the console driver's own mouse events land on, so
hit-testing, z-order and capture all run exactly as for a real click. A probe that reached
past dispatch could report hits a user could never produce.

---

## logs

The log buffer, which a TUI can never print to its own screen.

```json
{"cmd":"logs","count":20}
{"cmd":"logs","level":"Warning"}
{"cmd":"logs","category":"Focus","contains":"lost"}
```

| Field | Default | Meaning |
|-------|---------|---------|
| `count` | `50` | How many entries. |
| `level` | *(all)* | Minimum level. |
| `category` | *(all)* | `Window`, `Focus`, `Input`, `Render`, ... |
| `contains` | *(all)* | Substring match on the message. |

---

## set

Reads, and optionally changes, the switches worth flipping mid-session. Sent with no
fields it is a pure read, so `{"cmd":"set"}` answers "what is this app doing right now".

```json
{"cmd":"set"}
{"cmd":"set","logLevel":"Trace"}
{"cmd":"set","diagnostics":true}
{"cmd":"set","clearLogs":true}
```

| Field | Meaning |
|-------|---------|
| `diagnostics` | Render-diagnostics recording (needed by `metrics` and `composed`). |
| `logging` | Log capture on or off. |
| `logLevel` | `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`. |
| `clearLogs` | Empties the buffer. |

The reply always reports the resulting state, so a write reads back and verifies itself.

Raising the log floor mid-run is the cheapest way to find a noisy category without
restarting:

```bash
echo '{"cmd":"set","logLevel":"Trace"}' | socat - UNIX-CONNECT:/tmp/myapp.sock
# reproduce the bug
echo '{"cmd":"logs","count":100,"category":"Focus"}' | socat - UNIX-CONNECT:/tmp/myapp.sock
```

**Not exposed on purpose:** `BlockWhenIdle`. It is host-integration wiring, and a remote
flip to `false` would spin the render loop on whatever thread the host owns. A probe may
change what is *observed*, never how the app is *driven*.

---

## Render diagnostics

Two things need the renderer to be recording: `metrics`, and anything with
`composed: true`. Recording is a per-frame deep copy of the console buffer, so it is off by
default -- a probe should not silently change the performance profile of the app it is
measuring.

It is enabled at construction:

```csharp
var options = ConsoleWindowSystemOptions.Create() with
{
    EnableDiagnostics = true,
};
```

If an app was built **without** it, the diagnostics recorder is never created, and no verb
can conjure it. The error says so, and points at the alternative:

> no composed buffer: this app runs with EnableDiagnostics off... Capture a window instead
> -- `{"cmd":"png","window":"<title>"}` -- which needs no diagnostics.

Per-window `snapshot` and `png` read the window's live render buffer and work regardless.
That is the common case and the cheaper one.

If the recorder exists but is paused, `composed` turns it on and **waits for a frame
captured after that point** -- never serving the stale snapshot still in hand, which would
be a screenshot of the past presented as the present.

---

## Worked example: find why a button does nothing

```bash
S=/tmp/myapp.sock
ask() { echo "$1" | socat - UNIX-CONNECT:$S | tail -1; }

ask '{"cmd":"windows"}'                                  # which window is active?
ask '{"cmd":"focus"}'                                     # where will Enter go?
ask '{"cmd":"tree","depth":3,"props":true}'               # is the button even laid out?
ask '{"cmd":"set","logLevel":"Trace"}'
ask '{"cmd":"mouse","x":40,"y":25,"settle":true}'         # click it for real
ask '{"cmd":"logs","count":30}'                           # what did that do?
ask '{"cmd":"png","path":"/tmp/after.png"}'               # and what does it look like now?
```

`tree` is often the one that answers it: a control with a zero-width arranged rectangle is
present in the DOM, reachable by focus, and invisible to the mouse.

If `logs` comes back empty after the click, that is itself the finding -- the click landed
on nothing. Compare `focus` before and after, or click a coordinate you read out of
`tree`'s arranged bounds rather than one you guessed from the screen.

---

## Security

- The socket is **owner-only** (`srw-------`) on the local filesystem. It is not a network
  service and never binds a port.
- It is **off unless asked for**, and the environment route requires a typed `y` at a TTY.
- **Anyone who can reach the socket can control the app.** That is the whole point, and it
  is why the prompt says so in those words. Do not enable it in production, and do not
  leave the variable in a shell profile.
- A probe must never crash the app it is probing: a handler that throws answers with an
  error rather than taking the process down.

---

## NativeAOT

The socket is fully AOT-compatible, and the repository's AOT gate exercises it -- opening a
real socket, round-tripping the JSON verbs, and decoding the PNG to verify its magic bytes.

One wrinkle worth knowing if you extend it: `JsonArray.Add<T>` creates values reflectively
and is neither trim- nor AOT-safe, while `ICollection<JsonNode?>.Add` is fine. The casts
through `JsonValue.Create` in the serializer are not noise -- they pick the safe overload.

---

## See also

- [`AOT.md`](AOT.md) -- NativeAOT support and the CI gate
- [`CONFIGURATION.md`](CONFIGURATION.md) -- `ConsoleWindowSystemOptions`
- `SHARPCONSOLEUI_DEBUG_LOG` / `SHARPCONSOLEUI_DEBUG_LEVEL` -- file-based debug logging,
  the same buffer the `logs` verb reads

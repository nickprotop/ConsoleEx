# Debugging a running app

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
echo '{"cmd":"image","path":"/tmp/shot.png"}' | socat - UNIX-CONNECT:/tmp/myapp.sock
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

### Error codes

Most errors are prose, because most are "your request was wrong" -- a missing field, an
unknown control name -- and a developer reads the sentence and fixes it. Three failures are
different: each describes a **condition** rather than a mistake, and a client has to behave
differently when it sees one. Those carry a `"code"`:

| `code` | Means | What a client should do |
|--------|-------|-------------------------|
| `wrong_driver` | This driver cannot do it -- resizing a real terminal, or reading the composed desktop in `RenderMode.Direct` | Stop asking. No retry helps. |
| `diagnostics_disabled` | The app was built without the recording this verb reads | Disable the feature. Not switchable from here. |

```json
{"ok":false,"code":"diagnostics_disabled","error":"no metrics: this app runs with..."}
{"ok":false,"error":"no control named 'btn9' in 'Settings'"}
```

The second has no code on purpose. Branching on a code is reliable; substring-matching an
English sentence is not, and leaving ordinary errors uncoded keeps their wording free to
improve without breaking anybody.

Input is asynchronous: a verb returns as soon as the event is queued, not when the screen
has caught up. To read a coherent screen afterwards, watch the frame counter -- see
[Waiting for the screen to catch up](#waiting-for-the-screen-to-catch-up).

Any request may carry an `"id"`, which the reply echoes:

```json
{"id":"a7","cmd":"tree"}   →   {"id":"a7","ok":true,"window":"Settings",...}
```

Today it is a convenience -- the protocol is strictly one reply per request, so position
already identifies the answer. It matters once that stops being true. Send a string or a
number, at most 64 characters; anything longer or more complex is ignored rather than
rejected, because failing a working request over its label would be a poor trade. **Omit it
and the reply is exactly what it was before the field existed.**

One gap worth knowing: a request whose JSON does not parse has no id to read, so the error
comes back without one.

### Seeing

| Verb | What it answers |
|------|-----------------|
| `snapshot` | What is on the screen, as text |
| `image` | What is on the screen, as a PNG, TGA or JPEG |
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
| `size` | Reports the screen size, and sets it on a headless app |

### Housekeeping

| Verb | What it does |
|------|--------------|
| `set` | Reads, and optionally changes, runtime switches |
| `frame` | How many frames have been presented |
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

## image

A screenshot, with real text.

```json
{"cmd":"image"}                                    → PNG, inline base64url
{"cmd":"image","path":"/tmp/shot.png"}             → written to disk
{"cmd":"image","composed":true,"cellHeight":24}    → the whole desktop, larger
{"cmd":"image","glyphs":false}                     → colour blocks only, no font needed
{"cmd":"image","format":"tga"}                     → fast to encode, large
{"cmd":"image","format":"jpeg","quality":75}       → lossy, small
```

The reply always names what it produced in its own `format` field, so a client picking a
file extension or a decoder reads that rather than inferring it from the request.

| Field | Default | Meaning |
|-------|---------|---------|
| `format` | `"png"` | `png`, `tga` or `jpeg`. |
| `quality` | `90` | 1--100, JPEG only. Out of range is an error, not a clamp. |
| `path` | *(none)* | Write to a file instead of returning bytes. |
| `cellHeight` | `16` | Pixels per cell vertically. |
| `cellWidth` | *(font advance)* | Pixels per cell horizontally. |
| `glyphs` | `true` | Draw text. `false` gives a colour map with no font. |
| `font` | *(auto)* | Path to a monospace `.ttf`. Implies `fontSource: "file"`. |
| `fontSource` | `"automatic"` | `automatic`, `embedded`, `strict` or `file`. See below. |
| `composed` | `false` | The whole desktop instead of one window. |
| `sinceFrame` | *(none)* | Answer `unchanged` if no frame has been presented since this one. |
| `x`,`y`,`w`,`h` | *(all)* | A region, as for `snapshot`. |

**Bytes are the default, and a path is the opt-in.** A full 120x40 desktop is around 130 KB
of PNG -- smaller than a verbose `tree` reply. Returning it inline means the reply *is* the
screenshot: no second step, nothing left behind on a crash, and it works across a container
or user boundary where a path would not. Writing a file is also the one thing the consent
prompt does not already cover, which is why you have to ask for it.

The inline field is **base64url** (`-` and `_`). Plain base64 would be escaped by
`System.Text.Json` -- `+` becomes `+` -- which inflates the reply and breaks a client
that slices the field out by hand.

### Which format

Measured on a 174x50 desktop at 8x16, encoding the same pixels:

| Format | Encode | Size | Use it for |
|---|---|---|---|
| `png` | 70 ms | 50 KB | Everything, unless you have a reason. The only deterministic one. |
| `tga` | 4 ms | 569 KB | Streaming frames over a local socket, where encode time bounds the frame rate. |
| `jpeg` | 9 ms (q75) | 113 KB | Attachments, and surfaces carrying an image or a gradient. |

PNG is smallest because terminal output is flat colour, which deflate handles best. The
reply always names what it produced in its `format` field, so a client never has to infer
it. Only PNG is byte-identical run to run, so golden-image comparison stays on PNG.

### Fonts

With `glyphs: true` (the default) the image is drawn with a real TrueType face. Which one
depends on `fontSource`:

| `fontSource` | Where the glyphs come from | Reproducible? |
|---|---|---|
| `automatic` *(default)* | A font installed on this machine, falling back to the embedded face; installed fonts are also chained for scripts the first lacks. | No |
| `embedded` | Only the face shipped inside the library. | **Yes, everywhere** |
| `strict` | Only installed fonts. Errors when none is found. | No |
| `file` | The `.ttf` named by `font`. Errors if it cannot be loaded. | If the file travels |

**The default is not your terminal's font.** A terminal's font lives in that emulator's own
configuration, and there is no cross-platform way to ask which one is in use -- so
`automatic` picks a common monospace face found on disk. Layout, colour and content are
exact; the letterforms may differ from what is on your screen.

**Nothing silently renders blank.** `automatic` always ends at the embedded face, because an
image with no text would still hash consistently and so would pass a golden-image check it
ought to have failed. `strict` and `file` fail loudly instead, which is the point of asking
for them.

The embedded face (DejaVu Sans Mono, Bitstream Vera licence, ~335 KB) covers Latin, Greek,
Cyrillic, box-drawing and common symbols. **It has no CJK**, which is why `automatic` still
reaches for installed fonts: a Han or Kana glyph can only come from the system. Anything no
available face can draw is listed in `missingGlyphs` rather than left as a silent gap, and
`fallbackFonts` names the faces that filled in.

> A caveat on CJK away from Linux: stb_truetype rasterises TrueType (`glyf`) outlines only,
> and most modern CJK faces on macOS and Windows -- Noto CJK, PingFang, and the Microsoft
> `.ttc` collections -- are CFF/PostScript. Those are detected and skipped rather than loaded
> to draw nothing, so CJK via system fonts is reliable on Linux and best-effort elsewhere.

### Substituted colours

The composed desktop stores the escape the renderer emitted, and that escape may say `39` or
`49` -- "whatever the terminal's default is" -- a colour this process genuinely does not
know. Those cells are filled from the theme, and the reply **says so** rather than letting a
guess pass for a measurement:

```json
{"ok":true,...,"defaultedCells":96,
 "note":"cells using the terminal's default background were filled from the theme"}
```

A per-window capture never reports this: those cells carry real typed colour.

The glyph is composited over whatever is already in the image, not over its own cell's
background. Box-drawing characters are designed to overhang their cell -- `─` is drawn 10px
wide at an 8px pitch precisely so neighbours fuse -- and compositing per-cell would chop a
continuous rule into dashes. Combining marks are drawn at the base glyph's own origin, which
is where a terminal puts them.

---

## frame, and streaming the screen

```json
{"cmd":"frame"}     →  {"ok":true,"frame":1204}
```

How many frames the renderer has presented. Monotonic, needs no diagnostics, and is the
cheapest verb in the protocol - which matters, because anything streaming the screen asks
it many times a second.

Pair it with `sinceFrame` and an unchanged screen costs almost nothing:

```json
{"cmd":"image","composed":true,"sinceFrame":1204}
  →  {"ok":true,"unchanged":true,"frame":1204}      // 29 bytes
  →  {"ok":true,"frame":1211,"bytes":6322,...}      // a real frame
```

The short-circuit happens before a font is loaded or a buffer is touched, so the idle case
is genuinely cheap rather than merely smaller. A full 120x40 desktop is around 130 KB; at
10fps that is 1.3 MB/s of mostly identical pixels, and a TUI is idle between keystrokes.
Every `image` reply carries `frame`, so a client tracks it without a second call.

**The count only ever increases**, including across a resize - which rebuilds the console
buffer underneath. It is a 64-bit value, because a 32-bit one at 60fps wraps in about
thirteen months and a counter that went backwards would strand a client waiting for a
number it had already passed.

---

## key

tmux/emacs notation, because it is what anyone reaching for this already knows.

```json
{"cmd":"key","spec":"Down"}
{"cmd":"key","spec":"C-c"}
{"cmd":"key","spec":"S-Tab"}
{"cmd":"key","spec":"F5"}
{"cmd":"key","spec":"\"hello\""}
```

`C-` is ctrl, `M-` is alt, `S-` is shift, in any order. A quoted string is typed literally,
one keystroke per character.

An unrecognised name is an **error**, not typed literally. Silently typing `Dwon` into the
app would be a confusing way to discover the typo.

---

## mouse

```json
{"cmd":"mouse","x":40,"y":25}
{"cmd":"mouse","x":40,"y":25,"button":"right","action":"click"}
{"cmd":"mouse","x":40,"y":25,"action":"wheelup"}
```

| Field | Values |
|-------|--------|
| `button` | `left`, `right`, `middle` |
| `action` | `press`, `release`, `drag`, `move`, `click`, `doubleclick`, `releaseclick`, `wheelup`, `wheeldown` |

This routes into the same method the console driver's own mouse events land on, so
hit-testing, z-order and capture all run exactly as for a real click.

**Send facts; the gestures are derived for you.** A `press` followed by a `release` at the
same cell produces a `Button1Clicked` automatically, and a second quick press produces a
`Button1DoubleClicked` on top of its press - `MouseGestureRecognizer` does this for socket
input exactly as it does for a real terminal. So the natural way to click is:

```json
{"cmd":"mouse","x":20,"y":8,"action":"press","button":"left"}
{"cmd":"mouse","x":20,"y":8,"action":"release","button":"left"}
```

**Dragging needs `drag`, not `move`.** A `move` is motion with no button down; `drag` is
motion *while the button is held*, which is what a terminal reports mid-gesture and what a
drag needs in the middle — a splitter, a slider, a scrollbar thumb, a text selection, a
window move:

```json
{"cmd":"mouse","x":30,"y":0,"action":"press","button":"left"}
{"cmd":"mouse","x":36,"y":4,"action":"drag","button":"left"}
{"cmd":"mouse","x":42,"y":8,"action":"drag","button":"left"}
{"cmd":"mouse","x":42,"y":8,"action":"release","button":"left"}
```

A drag starts only if the press lands on the thing that owns it, and a press one row off
does nothing at all — so take the coordinates from `tree` or `windows` rather than guessing.
Note that a window's `bounds` are in the desktop's own space: with a top panel visible, a
window reported at `y: 0` has its title bar on screen row 1, and pressing row 0 hits the
panel instead. `windows` before and after is the check that it worked.

The remaining actions send a flag directly, for a client that wants one specific event
rather than a gesture: `click`, `doubleclick`, and `releaseclick` (release and click in one
event, which is how a terminal reports the end of a click). They are idempotent with the
recogniser - a flag already present is not added twice.

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

The reply's `diagnosticsAvailable` says whether a recorder exists at all. `false` means the
app was built with `EnableDiagnostics: false`, so `diagnostics: true` cannot turn one on -
it was never created. `true` with `diagnostics: false` means it exists but is paused, which
IS switchable from here.

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

`metrics` needs the renderer to be recording. Recording is a per-frame deep copy of the
console buffer, so it is off by default -- a probe should not silently change the
performance profile of the app it is measuring.

**`composed: true` does not need it.** The driver keeps the composed desktop permanently
anyway, because the diff engine needs it, so `snapshot` and `image` read that directly and
cost one copy per capture. The exception is `RenderMode.Direct`, which keeps no buffer at
all; capture a window instead.

It is enabled at construction:

```csharp
var options = ConsoleWindowSystemOptions.Create() with
{
    EnableDiagnostics = true,
};
```

If an app was built **without** it, the recorder is never created and no verb can conjure
it, so `metrics` returns an error naming the switch rather than reporting zeroes.

Every other verb works regardless. Per-window `snapshot` and `image` read the window's own
render buffer, and `composed` reads the driver's -- both exist because the renderer needs
them.

If the recorder exists but is paused, `composed` turns it on and **waits for a frame
captured after that point** -- never serving the stale snapshot still in hand, which would
be a screenshot of the past presented as the present.

---

## size

Reports the screen size. On a **headless** app it also sets it.

```json
{"cmd":"size"}                              → {"ok":true,"w":120,"h":40}
{"cmd":"size","w":40,"h":12}                → resize
```

Narrow and short sizes are exactly where layout bugs hide, and reaching them otherwise means
restarting the app or dragging a terminal corner. Headless, it is one line.

Asking a real driver to resize is an **error**, not a silent no-op:

> this app runs on NetConsoleDriver, whose size is owned by the terminal and cannot be set
> from here. Resize the terminal, or run the app on HeadlessConsoleDriver to drive size from
> tests.

A test that silently kept its original size would assert against a layout it never actually
produced, and pass.

---

## Waiting for the screen to catch up

Input is asynchronous. A verb replies as soon as the event is queued, so a `snapshot` taken
immediately afterwards can show the screen *before* the key took effect - intermittently,
which is the worst kind of test failure.

The fix is to watch the frame counter, which `frame` reports and every `image` reply carries:

```bash
before=$(ask '{"cmd":"frame"}' | jq .frame)
ask '{"cmd":"key","spec":"Down"}'

# Wait for a frame newer than the one before the input.
for i in $(seq 1 50); do
    now=$(ask '{"cmd":"frame"}' | jq .frame)
    [ "$now" -gt "$before" ] && break
    sleep 0.02
done

ask '{"cmd":"snapshot"}'
```

**The client owns this policy deliberately, because only the client knows what it is waiting
for.** An earlier `settle` verb tried to wait for the UI to "go quiet", and that guarantee
cannot be made for any application with a moving part - a clock, a spinner, a progress bar,
a background simulation. On those it waits the full timeout every time and then reports
failure, which is worse than not offering it: in the WebHost example it turned every click
into a flat two-second stall while the application itself was rendering happily.

Waiting for *one more frame* is a promise the framework can actually keep. Wait for several
if a single frame is not enough, poll until the content you expect appears, or do not wait
at all if you are already polling - all of which are three lines of client code, and none of
which the server can choose correctly on your behalf.

A counter that never advances means the render loop is not running - a host that owns its
own loop has stopped calling `Tick()`. Nothing read after that reflects a live screen, so a
test should fail rather than assert against a frozen frame.

---

## tree

What the layout engine believes, as opposed to what the screen shows. The whole stack —
the desktop, its windows, and their controls — in one reply.

```json
{"cmd":"tree"}                                  → everything
{"cmd":"tree","depth":2}                        → shallower
{"cmd":"tree","window":"Settings"}              → one window's controls
{"cmd":"tree","props":true}                     → plus layout properties
```

The **desktop** node reports the screen size and `originX`/`originY` — the offset at which
the desktop starts, below any top panel.

Each **window** reports `bounds` (desktop coordinates), `absolute` (where that lands on
screen), and a `state` block: `windowState`, `active`, `modal`, `movable`, `resizable`,
`closable`. Those last three decide whether a gesture can work at all — dragging a window
that is not movable looks exactly like aiming wrong.

Each **control** node reports `desired` (what it asked for), `bounds` (what layout gave it,
window-relative), `absolute` (where that lands on screen), and `state` (what the control
currently *is*: checked, open, selected, its text). **A control whose arranged `bounds` has
zero width is present in the tree, reachable by focus, and invisible to the mouse** — which
is the single most useful thing here, and the one question a screen scrape can never answer.

**Always click `absolute`, never `bounds`.** A window at `y: 0` with a top panel visible has
its title bar on screen row 1; `absolute` already accounts for that, so a client that reads
it and acts on it is right the first time:

```json
{"cmd":"windows"}
→ {"bounds":{"x":0,"y":0,...}, "absolute":{"x":0,"y":1,...}}
```

`childrenElided: <n>` on a node means the walk stopped at your `depth` and that many
children were not shown. Raise `depth` to see them.

---

## Headless: driving an app with no terminal

Everything above works against an app running in a terminal. It also works against an app with
**no terminal at all** — which is where this stops being a debugging aid and becomes a test
harness.

```csharp
var driver = new HeadlessConsoleDriver(120, 40);
var options = ConsoleWindowSystemOptions.Create() with
{
    DiagnosticsSocket = "/tmp/headless.sock",
};
var ws = new ConsoleWindowSystem(driver, options);

BuildYourUI(ws);

using var session = ws.RunInBackground();
```

That is the whole setup. The app is now reachable on the socket, with no terminal, no pty and
no screen-scraping — and the full verb set works, including the ones that drive.

```bash
S=/tmp/headless.sock
ask() { echo "$1" | socat - UNIX-CONNECT:$S | tail -1; }

ask '{"cmd":"invoke","name":"btn1"}'
# {"ok":true,"invoked":"btn1"}
```

The button's real `Click` handler ran. Not a simulated one — `invoke` goes through the same
dispatch a user's click does.

`RunInBackground()` runs the loop on its own thread and returns once a frame exists, so the
socket is already accepting connections and there is something to look at. If your host has
a frame loop of its own, use `BeginHosted()` and call `Tick()` yourself instead — see
[`DRIVERS.md`](DRIVERS.md) for the difference.

Why this is worth the setup: most TUI test tooling drives a pty and scrapes text, so you end up
testing what the *terminal* did, not what the framework decided. Here the app runs with no
terminal, you drive it through the real input path, and you can ask three independent questions
— `snapshot` (what it shows), `tree` (what layout believes) and `image` (what it looks like).
**When those three disagree, the disagreement is the bug.**

---

## Cookbook

### Golden-image regression

PNG output is deterministic: the same UI at the same size and cell size produces
byte-identical PNG bytes. So a hash is a regression test.

```bash
ask '{"cmd":"image","composed":true,"fontSource":"embedded","path":"/tmp/now.png"}' > /dev/null
sha256sum /tmp/now.png | cut -d' ' -f1 > /tmp/now.sha
diff /tmp/now.sha testdata/golden/main-screen.sha || {
    echo "UI changed — review /tmp/now.png"
    exit 1
}
```

**`"fontSource":"embedded"` is not optional here.** By default the renderer prefers a font
installed on the machine, which differs between your laptop, CI and a colleague's Mac — three
machines, three hashes, for the same UI. The embedded face ships inside the library, so it
renders identically everywhere including a container with no fonts at all. A baseline recorded
without it is only valid on the machine that recorded it.

Keep the PNG as the CI artifact, not just the hash: when it fails you want to *look* at it. Pin
`cellHeight` and the region, since both change the bytes.

**Watch for anything that changes on its own.** The composed desktop includes the status-bar
clock, so a baseline that captures it fails once a second. Capture a window instead, or crop
the clock out with `x`/`y`/`w`/`h`.

### Responsive layout testing

```bash
for size in "40 12" "80 24" "200 60"; do
    set -- $size
    ask "{\"cmd\":\"size\",\"w\":$1,\"h\":$2}"
    sleep 0.2                     # let the relayout land
    ask "{\"cmd\":\"image\",\"composed\":true,\"path\":\"/tmp/layout-$1x$2.png\"}"
done
```

Three screenshots at hostile sizes, no terminal involved. Widths where text must truncate and
heights where a panel must scroll are where the bugs are.

### Is it rendering, or is it stuck?

```bash
a=$(ask '{"cmd":"frame"}' | jq .frame)
sleep 1
b=$(ask '{"cmd":"frame"}' | jq .frame)

[ "$b" -gt "$a" ] && echo "rendering ($((b-a)) frames/s)" \
                  || echo "render loop is not running"
```

A counter that does not move means nobody is calling `Tick()`. Anything read after that is
a frozen screen, so a test should fail rather than assert against it.

### Debugging an app you did not start

```bash
ask '{"cmd":"set","logLevel":"Trace"}'   # raise the floor, no restart
# reproduce the bug in the app's own terminal
ask '{"cmd":"logs","count":100,"category":"Focus"}'
ask '{"cmd":"set","logLevel":"Warning"}' # put it back
```

### Find out what the app can do

```bash
ask '{"cmd":"commands"}'   # the verbs
ask '{"cmd":"set"}'        # every runtime switch and its current value
```

---

## Worked example: find why a button does nothing

```bash
S=/tmp/myapp.sock
ask() { echo "$1" | socat - UNIX-CONNECT:$S | tail -1; }

ask '{"cmd":"windows"}'                                  # which window is active?
ask '{"cmd":"focus"}'                                     # where will Enter go?
ask '{"cmd":"tree","depth":3,"props":true}'               # is the button even laid out?
ask '{"cmd":"set","logLevel":"Trace"}'
ask '{"cmd":"mouse","x":40,"y":25}'                       # click it for real
ask '{"cmd":"logs","count":30}'                           # what did that do?
ask '{"cmd":"image","path":"/tmp/after.png"}'             # and what does it look like now?
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

- [`DRIVERS.md`](DRIVERS.md) -- drivers, render modes, and running headless
- [`EXPORT.md`](EXPORT.md) -- the same captures as a public API, for use inside your own app
- [`AOT.md`](AOT.md) -- NativeAOT support and the CI gate
- [`CONFIGURATION.md`](CONFIGURATION.md) -- `ConsoleWindowSystemOptions`
- `SHARPCONSOLEUI_DEBUG_LOG` / `SHARPCONSOLEUI_DEBUG_LEVEL` -- file-based debug logging,
  the same buffer the `logs` verb reads

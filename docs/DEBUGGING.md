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

> **Looking for the debugger, not the protocol?**
>
> ```bash
> dotnet tool install -g cxdbg
> cxdbg run ./MyApp --attach
> ```
>
> **[cxdbg](CXDBG.md)** speaks all of this for you, and its
> **[first five minutes](CXDBG.md#the-first-five-minutes)** is one screen rather
> than this page. Come back here for what a verb answers and what each field of a
> reply means.

---

## cxdbg, or raw JSON

Everything here is the wire protocol: one JSON object per line, over a Unix socket. That is
worth knowing, because it is what a script or an agent talks to, and it is the authority
when a reply surprises you.

For working by hand there is **[cxdbg](CXDBG.md)**, a dotnet tool that speaks it
for you:

```bash
dotnet tool install -g cxdbg

cxdbg run ./MyApp --attach        # start it, and open a prompt on it
cxdbg show tree -s /tmp/myapp.sock
cxdbg why size ChartControl#b679e5
cxdbg watch snapshot --composed   # the screen, live, even headless
```

`--attach` opens a prompt on the application, where the verbs below are typed
without repeating `cxdbg` or the socket, replies can be redirected to a file
with `>`, and `.help` lists what else is there. It is the way in for working by
hand; everything else on this page is what a script or an agent sends.

`watch` asks one verb over and over and redraws it in place. It is cxdbg's, not the
protocol's - any verb works, the verb's own fields are passed through, and esc or q stops
it. Watching `snapshot` mirrors the application as it runs, which is the only way to SEE
one built on `HeadlessConsoleDriver`: it has no terminal, so its screen exists only as a
buffer until something asks for it. `cxdbg --help watch` has the detail.

Every verb below is a cxdbg command of the same name, and every field below is an option of
the same name:

```json
{"cmd":"snapshot","format":"cells","y":7,"h":1}
```
```bash
cxdbg snapshot --format cells --y 7 --h 1
```

So nothing here has to be learned twice, and a verb this document has not caught up with
still works - cxdbg declares no list of its own. **[cxdbg](CXDBG.md)** is the tool's own
page: installing it, the prompt, and every command it has.

**This document is the protocol's.** It describes the wire, which is the library's feature
and the thing an agent, a script or a future client talks to. cxdbg is one client of it and
is documented separately; where the two disagree, the wire is right.

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

Writing that line out every time gets tiring, so the rest of this document uses a
one-line shell function. Define it once and `ask` stands for the whole pipeline:

```bash
S=/tmp/myapp.sock
ask() { echo "$1" | socat - UNIX-CONNECT:$S | tail -1; }

ask '{"cmd":"snapshot"}'
ask '{"cmd":"key","spec":"Down"}'
```

`tail -1` is there because the server greets a new connection with a hello line before
answering: the reply is the last line, not the first.

`SHARPCONSOLEUI_DIAGNOSTICS_SOCKET=1` picks a path for you, under `$XDG_RUNTIME_DIR` with
the pid in the name. Without `$XDG_RUNTIME_DIR` it creates an owner-only directory inside
the temp directory rather than dropping the socket in the open: the socket file is
owner-only either way, but a shared temp directory tells every other user that this app is
running and what its process id is.

### Who can reach it

**Anything running as this user, on every platform.** On Linux the socket file is
chmod'd owner-only, which bounds *other* users. `File.SetUnixFileMode` is a no-op on
Windows, so there the containing directory's ACL does that work. Neither stops a process
running as the same user — and nothing can: a token written beside the socket is readable
by exactly the attacker it would be meant to stop. Every local debugger has this property;
`gdb` attaches to your processes and the .NET diagnostic pipes behave the same way.

So the protection that remains is **visibility**: opening a socket from the environment
logs a Warning, and every connection logs one too.

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
- **No TTY means no — on a driver that has one.** If stdin is redirected there is nobody
  to ask, and "nobody is watching" must mean no, never "assume yes". That is exactly the
  case a hostile variable would exploit.

- **A headless app is asked nothing.** `HeadlessConsoleDriver` has no terminal by design,
  so "is stdin a terminal?" says nothing about whether a user is present — and denying on
  it made the variable silently terminal-only. A headless app honors the variable and logs
  a Warning that it did.

  This is not a hole. The prompt protects one scenario: an ordinary app on your terminal
  inherits the variable and a channel appears that reads your screen. A headless app has no
  terminal, no screen to expose, and is headless because someone wrote
  `new HeadlessConsoleDriver(...)` in code — the same deliberate, compiled-in statement as
  setting `DiagnosticsSocket` there, which has always been honoured without asking. The
  threat given up is an attacker who can already set your environment, targeting a headless
  app, to read a screen nobody is looking at.
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
| `wrong_driver` | This driver cannot do it -- resizing a real terminal, or reading a composed desktop from a driver that keeps no buffer | Stop asking. No retry helps. |
| `diagnostics_disabled` | The recording this verb reads is off | Turn it on with `{"cmd":"set","diagnostics":true}` and ask again. |

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
| `desktop` | The screen itself: size, where it starts, what is on it |
| `control` | One control: its size, state, and what contains it |
| `inspect` | What is under a point: window, region, control, cell |
| `focus` | Where keyboard input will go |
| `reachable` | Which controls Tab can reach, in Tab order |
| `measure` | Why a control is the size it is: the offers, and which one decided |
| `color` | Why a role is the color it is, resolution step by step |
| `metrics` | Frame number, bytes written, characters changed |
| `performance` | The rate the screen actually changes at, over the last few hundred frames. `cellsMarked` is the status bar's dirty-chars count, summed over the window |
| `invalidations` | Which control asked for the frame. Needs diagnostics on |
| `events` | What input the app was told about. Needs diagnostics on |
| `logs` | The log buffer a TUI can never print to itself |

### Driving

| Verb | What it does |
|------|--------------|
| `key` | Sends keystrokes |
| `mouse` | Clicks, moves, scrolls |
| `seq` | Feeds raw terminal bytes through the decoder |
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
| `settled` | Whether the application has finished reacting, and what is holding it |
| `clock` | Read the clocks, or freeze one and step it by hand |
| `ping` | Liveness |
| `commands` | Lists the verbs |

---

## snapshot

```json
{"cmd":"snapshot"}
{"cmd":"snapshot","window":"Settings"}
{"cmd":"snapshot","composed":true,"ansi":true}
{"cmd":"snapshot","format":"cells","y":7,"h":1}
{"cmd":"snapshot","y":20,"h":3}
```

| Field | Meaning |
|-------|---------|
| `window` | Window title or GUID. Omitted, the active window. |
| `composed` | The whole desktop as the user sees it, instead of one window. |
| `ansi` | Include the color escapes. Pipe it to a terminal to see it in color. |
| `format` | `cells` for one entry per column, `markup` to repaint it elsewhere. |
| `x`,`y`,`w`,`h` | A region. Omitted fields mean "to the edge". |

`ansi` is an ANSI **rendering of the desktop**, not a recording of what the driver sent.
The driver emits a diff -- only changed cells, cursor jumps between fragments, a per-line
choice of strategy and a style cache that spans cells -- so its output is a fragment of a
frame, not a picture of one. The snapshot is generated fresh from the retained buffer and
is complete.

### format: markup

The format that parses back into cells, which is what a viewer asks for when it intends to
**repaint** the screen rather than read it.

```json
{"cmd":"snapshot","format":"markup"}
```

```
[#EEEEEE on #323232 bold]  SharpConsoleUI[/][#EEEEEE on #323232]    [/]...
```

`MarkupParser.Parse` reconstructs the cells exactly -- colors, decorations, combining
marks, and the continuation cell of a wide glyph. Both ends use the same parser, so they
cannot drift. `SurfaceViewControl` takes the result and paints it, which is how one
application shows another application's screen.

Plain text would lose the wide-glyph continuation and all color; ANSI would need a second
SGR parser at the receiving end. Markup is the one format designed for the round trip.

### format: cells

Text answers "what does this row say". `cells` answers "what is in each column", which is
a different question that text cannot carry: both text writers skip continuation cells and
fold combining marks into the character before them, so a wide glyph arrives as one
character with no width and the second column it occupies is gone.

```json
{"ch":"\u65e5","w":2,"fg":[238,238,238],"bg":[50,50,50],"dec":"Bold"}
{"cont":true,"fg":[238,238,238],"bg":[50,50,50]}
{"ch":"A","w":1,"fg":[238,238,238],"bg":[50,50,50]}
```

One entry per column, continuation cells included, so index equals column and arithmetic
over a row is trustworthy. Color comes back as numbers; a caller that wanted escapes
would have asked for `ansi`, and the two together are refused. Fields a cell does not have
-- `dec`, `comb` -- are omitted rather than sent as null.

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
{"cmd":"image","glyphs":false}                     → color blocks only, no font needed
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
| `glyphs` | `true` | Draw text. `false` gives a color map with no font. |
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

PNG is smallest because terminal output is flat color, which deflate handles best. The
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
`automatic` picks a common monospace face found on disk. Layout, color and content are
exact; the letterforms may differ from what is on your screen.

**Nothing silently renders blank.** `automatic` always ends at the embedded face, because an
image with no text would still hash consistently and so would pass a golden-image check it
ought to have failed. `strict` and `file` fail loudly instead, which is the point of asking
for them.

The embedded face (DejaVu Sans Mono, Bitstream Vera license, ~335 KB) covers Latin, Greek,
Cyrillic, box-drawing and common symbols. **It has no CJK**, which is why `automatic` still
reaches for installed fonts: a Han or Kana glyph can only come from the system.

Braille is the exception. No monospace face in the DejaVu family carries the block, and a
terminal shows it by composing several faces - which `embedded` cannot do without becoming
machine-dependent again. So the 256 patterns are drawn from geometry instead of looked up in
a font: eight dots on a 2x4 grid, identical on every machine, costing nothing in the package.
Braille charts photograph correctly in `embedded` mode as a result. Anything no
available face can draw is listed in `missingGlyphs` rather than left as a silent gap, and
`fallbackFonts` names the faces that filled in.

> A caveat on CJK away from Linux: stb_truetype rasterizes TrueType (`glyf`) outlines only,
> and most modern CJK faces on macOS and Windows -- Noto CJK, PingFang, and the Microsoft
> `.ttc` collections -- are CFF/PostScript. Those are detected and skipped rather than loaded
> to draw nothing, so CJK via system fonts is reliable on Linux and best-effort elsewhere.

### Substituted colors

The composed desktop stores the escape the renderer emitted, and that escape may say `39` or
`49` -- "whatever the terminal's default is" -- a color this process genuinely does not
know. Those cells are filled from the theme, and the reply **says so** rather than letting a
guess pass for a measurement:

```json
{"ok":true,...,"defaultedCells":96,
 "note":"cells using the terminal's default background were filled from the theme"}
```

A per-window capture never reports this: those cells carry real typed color.

The glyph is composited over whatever is already in the image, not over its own cell's
background. Box-drawing characters are designed to overhang their cell -- `─` is drawn 10px
wide at an 8px pitch precisely so neighbors fuse -- and compositing per-cell would chop a
continuous rule into dashes. Combining marks are drawn at the base glyph's own origin, which
is where a terminal puts them.

---

## frame, and streaming the screen

```json
{"cmd":"frame"}     →  {"ok":true,"frame":1204}
{"cmd":"settled"}   →  {"ok":true,"settled":true,"frame":1204,"blockers":null}
{"cmd":"clock"}     →  {"ok":true,"clocks":{"mouse":{"clock":"mouse","kind":"system",...
{"cmd":"clock","target":"mouse","fake":true}       // freeze where it stands
{"cmd":"clock","target":"mouse","advance":600}     // step it 600ms
{"cmd":"clock","target":"mouse","system":true}     // hand it back
                    →  {"ok":true,"settled":false,"frame":1204,
                        "blockers":"2 window(s) with work pending"}
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

## settled

Whether the application has finished reacting to what it has been given, and what is
holding it if not.

```json
{"cmd":"settled"}   →  {"ok":true,"settled":true,"frame":518,"blockers":null}
                    →  {"ok":true,"settled":false,"frame":518,
                        "blockers":"a mouse handler is running, 2 window(s) with work pending"}
```

Five conditions, all of them: no input waiting, no mouse handler running, nothing queued
for the UI thread, no animation in flight, and no window with painting still to do.
`blockers` names whichever are false, so a script that gives up waiting can say why.

The mouse is the odd one. A mouse event is dispatched on whichever thread raised it and
never enters a queue, which is what keeps it responsive while the UI is busy. Nothing that
asks "is anything queued" can see it, so it is a condition of its own.

**This is a read, not a wait.** It answers for the moment it was asked, and each condition
belongs to a subsystem running on its own thread, so clearing all five does not promise
that nothing is about to arrive. For waiting, use the frame counter above: in-process,
`WaitUntilSettledAsync()` requires both the conditions and a frame composed since the call.

**It is UI-level.** It says nothing about work the application itself is doing on its own
threads.

---

## performance

The rate the screen actually changes at, over the last few hundred frames.

```json
{"cmd":"performance"}
  →  {"ok":true,"achievedFps":0.97,"targetFps":60,"minFrameMs":16.67,
      "frameRateLimiting":true,"frames":300,"totalFrames":518,
      "minMs":1017.69,"meanMs":1026.84,"p95Ms":1033.12,"maxMs":1085,
      "bound":"work"}
```

`achievedFps` is what happened, `targetFps` what was asked for. An idle application sits
far below its target and that is correct: nothing asked it to paint. `bound` says which
side the limit came from, the work or the cap.

`--reset` starts a fresh window, for measuring one thing rather than the session so far.

Stage timings and byte counts need `{"cmd":"set","diagnostics":true}`; the reply says so
rather than reporting zeros as though they were measurements.

---

## clock

Reads the clocks, or points one at a frozen clock and steps it by hand.

```json
{"cmd":"clock"}
  →  {"ok":true,"clocks":{"mouse":{"clock":"mouse","kind":"system","now":"..."},
                          "repaint":{...},"animation":{...}}}

{"cmd":"clock","target":"mouse","fake":true}       // freeze where it stands
{"cmd":"clock","target":"mouse","advance":600}     // step it 600ms
{"cmd":"clock","target":"mouse","system":true}     // hand it back
```

Three clocks, because the parts measure different things: `mouse` is the multi-click
window, `repaint` the cadence of controls that redraw themselves, `animation` the per-frame
delta. Freezing one leaves the others running, so the application keeps drawing while the
thing under test holds still.

Telling one double click from two slow ones is a question about timing, and waiting out a
real multi-click window to test it is a poor way to spend a second.

Frame pacing is not among them. Redrawing no more than `targetFps` times a second is a fact
about the terminal, so it stays on the machine clock: a frozen clock there would render
nothing at all.

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

`click` and `doubleclick` send the press and release a terminal sends - two events for a
click, four for a double - so the recogniser derives the gesture exactly as it does for a
real one. That matters: sending `Button1DoubleClicked` directly would skip the derivation,
and a bug in it would be invisible from here while a real terminal misbehaved.

`press`, `release` and `releaseclick` still send exactly one event each, for a client that
wants one specific thing rather than a gesture. `releaseclick` carries the release and the
click together, which is how a terminal reports the end of a click - a client that sends
them as two messages breaks any control that opens a portal on release. All of them are
idempotent with the recogniser: a flag already present is not added twice.

---

## seq

`key` and `mouse` send input that is already decoded. `seq` goes one layer below them and
feeds raw bytes through the decoder itself, which is the only way to drive the things the
decoder alone handles: legacy X10 mouse reports, bracketed paste, cursor-position replies,
and anything malformed.

```json
{"cmd":"seq","text":"\\e[<0;12;7M"}
{"cmd":"seq","text":"\\e[200~hi\\e[201~"}
{"cmd":"seq","raw":"1b5b41"}
```

| Field | Meaning |
|-------|---------|
| `text` | The bytes with backslash escapes: `\e` `\n` `\r` `\t` `\0` `\\` `\xNN`. Anything else is sent as UTF-8, the way a terminal sends it. |
| `raw` | The bytes as hex. Spaces, commas and a leading `0x` are ignored, so a dump pasted from anywhere works. |

One or the other, not both. `raw` is what replays a byte dump captured from somebody's
broken terminal verbatim, with no escaping to get wrong on the way.

The reply says how many bytes were read and what each one **decoded to**, so the decode
and its effect arrive in one round trip:

```json
{"ok":true,"bytes":10,"events":["mouse Button1Pressed @11,6"]}
{"ok":true,"bytes":14,"events":["paste 2 char(s)"]}
{"ok":true,"bytes":6,"events":["mouse Button1Pressed @8,4"]}
{"ok":true,"bytes":3,"events":["key UpArrow"]}
```

Each decoded event is then delivered where its own driver would deliver it: a key is
queued, a paste is queued, a mouse event dispatches inline through the same hit-test
path `mouse` uses. An event the parser could not identify is **reported but not
delivered** - it says what it saw and stops, because guessing would be worse:

```json
{"cmd":"seq","text":"\\e[<0;10"}
{"ok":true,"bytes":7,"events":["unknown 7 byte(s)"]}
```

Each call stands alone. A sequence that ends mid-way leaves the parser expecting more, so
anything partial is flushed at the end of the call rather than swallowing the next
injection to complete itself.

A malformed request is an **error**, not a guess, for the same reason an unknown key name
is:

```json
{"cmd":"seq","text":"\\q"}
{"ok":false,"error":"unknown escape '\\q'; known: \\e \\n \\r \\t \\0 \\\\ \\xNN"}

{"cmd":"seq","raw":"1b5b4"}
{"ok":false,"error":"'raw' has 5 hex digits, which is not a whole number of bytes"}
```

An odd digit count is refused rather than padded, because the missing nibble could belong
at either end.

**It decodes bytes the way the library decodes bytes. It does not pretend a terminal sent
them.** The byte-reading loop, its partial reads and its escape timeout all need a real
terminal - use tmux, or run the app under a pty, to exercise those. What `seq` covers is
everything from the decoder upward, which is where a parser actually goes wrong.

Because it is the decoder and not the driver, it works the same on all three
configurations: a real terminal, a terminal without raw mode, and `HeadlessConsoleDriver`
with no terminal at all.

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

The reply's `diagnosticsAvailable` says whether a recorder exists at all. It is `true` for
any app built on the current library: the recorder is always created, and
`EnableDiagnostics` decides only whether it STARTS enabled. So `diagnostics: true` works on
an app already running, including one built without the option -- which is the app you most
often want to interrogate, since restarting it loses the state you attached to look at.

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
cost one copy per capture. A custom driver that keeps no buffer has nothing to read;
capture a window instead.

Start it on at construction:

```csharp
var options = ConsoleWindowSystemOptions.Create() with
{
    EnableDiagnostics = true,
};
```

Or turn it on over the socket, on an app already running:

```bash
echo '{"cmd":"set","diagnostics":true}' | socat - UNIX-CONNECT:/tmp/myapp.sock
```

Until it is on, `metrics` returns an error naming the switch rather than reporting zeroes.
Turn it back off when you are done: a probe that cannot stop costs the app a deep copy
every frame from then on.

Every other verb works regardless. Per-window `snapshot` and `image` read the window's own
render buffer, and `composed` reads the driver's -- both exist because the renderer needs
them.

If recording is off, `composed` turns it on and **waits for a frame
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
for.** Waiting for the UI to "go quiet" is not a guarantee the protocol can make: any
application with a moving part - a clock, a spinner, a progress bar, a background
simulation - is never quiet, so such a wait costs the full timeout and then reports failure.

Waiting for *one more frame* is a promise the framework can keep. Wait for several if a
single frame is not enough, poll until the content you expect appears, or do not wait at all
if you are already polling - all of which are three lines of client code, and none of which
the server can choose correctly on your behalf.

A counter that never advances means the render loop is not running - a host that owns its
own loop has stopped calling `Tick()`. Nothing read after that reflects a live screen, so a
test should fail rather than assert against a frozen frame.

---

## inspect

What is under a point. The inspector: you are looking at a screenshot, something is wrong
*there*, and you have coordinates rather than a name.

```json
{"cmd":"inspect","x":20,"y":10}
```

```json
{"ok":true,"x":20,"y":10,
 "cell":{"ch":"a","fg":[238,238,238],"bg":[45,45,45]},
 "region":"client",
 "window":{"title":"Settings","active":true,"movable":true,"resizable":true,"bounds":{...}},
 "control":{"control":"MarkupControl","parents":[...], ...},
 "covered":[]}
```

Layered on purpose, because the interesting answer is often not the topmost thing.

**`region`** is the field most questions end at:

| | |
|---|---|
| `desktop` | bare desktop, no window there |
| `topPanel` `bottomPanel` | the desktop's own chrome |
| `client` | inside a window, where controls live |
| `title` | the title bar — a drag here moves the window, if it is movable |
| `resize` | the frame; `resize` names the direction (`TopLeft`, `Right`, …) |

A point with **no control** is a real answer: `"control": null` with a window means window
chrome, which is why a click there does nothing. That case and "a control that ignored the
click" look identical on screen and nowhere else.

**`covered`** lists the windows under this point that lost. A modal eating clicks is
invisible without it.

The window and the region come from the **input coordinator** — the same code a real press
runs, asked in the same order. A reimplementation walking the layout tree would report what
*should* be under the point rather than what a click will find, and the gap between those
two is what this verb exists to show.

---

## desktop

The screen itself, without walking anything on it.

```json
{"cmd":"desktop"}
```

```json
{"ok":true,"width":100,"height":30,"originX":0,"originY":1,
 "topPanelRows":1,"bottomPanelRows":1,"desktopBottom":28,
 "theme":"ModernGray","windows":1,"modals":false,"portals":true,
 "driver":"HeadlessConsoleDriver"}
```

`originY` is the row the desktop starts on, below any top panel — the reason a window's
`bounds` and its `absolute` differ, and the commonest way to click in the wrong place.

**`modals` and `portals` answer "why does nothing respond".** A modal takes the keyboard
and a portal takes the frame; both are invisible in a screenshot and decisive for input.

The same block appears inside a `tree` reply, from the same code — ask `tree` when you want
the controls too, and this when you do not.

---

## control

One control: what it is, where it sits, and what contains it.

```json
{"cmd":"control","control":"okButton"}
{"cmd":"control","control":"MarkupControl#3f2a1b"}
{"cmd":"control","control":"okButton","window":"Settings"}
```

Name it by its `Name`, by the `id` that `tree` reports, or by its bare type when only one
of that type is around — the same three spellings `measure` takes. Without a `window`, the
active one.

The reply is the same shape `tree` gives a control node — `desired`, `bounds`, `absolute`,
`props`, `state` — plus **`parents`**: the chain of containers above it, outermost first.

```json
{"ok":true,"control":"ScrollablePanelControl","id":"ScrollablePanelControl#165f26b",
 "desired":{"w":30,"h":10},"bounds":{"x":0,"y":3,"w":30,"h":25},
 "props":{"horizontal":"Left","vertical":"Fill"},
 "state":{"scrollOffset":0,"viewportExtent":25,"contentExtent":65},
 "parents":[{"control":"(root)","layout":"DockRootLayout","bounds":{"w":88,"h":28}}],
 "window":"SharpConsoleUI Demo"}
```

**No children.** This answers about one control; `tree` is the verb for a subtree.

Before this verb, reading what a control *is* meant asking for the whole tree with `props`
and walking it — a reply that grows with the application, to answer one question. The four
other control-scoped verbs (`measure`, `color`, `focusctl`, `invoke`) act on a control or
explain one axis of it; this one describes it.

An unknown control is an error that names what was asked for, because a typo and a control
that is genuinely absent otherwise read the same.

---

## measure

Why a control is the size it is.

```json
{"cmd":"measure","control":"ScrollablePanelControl#165f26b"}
```

The layout tree says a control wanted one size and got another. It does not say what the
control was *allowed* to want, and that is usually the answer: a chart measuring itself at
`int.MaxValue` rows is obvious the moment you see it was handed an unbounded height, and
invisible until then.

The reply gives the offer the control received, what it asked for, and the **cascade** —
every container between the root and the control, outermost first, with the offer each one
passed down. `setsWidth` and `setsHeight` name the link that bounded the result.

```json
{"ok":true,"window":"Demo","controls":[
  {"control":"ScrollablePanelControl","id":"ScrollablePanelControl#165f26b",
   "offeredWidth":30,"offeredHeight":25,"desiredWidth":30,"desiredHeight":25,
   "cascade":[
     {"layout":"DockRootLayout","control":null,"id":null,
      "offeredWidth":88,"offeredHeight":28,"setsWidth":false,"setsHeight":false},
     {"layout":"VerticalStackLayout","control":"ColumnContainer","id":"ColumnContainer#29e8405",
      "offeredWidth":30,"offeredHeight":28,"setsWidth":true,"setsHeight":true}]}]}
```

Offers never widen going inwards, so the cascade is non-increasing — if it is not, the
chain is being read in the wrong order.

The deciding link is the **narrowest ancestor**, not one whose offer happens to equal the
result exactly. A container that subtracts its own padding leaves no link matching the
final number, and an exact-match rule named nothing at all.

`control` accepts a name, an id from `tree`, or a bare type when only one of that type
exists.

---

## color

Why a role is the color it is.

```json
{"cmd":"color","role":"Danger"}
{"cmd":"color","role":"Danger","state":"Disabled"}
{"cmd":"color","role":"Primary","outline":true}
```

A role resolves through the theme, then through contrast adjustment, then through how it is
painted. Each step can move the color, and the reply shows all of them in order.

```json
{"ok":true,"theme":"ModernGray","role":"Danger","state":"Normal","outline":false,
 "background":"Color(128, 0, 0)","text":"Color(185, 114, 114)",
 "cascade":[
   {"source":"theme DangerColor","color":"Color(128, 0, 0)"},
   {"source":"moved for contrast against the surface (text and border only)",
    "color":"Color(185, 114, 114)"},
   {"source":"painted as fill","color":"Color(128, 0, 0)"}]}
```

`state` is `Normal`, `Focused` or `Disabled`; `outline` resolves the role as an outline
rather than a fill. Both change the answer, which is the point of asking.

---

## focus

Where keyboard input will go, and the path that reaches it.

```json
{"cmd":"focus"}
```

```json
{"ok":true,"window":"Demo","focused":"DropdownControl#1ac495c",
 "chain":["Window:Demo","NavigationView#201d787","ToolbarControl#19fd5c7",
          "DropdownControl#1ac495c"]}
```

The chain is the answer more often than `focused` is: focus lands somewhere unexpected
because of a container in the middle, and the chain is the only thing that names it.

---

## reachable

Which controls Tab can reach, in Tab order.

```json
{"cmd":"reachable"}
```

```json
{"ok":true,"window":"Demo","count":3,"stops":[
  {"control":"ScrollablePanelControl","id":"ScrollablePanelControl#165f26b",
   "type":"ScrollablePanelControl"},
  {"control":"ButtonControl","id":"ButtonControl#2ec5d9c","type":"ButtonControl"}]}
```

`control` is the control's `Name` when it has one and its type otherwise; `id` is always
addressable by another verb. A control that is on screen and missing from this list cannot
be reached by keyboard, which is the accessibility question this verb exists to answer.

---

## windows

The windows, their bounds and their state, in z-order.

```json
{"cmd":"windows"}
```

```json
{"ok":true,"windows":[
  {"id":"77195774-76f4-4200-8fcf-e2788c02c7a0","title":"Demo",
   "bounds":{"x":0,"y":0,"w":90,"h":30},"absolute":{"x":0,"y":1,"w":90,"h":30},
   "modal":false,"state":"Normal","root":"DockRootLayout","active":true}]}
```

`bounds` is desktop-relative; `absolute` is where it actually sits on screen, which is what
`mouse` and `inspect` take. `state` is `Normal`, `Minimized` or `Maximized`.

---

## metrics

What the last frame cost. Needs `{"cmd":"set","diagnostics":true}`.

```json
{"cmd":"metrics"}
```

```json
{"ok":true,"frame":39,"bytesWritten":0,"charactersChanged":0,"ansiSequences":0,
 "cursorMovements":0,"dirtyCellsMarked":0,"cellsRendered":0,"efficiency":1,
 "staticFrame":true,"layoutMs":0,"ansiMs":0,"outputMs":0}
```

`staticFrame` true with zero bytes written means the diff engine decided nothing changed —
which is the correct answer for an idle app, and the wrong one if you just changed
something.

---

## invalidations

Which control asked for the frame. Needs diagnostics on.

```json
{"cmd":"invalidations"}
```

```json
{"ok":true,"windows":[
  {"window":"Demo","total":16,"callers":[
    {"control":"NavigationView#201d787","repaints":0,"relayouts":13},
    {"control":"(window)","repaints":0,"relayouts":3}]}]}
```

The verb for "why is this app repainting constantly". A control with hundreds of relayouts
on an idle screen is a setter firing on every tick with a value that has not changed.

**The buffer drains on read.** Two calls in a row give the second one nothing.

---

## events

What input the application was told about. Needs diagnostics on.

```json
{"cmd":"events"}
```

```json
{"ok":true,"recording":true,"count":2,"events":[
  {"kind":"key","flags":"Tab"},{"kind":"key","flags":"Down"}]}
```

The tape is what the app *received*, which is the half of an input problem a terminal
cannot show you: a key that never arrives and a key that arrives and is ignored look
identical on screen. Also drains on read.

---

## theme

Read the themes, or switch to one.

```json
{"cmd":"theme"}
{"cmd":"theme","name":"Ocean"}
```

```json
{"ok":true,"theme":"ModernGray","themes":[
  {"name":"Amber","description":"Warm amber accent, dark"},
  {"name":"Ocean","description":"Teal accent on a deep dark surface"}]}
```

Switching answers `{"ok":true,"theme":"Ocean"}`. An unknown name is refused with the list
of known ones, because a typo and a theme that does not exist otherwise read the same.

---

## window

Act on a window.

```json
{"cmd":"window","activate":"Settings"}
{"cmd":"window","close":"Settings"}
{"cmd":"window","window":"Settings","activate":true}
```

The action field may name the window, or be a bare flag beside a `window` field. Both work;
the named form is the one that reads on a command line.

```json
{"ok":true,"activated":"Settings"}
{"ok":true,"closed":"Settings"}
```

---

## focusctl and invoke

Move focus to a control, or activate it the way a user would.

```json
{"cmd":"focusctl","name":"okButton"}
{"cmd":"invoke","name":"okButton"}
```

```json
{"ok":true,"focused":"ButtonControl#2ec5d9c"}
{"ok":true,"invoked":"ButtonControl#2ec5d9c"}
```

`invoke` focuses first and then activates, so it is what a click would have done without
needing the control's coordinates. Both take a name or an id, and an optional `window`.

---

## ping and commands

```json
{"cmd":"ping"}       →  {"ok":true,"pong":true}
{"cmd":"commands"}   →  {"ok":true,"known":["snapshot","image","tree", …]}
```

`commands` is the authority on what an application supports — this document describes the
protocol, but the app in front of you may be older or newer than it.

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

Each **control** node reports an `id`, `desired` (what it asked for), `bounds` (what layout
gave it, window-relative), `absolute` (where that lands on screen), and `state` (what the
control currently *is*: checked, open, selected, its text).

The **`id`** is how you name one control to another verb. It is the control's `Name` when
it has one, and otherwise its type and instance — `MarkupControl#3f2a1b`. Hand it back to
`control`, `measure`, `focusctl` or `invoke` and they will find the control the tree was
showing you. **Every verb that names a control names it this way** — `focus` says what has
the keyboard, `reachable` lists the Tab stops, `inspect` reports what is under a point, and
each of them answers in ids you can ask the next question with. A bare type would read
fine and go nowhere, since most applications have several controls of a type. It is not
positional: inserting a sibling above a control does not change its id, which is the whole
reason for the instance part. It does not survive a restart, and an id whose control has
gone matches nothing rather than matching whatever took its place. **A control whose arranged `bounds` has
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
S=/tmp/headless.sock          # ask() as defined in Quick start, on this socket
ask() { echo "$1" | socat - UNIX-CONNECT:$S | tail -1; }

ask '{"cmd":"invoke","name":"btn1"}'
# {"ok":true,"invoked":"btn1"}
```

The button's real `Click` handler ran. Not a simulated one — `invoke` goes through the same
dispatch a user's click does.

`RunInBackground()` runs the loop on its own thread and returns once a frame exists, so the
socket is already accepting connections and there is something to look at. If your host has
a frame loop of its own, use `BeginHosted()` and call `Tick()` yourself instead — see
[Drivers](DRIVERS.md) for the difference.

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
S=/tmp/myapp.sock             # ask() as defined in Quick start
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

- [cxdbg](CXDBG.md) -- the debugger that speaks this protocol, and its prompt
  protocol, for working by hand
- [Drivers](DRIVERS.md) -- drivers, render modes, and running headless
- [Screen export](EXPORT.md) -- the same captures as a public API, for use inside your own app
- [NativeAOT](AOT.md) -- NativeAOT support and the CI gate
- [Configuration](CONFIGURATION.md) -- `ConsoleWindowSystemOptions`
- `SHARPCONSOLEUI_DEBUG_LOG` / `SHARPCONSOLEUI_DEBUG_LEVEL` -- file-based debug logging,
  the same buffer the `logs` verb reads

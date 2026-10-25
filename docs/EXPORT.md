# Exporting the screen

A window, a region of one, or the whole desktop, as an image or as text.

```csharp
using SharpConsoleUI.Surfaces;

File.WriteAllBytes("window.png", window.CaptureSurface().ToImage());
File.WriteAllText("window.txt", window.CaptureSurface().ToText());
```

That is the whole API for the common case. Everything else is the same two calls with a
different source or a different format.

---

## Surfaces

The library holds cells in several shapes -- a window's live buffer, an immutable snapshot,
the renderer's composed frame -- and they do not agree on how colour is stored. `ICellSurface`
is the one read lens over all of them, so an exporter is written once instead of once per
source.

```csharp
window.CaptureSurface()                 // a window's painted buffer
snapshot.AsSurface()             // a BufferSnapshot, captured earlier
system.ComposedSurface()         // the whole desktop, every window stacked
```

A surface is a view, not a copy: obtaining one allocates nothing.

```csharp
public interface ICellSurface
{
    int Width { get; }
    int Height { get; }
    SurfaceCell this[int x, int y] { get; }
}
```

`SurfaceCell` carries the character, its combining marks, foreground, background,
decorations, whether it is the second column of a wide glyph, and whether the background was
*substituted rather than measured* -- see [Defaulted colours](#defaulted-colours).

### What it costs

Nothing beyond one copy per capture. **No export needs `EnableDiagnostics`.**

A window's buffer exists because the renderer needs it. The composed desktop likewise: the
diff engine keeps it permanently, to know what to send to the terminal next. Both are read
directly, so a capture copies once rather than imposing a per-frame cost on the application.

`ComposedSurface()` is unavailable in only one case - `RenderMode.Direct`, which keeps no
buffer at all because every write goes straight out. It throws then, naming the per-window
alternative, rather than returning a blank image.

---

## Regions

`Crop` is a surface-to-surface operation, so it composes with every exporter rather than
being re-specified for each one.

```csharp
var box = new LayoutRect(X: 2, Y: 11, Width: 27, Height: 3);

window.CaptureSurface().Crop(box).ToImage();
window.CaptureSurface().Crop(box).ToText(TextFormat.Ansi);
```

A region that only partly overlaps is **clamped**, and the result reports its own clamped
size -- a partly-offscreen request has a useful answer. A region entirely outside **throws**,
because silently returning an empty surface would read as "that area is blank", which is a
different answer.

---

## Text formats

```csharp
surface.ToText()                       // TextFormat.Plain
surface.ToText(TextFormat.Ansi)
surface.ToText(TextFormat.Markup)
```

| Format | For |
|---|---|
| `Plain` | Golden files, diffs, grepping a layout. Trailing blanks trimmed. |
| `Ansi` | `cat` it into a terminal and see the real thing. Escapes coalesced into runs. |
| `Markup` | Parses back into cells. |

`Markup` is the one with no equivalent elsewhere: its output goes straight back into a
`MarkupControl`, which makes a captured screen a reusable asset -- a fixture, a splash
screen, a test baseline.

```csharp
string captured = window.CaptureSurface().ToText(TextFormat.Markup);
// ... later, or in another program
var control = new MarkupControl(captured.Split('\n').ToList());
```

All three skip wide-continuation cells, so a wide glyph is emitted once rather than twice,
and all three carry combining marks.

---

## Images

```csharp
surface.ToImage();
surface.ToImage(new ExportOptions { CellHeight = 24, Glyphs = false });
surface.ToImage(new ExportOptions { Format = ImageFormat.Tga });
```

| Option | Default | Meaning |
|---|---|---|
| `Format` | `Png` | `Png`, `Tga` or `Jpeg`. |
| `JpegQuality` | `90` | 1--100. Ignored unless `Format` is `Jpeg`. |
| `CellHeight` | `16` | Pixels per cell vertically. |
| `CellWidth` | *(font advance)* | Pixels per cell horizontally. |
| `Glyphs` | `true` | Draw text. `false` gives a colour map and needs no font. |
| `FontPath` | *(none)* | A specific `.ttf`. Implies `FontSource.File`. |
| `FontSource` | `Automatic` | Where to find the font. See below. |

**Bytes, not a path.** `File.WriteAllBytes(path, surface.ToImage())` is the one-line file case,
and bytes also serve an HTTP response, a test assertion or a clipboard, where a path does not.

### Choosing a format

Measured on a 174x50 desktop at 8x16 (1392x800 pixels), encoding the same pixels:

| Format | Encode | Size | Use it for |
|---|---|---|---|
| `Png` | 70 ms | 50 KB | Everything, unless you have a reason. Lossless, smallest here, and the only deterministic one. |
| `Tga` | 4 ms | 569 KB | Streaming. Lossless, encodes in a fraction of the time, and the bytes never reach a disk. |
| `Jpeg` | 9 ms (q75) | 113 KB | Attachments, and surfaces carrying an image or a gradient. |

PNG is smallest because terminal output is large areas of flat colour -- the case deflate
handles best and JPEG handles worst. That advantage disappears as a surface gains
photographic content, which is when JPEG starts to win.

PNG is also the only format whose output is byte-identical run to run, so it is the only one
usable for golden-image comparison. Pair it with `FontSource.Embedded`.

### Fonts

```csharp
public enum FontSource { Automatic, Embedded, Strict, File }
```

| | Glyphs from | Reproducible across machines |
|---|---|---|
| `Automatic` *(default)* | Installed fonts, falling back to the embedded face; installed fonts chained for scripts the first lacks. | No |
| `Embedded` | The face shipped inside the library, only. | **Yes** |
| `Strict` | Installed fonts only. Errors when none is found. | No |
| `File` | `FontPath`. Errors if it cannot be loaded. | If the file travels |

**`Automatic` is not your terminal's font.** A terminal's font lives in that emulator's own
configuration, and there is no cross-platform way to ask which one is in use, so a common
monospace face is picked off disk. Layout, colour and content are exact; the letterforms may
differ from what is on screen.

**Nothing silently renders blank.** `Automatic` always ends at the embedded face, because an
image with no text would still hash consistently -- and so would pass a golden-image check it
ought to have failed. `Strict` and `File` fail loudly instead, which is why you would ask for
them.

The embedded face is DejaVu Sans Mono (Bitstream Vera licence, ~335 KB), covering Latin,
Greek, Cyrillic, box-drawing and common symbols. **It has no CJK**, which is why `Automatic`
still reaches for installed fonts.

> stb_truetype rasterises TrueType (`glyf`) outlines only, and most modern CJK faces on macOS
> and Windows -- Noto CJK, PingFang, the Microsoft `.ttc` collections -- are CFF/PostScript.
> Those are detected and skipped rather than loaded to draw nothing. CJK through system fonts
> is reliable on Linux and best-effort elsewhere.

---

## Golden-image testing

PNG output is deterministic: the same surface, cell size and font produce byte-identical
bytes. That makes a hash a regression test.

```csharp
var options = new ExportOptions { FontSource = FontSource.Embedded, CellHeight = 16 };
byte[] actual = window.CaptureSurface().ToImage(options);

Assert.Equal(File.ReadAllBytes("testdata/golden/settings.png"), actual);
```

**`FontSource.Embedded` is not optional here.** The default prefers a font installed on the
machine, which differs between a laptop, CI and a colleague's Mac -- three machines, three
hashes, for the same UI. The embedded face ships in the library and renders identically
everywhere, including a container with no fonts at all. A baseline recorded without it is
only valid on the machine that recorded it.

Write the actual bytes out on failure, not just the hash: when it fails you want to look at
the picture.

**Determinism is a property of the pixels, not of your UI.** Anything on screen that changes
by itself makes the bytes change with it -- a status-bar clock is the usual culprit, and the
composed desktop includes one by default. Either capture a window rather than the desktop,
`Crop` the changing part out, or freeze the value for the test. A baseline that captures a
clock fails once a second.

Combined with [`HeadlessConsoleDriver`](DRIVERS.md), this runs with no terminal:

```csharp
var driver = new HeadlessConsoleDriver(120, 40);
var ws = new ConsoleWindowSystem(driver, ConsoleWindowSystemOptions.Create());
BuildYourUI(ws);

using var session = ws.BeginHosted();
for (int i = 0; i < 10; i++) session.Tick();   // let it settle

byte[] png = theWindow.CaptureSurface().ToImage(
    new ExportOptions { FontSource = FontSource.Embedded });
```

---

## Capture now, export later

A surface over a live buffer reads the buffer *as it is when read*. If anything repaints in
between -- a dialog opening, say -- the export describes the new screen, not the one you
meant.

Take a snapshot first. It is a deep copy, so it survives repaints and an `await`:

```csharp
var snapshot = window.Buffer!.CreateSnapshot();   // cheap, immutable, now

var path = await FileDialogs.ShowSaveFileAsync(/* ... */);   // repaints the screen

File.WriteAllBytes(path, snapshot.AsSurface().ToImage());    // still the old screen
```

---

## Defaulted colours

The composed desktop stores the escape the renderer emitted rather than the values behind it,
and that escape may say `39`/`49` -- "whatever the terminal's default is" -- a colour this
process genuinely does not know.

Those cells are filled from the theme and **flagged**, rather than passed off as measured:

```csharp
var cell = surface[x, y];
if (cell.BackgroundWasDefaulted)
{
    // substituted from the theme, not read from the frame
}
```

`ToText(TextFormat.Ansi)` honours the flag by emitting `49` rather than the substitute, so
the output round-trips through a real terminal correctly instead of baking in a guess. A
per-window surface never sets it: those cells carry real typed colour.

---

## See also

- [`DRIVERS.md`](DRIVERS.md) -- the headless driver, for exporting with no terminal
- [`DEBUGGING.md`](DEBUGGING.md) -- the diagnostics socket, which exposes the same exports
  over a socket to another process
- [`MARKUP_SYNTAX.md`](MARKUP_SYNTAX.md) -- the syntax `TextFormat.Markup` produces

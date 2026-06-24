# FigleControl

Render large text as FIGlet ASCII art for banners, titles, and clocks.

## Overview

FigleControl renders text using FIGlet ASCII art fonts, turning short strings into large multi-line block letters. It uses a built-in FIGlet parser and embedded fonts, so it has no dependency on Spectre.Console.

Three embedded font sizes are available (Small, Default/Standard, Large/Banner), or you can supply a custom `FigletFont` instance or load a `.flf` font file by name. Loaded fonts are cached internally so the text is not re-parsed on every render frame.

The control supports color, horizontal alignment (including a `Centered()` shortcut), margins, a fixed width, and optional word/character wrapping when the rendered art is wider than the available space. It is a display-only control with no keyboard or mouse interaction.

See also: [MarkupControl](MarkupControl.md)

## Quick Start

```csharp
var banner = Controls.Figlet("Hello")
    .Centered()
    .WithColor(Color.Cyan)
    .Build();

window.AddControl(banner);
```

## Builder API

Create a builder with `Controls.Figlet(text)` (text is optional) or `new FigleControlBuilder()`.

### Content

```csharp
.WithText("Banner Text")            // Text to render as ASCII art
.WithColor(Color.Cyan)              // Text color
```

### Font / Size

```csharp
.WithSize(FigletSize.Default)       // Embedded font size
.Small()                            // Shortcut for FigletSize.Small (~4 lines)
.Large()                            // Shortcut for FigletSize.Large (~8 lines)
.WithCustomFont(myFigletFont)       // Use a FigletFont instance (highest precedence)
.WithFontPath("slant")              // Load 'fonts/slant.flf' relative to app base dir
```

### Layout

```csharp
.WithAlignment(HorizontalAlignment.Center)  // Horizontal alignment
.Centered()                                 // Shortcut for HorizontalAlignment.Center
.WithVerticalAlignment(VerticalAlignment.Top)
.WithWrapMode(WrapMode.WrapWords)           // Wrap when wider than available width
.WithWidth(60)                              // Fixed control width
.WithMargin(1)                              // Uniform margin
.WithMargin(left, top, right, bottom)       // Per-side margins
.WithRightPadded(true)                      // Whether the right side is padded
```

### Positioning / Identity

```csharp
.StickyTop()                        // Stick to top of window
.StickyBottom()                     // Stick to bottom of window
.WithStickyPosition(StickyPosition.None)
.Visible(true)                      // Control visibility
.WithName("banner")                 // Name for FindControl lookup
.WithTag(myObject)                  // Arbitrary tag object
```

### Build

```csharp
.Build()                            // Returns the configured FigleControl
```

A `FigleControlBuilder` also implicitly converts to `FigleControl`, so it can be passed wherever a control is expected without an explicit `.Build()` call.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string?` | `null` | The text to render as FIGlet ASCII art |
| `Color` | `Color?` | `null` | Color of the FIGlet text (falls back to container/default foreground) |
| `Size` | `FigletSize` | `FigletSize.Default` | FIGlet text size using embedded fonts |
| `CustomFont` | `FigletFont?` | `null` | Custom font instance; takes precedence over `Size` and `FontPath` |
| `FontPath` | `string?` | `null` | Name of a `.flf` font file in the `fonts` directory (without extension); takes precedence over `Size` but below `CustomFont` |
| `WrapMode` | `WrapMode` | `WrapMode.NoWrap` | Wrap mode when the rendered text exceeds available width |
| `RightPadded` | `bool` | `true` | Whether the right side should be padded |

The control also inherits common `BaseControl` members such as `HorizontalAlignment`, `VerticalAlignment`, `Margin`, `Width`, `Visible`, `Name`, `Tag`, and `StickyPosition`.

### FigletSize Values

| Value | Description |
|-------|-------------|
| `FigletSize.Small` | Small font (~4 lines height) |
| `FigletSize.Default` | Default/Standard font (~6 lines height) — the default |
| `FigletSize.Large` | Large/Banner font (~8 lines height) |
| `FigletSize.Custom` | Custom font provided by the user |

### Font Resolution Order

When rendering, the font is chosen by this precedence:

1. `CustomFont` (if set)
2. `FontPath` (if set and the `.flf` file is found under `fonts/`)
3. `Size` (embedded `small.flf` / `standard.flf` / `banner.flf`)

`FontPath` is validated to prevent path traversal — the resolved path must stay within the application's `fonts` directory, otherwise an `ArgumentException` is thrown.

## Methods

| Method | Description |
|--------|-------------|
| `SetText(string text)` | Sets the text to render |
| `SetColor(Color color)` | Sets the text color |

## Events

FigleControl does not expose any control-specific events.

## Examples

### Simple Banner

```csharp
window.AddControl(
    Controls.Figlet("WELCOME")
        .Centered()
        .WithColor(Color.Yellow)
        .Build()
);
```

### Small and Large Fonts

```csharp
// Compact heading
window.AddControl(
    Controls.Figlet("Menu")
        .Small()
        .Build()
);

// Big banner
window.AddControl(
    Controls.Figlet("SALE")
        .Large()
        .WithColor(Color.Red)
        .Centered()
        .Build()
);
```

### Wrapping Long Text

```csharp
var banner = Controls.Figlet("SharpConsoleUI")
    .Centered()
    .WithColor(Color.Cyan)
    .WithWrapMode(WrapMode.WrapWords)
    .Build();

window.AddControl(banner);
```

### Live Updating Clock

```csharp
var clock = Controls.Figlet(DateTime.Now.ToString("HH:mm:ss"))
    .Centered()
    .WithColor(Color.Green)
    .Build();

new WindowBuilder(ws)
    .WithTitle("Clock")
    .WithSize(50, 12)
    .Centered()
    .AddControl(clock)
    .WithAsyncWindowThread(async (window, ct) =>
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);
            clock.Text = DateTime.Now.ToString("HH:mm:ss");
            window.Invalidate(Invalidation.Relayout);
        }
    })
    .BuildAndShow();
```

### Custom Font File

```csharp
// Loads 'fonts/slant.flf' relative to the application base directory
var banner = Controls.Figlet("Slanted")
    .WithFontPath("slant")
    .WithColor(Color.Magenta)
    .Build();

window.AddControl(banner);
```

### Custom Font Instance

```csharp
using var stream = File.OpenRead("path/to/myfont.flf");
var font = FigletFont.Load(stream);

var banner = Controls.Figlet("Custom")
    .WithCustomFont(font)
    .Centered()
    .Build();

window.AddControl(banner);
```

## Best Practices

1. **Keep text short**: FIGlet art is wide; long strings overflow quickly. Prefer single words or short phrases for banners and titles.
2. **Use wrapping for variable text**: When the rendered width may exceed the window, set `WithWrapMode(WrapMode.WrapWords)` so the art reflows instead of clipping.
3. **Update via the property**: For live displays like clocks, assign `clock.Text` and call `window.Invalidate(Invalidation.Relayout)` rather than recreating the control — the font is cached and reused. (FIGlet glyph widths vary, so a text change can change the rendered size; use `Relayout`.)
4. **Pick the size for the space**: Use `Small()` for tight headings and `Large()` only where there is room for ~8 lines of height.
5. **Keep custom fonts in `fonts/`**: `FontPath` only resolves files inside the application's `fonts` directory; absolute paths and `..` traversal are rejected.
6. **Marshal updates from background work**: When updating `Text` from timers or `Task.Run`, route the change through the UI thread (e.g. `EnqueueOnUIThread`) as with any control mutation.

## See Also

- [MarkupControl](MarkupControl.md) - For rich formatted multi-line text

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

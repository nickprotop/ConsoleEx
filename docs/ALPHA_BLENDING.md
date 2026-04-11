# Alpha Blending & Transparent Windows

SharpConsoleUI provides per-cell alpha compositing using Porter-Duff "over" blending. Any color with alpha < 255 composites against the content beneath it — including window backgrounds, control colors, and markup text.

## Table of Contents

1. [Overview](#overview)
2. [Color Alpha](#color-alpha)
3. [Transparent Windows](#transparent-windows)
4. [TransparencyBrush](#transparencybrush)
5. [Border Compositing](#border-compositing)
6. [Control-Level Alpha](#control-level-alpha)
7. [Performance](#performance)
8. [Examples](#examples)
9. [Best Practices](#best-practices)

## Overview

Alpha blending happens at two levels:

1. **Intra-window** — controls with semi-transparent colors blend against the window's background within the `CharacterBuffer` at paint time.
2. **Inter-window** — transparent windows (where `BackgroundColor.A < 255`) composite against windows below and the desktop background at render time.

The blending function is `Color.Blend(src, dst)` — a Porter-Duff "source over" operation that always produces a fully opaque result (A=255). Alpha is consumed at blend time.

## Color Alpha

Every `Color` has an alpha channel (0–255):

```csharp
// Fully opaque (default)
var solid = new Color(255, 0, 0);           // A = 255

// Semi-transparent
var glass = new Color(255, 0, 0, 128);      // 50% red

// Fully transparent
var clear = Color.Transparent;               // A = 0

// Modify alpha on existing color
var faded = Color.Red.WithAlpha(128);        // 50% red
```

### Porter-Duff Blend

```csharp
var result = Color.Blend(src, dst);
// src.A == 255 → returns src (fully opaque, covers everything)
// src.A == 0   → returns dst (fully transparent, shows what's below)
// src.A == 128 → blends 50/50 between src and dst RGB values
// Result always has A = 255
```

## Transparent Windows

Setting a window's background color with alpha < 255 makes it transparent:

```csharp
var window = new WindowBuilder(ws)
    .WithTitle("Transparent")
    .WithSize(40, 15)
    .WithBackgroundColor(new Color(0, 20, 60, 180))  // 70% opaque dark blue
    .BuildAndShow();
```

### Default Behavior (True Transparency)

When no `TransparencyBrush` is set, the window is truly transparent:

- **Background**: composites at the window's raw alpha against what's below
- **Character bubble-up**: empty cells (spaces) in the window show characters from windows/desktop beneath, with faded foreground
- **Parabolic foreground fade**: characters from below fade faster than the background bleed, using `fadeAlpha = 1 - (1 - α/255)²`
- **Block character guard**: block characters (`█▀▄░▒▓`) use their foreground as the effective background for compositing, since they visually fill the entire cell

| Window Alpha | Background Opacity | Foreground Fade | Character Visibility |
|-------------|-------------------|----------------|---------------------|
| 64          | 25%               | 44%            | Clearly visible |
| 128         | 50%               | 75%            | Notably dimmed |
| 180         | 71%               | 91%            | Barely visible |
| 220         | 86%               | 98%            | Ghost |

### How It Works

The compositor (`Renderer.cs`) walks the Z-order for each cell:

1. If the cell's background alpha is 255, write it directly (fast path)
2. If alpha < 255, call `ResolveCellBelow()` to find what's beneath at that screen position
3. Composite: `resolvedBg = Color.Blend(cellBg, bgBelow)`
4. For empty cells, bubble up the character from below with faded foreground
5. Write the composited cell

`ResolveCellBelow` walks windows in descending Z-order, checking content areas and borders. It falls through to the desktop background buffer as the final layer.

### Invalidation

Transparent windows automatically re-render when:
- The desktop background animates (gradient, pattern changes)
- A window below the transparent window changes content
- The transparent window itself is moved, resized, or activated

## TransparencyBrush

The `TransparencyBrush` overrides the default true-transparency compositing style. The brush does **not** own the color or alpha — it only controls how compositing is performed.

```csharp
.WithBackgroundColor(new Color(0, 20, 60, 180))
.WithTransparencyBrush(TransparencyBrush.Mica())
```

### Brush Styles

| Style | Background | Character Bubble-Up | Use Case |
|-------|-----------|--------------------|---------| 
| *(none)* | Raw alpha blend | Yes, parabolic fg fade | True transparency — see through the window |
| **Acrylic** | Gaussian blend (fg+bg below) | Yes, configurable power-curve fade | Rich color impression with faded text bleed |
| **Mica** | Gaussian blend (fg+bg below) | No | Tinted color field — no text visible, WinUI Mica analog |
| **Tinted** | Raw bg-only blend | No | Simple colored overlay filter |
| **Custom** | User-defined | User-defined | Full control via delegate |

### Acrylic

Uses `PerceivedCellColor` to compute a Gaussian-style blend of the cell below's foreground and background, weighted by estimated glyph coverage. Characters bubble up with a configurable power-curve fade.

```csharp
.WithTransparencyBrush(TransparencyBrush.Acrylic())

// Custom fade exponent (lower = more aggressive fade)
.WithTransparencyBrush(TransparencyBrush.Acrylic(fadeExponent: 0.25f))

// Custom text coverage estimate (0-255)
.WithTransparencyBrush(TransparencyBrush.Acrylic(textCoverage: 120))
```

### Mica

Like Acrylic for background (Gaussian color impression from content below) but without character bubble-up. You see tinted color fields, not text — similar to WinUI's Mica material.

```csharp
.WithTransparencyBrush(TransparencyBrush.Mica())
```

### Tinted

Simple flat overlay — composites only background colors. No foreground influence from below, no character bubble-up, no block character guard. Just a colored filter.

```csharp
.WithTransparencyBrush(TransparencyBrush.Tinted())
```

### Custom

Full control via a per-cell compositing delegate:

```csharp
.WithTransparencyBrush(TransparencyBrush.WithCustom((topCell, cellBelow, alpha) =>
{
    var bg = Color.Blend(topCell.Background, cellBelow.Background);
    return new Cell(cellBelow.Character, cellBelow.Foreground, bg);
}))
```

The delegate receives `(Cell topCell, Cell cellBelow, byte overlayAlpha)` and returns the composited `Cell`.

## Border Compositing

Borders of transparent windows also composite against content below. The compositor re-renders border cells after `BorderRenderer` writes them, using the same `ResolveCellBelow` mechanism. Both horizontal borders (from cached border buffers) and vertical borders (including scrollbar) are composited.

## Control-Level Alpha

Controls inside a window can use semi-transparent colors:

```csharp
// Semi-transparent markup text
window.AddControl(new MarkupControl(new List<string>
{
    "[#FF000080]50% red text[/]",           // foreground alpha
    "[on #0000FF80]50% blue background[/]"  // background alpha
}));
```

Control-level alpha blends within the window's `CharacterBuffer` at paint time. If the resulting cell background still has alpha < 255 (because the window background also has alpha), the compositor resolves it against layers below.

## Terminal Transparency

Modern terminal emulators (Kitty, Alacritty, WezTerm) support native background transparency via settings like `background_opacity`. To allow the terminal's transparency to show through SharpConsoleUI, set the desktop background to `Color.Transparent`:

### Full Terminal Transparency

```csharp
// Set desktop to transparent — terminal wallpaper shows through exposed areas
windowSystem.DesktopBackgroundColor = Color.Transparent;

// Window with transparent background — terminal shows through
var window = new WindowBuilder(ws)
    .WithTitle("Transparent")
    .WithBackgroundColor(Color.Transparent)
    .Maximized()
    .Borderless()
    .BuildAndShow();
```

### How It Works

When a cell's background color has alpha = 0 (`Color.Transparent`), the ANSI formatter emits `\x1b[49m` (reset to terminal default background) instead of explicit `\x1b[48;2;R;G;Bm`. This tells the terminal emulator to use its configured default background, which may be semi-transparent.

### Terminal Configuration

**Kitty** (`~/.config/kitty/kitty.conf`):
```
background_opacity 0.7
```

**Alacritty** (`~/.config/alacritty/alacritty.toml`):
```toml
[window]
opacity = 0.7
```

**WezTerm** (`~/.config/wezterm/wezterm.lua`):
```lua
config.window_background_opacity = 0.7
```

### Semi-Transparent Windows over Transparent Desktop

When a semi-transparent window sits over a transparent desktop, there's a conflict: the window wants to apply a color tint, but there's no known color to tint against. The `TerminalTransparencyMode` option controls this:

```csharp
// Default: window tint blends against black, emits explicit RGB.
// Terminal transparency is lost under the window, but the tint color is preserved.
var options = new ConsoleWindowSystemOptions(
    TerminalTransparencyMode: TerminalTransparencyMode.PreserveWindowColor
);

// Alternative: emit 49m for any non-opaque cell over transparent desktop.
// Terminal transparency shows through the window, but the tint color is lost.
var options = new ConsoleWindowSystemOptions(
    TerminalTransparencyMode: TerminalTransparencyMode.PreserveTerminalTransparency
);
```

## Performance

- **Opaque windows** (A=255): zero overhead — single branch check, fast path only
- **Transparent windows**: per-cell compositing with `ResolveCellBelow` walk. Cost scales with overlapping window count (typically 2-3)
- **Scratch buffer**: allocated once per `Renderer` instance, reused across frames
- **Recursion**: bounded by overlapping window count + depth guard at 20

## Examples

### Semi-Transparent Overlay

```csharp
var overlay = new WindowBuilder(ws)
    .WithTitle("Overlay")
    .WithSize(50, 20)
    .WithBackgroundColor(new Color(0, 0, 40, 160))
    .BuildAndShow();
```

### Frosted Glass Panel (Mica)

```csharp
var panel = new WindowBuilder(ws)
    .WithTitle("Settings")
    .WithSize(60, 25)
    .WithBackgroundColor(new Color(20, 20, 30, 200))
    .WithTransparencyBrush(TransparencyBrush.Mica())
    .BuildAndShow();
```

### Dynamic Alpha with Slider

```csharp
byte alpha = 128;
slider.ValueChanged += (_, _) =>
{
    alpha = (byte)slider.Value;
    var old = window.BackgroundColor;
    window.BackgroundColor = new Color(old.R, old.G, old.B, alpha);
};
```

## Best Practices

- Use transparent windows sparingly — each one adds per-cell compositing cost
- The default true-transparency style is best for overlays where seeing content below matters
- Use **Mica** or **Tinted** brush when you want visual depth without text bleed-through
- Set `WithBackgroundColor` alpha between 160–220 for most uses — below 128 is very transparent, above 230 is barely noticeable
- Transparent windows automatically invalidate when content below changes — no manual refresh needed

## See Also

- [Compositor Effects](COMPOSITOR_EFFECTS.md) — post-processing buffer effects
- [Gradients](GRADIENTS.md) — gradient backgrounds that work with transparency
- [Desktop Background](DESKTOP_BACKGROUND.md) — the bottom layer of the compositing stack
- [Rendering Pipeline](RENDERING_PIPELINE.md) — how the full render chain works

[Back to Main Documentation](../README.md)

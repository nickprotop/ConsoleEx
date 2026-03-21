# Gradients

SharpConsoleUI supports gradient colors for both text and window backgrounds, extending the existing `ColorGradient` system.

## Table of Contents

1. [Gradient Text in Markup](#gradient-text-in-markup)
2. [Window Background Gradients](#window-background-gradients)
3. [CharacterBuffer Direct API](#characterbuffer-direct-api)
4. [Gradient Direction](#gradient-direction)
5. [Predefined Gradients](#predefined-gradients)
6. [Custom Gradients](#custom-gradients)

## Gradient Text in Markup

Use the `[gradient=...]` tag in markup to apply color gradients across text:

```csharp
// Named gradient
window.AddControl(new MarkupControl("[gradient=spectrum]Rainbow Text[/]"));

// Two-color gradient
window.AddControl(new MarkupControl("[gradient=blue->cyan]Ocean Breeze[/]"));

// Multi-stop gradient
window.AddControl(new MarkupControl("[gradient=red->yellow->green]Traffic Light[/]"));

// Predefined gradient names
window.AddControl(new MarkupControl("[gradient=warm]Temperature Rising[/]"));
window.AddControl(new MarkupControl("[gradient=cool]Arctic Chill[/]"));
```

### How It Works

The parser measures the text span length, then assigns per-character interpolated foreground colors. Inner decorations (bold, underline, etc.) are preserved.

```csharp
// Gradient with decorations
"[gradient=blue->white][bold]Bold Gradient[/][/]"

// Nested tags inside gradient
"[gradient=spectrum]Hello [underline]World[/][/]"
```

### Separator Syntax

Both `→` (Unicode arrow) and `->` (ASCII) are supported as color stop separators:

```csharp
"[gradient=red→blue]Unicode arrow[/]"
"[gradient=red->blue]ASCII arrow[/]"
```

## Window Background Gradients

Apply gradient backgrounds to entire windows:

```csharp
// Via builder
var window = new WindowBuilder(ws)
    .WithTitle("Gradient Background")
    .WithBackgroundGradient(
        ColorGradient.FromColors(Color.DarkBlue, Color.Black),
        GradientDirection.Vertical)
    .Build();

// Programmatic
window.BackgroundGradient = new GradientBackground(
    ColorGradient.FromColors(Color.DarkBlue, Color.DarkCyan),
    GradientDirection.Horizontal);

// Remove gradient
window.BackgroundGradient = null;
```

Background gradients are rendered via `PreBufferPaint`, so all controls paint on top of the gradient.

## CharacterBuffer Direct API

Apply gradients directly to any `CharacterBuffer` region:

```csharp
// In a PostBufferPaint handler or CanvasControl
buffer.FillGradient(
    new LayoutRect(0, 0, 40, 10),
    ColorGradient.Predefined["spectrum"],
    GradientDirection.DiagonalDown);
```

This delegates to `GradientRenderer.FillGradient()` which handles the interpolation and direction logic.

## Gradient Direction

```csharp
public enum GradientDirection
{
    Horizontal,   // Left to right
    Vertical,     // Top to bottom
    DiagonalDown, // Top-left to bottom-right
    DiagonalUp    // Bottom-left to top-right
}
```

## Predefined Gradients

Access via `ColorGradient.Predefined`:

| Name | Colors |
|------|--------|
| `spectrum` | Full rainbow (red → orange → yellow → green → cyan → blue → violet) |
| `warm` | Red → Orange → Yellow |
| `cool` | Blue → Cyan → Green |

```csharp
var gradient = ColorGradient.Predefined["spectrum"];
```

## Custom Gradients

### FromColors Factory

```csharp
// Two colors
var gradient = ColorGradient.FromColors(Color.Red, Color.Blue);

// Multiple stops
var gradient = ColorGradient.FromColors(
    Color.Red, Color.Yellow, Color.Green, Color.Cyan, Color.Blue);
```

### Parse from String

```csharp
// Color names
var gradient = ColorGradient.Parse("red->blue");

// Hex colors
var gradient = ColorGradient.Parse("#FF0000->#0000FF");

// Predefined name
var gradient = ColorGradient.Parse("spectrum");
```

## Transparent Control Backgrounds

Controls support `Color.Transparent` as their background color, which tells the renderer to
composite the control's cells against whatever is already in the buffer — including window
background gradients.

```csharp
// Control floats on top of the window gradient — no opaque rectangle behind it
var label = new MarkupControl("[bold cyan]CPU Usage[/]");
// BackgroundColor defaults to null, which resolves to Color.Transparent

// Opaque override — solid background, ignores whatever is behind the control
label.BackgroundColor = Color.Grey11;
```

### The Resolution Chain

Each control resolves its background through a priority chain:

1. **Control's own `BackgroundColor`** — if explicitly set to a non-transparent color
2. **Theme slot** — e.g. `ITheme.LineGraphBackgroundColor`, `ITheme.SparklineBackgroundColor`
3. **`Color.Transparent`** — the terminal fallback

`Color.Transparent` is not "no color" — it is a definite instruction: *show what is underneath*.
The underlying content might be the window's gradient background, another control, or the desktop
fill.

### Alpha Compositing in CharacterBuffer

When a control writes a cell with `Color.Transparent` as the background, `CharacterBuffer`
resolves the displayed background from whatever was previously written at that position —
typically the gradient painted by `PreBufferPaint` before controls render.

```
cell written with Transparent bg  →  display_bg = gradient color at that position
cell foreground                   →  displayed as-is over the composited background
```

This means gradient window backgrounds remain visible beneath any control that uses
`Color.Transparent`, even when that control has opaque foreground characters.

```csharp
// Window with a blue→black vertical gradient
var window = new WindowBuilder(ws)
    .WithBackgroundGradient(
        ColorGradient.FromColors(Color.DarkBlue, Color.Black),
        GradientDirection.Vertical)
    .Build();

// SparklineControl with transparent background — gradient shows through the graph area
var spark = new SparklineControl();
// BackgroundColor is null → resolves to Color.Transparent → gradient visible behind bars
window.AddControl(spark);
```

### Alpha Channel in Color

`Color` carries an alpha channel. `ColorGradient.BlendColors` interpolates all four channels
(R, G, B, A), so gradients can themselves fade to transparent — creating controls whose
background dissolves into the window gradient at one edge.

```csharp
// Gradient that fades from cyan to fully transparent
var fadeOut = ColorGradient.FromColors(Color.Cyan, Color.FromArgb(0, 0, 255, 255));
var bar = new BarGraphControl { SmoothGradient = fadeOut };
```

## See Also

- [Markup Syntax](MARKUP_SYNTAX.md) — Full markup reference
- [Compositor Effects](COMPOSITOR_EFFECTS.md) — Buffer manipulation hooks
- [Themes](THEMES.md) — Customizing visual appearance

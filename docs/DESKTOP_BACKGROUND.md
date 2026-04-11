# Desktop Background

SharpConsoleUI supports rich desktop backgrounds — the area behind all windows. Backgrounds can be solid colors (from the theme), gradients, repeating patterns, or fully animated via custom paint callbacks.

## Table of Contents

- [Overview](#overview)
- [Configuration](#configuration)
- [Gradient Backgrounds](#gradient-backgrounds)
- [Pattern Backgrounds](#pattern-backgrounds)
- [Combined (Gradient + Pattern)](#combined-gradient--pattern)
- [Animated Backgrounds](#animated-backgrounds)
- [Built-in Effects](#built-in-effects)
- [Custom Paint Callbacks](#custom-paint-callbacks)
- [Theme Integration](#theme-integration)
- [Runtime Changes](#runtime-changes)

## Overview

The desktop background is managed by `DesktopBackgroundService`, which maintains a cached `CharacterBuffer`. The buffer is rendered once and blitted to exposed desktop regions each frame. It is only re-rendered when the configuration changes, the theme changes, the screen resizes, or an animation timer fires.

Three composable layers:
1. **Base fill** — theme character + colors (`DesktopBackgroundChar`, `DesktopBackgroundColor`, `DesktopForegroundColor`)
2. **Gradient overlay** — optional `GradientBackground` applied over the base fill
3. **Pattern overlay** — optional `DesktopPattern` tiled over the gradient/base

When an animated `PaintCallback` is set, it takes full control of the buffer.

## Configuration

Set the desktop background at startup via `ConsoleWindowSystemOptions`:

```csharp
using SharpConsoleUI.Rendering;

var options = new ConsoleWindowSystemOptions(
    DesktopBackground: DesktopBackgroundConfig.FromGradient(
        ColorGradient.FromColors(new Color(10, 15, 40), new Color(2, 3, 10)),
        GradientDirection.Vertical)
);

var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: options);
```

Or change it at runtime:

```csharp
windowSystem.DesktopBackground = DesktopBackgroundConfig.FromGradient(
    ColorGradient.FromColors(Color.DarkBlue, Color.Black),
    GradientDirection.Vertical);
```

Setting to `null` reverts to theme defaults:

```csharp
windowSystem.DesktopBackground = null;
```

All changes are applied automatically on the next frame — no manual redraw needed.

## Gradient Backgrounds

Apply a color gradient across the entire desktop area:

```csharp
windowSystem.DesktopBackground = DesktopBackgroundConfig.FromGradient(
    ColorGradient.FromColors(new Color(0, 30, 60), new Color(0, 5, 15)),
    GradientDirection.Vertical);
```

All four gradient directions are supported:

| Direction | Effect |
|-----------|--------|
| `Vertical` | Top to bottom |
| `Horizontal` | Left to right |
| `DiagonalDown` | Top-left to bottom-right |
| `DiagonalUp` | Bottom-left to top-right |

Multi-stop gradients work too:

```csharp
var gradient = ColorGradient.FromColors(
    new Color(10, 10, 40),
    new Color(40, 10, 30),
    new Color(10, 10, 40));

windowSystem.DesktopBackground = DesktopBackgroundConfig.FromGradient(
    gradient, GradientDirection.Horizontal);
```

## Pattern Backgrounds

Tile a repeating character pattern across the desktop:

```csharp
windowSystem.DesktopBackground = DesktopBackgroundConfig.FromPattern(
    DesktopPatterns.Checkerboard);
```

### Built-in Patterns

| Preset | Description | Size |
|--------|-------------|------|
| `DesktopPatterns.Checkerboard` | Alternating ░ and space | 2x2 |
| `DesktopPatterns.Dots` | Sparse dots | 3x3 |
| `DesktopPatterns.HatchDown` | Diagonal lines ╲ | 3x3 |
| `DesktopPatterns.HatchUp` | Diagonal lines ╱ | 3x3 |
| `DesktopPatterns.Crosshatch` | Cross-hatching ╳ | 3x3 |
| `DesktopPatterns.LightShade` | Light shade ░ | 1x1 |
| `DesktopPatterns.MediumShade` | Medium shade ▒ | 1x1 |
| `DesktopPatterns.DenseShade` | Dense shade ▓ | 1x1 |
| `DesktopPatterns.HorizontalLines` | Horizontal lines | 1x2 |
| `DesktopPatterns.VerticalLines` | Vertical lines | 3x1 |
| `DesktopPatterns.Grid` | Thin grid | 3x3 |

### Custom Patterns

Define your own tile with a `char[,]` grid:

```csharp
var customPattern = new DesktopPattern(new char[,]
{
    { '╔', '═', '╗' },
    { '║', ' ', '║' },
    { '╚', '═', '╝' }
});

windowSystem.DesktopBackground = DesktopBackgroundConfig.FromPattern(customPattern);
```

Patterns support per-cell foreground and background colors:

```csharp
var pattern = new DesktopPattern(new char[,] { { '●', ' ' }, { ' ', '●' } })
{
    ForegroundColors = new Color?[,]
    {
        { Color.Cyan1, null },
        { null, Color.Green }
    }
};
```

## Combined (Gradient + Pattern)

Set both a gradient and a pattern. The gradient is rendered first, then the pattern characters are tiled on top (inheriting gradient colors where pattern colors are null):

```csharp
windowSystem.DesktopBackground = new DesktopBackgroundConfig
{
    Gradient = new GradientBackground(
        ColorGradient.FromColors(new Color(0, 20, 50), new Color(0, 5, 15)),
        GradientDirection.Vertical),
    Pattern = DesktopPatterns.Dots
};
```

This produces dots whose colors shift along the gradient.

## Animated Backgrounds

Animated backgrounds use a `PaintCallback` that receives the buffer, dimensions, and elapsed time. The callback is invoked on the render thread at `AnimationIntervalMs` intervals.

### Built-in Effects

Three built-in effects are available in `DesktopEffects`:

#### Color Cycling

Smoothly cycles through hues over time:

```csharp
windowSystem.DesktopBackground = DesktopEffects.ColorCycling(
    cycleDurationSeconds: 12.0,
    direction: GradientDirection.Vertical,
    intervalMs: 100);
```

#### Pulse

Subtle brightness pulsing on a base color:

```csharp
windowSystem.DesktopBackground = DesktopEffects.Pulse(
    baseColor: new Color(15, 25, 60),
    pulseRange: 0.15,
    pulseDurationSeconds: 4.0);
```

#### Drifting Gradient

Gradient that cycles through directions:

```csharp
windowSystem.DesktopBackground = DesktopEffects.DriftingGradient(
    color1: new Color(20, 40, 80),
    color2: new Color(60, 20, 70),
    cycleDurationSeconds: 8.0);
```

### Custom Paint Callbacks

For full control, provide a `PaintCallback`:

```csharp
windowSystem.DesktopBackground = new DesktopBackgroundConfig
{
    AnimationIntervalMs = 70,
    PaintCallback = (buffer, width, height, elapsed) =>
    {
        // Paint directly to the CharacterBuffer.
        // Called on the render thread at AnimationIntervalMs intervals.
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double t = (Math.Sin(elapsed.TotalSeconds + x * 0.1 + y * 0.05) + 1.0) / 2.0;
                byte b = (byte)(t * 40);
                buffer.SetCell(x, y, new Cell(' ', Color.White, new Color(0, 0, b)));
            }
    }
};
```

**Performance notes:**
- The callback runs on the render thread — keep it fast
- `AnimationIntervalMs` controls how often the buffer is re-rendered (default: 100ms / 10 FPS)
- Only exposed desktop regions (not covered by windows) are blitted to screen
- When an animation is active during window drag, the buffer is re-rendered to keep the exposed area in sync

### Stopping Animation

Set the background to `null` or a static config:

```csharp
windowSystem.DesktopBackground = null;  // Revert to theme default
```

## Theme Integration

Themes can provide a default desktop gradient via `DesktopBackgroundGradient`:

```csharp
public class MyTheme : ITheme
{
    // ... other theme properties ...

    public GradientBackground? DesktopBackgroundGradient { get; set; }
        = new GradientBackground(
            ColorGradient.FromColors(new Color(10, 10, 30), Color.Black),
            GradientDirection.Vertical);
}
```

The priority is:
1. `ConsoleWindowSystem.DesktopBackground` config gradient (if set)
2. Theme's `DesktopBackgroundGradient` (if set)
3. `ConsoleWindowSystem.DesktopBackground` config `BackgroundColor` (if set)
4. Theme's solid `DesktopBackgroundColor` + `DesktopBackgroundChar`

If `DesktopBackgroundGradient` returns `null` (the default), the solid color is used.

## Runtime Changes

All desktop background changes are applied automatically:

```csharp
// These all take effect on the next frame — no manual action needed
windowSystem.DesktopBackgroundColor = new Color(20, 20, 40);       // Solid color
windowSystem.DesktopBackgroundColor = Color.Transparent;            // Terminal transparency
windowSystem.DesktopBackground = DesktopBackgroundConfig.FromGradient(...);
windowSystem.DesktopBackground = DesktopBackgroundConfig.FromPattern(...);
windowSystem.DesktopBackground = DesktopEffects.ColorCycling();
windowSystem.DesktopBackground = null;  // Reset to theme default
```

### Terminal Transparency

Set `DesktopBackgroundColor` to `Color.Transparent` to allow the terminal emulator's native transparency to show through. The ANSI formatter emits `\x1b[49m` (terminal default background) for cells with alpha = 0. See [Alpha Blending](ALPHA_BLENDING.md#terminal-transparency) for details.

```csharp
windowSystem.DesktopBackgroundColor = Color.Transparent;
```

The background correctly handles:
- **Screen resize** — buffer is re-rendered at new dimensions
- **Panel visibility changes** — background adapts to new desktop geometry
- **Theme changes** — base colors and theme gradient update automatically
- **Window move/close** — exposed desktop regions are restored from the cached buffer

## See Also

- [Configuration Guide](CONFIGURATION.md) — `DesktopBackground` option
- [Gradients & Alpha](GRADIENTS.md) — `ColorGradient`, `GradientDirection`, window gradients
- [Themes](THEMES.md) — `DesktopBackgroundGradient` theme property
- [Compositor Effects](COMPOSITOR_EFFECTS.md) — `PreBufferPaint`/`PostBufferPaint` for per-window effects

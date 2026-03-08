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

## See Also

- [Markup Syntax](MARKUP_SYNTAX.md) — Full markup reference
- [Compositor Effects](COMPOSITOR_EFFECTS.md) — Buffer manipulation hooks
- [Themes](THEMES.md) — Customizing visual appearance

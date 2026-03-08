# Image Rendering

SharpConsoleUI can render pixel-based images in the console using Unicode half-block characters. Each character cell represents 2 vertical pixels: the foreground color is the top pixel and the background color is the bottom pixel.

## Table of Contents

1. [Overview](#overview)
2. [PixelBuffer](#pixelbuffer)
3. [ImageControl](#imagecontrol)
4. [Scale Modes](#scale-modes)
5. [Alignment and Scale Mode Interaction](#alignment-and-scale-mode-interaction)
6. [Half-Block Rendering](#half-block-rendering)
7. [Creating Test Images](#creating-test-images)

## Overview

The imaging system consists of:

- **`PixelBuffer`** — A 2D buffer of RGB pixels
- **`ImageControl`** — A `BaseControl` that renders a `PixelBuffer` using half-block characters
- **`HalfBlockRenderer`** — Converts pixel data to `Cell[,]` arrays using `▀` (U+2580)
- **`ImageScaleMode`** — Controls how images scale to fit available space

## PixelBuffer

A simple 2D pixel buffer for storing RGB image data.

```csharp
// Create a 100x50 pixel buffer
var pixels = new PixelBuffer(100, 50);

// Set individual pixels
pixels.SetPixel(0, 0, new ImagePixel(255, 0, 0));   // Red
pixels.SetPixel(1, 0, new ImagePixel(0, 255, 0));   // Green
pixels.SetPixel(0, 1, new ImagePixel(0, 0, 255));   // Blue

// Get pixel
ImagePixel pixel = pixels.GetPixel(0, 0);

// Resize with bilinear interpolation
PixelBuffer resized = pixels.Resize(50, 25);
```

### Creating from Arrays

```csharp
// From ImagePixel array (row-major order)
var pixelArray = new ImagePixel[width * height];
// ... fill array ...
var buffer = PixelBuffer.FromPixelArray(pixelArray, width, height);

// From ARGB int array (alpha is ignored)
var argbArray = new int[width * height];
// ... fill array ...
var buffer = PixelBuffer.FromArgbArray(argbArray, width, height);
```

## ImageControl

Add images to windows using `ImageControl`:

```csharp
// Create and add image
var pixels = new PixelBuffer(40, 20);
// ... fill pixels ...

window.AddControl(new ImageControl
{
    Source = pixels,
    ScaleMode = ImageScaleMode.Fit
});

// Via builder
builder.AddControl(Controls.Image(pixels));
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Source` | `PixelBuffer?` | The pixel data to render |
| `ScaleMode` | `ImageScaleMode` | How the image scales (default: `Fit`) |

Setting `Source` or `ScaleMode` automatically invalidates the render cache and triggers a repaint.

## Scale Modes

```csharp
public enum ImageScaleMode
{
    Fit,     // Scale to fit within bounds, preserving aspect ratio (no upscale)
    Fill,    // Scale to cover bounds, cropping as needed
    Stretch, // Stretch to fill bounds exactly, ignoring aspect ratio
    None     // Display at natural pixel size, clipped to bounds
}
```

### Behavior Summary

| Mode | Expands to fill? | Preserves aspect ratio? | May crop? |
|------|-------------------|------------------------|-----------|
| `Fit` | No — uses natural size | Yes | No |
| `Fill` | Yes — covers available space | Yes | Yes |
| `Stretch` | Yes — fills available space | No | No |
| `None` | No — uses natural size | N/A (no scaling) | Yes (if larger than bounds) |

### Examples

Given a 20x10 pixel image (natural: 20 cols x 5 rows) in a 40x20 space:

- **Fit**: 20x5 (natural size, doesn't upscale)
- **Fill**: 40x20 (scales up to cover, may crop)
- **Stretch**: 40x20 (distorts to fill exactly)
- **None**: 20x5 (natural size, no scaling)

## Alignment and Scale Mode Interaction

Alignment and scale mode are independent concerns:

- **Alignment** (`HorizontalAlignment`, `VerticalAlignment`) — How much space the **control** claims from the layout
- **ScaleMode** — How the **image** fits within the control's allocated space

### Without Explicit Alignment (defaults: Left/Top)

- `Fit` and `None` use natural image dimensions
- `Fill` and `Stretch` expand to use available constraint space

```csharp
// Fit: claims natural size (20x5)
new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.Fit };

// Stretch: claims full constraint space (e.g., 80x25)
new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.Stretch };
```

### With Explicit Alignment

Setting `HorizontalAlignment.Stretch` or `VerticalAlignment.Fill` forces the control to claim all available space in that dimension, regardless of scale mode.

```csharp
// Fit with stretch alignment: claims full width/height,
// but image is scaled to fit within (preserving aspect ratio)
new ImageControl
{
    Source = pixels,
    ScaleMode = ImageScaleMode.Fit,
    HorizontalAlignment = HorizontalAlignment.Stretch,
    VerticalAlignment = VerticalAlignment.Fill
};
```

## Half-Block Rendering

The `HalfBlockRenderer` converts pixel data to console cells using the `▀` (upper half block, U+2580) character:

```
For each cell at (x, y):
  - Foreground color = top pixel  (image row y*2)
  - Background color = bottom pixel (image row y*2+1)
  - Character = '▀'
```

This gives 2x vertical resolution compared to using full characters.

For odd-height images, the last row uses `▀` with the background set to the window's background color.

### Direct Usage

```csharp
// Render at natural size
Cell[,] cells = HalfBlockRenderer.Render(pixelBuffer, backgroundColor);

// Render at specific dimensions (with bilinear resize)
Cell[,] cells = HalfBlockRenderer.RenderScaled(
    pixelBuffer, targetCols, targetRows, backgroundColor);
```

## Creating Test Images

Generate images programmatically without file dependencies:

```csharp
// Rainbow gradient
var pixels = new PixelBuffer(80, 40);
for (int y = 0; y < 40; y++)
{
    for (int x = 0; x < 80; x++)
    {
        double hue = (double)x / 80 * 360;
        var color = HsvToRgb(hue, 1.0, 1.0);
        pixels.SetPixel(x, y, color);
    }
}

// Checkerboard
var checker = new PixelBuffer(40, 40);
for (int y = 0; y < 40; y++)
{
    for (int x = 0; x < 40; x++)
    {
        bool isWhite = (x / 4 + y / 4) % 2 == 0;
        checker.SetPixel(x, y, isWhite
            ? new ImagePixel(255, 255, 255)
            : new ImagePixel(0, 0, 0));
    }
}
```

## Configuration

Image defaults are in `ImagingDefaults`:

| Constant | Default | Description |
|----------|---------|-------------|
| `DefaultScaleMode` | `Fit` | Default scale mode for ImageControl |
| `MaxImageDimension` | 500 | Maximum image dimension (prevents overflow in unbounded layouts) |
| `PixelsPerCell` | 2 | Vertical pixels per character cell |
| `HalfBlockChar` | `'▀'` | The Unicode half-block character used for rendering |

## See Also

- [Controls Reference](CONTROLS.md) — All available controls
- [DOM Layout System](DOM_LAYOUT_SYSTEM.md) — How controls are measured and arranged
- [Compositor Effects](COMPOSITOR_EFFECTS.md) — Post-processing effects

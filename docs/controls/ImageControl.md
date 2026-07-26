# ImageControl

Displays an image in the terminal — full-resolution via the Kitty graphics protocol, with a
half-block fallback everywhere else.

> **The full reference for this control lives in [Image Rendering](../IMAGE_RENDERING.md).**
> This page is a short orientation and a set of links into it.

## Overview

`ImageControl` renders a [`PixelBuffer`](../IMAGE_RENDERING.md#pixelbuffer) into a window. On
terminals that support the Kitty graphics protocol (Kitty, WezTerm, Ghostty) it draws the image at
full pixel resolution; elsewhere it falls back to half-block cells, which gives two vertical pixels
per character row. Detection is automatic — you do not choose the path.

Supported formats are PNG, JPEG, BMP, GIF, WebP and TIFF, loaded from a path, a stream, or an
ImageSharp image.

## Quick Start

```csharp
var pixels = PixelBuffer.FromFile("photo.png");

window.AddControl(Controls.Image(pixels));
```

With the builder, when you need layout or identity options:

```csharp
var image = Controls.Image()
    .WithSource(pixels)
    .WithScaleMode(ImageScaleMode.Fill)
    .WithAlignment(HorizontalAlignment.Center)
    .WithName("preview")
    .Build();
```

## Where to read more

| Topic | Section |
|-------|---------|
| Properties (`Source`, `ScaleMode`, `MinimumWidth`, `MinimumHeight`) | [ImageControl → Properties](../IMAGE_RENDERING.md#properties) |
| All builder methods | [ImageControl → Builder API](../IMAGE_RENDERING.md#builder-api) |
| Creating and filling pixel data | [PixelBuffer](../IMAGE_RENDERING.md#pixelbuffer) |
| Loading from files, streams, ImageSharp, or a file picker | [Loading Images from Files](../IMAGE_RENDERING.md#loading-images-from-files) |
| `Fit` / `Fill` / `Stretch` / `None` and how they behave | [Scale Modes](../IMAGE_RENDERING.md#scale-modes) |
| How alignment interacts with scale mode | [Alignment and Scale Mode Interaction](../IMAGE_RENDERING.md#alignment-and-scale-mode-interaction) |
| Terminal support and how detection works | [Kitty Graphics Protocol](../IMAGE_RENDERING.md#kitty-graphics-protocol) |
| The fallback renderer | [Half-Block Rendering](../IMAGE_RENDERING.md#half-block-rendering) |

## See Also

- [Image Rendering](../IMAGE_RENDERING.md) — the complete guide
- [VideoControl](VideoControl.md) — moving images, same rendering backends
- [CanvasControl](CanvasControl.md) — draw your own graphics cell by cell
- [Controls Reference](../CONTROLS.md) — all controls

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)

# Compositor Effects

SharpConsoleUI provides a powerful compositor-style buffer manipulation API that enables advanced visual effects like transitions, filters, blur, and custom rendering overlays. This system exposes the internal `CharacterBuffer` through safe, event-based hooks that fire at precise points in the rendering pipeline.

## Table of Contents

1. [Overview](#overview)
2. [Core API](#core-api)
3. [Quick Start](#quick-start)
4. [Use Cases](#use-cases)
5. [Examples](#examples)
6. [Best Practices](#best-practices)
7. [Performance Considerations](#performance-considerations)
8. [Thread Safety](#thread-safety)
9. [API Reference](#api-reference)

## Overview

The compositor effects system allows you to manipulate the rendered buffer **after** controls have painted but **before** conversion to ANSI strings. This is the ideal hook point for applying post-processing effects without interfering with the normal rendering pipeline.

### Key Features

- **Post-Paint Hook**: `PostBufferPaint` event fires after painting, before ANSI conversion
- **Direct Buffer Access**: Full `CharacterBuffer` API for cell-level manipulation
- **Immutable Snapshots**: `BufferSnapshot` for safe buffer capture (screenshots, recording)
- **Zero Overhead**: Event system has no cost when not used
- **Thread Safe**: Event fires within existing render lock
- **Flexible**: Supports transitions, filters, overlays, and custom effects

### Architecture

```
Window Rendering Pipeline:
┌─────────────────────┐
│ RebuildDOMTree()    │  Build layout nodes
└──────────┬──────────┘
           │
┌──────────▼──────────┐
│ PerformDOMLayout()  │  Measure & Arrange
└──────────┬──────────┘
           │
┌──────────▼──────────┐
│ PaintDOM()          │  Paint controls to CharacterBuffer
└──────────┬──────────┘
           │
┌──────────▼──────────────────────┐
│ PostBufferPaint Event Fires     │  ◄── YOUR EFFECTS GO HERE
│ (Buffer manipulation allowed)   │
└──────────┬──────────────────────┘
           │
┌──────────▼──────────┐
│ BufferToLines()     │  Convert to ANSI strings
└─────────────────────┘
```

## Core API

### 1. Window.Renderer Property

Exposes the window's internal renderer for accessing rendering internals.

```csharp
public WindowRenderer? Renderer { get; }
```

### 2. WindowRenderer.Buffer Property

Provides direct access to the character buffer.

```csharp
public CharacterBuffer? Buffer { get; }
```

**CAUTION**: Direct buffer manipulation should only be done via the `PostBufferPaint` event to avoid race conditions. Reading is safe at any time.

### 3. WindowRenderer.PostBufferPaint Event

Event that fires after painting controls but before converting to ANSI strings.

```csharp
public delegate void PostBufferPaintDelegate(
    CharacterBuffer buffer,
    LayoutRect dirtyRegion,
    LayoutRect clipRect);

public event PostBufferPaintDelegate? PostBufferPaint;
```

**Parameters**:
- `buffer`: The character buffer that was just painted
- `dirtyRegion`: The region that was painted (or full bounds if entire buffer)
- `clipRect`: The clipping rectangle used during paint

### 4. BufferSnapshot

Immutable snapshot of a CharacterBuffer at a point in time.

```csharp
public readonly record struct BufferSnapshot(int Width, int Height, Cell[,] Cells)
{
    public Cell GetCell(int x, int y);
}
```

**Creating Snapshots**:

```csharp
public BufferSnapshot CreateSnapshot();
```

Snapshots perform a deep copy of all cells, creating an independent copy safe for concurrent access, serialization, or comparison.

## Quick Start

### Basic Effect Example

```csharp
public class MyWindow : Window
{
    public MyWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
    {
        Title = "Effect Demo";

        // Subscribe to post-paint event
        Renderer.PostBufferPaint += ApplyMyEffect;
    }

    private void ApplyMyEffect(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
    {
        // Manipulate buffer after painting
        for (int y = 0; y < buffer.Height; y++)
        {
            for (int x = 0; x < buffer.Width; x++)
            {
                var cell = buffer.GetCell(x, y);
                // Modify cell colors, characters, etc.
                buffer.SetCell(x, y, cell.Character, modifiedFg, modifiedBg);
            }
        }
    }
}
```

### Screenshot Example

```csharp
private void TakeScreenshot()
{
    var buffer = Renderer?.Buffer;
    if (buffer == null) return;

    var snapshot = buffer.CreateSnapshot();

    // Convert to text
    var lines = new List<string>();
    for (int y = 0; y < snapshot.Height; y++)
    {
        var sb = new StringBuilder();
        for (int x = 0; x < snapshot.Width; x++)
        {
            sb.Append(snapshot.GetCell(x, y).Character);
        }
        lines.Add(sb.ToString());
    }

    File.WriteAllLines("screenshot.txt", lines);
}
```

## Use Cases

### 1. Transition Effects

Apply fade-in, fade-out, slide, or wipe transitions between states.

**Example**: Fade-in effect
```csharp
private float _fadeProgress = 0f;

private void ApplyFadeEffect(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
{
    if (_fadeProgress >= 1.0f) return;

    for (int y = 0; y < buffer.Height; y++)
    {
        for (int x = 0; x < buffer.Width; x++)
        {
            var cell = buffer.GetCell(x, y);

            // Blend from black to target color based on progress
            var newFg = BlendColor(Color.Black, cell.Foreground, _fadeProgress);
            var newBg = BlendColor(Color.Black, cell.Background, _fadeProgress);

            buffer.SetCell(x, y, cell.Character, newFg, newBg);
        }
    }
}

private Color BlendColor(Color from, Color to, float t)
{
    return Color.FromArgb(
        (byte)(from.R + (to.R - from.R) * t),
        (byte)(from.G + (to.G - from.G) * t),
        (byte)(from.B + (to.B - from.B) * t));
}
```

### 2. Blur and Filter Effects

Apply post-processing filters like blur for modal backgrounds or focus effects.

**Example**: Box blur
```csharp
private void ApplyBlur(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
{
    int radius = 2;
    var blurred = new CharacterBuffer(buffer.Width, buffer.Height);

    for (int y = 0; y < buffer.Height; y++)
    {
        for (int x = 0; x < buffer.Width; x++)
        {
            var avgFg = AverageColorInRadius(buffer, x, y, radius, c => c.Foreground);
            var avgBg = AverageColorInRadius(buffer, x, y, radius, c => c.Background);

            blurred.SetCell(x, y, '░', avgFg, avgBg);
        }
    }

    // Copy blurred buffer back
    buffer.CopyFrom(blurred, LayoutRect.FromDimensions(0, 0, buffer.Width, buffer.Height));
}

private Color AverageColorInRadius(CharacterBuffer buffer, int cx, int cy, int radius,
    Func<Cell, Color> selector)
{
    int r = 0, g = 0, b = 0, count = 0;

    for (int dy = -radius; dy <= radius; dy++)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            int x = cx + dx, y = cy + dy;
            if (x >= 0 && x < buffer.Width && y >= 0 && y < buffer.Height)
            {
                var color = selector(buffer.GetCell(x, y));
                r += color.R;
                g += color.G;
                b += color.B;
                count++;
            }
        }
    }

    return Color.FromArgb((byte)(r / count), (byte)(g / count), (byte)(b / count));
}
```

### 3. Custom Overlays

Draw glow effects, highlights, or decorations on top of rendered content.

**Example**: Glow around focused control
```csharp
private void DrawFocusGlow(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
{
    var focusedControl = GetFocusedControl();
    if (focusedControl == null) return;

    var layoutNode = Renderer.GetLayoutNode(focusedControl);
    if (layoutNode == null) return;

    var bounds = layoutNode.AbsoluteBounds;
    Color glowColor = Color.Cyan;

    // Draw glow border
    for (int x = bounds.Left - 1; x <= bounds.Right; x++)
    {
        if (x >= 0 && x < buffer.Width)
        {
            // Top
            if (bounds.Top - 1 >= 0)
            {
                var cell = buffer.GetCell(x, bounds.Top - 1);
                buffer.SetCell(x, bounds.Top - 1, cell.Character, glowColor, cell.Background);
            }

            // Bottom
            if (bounds.Bottom < buffer.Height)
            {
                var cell = buffer.GetCell(x, bounds.Bottom);
                buffer.SetCell(x, bounds.Bottom, cell.Character, glowColor, cell.Background);
            }
        }
    }
}
```

### 4. Screenshots and Recording

Capture buffer state for saving to file or creating recordings.

**Example**: Save screenshot
```csharp
private void TakeScreenshot()
{
    var buffer = Renderer?.Buffer;
    if (buffer == null) return;

    var snapshot = buffer.CreateSnapshot();
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    var filename = $"screenshot_{timestamp}.txt";

    var lines = new List<string>
    {
        $"=== Screenshot captured at {DateTime.Now} ===",
        $"Size: {snapshot.Width} x {snapshot.Height}",
        new string('=', 60),
        ""
    };

    for (int y = 0; y < snapshot.Height; y++)
    {
        var sb = new StringBuilder();
        for (int x = 0; x < snapshot.Width; x++)
        {
            sb.Append(snapshot.GetCell(x, y).Character);
        }
        lines.Add(sb.ToString());
    }

    File.WriteAllLines(filename, lines);
}
```

**Example**: Record frames
```csharp
private List<BufferSnapshot> _frames = new();

public void StartRecording()
{
    _frames.Clear();

    Renderer.PostBufferPaint += (buffer, dirtyRegion, clipRect) =>
    {
        _frames.Add(buffer.CreateSnapshot());
    };
}

public void StopRecording()
{
    Renderer.PostBufferPaint -= RecordFrame;
    // Process _frames (save as animated GIF, video, etc.)
}
```

### 5. Buffer Compositing

Manually composite multiple buffer snapshots for advanced effects.

```csharp
public class CustomCompositor
{
    private List<(BufferSnapshot snapshot, int z)> _layers = new();

    public void AddLayer(BufferSnapshot snapshot, int z)
    {
        _layers.Add((snapshot, z));
        _layers = _layers.OrderBy(l => l.z).ToList();
    }

    public CharacterBuffer Render()
    {
        if (_layers.Count == 0) return null;

        var first = _layers[0].snapshot;
        var result = new CharacterBuffer(first.Width, first.Height);

        foreach (var (snapshot, _) in _layers)
        {
            for (int y = 0; y < snapshot.Height; y++)
            {
                for (int x = 0; x < snapshot.Width; x++)
                {
                    var cell = snapshot.GetCell(x, y);
                    if (cell.Character != ' ') // Simple alpha test
                    {
                        result.SetCell(x, y, cell.Character, cell.Foreground, cell.Background);
                    }
                }
            }
        }

        return result;
    }
}
```

## Examples

The `Examples/CompositorEffectsExample` project demonstrates all major use cases:

### FadeInWindow.cs

Demonstrates a smooth fade-in transition effect that gradually blends from black to the window's rendered content over 60 frames.

**Key Features**:
- Animated fade using System.Threading.Timer
- Color blending algorithm
- Progress tracking (0.0 to 1.0)
- Automatic cleanup on completion

**Run**: Launch example, press `1` or click "Fade-In Transition"

### ModalBlurWindow.cs

Demonstrates a box blur effect that can be toggled on/off, with adjustable blur radius.

**Key Features**:
- Interactive blur toggle (B key)
- Adjustable blur radius (+/- keys)
- Radius display in title
- Buffer copy to avoid feedback loop

**Run**: Launch example, press `2` or click "Modal Blur Effect"

### ScreenshotWindow.cs

Demonstrates capturing buffer snapshots and saving to file with metadata.

**Key Features**:
- BufferSnapshot usage
- File I/O with timestamps
- Screenshot counter
- Success/error notifications

**Run**: Launch example, press `3` or click "Screenshot Capture", then press F12 or S

### Running Examples

```bash
cd Examples/CompositorEffectsExample
dotnet run
```

## Best Practices

### 1. Use Dirty Regions for Optimization

Only process the area that was actually repainted:

```csharp
private void ApplyEffect(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
{
    // Only process dirty region, not entire buffer
    for (int y = dirtyRegion.Top; y < dirtyRegion.Bottom; y++)
    {
        for (int x = dirtyRegion.Left; x < dirtyRegion.Right; x++)
        {
            // Apply effect
        }
    }
}
```

### 2. Cache Expensive Operations

Don't recalculate the same values every frame:

```csharp
private float[]? _blurWeights;

private void ApplyBlur(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
{
    // Calculate weights once
    if (_blurWeights == null)
    {
        _blurWeights = CalculateGaussianWeights(_blurRadius);
    }

    // Use cached weights
    ApplyBlurWithWeights(buffer, _blurWeights);
}
```

### 3. Unsubscribe Events Properly

Always unsubscribe from events when no longer needed:

```csharp
public class MyWindow : Window
{
    public MyWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
    {
        Renderer.PostBufferPaint += ApplyEffect;
        OnClosing += (s, e) => Cleanup();
    }

    private void Cleanup()
    {
        if (Renderer != null)
        {
            Renderer.PostBufferPaint -= ApplyEffect;
        }
    }
}
```

### 4. Avoid Feedback Loops in Blur/Copy Operations

When copying entire buffer back, use a temporary buffer:

```csharp
// GOOD: Use temporary buffer
var temp = new CharacterBuffer(buffer.Width, buffer.Height);
ApplyEffectTo(buffer, temp);
buffer.CopyFrom(temp, ...);

// BAD: Modifying buffer while reading causes feedback
for (int y = 0; y < buffer.Height; y++)
{
    for (int x = 0; x < buffer.Width; x++)
    {
        var avg = AverageNeighbors(buffer, x, y); // Reading modified cells!
        buffer.SetCell(x, y, ...);
    }
}
```

### 5. Conditional Effect Application

Only apply effects when necessary:

```csharp
private bool _effectEnabled = true;
private float _effectIntensity = 1.0f;

private void ApplyEffect(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
{
    if (!_effectEnabled || _effectIntensity <= 0f)
        return; // Skip processing

    // Apply effect with intensity
}
```

### 6. Use StringBuilder for Text Construction

When converting snapshots to text, use StringBuilder:

```csharp
// GOOD
var sb = new StringBuilder();
for (int x = 0; x < width; x++)
    sb.Append(snapshot.GetCell(x, y).Character);
string line = sb.ToString();

// BAD
string line = "";
for (int x = 0; x < width; x++)
    line += snapshot.GetCell(x, y).Character; // Creates many temporary strings
```

## Performance Considerations

### Complexity

- **Full buffer iteration**: O(width × height) - Use sparingly
- **Dirty region iteration**: O(dirty_width × dirty_height) - Preferred
- **Blur effects**: O(width × height × radius²) - Expensive, cache when possible

### Optimization Strategies

1. **Process Only Dirty Regions**: Use the `dirtyRegion` parameter
2. **Early Exit**: Skip processing when effect is disabled/completed
3. **Incremental Updates**: For animations, only update changed values
4. **Use Lookup Tables**: Pre-calculate color blends, blur weights, etc.
5. **Limit Effect Area**: Apply effects only to specific rectangles

### Performance Example

```csharp
// Optimized blur effect
private void ApplyBlur(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
{
    if (!_blurEnabled) return; // Early exit

    // Only blur the dirty region + blur radius margin
    var effectRegion = new LayoutRect(
        Math.Max(0, dirtyRegion.Left - _blurRadius),
        Math.Max(0, dirtyRegion.Top - _blurRadius),
        Math.Min(buffer.Width, dirtyRegion.Right + _blurRadius),
        Math.Min(buffer.Height, dirtyRegion.Bottom + _blurRadius)
    );

    // Process only effectRegion, not entire buffer
    for (int y = effectRegion.Top; y < effectRegion.Bottom; y++)
    {
        for (int x = effectRegion.Left; x < effectRegion.Right; x++)
        {
            // Apply blur
        }
    }
}
```

## Thread Safety

The `PostBufferPaint` event fires **within the existing render lock**, ensuring thread safety:

```csharp
// WindowRenderer.RebuildContentCacheDOM()
lock (_renderLock)
{
    PaintDOM(clipRect, backgroundColor);

    // Event fires within lock - thread safe
    PostBufferPaint?.Invoke(_buffer, dirtyRegion, clipRect);

    return BufferToLines(foregroundColor, backgroundColor);
}
```

### Safe Operations

All `CharacterBuffer` operations are safe within the event handler:
- `GetCell(x, y)` - Thread safe (within lock)
- `SetCell(x, y, ...)` - Thread safe (within lock)
- `CreateSnapshot()` - Thread safe (creates independent copy)

### Unsafe Operations

Do NOT perform long-running operations in the event handler:

```csharp
// BAD: Blocks rendering
private void ApplyEffect(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
{
    Thread.Sleep(1000); // BLOCKS RENDERING!
    File.WriteAllText(...); // BLOCKS RENDERING!
}

// GOOD: Defer expensive work
private void ApplyEffect(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
{
    var snapshot = buffer.CreateSnapshot(); // Fast

    // Defer expensive work
    Task.Run(() =>
    {
        ProcessSnapshot(snapshot);
        File.WriteAllText(...);
    });
}
```

## API Reference

### Window Class

```csharp
namespace SharpConsoleUI
{
    public class Window
    {
        /// <summary>
        /// Gets the window's renderer, providing access to rendering internals.
        /// </summary>
        /// <remarks>
        /// Exposes the renderer for advanced scenarios like custom buffer effects,
        /// transitions, and compositor-style manipulations.
        /// </remarks>
        public Windows.WindowRenderer? Renderer { get; }
    }
}
```

### WindowRenderer Class

```csharp
namespace SharpConsoleUI.Windows
{
    public class WindowRenderer
    {
        /// <summary>
        /// Delegate for buffer post-processing after painting but before ANSI conversion.
        /// </summary>
        /// <param name="buffer">The character buffer that was just painted.</param>
        /// <param name="dirtyRegion">The region that was painted (or full bounds if entire buffer).</param>
        /// <param name="clipRect">The clipping rectangle used during paint.</param>
        public delegate void PostBufferPaintDelegate(
            CharacterBuffer buffer,
            LayoutRect dirtyRegion,
            LayoutRect clipRect);

        /// <summary>
        /// Raised after painting controls to the buffer but before converting to ANSI strings.
        /// </summary>
        /// <remarks>
        /// This event allows custom effects, transitions, filters, or compositor-style
        /// manipulations on the rendered buffer. The buffer can be safely modified here.
        ///
        /// Example use cases:
        /// - Fade in/out transitions
        /// - Blur effects for modal backgrounds
        /// - Glow effects around focused controls
        /// - Custom overlays and effects
        /// </remarks>
        public event PostBufferPaintDelegate? PostBufferPaint;

        /// <summary>
        /// Gets the current character buffer for this window.
        /// </summary>
        /// <remarks>
        /// CAUTION: Direct buffer manipulation should only be done via PostBufferPaint event
        /// to avoid race conditions. Reading is safe at any time.
        /// </remarks>
        public CharacterBuffer? Buffer { get; }
    }
}
```

### CharacterBuffer Class

```csharp
namespace SharpConsoleUI.Layout
{
    public class CharacterBuffer
    {
        /// <summary>
        /// Immutable snapshot of a CharacterBuffer at a point in time.
        /// </summary>
        public readonly record struct BufferSnapshot(int Width, int Height, Cell[,] Cells)
        {
            /// <summary>
            /// Gets the cell at the specified position.
            /// </summary>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown when x or y is outside the buffer bounds.
            /// </exception>
            public Cell GetCell(int x, int y);
        }

        /// <summary>
        /// Creates an immutable snapshot of the current buffer state.
        /// </summary>
        /// <returns>A deep copy of the buffer as a snapshot.</returns>
        /// <remarks>
        /// The snapshot is completely independent of the source buffer and safe for
        /// concurrent access, serialization, or comparison. Changes to the source
        /// buffer do not affect the snapshot.
        /// </remarks>
        public BufferSnapshot CreateSnapshot();
    }
}
```

## See Also

- [Rendering Pipeline](RENDERING_PIPELINE.md) - Understanding the rendering flow
- [Controls Documentation](CONTROLS.md) - Building UI with controls
- [Themes Guide](THEMES.md) - Customizing visual appearance
- [API Reference](docfx/_site/api/SharpConsoleUI.html) - Complete API documentation

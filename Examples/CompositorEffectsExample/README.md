# Compositor Effects Example

This example demonstrates the new compositor-style buffer manipulation capabilities in SharpConsoleUI using the `PostBufferPaint` event and `BufferSnapshot` API.

## Features Demonstrated

### 1. Fade-In Effect (`FadeInWindow.cs`)
- Demonstrates smooth fade-in transition from black to full color
- Uses `PostBufferPaint` event to manipulate buffer after painting
- Color interpolation (lerping) for smooth transitions
- Timer-based animation at ~60 FPS

### 2. Blur Effect (`ModalBlurWindow.cs`)
- Demonstrates box blur post-processing effect
- Shows how to create a copy of the buffer to avoid feedback loops
- Interactive controls to toggle blur and adjust radius
- Demonstrates averaging pixel colors in a radius

### 3. Screenshot Capture (`ScreenshotWindow.cs`)
- Demonstrates `BufferSnapshot` API for capturing window state
- Saves screenshots to text files
- Shows immutable snapshot creation independent of original buffer
- Integrates with notification system

## How to Run

```bash
dotnet run --project Examples/CompositorEffectsExample
```

## Key APIs Used

### PostBufferPaint Event

The `PostBufferPaint` event fires after controls are painted but before ANSI conversion:

```csharp
if (Renderer != null)
{
    Renderer.PostBufferPaint += (buffer, dirtyRegion, clipRect) =>
    {
        // Manipulate buffer here
        for (int y = 0; y < buffer.Height; y++)
        {
            for (int x = 0; x < buffer.Width; x++)
            {
                var cell = buffer.GetCell(x, y);
                // Modify cell colors, characters, etc.
                buffer.SetCell(x, y, newChar, newFg, newBg);
            }
        }
    };
}
```

### BufferSnapshot API

Create immutable snapshots of the buffer:

```csharp
var buffer = window.Renderer?.Buffer;
if (buffer != null)
{
    var snapshot = buffer.CreateSnapshot();

    // Access cells from snapshot
    for (int y = 0; y < snapshot.Height; y++)
    {
        for (int x = 0; x < snapshot.Width; x++)
        {
            var cell = snapshot.GetCell(x, y);
            // Process cell...
        }
    }
}
```

## Use Cases

These APIs enable:
- **Transitions**: Fade in/out, slide, dissolve effects
- **Filters**: Blur, sharpen, color grading
- **Overlays**: Glow effects, borders, highlights
- **Screenshots**: Capture buffer state for logging/debugging
- **Recording**: Frame-by-frame capture for replay
- **Compositing**: Layer multiple buffers with alpha blending

## Architecture Notes

- The `PostBufferPaint` event runs **after** control painting but **before** ANSI string conversion
- This is the perfect hook point for post-processing effects
- Event invocation happens within the existing render lock (thread-safe)
- Zero overhead when event not subscribed
- `BufferSnapshot` creates independent deep copies (safe for async operations)

## Credits

Part of SharpConsoleUI v2.0 - Modern .NET console windowing system

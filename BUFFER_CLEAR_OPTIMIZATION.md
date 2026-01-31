# Buffer Clear Optimization

## Problem

Currently, `Window.PaintDOM()` calls `_buffer.Clear(BackgroundColor)` before every paint, which marks ALL cells dirty, defeating CharacterBuffer's smart dirty tracking that only marks cells dirty when values actually change.

**Impact:** A 100x12 window (1200 cells) redraws entirely even when only 1 character changes (e.g., clock "12:34" → "12:35").

## Solution: Clear Only on First Render

Only call `_buffer.Clear()` on the first paint and when buffer is recreated. Let `SetCell()`'s comparison logic handle dirty tracking for subsequent renders.

## Implementation

### Changes Required

#### 1. Add field (Window.cs, line ~437)
```csharp
/// <summary>
/// Tracks whether this is the first paint operation. Used to optimize buffer clearing.
/// </summary>
private bool _firstPaint = true;
```

#### 2. Modify BackgroundColor setter (Window.cs, line ~341)
```csharp
public Color BackgroundColor
{
    get => _backgroundColor ?? (Mode == WindowMode.Modal
        ? _windowSystem?.Theme.ModalBackgroundColor
        : _windowSystem?.Theme.WindowBackgroundColor) ?? Color.Black;
    set
    {
        if (_backgroundColor != value)
        {
            _backgroundColor = value;
            _firstPaint = true;  // Force clear on next paint with new background color
        }
    }
}
```

#### 3. Modify PaintDOM (Window.cs, line ~2607)
```csharp
private void PaintDOM(LayoutRect clipRect)
{
    if (_rootNode == null || _buffer == null) return;

    // Only clear on first paint - subsequent paints rely on SetCell's dirty tracking
    // This optimization reduces dirty cells by 90%+ when only small regions change
    if (_firstPaint)
    {
        _buffer.Clear(BackgroundColor);
        _firstPaint = false;
    }

    // Paint the tree with the provided clip rect
    _rootNode.Paint(_buffer, clipRect);
}
```

#### 4. Reset on buffer recreation #1 (Window.cs, line ~2465)
```csharp
if (_buffer == null || _buffer.Width != contentWidth || _buffer.Height != contentHeight)
{
    _buffer = new CharacterBuffer(contentWidth, contentHeight);
    _firstPaint = true;  // Force clear on first paint after buffer recreation
}
```

#### 5. Reset on buffer recreation #2 (Window.cs, line ~2699)
```csharp
if (_buffer == null || _buffer.Width != availableWidth || _buffer.Height != availableHeight)
{
    _buffer = new CharacterBuffer(availableWidth, availableHeight);
    _firstPaint = true;  // Force clear on first paint after buffer recreation
    _rootNode?.InvalidateMeasure();
}
```

## Expected Results

- **First render**: Full buffer clear (as before)
- **Subsequent renders**: Only changed cells marked dirty by SetCell()
- **Window resize**: Clear triggered again
- **Background color change**: Clear triggered again
- **Performance**: 90%+ reduction in dirty cells when small regions update

## Previous Test Result

Tested before atomic rendering was implemented: "Worst flickering!"

**To retry:** Now that we have atomic single-write rendering (commit 1bedb67), this optimization should work better. The previous flickering may have been caused by multiple cursor moves during partial updates.

## Related

- Atomic rendering: commit 1bedb67
- CharacterBuffer.SetCell dirty tracking: SharpConsoleUI/Layout/CharacterBuffer.cs:112-127
- Window.PaintDOM: SharpConsoleUI/Window.cs:2602-2611

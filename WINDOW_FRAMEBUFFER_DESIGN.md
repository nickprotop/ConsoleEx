# Window Frame Buffer Architecture

## Overview

Each `Window` maintains its own `CharacterBuffer` instance (`_buffer`) as a frame buffer for rendering. This architectural decision enables advanced compositing features similar to modern GUI frameworks like WinUI and Win2D.

## Current Implementation (Intentionally Simple)

### Rendering Flow

```csharp
// In Window.PaintDOM()
_buffer.Clear(BackgroundColor);  // Clear entire buffer every frame
_rootNode.Paint(_buffer, clipRect);  // Paint DOM tree to buffer
// No Commit() call - dirty tracking intentionally unused
```

**Key characteristics:**
- ✅ **Full clear every frame** - Simple and predictable
- ✅ **No dirty tracking optimization** - All cells marked dirty
- ✅ **Stable and correct** - No subtle rendering bugs
- ⚠️ **Performance overhead** - Marks ~14k cells dirty even for small changes

### Why This Design?

**Correctness over performance:**
- Complex optimizations introduce subtle bugs (color state issues, sync problems)
- Rendering correctness is critical for a UI library
- "Make it work, make it right, make it fast" - we're at step 2

**Deferred optimization:**
- Premature optimization caused rendering glitches
- Need comprehensive test coverage before optimizing
- Current performance is acceptable for most use cases

## CharacterBuffer Infrastructure (Available but Unused)

The `CharacterBuffer` class has full dirty tracking infrastructure:

```csharp
// Infrastructure exists
private LayoutRect _dirtyRegion;  // Bounding box of dirty cells
public void Commit();             // Clears dirty flags
public IEnumerable<CellChange> GetChanges();  // Gets only dirty cells

// But NOT utilized by Window rendering
// - Clear() marks everything dirty
// - Commit() never called
// - GetChanges() not used
```

**This is intentional:**
- Infrastructure kept for future optimization
- Can be enabled when proper testing is in place
- No technical debt - just unused capability

## Architectural Vision

The frame buffer architecture enables future advanced features:

### 1. Public Buffer Exposure (Like WinUI Composition)
```csharp
// Future API
public CharacterBuffer GetFrameBuffer() => _buffer;

// Allows direct manipulation
var buffer = window.GetFrameBuffer();
buffer.SetCell(x, y, '█', Color.Red, Color.Black);
```

### 2. Direct Painting API (Like Win2D)
```csharp
// Future API
window.DirectPaint((buffer, clipRect) => {
    // Custom drawing code
    for (int y = 0; y < clipRect.Height; y++)
        buffer.SetCell(x, y, GetPixel(x, y), fg, bg);
});
```

### 3. Overlay and Composition Effects
```csharp
// Future possibilities
- Alpha blending between window layers
- Post-processing effects (blur, glow, shadow)
- Texture-based backgrounds
- Animated transitions
```

### 4. Performance Features
```csharp
// When properly implemented
- Dirty region rendering (only update changed areas)
- Partial invalidation (redraw only specific controls)
- Double buffering (smooth animations)
- Off-screen rendering (prepare frames in background)
```

## Why Keep Frame Buffer Despite No Current Optimization?

**1. Separation of Concerns**
- Painting logic doesn't deal with ANSI escape codes
- Buffer abstraction enables different rendering backends
- Clean API for controls (paint to buffer, not to terminal)

**2. Foundation for Advanced Features**
- Direct pixel manipulation
- Composition and layering
- Custom rendering effects
- Professional-grade UI capabilities

**3. Future Optimization Path**
- Infrastructure already in place
- Can enable dirty tracking when ready
- No architectural rewrite needed

**4. Industry Standard Pattern**
- All modern GUI frameworks use frame buffers
- Proven architecture for complex UIs
- Familiar to developers from other platforms

## Future Optimization Plan (When Ready)

When performance becomes critical AND we have proper test coverage:

### Phase 1: Fix Dirty Tracking
```csharp
private bool _firstPaint = true;

void PaintDOM(LayoutRect clipRect)
{
    if (_firstPaint || _backgroundChanged || _resized)
    {
        _buffer.Clear(BackgroundColor);
        _firstPaint = false;
    }

    _rootNode.Paint(_buffer, clipRect);
    _buffer.Commit();  // Clear dirty flags
}
```

### Phase 2: Partial Invalidation
```csharp
// Only repaint controls that changed
control.Invalidate();  // Marks control's region dirty
// Next paint: only regenerate dirty regions
```

### Phase 3: ToLines() Optimization
```csharp
// Cache ANSI strings per line
// Only regenerate lines in dirty region
// Track color state across line boundaries
```

**Prerequisites for optimization:**
- ✅ Comprehensive test suite (visual regression tests)
- ✅ Profiling data showing actual bottleneck
- ✅ Edge case documentation (overlapping windows, z-order, etc.)
- ✅ Rendering correctness verification tools

## Performance Characteristics

### Current (Unoptimized) Performance

**Per Frame (80x25 window):**
- Clear: ~2,000 cells marked dirty
- Paint: ~2,000 SetCell() calls
- ToLines(): Generates all ~25 ANSI strings
- Total dirty cells: 100% of buffer

**Acceptable for:**
- ✅ Most interactive applications
- ✅ 60fps updates on modern hardware
- ✅ Local terminal sessions
- ⚠️ Remote/SSH sessions (higher latency)

### Optimized Performance (Future)

**Per Frame (with dirty tracking):**
- Clear: Only on first paint/resize
- Paint: Only changed controls (5-20% of buffer)
- ToLines(): Only regenerated dirty lines
- Total dirty cells: 5-20% of buffer

**Benefits:**
- 🚀 5-20x reduction in dirty cells
- 🚀 Faster over SSH/remote connections
- 🚀 Smoother animations
- 🚀 Higher frame rates possible

## Decision Log

**2025-02-01: Reverted dirty tracking optimization**
- **Reason**: Introduced subtle rendering bugs
- **Decision**: Keep simple "clear every frame" approach
- **Rationale**: Correctness > performance, insufficient test coverage
- **Future**: Re-enable when proper testing in place

**2025-02-01: Keep frame buffer architecture**
- **Reason**: Enables WinUI-like compositing features
- **Decision**: Maintain CharacterBuffer infrastructure
- **Rationale**: Foundation for advanced features, industry standard pattern
- **Future**: Expose publicly for direct painting, overlays, effects

## Summary

The window frame buffer is a **strategic architectural choice**, not just an optimization. We intentionally keep the implementation simple (clear every frame) to ensure correctness while maintaining the infrastructure for future advanced features like direct painting, composition, and effects.

**Trade-offs:**
- ✅ **Simplicity**: Easy to understand and debug
- ✅ **Correctness**: No subtle rendering bugs
- ✅ **Flexibility**: Foundation for advanced features
- ⚠️ **Performance**: Sub-optimal but acceptable

When performance becomes critical, the optimization path is clear and the infrastructure is ready.

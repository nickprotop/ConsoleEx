# Render Pipeline Optimization Opportunities

**Date**: 2026-01-30
**Type**: Business-Standard Performance Improvements
**Focus**: Practical wins without over-engineering

---

## Priority 1: Hot Path Allocations (High Impact, Low Risk)

### 1.1 Remove LINQ from Render Loop

**Location**: `ConsoleWindowSystem.cs:2543-2545, 2587-2589`

**Current Code** (Allocates enumerators every frame):
```csharp
// PASS 1: Render normal windows
foreach (var window in Windows.Values
    .Where(w => !w.AlwaysOnTop)
    .OrderBy(w => w.ZIndex))
{
    if (window != ActiveWindow && windowsToRender.Contains(window))
    {
        _renderer.RenderWindow(window);
    }
}

// PASS 2: Render AlwaysOnTop windows
foreach (var window in Windows.Values
    .Where(w => w.AlwaysOnTop && w.State != WindowState.Minimized)
    .OrderBy(w => w.ZIndex))
{
    if (window.IsDirty || windowsToRender.Contains(window))
    {
        _renderer.RenderWindow(window);
    }
}
```

**Optimized** (Zero allocations):
```csharp
// Pre-sort windows once per frame (amortize cost)
var sortedWindows = Windows.Values.OrderBy(w => w.ZIndex).ToList(); // Once at start of UpdateDisplay()

// PASS 1: Render normal windows (no LINQ)
for (int i = 0; i < sortedWindows.Count; i++)
{
    var window = sortedWindows[i];
    if (window.AlwaysOnTop) continue;  // Skip AlwaysOnTop for this pass

    if (window != ActiveWindow && windowsToRender.Contains(window))
    {
        if (window.Width > 0 && window.Height > 0)
        {
            _renderer.RenderWindow(window);
        }
    }
}

// PASS 2: Render AlwaysOnTop windows (no LINQ)
for (int i = 0; i < sortedWindows.Count; i++)
{
    var window = sortedWindows[i];
    if (!window.AlwaysOnTop) continue;
    if (window.State == WindowState.Minimized) continue;

    if (window.IsDirty || windowsToRender.Contains(window))
    {
        if (window.Width > 0 && window.Height > 0)
        {
            _renderer.RenderWindow(window);
        }
    }
}
```

**Impact**: Eliminates 2-4 enumerator allocations per frame (16-32 bytes each + overhead)

---

### 1.2 Cache windowsToRender HashSet

**Location**: `ConsoleWindowSystem.cs:2491`

**Current Code**:
```csharp
private void UpdateDisplay()
{
    var windowsToRender = new HashSet<Window>();  // Allocated EVERY frame

    foreach (var window in Windows.Values)
    {
        if (!window.IsDirty) continue;
        windowsToRender.Add(window);
    }
}
```

**Optimized**:
```csharp
// Add field to ConsoleWindowSystem
private readonly HashSet<Window> _windowsToRender = new HashSet<Window>();

private void UpdateDisplay()
{
    _windowsToRender.Clear();  // Reuse, don't allocate

    foreach (var window in Windows.Values)
    {
        if (!window.IsDirty) continue;
        _windowsToRender.Add(window);
    }
}
```

**Impact**: Eliminates 1 HashSet allocation per frame (~40 bytes + buckets array ~64 bytes)

---

### 1.3 Optimize AnyWindowDirty()

**Location**: `ConsoleWindowSystem.cs:1568`

**Current Code**:
```csharp
private bool AnyWindowDirty()
{
    return Windows.Values.Any(window => window.IsDirty);  // Creates enumerator
}
```

**Optimized**:
```csharp
private bool AnyWindowDirty()
{
    foreach (var window in Windows.Values)
    {
        if (window.IsDirty) return true;
    }
    return false;
}
```

**Impact**: Called EVERY iteration of main loop (line 901). Eliminates enumerator allocation on hot path.

---

## Priority 2: String Allocations (Medium Impact, Low Risk)

### 2.1 Cache Window Border Strings

**Location**: `Renderer.cs:595-603`

**Problem**: Border strings are converted from Spectre markup to ANSI **every frame**, even when window hasn't changed size or activation state.

**Current Code**:
```csharp
private void DrawWindowBorders(Window window, List<Rectangle> visibleRegions)
{
    // These are computed EVERY FRAME for EVERY window:
    var topBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
        $"{borderColor}{topLeftCorner}...{topRightCorner}{resetColor}", ...)[0];

    var bottomBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
        $"{borderColor}{bottomLeftCorner}...{bottomRightChar}{resetColor}", ...)[0];

    var verticalBorderAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
        $"{borderColor}{verticalBorder}{resetColor}", ...)[0];
}
```

**Optimized**: Add border cache to Window class

```csharp
// In Window.cs - add fields:
private string? _cachedTopBorder;
private string? _cachedBottomBorder;
private string? _cachedVerticalBorder;
private int _cachedBorderWidth = -1;
private bool _cachedBorderIsActive;

public void InvalidateBorderCache()
{
    _cachedTopBorder = null;
    _cachedBottomBorder = null;
    _cachedVerticalBorder = null;
}

// Call InvalidateBorderCache() when:
// - Width/Height changes
// - Title changes
// - Active state changes
// - BorderStyle changes

// In Renderer.DrawWindowBorders():
private void DrawWindowBorders(Window window, List<Rectangle> visibleRegions)
{
    bool isActive = window.GetIsActive();

    // Check if cache is valid
    if (window._cachedTopBorder == null ||
        window._cachedBorderWidth != window.Width ||
        window._cachedBorderIsActive != isActive)
    {
        // Rebuild cache
        window._cachedTopBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(...)[0];
        window._cachedBottomBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(...)[0];
        window._cachedVerticalBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(...)[0];
        window._cachedBorderWidth = window.Width;
        window._cachedBorderIsActive = isActive;
    }

    // Use cached borders
    // ... rest of method ...
}
```

**Impact**:
- Avoids 3 AnsiConsoleHelper calls per window per frame
- Typical window renders borders 30-60 times/second
- Each conversion allocates ~200-500 bytes (markup parsing + string building)
- **Savings**: ~1.5KB per window per frame (for 3 windows = 4.5KB/frame)

---

### 2.2 Pool Rectangle Lists in VisibleRegions

**Location**: `VisibleRegions.cs:39, 83`

**Current Code**:
```csharp
public List<Rectangle> CalculateVisibleRegions(...)
{
    var regions = new List<Rectangle> { ... };  // Allocation

    foreach (var other in overlappingWindows)
    {
        regions = SubtractRectangle(regions, ...);  // More allocations in loop
    }
    return regions;
}

private List<Rectangle> SubtractRectangle(...)
{
    var result = new List<Rectangle>();  // Allocation
    // ...
    return result;
}
```

**Optimized**: Use object pooling or array-based lists

```csharp
// Simple version: reuse lists (not pool, just clear)
private readonly List<Rectangle> _regionsBuffer1 = new List<Rectangle>(8);
private readonly List<Rectangle> _regionsBuffer2 = new List<Rectangle>(8);

public List<Rectangle> CalculateVisibleRegions(...)
{
    _regionsBuffer1.Clear();
    _regionsBuffer1.Add(new Rectangle(...));

    var current = _regionsBuffer1;
    var next = _regionsBuffer2;

    foreach (var other in overlappingWindows)
    {
        next.Clear();
        SubtractRectangle(current, overlappingRect, next);

        // Swap buffers
        var temp = current;
        current = next;
        next = temp;
    }

    // Return as new list (caller expects ownership)
    return new List<Rectangle>(current);
}
```

**Alternative**: Use `ArrayPool<Rectangle>` for zero-allocation path (more complex)

**Impact**: Eliminates 2-6 List allocations per dirty window per frame

---

## Priority 3: Redundant Work (Medium Impact, Medium Risk)

### 3.1 Merge Dirty Detection and Render List Building

**Location**: `ConsoleWindowSystem.cs:2495-2540`

**Problem**: Currently iterates Windows.Values multiple times:
1. Line 2495: foreach to find dirty windows
2. Line 2543: `.Where().OrderBy()` to render normal windows
3. Line 2587: `.Where().OrderBy()` to render AlwaysOnTop windows

**Optimized**: Single pass with early filtering

```csharp
private void UpdateDisplay()
{
    _windowsToRender.Clear();

    // Single pass: identify dirty windows AND build sorted lists
    var normalWindows = new List<Window>(Windows.Count);
    var alwaysOnTopWindows = new List<Window>(Windows.Count);

    foreach (var window in Windows.Values)
    {
        // Skip invalid windows
        if (window.State == WindowState.Minimized) continue;
        if (window.Width <= 0 || window.Height <= 0) continue;

        // Track dirty windows
        if (window.IsDirty && !IsCompletelyCovered(window))
        {
            _windowsToRender.Add(window);
        }

        // Build render lists for both passes
        if (window.AlwaysOnTop)
            alwaysOnTopWindows.Add(window);
        else
            normalWindows.Add(window);
    }

    // Sort once per list
    normalWindows.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
    alwaysOnTopWindows.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

    // PASS 1: Render normal windows
    for (int i = 0; i < normalWindows.Count; i++)
    {
        var window = normalWindows[i];
        if (window != ActiveWindow && _windowsToRender.Contains(window))
        {
            _renderer.RenderWindow(window);
        }
    }

    // ... active window logic ...

    // PASS 2: Render AlwaysOnTop windows
    for (int i = 0; i < alwaysOnTopWindows.Count; i++)
    {
        var window = alwaysOnTopWindows[i];
        if (window.IsDirty || _windowsToRender.Contains(window))
        {
            _renderer.RenderWindow(window);
        }
    }
}
```

**Impact**: Reduces window iteration from 3-4 passes to 1 pass + 2 sorts

---

### 3.2 Cache IsCompletelyCovered() Results

**Location**: `ConsoleWindowSystem.cs:2508`

**Problem**: `IsCompletelyCovered()` checks all overlapping windows. For static layouts, this doesn't change between frames.

**Optimized**:
```csharp
// Add to ConsoleWindowSystem
private readonly Dictionary<Guid, bool> _coverageCache = new Dictionary<Guid, bool>();

public void InvalidateCoverageCache(Window? window = null)
{
    if (window == null)
        _coverageCache.Clear();  // Invalidate all
    else
        _coverageCache.Remove(window.Guid);
}

private bool IsCompletelyCovered(Window window)
{
    if (_coverageCache.TryGetValue(window.Guid, out bool cached))
        return cached;

    bool result = CalculateIsCompletelyCovered(window);
    _coverageCache[window.Guid] = result;
    return result;
}

// Call InvalidateCoverageCache() when:
// - Window moves/resizes
// - Window Z-index changes
// - Window added/removed
```

**Impact**: Avoids repeated overlap checks for static window layouts

---

## Priority 4: Data Structure Improvements (Low Impact, Low Risk)

### 4.1 Use Sorted Window List Instead of Dictionary

**Location**: `ConsoleWindowSystem.cs` - `Windows` property

**Current**: `Dictionary<Guid, Window>` requires OrderBy() for Z-order rendering

**Optimized**: Use `SortedSet<Window>` or maintain separate `List<Window>` sorted by Z-index

**Trade-offs**:
- SortedSet: Auto-sorted, O(log n) insert, no random access
- List: Manual sort, O(n log n) sort, O(1) access by index
- Dictionary: O(1) lookup by GUID, O(n) iteration

**Recommendation**: Keep Dictionary for GUID lookups, add:
```csharp
private readonly List<Window> _windowsByZOrder = new List<Window>();

public void AddWindow(Window window)
{
    Windows[window.Guid] = window;

    // Insert sorted by Z-index
    int index = _windowsByZOrder.BinarySearch(window, ZIndexComparer.Instance);
    if (index < 0) index = ~index;
    _windowsByZOrder.Insert(index, window);
}

public void UpdateWindowZIndex(Window window, int newZIndex)
{
    window.ZIndex = newZIndex;

    // Remove and re-insert to maintain sort order
    _windowsByZOrder.Remove(window);
    int index = _windowsByZOrder.BinarySearch(window, ZIndexComparer.Instance);
    if (index < 0) index = ~index;
    _windowsByZOrder.Insert(index, window);

    InvalidateCoverageCache();  // Z-order changed
}
```

**Impact**: Eliminates OrderBy() calls in render loop (saves O(n log n) sorts per frame)

---

## Priority 5: String Operations (Low-Medium Impact)

### 5.1 Avoid `new string(char, count)` in FillRect

**Location**: `Renderer.cs:68`

**Current Code**:
```csharp
_consoleWindowSystem.ConsoleDriver.WriteToConsole(
    left, top + _consoleWindowSystem.DesktopUpperLeft.Y + y,
    AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
        $"{new string(character, effectiveWidth)}",  // Allocates every call
        effectiveWidth, 1, false, backgroundColor, foregroundColor)[0]);
```

**Optimized**: Cache common fill strings

```csharp
// In Renderer class:
private readonly Dictionary<(char, int), string> _fillStringCache = new Dictionary<(char, int), string>();

private string GetFillString(char character, int width)
{
    var key = (character, width);
    if (_fillStringCache.TryGetValue(key, out string cached))
        return cached;

    // Limit cache size to prevent memory leak
    if (_fillStringCache.Count > 100)
        _fillStringCache.Clear();

    string result = new string(character, width);
    _fillStringCache[key] = result;
    return result;
}
```

**Impact**: Reduces string allocations for common fill operations (desktop background, window backgrounds)

---

### 5.2 Pre-compute Status Bar Components

**Location**: `ConsoleWindowSystem.cs:2668-2670`

**Current Code**:
```csharp
var taskBar = _options.StatusBar.ShowTaskBar ?
    $"{string.Join(" | ", topLevelWindows.Select((w, i) => {
        var minIndicator = w.State == WindowState.Minimized ? "[dim]" : "";
        var minEnd = w.State == WindowState.Minimized ? "[/]" : "";
        // ... more string building ...
    }))}" : "";
```

**Problem**: This runs EVERY frame even when windows haven't changed

**Optimized**: Cache taskbar string, invalidate on window state change

```csharp
private string? _cachedTaskBar;
private int _taskBarWindowCount;
private int _taskBarStateHash;

private string BuildTaskBar()
{
    var topLevelWindows = Windows.Values
        .Where(w => w.ParentWindow == null && !(w is OverlayWindow))
        .ToList();

    // Check if cache is valid
    int stateHash = ComputeTaskBarStateHash(topLevelWindows);
    if (_cachedTaskBar != null &&
        _taskBarWindowCount == topLevelWindows.Count &&
        _taskBarStateHash == stateHash)
    {
        return _cachedTaskBar;
    }

    // Rebuild
    _cachedTaskBar = /* build string */;
    _taskBarWindowCount = topLevelWindows.Count;
    _taskBarStateHash = stateHash;

    return _cachedTaskBar;
}

private int ComputeTaskBarStateHash(List<Window> windows)
{
    int hash = 0;
    foreach (var w in windows)
    {
        hash ^= w.Title.GetHashCode();
        hash ^= w.State.GetHashCode();
        hash ^= w.GetIsActive().GetHashCode();
    }
    return hash;
}

// Invalidate when:
// - Window title changes
// - Window state changes (minimize/restore)
// - Active window changes
```

**Impact**: Eliminates string building on every frame when taskbar hasn't changed

---

## Implementation Priorities

### Phase 1: Low-Hanging Fruit (1-2 days)
1. ✅ Remove LINQ from UpdateDisplay() render loops (1.1)
2. ✅ Cache windowsToRender HashSet (1.2)
3. ✅ Optimize AnyWindowDirty() (1.3)
4. ✅ Merge dirty detection loops (3.1)

**Expected Gain**: 10-20% reduction in frame time, eliminates ~500 bytes allocations per frame

---

### Phase 2: String Optimization (2-3 days)
1. ✅ Cache window border strings (2.1)
2. ✅ Pool rectangle lists (2.2)
3. ✅ Cache fill strings (5.1)

**Expected Gain**: 20-30% reduction in string allocations, smoother rendering

---

### Phase 3: Structural Changes (3-5 days)
1. ⚠️ Add sorted window list (4.1) - requires careful testing
2. ✅ Cache coverage results (3.2)
3. ✅ Pre-compute status bar (5.2)

**Expected Gain**: 15-25% improvement in complex scenarios (10+ windows)

---

## Measurement Points

Before implementing, add metrics to measure:

1. **Allocation Rate**: Track GC gen0 collections per second
2. **Frame Time**: Track UpdateDisplay() execution time
3. **String Allocations**: Count ConvertSpectreMarkupToAnsi calls per frame
4. **LINQ Enumerators**: Count Where/OrderBy calls per frame

**Profiling Command**:
```bash
dotnet run --project Example -c Release
# Monitor with dotnet-counters:
dotnet-counters monitor --process-id <PID> System.Runtime
```

**Expected Results After All Optimizations**:
- 30-50% reduction in GC gen0 collections
- 20-40% reduction in frame time (typical 4-6ms → 3-4ms)
- 60-80% reduction in string allocations
- 90%+ reduction in LINQ enumerator allocations

---

## Risk Assessment

| Optimization | Risk | Testing Required |
|--------------|------|------------------|
| 1.1 Remove LINQ | LOW | Verify render order unchanged |
| 1.2 Cache HashSet | LOW | Verify Clear() resets properly |
| 1.3 Optimize AnyWindowDirty | LOW | Compare results with LINQ version |
| 2.1 Border cache | MEDIUM | Test active state changes, resize |
| 2.2 Rectangle pooling | MEDIUM | Verify no list sharing bugs |
| 3.1 Merge loops | MEDIUM | Extensive render testing |
| 3.2 Coverage cache | MEDIUM | Test window move/resize invalidation |
| 4.1 Sorted list | HIGH | Full regression testing |
| 5.1 Fill string cache | LOW | Test cache size limits |
| 5.2 Status bar cache | MEDIUM | Test all window state transitions |

---

## Code Quality Notes

### DO:
- ✅ Use simple caching (invalidate on change)
- ✅ Replace LINQ in hot paths with manual loops
- ✅ Reuse collections (Clear() instead of new)
- ✅ Add metrics to prove improvements

### DON'T:
- ❌ Use complex pooling libraries (ArrayPool is fine, custom pools are overkill)
- ❌ Optimize non-hot paths (e.g., window creation)
- ❌ Sacrifice readability for < 1% gains
- ❌ Add caching without invalidation strategy

---

## Conclusion

These optimizations follow **business-standard practices**:
- Eliminate allocations in hot paths (P1, P2)
- Reduce redundant work (P3)
- Cache expensive computations (P2, P5)
- Use appropriate data structures (P4)

**Total Expected Improvement**: 30-50% reduction in frame time and memory allocations for typical 3-5 window scenarios.

All optimizations are **production-proven patterns** used in commercial UI frameworks (WPF, Qt, game engines). No over-engineering, no premature optimization.

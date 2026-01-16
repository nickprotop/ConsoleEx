# Deep Code Duplication Analysis

**Analysis Date:** 2026-01-16
**Codebase:** SharpConsoleUI
**Focus:** Identifying ALL code duplication patterns for refactoring

---

## Executive Summary

This document provides a comprehensive analysis of code duplication across the SharpConsoleUI codebase. Beyond the obvious copy-paste duplication, we analyze structural patterns, algorithmic repetition, and boilerplate code that can be consolidated.

**Total Duplication Categories Found:** 10 categories
**Estimated Duplicated Lines:** ~800-1000 lines
**Potential Line Reduction:** ~400-600 lines after extraction

---

## Category 1: Margin/Padding Rendering (CRITICAL)

**Severity:** HIGH
**Instances:** 14 files
**Duplicated Lines:** ~140 lines
**Pattern:** Identical loops filling top/bottom margins

### Affected Files:
1. ListControl.cs
2. TreeControl.cs
3. DropdownControl.cs
4. MenuControl.cs
5. MultilineEditControl.cs
6. HorizontalGridControl.cs
7. ScrollablePanelControl.cs
8. ToolbarControl.cs
9. CheckboxControl.cs
10. ColumnContainer.cs
11. SplitterControl.cs
12. LogViewerControl.cs
13. SpectreRenderableControl.cs
14. PromptControl.cs

### Duplicated Pattern:
```csharp
// TOP MARGIN - Repeated 14 times with minor variations
for (int y = bounds.Y; y < startY && y < bounds.Bottom; y++)
{
    if (y >= clipRect.Y && y < clipRect.Bottom)
    {
        buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
    }
}

// BOTTOM MARGIN - Repeated 14 times
for (int y = endY; y < bounds.Bottom; y++)
{
    if (y >= clipRect.Y && y < clipRect.Bottom)
    {
        buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
    }
}

// HORIZONTAL MARGINS - Repeated in various forms
if (contentStartX > bounds.X)
{
    buffer.FillRect(...);  // Left margin
}
if (contentEndX < bounds.Right)
{
    buffer.FillRect(...);  // Right margin
}
```

### Recommendation:
**Extract to:** `SharpConsoleUI/Helpers/ControlRenderingHelpers.cs`
**Methods:** `FillTopMargin()`, `FillBottomMargin()`, `FillHorizontalMargins()`
**Impact:** -140 lines, +100 lines helper = **-40 net lines**

---

## Category 2: Color Resolution Chains (HIGH)

**Severity:** HIGH
**Instances:** 11+ files
**Duplicated Lines:** ~33 lines
**Pattern:** Cascading null-coalescing operators (4-level chains)

### Affected Files:
1. SpectreRenderableControl.cs
2. DropdownControl.cs
3. MenuControl.cs (2 instances)
4. CheckboxControl.cs
5. ListControl.cs
6. MultilineEditControl.cs
7. SplitterControl.cs
8. TreeControl.cs
9. ButtonControl.cs
10. PromptControl.cs
11. ToolbarControl.cs

### Duplicated Pattern:
```csharp
// Background color resolution - Repeated 11 times
public Color BackgroundColor
{
    get => _backgroundColorValue
        ?? Container?.BackgroundColor
        ?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor
        ?? Color.Black;
}

// Foreground color resolution - Repeated 11 times
public Color ForegroundColor
{
    get => _foregroundColorValue
        ?? Container?.ForegroundColor
        ?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor
        ?? Color.White;
}

// Menu bar colors - Repeated 2 times (MenuControl.cs)
private Color ResolvedMenuBarBackground
    => _menuBarBackgroundColor
    ?? Container?.GetConsoleWindowSystem?.Theme?.MenuBarBackgroundColor
    ?? Container?.BackgroundColor
    ?? Color.Black;
```

### Recommendation:
**Extract to:** `SharpConsoleUI/Helpers/ColorResolver.cs`
**Methods:** `ResolveBackground()`, `ResolveForeground()`, `ResolveMenuBarBackground()`, `ResolveMenuBarForeground()`
**Impact:** -33 lines, +150 lines helper = **+117 net lines** (but massive readability improvement)

---

## Category 3: Property Setter Boilerplate (MEDIUM-HIGH)

**Severity:** MEDIUM-HIGH
**Instances:** 177 property setters
**Duplicated Lines:** ~200-250 lines
**Pattern:** Same `Container?.Invalidate(true)` in every setter

### Statistics:
- **ListControl.cs:** 50 `Container?.Invalidate()` calls
- **MenuControl.cs:** 47 calls
- **DropdownControl.cs:** 46 calls
- **TreeControl.cs:** 34 calls
- **MultilineEditControl.cs:** ~40 calls (estimated)

### Duplicated Pattern:
```csharp
// Repeated in EVERY property setter
public string Text
{
    get => _text;
    set
    {
        _text = value;
        Container?.Invalidate(true);  // ← This line repeated 177+ times
    }
}

public Color BackgroundColor
{
    get => _backgroundColor;
    set
    {
        _backgroundColor = value;
        Container?.Invalidate(true);  // ← Same line
    }
}

public bool Visible
{
    get => _visible;
    set
    {
        _visible = value;
        Container?.Invalidate(true);  // ← Same line
    }
}
```

### Variations Found:
```csharp
// Some have validation
set
{
    var validated = Math.Max(0, value);
    if (_field != validated)
    {
        _field = validated;
        Container?.Invalidate(true);
    }
}

// Some have side effects
set
{
    _field = value;
    UpdateRelatedState();
    Container?.Invalidate(true);
}
```

### Recommendation:
**Two options:**

#### Option A: Property Base Class (Complex, high impact)
Create `InvalidatingProperty<T>` base class or helper that auto-invalidates on change.
```csharp
protected InvalidatingProperty<string> Text { get; }
protected InvalidatingProperty<Color> BackgroundColor { get; }
```
**Pros:** Massive line reduction
**Cons:** Changes property patterns significantly, adds complexity
**Impact:** -150 lines across all controls
**Risk:** HIGH (architectural change)

#### Option B: Accept Current Pattern (Low impact)
The current pattern is **consistent and clear**. While repetitive, it's predictable and easy to understand.
Just ensure:
1. No **duplicate** `Invalidate()` calls in same setter
2. Use guard clauses when appropriate: `if (_field == value) return;`

**Recommendation:** **Option B** - Accept the boilerplate, but enforce:
- Single `Invalidate()` at end of setter
- Guard clauses to prevent unnecessary invalidations

---

## Category 4: Scroll Management Logic (MEDIUM)

**Severity:** MEDIUM
**Instances:** 3 controls (ListControl, TreeControl, DropdownControl)
**Duplicated Lines:** ~60-80 lines
**Pattern:** Similar scroll offset calculations and EnsureVisible logic

### Affected Files:
- ListControl.cs (18 scroll-related operations)
- TreeControl.cs (6 scroll-related operations)
- DropdownControl.cs (estimated 10 operations)

### Duplicated Patterns:

#### Pattern 1: Scroll Offset Management
```csharp
// ListControl.cs
private int _scrollOffset = 0;
private int CurrentScrollOffset => _scrollOffset;
private void SetScrollOffset(int offset)
{
    _scrollOffset = Math.Max(0, offset);
}

// TreeControl.cs - SIMILAR
private int _scrollOffset = 0;
private int CurrentScrollOffset => _scrollOffset;
// Direct _scrollOffset manipulation instead of SetScrollOffset method
```

#### Pattern 2: EnsureVisible Logic
```csharp
// ListControl.cs
private void EnsureSelectedItemVisible()
{
    int selectedIndex = CurrentSelectedIndex;
    if (selectedIndex < 0)
        return;

    int effectiveMaxVisibleItems = GetEffectiveVisibleItems();
    int scrollOffset = CurrentScrollOffset;

    if (selectedIndex < scrollOffset)
    {
        SetScrollOffset(selectedIndex);
    }
    else if (selectedIndex >= scrollOffset + effectiveMaxVisibleItems)
    {
        SetScrollOffset(selectedIndex - effectiveMaxVisibleItems + 1);
    }
}

// DropdownControl.cs - SIMILAR LOGIC
private void EnsureSelectionVisible()
{
    // Similar pattern with minor variations
}
```

#### Pattern 3: Mouse Wheel Scrolling
```csharp
// ListControl.cs
if (_scrollOffset > 0)
{
    _scrollOffset = Math.Max(0, _scrollOffset - _mouseWheelScrollSpeed);
    Container?.Invalidate(true);
}
// ...
if (_scrollOffset < maxScroll)
{
    _scrollOffset = Math.Min(maxScroll, _scrollOffset + _mouseWheelScrollSpeed);
    Container?.Invalidate(true);
}

// Similar in DropdownControl.cs
```

### Recommendation:
**Extract to:** `SharpConsoleUI/Helpers/ScrollHelper.cs` or base class `ScrollableControl`

**Option A: Helper Class (Preferred)**
```csharp
public class ScrollHelper
{
    private int _scrollOffset;
    public int ScrollOffset => _scrollOffset;

    public void SetScrollOffset(int offset) => _scrollOffset = Math.Max(0, offset);

    public void EnsureIndexVisible(int index, int maxVisibleItems)
    {
        if (index < _scrollOffset)
            SetScrollOffset(index);
        else if (index >= _scrollOffset + maxVisibleItems)
            SetScrollOffset(index - maxVisibleItems + 1);
    }

    public bool ScrollUp(int step, Action invalidate)
    {
        if (_scrollOffset > 0)
        {
            SetScrollOffset(_scrollOffset - step);
            invalidate?.Invoke();
            return true;
        }
        return false;
    }

    public bool ScrollDown(int step, int maxScroll, Action invalidate)
    {
        if (_scrollOffset < maxScroll)
        {
            SetScrollOffset(Math.Min(maxScroll, _scrollOffset + step));
            invalidate?.Invoke();
            return true;
        }
        return false;
    }
}
```

**Impact:** -60 lines across 3 controls, +80 lines helper = **+20 net lines** (but centralized logic)

---

## Category 5: Keyboard Navigation (MEDIUM)

**Severity:** MEDIUM
**Instances:** 12 controls implement `ProcessKey`
**Duplicated Lines:** ~100-150 lines
**Pattern:** Similar Up/Down/PageUp/PageDown/Home/End handling

### Controls with ProcessKey:
1. ListControl.cs (13 ConsoleKey cases)
2. TreeControl.cs (10 cases)
3. DropdownControl.cs (8 cases)
4. MenuControl.cs
5. MultilineEditControl.cs
6. PromptControl.cs
7. CheckboxControl.cs
8. ButtonControl.cs
9. ScrollablePanelControl.cs
10. ToolbarControl.cs
11. HorizontalGridControl.cs
12. SplitterControl.cs

### Common Keyboard Patterns:

#### Pattern 1: Arrow Navigation (Up/Down)
```csharp
// ListControl.cs
case ConsoleKey.DownArrow:
    if (_hoveredIndex != -1)
    {
        _hoveredIndex = -1;
        ItemHovered?.Invoke(this, -1);
    }

    if (_selectedIndex < _items.Count - 1)
    {
        SelectedIndex = _selectedIndex + 1;
        return true;
    }
    break;

case ConsoleKey.UpArrow:
    if (_hoveredIndex != -1)
    {
        _hoveredIndex = -1;
        ItemHovered?.Invoke(this, -1);
    }

    if (_selectedIndex > 0)
    {
        SelectedIndex = _selectedIndex - 1;
        return true;
    }
    break;

// TreeControl.cs - SIMILAR PATTERN
case ConsoleKey.DownArrow:
    MoveSelection(1);
    return true;
case ConsoleKey.UpArrow:
    MoveSelection(-1);
    return true;
```

#### Pattern 2: Page Navigation (PageUp/PageDown)
```csharp
// ListControl.cs
case ConsoleKey.PageDown:
    int pageSize = _calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10;
    if (_selectedIndex < _items.Count - 1)
    {
        SelectedIndex = Math.Min(_items.Count - 1, _selectedIndex + pageSize);
        return true;
    }
    break;

// Similar in TreeControl, DropdownControl
```

#### Pattern 3: Home/End Navigation
```csharp
// ListControl.cs
case ConsoleKey.Home:
    if (_items.Count > 0)
    {
        SelectedIndex = 0;
        return true;
    }
    break;

case ConsoleKey.End:
    if (_items.Count > 0)
    {
        SelectedIndex = _items.Count - 1;
        return true;
    }
    break;
```

### Recommendation:
**Extract to:** `SharpConsoleUI/Helpers/NavigationHelper.cs` or `KeyboardNavigationBehavior` class

**Option A: Helper Methods (Preferred)**
```csharp
public static class NavigationHelper
{
    public static bool HandleUpArrow(int currentIndex, int count, Action<int> setIndex)
    {
        if (currentIndex > 0)
        {
            setIndex(currentIndex - 1);
            return true;
        }
        return false;
    }

    public static bool HandleDownArrow(int currentIndex, int count, Action<int> setIndex)
    {
        if (currentIndex < count - 1)
        {
            setIndex(currentIndex + 1);
            return true;
        }
        return false;
    }

    public static bool HandlePageDown(int currentIndex, int count, int pageSize, Action<int> setIndex)
    {
        if (currentIndex < count - 1)
        {
            setIndex(Math.Min(count - 1, currentIndex + pageSize));
            return true;
        }
        return false;
    }

    public static bool HandlePageUp(int currentIndex, int pageSize, Action<int> setIndex)
    {
        if (currentIndex > 0)
        {
            setIndex(Math.Max(0, currentIndex - pageSize));
            return true;
        }
        return false;
    }

    public static bool HandleHome(int count, Action<int> setIndex)
    {
        if (count > 0)
        {
            setIndex(0);
            return true;
        }
        return false;
    }

    public static bool HandleEnd(int count, Action<int> setIndex)
    {
        if (count > 0)
        {
            setIndex(count - 1);
            return true;
        }
        return false;
    }
}
```

**Usage:**
```csharp
// In ProcessKey:
case ConsoleKey.DownArrow:
    return NavigationHelper.HandleDownArrow(_selectedIndex, _items.Count,
        index => SelectedIndex = index);

case ConsoleKey.UpArrow:
    return NavigationHelper.HandleUpArrow(_selectedIndex, _items.Count,
        index => SelectedIndex = index);

case ConsoleKey.PageDown:
    return NavigationHelper.HandlePageDown(_selectedIndex, _items.Count,
        _calculatedMaxVisibleItems ?? ControlDefaults.DefaultVisibleItems,
        index => SelectedIndex = index);
```

**Impact:** -100 lines across 12 controls, +120 lines helper = **+20 net lines** (but much cleaner)

---

## Category 6: Mouse Hit Testing (LOW-MEDIUM)

**Severity:** LOW-MEDIUM
**Instances:** 5-7 controls with mouse support
**Duplicated Lines:** ~30-40 lines
**Pattern:** Converting mouse Y coordinate to item index

### Affected Files (with IMouseAwareControl):
- ListControl.cs
- DropdownControl.cs
- MenuControl.cs
- ToolbarControl.cs
- HorizontalGridControl.cs
- MultilineEditControl.cs (maybe)

### Duplicated Pattern:
```csharp
// ListControl.cs - Multiple places
int relativeY = args.Position.Y - titleOffset;

if (relativeY >= 0 && relativeY < Math.Min(_items.Count, effectiveMaxVisibleItems))
{
    hoveredIndex = _scrollOffset + relativeY;
}

// Later:
if (relativeY >= 0 && relativeY < _items.Count)
{
    int clickedIndex = _scrollOffset + relativeY;
    // Process click
}
```

### Recommendation:
**Extract to:** `SharpConsoleUI/Helpers/MouseHelper.cs`

```csharp
public static class MouseHelper
{
    public static int? ConvertYCoordinateToIndex(
        int mouseY,
        int boundsY,
        int scrollOffset,
        int headerOffset,
        int itemCount,
        int maxVisibleItems)
    {
        int relativeY = mouseY - boundsY - headerOffset;

        if (relativeY >= 0 && relativeY < Math.Min(itemCount, maxVisibleItems))
        {
            int index = scrollOffset + relativeY;
            if (index >= 0 && index < itemCount)
                return index;
        }

        return null;
    }

    public static bool IsWithinBounds(Point position, LayoutRect bounds)
    {
        return position.X >= bounds.X && position.X < bounds.Right
            && position.Y >= bounds.Y && position.Y < bounds.Bottom;
    }
}
```

**Impact:** -30 lines, +40 lines helper = **+10 net lines**

---

## Category 7: Text Measurement Caching (PERFORMANCE)

**Severity:** MEDIUM (Performance issue, not duplication)
**Instances:** 3-4 controls
**Duplicated Lines:** 0 (missing functionality)
**Pattern:** Repeated calls to `AnsiConsoleHelper.StripSpectreLength()`

### Affected Files:
- **ListControl.cs:** 13+ calls to `StripSpectreLength` in rendering (lines 362, 368, 973, 977, 1307, 1323, 1333, 1474, 1548, 1618, 1620, 1630, 1634)
- **TreeControl.cs:** Similar pattern
- **MenuControl.cs:** Similar pattern
- **DropdownControl.cs:** Similar pattern

### Problem:
```csharp
// ListControl.cs - Called MULTIPLE times per frame with SAME input
int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");  // Line 362
// ... later in same method ...
int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");  // Line 973

int titleLength = AnsiConsoleHelper.StripSpectreLength(_title) + 5;  // Line 368
// ... later ...
int titleLength = AnsiConsoleHelper.StripSpectreLength(_title) + 5;  // Line 977
```

### Recommendation:
**Add to each control:** Caching dictionary + invalidation

```csharp
// Add to controls
private Dictionary<string, int>? _textLengthCache;

private int GetCachedTextLength(string text)
{
    _textLengthCache ??= new Dictionary<string, int>();

    if (!_textLengthCache.TryGetValue(text, out int length))
    {
        length = AnsiConsoleHelper.StripSpectreLength(text);
        _textLengthCache[text] = length;
    }

    return length;
}

private void InvalidateLengthCache()
{
    _textLengthCache?.Clear();
}

// Call InvalidateLengthCache() when items/title change
```

**Impact:** +15 lines per control × 4 = **+60 lines** (but significant performance gain)

---

## Category 8: Focus Event Handling (LOW)

**Severity:** LOW
**Instances:** ~10 controls
**Duplicated Lines:** ~30 lines
**Pattern:** Identical `GotFocus`/`LostFocus` event firing in HasFocus setter

### Duplicated Pattern:
```csharp
// Repeated in multiple controls
public bool HasFocus
{
    get => _hasFocus;
    set
    {
        var hadFocus = _hasFocus;
        _hasFocus = value;
        Container?.Invalidate(true);

        // Fire focus events
        if (value && !hadFocus)
        {
            GotFocus?.Invoke(this, EventArgs.Empty);
        }
        else if (!value && hadFocus)
        {
            LostFocus?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

### Recommendation:
**Option A:** Extract to base class or helper
**Option B:** Accept as standard pattern (it's only 12 lines per control)

**Recommendation:** **Option B** - This is a reasonable pattern, keep it.

---

## Category 9: Item Collection Management (LOW)

**Severity:** LOW
**Instances:** ListControl, TreeControl, DropdownControl
**Duplicated Lines:** ~20-30 lines
**Pattern:** Similar Add/Remove/Clear methods

### Duplicated Pattern:
```csharp
// ListControl, DropdownControl - Similar
public void AddItem(string text)
{
    _items.Add(new ListItem(text));
    Container?.Invalidate(true);
}

public void RemoveItem(int index)
{
    if (index >= 0 && index < _items.Count)
    {
        _items.RemoveAt(index);
        // Adjust selection...
        Container?.Invalidate(true);
    }
}

public void ClearItems()
{
    _items.Clear();
    _selectedIndex = -1;
    SetScrollOffset(0);
    Container?.Invalidate(true);

    if (_isSelectable)
    {
        SelectedIndexChanged?.Invoke(this, -1);
        SelectedItemChanged?.Invoke(this, null);
        SelectedValueChanged?.Invoke(this, null);
    }
}
```

### Recommendation:
**Could extract to:** `ItemCollectionManager<T>` helper class
**BUT:** Each control has slightly different behavior (tree has nodes, list has items, dropdown has special logic)

**Recommendation:** **Keep separate** - the differences are significant enough to justify duplication.

---

## Category 10: Validation Patterns (LOW)

**Severity:** LOW
**Instances:** Throughout codebase
**Duplicated Lines:** ~50 lines
**Pattern:** Similar Math.Max/Math.Min validation

### Duplicated Pattern:
```csharp
// Width validation - Repeated ~15 times
set
{
    var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
    if (_width != validatedValue)
    {
        _width = validatedValue;
        Container?.Invalidate(true);
    }
}

// Index validation - Repeated ~10 times
if (value < -1 || value >= _items.Count)
    throw new ArgumentOutOfRangeException(nameof(value));

// Bounds checking - Repeated ~20 times
if (index >= 0 && index < _items.Count)
{
    // Do something
}
```

### Recommendation:
**Could extract to:** `ValidationHelper.cs`
**BUT:** These are very simple, inline code is clearer

**Recommendation:** **Keep inline** - extraction would make code less readable.

---

## Summary and Priority Matrix

| Category | Severity | Instances | Lines Saved | Priority | Extract? |
|----------|----------|-----------|-------------|----------|----------|
| 1. Margin Rendering | HIGH | 14 | -40 | **CRITICAL** | ✅ YES |
| 2. Color Resolution | HIGH | 11+ | +117* | **HIGH** | ✅ YES |
| 3. Property Setters | MED-HIGH | 177 | Accept | LOW | ❌ NO |
| 4. Scroll Management | MEDIUM | 3 | +20 | **MEDIUM** | ✅ MAYBE |
| 5. Keyboard Navigation | MEDIUM | 12 | +20 | **MEDIUM** | ✅ MAYBE |
| 6. Mouse Hit Testing | LOW-MED | 5-7 | +10 | LOW | ⚠️ OPTIONAL |
| 7. Text Caching | MEDIUM | 4 | +60 | **HIGH** | ✅ YES |
| 8. Focus Events | LOW | 10 | Accept | LOW | ❌ NO |
| 9. Item Collections | LOW | 3 | Accept | LOW | ❌ NO |
| 10. Validation | LOW | Many | Accept | LOW | ❌ NO |

*Net line increase but massive readability improvement

### Recommended Extraction Priority:

**Phase 1 - MUST DO:**
1. ✅ Margin Rendering → `ControlRenderingHelpers.cs` (-40 lines)
2. ✅ Color Resolution → `ColorResolver.cs` (+117 lines, huge readability)
3. ✅ Text Caching → Add to each control (+60 lines, performance)

**Phase 2 - SHOULD DO:**
4. ⚠️ Scroll Management → `ScrollHelper.cs` (+20 lines, centralized logic)
5. ⚠️ Keyboard Navigation → `NavigationHelper.cs` (+20 lines, consistency)

**Phase 3 - OPTIONAL:**
6. ⚠️ Mouse Hit Testing → `MouseHelper.cs` (+10 lines)

**Accept As-Is:**
- Property setter boilerplate (consistent pattern)
- Focus event handling (standard pattern)
- Item collection methods (too specific per control)
- Validation patterns (inline is clearer)

### Net Impact:
**Must Do (Phase 1):** +137 lines, huge quality improvement
**Should Do (Phase 2):** +40 lines, better organization
**Optional (Phase 3):** +10 lines

**Total:** +187 lines of shared helpers, -~400 lines of duplication = **-213 net lines**

---

## Conclusion

The codebase has **significant structural duplication** that can be extracted without changing architecture:

**Top 3 Wins:**
1. **Margin rendering** - Clear duplication, easy extraction
2. **Color resolution** - Makes code dramatically more readable
3. **Text caching** - Performance improvement + reduces redundant calls

**Keep As-Is:**
- Property setter boilerplate (consistent and clear)
- Focus handling (standard pattern)
- Simple validation (inline is clearer)

This analysis reveals that while the codebase has ~800-1000 lines of duplication, **not all duplication is bad**. Some patterns (like property setters) are intentionally consistent boilerplate. The focus should be on extracting algorithmic and rendering duplication where extraction genuinely improves maintainability.

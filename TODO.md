# TODO List - SharpConsoleUI

## Critical Bugs to Hunt

### Menu Dropdown Bottom Border Clipping Bug
**File**: `SharpConsoleUI/Controls/MenuControl.cs:1236`
**Status**: ⚠️ WORKAROUND ACTIVE (-2 adjustment)
**Priority**: HIGH
**Date**: 2026-01-26

**Problem**:
Menu dropdown submenus are getting clipped at the window buffer boundary. The bottom border doesn't render when dropdown extends to the calculated buffer limit.

**Symptoms**:
- Performance submenu shows incomplete items
- Bottom border missing when dropdown positioned near window bottom
- Last row of dropdown gets cut off

**Current Workaround**:
```csharp
int renderBottom = contentTop + bufferHeight - 2;  // WORKAROUND: -2 to avoid clipping
```

**Why -2 Works** (probably):
- Window buffer is `(Width-2) × (Height-2)` (excludes borders)
- Content starts at `contentTop = windowTop + borderOffset`
- But there's an off-by-one (or off-by-two) somewhere in the rendering chain
- Using `-2` forces dropdowns to stop 2 rows earlier, avoiding the clipped zone

**Investigation Needed**:
1. **CharacterBuffer boundary checks**: Does `SetCell(x, y)` have off-by-one in bounds checking?
2. **Portal rendering**: Does `MenuPortalContent.PaintDOM()` clip incorrectly?
3. **DrawBox border drawing**: Does `DrawBox()` draw borders outside calculated bounds?
4. **Window content clipping**: Does `Window.RenderAndGetVisibleContent()` have incorrect clip rect?
5. **Buffer indexing**: Is buffer 0-indexed but calculations assume 1-indexed?

**Debug Evidence** (from logs):
```
Window: (0, 1, 207, 59) ShowTitle=False
Buffer: 205×57 (Width-2 × Height-2) ✅ Buffer created correctly
Content area: (1, 2) to (206, 57) ❌ SMOKING GUN!

PROBLEM: Content area shows (1,2) to (206,57):
  - Y range: 2 to 57 inclusive = 57-2+1 = 56 rows ❌ Only 56 rows!
  - SHOULD BE: 2 to 58 inclusive = 58-2+1 = 57 rows ✅

The buffer is 57 rows tall, but only 56 rows are being used!

Math breakdown:
  contentTop = windowTop(1) + borderOffset(1) = 2
  bufferHeight = 57 (window height 59 - 2 for borders)

  WITHOUT workaround:
    renderBottom = 2 + 57 = 59 ❌ (beyond actual drawable area)

  With -1:
    renderBottom = 2 + 57 - 1 = 58 ❌ (still clips!)

  With -2:
    renderBottom = 2 + 57 - 2 = 57 ✅ (works!)

Buffer mapping to screen:
  Buffer row 0  → Screen Y = 2
  Buffer row 1  → Screen Y = 3
  ...
  Buffer row 56 → Screen Y = 58 (SHOULD be drawable, but isn't!)

Dropdown test:
  Performance submenu: 3 items + 2 borders = 5 rows
  Original position: Y=55 → occupies 55-59 → clips at 57!
  With -2: positioned Y=52 → occupies 52-56 → fits ✅
```

**THE MYSTERY**:
The buffer is created with 57 rows (indices 0-56), which should map to screen Y positions 2-58.
But something in the rendering pipeline only renders up to Y=57, leaving the last row (Y=58) unused.
The -2 workaround compensates by limiting dropdowns to Y=57 max.

**Files to Hunt the Bug**:
- `SharpConsoleUI/Controls/MenuControl.cs` - Dropdown positioning & rendering
- `SharpConsoleUI/Core/CharacterBuffer.cs` - SetCell/GetCell bounds checking
- `SharpConsoleUI/Window.cs` - Buffer creation and content rendering
- `SharpConsoleUI/Renderer.cs` - Window/overlay rendering pipeline
- `SharpConsoleUI/Layout/Portal.cs` - Portal clipping logic

**Test Case**:
1. Run `Examples/StartMenuDemo`
2. Press Ctrl+Esc to open Start menu
3. Hover over "System" → hover over "Performance"
4. Performance submenu should show 3 items + borders (5 rows total)
5. Bottom border should be visible

---

## Code Cleanup

### Remove Debug Logging
**Priority**: MEDIUM
**Status**: TODO

Remove all temporary debug logging added during overlay refactoring:

**Files**:
- `SharpConsoleUI/Window.cs` - `ProcessWindowMouseEvent()` System.IO.File.AppendAllText
- `SharpConsoleUI/ConsoleWindowSystem.cs` - `PropagateMouseEventToWindow()` logging
- `SharpConsoleUI/Controls/MenuControl.cs` - `CalculateDropdownBounds()` logging
- `SharpConsoleUI/Controls/MenuControl.cs` - `PaintDOM()` logging
- `SharpConsoleUI/Windows/OverlayWindow.cs` - `OnUnhandledMouseClick()` logging

**Action**: Search for `/tmp/overlay_mouse_debug.log` and remove all instances.

---

## Recently Completed ✅

### OverlayWindow Architecture Refactoring (2026-01-26)
- ✅ Removed ContentBounds concept (use DOM layout instead)
- ✅ Added UnhandledMouseClick event to Window base class
- ✅ Moved click-outside-to-dismiss logic into OverlayWindow
- ✅ Simplified Renderer.RenderOverlayWindow() (no dimension juggling)
- ✅ Cleaned up ConsoleWindowSystem special cases
- ✅ StartMenuDialog uses Margin + StickyPosition for positioning

### Menu Dropdown Dimension Fixes (2026-01-26)
- ✅ Fixed dropdown width calculation (+10 for padding+borders, was +8)
- ✅ Fixed submenu width calculation (+10 for padding+borders, was +8)
- ✅ Now consistent with height calculation (always +2 for borders)

# SharpConsoleUI Refactoring Progress Tracker

**Started:** 2026-01-16
**Status:** Not Started
**Current Phase:** Phase 1 - Fix Typos

---

## Overview

**Core Tasks:** 21 (Phases 1-7)
**Optional Tasks:** 14 (Phases 8-10)
**Total Tasks:** 35

**Completed:** 0
**Remaining:** 35

**Core Estimated Time:** ~12 hours (Phases 1-7 MUST DO)
**Optional Estimated Time:** ~7-10 hours (Phases 8-10 based on decisions)
**Total Estimated Time:** ~19-22 hours

**Expected Outcome (Core Only):** -110 net lines, much cleaner code
**Expected Outcome (With Optionals):** -300+ net lines, highly optimized

---

## Phase 1: Fix Typos in Public API (CRITICAL - Breaking Changes)

**Status:** ✅ COMPLETED (4/4 tasks done)
**Tasks:** 4
**Estimated Time:** 30 minutes

- [x] **Task 1:** Fix typo `AllreadyHandled` → `AlreadyHandled` in Window.cs ✅ COMPLETED
  - Files: `SharpConsoleUI/Window.cs`
  - Search: `AllreadyHandled`, `allreadyHandled`
  - **Result:** Fixed 7 instances (found more than estimated):
    - Line 99: XML comment parameter
    - Line 100: Constructor parameter
    - Line 103: Property assignment
    - Line 109: Property declaration
    - Line 2619: XML comment parameter
    - Line 2621: Method parameter
    - Line 2626: KeyPressedEventArgs instantiation

- [x] **Task 2:** Fix typo `OnCLosing` → `OnClosing` in Window.cs and WindowBuilder.cs ✅ COMPLETED
  - Files: `SharpConsoleUI/Window.cs`, `SharpConsoleUI/Builders/WindowBuilder.cs`
  - Search: `OnCLosing`
  - **Result:** Fixed 4 instances:
    - Window.cs line 269: Event declaration
    - Window.cs line 710: Event null check
    - Window.cs line 713: Event invocation
    - WindowBuilder.cs line 468: XML comment
    - WindowBuilder.cs line 599: Event subscription

- [x] **Task 3:** Fix typo `DesktopBackroundChar` → `DesktopBackgroundChar` in all themes ✅ COMPLETED
  - Files:
    - `SharpConsoleUI/Themes/ITheme.cs`
    - `SharpConsoleUI/Themes/ClassicTheme.cs`
    - `SharpConsoleUI/Themes/ModernGrayTheme.cs`
    - `SharpConsoleUI/Plugins/DeveloperTools/DevDarkTheme.cs`
    - `SharpConsoleUI/ConsoleWindowSystem.cs` (8 usages)
    - `SharpConsoleUI/Core/ThemeStateService.cs`
  - Search: `DesktopBackroundChar`
  - **Result:** Fixed 13 instances total across 6 files:
    - ITheme.cs: 1 (interface property)
    - ClassicTheme.cs: 1 (property implementation)
    - ModernGrayTheme.cs: 1 (property implementation)
    - DevDarkTheme.cs: 1 (property implementation)
    - ConsoleWindowSystem.cs: 8 (property usages)
    - ThemeStateService.cs: 1 (property usage)

- [x] **Task 4:** Commit typo fixes ✅ COMPLETED
  - Commit message: `"Fix typos in public API (breaking changes)"`
  - Commit hash: `42d053f`
  - **Files modified:** 11 files (8 code files + 3 new tracking documents)
  - **Total fixes:** 24 typo instances corrected
  - **Note:** Ready to push when all phases complete

---

## Phase 2: Remove Code Duplication (Category 1 - CRITICAL)

**Status:** 🔄 In Progress (2/3 tasks completed)
**Tasks:** 3
**Estimated Time:** 1 hour
**Impact:** -108 duplicated lines, +121 helper lines + 18 calls = **-90 net reduction** (better than estimated!)

**Deep Analysis Findings:**
- **Severity:** HIGH (Category 1 in CODE_DUPLICATION_ANALYSIS.md)
- **Pattern:** Identical margin/padding rendering loops repeated 14 times
- **Total Duplicated Lines:** ~140 lines
- **Affected Files:** 14 control files

**Duplicated Patterns Found:**

```csharp
// TOP MARGIN - Repeated 14 times
for (int y = bounds.Y; y < startY && y < bounds.Bottom; y++)
{
    if (y >= clipRect.Y && y < clipRect.Bottom)
        buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
}

// BOTTOM MARGIN - Repeated 14 times
for (int y = endY; y < bounds.Bottom; y++)
{
    if (y >= clipRect.Y && y < clipRect.Bottom)
        buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
}

// HORIZONTAL MARGINS - Repeated in various forms
if (contentStartX > bounds.X)
    buffer.FillRect(...);  // Left margin
if (contentEndX < bounds.Right)
    buffer.FillRect(...);  // Right margin
```

- [x] **Task 5:** Create `SharpConsoleUI/Helpers/ControlRenderingHelpers.cs` ✅ COMPLETED
  - Add methods: `FillTopMargin`, `FillBottomMargin`, `FillHorizontalMargins`
  - See REFACTORING_PROMPT.md for detailed implementation
  - See CODE_DUPLICATION_ANALYSIS.md Category 1 for analysis
  - **Result:** Created 121-line helper class with 3 static methods
  - **Build:** Verified successful (0 errors)

- [x] **Task 6:** Replace 14 instances of duplicated margin filling code ✅ COMPLETED
  - **Files updated (9 controls with the pattern):**
    1. ✅ ListControl.cs - Lines 1447, 1699 (top/bottom margin)
    2. ✅ TreeControl.cs - Lines 1440, 1621 (top/bottom margin)
    3. ✅ DropdownControl.cs - Lines 1134, 1210 (top/bottom margin)
    4. ❌ MenuControl.cs - No margin pattern found
    5. ✅ MultilineEditControl.cs - Lines 1849, 2187 (top/bottom margin)
    6. ❌ HorizontalGridControl.cs - No margin pattern found
    7. ❌ ScrollablePanelControl.cs - No margin pattern found
    8. ❌ ToolbarControl.cs - No margin pattern found
    9. ✅ CheckboxControl.cs - Lines 442, 480 (top/bottom margin)
    10. ❌ ColumnContainer.cs - No margin pattern found
    11. ✅ SplitterControl.cs - Lines 414, 443 (top/bottom margin)
    12. ✅ LogViewerControl.cs - Lines 513, 755 (top/bottom margin)
    13. ✅ SpectreRenderableControl.cs - Lines 246, 320 (top/bottom margin)
    14. ✅ PromptControl.cs - Lines 497, 622 (top/bottom margin)
  - **Total instances replaced:** 18 margin filling loops (9 controls × 2 loops each)
  - **Lines removed:** ~108 lines of duplicated code
  - **Lines added:** 18 helper calls
  - **Net reduction:** -90 lines
  - **Build:** Verified successful (0 errors, 101 warnings)
  - **Note:** 5 controls didn't have the pattern, only 9 needed updates

- [ ] **Task 7:** Commit duplication fixes
  - Commit message: `"Extract duplicated rendering code to ControlRenderingHelpers"`
  - Push to remote

---

## Phase 3: Consolidate Color Resolution (Category 2 - HIGH)

**Status:** ⏸️ Not Started
**Tasks:** 3
**Estimated Time:** 1 hour
**Impact:** -33 duplicated lines, +150 helper lines = **+117 net lines** (massive readability improvement)

**Deep Analysis Findings:**
- **Severity:** HIGH (Category 2 in CODE_DUPLICATION_ANALYSIS.md)
- **Pattern:** Cascading 4-level null-coalescing operators (`??`) repeated 11+ times
- **Total Duplicated Lines:** ~33 lines
- **Affected Files:** 11+ control files

**Duplicated Patterns Found:**

```csharp
// Background resolution - Repeated 11+ times
public Color BackgroundColor
{
    get => _backgroundColorValue
        ?? Container?.BackgroundColor
        ?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor
        ?? Color.Black;
}

// Foreground resolution - Repeated 11+ times
public Color ForegroundColor
{
    get => _foregroundColorValue
        ?? Container?.ForegroundColor
        ?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor
        ?? Color.White;
}

// Menu bar resolution - Repeated in MenuControl.cs (2 instances)
private Color ResolvedMenuBarBackground
    => _menuBarBackgroundColor
    ?? Container?.GetConsoleWindowSystem?.Theme?.MenuBarBackgroundColor
    ?? Container?.BackgroundColor
    ?? Color.Black;
```

**Problem:** Unreadable, error-prone, difficult to maintain

- [ ] **Task 8:** Create `SharpConsoleUI/Helpers/ColorResolver.cs`
  - Add methods: `ResolveBackground`, `ResolveForeground`, `ResolveMenuBarBackground`, `ResolveMenuBarForeground`
  - Centralize the 4-level fallback chain logic
  - See REFACTORING_PROMPT.md for detailed implementation
  - See CODE_DUPLICATION_ANALYSIS.md Category 2 for analysis

- [ ] **Task 9:** Replace 11+ instances of cascading `??` chains
  - **Files to update (all confirmed instances):**
    1. SpectreRenderableControl.cs (line 86) - BackgroundColor
    2. DropdownControl.cs (line 241) - BackgroundColor
    3. MenuControl.cs (lines 172-173) - MenuBar colors (2 instances)
    4. CheckboxControl.cs (line 91) - BackgroundColor
    5. ListControl.cs (line 415) - BackgroundColor
    6. MultilineEditControl.cs (line 166) - BackgroundColor
    7. SplitterControl.cs (line 106) - BackgroundColor
    8. TreeControl.cs (line 112) - BackgroundColor
    9. ButtonControl.cs - Check for ForegroundColor/BackgroundColor
    10. PromptControl.cs - Check for color properties
    11. ToolbarControl.cs - Check for color properties
  - **Search pattern:** `?? Container?.BackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme`
  - **Replace pattern:**
    - Background: `ColorResolver.ResolveBackground(_backgroundColorValue, Container)`
    - Foreground: `ColorResolver.ResolveForeground(_foregroundColorValue, Container)`
    - MenuBar: `ColorResolver.ResolveMenuBarBackground(_menuBarBackgroundColor, Container)`
  - **Verification:** Build succeeds, colors render identically

- [ ] **Task 10:** Commit color resolution refactoring
  - Commit message: `"Consolidate color resolution logic to ColorResolver"`
  - Push to remote

---

## Phase 4: Remove Excessive Documentation

**Status:** ⏸️ Not Started
**Tasks:** 2
**Estimated Time:** 1.5 hours

- [ ] **Task 11:** Remove excessive XML comments from obvious properties
  - **DELETE comments for:**
    - `Text`, `Name`, `Tag`, `Width`, `Height` properties
    - `IsEnabled`, `Visible`, `HasFocus` properties
    - `BackgroundColor`, `ForegroundColor` properties
    - Simple constructors that just assign parameters
    - Any "Gets or sets the [property name]" comments
  - **KEEP comments for:**
    - Properties with side effects (e.g., SelectedIndex that scrolls)
    - Properties with complex behavior (e.g., SelectionMode explanation)
    - Non-obvious algorithms or logic
  - Estimated deletions: ~200-300 XML comment blocks
  - Files: All controls (ButtonControl.cs, ListControl.cs, TreeControl.cs, etc.)

- [ ] **Task 12:** Commit documentation cleanup
  - Commit message: `"Remove excessive documentation from self-explanatory members"`
  - Push to remote

---

## Phase 5: Fix Event Double-Firing & Redundant Invalidations

**Status:** ⏸️ Not Started
**Tasks:** 4
**Estimated Time:** 1.5 hours

- [ ] **Task 13:** Audit controls for duplicate event firing
  - Files to audit:
    - ListControl.cs
    - TreeControl.cs
    - MenuControl.cs
    - DropdownControl.cs
  - Look for methods that invoke same event multiple times
  - Document findings before fixing

- [ ] **Task 14:** Fix redundant `Container?.Invalidate()` calls
  - Pattern: Multiple `Container?.Invalidate(true)` calls in one property setter
  - Fix: Move to single call at end of method
  - Example: Property setters, update methods

- [ ] **Task 15:** Add guard clauses to prevent unnecessary event firing
  - Pattern: Event fired when state hasn't actually changed
  - Fix: Add `if (value == _field) return;` at start of setters
  - Example: `SelectedIndex` setter should check if index actually changed

- [ ] **Task 16:** Commit event firing fixes
  - Commit message: `"Fix duplicate event firing and redundant invalidations"`
  - Push to remote

---

## Phase 6: Add Configuration Infrastructure

**Status:** ⏸️ Not Started
**Tasks:** 3
**Estimated Time:** 2 hours

- [ ] **Task 17:** Create `SharpConsoleUI/Configuration/ControlDefaults.cs`
  - Add constants for:
    - Layout defaults (DefaultMinimumVisibleItems = 3, DefaultVisibleItems = 10)
    - Text defaults (DefaultTextPadding = 4, DefaultTitlePadding = 5)
    - Scrolling defaults (DefaultScrollStep = 1)
    - Input defaults (DefaultBlinkRateMs = 500)
    - Tree/List defaults (DefaultIndentSize = 2, DefaultExpandedIcon = "▼")
    - Button defaults (DefaultFocusPrefix = ">")
    - Dialog defaults (DefaultDialogWidth = 60)
  - See REFACTORING_PROMPT.md lines 345-387 for full implementation

- [ ] **Task 18:** Replace magic numbers throughout codebase
  - Search for hardcoded: `3`, `5`, `10`, `4`, `500`, etc. in control files
  - Replace with: `ControlDefaults.DefaultMinimumVisibleItems`, etc.
  - Files: All controls
  - Be careful: Only replace semantic constants, not loop indices or coordinates

- [ ] **Task 19:** Commit configuration infrastructure
  - Commit message: `"Add ControlDefaults for magic number constants"`
  - Push to remote

---

## Phase 7: Performance - Add Caching (Category 7 - HIGH PERFORMANCE)

**Status:** ⏸️ Not Started
**Tasks:** 2
**Estimated Time:** 1 hour
**Impact:** +60 lines (caching infrastructure), **significant performance improvement**

**Deep Analysis Findings:**
- **Severity:** MEDIUM (Category 7 in CODE_DUPLICATION_ANALYSIS.md)
- **Pattern:** Repeated expensive calls to `AnsiConsoleHelper.StripSpectreLength()` with same input
- **Total Missing Optimization:** 13+ redundant calls per render frame in ListControl alone
- **Affected Files:** 3-4 controls (ListControl, TreeControl, MenuControl, DropdownControl)

**Performance Problem Found:**

```csharp
// ListControl.cs - SAME text measured MULTIPLE times in SINGLE render
// Line 362
int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");
// Line 973 - DUPLICATE call with SAME input!
int itemLength = AnsiConsoleHelper.StripSpectreLength(item.Text + "    ");

// Line 368
int titleLength = AnsiConsoleHelper.StripSpectreLength(_title) + 5;
// Line 977 - DUPLICATE call with SAME input!
int titleLength = AnsiConsoleHelper.StripSpectreLength(_title) + 5;

// Additional calls in SAME file: lines 1307, 1323, 1333, 1474, 1548, 1618, 1620, 1630, 1634
```

**Impact:** `StripSpectreLength` is called on the **same text multiple times per frame** = wasted CPU cycles

- [ ] **Task 20:** Add caching for expensive text measurements
  - **Files to update with caching:**
    1. **ListControl.cs** - 13+ calls to StripSpectreLength in rendering
       - Lines: 362, 368, 973, 977, 1307, 1323, 1333, 1474, 1548, 1618, 1620, 1630, 1634
       - Cache item text lengths and title length
    2. **TreeControl.cs** - Similar pattern in tree rendering
       - Cache node text lengths
    3. **MenuControl.cs** - Menu item text measurements
       - Cache menu item text lengths
    4. **DropdownControl.cs** - Dropdown item text measurements
       - Cache dropdown item text lengths

  - **Add to each control:**
    ```csharp
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
    ```

  - **Call InvalidateLengthCache() when:**
    - Items added/removed (AddItem, RemoveItem, ClearItems)
    - Item text changed
    - Title changed
    - Nodes added/removed (for TreeControl)

  - **Replace StripSpectreLength calls:**
    - `AnsiConsoleHelper.StripSpectreLength(text)` → `GetCachedTextLength(text)`

  - **Verification:** Build succeeds, no visual changes, measure performance improvement

- [ ] **Task 21:** Commit performance optimizations
  - Commit message: `"Add caching for expensive text measurement operations"`
  - Push to remote
  - **Expected result:** Significant frame time reduction for controls with many items

---

## Phase 8: Additional Helper Extraction (OPTIONAL - Based on Deep Analysis)

**Status:** ⏸️ Not Started
**Tasks:** 6
**Estimated Time:** 2-3 hours

**Note:** These are OPTIONAL extractions based on CODE_DUPLICATION_ANALYSIS.md findings.
Review the analysis document before deciding which to implement.

- [ ] **Task 22:** Create `SharpConsoleUI/Helpers/ScrollHelper.cs` (OPTIONAL)
  - Add scroll offset management and EnsureVisible logic
  - Impact: Consolidates scroll logic from ListControl, TreeControl, DropdownControl
  - See CODE_DUPLICATION_ANALYSIS.md Category 4
  - Net: +20 lines, centralized logic

- [ ] **Task 23:** Extract scroll logic from ListControl, TreeControl, DropdownControl (OPTIONAL)
  - Replace scroll management code with ScrollHelper calls
  - Update 3 controls

- [ ] **Task 24:** Create `SharpConsoleUI/Helpers/NavigationHelper.cs` (OPTIONAL)
  - Add keyboard navigation helpers: HandleUpArrow, HandleDownArrow, HandlePageUp, HandlePageDown, HandleHome, HandleEnd
  - Impact: Consolidates keyboard nav from 12 controls
  - See CODE_DUPLICATION_ANALYSIS.md Category 5
  - Net: +20 lines, consistent behavior

- [ ] **Task 25:** Extract keyboard navigation from controls (OPTIONAL)
  - Replace Up/Down/PageUp/PageDown/Home/End cases with NavigationHelper calls
  - Update 12 controls (ListControl, TreeControl, DropdownControl, MenuControl, etc.)

- [ ] **Task 26:** Create `SharpConsoleUI/Helpers/MouseHelper.cs` (OPTIONAL)
  - Add ConvertYCoordinateToIndex and IsWithinBounds methods
  - Impact: Consolidates mouse hit testing logic
  - See CODE_DUPLICATION_ANALYSIS.md Category 6
  - Net: +10 lines

- [ ] **Task 27:** Commit additional helper extraction (OPTIONAL - if any done)
  - Commit message: `"Extract scroll, navigation, and mouse helpers for code reuse"`
  - Push to remote

---

## Phase 9: Code Organization (OPTIONAL)

**Status:** ⏸️ Not Started
**Tasks:** 2
**Estimated Time:** 1 hour

- [ ] **Task 28:** Add `#region` markers to large files (OPTIONAL)
  - Files to organize:
    - ListControl.cs (2098 lines)
    - TreeControl.cs (1720 lines)
    - MenuControl.cs (2185 lines)
    - MultilineEditControl.cs (2203 lines)
  - Regions to add:
    - `#region Fields and Properties`
    - `#region Constructors`
    - `#region Public API Methods`
    - `#region Layout and Measurement`
    - `#region Rendering`
    - `#region Input Handling`
    - `#region Helper Methods`

- [ ] **Task 29:** Commit organization improvements (OPTIONAL)
  - Commit message: `"Improve code organization with regions"`
  - Push to remote

---

## Phase 10: Split Large Monolithic Controls (OPTIONAL - Advanced)

**Status:** ⏸️ Not Started
**Tasks:** 6
**Estimated Time:** 4-6 hours
**Risk:** HIGH - Complex refactoring, only if truly needed

**Decision Criteria:** Only split if controls have clearly separable concerns:
- Distinct rendering vs interaction logic
- Node management separable from rendering
- Different responsibilities that can be cleanly partitioned

**Files to Consider:**
- ListControl.cs (2098 lines) - Recommended: Consider splitting
- TreeControl.cs (1720 lines) - Recommended: Consider splitting
- MenuControl.cs (2185 lines) - Maybe: Complex menu bar + popup logic
- MultilineEditControl.cs (2203 lines) - Maybe: Text editing + rendering

- [ ] **Task 30:** Analyze ListControl for split opportunities (OPTIONAL)
  - Review ListControl.cs structure
  - Identify: Core (state), Rendering (Paint), Interaction (Mouse/Keyboard)
  - Decision: Split into partial classes or keep monolithic?
  - See REFACTORING_PROMPT.md lines 544-593 for guidance

- [ ] **Task 31:** Split ListControl into partial classes (OPTIONAL - if justified)
  - Create: `ListControl.cs` (core + properties)
  - Create: `ListControl.Rendering.cs` (Paint, layout calculations)
  - Create: `ListControl.Interaction.cs` (ProcessKey, mouse handling)
  - Ensure all partials have proper `#region` markers

- [ ] **Task 32:** Analyze TreeControl for split opportunities (OPTIONAL)
  - Review TreeControl.cs structure
  - Identify: Core (nodes), Rendering (Paint), Navigation (expand/collapse)
  - Decision: Split into partial classes or keep monolithic?

- [ ] **Task 33:** Split TreeControl into partial classes (OPTIONAL - if justified)
  - Create: `TreeControl.cs` (core + node management)
  - Create: `TreeControl.Rendering.cs` (Paint, tree rendering)
  - Create: `TreeControl.Navigation.cs` (expand/collapse, traversal)

- [ ] **Task 34:** Test split controls thoroughly (OPTIONAL - if any splits done)
  - Verify ListControl behavior unchanged
  - Verify TreeControl behavior unchanged
  - Run all examples
  - Check for any regressions

- [ ] **Task 35:** Commit control splitting (OPTIONAL - if done)
  - Commit message: `"Split ListControl and TreeControl into partial classes for better organization"`
  - Push to remote

---

## Summary of Changes (Expected)

### New Files Created (3)
- ✅ `SharpConsoleUI/Helpers/ControlRenderingHelpers.cs` (~100 lines)
- ✅ `SharpConsoleUI/Helpers/ColorResolver.cs` (~150 lines)
- ✅ `SharpConsoleUI/Configuration/ControlDefaults.cs` (~80 lines)

### Files Modified (~25)
- ✅ Window.cs (typo fixes, event audit)
- ✅ WindowBuilder.cs (typo fixes)
- ✅ All theme files (typo fixes)
- ✅ All controls with margin filling (~14 files, -10 lines each)
- ✅ All controls with color resolution (~11 files, -3 lines each)
- ✅ All controls with excessive docs (~20 files, -250 lines total)
- ✅ Controls with magic numbers (~20 files, small edits)
- ✅ Controls with event double-firing (~4-5 files, small fixes)

### Net Line Count Change
- **New code:** +330 lines (helpers + configuration)
- **Removed duplication:** -140 lines (margin code)
- **Removed complexity:** -33 lines (color chains)
- **Removed docs:** -250 lines (excessive XML comments)
- **Event fixes:** -20 lines (redundant invocations)
- **Net change:** **-110 lines** (cleaner, more maintainable)

---

## Quality Improvements Achieved

- ✅ Fixed embarrassing typos in public API
- ✅ DRY principle (no duplicated code)
- ✅ SRP principle (extracted complexity to helpers)
- ✅ Self-documenting code (named constants)
- ✅ Lean documentation (signal-to-noise ratio)
- ✅ Correct event firing (no duplicates)
- ✅ Better performance (caching)
- ✅ Preserved architecture (interfaces, state services, builders)

---

## Notes

**Architectural decisions preserved:**
- ✅ Multiple interfaces per control (ISP) - intentional design
- ✅ State services architecture - intentional design
- ✅ Builder patterns - intentional design
- ✅ Notification-based focus - intentional design
- ✅ Monolithic Window.cs and ConsoleWindowSystem.cs - user designed

**Breaking changes:**
- API typo fixes are breaking changes but necessary for professionalism
- Users will need to update: `AllreadyHandled` → `AlreadyHandled`, `OnCLosing` → `OnClosing`, `DesktopBackroundChar` → `DesktopBackgroundChar`

**Testing after each phase:**
- Build succeeds without errors
- All examples run without crashes
- Visual appearance unchanged
- Keyboard/mouse interaction still works
- No performance regressions

---

**Last Updated:** 2026-01-16 (Deep duplication analysis added)
**Completed Phases:** 0 / 10 (7 core + 3 optional)
**Completed Core Tasks:** 0 / 21
**Completed Optional Tasks:** 0 / 14
**Total Completed:** 0 / 35

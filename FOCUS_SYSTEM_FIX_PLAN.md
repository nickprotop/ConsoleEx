# Focus System Fix Plan - Phase 6

## Overview
Comprehensive fix for focus system bugs discovered during deep analysis. Implements parallel fix-then-test workflow where each bug fix is immediately validated with dedicated tests.

**Timeline**: Week 3 - Days 2-4 (aligned with TEST_IMPLEMENTATION_PLAN.md Phase 6)
**Approach**: Fix → Test → Verify (parallel workflow)
**Total Bugs**: 10 bugs identified and documented
**Total Tests**: ~128 new tests across 11 test files
**Expected Result**: 401 tests passing (273 current + 128 new)

---

## Bugs Summary

### CRITICAL (Architectural)
- **Bug #1**: Window._interactiveContents only contains top-level controls
- **Bug #2**: No IContainerControl interface to expose children ← ROOT CAUSE
- **Bug #7**: No recursive collection mechanism

### HIGH Priority
- **Bug #3**: Missing IDirectionalFocusControl on ToolbarControl, MenuControl, ColumnContainer
- **Bug #4**: Invisible controls can receive Tab focus (no Visible check)
- **Bug #8**: BringIntoFocus uses wrong index after flattening

### MEDIUM Priority
- **Bug #5**: Mouse unfocus loop only checks top-level controls
- **Bug #6**: Ambiguous focus detection uses LastOrDefault()
- **Bug #9**: BringIntoFocus bounds may be incorrect for nested controls

### LOW Priority
- **Bug #10**: Sticky controls edge case in scroll calculation

---

## Phase 1: Architectural Foundation

### Task #4: Create IContainerControl Interface ⏳ IN PROGRESS
**Status**: Not Started
**Bug**: #2 (ROOT CAUSE)
**File**: `SharpConsoleUI/Controls/IContainerControl.cs`

**Implementation**:
```csharp
namespace SharpConsoleUI.Controls
{
    /// <summary>
    /// Interface for controls that contain child controls.
    /// All GUI frameworks expose children from containers - this is fundamental.
    /// </summary>
    public interface IContainerControl
    {
        /// <summary>
        /// Gets the direct child controls of this container.
        /// Does not recursively include grandchildren - recursion happens in caller.
        /// </summary>
        /// <returns>Read-only list of direct child controls</returns>
        IReadOnlyList<IWindowControl> GetChildren();
    }
}
```

**Verification**:
- [ ] Interface compiles without errors
- [ ] Can be implemented by container controls
- [ ] Follows standard GUI framework patterns (WPF, WinForms, Qt)

---

### Task #5: Implement IContainerControl on All Containers
**Status**: Blocked by Task #4
**Bug**: #2
**Files**: 5 container controls

**Implementations**:

1. **ColumnContainer.cs** (line ~28)
```csharp
public class ColumnContainer : IContainer, IInteractiveControl, IFocusableControl,
                                IMouseAwareControl, ILayoutAware, IDOMPaintable, IContainerControl
{
    public IReadOnlyList<IWindowControl> GetChildren()
    {
        return _contents.AsReadOnly();
    }
}
```

2. **HorizontalGridControl.cs** (line ~40)
```csharp
public IReadOnlyList<IWindowControl> GetChildren()
{
    var children = new List<IWindowControl>();
    foreach (var column in _columns)
    {
        children.AddRange(column.GetChildren());
    }
    return children.AsReadOnly();
}
```

3. **ScrollablePanelControl.cs** (line ~30)
```csharp
public IReadOnlyList<IWindowControl> GetChildren()
{
    return _children.AsReadOnly();
}
```

4. **ToolbarControl.cs** (line ~25)
```csharp
public IReadOnlyList<IWindowControl> GetChildren()
{
    return _items.Cast<IWindowControl>().ToList().AsReadOnly();
}
```

5. **MenuControl.cs** (line ~30)
```csharp
public IReadOnlyList<IWindowControl> GetChildren()
{
    return _items.Cast<IWindowControl>().ToList().AsReadOnly();
}
```

**Tests**: `SharpConsoleUI.Tests/FocusManagement/ContainerChildExposureTests.cs` (10 tests)
- [ ] ColumnContainer exposes _contents correctly
- [ ] HorizontalGridControl flattens all column children
- [ ] ScrollablePanelControl exposes _children correctly
- [ ] ToolbarControl exposes _items correctly
- [ ] MenuControl exposes menu items correctly
- [ ] GetChildren() returns direct children only (not recursive)
- [ ] Nested containers work (container in container)
- [ ] Empty containers return empty list
- [ ] Children list is read-only
- [ ] Children list updates when controls added/removed

**Verification**:
- [ ] All 5 containers implement interface
- [ ] All 10 tests pass
- [ ] No build errors

---

### Task #6: Add GetAllFocusableControlsFlattened to Window
**Status**: Blocked by Task #5
**Bug**: #1, #7
**File**: `SharpConsoleUI/Window.cs`

**Implementation** (add around line ~900):
```csharp
/// <summary>
/// Gets all focusable controls in the window, flattened into a single list.
/// Recursively traverses container hierarchy and includes nested controls.
/// Excludes invisible controls and containers without IDirectionalFocusControl.
/// </summary>
/// <returns>Flattened list of focusable controls in visual order</returns>
private List<IInteractiveControl> GetAllFocusableControlsFlattened()
{
    var result = new List<IInteractiveControl>();
    foreach (var control in _controls)
    {
        CollectFocusableControls(control, result);
    }
    return result;
}

/// <summary>
/// Recursively collects focusable controls from a control and its children.
/// </summary>
private void CollectFocusableControls(IWindowControl control, List<IInteractiveControl> result)
{
    // Skip invisible controls
    if (!control.Visible)
        return;

    // Recurse into container children first (depth-first traversal)
    if (control is IContainerControl container)
    {
        foreach (var child in container.GetChildren())
        {
            CollectFocusableControls(child, result);
        }
    }

    // Add control itself if focusable
    // Containers with IDirectionalFocusControl are included (they delegate properly)
    // Containers without IDirectionalFocusControl are excluded (transparent)
    if (control is IInteractiveControl interactive &&
        control is IFocusableControl focusable &&
        focusable.CanReceiveFocus)
    {
        result.Add(interactive);
    }
}
```

**Tests**: `SharpConsoleUI.Tests/FocusManagement/FocusFlatteningTests.cs` (12 tests)
- [ ] Flattened list includes nested controls
- [ ] Top-level controls included
- [ ] 2-level nesting works (button in container)
- [ ] 3-level nesting works (button in container in container)
- [ ] Invisible controls excluded from list
- [ ] Disabled controls excluded from list
- [ ] Containers without IDirectionalFocusControl excluded (transparent)
- [ ] Containers with IDirectionalFocusControl included
- [ ] Empty window returns empty list
- [ ] Order is depth-first (visual order)
- [ ] Dynamic changes reflected (add/remove control)
- [ ] Performance acceptable for complex UIs

**Verification**:
- [ ] Methods compile without errors
- [ ] All 12 tests pass
- [ ] No infinite recursion
- [ ] Performance acceptable

---

## Phase 2: Navigation Core

### Task #7: Replace _interactiveContents with Flattened List in SwitchFocus
**Status**: Blocked by Task #6
**Bug**: #1, #7
**File**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**Implementation** (modify SwitchFocus method, line ~620):
```csharp
public void SwitchFocus(bool backward = false)
{
    lock (_window._lock)
    {
        // NEW: Use flattened list instead of _interactiveContents
        var focusableControls = _window.GetAllFocusableControlsFlattened();

        if (focusableControls.Count == 0) return;

        // Find currently focused control in flattened list
        var currentIndex = focusableControls.FindIndex(ic => ic.HasFocus);

        // If no control is focused but we have a last focused control, use that
        if (currentIndex == -1 && _window._lastFocusedControl != null)
        {
            currentIndex = focusableControls.IndexOf(_window._lastFocusedControl);
        }

        // Remove focus from current if there is one
        if (currentIndex != -1)
        {
            _window._lastFocusedControl = focusableControls[currentIndex];
            focusableControls[currentIndex].HasFocus = false;
        }

        // Find next focusable control
        int nextIndex = currentIndex;
        int attempts = 0;
        do
        {
            if (backward)
            {
                nextIndex = (nextIndex - 1 + focusableControls.Count) % focusableControls.Count;
            }
            else
            {
                nextIndex = (nextIndex + 1) % focusableControls.Count;
            }

            attempts++;

            var control = focusableControls[nextIndex];
            bool canFocus = true;

            if (control is Controls.IFocusableControl focusable)
            {
                canFocus = focusable.CanReceiveFocus;
            }

            if (canFocus)
            {
                // Use directional focus for containers
                if (control is Controls.IDirectionalFocusControl directional)
                {
                    directional.SetFocusWithDirection(true, backward);
                }
                else if (control is Controls.IFocusableControl focusableControl)
                {
                    focusableControl.SetFocus(true, Controls.FocusReason.Keyboard);
                }
                else
                {
                    control.HasFocus = true;
                }

                _window._lastFocusedControl = control;
                _window.FocusService?.SetFocus(_window, control, FocusChangeReason.Keyboard);
                _window._windowSystem?.LogService?.LogTrace($"Focus switched in '{_window.Title}': {_window._lastFocusedControl?.GetType().Name}", "Focus");

                BringIntoFocus(control); // Changed from BringIntoFocus(nextIndex)
                break;
            }

        } while (attempts < focusableControls.Count && nextIndex != currentIndex);
    }
}
```

**Tests**: `SharpConsoleUI.Tests/FocusManagement/TabNavigationTests.cs` (20 tests)
- [ ] Tab reaches nested controls
- [ ] Tab cycles through complete list
- [ ] Shift+Tab goes backward correctly
- [ ] Tab order respects visual layout (depth-first)
- [ ] Tab skips invisible controls
- [ ] Tab skips disabled controls
- [ ] Tab into container with IDirectionalFocusControl focuses first child
- [ ] Shift+Tab into container focuses last child
- [ ] Tab out of container continues correctly
- [ ] Tab with no focusable controls does nothing
- [ ] Tab with single control stays on it
- [ ] Tab from last control wraps to first
- [ ] Shift+Tab from first control wraps to last
- [ ] Nested containers work (3 levels deep)
- [ ] HorizontalGridControl columns navigated correctly
- [ ] ScrollablePanel children navigated correctly
- [ ] ColumnContainer children navigated correctly
- [ ] Mixed containers work (grid with panels with buttons)
- [ ] Dynamic add/remove control updates Tab order
- [ ] Tab performance acceptable with 100+ controls

**Verification**:
- [ ] All 20 tests pass
- [ ] Tab reaches all nested controls
- [ ] No infinite loops

---

### Task #8: Add Visibility Check to Tab Navigation
**Status**: Blocked by Task #7
**Bug**: #4
**File**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**Implementation** (modify SwitchFocus, line ~665):
```csharp
var control = focusableControls[nextIndex];
bool canFocus = control.Visible; // ADD THIS CHECK

if (control is Controls.IFocusableControl focusable)
{
    canFocus = canFocus && focusable.CanReceiveFocus;
}
```

**Tests**: `SharpConsoleUI.Tests/FocusManagement/VisibilityFocusTests.cs` (8 tests)
- [ ] Invisible controls cannot receive Tab focus
- [ ] Setting Visible=false while focused removes focus
- [ ] Tab skips invisible controls
- [ ] Setting Visible=true makes control focusable again
- [ ] Invisible nested control skipped
- [ ] All children invisible makes container transparent
- [ ] Mouse cannot focus invisible (verify existing behavior)
- [ ] Keyboard and mouse visibility behavior consistent

**Verification**:
- [ ] All 8 tests pass
- [ ] Invisible controls never receive focus
- [ ] Matches mouse behavior

---

## Phase 3: Container Focus

### Task #9: Implement IDirectionalFocusControl on ToolbarControl
**Status**: Blocked by Task #6
**Bug**: #3 (1/3)
**File**: `SharpConsoleUI/Controls/ToolbarControl.cs`

**Implementation**:
1. Add interface (line ~25): `, IDirectionalFocusControl`
2. Add field: `private bool _focusFromBackward = false;`
3. Add method:
```csharp
public void SetFocusWithDirection(bool focus, bool backward)
{
    _focusFromBackward = backward;
    SetFocus(focus, FocusReason.Keyboard);
}
```
4. Modify SetFocus to handle backward flag:
```csharp
if (focus && _focusedItem == null)
{
    _focusedItem = _focusFromBackward
        ? GetFocusableItems().LastOrDefault()
        : GetFocusableItems().FirstOrDefault();
    _focusFromBackward = false; // Reset
}
```

**Tests**: `SharpConsoleUI.Tests/FocusManagement/ToolbarFocusTests.cs` (8 tests)
- [ ] Tab into toolbar focuses first item
- [ ] Shift+Tab into toolbar focuses last item
- [ ] Tab out of toolbar continues to next control
- [ ] Arrow keys navigate within toolbar
- [ ] Disabled items are skipped
- [ ] Empty toolbar doesn't receive focus
- [ ] Toolbar with single item works
- [ ] Nested toolbar in container works

**Verification**:
- [ ] All 8 tests pass
- [ ] Pattern matches ScrollablePanelControl

---

### Task #10: Implement IDirectionalFocusControl on MenuControl
**Status**: Blocked by Task #6
**Bug**: #3 (2/3)
**File**: `SharpConsoleUI/Controls/MenuControl.cs`

**Implementation**: Same pattern as ToolbarControl

**Tests**: `SharpConsoleUI.Tests/FocusManagement/MenuFocusTests.cs` (10 tests)
- [ ] Tab into menu focuses first item
- [ ] Shift+Tab into menu focuses last item
- [ ] Arrow keys navigate menu items
- [ ] Enter opens submenu
- [ ] Submenu focus works correctly
- [ ] Tab out of menu continues correctly
- [ ] Disabled menu items skipped
- [ ] Separator items skipped
- [ ] Nested menu in toolbar works
- [ ] Menu with no items doesn't receive focus

**Verification**:
- [ ] All 10 tests pass
- [ ] Menu navigation feels natural

---

### Task #11: Implement IDirectionalFocusControl on ColumnContainer
**Status**: Blocked by Task #6
**Bug**: #3 (3/3)
**File**: `SharpConsoleUI/Controls/ColumnContainer.cs`

**Implementation**: Same pattern, use GetChildren() from IContainerControl

**Tests**: `SharpConsoleUI.Tests/FocusManagement/ColumnContainerFocusTests.cs` (8 tests)
- [ ] Tab into container focuses first child
- [ ] Shift+Tab into container focuses last child
- [ ] Tab through all children works
- [ ] Nested containers work correctly
- [ ] Empty container doesn't receive focus
- [ ] Single child container works
- [ ] Container in HorizontalGrid works
- [ ] Dynamic add child updates focus

**Verification**:
- [ ] All 8 tests pass
- [ ] All 3 containers now have IDirectionalFocusControl

---

## Phase 4: Scrolling

### Task #12: Fix BringIntoFocus Signature
**Status**: Blocked by Task #7
**Bug**: #8
**File**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**Implementation** (line ~704):
```csharp
// OLD:
private void BringIntoFocus(int nextIndex)
{
    var focusedContent = _window._interactiveContents[nextIndex] as IWindowControl;
    // ...
}

// NEW:
private void BringIntoFocus(IWindowControl focusedControl)
{
    if (focusedControl == null) return;

    var bounds = _window._layoutManager.GetOrCreateControlBounds(focusedControl);
    var controlBounds = bounds.ControlContentBounds;
    // ... rest of method uses focusedControl directly
}
```

Update call site (line ~693): `BringIntoFocus(control);`

**Tests**: `SharpConsoleUI.Tests/FocusManagement/FocusScrollingTests.cs` (15 tests)
- [ ] Tab to off-screen control scrolls into view (below)
- [ ] Tab to off-screen control scrolls into view (above)
- [ ] Shift+Tab scrolls correctly
- [ ] Control at bottom edge scrolls correctly
- [ ] Control at top edge scrolls correctly
- [ ] Nested control scrolls correctly
- [ ] 2-level nesting scrolls correctly
- [ ] 3-level nesting scrolls correctly
- [ ] Partially visible control scrolls into full view
- [ ] Scroll doesn't move if already visible
- [ ] Scroll respects sticky controls
- [ ] Scroll boundaries respected (no negative)
- [ ] Scroll boundaries respected (no overflow)
- [ ] PageUp/PageDown with focus works
- [ ] Home/End with focus works

**Verification**:
- [ ] All 15 tests pass
- [ ] No index out of range errors

---

### Task #13: Verify BringIntoFocus Bounds for Nested Controls
**Status**: Blocked by Task #12
**Bug**: #9
**File**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**Implementation** (modify BringIntoFocus if needed):
```csharp
// Consider using DOM absolute bounds for nested controls:
var node = _window._renderer?.GetLayoutNode(focusedControl);
if (node != null)
{
    var absoluteBounds = node.AbsoluteBounds; // Already in window coordinates
    // Use absoluteBounds for scroll calculation
}
else
{
    // Fallback to layout manager
    var bounds = _window._layoutManager.GetOrCreateControlBounds(focusedControl);
    // ...
}
```

**Tests**: Add to FocusScrollingTests.cs
- [ ] Button in ColumnContainer scrolls correctly
- [ ] Button in ColumnContainer in ScrollablePanel scrolls correctly
- [ ] Deep nesting (4 levels) scrolls correctly

**Verification**:
- [ ] Tests pass with nested controls
- [ ] Scroll position is accurate

---

## Phase 5: Cleanup

### Task #14: Fix Mouse Unfocus Loop
**Status**: Blocked by Task #6
**Bug**: #5
**File**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**Implementation** (line ~305):
```csharp
// OLD:
foreach (var control in _window._interactiveContents)
{
    if (control.HasFocus && control != newFocusTarget && control is IFocusableControl focusable)
    {
        focusable.SetFocus(false, FocusReason.Mouse);
    }
}

// NEW:
var allFocusable = _window.GetAllFocusableControlsFlattened();
foreach (var control in allFocusable)
{
    if (control.HasFocus && control != newFocusTarget && control is IFocusableControl focusable)
    {
        focusable.SetFocus(false, FocusReason.Mouse);
    }
}
```

**Tests**: `SharpConsoleUI.Tests/FocusManagement/MouseFocusTests.cs` (12 tests)
- [ ] Click nested control to focus
- [ ] Click elsewhere unfocuses nested control
- [ ] Click another nested control switches focus
- [ ] Click empty space clears focus
- [ ] Invisible controls cannot be clicked (verify)
- [ ] Double-click works on nested controls
- [ ] Click container background doesn't focus container
- [ ] Click focuses deepest control at position
- [ ] Mouse and keyboard focus are consistent
- [ ] Click during Tab navigation works correctly
- [ ] Right-click doesn't change focus
- [ ] Drag doesn't change focus

**Verification**:
- [ ] All 12 tests pass
- [ ] Nested controls unfocus properly

---

### Task #15: Improve Focus Detection
**Status**: Blocked by Task #6
**Bug**: #6
**File**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**Implementation** (line ~566):
```csharp
// OLD:
interactiveContent = _window._interactiveContents.LastOrDefault(ic => ic.IsEnabled && ic.HasFocus);

// NEW:
var focusedControls = _window.GetAllFocusableControlsFlattened()
    .Where(ic => ic.IsEnabled && ic.HasFocus)
    .ToList();

if (focusedControls.Count > 1)
{
    _window._windowSystem?.LogService?.LogWarning(
        $"Multiple controls have focus in '{_window.Title}': {string.Join(", ", focusedControls.Select(c => c.GetType().Name))}",
        "Focus");
}

interactiveContent = focusedControls.FirstOrDefault();
```

**Tests**: `SharpConsoleUI.Tests/FocusManagement/FocusStateTests.cs` (10 tests)
- [ ] Only one control has focus at a time
- [ ] Focus state is consistent across operations
- [ ] FocusStateService tracks window focus correctly
- [ ] FocusStateService tracks control focus correctly
- [ ] Focus events fire in correct order (blur then focus)
- [ ] Focus/blur events match HasFocus property
- [ ] GotFocus/LostFocus events fire correctly
- [ ] Focus state survives window resize
- [ ] Focus state survives theme change
- [ ] Focus state cleared on window close

**Verification**:
- [ ] All 10 tests pass
- [ ] Multiple focus detected and logged

---

## Phase 6: Integration & Verification

### Task #16: Create Integration Tests
**Status**: Blocked by Tasks #8, #11, #13, #14, #15
**File**: `SharpConsoleUI.Tests/FocusManagement/FocusIntegrationTests.cs`

**Tests**: (15 tests)
- [ ] Complex nested UI: Window with Toolbar, Panel with Buttons, List
- [ ] Tab through entire hierarchy works
- [ ] Shift+Tab through entire hierarchy works
- [ ] Mouse and keyboard focus are consistent
- [ ] Window switching preserves focus state
- [ ] Modal focus trapping works
- [ ] Multiple windows with independent focus
- [ ] Focus survives scroll
- [ ] Focus survives resize
- [ ] Focus survives control add/remove
- [ ] Focus survives visibility changes
- [ ] Focus with overlapping windows
- [ ] Focus with sticky controls
- [ ] Real-world scenario: Form with mixed controls
- [ ] Performance: 1000 controls, Tab works smoothly

**Verification**:
- [ ] All 15 tests pass
- [ ] All fixes work together harmoniously

---

### Task #17: Run All Tests and Verify
**Status**: Blocked by Task #16

**Expected Test Count**:
- Phase 1-5: 273 tests (current)
- ContainerChildExposureTests: 10 tests
- FocusFlatteningTests: 12 tests
- TabNavigationTests: 20 tests
- VisibilityFocusTests: 8 tests
- ToolbarFocusTests: 8 tests
- MenuFocusTests: 10 tests
- ColumnContainerFocusTests: 8 tests
- FocusScrollingTests: 15 tests
- MouseFocusTests: 12 tests
- FocusStateTests: 10 tests
- FocusIntegrationTests: 15 tests

**Total**: ~401 tests (273 + 128)

**Verification**:
- [ ] All tests pass (401/401)
- [ ] No regressions in Phase 1-5 tests
- [ ] Build succeeds with 0 errors
- [ ] All 10 bugs fixed
- [ ] Manual testing shows improved focus behavior

---

### Task #18: Commit Phase 6
**Status**: Blocked by Task #17

**Commit Message**:
```
Phase 6: Fix focus system with comprehensive test coverage

BUGS FIXED (10 total):
- Bug #1,#7: Incomplete focus list, no recursive collection (CRITICAL)
- Bug #2: Missing IContainerControl interface (ROOT CAUSE)
- Bug #3: Missing IDirectionalFocusControl on 3 containers (HIGH)
- Bug #4: Invisible controls could receive Tab focus (HIGH)
- Bug #5: Mouse unfocus incomplete for nested controls (MEDIUM)
- Bug #6: Ambiguous focus detection (LastOrDefault) (MEDIUM)
- Bug #8: BringIntoFocus index mismatch after flattening (HIGH)
- Bug #9: Bounds incorrect for nested controls (MEDIUM)
- Bug #10: Sticky controls edge case (LOW)

ARCHITECTURAL CHANGES:
- Add IContainerControl interface for child exposure
- Implement on all 5 containers (ColumnContainer, HorizontalGrid, ScrollablePanel, Toolbar, Menu)
- Add recursive focus flattening: GetAllFocusableControlsFlattened()
- Implement IDirectionalFocusControl on 3 missing containers

NAVIGATION FIXES:
- SwitchFocus uses flattened list, reaches nested controls
- Add visibility check to Tab navigation
- Change LastOrDefault to FirstOrDefault with logging

SCROLLING FIXES:
- BringIntoFocus accepts control instead of index
- Verify bounds are window-relative for nested controls

MOUSE FOCUS:
- Unfocus loop uses flattened list

TESTS ADDED (128 tests across 11 files):
- ContainerChildExposureTests (10)
- FocusFlatteningTests (12)
- TabNavigationTests (20)
- VisibilityFocusTests (8)
- ToolbarFocusTests (8)
- MenuFocusTests (10)
- ColumnContainerFocusTests (8)
- FocusScrollingTests (15)
- MouseFocusTests (12)
- FocusStateTests (10)
- FocusIntegrationTests (15)

Total: 401/401 tests passing (100%)

Aligns with TEST_IMPLEMENTATION_PLAN.md Phase 6.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
```

**Verification**:
- [ ] Commit succeeds
- [ ] Git log shows proper attribution
- [ ] All files staged correctly
- [ ] No Claude notification message added

---

## Current Status

**Phase**: Phase 1 - Architectural Foundation
**Current Task**: Task #4 - Create IContainerControl Interface ⏳ IN PROGRESS
**Tasks Complete**: 0 / 15 (0%)
**Tests Passing**: 273 / 401 (68% - Phase 5 baseline)

**Next Steps**:
1. Create IContainerControl interface
2. Implement on 5 containers + write 10 tests
3. Add recursive flattening + write 12 tests
4. Continue through phases...

---

## Notes

- **Parallel Workflow**: Each fix immediately followed by tests
- **Test-Driven Validation**: Every bug fix has dedicated test coverage
- **Regression Protection**: 128 new tests ensure bugs don't return
- **Aligns with TEST_IMPLEMENTATION_PLAN.md**: Phase 6 Focus & Input Tests
- **No Breaking Changes**: All changes are fixes, not API changes
- **Comprehensive**: Covers all 10 identified bugs

---

## Success Criteria

- [ ] All 10 bugs fixed
- [ ] 401 tests passing (100%)
- [ ] No regressions in existing tests
- [ ] Tab navigation reaches all nested controls
- [ ] Invisible controls cannot receive focus
- [ ] Scroll brings focused controls into view
- [ ] Mouse and keyboard focus are consistent
- [ ] All containers expose children properly
- [ ] Focus state is always consistent
- [ ] Manual testing confirms improved UX

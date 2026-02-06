# Focus System Fix Plan - Phase 6

## Overview
Comprehensive fix for focus system bugs discovered during deep analysis. Implements parallel fix-then-test workflow where each bug fix is immediately validated with dedicated tests.

**Timeline**: Week 3 - Days 2-4 (aligned with TEST_IMPLEMENTATION_PLAN.md Phase 6)
**Approach**: Fix → Test → Verify (parallel workflow)
**Total Bugs**: 17 bugs (10 original + 4 from deep analysis + 3 from plan review)
**Total Tests**: ~105 new tests across 10 test files (revised from 128)
**Expected Result**: 378 tests passing (273 current + 105 new)
**Critical Discovery**: 3 containers (ColumnContainer, MenuControl, ToolbarControl) have broken/inconsistent focus behavior

---

## 🔴 CRITICAL FINDINGS - Container Focus Inconsistencies

**Deep analysis revealed existing containers have broken/inconsistent focus behavior:**

### ✅ ScrollablePanelControl (GOOD)
- Has IDirectionalFocusControl ✅
- SetFocus correctly focuses first/last child based on direction
- Has scroll methods: `ScrollVerticalBy()`, `ScrollVerticalTo()`, `ScrollHorizontalBy()`
- **Missing**: `ScrollChildIntoView(IWindowControl child)` - needed for BringIntoFocus

### ✅ HorizontalGridControl (GOOD)
- Has IDirectionalFocusControl ✅
- HasFocus setter calls `FocusChanged()` which correctly focuses first/last child
- **Separators**: SplitterControl objects ARE separate IInteractiveControl instances
- **Separator Behavior**: Splitters are focusable (receive keyboard for resizing), included in Tab order
- **GetChildren() Should Return**: [Column1, Splitter1, Column2, Splitter2, Column3] in order

### ⚠️ ToolbarControl (INCONSISTENT)
- **Missing** IDirectionalFocusControl ❌
- HasFocus setter ONLY focuses first item (line 138)
- **Problem**: Ignores backward direction - should focus last item on Shift+Tab

### 🔴 ColumnContainer (BROKEN)
- **Missing** IDirectionalFocusControl ❌
- SetFocus just sets HasFocus, **NO child focusing at all!**
- **Problem**: Container gets focus but children never do - focus appears "lost"

### 🔴 MenuControl (BROKEN)
- **Missing** IDirectionalFocusControl ❌
- HasFocus setter just sets field, **NO child focusing at all!**
- **Problem**: Same as ColumnContainer - completely broken

**Impact**: Tasks #9, #10, #11 are CRITICAL fixes, not just enhancements!

---

## 🆕 NEW BUGS DISCOVERED IN PLAN REVIEW

### Bug #15: GetAllFocusableControlsFlattened Algorithm Incomplete (CRITICAL)
**Problem**: Plan doesn't specify that containers must be EXCLUDED from flattened list!

**Issue**: If containers are included, they AND their children both appear in list:
- `[Button1, ScrollablePanelControl, ButtonA, ButtonB, Button2]`
- Tab lands on container → container focuses ButtonA
- Next Tab lands on ButtonA again (already focused!) → DOUBLE FOCUS

**Correct behavior**: Only leaf controls in list:
- `[Button1, ButtonA, ButtonB, Button2]`
- Tab flows naturally through children without re-focusing

### Bug #16: HasActiveInteractiveContent Will Be Inefficient (MEDIUM)
**Problem**: After Task #7, this property rebuilds entire flattened list on every access:
```csharp
public bool HasActiveInteractiveContent =>
    GetAllFocusableControlsFlattened().Any(c => c.HasFocus); // REBUILDS!
```

**Better**: Use FocusStateService directly (no list rebuild):
```csharp
public bool HasActiveInteractiveContent =>
    FocusStateService.GetFocusedControl(_window) != null;
```

### Bug #17: Edge Case - Dual-Purpose Controls (LOW)
**Problem**: What about controls that are BOTH containers AND directly focusable?
- Example: TreeControl might handle keyboard (arrow keys) AND have TreeNode children
- Are TreeNodes IWindowControl or internal data?
- Should TreeControl be in Tab order, or only its children?

**Status**: Needs investigation of complex controls (TreeControl, ListControl, MenuControl)

---

## Bugs Summary

### CRITICAL (Architectural)
- **Bug #1**: Window._interactiveContents only contains top-level controls
- **Bug #2**: No IContainerControl interface to expose children ← ROOT CAUSE
- **Bug #7**: No recursive collection mechanism
- **Bug #11 (NEW)**: ColumnContainer doesn't focus children when receiving focus
- **Bug #12 (NEW)**: MenuControl doesn't focus children when receiving focus
- **Bug #13 (NEW)**: No IScrollableContainer interface for parent notification
- **Bug #15 (NEW)**: GetAllFocusableControlsFlattened must exclude containers from list

### HIGH Priority
- **Bug #3**: Missing IDirectionalFocusControl on ToolbarControl, MenuControl, ColumnContainer
- **Bug #4**: Invisible controls can receive Tab focus (no Visible check)
- **Bug #8**: BringIntoFocus uses wrong index after flattening
- **Bug #14 (NEW)**: ToolbarControl only focuses first item (ignores backward direction)
- **Bug #16 (NEW)**: HasActiveInteractiveContent inefficient after flattening

### MEDIUM Priority
- **Bug #5**: Mouse unfocus loop only checks top-level controls
- **Bug #6**: Ambiguous focus detection uses LastOrDefault()
- **Bug #9**: BringIntoFocus bounds may be incorrect for nested controls

### LOW Priority
- **Bug #10**: Sticky controls edge case in scroll calculation
- **Bug #17 (NEW)**: Dual-purpose controls (container + focusable) edge case

**Total Bugs**: 17 (10 original + 4 from deep analysis + 3 from plan review)

---

## Phase 1: Architectural Foundation

### Task #4: Create IContainerControl and IScrollableContainer Interfaces ⏳ IN PROGRESS
**Status**: In Progress
**Bugs Fixed**: #2, #13 (ROOT CAUSE)
**Files to Create**:
- `SharpConsoleUI/Controls/IContainerControl.cs`
- `SharpConsoleUI/Controls/IScrollableContainer.cs`

**Implementation**:

**IContainerControl.cs**:
```csharp
namespace SharpConsoleUI.Controls
{
    /// <summary>
    /// Interface for controls that contain child controls.
    /// All GUI frameworks expose children from containers - this is fundamental.
    /// Enables focus system to build flattened list of all focusable controls,
    /// including deeply nested controls within containers.
    /// </summary>
    public interface IContainerControl
    {
        /// <summary>
        /// Gets the direct child controls of this container.
        /// Does not recursively include grandchildren - recursion happens in caller.
        /// </summary>
        /// <returns>Read-only list of direct child controls (may include other containers)</returns>
        /// <remarks>
        /// IMPORTANT: For HorizontalGridControl, this should return columns AND splitters in order:
        /// [Column1, Splitter1, Column2, Splitter2, Column3]
        ///
        /// Splitters are IInteractiveControl and should be included in Tab navigation.
        /// </remarks>
        IReadOnlyList<IWindowControl> GetChildren();
    }
}
```

**IScrollableContainer.cs**:
```csharp
namespace SharpConsoleUI.Controls
{
    /// <summary>
    /// Interface for containers that can scroll to bring children into view.
    /// Used by BringIntoFocus to notify parent containers when nested child receives focus.
    /// </summary>
    public interface IScrollableContainer
    {
        /// <summary>
        /// Scrolls the container to bring the specified child control into view.
        /// Should also show/highlight scrollbars if applicable.
        /// </summary>
        /// <param name="child">The child control to bring into view (may be deeply nested)</param>
        /// <remarks>
        /// Implementation should use child.AbsoluteBounds to calculate position,
        /// which works correctly for deeply nested children (grandchildren, etc).
        /// </remarks>
        void ScrollChildIntoView(IWindowControl child);
    }
}
```

**Verification**:
- Both interfaces compile successfully
- XML documentation is clear and complete
- Noted HorizontalGrid separator behavior

---

### Task #5: Implement IContainerControl on 5 Containers
**Status**: Blocked by Task #4
**Dependencies**: Task #4
**Bugs Fixed**: #1 (partial), #7 (partial), #13 (partial)
**Files to Modify**:
- `SharpConsoleUI/Controls/ScrollablePanelControl.cs`
- `SharpConsoleUI/Controls/ColumnContainer.cs`
- `SharpConsoleUI/Controls/HorizontalGridControl.cs`
- `SharpConsoleUI/Controls/ToolbarControl.cs`
- `SharpConsoleUI/Controls/MenuControl.cs`

**Implementation for Each Container**:

**ScrollablePanelControl** - Add IScrollableContainer too:
```csharp
public class ScrollablePanelControl : ..., IContainerControl, IScrollableContainer
{
    public IReadOnlyList<IWindowControl> GetChildren()
    {
        return _content != null ? new[] { _content } : Array.Empty<IWindowControl>();
    }

    public void ScrollChildIntoView(IWindowControl child)
    {
        // Calculate child position using AbsoluteBounds (works for nested children)
        var childBounds = (child as IWindowControl)?.AbsoluteBounds ?? default;
        var ourBounds = this.AbsoluteBounds;

        // Calculate relative position within our scroll region
        int childRelativeY = childBounds.Y - ourBounds.Y + _scrollOffsetY;

        // Scroll vertically if child is outside visible region
        if (childRelativeY < _scrollOffsetY)
        {
            ScrollVerticalTo(childRelativeY);
        }
        else if (childRelativeY + childBounds.Height > _scrollOffsetY + _viewportHeight)
        {
            ScrollVerticalTo(childRelativeY + childBounds.Height - _viewportHeight);
        }

        // Similar for horizontal scrolling if supported
        // ...

        // Highlight scrollbars to indicate focused region
        _scrollbarsHighlighted = true;
        Container?.Invalidate(true);
    }
}
```

**ColumnContainer**:
```csharp
public class ColumnContainer : ..., IContainerControl
{
    public IReadOnlyList<IWindowControl> GetChildren()
    {
        return _controls.AsReadOnly(); // Direct access to _controls list
    }
}
```

**HorizontalGridControl** - Must include splitters!:
```csharp
public class HorizontalGridControl : ..., IContainerControl
{
    public IReadOnlyList<IWindowControl> GetChildren()
    {
        var children = new List<IWindowControl>();

        // Add columns and splitters in proper order
        for (int i = 0; i < _columns.Count; i++)
        {
            children.Add(_columns[i]);

            // Add splitter after this column if it exists
            var splitter = _splitters.FirstOrDefault(s => _splitterControls[s] == i);
            if (splitter != null)
            {
                children.Add(splitter);
            }
        }

        return children;
    }
}
```

**ToolbarControl**:
```csharp
public class ToolbarControl : ..., IContainerControl
{
    public IReadOnlyList<IWindowControl> GetChildren()
    {
        // Return toolbar items as controls
        return _items.Select(item => item.Control).Where(c => c != null).ToList();
    }
}
```

**MenuControl**:
```csharp
public class MenuControl : ..., IContainerControl
{
    public IReadOnlyList<IWindowControl> GetChildren()
    {
        return _menuItems.AsReadOnly(); // MenuItem controls
    }
}
```

**Verification**:
- Each container compiles successfully
- GetChildren() returns correct direct children
- HorizontalGridControl includes splitters in order
- ScrollablePanelControl.ScrollChildIntoView correctly calculates nested positions
- Run unit tests for each container's GetChildren() method

---

## Phase 2: Window Focus List Reconstruction

### Task #6: Add GetAllFocusableControlsFlattened to Window ⚠️ UPDATED
**Status**: Blocked by Task #5
**Dependencies**: Task #5
**Bugs Fixed**: #1, #7, #15 (CRITICAL UPDATE)
**File to Modify**: `SharpConsoleUI/Windows/Window.cs`

**CRITICAL ALGORITHM SPECIFICATION** (Bug #15 fix):

**GetAllFocusableControlsFlattened() MUST:**
1. Recursively traverse control tree using IContainerControl.GetChildren()
2. **EXCLUDE containers from result** - only add leaf controls
3. If control is IContainerControl: **SKIP IT**, recurse into children
4. If control is IFocusableControl/IInteractiveControl (and NOT IContainerControl): **ADD IT**
5. Return flattened list containing ONLY leaf controls (no containers)

**Rationale**: Containers delegate focus to children. Including both in Tab order causes double-focus.
Children naturally appear in correct Tab sequence without special handling.

**Implementation**:
```csharp
// Add to Window class
private List<IWindowControl>? _focusableControlsFlattened;
private bool _focusableControlsDirty = true;

/// <summary>
/// Gets all focusable controls in the window, flattened into a single list.
/// Recursively traverses containers to include nested controls.
///
/// IMPORTANT: Containers themselves are EXCLUDED from the list - only leaf controls
/// (controls that are focusable but not containers) are included. This prevents
/// double-focus when Tab lands on container that then focuses its child.
/// </summary>
/// <returns>Flattened list of all focusable leaf controls</returns>
private List<IWindowControl> GetAllFocusableControlsFlattened()
{
    // Return cached list if valid
    if (!_focusableControlsDirty && _focusableControlsFlattened != null)
        return _focusableControlsFlattened;

    var result = new List<IWindowControl>();

    void RecursiveAdd(IWindowControl control)
    {
        if (control is IContainerControl container)
        {
            // Container - SKIP IT, recurse into children
            foreach (var child in container.GetChildren())
            {
                RecursiveAdd(child);
            }
        }
        else if (control is IInteractiveControl || control is IFocusableControl)
        {
            // Leaf control - ADD IT
            result.Add(control);
        }
    }

    // Start with top-level controls
    foreach (var control in GetAllControls())
    {
        RecursiveAdd(control);
    }

    _focusableControlsFlattened = result;
    _focusableControlsDirty = false;

    return result;
}

/// <summary>
/// Invalidates the flattened focusable controls cache.
/// Call when controls are added/removed or when container structure changes.
/// </summary>
private void InvalidateFocusableControlsCache()
{
    _focusableControlsDirty = true;
}
```

**Cache Invalidation** (add to OnControlAddedToDOM/OnControlRemovedFromDOM):
```csharp
protected override void OnControlAddedToDOM(IWindowControl control)
{
    base.OnControlAddedToDOM(control);
    // ... existing code ...
    InvalidateFocusableControlsCache(); // NEW
}

protected override void OnControlRemovedFromDOM(IWindowControl control)
{
    base.OnControlRemovedFromDOM(control);
    // ... existing code ...
    InvalidateFocusableControlsCache(); // NEW
}
```

**Verification**:
- ✅ Flattened list contains ONLY leaf controls (no containers)
- ✅ Nested controls (ButtonA in ColumnContainer in ScrollablePanelControl) appear in list
- ✅ Containers themselves do NOT appear in list
- ✅ HorizontalGrid splitters ARE included (they're leaf controls)
- ✅ Cache invalidation works when controls added/removed
- ✅ Test with 3+ levels of nesting

**Tests to Add** (ContainerFlatteningTests.cs):
- `Test_ContainerNotInFlattenedList`: Verify ScrollablePanelControl with children is NOT in list
- `Test_OnlyChildrenInFlattenedList`: Verify only ButtonA, ButtonB appear (not container)
- `Test_EmptyContainerNotInList`: ColumnContainer with no children shouldn't be in list
- `Test_TripleNestedControlsFlattened`: ButtonA → ColumnContainer → ScrollablePanelControl → Window
- `Test_HorizontalGridSplittersIncluded`: Splitters ARE in flattened list

---

### Task #7: Replace _interactiveContents with Flattened List
**Status**: Blocked by Task #6
**Dependencies**: Task #6
**Bugs Fixed**: #1 (complete), #7 (complete)
**File to Modify**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**Implementation**:
```csharp
// In SwitchFocus method (lines 620-699):

// REPLACE:
// foreach (var control in _window._interactiveContents)

// WITH:
var focusableControls = _window.GetAllFocusableControlsFlattened();
foreach (var control in focusableControls)
{
    // ... rest of loop ...
}
```

**Verification**:
- Tab navigation works through nested controls
- Containers are NOT in Tab order (only their children)
- Focus flows naturally: Button1 → ButtonA_in_Panel → ButtonB_in_Panel → Button2

---

### Task #8: Add Visibility Check to Tab Navigation
**Status**: Blocked by Task #7
**Dependencies**: Task #7
**Bugs Fixed**: #4 (complete)
**File to Modify**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**Implementation**:
```csharp
// In SwitchFocus method, eligibility check (lines 659-670):

// ADD visibility check:
if (potentialNext.Visible &&  // NEW CHECK!
    potentialNext is IInteractiveControl interactive &&
    interactive.CanReceiveFocus(FocusReason.Tab))
{
    // ... focus the control ...
}
```

**Why not filter in GetAllFocusableControlsFlattened()?**
- Visibility can change between building cache and Tab press
- Checking at focus-time handles dynamic visibility correctly

**Verification**:
- Invisible controls are skipped during Tab navigation
- Setting control.Visible = false removes it from Tab sequence
- Test: Make ButtonA invisible, Tab skips from Button1 → ButtonB

---

## Phase 3: Container Focus Implementations (CRITICAL)

### Task #9: Implement IDirectionalFocusControl on ToolbarControl
**Status**: Blocked by Task #5
**Dependencies**: Task #5
**Bugs Fixed**: #3 (partial), #14 (complete)
**File to Modify**: `SharpConsoleUI/Controls/ToolbarControl.cs`

**Implementation** (similar to ScrollablePanelControl pattern):
```csharp
public class ToolbarControl : ..., IContainerControl, IDirectionalFocusControl
{
    private bool _focusFromBackward = false;
    private ToolbarItem? _focusedItem;

    // IDirectionalFocusControl implementation
    public void SetFocusWithDirection(bool focus, bool backward)
    {
        _focusFromBackward = backward;
        HasFocus = focus;
    }

    // Update HasFocus setter:
    public bool HasFocus
    {
        get => _hasFocus;
        set
        {
            bool hadFocus = _hasFocus;
            _hasFocus = value;

            if (value && !hadFocus)
            {
                // Focus gained - focus first or last item based on direction
                var focusableItems = GetFocusableItems().ToList();
                if (focusableItems.Count > 0)
                {
                    _focusedItem = _focusFromBackward
                        ? focusableItems.Last()   // Backward - focus last item
                        : focusableItems.First(); // Forward - focus first item
                    _focusFromBackward = false; // Reset after use

                    SetItemFocus(_focusedItem, true);
                }
            }
            else if (!value && hadFocus)
            {
                // Focus lost - unfocus current item
                if (_focusedItem != null)
                {
                    SetItemFocus(_focusedItem, false);
                }
            }

            Container?.Invalidate(true);
        }
    }
}
```

**Verification**:
- Tab into ToolbarControl focuses first item
- Shift+Tab into ToolbarControl focuses last item
- Test both directions explicitly

---

### Task #10: Implement IDirectionalFocusControl on MenuControl
**Status**: Blocked by Task #5
**Dependencies**: Task #5
**Bugs Fixed**: #3 (partial), #12 (complete)
**File to Modify**: `SharpConsoleUI/Controls/MenuControl.cs`

**Implementation** (similar pattern):
```csharp
public class MenuControl : ..., IContainerControl, IDirectionalFocusControl
{
    private bool _focusFromBackward = false;
    private MenuItem? _focusedMenuItem;

    // IDirectionalFocusControl implementation
    public void SetFocusWithDirection(bool focus, bool backward)
    {
        _focusFromBackward = backward;
        HasFocus = focus;
    }

    // Update HasFocus setter:
    public bool HasFocus
    {
        get => _hasFocus;
        set
        {
            bool hadFocus = _hasFocus;
            _hasFocus = value;

            if (value && !hadFocus)
            {
                // Focus gained - focus first or last menu item based on direction
                var focusableItems = _menuItems.Where(m => m.CanReceiveFocus).ToList();
                if (focusableItems.Count > 0)
                {
                    _focusedMenuItem = _focusFromBackward
                        ? focusableItems.Last()   // Backward - focus last item
                        : focusableItems.First(); // Forward - focus first item
                    _focusFromBackward = false; // Reset after use

                    SetMenuItemFocus(_focusedMenuItem, true);
                }
            }
            else if (!value && hadFocus)
            {
                // Focus lost - unfocus current item
                if (_focusedMenuItem != null)
                {
                    SetMenuItemFocus(_focusedMenuItem, false);
                }
            }

            Container?.Invalidate(true);
        }
    }
}
```

**Verification**:
- Tab into MenuControl focuses first menu item
- Shift+Tab into MenuControl focuses last menu item

**⚠️ CLARIFICATION NEEDED**: User mentioned "when we focus menu with back direction, I think that the logic says to focus the first top menu item!" - Is MenuControl special? Should backward also focus first item (not last)? **ASSUMING STANDARD BEHAVIOR** for now (backward → last item).

---

### Task #11: Implement IDirectionalFocusControl on ColumnContainer
**Status**: Blocked by Task #5
**Dependencies**: Task #5
**Bugs Fixed**: #3 (complete), #11 (complete)
**File to Modify**: `SharpConsoleUI/Controls/ColumnContainer.cs`

**Implementation** (similar pattern):
```csharp
public class ColumnContainer : ..., IContainerControl, IDirectionalFocusControl
{
    private bool _focusFromBackward = false;
    private IWindowControl? _focusedChild;

    // IDirectionalFocusControl implementation
    public void SetFocusWithDirection(bool focus, bool backward)
    {
        _focusFromBackward = backward;
        SetFocus(focus);
    }

    // Update SetFocus implementation:
    public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
    {
        bool hadFocus = HasFocus;
        HasFocus = focus;

        if (focus && !hadFocus)
        {
            // Focus gained - focus first or last child based on direction
            var focusableChildren = _controls
                .Where(c => c is IFocusableControl fc && fc.CanReceiveFocus)
                .ToList();

            if (focusableChildren.Count > 0)
            {
                _focusedChild = _focusFromBackward
                    ? focusableChildren.Last()
                    : focusableChildren.First();
                _focusFromBackward = false; // Reset after use

                if (_focusedChild is IFocusableControl focusable)
                {
                    focusable.SetFocus(true, reason);
                }
            }
        }
        else if (!focus && hadFocus)
        {
            // Focus lost - unfocus focused child
            if (_focusedChild is IFocusableControl focusable)
            {
                focusable.SetFocus(false, reason);
            }
        }

        if (hadFocus != focus)
        {
            this.NotifyParentWindowOfFocusChange(focus);
        }
    }
}
```

**Verification**:
- Tab into ColumnContainer focuses first child
- Shift+Tab into ColumnContainer focuses last child
- Container correctly tracks _focusedChild

---

## Phase 4: BringIntoFocus Fixes

### Task #12: Fix BringIntoFocus Signature and Parent Chain Walking
**Status**: Blocked by Task #5
**Dependencies**: Task #5
**Bugs Fixed**: #8 (complete), #13 (complete)
**File to Modify**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**Implementation**:
```csharp
// REPLACE BringIntoFocus method (lines 704-743):

/// <summary>
/// Brings a control into view by scrolling the window if needed.
/// Also notifies parent containers via IScrollableContainer to scroll nested children into view.
/// </summary>
/// <param name="control">The control to bring into focus</param>
/// <param name="backward">Whether focus is moving backward (affects scroll positioning)</param>
private void BringIntoFocus(IWindowControl control, bool backward = false)
{
    if (control == null) return;

    // Walk up the parent chain and notify all IScrollableContainer parents
    var current = control;
    while (current != null)
    {
        if (current.Container is IScrollableContainer scrollableContainer)
        {
            scrollableContainer.ScrollChildIntoView(control);
        }

        // Move to parent control
        current = current.Container as IWindowControl;
    }

    // Original window-level scrolling logic (if window is scrollable)
    // ... existing window scroll code ...
}
```

**Verification**:
- Focusing ButtonA inside ScrollablePanelControl scrolls panel to show ButtonA
- Focusing ButtonA inside ColumnContainer inside ScrollablePanelControl scrolls panel correctly
- ScrollChildIntoView is called on all IScrollableContainer parents in chain
- Scrollbars are highlighted when child receives focus

---

### Task #13: Verify BringIntoFocus Bounds for Nested Controls
**Status**: Blocked by Task #12
**Dependencies**: Task #12
**Bugs Fixed**: #9 (verification)
**Test File**: Add to `FocusManagement/NestedControlTests.cs`

**Tests to Add**:
```csharp
[Fact]
public void NestedControl_AbsoluteBounds_IncludesAllParentOffsets()
{
    // ButtonA at (2,2) in ColumnContainer at (5,5) in ScrollablePanelControl at (10,10)
    // Expected AbsoluteBounds: (17, 17, ...)
    var system = CreateTestSystem();
    var panel = new ScrollablePanelControl { Left = 10, Top = 10 };
    var column = new ColumnContainer { /* positioned at 5,5 relative to panel */ };
    var button = new ButtonControl { /* positioned at 2,2 relative to column */ };

    column.AddContent(button);
    panel.SetContent(column);

    system.Render.UpdateDisplay(); // Trigger layout

    // Verify AbsoluteBounds includes all parent offsets
    var bounds = button.AbsoluteBounds;
    Assert.Equal(17, bounds.X);
    Assert.Equal(17, bounds.Y);
}

[Fact]
public void ScrollChildIntoView_Works_ForGrandchildren()
{
    // Test that ScrollablePanelControl.ScrollChildIntoView correctly scrolls
    // to show a deeply nested child (grandchild, great-grandchild)
}
```

**Verification**:
- AbsoluteBounds are correct for nested controls (3+ levels)
- ScrollChildIntoView works for grandchildren
- No need for code changes if tests pass

---

## Phase 5: Mouse and Misc Fixes

### Task #14: Fix Mouse Unfocus - Use FocusStateService ⚠️ REVISED
**Status**: Blocked by Task #7
**Dependencies**: Task #7
**Bugs Fixed**: #5 (complete)
**File to Modify**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**REVISED IMPLEMENTATION** (simpler, no loop needed):
```csharp
// In HandleClickFocus method (~line 318):

// REPLACE:
// foreach (var control in _window._interactiveContents)
// {
//     if (control.HasFocus)
//     {
//         FocusStateService.SetFocus(_window, null);
//         break;
//     }
// }

// WITH:
if (FocusStateService.GetFocusedControl(_window) != null)
{
    FocusStateService.SetFocus(_window, null);
}
```

**Rationale**: No need to loop through list - just check if ANY control has focus via FocusStateService.

**Verification**:
- Click on empty space clears focus (even from nested controls)
- No performance impact (single service call vs list traversal)

---

### Task #15: Fix HasActiveInteractiveContent - Use FocusStateService ⚠️ REVISED
**Status**: Blocked by Task #7
**Dependencies**: Task #7
**Bugs Fixed**: #6 (complete), #16 (complete)
**File to Modify**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`

**REVISED IMPLEMENTATION** (use FocusStateService, not Any()):
```csharp
// REPLACE (line 566):
// public bool HasActiveInteractiveContent =>
//     _window._interactiveContents.LastOrDefault(c => c.HasFocus) != null;

// WITH:
public bool HasActiveInteractiveContent =>
    FocusStateService.GetFocusedControl(_window) != null;
```

**Rationale**:
- LastOrDefault → FirstOrDefault is arbitrary (checking existence)
- Better: Use FocusStateService.GetFocusedControl (no list rebuild, clearer intent)
- Avoids inefficiency of rebuilding flattened list every property access

**Verification**:
- Property returns true when any control has focus
- Property returns false when no control has focus
- No performance impact (single service call)

---

## Phase 6: Test Suite Implementation

### Task #16: Create Phase 6 Focus Management Test Suite
**Status**: Blocked by all previous tasks
**Dependencies**: Tasks #4-15
**Bugs Verified**: All 17 bugs
**Test Files to Create** (10 files, ~105 tests):

**Test Structure**:
```
SharpConsoleUI.Tests/FocusManagement/
├── KeyboardFocusTests.cs              (~18 tests)
├── TabNavigationTests.cs              (~25 tests) - MOST IMPORTANT
├── ContainerFocusTests.cs             (~20 tests) - 5 containers × 4 tests each
├── NestedControlTests.cs              (~12 tests) - NEW: flattening, nesting
├── MouseFocusTests.cs                 (~12 tests)
├── FocusEventsTests.cs                (~10 tests)
├── VisibilityFilteringTests.cs        (~5 tests)
└── ContainerFlatteningTests.cs        (~5 tests) - NEW: Bug #15 verification

Total: ~105 tests (revised from 128 - removed WindowSwitchingTests which is Phase 5)
```

**KeyboardFocusTests.cs** (~18 tests):
- `Test_SetFocus_ControlBecomesFocused`
- `Test_SetFocus_PreviousControlLosesFocus`
- `Test_CanReceiveFocus_False_PreventsTabFocus`
- `Test_InvisibleControl_SkippedByTab` (Bug #4)
- `Test_DisabledControl_SkippedByTab`
- `Test_FocusStateService_TracksFocusedControl`
- `Test_FocusEvents_GotFocus_Fired`
- `Test_FocusEvents_LostFocus_Fired`
- And more...

**TabNavigationTests.cs** (~25 tests) - MOST CRITICAL:
- `Test_Tab_MovesBetweenControls`
- `Test_ShiftTab_MovesBackward`
- `Test_Tab_EntersContainer_FocusesFirstChild` (Bug #11, #12)
- `Test_ShiftTab_EntersContainer_FocusesLastChild` (Bug #3, #14)
- `Test_Tab_ThroughNestedControls_SkipsContainers` (Bug #15 - CRITICAL)
- `Test_Tab_WithinHorizontalGrid_IncludesSplitters`
- `Test_Tab_WrapAround_AtEnd`
- `Test_Tab_SkipsDisabledControls`
- `Test_Tab_SkipsInvisibleControls` (Bug #4)
- `Test_Tab_FromButton1_ToButtonA_InPanel` (nested)
- `Test_Tab_FromButtonB_InPanel_ToButton2` (exit container)
- `Test_ShiftTab_FromButtonA_InPanel_ToButton1` (exit backward)
- `Test_TripleNested_TabSequence_Correct`
- And more...

**ContainerFocusTests.cs** (~20 tests) - Test all 5 containers:
- ScrollablePanelControl (4 tests): forward/backward focus, scroll on focus
- ColumnContainer (4 tests): forward/backward focus, empty container
- HorizontalGridControl (4 tests): forward/backward focus, splitter handling
- ToolbarControl (4 tests): forward/backward focus (Bug #14 fix)
- MenuControl (4 tests): forward/backward focus (Bug #12 fix)

**NestedControlTests.cs** (~12 tests) - NEW FILE:
- `Test_DoubleNested_ButtonInColumnInPanel_InTabOrder`
- `Test_TripleNested_AbsoluteBoundsCorrect` (Bug #9)
- `Test_ScrollChildIntoView_WorksForGrandchildren` (Bug #13)
- `Test_ContainerHasFocus_WhenChildHasFocus`
- `Test_ContainerLosesFocus_WhenAllChildrenLoseFocus`
- `Test_FocusChain_NotifiesAllParents`
- And more...

**ContainerFlatteningTests.cs** (~5 tests) - NEW FILE for Bug #15:
- `Test_ContainerNotInFlattenedList` - CRITICAL
- `Test_OnlyChildrenInFlattenedList` - CRITICAL
- `Test_EmptyContainerNotFocusable`
- `Test_TripleNestedControlsFlattened`
- `Test_HorizontalGridSplittersIncluded`

**MouseFocusTests.cs** (~12 tests):
- `Test_ClickControl_ReceivesFocus`
- `Test_ClickEmptySpace_ClearsFocus` (Bug #5)
- `Test_ClickNestedControl_ReceivesFocus` (Bug #5)
- And more...

**FocusEventsTests.cs** (~10 tests):
- `Test_GotFocus_Propagates`
- `Test_LostFocus_Propagates`
- `Test_FocusEvents_Cancellable`
- And more...

**VisibilityFilteringTests.cs** (~5 tests):
- `Test_InvisibleControl_NotInTabOrder` (Bug #4)
- `Test_ControlBecomeVisible_AddedToTabOrder`
- `Test_ControlBecomeInvisible_RemovedFromTabOrder`
- And more...

**Verification**:
- All 17 bugs have corresponding test coverage
- Tests use MockConsoleDriver for isolation
- Tests verify both behavior AND performance (e.g., zero redundant focus changes)

---

### Task #17: Run All Tests and Verify 378 Passing
**Status**: Blocked by Task #16
**Dependencies**: Task #16
**Expected Result**: 378 tests passing (273 current + 105 new)

**Command**:
```bash
dotnet test SharpConsoleUI.Tests/SharpConsoleUI.Tests.csproj --verbosity normal
```

**Success Criteria**:
- ✅ All 273 existing tests still pass (no regressions)
- ✅ All 105 new focus tests pass
- ✅ Total: 378 tests passing
- ✅ 0 tests failing
- ✅ 0 tests skipped

**If Tests Fail**:
- Identify which bug fix failed
- Review implementation vs test expectations
- Fix implementation or test (not both!)
- Re-run tests until all pass

---

### Task #18: Commit Phase 6 Focus System Fixes
**Status**: Blocked by Task #17
**Dependencies**: Task #17
**Action**: Git commit with comprehensive message

**Commit Message**:
```
feat: Phase 6 - Fix focus system (17 bugs fixed, 105 tests added)

Comprehensive focus system overhaul addressing architectural issues,
container focus bugs, and keyboard/mouse navigation problems.

ARCHITECTURAL CHANGES:
- Added IContainerControl interface (exposes children for recursive traversal)
- Added IScrollableContainer interface (parent notification for BringIntoFocus)
- Implemented GetAllFocusableControlsFlattened() with proper container exclusion
- Cache invalidation when controls added/removed

CONTAINER FIXES (3 BROKEN, 2 INCONSISTENT):
- ColumnContainer: Added IDirectionalFocusControl, now focuses children (Bug #11)
- MenuControl: Added IDirectionalFocusControl, now focuses children (Bug #12)
- ToolbarControl: Added IDirectionalFocusControl, backward focus fixed (Bug #14)
- ScrollablePanelControl: Added ScrollChildIntoView for parent notification (Bug #13)
- HorizontalGridControl: GetChildren includes splitters in proper order

CRITICAL FIXES:
- Bug #15: GetAllFocusableControlsFlattened excludes containers (prevents double-focus)
- Bug #4: Invisible controls skipped in Tab navigation
- Bug #5: Mouse unfocus works for nested controls (uses FocusStateService)
- Bug #16: HasActiveInteractiveContent optimized (uses FocusStateService)

BUGS FIXED (17 total):
- #1: _interactiveContents replaced with flattened list (recursive)
- #2: IContainerControl interface created (ROOT CAUSE)
- #3: IDirectionalFocusControl on 3 containers (ToolbarControl, MenuControl, ColumnContainer)
- #4: Invisible controls filtered from Tab navigation
- #5: Mouse unfocus loop fixed for nested controls
- #6: HasActiveInteractiveContent uses FocusStateService (clearer)
- #7: Recursive collection via IContainerControl
- #8: BringIntoFocus uses control parameter (not index)
- #9: AbsoluteBounds verified for nested controls
- #10: Sticky controls edge case (tested)
- #11: ColumnContainer focuses children (CRITICAL)
- #12: MenuControl focuses children (CRITICAL)
- #13: IScrollableContainer for parent notification
- #14: ToolbarControl backward focus (CRITICAL)
- #15: Flattened list excludes containers (CRITICAL)
- #16: HasActiveInteractiveContent optimized
- #17: Dual-purpose controls edge case (documented)

TEST COVERAGE (105 new tests):
- KeyboardFocusTests.cs (18 tests)
- TabNavigationTests.cs (25 tests) - most critical
- ContainerFocusTests.cs (20 tests) - 5 containers
- NestedControlTests.cs (12 tests)
- MouseFocusTests.cs (12 tests)
- FocusEventsTests.cs (10 tests)
- VisibilityFilteringTests.cs (5 tests)
- ContainerFlatteningTests.cs (5 tests)

RESULTS:
- ✅ 378/378 tests passing (273 existing + 105 new)
- ✅ Zero regressions
- ✅ All 17 bugs verified fixed

FILES MODIFIED (8):
- SharpConsoleUI/Controls/IContainerControl.cs (NEW)
- SharpConsoleUI/Controls/IScrollableContainer.cs (NEW)
- SharpConsoleUI/Controls/ScrollablePanelControl.cs (+ScrollChildIntoView)
- SharpConsoleUI/Controls/ColumnContainer.cs (+IContainerControl, +IDirectionalFocusControl)
- SharpConsoleUI/Controls/HorizontalGridControl.cs (+IContainerControl)
- SharpConsoleUI/Controls/ToolbarControl.cs (+IContainerControl, +IDirectionalFocusControl)
- SharpConsoleUI/Controls/MenuControl.cs (+IContainerControl, +IDirectionalFocusControl)
- SharpConsoleUI/Windows/Window.cs (+GetAllFocusableControlsFlattened, +cache invalidation)
- SharpConsoleUI/Windows/WindowEventDispatcher.cs (SwitchFocus, BringIntoFocus, mouse unfocus)

TEST FILES ADDED (8):
- SharpConsoleUI.Tests/FocusManagement/KeyboardFocusTests.cs
- SharpConsoleUI.Tests/FocusManagement/TabNavigationTests.cs
- SharpConsoleUI.Tests/FocusManagement/ContainerFocusTests.cs
- SharpConsoleUI.Tests/FocusManagement/NestedControlTests.cs
- SharpConsoleUI.Tests/FocusManagement/MouseFocusTests.cs
- SharpConsoleUI.Tests/FocusManagement/FocusEventsTests.cs
- SharpConsoleUI.Tests/FocusManagement/VisibilityFilteringTests.cs
- SharpConsoleUI.Tests/FocusManagement/ContainerFlatteningTests.cs

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
```

---

## ⚠️ OPEN QUESTIONS / CLARIFICATIONS NEEDED

### Question #1: MenuControl Backward Focus Behavior
**Context**: User said "when we focus menu with back direction, I think that the logic says to focus the first top menu item!"

**Standard behavior**:
- Forward (Tab): Focus first child
- Backward (Shift+Tab): Focus **last** child

**User suggests**: Backward should also focus first child (not last)?

**Current Plan**: Assumes STANDARD behavior (backward → last item)

**Action Needed**: Clarify if MenuControl should have special backward behavior

---

### Question #2: Flattened List Caching Strategy
**Context**: Current `_interactiveContents` is cached, updated in OnControlAddedToDOM/OnControlRemovedFromDOM

**Options**:
- **Cached** (Task #6 implementation): Better performance, need invalidation logic
- **Computed on-demand**: Always up-to-date, performance cost of recursive traversal

**Current Plan**: Uses CACHED approach (matching existing pattern)

**Action Needed**: Confirm caching strategy is acceptable

---

### Question #3: Complex Control Structures (TreeControl, ListControl)
**Context**: Do these controls have IWindowControl children, or internal data structures?

**Impact**:
- If TreeNodes/ListItems are IWindowControl → implement IContainerControl
- If internal data → controls handle focus internally, don't implement IContainerControl

**Current Plan**: Assumes internal data structures (controls handle focus internally)

**Action Needed**: Investigate TreeControl, ListControl, and other complex controls

---

## Summary

**Total Bugs**: 17 (10 original + 4 from deep analysis + 3 from plan review)
**Total Tasks**: 15 (sequential + parallel workflow)
**Total Tests**: ~105 new tests (10 test files)
**Expected Result**: 378 tests passing (273 current + 105 new)

**Critical Path**:
1. Task #4 (interfaces) → Task #5 (implement on containers)
2. Task #5 → Task #6 (flattened list) → Task #7 (use in SwitchFocus) → Task #8 (visibility)
3. Task #5 → Tasks #9, #10, #11 (IDirectionalFocusControl - parallel)
4. Task #5 → Task #12 (BringIntoFocus) → Task #13 (verify bounds)
5. Task #7 → Tasks #14, #15 (mouse + misc - parallel)
6. All → Task #16 (tests) → Task #17 (verify) → Task #18 (commit)

**Estimated Timeline**: 2-3 days for implementation + 1 day for comprehensive testing

---

## Progress Tracking

- [x] Task #4: Create interfaces (IN PROGRESS - RESUME HERE)
- [ ] Task #5: Implement on 5 containers
- [ ] Task #6: Add GetAllFocusableControlsFlattened
- [ ] Task #7: Replace _interactiveContents
- [ ] Task #8: Add visibility check
- [ ] Task #9: IDirectionalFocusControl on ToolbarControl
- [ ] Task #10: IDirectionalFocusControl on MenuControl
- [ ] Task #11: IDirectionalFocusControl on ColumnContainer
- [ ] Task #12: Fix BringIntoFocus
- [ ] Task #13: Verify bounds for nested controls
- [ ] Task #14: Fix mouse unfocus
- [ ] Task #15: Fix HasActiveInteractiveContent
- [ ] Task #16: Create test suite (105 tests)
- [ ] Task #17: Run all tests (378 expected)
- [ ] Task #18: Commit

**Current Status**: Ready to resume Task #4 (create interfaces) after plan review complete.

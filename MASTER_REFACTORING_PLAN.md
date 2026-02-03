# SharpConsoleUI Master Refactoring Plan

## Overview

**Vision**: Transform SharpConsoleUI from a monolithic architecture into a clean, maintainable, testable codebase while maintaining 100% backward compatibility.

**Progress**: Phase 1 & 2 Complete ✅ (ConsoleWindowSystem refactoring)

---

## Architecture Goals

### Current State
- **ConsoleWindowSystem**: 1,743 lines (was 1,977, originally 2,400)
- **Window**: ~2,800 lines (needs refactoring)
- **Multiple god objects** with mixed responsibilities
- **Tight coupling** between system components
- **Limited testability** due to monolithic design

### Target State
- **ConsoleWindowSystem**: < 1,000 lines (core orchestration only)
- **Window**: < 1,500 lines (clean content management)
- **Single Responsibility** for all classes
- **Loosely coupled** via interfaces
- **Highly testable** with mockable components
- **100% backward compatible** public APIs

---

## Phase 1: ConsoleWindowSystem Refactoring ✅ COMPLETE

Extract specialized coordinator classes to handle distinct responsibilities.

### Phase 1.1: InputCoordinator ✅ COMPLETE
- **Status**: ✅ Committed (5b2c7c3)
- **Extracted**: ~300 lines → `SharpConsoleUI/Input/InputCoordinator.cs` (350 lines)
- **Responsibilities**: Mouse events, keyboard input, window clicks, drag/resize
- **Impact**: ConsoleWindowSystem reduced by ~300 lines

### Phase 1.2: RenderCoordinator ✅ COMPLETE
- **Status**: ✅ Committed (5b2c7c3)
- **Extracted**: ~420 lines → `SharpConsoleUI/Rendering/RenderCoordinator.cs` (617 lines)
- **Responsibilities**: Window rendering, status bars, cache management, dirty tracking
- **Impact**: ConsoleWindowSystem reduced by ~420 lines

### Phase 1.3: PerformanceTracker ✅ COMPLETE
- **Status**: ✅ Committed (ba4e8e5)
- **Extracted**: ~100 lines → `SharpConsoleUI/Performance/PerformanceTracker.cs` (156 lines)
- **Responsibilities**: Frame timing, FPS calculation, metrics tracking
- **Impact**: ConsoleWindowSystem reduced by ~100 lines, RenderCoordinator reduced by 68 lines

### Phase 1.4: StartMenuCoordinator ✅ COMPLETE
- **Status**: ✅ Committed (9cd22ee)
- **Extracted**: ~20 lines → `SharpConsoleUI/StartMenu/StartMenuCoordinator.cs` (70 lines)
- **Responsibilities**: Start menu action registration, menu display
- **Impact**: Clean separation of menu concerns

**Phase 1 Total**: Extracted ~840 lines into 4 specialized coordinators

---

## Phase 2: Window Lifecycle Management ✅ COMPLETE

Extract window creation, activation, closing, and state management logic from ConsoleWindowSystem.

### Phase 2.1: WindowLifecycleManager ✅ COMPLETE
- **Status**: ✅ Committed (13e8454)
- **Extracted**: 152 lines → `SharpConsoleUI/Windows/WindowLifecycleManager.cs` (301 lines)
- **Target File**: `SharpConsoleUI/Windows/WindowLifecycleManager.cs`
- **Responsibilities**:
  - Window creation and initialization
  - Window activation/deactivation
  - Window closing logic (with OnClosing cancellation)
  - Window state transitions (minimize, maximize, restore)
  - Window flashing
  - Parent-child window relationships

**Methods to Extract**:
```csharp
- AddWindow(Window window)
- RemoveWindow(Window window)
- CloseWindow(Window window, bool activateParent = true)
- ActivateNextNonMinimizedWindow(Window minimizedWindow)
- FlashWindow(Window window)
- MinimizeWindow(Window window)
- MaximizeWindow(Window window)
- RestoreWindow(Window window)
```

### Phase 2.2: WindowPositioningManager ✅ COMPLETE
- **Status**: ✅ Committed (f27a5a8)
- **Extracted**: 82 lines → `SharpConsoleUI/Windows/WindowPositioningManager.cs` (264 lines)
- **Target File**: `SharpConsoleUI/Windows/WindowPositioningManager.cs`
- **Responsibilities**:
  - Window move operations (MoveWindowTo, MoveWindowBy)
  - Window resize operations (ResizeWindowTo, ResizeWindowBy)
  - Keyboard-based movement/resize (MoveOrResizeOperation)
  - Position clamping and validation
  - Desktop bounds enforcement

**Methods to Extract**:
```csharp
- MoveWindowTo(Window window, int newLeft, int newTop)
- MoveWindowBy(Window window, int deltaX, int deltaY)
- ResizeWindowTo(Window window, int newLeft, int newTop, int newWidth, int newHeight)
- ResizeWindowBy(Window window, int deltaWidth, int deltaHeight)
- MoveOrResizeOperation(Window window, WindowTopologyAction action, Direction direction)
```

**Phase 2 Total**: Extracted 234 lines into 2 window management classes (11.8% reduction)

---

## Phase 3: Window Class Refactoring 🔮 FUTURE

Break down the Window god object (~2,800 lines) into maintainable components.

### Current Window.cs Analysis
| Category | Lines | Purpose |
|----------|-------|---------|
| Properties & Fields | ~400 | Window state, dimensions, colors, theme |
| Control Management | ~300 | Add/remove controls, control tree |
| Event Handling | ~400 | Mouse, keyboard, focus, resize events |
| Rendering | ~800 | DOM building, measure/arrange/paint, ANSI serialization |
| Border Management | ~200 | Border rendering, caching, hit testing |
| Scrolling | ~200 | Scroll offsets, viewport, scroll bars |
| Lifecycle | ~200 | Initialize, dispose, close, show/hide |
| Helpers | ~300 | Utility methods, coordinate conversion |

**Total**: ~2,800 lines

### Phase 3.1: Extract WindowContentManager
- **Estimate**: ~300 lines
- **Target File**: `SharpConsoleUI/Windows/WindowContentManager.cs`
- **Responsibilities**:
  - Control collection management
  - Control tree building
  - Control invalidation
  - Content region calculation

**Interface**:
```csharp
public class WindowContentManager
{
    public void AddControl(IWindowControl control);
    public void RemoveControl(IWindowControl control);
    public void ClearControls();
    public IReadOnlyList<IWindowControl> GetControls();
    public Rectangle GetContentBounds();
    public void InvalidateControl(IWindowControl control);
}
```

### Phase 3.2: Extract WindowRenderer
- **Estimate**: ~800 lines
- **Target File**: `SharpConsoleUI/Windows/WindowRenderer.cs`
- **Responsibilities**:
  - DOM tree building (RebuildLayoutTree)
  - Three-stage layout (Measure, Arrange, Paint)
  - CharacterBuffer management
  - ANSI serialization
  - Visible region clipping

**Interface**:
```csharp
public class WindowRenderer
{
    public void BuildLayoutTree(IReadOnlyList<IWindowControl> controls);
    public void PerformLayout(int contentWidth, int contentHeight);
    public List<(int X, int Y, string Content)> RenderToLines(List<LayoutRect> visibleRegions);
    public void InvalidateLayout();
}
```

### Phase 3.3: Extract BorderRenderer
- **Estimate**: ~200 lines
- **Target File**: `SharpConsoleUI/Windows/BorderRenderer.cs`
- **Responsibilities**:
  - Border string caching
  - Border rendering
  - Border hit testing
  - Title rendering

**Interface**:
```csharp
public class BorderRenderer
{
    public void BuildBorderCache(int width, int height, string title, bool isActive);
    public List<string> GetBorderLines();
    public bool IsPointOnBorder(int x, int y, Rectangle bounds);
    public BorderHitTestResult HitTest(int x, int y, Rectangle bounds);
}
```

### Phase 3.4: Extract ScrollManager
- **Estimate**: ~200 lines
- **Target File**: `SharpConsoleUI/Windows/ScrollManager.cs`
- **Responsibilities**:
  - Scroll offset tracking
  - Scroll bounds calculation
  - Scroll bar rendering
  - Wheel/key scroll handling

**Interface**:
```csharp
public class ScrollManager
{
    public int ScrollOffsetX { get; set; }
    public int ScrollOffsetY { get; set; }
    public bool CanScrollVertically { get; }
    public bool CanScrollHorizontally { get; }
    public void ScrollBy(int deltaX, int deltaY);
    public void ScrollTo(int x, int y);
    public void EnsureVisible(Rectangle rect);
}
```

### Phase 3.5: Extract WindowEventDispatcher
- **Estimate**: ~400 lines
- **Target File**: `SharpConsoleUI/Windows/WindowEventDispatcher.cs`
- **Responsibilities**:
  - Mouse event routing
  - Keyboard event routing
  - Focus management
  - Event bubbling/capture

**Interface**:
```csharp
public class WindowEventDispatcher
{
    public bool HandleMouseEvent(MouseEventArgs e);
    public bool HandleKeyboardEvent(KeyboardEventArgs e);
    public void SetFocusedControl(IFocusableControl control);
    public IFocusableControl? GetFocusedControl();
}
```

**Phase 3 Total**: Extract ~1,900 lines, leaving Window as a thin coordinator (~900 lines)

---

## Phase 4: ConsoleWindowSystem Simplification ⏳ FUTURE

After Phase 2, ConsoleWindowSystem should be down to ~1,400 lines. Further simplification:

### Phase 4.1: Property Cleanup
- **Action**: Group related properties into configuration objects
- **Example**: Desktop bounds properties → DesktopBounds record
- **Impact**: Reduce property count, improve cohesion

### Phase 4.2: Service Exposure Cleanup
- **Action**: Create ServiceRegistry or ServiceProvider pattern
- **Current**: Individual properties for each service (WindowStateService, FocusStateService, etc.)
- **Proposed**: Single Services property with typed access
- **Impact**: Reduce surface area, cleaner API

### Phase 4.3: Method Delegation Review
- **Action**: Review all remaining public methods
- **Goal**: Ensure each method is in the right place
- **Consider**: Some window control methods might belong in Window class itself

---

## Phase 5: State Service Consolidation (Optional) 🔮 LONG-TERM

Currently have 11+ state services. Consider consolidation where appropriate.

### Potential Consolidations:
1. **SelectionStateService** + **EditStateService** → EditingStateService
2. **ScrollStateService** + **LayoutStateService** → ViewportStateService
3. **CursorStateService** + **InputStateService** → InputStateService (enhanced)

**Caution**: Only do this if it genuinely improves clarity. Current separation may be intentional.

---

## Phase 6: Testing Infrastructure 🔮 LONG-TERM

### Phase 6.1: Test Harness
- Create mock implementations of core interfaces
- Build test window system without real console
- Enable automated testing of window management

### Phase 6.2: Integration Tests
- Test window lifecycle operations
- Test rendering pipeline
- Test input handling
- Test state management

### Phase 6.3: Performance Benchmarks
- Frame time benchmarks
- Memory allocation tracking
- Rendering efficiency tests

---

## Success Metrics

### Quantitative Goals:
- ✅ **Phase 1**: ConsoleWindowSystem < 2,000 lines (Was: 1,977)
- ✅ **Phase 2**: ConsoleWindowSystem < 1,800 lines (Currently: 1,743)
- ⏳ **Phase 3**: Window < 1,500 lines
- ⏳ **Phase 4**: ConsoleWindowSystem < 1,000 lines
- ⏳ **Final**: Core classes ~800 lines each (orchestration only)

### Qualitative Goals:
- ✅ Single Responsibility Principle for all extracted classes
- ✅ 100% backward compatibility maintained
- ✅ Zero functional changes during refactoring
- ✅ All tests pass after each phase
- ⏳ Improved testability (can mock coordinators)
- ⏳ Reduced cognitive load (easier to understand each piece)
- ⏳ Better performance (optimized components)

---

## Current Architecture

```
ConsoleWindowSystem (1,743 lines)
├── InputCoordinator (350 lines)         ✅ Phase 1.1
├── RenderCoordinator (617 lines)        ✅ Phase 1.2
├── PerformanceTracker (156 lines)       ✅ Phase 1.3
├── StartMenuCoordinator (70 lines)      ✅ Phase 1.4
├── WindowLifecycleManager (301 lines)   ✅ Phase 2.1
├── WindowPositioningManager (264 lines) ✅ Phase 2.2
├── WindowStateService                  [Existing]
├── FocusStateService                   [Existing]
├── ModalStateService                   [Existing]
├── NotificationStateService            [Existing]
├── ThemeStateService                   [Existing]
├── PluginStateService                  [Existing]
├── CursorStateService                  [Existing]
├── InputStateService                   [Existing]
└── ... (other state services)          [Existing]

Window (2,800 lines)                    [To be refactored Phase 3]
├── Properties & Fields (~400)
├── Control Management (~300)
├── Event Handling (~400)
├── Rendering (~800)
├── Border Management (~200)
├── Scrolling (~200)
├── Lifecycle (~200)
└── Helpers (~300)
```

## Target Architecture (After Phase 2) ✅ ACHIEVED

```
ConsoleWindowSystem (1,743 lines)
├── InputCoordinator                    ✅ Input handling
├── RenderCoordinator                   ✅ Display & rendering
├── PerformanceTracker                  ✅ Metrics
├── StartMenuCoordinator                ✅ Start menu
├── WindowLifecycleManager              ✅ Window add/close/flash
├── WindowPositioningManager            ✅ Window move/resize
└── State Services                      [Existing]

## Target Architecture (After Phase 3) ⏳ FUTURE

```
ConsoleWindowSystem (~1,000 lines)
├── All Phase 1 & 2 Coordinators        ✅ Complete
└── State Services                      [Existing]

Window (~900 lines)
├── WindowContentManager                ⏳ Phase 3.1
├── WindowRenderer                      ⏳ Phase 3.2
├── BorderRenderer                      ⏳ Phase 3.3
├── ScrollManager                       ⏳ Phase 3.4
└── WindowEventDispatcher               ⏳ Phase 3.5
```

---

## Implementation Guidelines

### For Each Phase:

1. **Analyze**: Identify cohesive responsibility to extract
2. **Create**: New coordinator/manager class with clear interface
3. **Extract**: Move fields, methods, and logic to new class
4. **Delegate**: Parent class delegates to coordinator
5. **Test**: Verify all functionality works identically
6. **Commit**: Atomic commit with clear description
7. **Verify**: Run full test suite and example apps

### Principles:

- **No Functional Changes**: Pure refactoring only
- **Backward Compatibility**: All public APIs remain unchanged
- **Interface Separation**: Use interfaces to break circular dependencies
- **Lazy Initialization**: Use `Func<T>` for circular dependency resolution
- **Single Responsibility**: Each class has one clear purpose
- **Progressive Enhancement**: Small, safe steps

---

## Risk Assessment

### Low Risk:
- ✅ Phase 1 coordinators (already complete)
- Extraction of window lifecycle methods (clear boundaries)
- WindowContentManager (simple collection management)

### Medium Risk:
- Window positioning logic (complex interactions with drag/resize)
- WindowRenderer (large extraction, many dependencies)
- Service consolidation (could break existing patterns)

### High Risk:
- Major API changes (would break backward compatibility - AVOID)
- Thread safety changes (could introduce subtle bugs - VERY CAREFUL)
- Changing existing event semantics

---

## Timeline Estimate

- ✅ **Phase 1**: COMPLETE (~4 commits, ~8 hours)
- ✅ **Phase 2.1**: COMPLETE (WindowLifecycleManager, ~2 hours)
- ✅ **Phase 2.2**: COMPLETE (WindowPositioningManager, ~2 hours)
- ⏳ **Phase 3.1**: 3-4 hours (WindowContentManager)
- ⏳ **Phase 3.2**: 5-6 hours (WindowRenderer - largest extraction)
- ⏳ **Phase 3.3**: 2-3 hours (BorderRenderer)
- ⏳ **Phase 3.4**: 2-3 hours (ScrollManager)
- ⏳ **Phase 3.5**: 3-4 hours (WindowEventDispatcher)
- ⏳ **Phase 4**: 3-4 hours (Cleanup and simplification)
- ⏳ **Phase 5**: Optional, as needed
- ⏳ **Phase 6**: Long-term (testing infrastructure)

**Total Completed**: ~12 hours
**Total Remaining**: ~25-35 hours of focused refactoring

---

## Decision Points

### Before Phase 2
1. **Is Phase 1 sufficient?** Have we achieved enough improvement? ✅ YES, proceed to Phase 2
2. **Testing Strategy**: Should we add more tests before Phase 2? ⏸️ Can defer to Phase 6
3. **Documentation**: Should we document the new architecture? ✅ This document serves as documentation
4. **Performance**: Any performance impact from the coordinators? ✅ None detected

### Before Phase 3
1. **Is Window refactoring needed?** Window.cs is still large but functional
2. **Risk vs Reward**: Is the complexity of Window extraction worth the benefit?
3. **Testing Coverage**: Do we have sufficient tests to validate Window refactoring?
4. **User Impact**: Will this improve developer experience significantly?

---

## Notes

- All phases maintain backward compatibility
- Each phase is independently valuable
- Can stop at any phase if complexity/risk becomes too high
- Focus on clarity over line count reduction
- God object refactoring is iterative - perfection not required

---

## Related Documentation

- **REFACTORING_PLAN.md** - Detailed Phase 1 & 2 implementation plans
- **DOM_LAYOUT_SYSTEM.md** - Layout system architecture
- **RENDERING_PIPELINE.md** - Rendering flow documentation
- **CLAUDE.md** - Development guidelines and code quality requirements

---

Last Updated: 2026-02-03
Status: Phase 1 & 2 COMPLETE, Phase 3 READY TO START

---

## Progress Summary

### Phase 1 & 2 Achievements:
- **6 Coordinators/Managers Created**: 1,758 total lines
  - InputCoordinator (350 lines)
  - RenderCoordinator (617 lines)
  - PerformanceTracker (156 lines)
  - StartMenuCoordinator (70 lines)
  - WindowLifecycleManager (301 lines)
  - WindowPositioningManager (264 lines)

- **ConsoleWindowSystem Reduction**: 1,977 → 1,743 lines (234 lines, 11.8%)
- **Build Status**: ✅ 0 errors, 0 warnings
- **Backward Compatibility**: ✅ 100% maintained
- **Commits**: 6 atomic commits
- **Time Investment**: ~12 hours

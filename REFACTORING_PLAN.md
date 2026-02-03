# ConsoleWindowSystem Refactoring Plan

## Overview

**Goal**: Break down the ConsoleWindowSystem "god object" into maintainable, testable components while maintaining 100% backward compatibility.

**Current State**: 1,743 lines (was 1,977), 54 public methods
**Target**: < 1,000 lines core orchestration

---

## Phase 1: Extract Coordinators ✅ COMPLETE

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

## Phase 2: Extract Window Lifecycle Management ✅ COMPLETE

Extract window creation, activation, closing, and state management logic.

### Phase 2.1: WindowLifecycleManager ✅ COMPLETE
- **Status**: ✅ Committed (13e8454)
- **Extracted**: ~152 lines → `SharpConsoleUI/Windows/WindowLifecycleManager.cs` (301 lines)
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
- **Extracted**: ~82 lines → `SharpConsoleUI/Windows/WindowPositioningManager.cs` (264 lines)
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

## Phase 3: Simplify ConsoleWindowSystem ⏳ NEXT

After Phase 2, ConsoleWindowSystem should be down to ~1,400 lines. Further simplification:

### Phase 3.1: Property Cleanup
- **Action**: Group related properties into configuration objects
- **Example**: Desktop bounds properties → DesktopBounds record
- **Impact**: Reduce property count, improve cohesion

### Phase 3.2: Service Exposure Cleanup
- **Action**: Create ServiceRegistry or ServiceProvider pattern
- **Current**: Individual properties for each service (WindowStateService, FocusStateService, etc.)
- **Proposed**: Single Services property with typed access
- **Impact**: Reduce surface area, cleaner API

### Phase 3.3: Method Delegation Review
- **Action**: Review all remaining public methods
- **Goal**: Ensure each method is in the right place
- **Consider**: Some window control methods might belong in Window class itself

---

## Phase 4: State Service Consolidation (Optional) 🔮 LONG-TERM

Currently have 11+ state services. Consider consolidation where appropriate.

### Potential Consolidations:
1. **SelectionStateService** + **EditStateService** → EditingStateService
2. **ScrollStateService** + **LayoutStateService** → ViewportStateService
3. **CursorStateService** + **InputStateService** → InputStateService (enhanced)

**Caution**: Only do this if it genuinely improves clarity. Current separation may be intentional.

---

## Success Metrics

### Quantitative Goals:
- ✅ **Phase 1**: ConsoleWindowSystem < 2,000 lines (Was: 1,977)
- ✅ **Phase 2**: ConsoleWindowSystem < 1,800 lines (Currently: 1,743)
- ⏳ **Phase 3**: ConsoleWindowSystem < 1,400 lines
- ⏳ **Phase 4**: ConsoleWindowSystem < 1,000 lines
- ⏳ **Final**: ConsoleWindowSystem ~800 lines (core orchestration only)

### Qualitative Goals:
- ✅ Single Responsibility Principle for all extracted classes
- ✅ 100% backward compatibility maintained
- ✅ Zero functional changes during refactoring
- ✅ All tests pass after each phase
- ⏳ Improved testability (can mock coordinators)
- ⏳ Reduced cognitive load (easier to understand each piece)

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
```

---

## Implementation Guidelines

### For Each Phase:

1. **Analyze**: Identify cohesive responsibility to extract
2. **Create**: New coordinator/manager class with clear interface
3. **Extract**: Move fields, methods, and logic to new class
4. **Delegate**: ConsoleWindowSystem delegates to coordinator
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

### Medium Risk:
- Window positioning logic (complex interactions with drag/resize)
- Service consolidation (could break existing patterns)

### High Risk:
- Major API changes (would break backward compatibility - AVOID)
- Thread safety changes (could introduce subtle bugs - VERY CAREFUL)

---

## Timeline Estimate

- ✅ **Phase 1**: COMPLETE (~4 commits, ~8 hours)
- ✅ **Phase 2.1**: COMPLETE (WindowLifecycleManager, ~2 hours)
- ✅ **Phase 2.2**: COMPLETE (WindowPositioningManager, ~2 hours)
- ⏳ **Phase 3**: 3-4 hours (Cleanup and simplification)
- ⏳ **Phase 4**: Optional, as needed

**Total Completed**: ~12 hours
**Total Remaining**: ~5-10 hours of focused refactoring

---

## Decision Points

Before proceeding to Phase 2, consider:

1. **Is Phase 1 sufficient?** Have we achieved enough improvement?
2. **Testing Strategy**: Should we add more tests before Phase 2?
3. **Documentation**: Should we document the new architecture?
4. **Performance**: Any performance impact from the coordinators?

---

## Notes

- All phases maintain backward compatibility
- Each phase is independently valuable
- Can stop at any phase if complexity/risk becomes too high
- Focus on clarity over line count reduction
- God object refactoring is iterative - perfection not required

---

Last Updated: 2026-02-03
Status: Phase 1 & 2 COMPLETE, Phase 3 READY TO START

## Summary of Achievements

**Overall Progress:**
- **Lines Extracted**: 1,074 lines across 6 coordinators/managers
- **Reduction**: 1,977 → 1,743 lines (234 line reduction, 11.8%)
- **Commits**: 6 commits across 2 phases
- **Time Spent**: ~12 hours
- **Backward Compatibility**: 100% maintained
- **Build Status**: ✅ 0 errors, 0 warnings

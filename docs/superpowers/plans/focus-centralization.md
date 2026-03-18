# Focus System Centralization Plan

> **Baseline**: Commit `03bd0ce`, 2331 tests passing.
> **Goal**: Replace 4 parallel focus authorities with one FocusManager that owns the focus path.

## Problem

The focus system has 4 independent authorities that must stay synchronized:
1. `FocusStateService` — centralized but fights with local state
2. `Window._lastFocusedControl / _lastDeepFocusedControl` — window-level tracking
3. Container `_focusedChild` / `_focusedContent` — per-container tracking
4. Control `HasFocus` — leaf-level state with side effects in setter

Each bug fix adds workarounds (save/restore, notification chain guards) instead of fixing the architecture.

## Target

One `FocusManager` owns a **focus path** — an ordered list from root to leaf:
```
[NavigationView, ScrollablePanelControl, ButtonControl]
```
- Controls are passive (don't track focus themselves)
- Tab traversal is one algorithm (not reimplemented per container)
- HasFocus is derived from the path (not independently managed)

## Phases

### Phase 1: Focus path (additive) — LOW risk ✅ DONE
- [x] Evolve `FocusCoordinator` with `FocusPath` field (List<IWindowControl>)
- [x] Add `FocusPath` (read-only), `FocusedLeaf` properties
- [x] Add `GetFocusedChild(IContainer)` — returns path entry that is child of container
- [x] Add `IsInFocusPath(IWindowControl)` — returns true/false
- [x] `RequestFocus` computes and stores the focus path via `UpdateFocusPath`
- [x] `MoveFocus` updates path after focusing (finds deep leaf)
- [x] `HandleClickFocus` updates path on same-top-level clicks
- [x] `ClearFocus` clears the path
- [x] Manager also sets `HasFocus` on path controls (backward compat)
- [x] Existing `_focusedChild` fields stay (redundant but harmless)
- [x] 9 FocusPath tests added
- [x] All 2340 tests pass — no behavior change

**Note**: In Phase 1, the path is only updated through coordinator entry points (RequestFocus, MoveFocus, HandleClickFocus, FocusControl). Direct SetFocus calls and container-internal Tab don't update the path yet — that's Phase 2+.

**Files**: `Core/FocusCoordinator.cs`, `Tests/FocusManagement/FocusPathTests.cs`

### Phase 2: Route ProcessKey through manager — MEDIUM risk
- [ ] `SPC.ProcessKey`: replace `_focusedChild.ProcessKey(key)` with `manager.GetFocusedChild(this)?.ProcessKey(key)`
- [ ] `HGrid.ProcessKey`: replace `_focusedContent.ProcessKey(key)` with `manager.GetFocusedChild(this)?.ProcessKey(key)`
- [ ] Remove `_focusedChild` from SPC
- [ ] Remove `_focusedContent` from HGrid
- [ ] Remove `NotifyChildFocusChanged` calls where manager handles it
- [ ] All tests pass

**Files**: `ScrollablePanelControl.Input.cs`, `HorizontalGridControl.Input.cs`, `ScrollablePanelControl.Children.cs`

### Phase 3: Centralize Tab traversal — HIGH risk
- [ ] Add `HandleTab(backward)` to FocusManager
- [ ] Manager walks tree: knows opaque containers, scroll mode, direction
- [ ] Remove Tab handling from `SPC.ProcessKey`
- [ ] Remove Tab handling from `HGrid.ProcessKey`
- [ ] Remove Tab handling from `NavigationView.ProcessKey`
- [ ] SPC only handles arrow keys (scroll) and Escape
- [ ] HGrid only handles non-Tab key routing
- [ ] NavigationView only handles nav-specific keys
- [ ] All tests pass

**Files**: `Core/FocusManager.cs`, `ScrollablePanelControl.Input.cs`, `HorizontalGridControl.Input.cs`, `NavigationView.Input.cs`

### Phase 4: Simplify HasFocus — MEDIUM risk
- [ ] `HasFocus` getter: `manager.IsInFocusPath(this)`
- [ ] `HasFocus` setter: `manager.RequestFocus(this)` / `manager.Unfocus(this)`
- [ ] Remove `_hasFocus` field from controls
- [ ] GotFocus/LostFocus events fired by manager
- [ ] Remove notification chains (NotifyParentWindowOfFocusChange)
- [ ] All tests pass

**Files**: `BaseControl.cs`, `ScrollablePanelControl.cs`, `HorizontalGridControl.cs`, `NavigationView.Input.cs`, `Core/FocusManager.cs`

### Phase 5: Clean up dead code — LOW risk
- [ ] Remove or empty `IFocusTrackingContainer` interface
- [ ] Remove `_lastFocusedControl`, `_lastDeepFocusedControl` from Window
- [ ] Remove or merge `FocusStateService` into FocusManager
- [ ] Remove `NotifyParentWindowOfFocusChange` extension
- [ ] Remove per-container focus fields and methods
- [ ] All tests pass

**Files**: `Window.cs`, `Window.Focus.cs`, `Core/FocusStateService.cs`, `Extensions/WindowControlExtensions.cs`, interface files

## Backward Compatibility (external API)

These must continue working for mide and squad-monitor:
- `HasFocus` property (read/write)
- `SetFocus(bool, FocusReason)` method
- `GotFocus` / `LostFocus` events
- `IFocusableControl` interface
- `Window.FocusControl()`, `Window.SwitchFocus()`

## Test Strategy

2331 existing tests (including 51 focus-specific tests) serve as regression suite. Each phase must pass all tests before proceeding.

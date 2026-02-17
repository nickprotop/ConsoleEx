# ConsoleEx Roadmap

Findings from an honest architectural review of the framework. Organized by priority — foundations first, then polish, then growth.

---

## 1. API Consistency (High Priority)

These are the things that will burn every new user on day one.

### ~~Color Parameter Ordering~~ (Done)

All `WithColors`, `WithFocusedColors`, and `WithHighlightColors` methods now consistently use `(foreground, background)` parameter ordering across all builders: ListBuilder, ButtonBuilder, WindowBuilder, and SpectreRenderableBuilder.

### Method Naming

Audit all builders for naming consistency:
- `WithHighlightColors` vs `WithFocusedColors` — are these the same concept with different names on different controls, or genuinely different states? If different, document the distinction clearly. If same, unify.
- `SimpleMode()` vs `With*()` prefix pattern — most methods use `With` prefix, some don't. Decide on one convention.

---

## 2. Class Decomposition (High Priority)

These files are too large and carry too many responsibilities. They'll slow down every future change.

### Window.cs (2,350 lines)

Currently acts as: control container, render coordinator, input dispatcher, state manager, lifecycle owner, scroll manager, and border owner.

Split candidates:
- `WindowState` — state properties (position, size, minimized/maximized, title)
- `WindowControls` — child control management (add, remove, find by name)
- `WindowScrolling` — scroll offset, scroll-into-view logic
- Keep `Window` as the facade that delegates to these

### WindowStateService.cs (43KB)

This is the largest single file. Likely managing window creation, destruction, activation, Z-order, enumeration, and events all in one place.

### AnsiConsoleHelper.cs (35KB)

A static helper with 1,000+ lines doing markup conversion, ANSI generation, string measurement, color parsing, and capture console creation. Split by concern:
- `MarkupConverter` — Spectre markup to ANSI
- `AnsiStringUtils` — substring, truncate, measure with ANSI awareness
- `CaptureConsoleFactory` — Spectre capture console creation

### NetConsoleDriver.cs (35KB)

Input polling, mouse parsing, resize detection, ANSI output, cursor management, platform detection. Split input handling from output handling at minimum.

---

## 3. Control Authoring Story (High Priority)

This is the ceiling on adoption. Right now, writing a custom control means implementing 3-5 interfaces from scratch with no guidance on which combinations to use.

### Add ControlBase Abstract Class

```
ControlBase : IWindowControl, IDOMPaintable, IDisposable
  - Default implementations for layout properties, margins, visibility
  - Default Invalidate() that walks up to container
  - Abstract MeasureDOM() and PaintDOM() — the only things authors MUST implement

InteractiveControlBase : ControlBase, IInteractiveControl, IFocusableControl
  - Default focus management (HasFocus, IsEnabled, GotFocus/LostFocus events)
  - Abstract ProcessKey() — the only thing authors MUST implement

MouseAwareControlBase : InteractiveControlBase, IMouseAwareControl
  - Default mouse event plumbing
  - Virtual ProcessMouseEvent() with default no-op
```

This doesn't break existing controls — they can keep their direct interface implementations. But new control authors get a fast path.

---

## 4. Rendering Pipeline Optimization (Medium Priority)

### String-to-Cell Round-Trip

Current flow: Spectre Markup -> ANSI string -> parse to cells -> CharacterBuffer -> diff -> emit as ANSI string again.

The markup-to-ANSI-to-cell-to-ANSI round-trip creates allocation pressure. Consider:
- Caching parsed cell arrays for static content (markup that doesn't change between frames)
- Direct cell emission from controls that don't use Spectre markup (buttons, rules, separators can write cells directly without going through string intermediary)

### Two Buffer Systems

`CharacterBuffer` (layout-level, per-window) and `ConsoleBuffer` (driver-level, whole screen) both maintain cell grids with different `Cell` types. Evaluate whether these can share a cell representation, or whether the layout buffer can write directly to the driver buffer's coordinate space (eliminating the per-window buffer copy).

---

## 5. Input Responsiveness (Medium Priority)

### Replace Thread.Sleep with Waitable Event

The main loop uses `Thread.Sleep(_idleTime)` for frame pacing. When idle, the adaptive sleep can push this to 50ms+, adding perceptible lag to the first keypress after an idle period.

Replace with `ManualResetEventSlim` or `AutoResetEvent`:
- Input thread signals the event when a key/mouse event arrives
- Main loop waits on the event with timeout (the idle duration)
- First input after idle wakes the loop immediately instead of waiting for the sleep to expire

This is a small change with noticeable UX improvement.

---

## 6. Missing Controls (Medium Priority)

Controls that real applications commonly need:

- **RadioButtonGroup** — exclusive selection from a set (CheckboxControl can't do this alone)
- **NumericSpinner** — increment/decrement number with arrow keys
- **DatePicker / TimePicker** — date/time selection
- **ColorPicker** — color selection dialog
- **StatusBarControl** — per-window status bar (not just the system-level one)
- **SplitPanel** — vertical splitter (HorizontalGrid handles horizontal, but vertical split between two panels is a common need)

### Data Virtualization

ListControl, TreeControl, and TableControl appear to hold all items in memory. For large datasets (100K+ rows), add a virtualized data source pattern:
- `IVirtualListSource` — provides count + items-in-range
- Only measure/render visible items
- Scrollbar reflects total count

---

## 7. Driver Abstraction (Medium Priority)

### ~~Headless/Test Driver~~ (Done)

`HeadlessConsoleDriver` is now in the main library (`SharpConsoleUI.Drivers`). `MockConsoleDriver` in the test project is a thin subclass.

### Consider Additional Backends

The `IConsoleDriver` interface is clean enough to support:
- Windows Console Host (native Win32, not ANSI — for legacy terminals)
- SSH/remote session driver
- Web terminal driver (for browser-based TUI)

Not urgent, but the abstraction is already there — this is future growth potential.

---

## 8. Documentation Gaps (Low Priority)

The existing docs are solid (12 files, 200KB+). What's missing:

- **Custom Control Authoring Guide** — step-by-step guide to creating a new control (blocked by item 3 above)
- **Architecture Overview** — a single diagram showing the rendering pipeline layers, the event flow, and the class relationships
- **Migration/Changelog** — when breaking changes happen (like the color parameter unification), document them clearly
- **Performance Tuning Guide** — when to use frame rate limiting, how to optimize large windows, how to profile with the built-in diagnostics

---

## 9. Testing Gaps (Low Priority)

Current tests focus heavily on rendering (23 of 51 files). Areas with less coverage:

- **Control behavior tests** — only 3 control test files exist. Each interactive control should have tests for keyboard handling, mouse handling, and state changes.
- **Builder tests** — verify that builders produce controls with correct properties
- **Theme tests** — verify color resolution cascade works correctly
- **Integration tests** — multi-window scenarios (modal on top of modal, window activation with overlapping, resize with multiple dirty windows)

---

## 10. Minor Cleanup (Low Priority)

- Remove `FIX` toggle constants after confirming fixes are stable (they've served their purpose as feature flags during development, but permanent `const bool FIX11 = true` is dead code)
- Inconsistent indentation in `Renderer.cs` — some blocks use tabs, some use spaces (visible in the RenderLock guard blocks)
- `Performance` field on `ConsoleWindowSystem` is `public` without a property wrapper — should be `{ get; }` or `{ get; private set; }`
- `Input` field on `ConsoleWindowSystem` same issue
- `Render` property is `{ get; private set; }` with `= null!` — initialized in constructor, could be `{ get; }` with readonly backing

---

## Summary

| Priority | Item | Effort | Impact |
|----------|------|--------|--------|
| High | Color parameter ordering unification | Small | Prevents every user's first bug |
| High | Class decomposition (Window, WSS, Driver) | Large | Maintainability, contributor onboarding |
| High | ControlBase abstract class | Medium | Unlocks third-party controls |
| Medium | String-to-cell round-trip optimization | Medium | Performance under load |
| Medium | Thread.Sleep -> waitable event | Small | Input responsiveness |
| Medium | Missing common controls | Large | Feature completeness |
| Medium | Data virtualization | Medium | Large dataset support |
| ~~Medium~~ | ~~Headless driver in main library~~ | ~~Small~~ | ~~Done~~ |
| Low | Documentation gaps | Medium | Adoption |
| Low | Testing gaps | Large | Reliability confidence |
| Low | Minor code cleanup | Small | Code quality |

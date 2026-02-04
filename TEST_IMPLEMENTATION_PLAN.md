# Test Infrastructure Implementation Plan

## Overview
Comprehensive testing system for the TUI rendering pipeline validating correctness, performance, and quality across all three layers: DOM/CharacterBuffer, ANSI generation, and ConsoleBuffer/double-buffering.

**Timeline**: 5 weeks across 11 implementation phases
**Deliverables**: ~95 new test files, 12 diagnostics files, 6 test infrastructure helpers, 5 modified existing files

---

## Phase 1: Core Infrastructure (Week 1 - Days 1-2) ✅ COMPLETE
**Goal**: Set up diagnostics infrastructure and test project foundation.

### Files to Create
- [x] `SharpConsoleUI/Diagnostics/RenderingDiagnostics.cs`
- [x] `SharpConsoleUI/Diagnostics/RenderingMetrics.cs`
- [x] `SharpConsoleUI/Diagnostics/Snapshots/CharacterBufferSnapshot.cs`
- [x] `SharpConsoleUI/Diagnostics/Snapshots/AnsiLinesSnapshot.cs`
- [x] `SharpConsoleUI/Diagnostics/Snapshots/ConsoleBufferSnapshot.cs`
- [x] `SharpConsoleUI/Diagnostics/Snapshots/RenderOutputSnapshot.cs`
- [x] `SharpConsoleUI/Diagnostics/Analysis/QualityAnalyzer.cs`
- [x] `SharpConsoleUI/Diagnostics/Analysis/QualityReport.cs`
- [x] `SharpConsoleUI.Tests/SharpConsoleUI.Tests.csproj`
- [x] `SharpConsoleUI.Tests/Infrastructure/TestWindowSystemBuilder.cs`
- [x] `SharpConsoleUI.Tests/Infrastructure/MockConsoleDriver.cs`

### Files to Modify
- [x] `SharpConsoleUI/Configuration/ConsoleWindowSystemOptions.cs` (add diagnostics options)
- [x] `SharpConsoleUI/ConsoleWindowSystem.cs` (add RenderingDiagnostics property)

### Verification
- [x] Build succeeds with 0 errors
- [x] Diagnostics can be enabled/disabled
- [x] `dotnet test` runs (no tests yet, just infrastructure)
- [x] Mock console driver captures output

**Status**: Phase 1 complete! All diagnostics infrastructure and test project files created and verified.

---

## Phase 2: Rendering Pipeline Metrics (Week 1 - Days 3-4) ✅ COMPLETE
**Goal**: Add metrics collection hooks to rendering pipeline.

### Files to Modify
- [x] `SharpConsoleUI/Drivers/ConsoleBuffer.cs` (add metrics capture in Render())
- [x] `SharpConsoleUI/Layout/CharacterBuffer.cs` (add metrics in ToLines())
- [x] `SharpConsoleUI/Windows/WindowRenderer.cs` (add snapshot capture)
- [x] `SharpConsoleUI/Drivers/NetConsoleDriver.cs` (connect diagnostics to ConsoleBuffer)

### Verification
- [x] Metrics are captured per frame
- [x] Snapshots contain correct data
- [x] Diagnostics can be queried by frame number
- [x] No performance impact when diagnostics disabled (zero overhead when disabled)

**Implementation Details:**
- Added `Diagnostics` property to ConsoleBuffer and CharacterBuffer
- ConsoleBuffer.Render() captures: metrics, console buffer snapshot, output snapshot
- CharacterBuffer.ToLines() captures: ANSI lines snapshot
- WindowRenderer.PaintDOM() captures: CharacterBuffer snapshot
- NetConsoleDriver connects diagnostics when creating ConsoleBuffer
- Helper methods: CountAnsiSequences(), CountCursorMoves(), CaptureConsoleBufferSnapshot()

**Status**: Phase 2 complete! All rendering layers now capture diagnostics data.

---

## Phase 3: Rendering Pipeline Tests (Week 1 - Day 5, Week 2 - Days 1-2)
**Goal**: Comprehensive testing of the three-layer rendering pipeline.

### Top Layer Tests (4 files)
- [ ] `SharpConsoleUI.Tests/Rendering/Unit/TopLayer/CharacterBufferTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Unit/TopLayer/WindowRendererTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Unit/TopLayer/DOMLayoutTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Unit/TopLayer/CellRenderingTests.cs`

### Middle Layer Tests (3 files)
- [ ] `SharpConsoleUI.Tests/Rendering/Unit/MiddleLayer/AnsiGenerationTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Unit/MiddleLayer/AnsiOptimizationTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Unit/MiddleLayer/ColorSequenceTests.cs`

### Bottom Layer Tests (3 files)
- [ ] `SharpConsoleUI.Tests/Rendering/Unit/BottomLayer/ConsoleBufferTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Unit/BottomLayer/DoubleBufferingTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Unit/BottomLayer/DirtyTrackingTests.cs`

### Integration Tests (3 files)
- [ ] `SharpConsoleUI.Tests/Rendering/Integration/PipelineTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Integration/WindowOverlapTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Integration/ContentPreservationTests.cs`

### Verification
- [ ] All rendering unit tests pass
- [ ] Integration tests validate data flow through all layers
- [ ] Can assert on cell contents, colors, positions at each layer

---

## Phase 4: Performance & Quality Tests (Week 2 - Days 3-4)
**Goal**: Validate performance optimizations and detect quality issues.

### Performance Tests (4 files)
- [ ] `SharpConsoleUI.Tests/Rendering/Performance/StaticContentTests.cs` ⭐ CRITICAL
- [ ] `SharpConsoleUI.Tests/Rendering/Performance/IncrementalUpdateTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Performance/OptimizationTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Performance/BenchmarkTests.cs`

### Quality Tests (4 files)
- [ ] `SharpConsoleUI.Tests/Rendering/Quality/RedundantAnsiTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Quality/OverInvalidationTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Quality/CursorEfficiencyTests.cs`
- [ ] `SharpConsoleUI.Tests/Rendering/Quality/RegressionTests.cs`

### Additional Infrastructure
- [ ] `SharpConsoleUI/Diagnostics/Analysis/AnsiParser.cs`

### Verification
- [ ] ✅ Static content produces ZERO output (CI quality gate)
- [ ] ✅ Single character change produces <50 bytes
- [ ] ✅ Efficiency ratio >80%
- [ ] ✅ Zero redundant ANSI sequences
- [ ] ✅ Over-invalidation detection works
- [ ] Quality gates can fail CI builds

---

## Phase 5: Window Management Tests (Week 2 - Day 5, Week 3 - Day 1)
**Goal**: Test window lifecycle, z-order, states, and positioning.

### Window Management Tests (5 files)
- [ ] `SharpConsoleUI.Tests/WindowManagement/WindowLifecycleTests.cs`
- [ ] `SharpConsoleUI.Tests/WindowManagement/ZOrderTests.cs`
- [ ] `SharpConsoleUI.Tests/WindowManagement/WindowActivationTests.cs`
- [ ] `SharpConsoleUI.Tests/WindowManagement/WindowStatesTests.cs`
- [ ] `SharpConsoleUI.Tests/WindowManagement/WindowPositioningTests.cs`

### Verification
- [ ] Window lifecycle works correctly
- [ ] Z-order is maintained
- [ ] Window states transition properly
- [ ] Drag and resize operations work

---

## Phase 6: Focus & Input Tests (Week 3 - Days 2-3)
**Goal**: Test focus management and input handling systems.

### Focus Management Tests (5 files)
- [ ] `SharpConsoleUI.Tests/FocusManagement/KeyboardFocusTests.cs`
- [ ] `SharpConsoleUI.Tests/FocusManagement/MouseFocusTests.cs`
- [ ] `SharpConsoleUI.Tests/FocusManagement/TabNavigationTests.cs`
- [ ] `SharpConsoleUI.Tests/FocusManagement/WindowSwitchingTests.cs`
- [ ] `SharpConsoleUI.Tests/FocusManagement/FocusEventsTests.cs`

### Input Handling Tests (5 files)
- [ ] `SharpConsoleUI.Tests/InputHandling/KeyboardEventTests.cs`
- [ ] `SharpConsoleUI.Tests/InputHandling/MouseEventTests.cs`
- [ ] `SharpConsoleUI.Tests/InputHandling/EventPropagationTests.cs`
- [ ] `SharpConsoleUI.Tests/InputHandling/EventCancellationTests.cs`
- [ ] `SharpConsoleUI.Tests/InputHandling/ShortcutsTests.cs`

### Verification
- [ ] Focus changes work correctly
- [ ] Tab navigation respects TabIndex and disabled state
- [ ] Events route to correct controls/windows
- [ ] Event cancellation stops propagation

---

## Phase 7: Modal & State Services Tests (Week 3 - Days 4-5)
**Goal**: Test modal system and state service coordination.

### Modal System Tests (4 files)
- [ ] `SharpConsoleUI.Tests/ModalSystem/ModalStackTests.cs`
- [ ] `SharpConsoleUI.Tests/ModalSystem/ModalBlockingTests.cs`
- [ ] `SharpConsoleUI.Tests/ModalSystem/ModalFocusTests.cs`
- [ ] `SharpConsoleUI.Tests/ModalSystem/ModalResultTests.cs`

### State Services Tests (6 files)
- [ ] `SharpConsoleUI.Tests/StateServices/WindowStateServiceTests.cs`
- [ ] `SharpConsoleUI.Tests/StateServices/FocusStateServiceTests.cs`
- [ ] `SharpConsoleUI.Tests/StateServices/ModalStateServiceTests.cs`
- [ ] `SharpConsoleUI.Tests/StateServices/NotificationStateServiceTests.cs`
- [ ] `SharpConsoleUI.Tests/StateServices/ThemeStateServiceTests.cs`
- [ ] `SharpConsoleUI.Tests/StateServices/StateConsistencyTests.cs`

### Verification
- [ ] Modal blocking works correctly
- [ ] State services maintain consistency
- [ ] Modal stack operates correctly
- [ ] State transitions are valid

---

## Phase 8: Control Tests (Week 4 - Days 1-2)
**Goal**: Test all control types and their behavior.

### Control Tests (8+ files)
- [ ] `SharpConsoleUI.Tests/Controls/ButtonControlTests.cs`
- [ ] `SharpConsoleUI.Tests/Controls/ListControlTests.cs`
- [ ] `SharpConsoleUI.Tests/Controls/TreeControlTests.cs`
- [ ] `SharpConsoleUI.Tests/Controls/TextBoxControlTests.cs`
- [ ] `SharpConsoleUI.Tests/Controls/LabelControlTests.cs`
- [ ] `SharpConsoleUI.Tests/Controls/CheckboxControlTests.cs`
- [ ] `SharpConsoleUI.Tests/Controls/ControlStateTests.cs`
- [ ] `SharpConsoleUI.Tests/Controls/ControlEventsTests.cs`

### Verification
- [ ] Each control type behaves correctly
- [ ] State changes work as expected
- [ ] Events fire at appropriate times
- [ ] User interactions produce correct results

---

## Phase 9: Layout & Theme Tests (Week 4 - Days 3-4)
**Goal**: Test layout system and theming.

### Layout Tests (6 files)
- [ ] `SharpConsoleUI.Tests/Layout/MeasurePhaseTests.cs`
- [ ] `SharpConsoleUI.Tests/Layout/ArrangePhaseTests.cs`
- [ ] `SharpConsoleUI.Tests/Layout/MarginPaddingTests.cs`
- [ ] `SharpConsoleUI.Tests/Layout/ContainerTests.cs`
- [ ] `SharpConsoleUI.Tests/Layout/SplitterTests.cs`
- [ ] `SharpConsoleUI.Tests/Layout/DynamicResizingTests.cs`

### Theme Tests (4 files)
- [ ] `SharpConsoleUI.Tests/Theming/ThemeSwitchingTests.cs`
- [ ] `SharpConsoleUI.Tests/Theming/ThemeInheritanceTests.cs`
- [ ] `SharpConsoleUI.Tests/Theming/ColorResolutionTests.cs`
- [ ] `SharpConsoleUI.Tests/Theming/CustomThemeTests.cs`

### Notification Tests (5 files)
- [ ] `SharpConsoleUI.Tests/Notifications/NotificationDisplayTests.cs`
- [ ] `SharpConsoleUI.Tests/Notifications/NotificationTimeoutTests.cs`
- [ ] `SharpConsoleUI.Tests/Notifications/NotificationDismissalTests.cs`
- [ ] `SharpConsoleUI.Tests/Notifications/NotificationSeverityTests.cs`
- [ ] `SharpConsoleUI.Tests/Notifications/NotificationStackTests.cs`

### Verification
- [ ] Layout calculations are correct
- [ ] Margins and padding apply properly
- [ ] Containers position children correctly
- [ ] Theme switching updates all windows
- [ ] Notifications display and dismiss correctly

---

## Phase 10: End-to-End & Advanced Features (Week 4 - Day 5, Week 5)
**Goal**: End-to-end scenarios and advanced testing features.

### End-to-End Tests (4 files)
- [ ] `SharpConsoleUI.Tests/EndToEnd/UserScenarioTests.cs`
- [ ] `SharpConsoleUI.Tests/EndToEnd/MultiWindowWorkflowTests.cs`
- [ ] `SharpConsoleUI.Tests/EndToEnd/ComplexInteractionTests.cs`
- [ ] `SharpConsoleUI.Tests/EndToEnd/IntegrationSmokeTests.cs`

### Advanced Infrastructure (6 files)
- [ ] `SharpConsoleUI.Tests/Infrastructure/GoldenSnapshotManager.cs`
- [ ] `SharpConsoleUI.Tests/Infrastructure/SnapshotVisualizer.cs`
- [ ] `SharpConsoleUI.Tests/Infrastructure/DiagnosticsAssertions.cs`
- [ ] `SharpConsoleUI/Diagnostics/Profiling/MemoryProfiler.cs`
- [ ] `SharpConsoleUI/Diagnostics/Profiling/TimingProfiler.cs`
- [ ] `SharpConsoleUI/Diagnostics/Export/MetricsExporter.cs`

### Verification
- [ ] End-to-end scenarios work correctly
- [ ] Golden snapshots catch visual regressions
- [ ] Performance benchmarks establish baselines
- [ ] Reports can be generated for CI/CD
- [ ] Memory and timing profiling work

---

## Phase 11: CI/CD Integration & Documentation (Week 5)
**Goal**: CI/CD quality gates and comprehensive documentation.

### Quality Gates (1 file)
- [ ] `SharpConsoleUI.Tests/Quality/QualityGateTests.cs`

### CI/CD Configuration
- [ ] `.github/workflows/test.yml`
- [ ] Quality gate enforcement scripts

### Documentation
- [ ] `TESTING.md` (testing guide)
- [ ] Update main README.md with testing section

### Verification
- [ ] CI/CD pipeline runs all tests
- [ ] Quality gates properly fail builds on regressions
- [ ] Documentation is clear and complete
- [ ] Team can run and understand tests

---

## Success Criteria Summary

### Rendering Pipeline ✅
- [ ] Window rendering validated at all three layers
- [ ] Content preservation through pipeline verified
- [ ] Window overlap and Z-order render correctly
- [ ] Static content produces **ZERO bytes output** (CRITICAL)
- [ ] Single character change produces <50 bytes output
- [ ] Efficiency ratio >80% for typical scenarios
- [ ] Zero redundant ANSI sequences detected
- [ ] Over-invalidation <20% of window area
- [ ] ANSI optimization score >90%

### System Tests ✅
- [ ] Window management (lifecycle, z-order, states, positioning)
- [ ] Focus management (keyboard, mouse, tab navigation)
- [ ] Modal system (stack, blocking, focus trapping)
- [ ] Input handling (keyboard, mouse, propagation, cancellation)
- [ ] All control types work correctly
- [ ] Layout system (measure, arrange, margins, padding, containers)
- [ ] Theme system (switching, inheritance, color resolution)
- [ ] Notification system (display, timeout, dismissal, severity)
- [ ] State services maintain consistency
- [ ] End-to-end user workflows execute properly

### CI/CD Integration ✅
- [ ] Quality gates fail build on regressions
- [ ] Performance benchmarks establish baselines
- [ ] Golden snapshots catch visual regressions
- [ ] All tests pass in CI environment
- [ ] Test reports generated

### Code Coverage ✅
- [ ] Rendering pipeline: >90% coverage
- [ ] Window management: >85% coverage
- [ ] Focus/Input systems: >85% coverage
- [ ] Controls: >80% coverage (each control)
- [ ] Layout system: >85% coverage
- [ ] State services: >90% coverage
- [ ] Overall: >80% code coverage

---

## File Count Summary

- **Diagnostics Infrastructure**: 12 files
- **Test Infrastructure**: 6 files
- **Rendering Tests**: 21 files
- **Window Management Tests**: 5 files
- **Focus & Input Tests**: 10 files
- **Modal & State Tests**: 10 files
- **Control Tests**: 8+ files
- **Layout & Theme Tests**: 15 files
- **End-to-End Tests**: 4 files
- **Quality Gates**: 1 file
- **Modified Existing Files**: 5 files

**TOTAL: ~95 NEW FILES + 5 MODIFIED FILES**

---

## Current Status

**Phase**: Phase 2 (Rendering Pipeline Metrics) - ✅ COMPLETE
**Files Created**: 11 / ~95
**Files Modified**: 6 / 5 (exceeded plan - added NetConsoleDriver)
**Tests Passing**: 0 (no tests written yet, infrastructure only)
**Coverage**: 0%

**Completed**:
- ✅ Phase 1: Core diagnostics infrastructure and test project
  - RenderingDiagnostics, RenderingMetrics, Snapshot classes
  - QualityAnalyzer and QualityReport
  - Test project with MockConsoleDriver and TestWindowSystemBuilder
  - Build verified, dotnet test runs successfully

- ✅ Phase 2: Rendering pipeline metrics hooks
  - ConsoleBuffer.Render() captures metrics, snapshots, and output
  - CharacterBuffer.ToLines() captures ANSI snapshot
  - WindowRenderer.PaintDOM() captures CharacterBuffer snapshot
  - All three layers (DOM, ANSI, ConsoleBuffer) now instrumented
  - Zero overhead when diagnostics disabled

**Next Steps**: Phase 3 - Write rendering pipeline tests (Unit, Integration, Performance, Quality)

---

## Notes

- All diagnostics features are opt-in (disabled by default, zero overhead)
- Non-invasive modifications to existing code (capture hooks only)
- Mock console driver allows fast, deterministic, parallelizable tests
- Golden snapshots provide visual regression protection
- Quality gates prevent performance degradations in CI/CD

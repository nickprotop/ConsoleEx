# SharpConsoleUI Benchmarks

Headless, deterministic [BenchmarkDotNet](https://benchmarkdotnet.org/) measurements of the
core hot paths and the layout engine. Their purpose is **regression tracking** — comparable
run-to-run so a slowdown in a hot path shows up in a diff — and providing a before/after
baseline for the planned ScrollLayout refactor.

> **Not the same as `Examples/BenchmarkApp`.** That is a *live, interactive* "rate your
> terminal" showcase: it renders animated scenes to a real terminal and reports end-to-end FPS
> with a star rating. Its numbers depend on the machine, the terminal emulator, and the
> animation, so they are **not** comparable across commits. The suite documented here is the
> opposite: headless (in-memory `MockConsoleDriver`), allocation-aware, and deterministic. Use
> `BenchmarkApp` to *feel* the framework; use this suite to *measure* it.

## Running

The benchmark project is standalone (not in `SharpConsoleUI.sln`, not run in CI — performance
is too noisy on shared runners to gate on). Run it by path, in Release:

```bash
# Full suite (several minutes — the layout-tree macro is intentionally heavy):
dotnet run -c Release --project SharpConsoleUI.Benchmarks -- --filter '*'

# A single class:
dotnet run -c Release --project SharpConsoleUI.Benchmarks -- --filter '*MarkupParsingBenchmarks*'

# Fast pass (3 iterations) — what the baseline below was captured with:
dotnet run -c Release --project SharpConsoleUI.Benchmarks -- --filter '*' --job short
```

BenchmarkDotNet writes per-class markdown/CSV/HTML under `BenchmarkDotNet.Artifacts/results/`
(gitignored). To refresh the baseline below, run the suite and paste the `-report-github.md`
tables here, updating the "Captured on" line.

## Baseline

> **Captured on:** AMD Ryzen 7 8845HS (8 physical / 16 logical cores), Ubuntu 26.04, .NET
> 10.0.9 (RyuJIT AVX-512), BenchmarkDotNet v0.14.0. **`--job short`** (3 warmup + 3 iterations)
> — a fast pass, so the `Error` column is wide; treat these as order-of-magnitude baselines, not
> precise figures. Re-run with the default job for tighter numbers before drawing fine
> conclusions. Numbers are hardware-specific; only compare runs from the same machine.

### MarkupParsingBenchmarks (micro)

`MarkupParser.Parse` / `StripLength` / `Truncate` over four input shapes.

| Method      | Markup               | Mean       | Allocated |
|------------ |--------------------- |-----------:|----------:|
| Parse       | heavy-nested [78]    | 1,746.5 ns |    2456 B |
| StripLength | heavy-nested [78]    |   362.4 ns |       0 B |
| Truncate    | heavy-nested [78]    |   617.4 ns |    1256 B |
| Parse       | moderate [51]        | 2,047.9 ns |    2728 B |
| StripLength | moderate [51]        |   684.1 ns |       0 B |
| Truncate    | moderate [51]        |   422.7 ns |     464 B |
| Parse       | wide-unicode [52]    | 2,829.2 ns |    4656 B |
| StripLength | wide-unicode [52]    |   980.8 ns |       0 B |
| Truncate    | wide-unicode [52]    |   385.9 ns |     416 B |
| Parse       | plain text [58]      | 4,039.6 ns |    4248 B |
| StripLength | plain text [58]      | 1,658.7 ns |       0 B |
| Truncate    | plain text [58]      |   362.2 ns |     184 B |

*Notes:* `StripLength` is zero-allocation. Interestingly, the **plain-text** `Parse`/`StripLength`
is the *slowest* of the four inputs — the parser has no fast path for untagged text, so a long
plain string costs more than shorter strings with markup. A candidate optimization.

### UnicodeWidthBenchmarks (micro)

`UnicodeWidth.GetStringWidth` over ASCII / CJK / emoji+VS16 / combining-mark strings.

| Method         | Text                 | Mean       | Allocated |
|--------------- |--------------------- |-----------:|----------:|
| GetStringWidth | emoji+vs16 [25]      |   550.7 ns |       0 B |
| GetStringWidth | combining marks [27] |   774.0 ns |       0 B |
| GetStringWidth | ascii [54]           | 1,606.5 ns |       0 B |
| GetStringWidth | cjk wide [27]        | 1,097.1 ns |       0 B |

*Notes:* Zero-allocation across the board. Cost tracks rune **count**, not display width — the
54-rune ASCII string costs more than the 27-rune CJK string despite being half the columns.

### CellDiffBenchmarks (micro, headless render diff)

Re-rendering an 80×25 window: an unchanged frame vs a small content change. Default
(no-diagnostics) render path. *(A per-`DirtyTrackingMode` comparison is intentionally omitted —
the only Line/Cell-mode test builders also enable diagnostics, which would dominate the
measurement.)*

| Method          | Mean     | Allocated | Alloc Ratio |
|---------------- |---------:|----------:|------------:|
| NoChange        | 29.19 μs |  12.83 KB |        1.00 |
| ScatteredChange | 30.57 μs |  17.05 KB |        1.33 |

*Notes:* Dirty tracking shows up in **allocations** (an unchanged frame allocates ~25% less) but
not in wall-clock at this size — the harness's per-call layout + `List<string>` snapshot dominates
the timing.

### LayoutTreeBenchmarks (macro — the headline)

`Measure` / `Arrange` / `Paint` over a balanced control tree (`ScrollablePanelControl` containers,
`MarkupControl` leaves), parameterized by depth × breadth. **`CreateSubtree` is inside the
measured region** because the real renderer rebuilds layout subtrees per frame.

| Method              | Depth | Breadth | Mean          | Allocated    |
|-------------------- |------ |-------- |--------------:|-------------:|
| Measure             | 2     | 2       |      24.79 μs |     14.91 KB |
| MeasureArrange      | 2     | 2       |      23.59 μs |     14.91 KB |
| MeasureArrangePaint | 2     | 2       |      69.10 μs |     38.60 KB |
| Measure             | 2     | 3       |      48.15 μs |     32.04 KB |
| MeasureArrangePaint | 2     | 3       |     142.33 μs |     83.57 KB |
| Measure             | 4     | 2       |     367.23 μs |    240.54 KB |
| MeasureArrangePaint | 4     | 2       |     520.30 μs |    301.13 KB |
| Measure             | 4     | 3       |   1,731.05 μs |   1156.20 KB |
| MeasureArrangePaint | 4     | 3       |   4,626.47 μs |   2877.90 KB |
| Measure             | 6     | 2       |   6,107.64 μs |   3850.54 KB |
| MeasureArrangePaint | 6     | 2       |   8,411.03 μs |   5133.80 KB |
| Measure             | 6     | 3       |  65,580.55 μs |  41625.91 KB |
| MeasureArrangePaint | 6     | 3       | 224,011.93 μs | 144024.77 KB |

*(Abridged — `MeasureArrange` tracks `Measure` closely; see the full report for every row.)*

> ⚠️ **Finding — the layout engine scales super-linearly with node count.** From (depth 4,
> breadth 3) to (depth 6, breadth 3) the node count grows ~9× but `Measure` time grows ~38×
> (1.7 ms → 65 ms) and allocation grows ~36× (1.1 MB → 41 MB). The deepest measured tree
> (depth 6, breadth 3 ≈ 729 leaves) takes **224 ms** and allocates **144 MB** for a single
> measure+arrange+paint. This is the per-frame `CreateSubtree` rebuild plus the redundant layout
> traversals that the roadmapped **ScrollLayout refactor** targets — these numbers are the
> before-baseline for that work. (Params are capped at depth 6 / breadth 3 for this reason; the
> growth is exponential in node count and super-linear in time.)

#### ScrollLayout refactor — before / after

The table above is the **pre-refactor baseline** (SPC was a self-painting container; `CreateSubtree`
stopped at each SPC, so nested SPCs were never recursed into). The ScrollLayout refactor made SPC a
real layout-tree participant — so this synthetic all-nested-SPC tree now genuinely recurses through
every level (more work by design). A first cut of the refactor was **10× slower** here; the redundant
per-pass child re-measure (`ComputeChildHeight` rebuilt each child subtree via `CreateSubtree` on
every call) was then eliminated by reusing the persistent tree nodes. After that optimization the
refactored path is **faster than the original self-painter** on the overlapping configs:

| Config (Method)        | Pre-refactor baseline | Refactored + optimized | Δ |
|------------------------|----------------------:|-----------------------:|---|
| D2/B2 Measure          |      24.79 μs / 14.9 KB |        ~22 μs / ~16 KB | ≈ parity |
| D4/B3 Measure          |   1,731 μs / 1,156 KB  |        ~442 μs / ~479 KB | **~4× faster, ~2.4× less alloc** |
| D4/B3 MeasureArrangePaint | 4,626 μs / 2,878 KB |      ~2,619 μs / ~2,688 KB | **~1.8× faster** |

(The benchmark's `Depth` is now capped at 4 — nested-SPC node count is exponential and depth-6
all-SPC trees are a pathological synthetic case, not a real-UI shape. Real layouts nest scroll panels
1–3 deep at most. Re-run `./run-benchmarks.sh --filter '*LayoutTreeBenchmarks*'` to refresh; numbers
are hardware-specific.)

### FullFrameRenderBenchmarks (macro — end-to-end)

A representative 120×40 window (header + a 12-row stack in a `ScrollablePanelControl`) rendered
via `Window.RenderAndGetVisibleContent()`.

| Method       | Mean     | Allocated |
|------------- |---------:|----------:|
| NoOpReRender | 345.5 μs | 245.95 KB |
| FullRebuild  | 374.2 μs | 245.95 KB |

*Notes:* `NoOpReRender` and `FullRebuild` cost essentially the same here. `RenderAndGetVisibleContent()`
performs a full layout + paint + snapshot pass regardless of dirty state — i.e. this **test seam**
does not exercise the dirty-tracking fast path. The live render loop (`ConsoleWindowSystem.Run()`)
gates on `shouldRender` *before* calling render, so in production an idle frame does no work; this
benchmark measures the cost of a frame *once render is invoked*, not the idle-skip path.

## Adding a benchmark

Add a `[MemoryDiagnoser]`-annotated class to `SharpConsoleUI.Benchmarks/`; `BenchmarkSwitcher`
in `Program.cs` discovers it automatically. Build heavy fixtures in `[GlobalSetup]` (see
`BenchTrees.cs`) so construction is never timed.

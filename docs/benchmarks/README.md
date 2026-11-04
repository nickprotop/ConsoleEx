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

> **Captured on:** 2026-11-04, **SharpConsoleUI v2.10.8** (`684fd7a1`). AMD Ryzen 7 8845HS
> (8 physical / 16 logical cores), Ubuntu 26.04, .NET 10.0.9 (RyuJIT x86-64-v4),
> BenchmarkDotNet v0.15.8. **`--job short`** (3 warmup + 3 iterations) — a fast pass, so the
> `Error` column is wide; treat these as order-of-magnitude baselines, not precise figures.
> Re-run with the default job for tighter numbers before drawing fine conclusions. Numbers are
> hardware-specific; only compare runs from the same machine.
>
> Pin the version when you refresh these tables. A figure with no version attached cannot be
> read as a regression or an improvement later — it is just a number from an unknown tree.

### MarkupParsingBenchmarks (micro)

`MarkupParser.Parse` / `StripLength` / `Truncate` over four input shapes.

| Method      | Markup               | Mean (v2.10.8) | Allocated |
|------------ |--------------------- |-----------:|----------:|
| Parse       | heavy-nested [78]    | 1,626.8 ns |    2456 B |
| StripLength | heavy-nested [78]    |   350.6 ns |       0 B |
| Truncate    | heavy-nested [78]    |   580.1 ns |    1256 B |
| Parse       | moderate [51]        | 1,886.1 ns |    2728 B |
| StripLength | moderate [51]        |   686.3 ns |       0 B |
| Truncate    | moderate [51]        |   412.7 ns |     464 B |
| Parse       | wide-unicode [52]    | 2,567.2 ns |    4656 B |
| StripLength | wide-unicode [52]    |   965.9 ns |       0 B |
| Truncate    | wide-unicode [52]    |   362.5 ns |     416 B |
| Parse       | plain text [58]      | 3,858.0 ns |    4248 B |
| StripLength | plain text [58]      | 1,608.8 ns |       0 B |
| Truncate    | plain text [58]      |   342.6 ns |     184 B |

*Notes:* `StripLength` is zero-allocation. Interestingly, the **plain-text** `Parse`/`StripLength`
is the *slowest* of the four inputs — the parser has no fast path for untagged text, so a long
plain string costs more than shorter strings with markup. A candidate optimization.

### UnicodeWidthBenchmarks (micro)

`UnicodeWidth.GetStringWidth` over ASCII / CJK / emoji+VS16 / combining-mark strings.

| Method         | Text                 | Mean       | Allocated |
|--------------- |--------------------- |-----------:|----------:|
| GetStringWidth | emoji+vs16 [25]      |   506.5 ns |       0 B |
| GetStringWidth | combining marks [27] |   742.3 ns |       0 B |
| GetStringWidth | ascii [54]           | 1,479.0 ns |       0 B |
| GetStringWidth | cjk wide [27]        | 1,017.5 ns |       0 B |

*Notes:* Zero-allocation across the board. Cost tracks rune **count**, not display width — the
54-rune ASCII string costs more than the 27-rune CJK string despite being half the columns.

### CellDiffBenchmarks (micro, headless render diff)

Re-rendering an 80×25 window: an unchanged frame vs a small content change. Default
(no-diagnostics) render path. *(A per-`DirtyTrackingMode` comparison is intentionally omitted —
the only Line/Cell-mode test builders also enable diagnostics, which would dominate the
measurement.)*

| Method          | Mean     | Allocated | Alloc Ratio |
|---------------- |---------:|----------:|------------:|
| NoChange        | 22.13 μs |   7.99 KB |        1.00 |
| ScatteredChange | 28.46 μs |  12.88 KB |        1.61 |

*Notes:* Dirty tracking shows up in **allocations** (an unchanged frame allocates ~25% less) but
not in wall-clock at this size — the harness's per-call layout + `List<string>` snapshot dominates
the timing.

### LayoutTreeBenchmarks (macro — the headline)

`Measure` / `Arrange` / `Paint` over a balanced control tree (`ScrollablePanelControl` containers,
`MarkupControl` leaves), parameterized by depth × breadth. **`CreateSubtree` is inside the
measured region** because the real renderer rebuilds layout subtrees per frame.

| Method              | Depth | Breadth | Mean          | Allocated    |
|-------------------- |------ |-------- |--------------:|-------------:|
| Measure             | 2     | 2       |       6.60 μs |      4.16 KB |
| MeasureArrange      | 2     | 2       |       7.17 μs |      5.92 KB |
| MeasureArrangePaint | 2     | 2       |      35.52 μs |     18.70 KB |
| Measure             | 2     | 3       |      12.33 μs |      6.73 KB |
| MeasureArrangePaint | 2     | 3       |      72.09 μs |     32.66 KB |
| Measure             | 3     | 2       |      28.46 μs |     18.48 KB |
| MeasureArrangePaint | 3     | 2       |     200.29 μs |    113.73 KB |
| Measure             | 3     | 3       |      84.94 μs |     42.22 KB |
| MeasureArrangePaint | 3     | 3       |     604.13 μs |    277.63 KB |
| Measure             | 4     | 2       |     228.08 μs |    136.85 KB |
| MeasureArrangePaint | 4     | 2       |   1,586.82 μs |    860.87 KB |
| Measure             | 4     | 3       |   1,040.39 μs |    469.37 KB |
| MeasureArrangePaint | 4     | 3       |   5,998.06 μs |   2656.04 KB |

*(Abridged — `MeasureArrange` tracks `Measure` closely; see the full report for every row.)*

> ⚠️ **Finding — the layout engine scales super-linearly with node count.** Leaves grow as
> `Breadth^Depth`, and time grows faster. From (depth 3, breadth 3) to (depth 4, breadth 3) the
> node count grows ~3× but `Measure` time grows ~12× (85 μs → 1,040 μs) and
> `MeasureArrangePaint` ~10× (604 μs → 5,998 μs). Paint dominates throughout: it is 5-7× the
> cost of measure alone at every size. This is the per-frame `CreateSubtree` rebuild plus the
> redundant layout
> traversals that the roadmapped **ScrollLayout refactor** targets — these numbers are the
> before-baseline for that work. (Params are capped at depth 6 / breadth 3 for this reason; the
> growth is exponential in node count and super-linear in time.)

#### ScrollLayout refactor — before / after (historical)

> The figures in this subsection are from the refactor itself and are **not** the current
> numbers. They are kept because they record why the engine looks the way it does. The table
> above is what master measures today.

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

### MarkupAppendBenchmarks (macro — the append-heavy shape)

The cost of **one** line arriving in a `MarkupControl` that already holds a scrollback of N lines:
the shape every append-heavy view has (chat transcript, log pane, terminal output). `NoOpRepaint`
is the control — an unchanged frame is a pure cache hit. `AppendOneLine` is the same repaint with
one line added first, so the gap between them is the re-parse. `ParsesPerAppend` reports that gap
as a count via `MarkupControl.TotalParseCount`, which does not move with machine or noise.

| Lines  | NoOpRepaint        | AppendOneLine        | Alloc ratio |
|------- |-------------------:|---------------------:|------------:|
| 1,000  |  681.9 μs / 26.58 KB |  2,698.1 μs /  260.47 KB |   9.8× |
| 5,000  |  681.8 μs / 26.58 KB |  7,517.0 μs / 1599.39 KB |  60.2× |
| 20,000 |  687.5 μs / 26.58 KB | 16,525.1 μs / 6202.86 KB | 233.4× |

> ⚠️ **Finding — an append costs the whole buffer, not the line.** `NoOpRepaint` is flat in N
> (26.58 KB at every size — the cache works), but one appended line into a 20,000-line control
> costs **16.5 ms and 6.2 MB**, 233× the no-op. The parse cache is keyed on a content version
> that every mutator bumps, so appending invalidates the parse of the entire buffer; the
> multiplier is ~2N because the scroll-panel overflow probe measures children at two widths per
> layout pass. This is the cost a long-running transcript pays on every token, and it is the
> case `ScrollablePanelControl.VerticalScrollOffsetChanged` (2.10.8) exists to let a consumer
> virtualize away.

### FullFrameRenderBenchmarks (macro — end-to-end)

A representative 120×40 window (header + a 12-row stack in a `ScrollablePanelControl`) rendered
via `Window.RenderAndGetVisibleContent()`.

| Method       | Mean      | Allocated |
|------------- |----------:|----------:|
| NoOpReRender |  73.61 μs |  20.68 KB |
| FullRebuild  |  76.15 μs |  22.55 KB |

*Notes:* `NoOpReRender` and `FullRebuild` cost nearly the same here (the rebuild adds ~3% time and
~9% allocation). `RenderAndGetVisibleContent()`
performs a full layout + paint + snapshot pass regardless of dirty state — i.e. this **test seam**
does not exercise the dirty-tracking fast path. The live render loop (`ConsoleWindowSystem.Run()`)
gates on `shouldRender` *before* calling render, so in production an idle frame does no work; this
benchmark measures the cost of a frame *once render is invoked*, not the idle-skip path.

## Adding a benchmark

Add a `[MemoryDiagnoser]`-annotated class to `SharpConsoleUI.Benchmarks/`; `BenchmarkSwitcher`
in `Program.cs` discovers it automatically. Build heavy fixtures in `[GlobalSetup]` (see
`BenchTrees.cs`) so construction is never timed.

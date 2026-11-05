# SharpConsoleUI Benchmarks

Headless, deterministic [BenchmarkDotNet](https://benchmarkdotnet.org/) measurements of the
core hot paths and the layout engine. Their purpose is **regression tracking** — comparable
run-to-run so a slowdown in a hot path shows up in a diff — and giving an optimization a number
to argue from. They have already earned that twice: the ScrollLayout refactor was measured here
before and after, and the per-child quadratic in `ScrollBenchmarks` was found by profiling a row
of this suite that looked wrong.

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

> **Captured on:** 2026-11-05, **SharpConsoleUI v2.10.8** (`c7fb3968`). AMD Ryzen 7 8845HS
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
| Parse       | heavy-nested [78]    | 1,634.2 ns |    2456 B |
| StripLength | heavy-nested [78]    |   358.5 ns |       0 B |
| Truncate    | heavy-nested [78]    |   574.9 ns |    1256 B |
| Parse       | moderate [51]        | 1,868.5 ns |    2728 B |
| StripLength | moderate [51]        |   660.9 ns |       0 B |
| Truncate    | moderate [51]        |   409.1 ns |     464 B |
| Parse       | wide-unicode [52]    | 2,722.3 ns |    4656 B |
| StripLength | wide-unicode [52]    |   950.2 ns |       0 B |
| Truncate    | wide-unicode [52]    |   361.9 ns |     416 B |
| Parse       | plain text [58]      | 3,802.0 ns |    4248 B |
| StripLength | plain text [58]      | 1,592.0 ns |       0 B |
| Truncate    | plain text [58]      |   336.5 ns |     184 B |

*Notes:* `StripLength` is zero-allocation. The **plain-text** row has the largest absolute
`Parse`/`StripLength` time, but that is an artifact of the inputs not being length-matched: it is
the longest string of the four in visible characters. **Per visible character, plain text is the
FASTEST path** (~66.5 ns/char against ~162.7 for heavy-nested), which is what you would expect —
there are no tags to open or close. Do not read this table as "untagged text needs a fast path";
an earlier revision did, and it was wrong.

### UnicodeWidthBenchmarks (micro)

`UnicodeWidth.GetStringWidth` over ASCII / CJK / emoji+VS16 / combining-mark strings.

| Method         | Text                 | Mean       | Allocated |
|--------------- |--------------------- |-----------:|----------:|
| GetStringWidth | emoji+vs16 [25]      |   502.3 ns |       0 B |
| GetStringWidth | combining marks [27] |   732.6 ns |       0 B |
| GetStringWidth | ascii [54]           | 1,467.0 ns |       0 B |
| GetStringWidth | cjk wide [27]        |   996.8 ns |       0 B |

*Notes:* Zero-allocation across the board. Cost tracks rune **count**, not display width — the
54-rune ASCII string costs more than the 27-rune CJK string despite being half the columns.

### CellDiffBenchmarks (micro, headless render diff)

Re-rendering an 80×25 window: an unchanged frame vs a small content change. Default
(no-diagnostics) render path. *(A per-`DirtyTrackingMode` comparison is intentionally omitted —
the only Line/Cell-mode test builders also enable diagnostics, which would dominate the
measurement.)*

| Method          | Mean     | Allocated | Alloc Ratio |
|---------------- |---------:|----------:|------------:|
| NoChange        | 22.67 μs |   7.99 KB |        1.00 |
| ScatteredChange | 29.44 μs |  12.68 KB |        1.59 |

*Notes:* Dirty tracking shows up in **allocations** (an unchanged frame allocates ~25% less) but
not in wall-clock at this size — the harness's per-call layout + `List<string>` snapshot dominates
the timing.

### LayoutTreeBenchmarks (macro — the headline)

`Measure` / `Arrange` / `Paint` over a balanced control tree (`ScrollablePanelControl` containers,
`MarkupControl` leaves), parameterized by depth × breadth. **`CreateSubtree` is inside the
measured region** because the real renderer rebuilds layout subtrees per frame.

| Method              | Depth | Breadth | Mean          | Allocated    |
|-------------------- |------ |-------- |--------------:|-------------:|
| Measure             | 2     | 2       |       6.58 μs |      4.16 KB |
| MeasureArrange      | 2     | 2       |       7.17 μs |      5.92 KB |
| MeasureArrangePaint | 2     | 2       |      35.48 μs |     18.28 KB |
| Measure             | 2     | 3       |      12.35 μs |      6.73 KB |
| MeasureArrangePaint | 2     | 3       |      72.38 μs |     31.72 KB |
| Measure             | 3     | 2       |      28.23 μs |     18.48 KB |
| MeasureArrangePaint | 3     | 2       |     200.68 μs |    112.75 KB |
| Measure             | 3     | 3       |      84.09 μs |     42.22 KB |
| MeasureArrangePaint | 3     | 3       |     648.06 μs |    276.10 KB |
| Measure             | 4     | 2       |     232.62 μs |    136.85 KB |
| MeasureArrangePaint | 4     | 2       |   1,600.50 μs |    859.93 KB |
| Measure             | 4     | 3       |   1,036.33 μs |    469.37 KB |
| MeasureArrangePaint | 4     | 3       |   5,985.49 μs |   2653.46 KB |

*(Abridged — `MeasureArrange` tracks `Measure` closely; see the full report for every row.)*

> ⚠️ **Finding — the layout engine scales super-linearly with node count.** Leaves grow as
> `Breadth^Depth`, and time grows faster. From (depth 3, breadth 3) to (depth 4, breadth 3) the
> node count grows ~3× but `Measure` time grows ~12× (84 μs → 1,036 μs) and
> `MeasureArrangePaint` ~9× (648 μs → 5,985 μs). Paint dominates throughout: it is 5-7× the
> cost of measure alone at every size. This is the per-frame `CreateSubtree` rebuild plus
> redundant layout traversals. (Params are capped at depth 4 / breadth 3 for this reason; the
> growth is exponential in node count and super-linear in time.)
>
> **This table did not move when the scroll-panel quadratic was fixed** (`1a1db9de`), and that is
> informative rather than disappointing. This tree is all-nested-`ScrollablePanelControl`, but
> with only 2–3 children per node: the per-child paint hook that was quadratic needs MANY children
> under ONE panel before it compounds, which is the `ScrollBenchmarks` shape (2,000 children), not
> this one. The super-linearity above is a separate, still-open problem — it is dominated by
> `CreateSubtree` rebuilding subtrees per frame, not by allocation in the paint walk.

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
| 1,000  |  683.3 μs / 26.58 KB |  2,716.6 μs /  189.07 KB |   7.1× |
| 5,000  |  679.7 μs / 26.58 KB |  7,167.2 μs /  831.10 KB |  31.3× |
| 20,000 |  685.3 μs / 26.58 KB | 16,949.1 μs / 3133.03 KB | 117.9× |

> ⚠️ **Finding — an append costs the whole buffer, not the line.** `NoOpRepaint` is flat in N
> (26.58 KB at every size — the cache works), but one appended line into a 20,000-line control
> costs **16.9 ms and 3.1 MB**, 118× the no-op. The parse cache is keyed on a content version
> that every mutator bumps, so appending invalidates the parse of the entire buffer; the
> multiplier is ~2N because the scroll-panel overflow probe measures children at two widths per
> layout pass. This is the cost a long-running transcript pays on every token, and it is the
> case `ScrollablePanelControl.VerticalScrollOffsetChanged` (2.10.8) exists to let a consumer
> virtualize away.
>
> The allocation halved at 2026-11-05 (`c7fb3968`, which pre-sized the row assembly lists) and
> the **time did not move at all** — that is the useful part of the result. This benchmark adds
> its `MarkupControl` straight to the window with no scroll panel, so the change is cleanly
> attributable: the lists were garbage, not work. The ratio falling 233× → 118× measures less GC
> pressure, not a cheaper append. The whole-buffer re-parse above is untouched and is still the
> thing to fix.

### ScrollBenchmarks (macro — moving a viewport over unchanged content)

Scrolling a long markup document: a chat transcript, a log pane, a file view. The parse cache
stays warm throughout, so this measures layout + paint + diff, never parsing. `NoScrollReRender`
is the control — the same window re-rendered without moving.

| Method           | Lines | Mean        | Gen1    | Allocated | Alloc ratio |
|----------------- |------ |------------:|--------:|----------:|------------:|
| ScrollByOneLine  | 200   |    554.2 μs |       - | 106.52 KB |       1.51× |
| ScrollPageDown   | 200   |    559.8 μs |       - | 105.99 KB |       1.50× |
| NoScrollReRender | 200   |    366.3 μs |  0.4883 |  70.61 KB |       1.00× |
| ScrollByOneLine  | 2,000 | 18,802.8 μs | 15.6250 | 765.21 KB |       1.85× |
| ScrollPageDown   | 2,000 | 18,366.1 μs | 15.6250 | 764.65 KB |       1.85× |
| NoScrollReRender | 2,000 | 15,892.3 μs |       - | 414.01 KB |       1.00× |

> ✅ **Fixed 2026-11-05 (`1a1db9de`) — the paint walk was quadratic in child count.**
> `ScrollLayout.GetPaintClipRect` runs once per child, including the ones scrolled out of view,
> and it called `Bands()`, which materializes a list of every child. N children each building an
> N-entry list made one frame O(N²) in allocation. It only ever needed the pinned band row
> counts, never the body list, so those are now summed without allocating.
>
> | At 2,000 lines | Before | After | |
> |---|---:|---:|---|
> | ScrollByOneLine | 32,046.7 KB | 765.2 KB | **41.9× less** |
> | NoScrollReRender | 31,695.6 KB | 414.0 KB | **76.6× less** |
> | Gen1 collections | 3,875 | 15.6 | **248× fewer** |
>
> Per child per scroll stopped growing with the panel: it was 2,780 B at 250 children rising to
> 32,342 B at 4,000, and is now 741 B falling to 325 B. At 4,000 children the saving is 99.5×.
>
> ⚠️ **Wall clock moved only ~10% (20.7 → 18.8 ms).** That gap is the standing finding here: this
> path is **work-bound, not allocation-bound**. The 32 MB was pure garbage, so removing it buys
> far fewer GC pauses (what a long session feels as the panel staying smooth) but not a cheaper
> frame. The remaining ~15.9 ms of `NoScrollReRender` at 2,000 children is the real ceiling, and
> it is per-node layout traversal, not allocation. Guarded by
> `SharpConsoleUI.Tests/Layout/ScrollPaintClipScalingTests.cs`, which asserts the complexity
> class rather than a byte budget.

### StreamingAppendBenchmarks (macro — a reply arriving token by token)

A model reply streaming into the LAST line of a transcript: the line is rewritten once per
token rather than a new line being added, so the buffer changes many times per visible line.
`TokensPerBurst = 10`.

| Scrollback | NoOpRepaint         | StreamTokenBurst        | Alloc ratio |
|----------- |--------------------:|------------------------:|------------:|
| 200        |  858.7 μs / 41.33 KB | 12,280.4 μs /  803.27 KB |      19.4× |
| 2,000      |  824.9 μs / 41.33 KB | 38,801.1 μs / 3818.51 KB |      92.4× |

> ⚠️ **Finding — the per-token cost grows with the history above the cursor.** Ten tokens into a
> 200-line transcript cost 12.3 ms; the same ten into a 2,000-line one cost 38.8 ms and 4.7× the
> allocation. A session therefore gets slower the longer it runs, which is the same whole-buffer
> re-parse `MarkupAppendBenchmarks` documents, paid per token instead of per line. Pre-sizing the
> assembly lists (`c7fb3968`) cut allocation 21–28% here with **no** change in time, which is the
> clean isolated measurement of that change: it removes garbage, not work.

### FullFrameRenderBenchmarks (macro — end-to-end)

A representative 120×40 window (header + a 12-row stack in a `ScrollablePanelControl`) rendered
via `Window.RenderAndGetVisibleContent()`.

| Method       | Mean      | Allocated |
|------------- |----------:|----------:|
| NoOpReRender |  74.01 μs |  19.37 KB |
| FullRebuild  |  74.59 μs |  21.24 KB |

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

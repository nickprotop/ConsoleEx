# Code Quality Enforcement Plan

The standards in [`CODE_QUALITY.md`](CODE_QUALITY.md) are currently enforced by **review
plus advisory CI** — the `Code Quality (advisory)` workflow reports findings but never
fails a PR. This document is the staged plan to turn the mechanical checks into **blocking
gates**. The phases are ordered so the build is never broken mid-migration.

## Current enforcement status

| Signal | Status | Notes |
|--------|--------|-------|
| Formatting (`dotnet format`) | ✅ **Blocking** | Codebase normalized to tabs per `.editorconfig`; build is format-clean. |
| License-header banner | ✅ **Blocking** | All 473 source files carry the banner. |
| Analyzer/compiler warnings | ✅ **Blocking** | Library builds warning-clean (CA catalog disabled via `AnalysisMode=None`; the library enforces its own targeted rules, not the full MS set). |
| Oversized files (>800 lines) | ⚠️ **Advisory** | 26 files remain over the limit; splitting is real refactoring (Phase 4). |
| Multi-TFM PR matrix | ⏳ Planned | Phase 5. |

The first three were brought to a clean state and flipped to blocking. File size stays
advisory until the remaining files are split.

## What's machine-checkable vs review-only

- **Machine-checkable (target of this plan):** formatting, license headers, compiler/
  analyzer warnings, file-size limits, trailing whitespace.
- **Review-only (cannot be automated):** no magic numbers, no duplication, correct
  `SetCell`/`SetNarrowCell` usage, display-width correctness, UI-thread marshalling,
  no-breaking-changes. These stay in human review — the PR template checklist exists to
  prompt them.

## Phases

### Phase 0 — Baseline ✅ done
- `.editorconfig` added (tabs, whitespace, analyzers at suggestion severity).
- `EnableNETAnalyzers` + `EnforceCodeStyleInBuild` on; `AnalysisMode=None` so the broad CA
  catalog doesn't escalate to build warnings.
- `code-quality.yml` workflow checks formatting / headers / warnings / file size.

### Phase 1 — Formatting gate ✅ done
- `dotnet format` applied across the library (normalized ~194 space-indented files to
  tabs; whitespace-only, all tests green). Formatting step is now blocking.

### Phase 2 — License-header gate ✅ done
- Banner backfilled into the 108 files that lacked it. Header check is now blocking.

### Phase 3 — Warning burn-down ✅ done
- Real CS warnings fixed (CS1573 docs; safe analyzer auto-fixes). Public-API CS0067/CS0414
  silenced with justification in `.editorconfig`. CA catalog disabled via `AnalysisMode=None`.
  Build is warning-clean and the warning check is blocking.

### Phase 4 — File-size gate (remaining)
1. Split the remaining oversized controls into Model / Renderer / InputHandler partials.
2. Make the file-size check blocking.

### Phase 5 — Multi-TFM PR matrix (related)
- Build + test across `net8.0`/`net9.0`/`net10.0` on PRs so a change can't silently break
  a target framework.

## Notes
- Each phase is independently shippable and keeps `main` green.
- No phase changes public API or runtime behavior — these are build/CI/style only.

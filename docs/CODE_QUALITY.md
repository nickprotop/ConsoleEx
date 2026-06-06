# Code Quality Standards

These are the rules SharpConsoleUI code is held to in review. They exist because
violating them has caused real bugs and technical debt in the past. Please read this
before opening a pull request — PRs are reviewed against these standards.

Rules fall into three groups: **hard rules** (a PR will be asked to change), **machine
checks** (flagged by CI tooling), and **review guidance** (judgment, discussed in review).

---

## 1. No breaking changes (the most important rule)

SharpConsoleUI has real third-party users who depend on it via NuGet. **Backward
compatibility is mandatory.**

- **Never** remove or rename a public API, control property, event, or builder method.
- **Never** change the signature or default behavior of an existing public member.
- **Always** add a new overload instead of changing an existing one.
- Existing user code must continue to compile and behave identically after your change.

Treat any removal or behavior change of public surface area as a breaking change that
requires explicit maintainer approval before it can be considered.

## 2. Never write to the console from library code

`Console.WriteLine()` / `Console.Write()` / `Console.Clear()` corrupt the UI rendering.
Use the built-in `LogService` (file/debug logging) instead. This applies to all library
code; the one-shot/classic command paths that run *before* the window system starts are
the only exception.

## 3. No magic numbers

Every literal number (except `0`, `1`, `-1`) must be a named constant in
`Configuration/ControlDefaults.cs` or `Configuration/LayoutDefaults.cs`.

## 4. No code duplication

If similar logic appears in 2+ files, extract it to a helper in `Helpers/`.

## 5. File size limits

- Simple controls: ≤ 500 lines.
- Complex controls (List, Tree, Table): ≤ 800 lines — split into Model / Renderer /
  InputHandler partials beyond that.
- Helper classes: ≤ 300 lines. Service classes: ≤ 600 lines.

## 6. Unicode-aware rendering

When rendering text to a `CharacterBuffer`:

- Use `SetCell(x, y, Cell)` for parsed or copied cells (preserves wide-continuation /
  combiner / decoration flags). Use `SetNarrowCell(...)` only for literal narrow chars
  (borders, padding, fill).
- Never use `string.Length` for display width — use `UnicodeWidth.GetStringWidth(...)`
  or `MarkupParser.StripLength(...)`.
- Never index a string by char position for rendering — parse to cells first.
- Use the narrow indicator symbols in `ControlDefaults` (e.g. `▾▴`, `◄►`), never the
  East-Asian-ambiguous `▼▲◆●`.

See [`docs/THREADING_AND_ASYNC.md`](THREADING_AND_ASYNC.md) and the control source for
worked examples.

## 7. Thread safety — marshal UI mutations to the UI thread

Never modify `Window`/control state from a background thread (`Task.Run`, timer
callbacks, async continuations). The render loop reads that state concurrently. Marshal
back with `EnqueueOnUIThread(...)` / `InvokeAsync(...)`. `Container?.Invalidate(true)` is
the only call safe to make directly from a background thread.

## 8. Other review guidance

- Cache expensive per-frame operations; don't recompute width/layout every frame.
- Use `StringBuilder` for repeated string building, not `+=` in a loop.
- Don't fire the same event twice or call `Invalidate()` redundantly — guard with state
  checks and fire once at the end.
- Match the surrounding code's style: tabs for indentation, fluent builders, the
  file-header banner (Author / Email / License) at the top of new source files.
- No typos in public API names.

---

## Tooling

Run before opening a PR:

```bash
dotnet build SharpConsoleUI/SharpConsoleUI.csproj -c Release   # must build clean
dotnet test  SharpConsoleUI.Tests/SharpConsoleUI.Tests.csproj -c Release   # all tests pass
dotnet format --verify-no-changes                              # formatting matches .editorconfig
```

CI runs **code-quality checks** on every PR. Formatting, license headers, and a
warning-clean build are **blocking** — a PR that regresses any of them fails. File size
is advisory (annotates only) until the remaining oversized files are split. See
[`docs/CODE_QUALITY_ENFORCEMENT.md`](CODE_QUALITY_ENFORCEMENT.md) for the enforcement
status and remaining phases.

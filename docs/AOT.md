# NativeAOT Compatibility

SharpConsoleUI is **NativeAOT-compatible**. The library is marked `<IsAotCompatible>true</IsAotCompatible>`, which turns on the full trim / AOT / single-file analyzer suite at build time, so AOT-hostile code in the library is flagged during compilation rather than failing at runtime.

This page explains what that guarantee covers, how it is verified, and the one caveat (`HtmlControl`).

## What "AOT-compatible" means here

- **The library builds analyzer-clean.** A Release build of `SharpConsoleUI` produces **zero `IL` (trim/AOT/single-file) warnings**.
- **No quarantined APIs.** There are no `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` attributes in the public surface — the library doesn't have to wall off any feature as AOT-unsafe.
- **A real native binary using the library runs.** A smoke-test app is published with `PublishAot=true` and executed headlessly in CI on every push (see below).

It does **not** mean your application is automatically AOT-compatible. If your own code uses reflection, dynamic code generation, or reflection-based serialization, that is still your responsibility — but SharpConsoleUI itself will not be the thing that breaks your AOT publish (with the single documented exception below).

## How it's verified

The repository ships a NativeAOT regression fixture at `SharpConsoleUI.Tests/aot.test/` (project `AotSmoke`). It is **not a usage example** — it's a CI gate.

`AotSmoke.csproj` sets:

```xml
<PublishAot>true</PublishAot>
<IlcTreatWarningsAsErrors>true</IlcTreatWarningsAsErrors>
```

so any trim/AOT `IL` warning in a reachable code path **fails the publish**. The CI `aot` job (`.github/workflows/code-quality.yml`):

1. Publishes `AotSmoke` as a native binary (`dotnet publish -p:PublishAot=true -r linux-x64`).
2. Runs the native binary headlessly and checks it prints `AOT SMOKE OK`.

If either step fails, CI fails.

### What the smoke test exercises

The smoke test instantiates and renders the broad control surface under the native binary — the full control set (Markup, Button, Checkbox, List, Tree, Table, Tab, Dropdown, ProgressBar, Sparkline, BarGraph, LineGraph, Slider, RangeSlider, Date/Time pickers, Menu, Toolbar, StatusBar, Prompt, MultilineEdit, NavigationView, Figlet, LogViewer, containers, Canvas primitives, …) plus the heavy dependency-backed paths that are the real AOT risk:

- the `[markdown]` tag (Markdig) including a syntax-highlighted code block,
- syntax highlighting (`SyntaxHighlighters.For(...)`), including a TextMate grammar loaded lazily and its native Oniguruma regex engine,
- `SpectreRenderableControl` (Spectre.Console),
- `ImageControl` decoding an in-memory image (StbImageSharp),
- `HtmlControl` rendering HTML with CSS `calc()` (AngleSharp + AngleSharp.Css),
- the **data-binding engine** — a one-way `Bind` and a two-way `BindTwoWay` driven by `INotifyPropertyChanged`. These compile member-access `Expression<Func<>>` trees via `LambdaExpression.Compile()`; under NativeAOT (`IsDynamicCodeSupported=false`) `System.Linq.Expressions` transparently falls back to its **interpreter** instead of `Reflection.Emit`, so the path is AOT-reachable and correct. The smoke test asserts the bound value is actually applied, so a silent no-op would fail the gate.
- the **`[gradient=…]` markup tag** — `MarkupParser.Parse` → `ColorGradient.Parse` → `typeof(Color).GetProperty(name, …)`. Because `typeof(Color)` is a closed type *literal* (not `object.GetType()`), trim analysis preserves `Color`'s static color properties and the named-color lookup resolves under NativeAOT (the smoke test verifies the interpolated color is non-default).

Reaching `AOT SMOKE OK` without an exception proves these paths run under NativeAOT.

**Subprocess-backed controls are actually started**, not just constructed: `TerminalControl` opens a PTY and spawns a shell (`/bin/sh` on Linux), and `VideoControl.Play()` is invoked (starting its background decode loop). The goal is to prove the native binary can *invoke* those code paths without an AOT/reflection failure — not that a PTY or FFmpeg is present. Each is wrapped in a try/catch that classifies the exception: an AOT/reflection/trim signal fails the gate, while a missing environment (no PTY, FFmpeg absent) is logged as an `environment-skipped` note and the run continues. Whatever is started is stopped/disposed so no subprocess or read thread is orphaned.

## HtmlControl

`HtmlControl` works under NativeAOT with nothing to add to your build. The smoke test
renders HTML with CSS `calc()` in a published native binary and runs correctly.

It did need a workaround until `AngleSharp.Css` 1.2.2. That version evaluates CSS `calc()`
without the `Activator.CreateInstance(value.GetType(), ...)` call that trim analysis
reported as four `IL2072` warnings from inside the dependency, so the scoped MSBuild
targets that collapsed them are gone from the smoke fixture and are not needed in your
project either.

If you are pinned to an older `AngleSharp.Css` and your build treats trim warnings as
errors, upgrading is the fix.

## Tips for AOT-publishing your own app

- Keep your own code reflection-free, or annotate it with the standard `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` attributes.
- For JSON, use `System.Text.Json` source generators (`JsonTypeInfo<T>`); the library's registry has AOT-safe overloads — see [Registry](REGISTRY.md).
- Set `<IsAotCompatible>true</IsAotCompatible>` in your own project to get the analyzers, then fix warnings as they appear.
- Test with an actual `dotnet publish -p:PublishAot=true` run, not just a build — some issues only surface during native compilation.

## See Also

- [HtmlControl](controls/HtmlControl.md) — the AOT caveat in control-specific context
- [Registry](REGISTRY.md) — AOT-safe JSON storage with source generation
- [Controls Reference](CONTROLS.md) — complete control listing

[Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)

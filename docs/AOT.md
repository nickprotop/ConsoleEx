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
- the built-in syntax highlighters (`SyntaxHighlighters.For(...)`),
- `SpectreRenderableControl` (Spectre.Console),
- `ImageControl` decoding an in-memory image (ImageSharp),
- `HtmlControl` rendering HTML with CSS `calc()` (AngleSharp + AngleSharp.Css),
- the **data-binding engine** — a one-way `Bind` and a two-way `BindTwoWay` driven by `INotifyPropertyChanged`. These compile member-access `Expression<Func<>>` trees via `LambdaExpression.Compile()`; under NativeAOT (`IsDynamicCodeSupported=false`) `System.Linq.Expressions` transparently falls back to its **interpreter** instead of `Reflection.Emit`, so the path is AOT-reachable and correct. The smoke test asserts the bound value is actually applied, so a silent no-op would fail the gate.
- the **`[gradient=…]` markup tag** — `MarkupParser.Parse` → `ColorGradient.Parse` → `typeof(Color).GetProperty(name, …)`. Because `typeof(Color)` is a closed type *literal* (not `object.GetType()`), trim analysis preserves `Color`'s static color properties and the named-color lookup resolves under NativeAOT (the smoke test verifies the interpolated color is non-default).

Reaching `AOT SMOKE OK` without an exception proves these paths run under NativeAOT.

**Subprocess-backed controls are actually started**, not just constructed: `TerminalControl` opens a PTY and spawns a shell (`/bin/sh` on Linux), and `VideoControl.Play()` is invoked (starting its background decode loop). The goal is to prove the native binary can *invoke* those code paths without an AOT/reflection failure — not that a PTY or FFmpeg is present. Each is wrapped in a try/catch that classifies the exception: an AOT/reflection/trim signal fails the gate, while a missing environment (no PTY, FFmpeg absent) is logged as an `environment-skipped` note and the run continues. Whatever is started is stopped/disposed so no subprocess or read thread is orphaned.

## HtmlControl caveat

`HtmlControl` **works** under NativeAOT (the smoke test renders HTML with CSS `calc()` and it runs correctly), but it is the one path that needs a build-time workaround.

**Why:** `AngleSharp.Css` evaluates CSS `calc()` expressions with `Activator.CreateInstance(value.GetType(), …)`. The trimmer can't statically prove the runtime `Type` keeps its constructors, so it emits **4 `IL2072` warnings** from inside `AngleSharp.Css` (not from SharpConsoleUI). At runtime the relevant expression types survive trimming, so it works — the warnings are an analysis limitation, not a failure.

**Upstream status:** `AngleSharp.Css` has no stable 1.x release (the entire `1.0.0` line is beta; the last stable is the old `0.17.0` series), and no recent version has addressed the reflection/trim issue. So there is no "just upgrade" fix today.

**If you AOT-publish an app that uses `HtmlControl`** and your build treats trim warnings as errors, collapse that one dependency's warnings with two scoped MSBuild targets in your `.csproj`. This leaves full `IL2072`-as-error analysis intact for your own code and every other assembly:

```xml
<!-- Collapse AngleSharp.Css's CSS calc() trim warnings (verified to run correctly under
     NativeAOT) into a single per-assembly note, then stop that note failing the publish.
     Scoped to AngleSharp.Css alone. -->
<Target Name="_ScopeAngleSharpCssTrimWarnings"
        AfterTargets="_ComputeManagedAssemblyForILLink"
        Condition="'$(NativeCompilationDuringPublish)' == 'true'">
  <ItemGroup>
    <ManagedAssemblyToLink Condition="'%(Filename)' == 'AngleSharp.Css'">
      <TrimmerSingleWarn>true</TrimmerSingleWarn>
    </ManagedAssemblyToLink>
  </ItemGroup>
</Target>

<Target Name="_SuppressAngleSharpCssSingleWarn"
        BeforeTargets="WriteIlcRspFileForCompilation"
        Condition="'$(NativeCompilationDuringPublish)' == 'true'">
  <ItemGroup>
    <IlcArg Include="--nowarnaserr:IL2104" />
    <IlcArg Include="--nowarn:IL2104" />
  </ItemGroup>
</Target>
```

This is exactly what the library's own AOT smoke test uses — see `SharpConsoleUI.Tests/aot.test/AotSmoke.csproj`.

**If you don't use `HtmlControl`,** none of this applies — the rest of the library is analyzer-clean with no action needed.

> A C# `[UnconditionalSuppressMessage("Trimming", "IL2072")]` attribute does **not** help here: it only suppresses warnings attributed to the annotated member, and these are attributed to AngleSharp's own methods. Cross-assembly suppression (attribute `Scope`/`Target`, or an `ILLink.LinkAttributes.xml` targeting another assembly) is not honored by the current ILC, which is why the fix is the MSBuild/ILC-arg approach above.

## Tips for AOT-publishing your own app

- Keep your own code reflection-free, or annotate it with the standard `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` attributes.
- For JSON, use `System.Text.Json` source generators (`JsonTypeInfo<T>`); the library's registry has AOT-safe overloads — see [REGISTRY.md](REGISTRY.md).
- Set `<IsAotCompatible>true</IsAotCompatible>` in your own project to get the analyzers, then fix warnings as they appear.
- Test with an actual `dotnet publish -p:PublishAot=true` run, not just a build — some issues only surface during native compilation.

## See Also

- [HtmlControl](controls/HtmlControl.md) — the AOT caveat in control-specific context
- [Registry](REGISTRY.md) — AOT-safe JSON storage with source generation
- [Controls Reference](CONTROLS.md) — complete control listing

[Back to Main Documentation](../README.md)

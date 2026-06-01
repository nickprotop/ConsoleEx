# SpinnerControl

An animated indeterminate-progress spinner that cycles through a set of glyph frames on a fixed interval. Built on the window system's animation manager, so it runs smoothly alongside all other animations with no blocking calls.

## Overview

SpinnerControl is the control form of the spinner feature. The same animation subsystem is also exposed as:

- `Controls.Spinner()` — fluent builder (recommended)
- `SpinnerTextAnimator` — drives any `Action<string>` setter (status bar labels, window titles)
- `[spinner]` inline markup tag — embed a spinner in any markup string

All four forms share the same `SpinnerStyle` presets and default 100 ms frame interval.

## Basic Usage

```csharp
// Default Braille spinner
window.AddControl(Controls.Spinner().Build());

// Yellow circle spinner
window.AddControl(
    Controls.Spinner()
        .WithStyle(SpinnerStyle.Circle)
        .WithColor(Color.Yellow)
        .Build()
);

// Slow dots spinner
window.AddControl(
    Controls.Spinner()
        .WithStyle(SpinnerStyle.Dots)
        .WithInterval(300)
        .Build()
);
```

The spinner starts animating automatically when added to a window. Call `Stop()` / `Start()` to pause and resume at any time.

## Styles

| `SpinnerStyle` | Glyphs | Width |
|----------------|--------|-------|
| `Braille` (default) | ⣷⣯⣟⡿⢿⣻⣽⣾ | 1 column |
| `Circle` | ◐◓◑◒ | 1 column |
| `Dots` | `.  ` / `.. ` / `...` | 3 columns |
| `Line` | `- \ | /` | 1 column |
| `Arc` | ◜◠◝◞◡◟ | 1 column |
| `Bounce` | ⠁⠂⠄⠂ | 1 column |

All styles reserve a fixed column width so surrounding text never reflows as frames change.

## Custom Frames

Frames are plain strings and may contain markup, including colors and decorations:

```csharp
// Markup frames (traffic-light cycle)
window.AddControl(
    Controls.Spinner()
        .WithFrames("[green]✔[/]", "[yellow]◐[/]", "[red]✗[/]")
        .WithInterval(400)
        .Build()
);

// Plain ASCII frames
window.AddControl(
    Controls.Spinner()
        .WithFrames("( ●    )", "(  ●   )", "(   ●  )", "(    ● )", "(     ●)", "(    ● )", "(   ●  )", "(  ●   )")
        .WithInterval(80)
        .Build()
);
```

Custom frames override the `Style` preset. The control's display width is automatically calculated as the widest frame's column count (after stripping markup tags).

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Style` | `SpinnerStyle` | `Braille` | Preset frame style (ignored when `Frames` is set) |
| `Frames` | `IReadOnlyList<string>?` | `null` | Custom frames; overrides `Style` when non-null and non-empty. May contain markup. |
| `EffectiveFrames` | `IReadOnlyList<string>` | — | Read-only. The frame set actually in use (custom or preset). |
| `IntervalMs` | `int` | `100` | Per-frame interval in milliseconds. Minimum is 1 ms. |
| `Color` | `Color?` | Theme | Foreground color for plain (un-marked-up) frames. Theme-resolved when `null`. |
| `IsSpinning` | `bool` | `true` | Whether the spinner is animating. Set to `false` to freeze. |
| `CurrentFrameIndex` | `int` | `0` | Read-only index of the currently displayed frame. |

## Methods

| Method | Description |
|--------|-------------|
| `Start()` | Starts (or resumes) animation. |
| `Stop()` | Stops animation, freezing the current frame. |

The spinner auto-starts when added to a window. If the window system's animation manager is disabled, the control renders the first frame statically.

## Builder Methods

### Spinner-Specific

| Method | Description |
|--------|-------------|
| `.WithStyle(SpinnerStyle style)` | Sets the preset frame style. |
| `.WithFrames(params string[] frames)` | Sets custom frames (overrides style). May contain markup. |
| `.WithInterval(int milliseconds)` | Sets the per-frame interval. |
| `.WithColor(Color color)` | Sets the foreground color for plain frames. |
| `.Spinning(bool spinning = true)` | Sets whether the spinner starts animating immediately (default `true`). |

### Standard Control Methods

| Method | Description |
|--------|-------------|
| `.WithName(string name)` | Names the control for `FindControl<T>()` lookups. |
| `.WithMargin(int left, int top, int right, int bottom)` | Sets the margin. |
| `.WithMargin(Margin margin)` | Sets the margin. |
| `.WithAlignment(HorizontalAlignment alignment)` | Sets horizontal alignment. |
| `.WithVerticalAlignment(VerticalAlignment alignment)` | Sets vertical alignment. |
| `.Visible(bool visible)` | Shows or hides the control. |
| `.WithTag(object tag)` | Attaches arbitrary metadata. |
| `.StickyTop()` | Pins to the top of a scrollable container. |
| `.StickyBottom()` | Pins to the bottom of a scrollable container. |

## SpinnerTextAnimator

`SpinnerTextAnimator` drives any `Action<string>` setter from a looping frame cycle. It is useful when you want an animated spinner in a context that is not a control — for example, a `StatusBarControl` item or a window title.

```csharp
using SharpConsoleUI.Helpers;
```

### Constructors

```csharp
// Preset style
var animator = new SpinnerTextAnimator(
    windowSystem,
    SpinnerStyle.Braille,
    text => statusLabel.SetContent(text)
);

// Custom frames (may contain markup)
var animator = new SpinnerTextAnimator(
    windowSystem,
    new[] { "[cyan]⣷[/]", "[cyan]⣯[/]", "[cyan]⣟[/]" },
    text => statusLabel.SetContent(text)
);

// Custom interval
var animator = new SpinnerTextAnimator(
    windowSystem,
    SpinnerStyle.Circle,
    text => statusLabel.SetContent(text),
    intervalMs: 150
);
```

### Methods

| Method | Description |
|--------|-------------|
| `Start()` | Starts the animation. Idempotent — safe to call when already started. Has no effect when the window system's animations are disabled. |
| `Stop()` | Stops the animation. Idempotent. |
| `Dispose()` | Stops and releases the animation (calls `Stop()`). |

### Example — Status Bar Label

```csharp
// Create a MarkupControl label for the status bar area
var statusLabel = Controls.Markup("Connecting...").WithName("status").Build();
window.AddControl(statusLabel.StickyBottom());

// Animate it via SpinnerTextAnimator
var spinner = new SpinnerTextAnimator(
    windowSystem,
    SpinnerStyle.Braille,
    frame => windowSystem.EnqueueOnUIThread(() =>
        statusLabel.SetContent($"Connecting {frame}"))
);
spinner.Start();

// ... later, when done:
spinner.Stop();
windowSystem.EnqueueOnUIThread(() => statusLabel.SetContent("[green]Connected[/]"));
```

`SpinnerTextAnimator` implements `IDisposable`. Wrap in a `using` statement or call `Dispose()` when done.

## Inline Markup Tag

The same spinner frames are available as an inline `[spinner]` tag anywhere markup is rendered — no control required:

```
[spinner]                     ← Braille (default)
[spinner circle]              ← Circle style
[yellow]Saving [spinner][/]   ← Inherits surrounding color
```

See [Markup Syntax → Spinner (animated)](../MARKUP_SYNTAX.md#spinner-animated) for the full tag reference.

## Related Controls

- [MarkupControl](MarkupControl.md) - Rich text display; supports inline `[spinner]` tags
- [StatusBarControl](StatusBarControl.md) - Three-zone status bar (use `SpinnerTextAnimator` to animate a zone)

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

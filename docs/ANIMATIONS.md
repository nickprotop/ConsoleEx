# Animation Framework

SharpConsoleUI includes a time-based animation system integrated into the render loop. Animations use tweened interpolation with configurable easing functions, driving any numeric or color property over time.

## Table of Contents

1. [Overview](#overview)
2. [AnimationManager](#animationmanager)
3. [Easing Functions](#easing-functions)
4. [Built-in Window Animations](#built-in-window-animations)
5. [Custom Animations](#custom-animations)
6. [Interpolators](#interpolators)
7. [Configuration](#configuration)

## Overview

The animation system consists of:

- **`AnimationManager`** — Owned by `ConsoleWindowSystem`, advances all active animations each frame
- **`Tween<T>`** — Generic interpolation from value A to B over a duration with easing
- **`EasingFunctions`** — Standard easing curves (Linear, EaseIn, EaseOut, Bounce, Elastic, etc.)
- **`WindowAnimations`** — Pre-built helpers for common window transitions (fade, slide)
- **`IInterpolator<T>`** — Type-specific interpolation (byte, int, float, Color)

### Architecture

```
ConsoleWindowSystem.Run() loop:
┌─────────────────────────────┐
│ Calculate deltaTime         │
└──────────┬──────────────────┘
           │
┌──────────▼──────────────────┐
│ Animations.Update(delta)    │  ◄── Advances all active tweens
│  - Caps delta to 33ms       │      Removes completed ones
│  - Fires onUpdate callbacks │
│  - Fires onComplete         │
└──────────┬──────────────────┘
           │
┌──────────▼──────────────────┐
│ Render windows              │
└─────────────────────────────┘
```

Delta time is capped to `MaxFrameDeltaMs` (33ms) to prevent animations completing instantly after idle periods (e.g., when the terminal was backgrounded).

## AnimationManager

Access via `ConsoleWindowSystem.Animations`:

```csharp
var ws = new ConsoleWindowSystem(RenderMode.Buffer);

// Create a float tween
ws.Animations.Animate(
    from: 0f,
    to: 100f,
    duration: TimeSpan.FromMilliseconds(500),
    easing: EasingFunctions.EaseOut,
    onUpdate: value => DoSomething(value),
    onComplete: () => Done());

// Check state
bool active = ws.Animations.HasActiveAnimations;
int count = ws.Animations.ActiveCount;

// Cancel all
ws.Animations.CancelAll();
```

### Supported Types

The manager provides overloads for common types:

```csharp
// Float
ws.Animations.Animate(0f, 1f, duration);

// Int
ws.Animations.Animate(0, 255, duration);

// Byte
ws.Animations.Animate((byte)0, (byte)255, duration);

// Color
ws.Animations.Animate(
    new Color(0, 0, 0),
    new Color(255, 255, 255),
    duration);

// Generic with custom interpolator
ws.Animations.Animate(fromVal, toVal, duration, myInterpolator);
```

### Cancellation

```csharp
// Cancel specific animation
var anim = ws.Animations.Animate(0f, 100f, TimeSpan.FromSeconds(1));
ws.Animations.Cancel(anim);

// Cancel all
ws.Animations.CancelAll();

// Check if complete
if (anim.IsComplete) { /* done or cancelled */ }
```

## Easing Functions

All easing functions map `t` in [0,1] to a progress value where `f(0)=0` and `f(1)=1`.

| Function | Description |
|----------|-------------|
| `Linear` | Constant speed |
| `EaseIn` | Starts slow, accelerates (quadratic) |
| `EaseOut` | Starts fast, decelerates (quadratic) |
| `EaseInOut` | Slow start and end, fast middle |
| `Bounce` | Bouncing ball effect at the end |
| `Elastic` | Spring-like overshoot and oscillation |
| `SinePulse` | Rises to 1 at midpoint, returns to 0 — for flash/pulse effects |

```csharp
// Use any easing function
ws.Animations.Animate(0f, 1f, duration, easing: EasingFunctions.Bounce);

// Custom easing via delegate
EasingFunction custom = t => t * t * t; // Cubic ease-in
ws.Animations.Animate(0f, 1f, duration, easing: custom);
```

## Built-in Window Animations

`WindowAnimations` provides ready-to-use window transitions that use `PostBufferPaint` hooks for visual effects.

### FadeIn

Fades a window in by overlaying a solid color that gradually becomes transparent.

```csharp
// Basic fade in
WindowAnimations.FadeIn(window);

// Custom duration and color
WindowAnimations.FadeIn(window,
    duration: TimeSpan.FromMilliseconds(500),
    fadeColor: Color.DarkBlue,
    easing: EasingFunctions.EaseOut);

// With completion callback
WindowAnimations.FadeIn(window, onComplete: () =>
{
    // Window is fully visible
});
```

### FadeOut

Fades a window out. Typically paired with closing the window in the `onComplete` callback.

```csharp
// Fade out and close
WindowAnimations.FadeOut(window, onComplete: () =>
{
    window.GetConsoleWindowSystem?.CloseWindow(window);
});

// Custom fade color
WindowAnimations.FadeOut(window,
    fadeColor: Color.White,
    duration: TimeSpan.FromMilliseconds(200));
```

### SlideIn

Slides a window from offscreen to its current position.

```csharp
// Slide in from the left
WindowAnimations.SlideIn(window, SlideDirection.Left);

// Slide in from the bottom with custom easing
WindowAnimations.SlideIn(window, SlideDirection.Bottom,
    duration: TimeSpan.FromMilliseconds(600),
    easing: EasingFunctions.Bounce);
```

### SlideOut

Slides a window offscreen in the specified direction.

```csharp
// Slide out to the right
WindowAnimations.SlideOut(window, SlideDirection.Right);

// Slide out and remove
WindowAnimations.SlideOut(window, SlideDirection.Top, onComplete: () =>
{
    window.GetConsoleWindowSystem?.CloseWindow(window);
});
```

### SlideDirection

```csharp
public enum SlideDirection
{
    Left,   // Slide from/to the left edge
    Right,  // Slide from/to the right edge
    Top,    // Slide from/to the top edge
    Bottom  // Slide from/to the bottom edge
}
```

## Custom Animations

### Animating Window Properties

```csharp
// Animate window position
var ws = window.GetConsoleWindowSystem!;
ws.Animations.Animate(
    from: window.Left,
    to: targetX,
    duration: TimeSpan.FromMilliseconds(300),
    easing: EasingFunctions.EaseInOut,
    onUpdate: x => ws.Positioning.MoveWindowTo(window, x, window.Top));
```

### Animating Colors

```csharp
// Pulse a window's background color
ws.Animations.Animate(
    from: Color.DarkBlue,
    to: Color.Blue,
    duration: TimeSpan.FromMilliseconds(500),
    easing: EasingFunctions.SinePulse,
    onUpdate: color => window.BackgroundColor = color);
```

### Chaining Animations

```csharp
// Slide in, then fade the background
WindowAnimations.SlideIn(window, SlideDirection.Left,
    onComplete: () =>
    {
        ws.Animations.Animate(
            new Color(0, 0, 0),
            new Color(30, 30, 60),
            TimeSpan.FromMilliseconds(500),
            onUpdate: c => window.BackgroundColor = c);
    });
```

### Pre-built Animation (IAnimation)

For complex animations, implement `IAnimation` and add directly:

```csharp
public class PulseAnimation : IAnimation
{
    public bool IsComplete { get; private set; }

    public void Update(TimeSpan deltaTime)
    {
        // Custom animation logic
    }

    public void Cancel()
    {
        IsComplete = true;
    }
}

ws.Animations.Add(new PulseAnimation());
```

## Interpolators

Built-in interpolators handle type-specific blending:

| Interpolator | Type | Behavior |
|-------------|------|----------|
| `ByteInterpolator` | `byte` | Clamped to [0,255] with rounding |
| `IntInterpolator` | `int` | Rounded to nearest integer |
| `FloatInterpolator` | `float` | Linear interpolation |
| `ColorInterpolator` | `Color` | Per-channel RGB blending |

### Custom Interpolator

```csharp
public class PointInterpolator : IInterpolator<Point>
{
    public static readonly PointInterpolator Instance = new();

    public Point Interpolate(Point from, Point to, double t) =>
        new Point(
            (int)Math.Round(from.X + (to.X - from.X) * t),
            (int)Math.Round(from.Y + (to.Y - from.Y) * t));
}

// Use it
ws.Animations.Animate(
    new Point(0, 0),
    new Point(50, 20),
    TimeSpan.FromMilliseconds(400),
    PointInterpolator.Instance,
    onUpdate: p => MoveWindow(p));
```

## Configuration

Animation defaults are in `AnimationDefaults`:

| Constant | Default | Description |
|----------|---------|-------------|
| `DefaultFadeDurationMs` | 300 | Default fade in/out duration |
| `DefaultSlideDurationMs` | 400 | Default slide in/out duration |
| `MaxConcurrentAnimations` | 50 | Maximum simultaneous animations |
| `MaxFrameDeltaMs` | 33.0 | Delta time cap per frame (~30fps) |

Animations can be disabled system-wide via `ConsoleWindowSystemOptions.EnableAnimations`.

## See Also

- [Compositor Effects](COMPOSITOR_EFFECTS.md) — Low-level buffer manipulation hooks
- [Gradients](GRADIENTS.md) — Gradient text and backgrounds
- [Controls Reference](CONTROLS.md) — UI controls

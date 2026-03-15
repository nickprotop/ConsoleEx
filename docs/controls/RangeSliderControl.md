# RangeSliderControl

A dual-thumb range slider that allows users to select a range of values by dragging two thumbs along a track. Supports minimum range enforcement, keyboard thumb switching, and both horizontal and vertical orientations.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LowValue` | `double` | `0` | Low end of the selected range |
| `HighValue` | `double` | `100` | High end of the selected range |
| `MinValue` | `double` | `0` | Minimum value of the full range |
| `MaxValue` | `double` | `100` | Maximum value of the full range |
| `Step` | `double` | `1` | Step increment for arrow keys |
| `LargeStep` | `double` | `10` | Large step for Page Up/Down and Shift+Arrow |
| `MinRange` | `double` | `0` | Minimum required gap between low and high values |
| `ActiveThumb` | `ActiveThumb` | `Low` | Which thumb receives keyboard input |
| `Orientation` | `SliderOrientation` | `Horizontal` | Slider orientation |
| `ShowValueLabel` | `bool` | `false` | Show range value label |
| `ShowMinMaxLabels` | `bool` | `false` | Show min/max labels at track ends |
| `ValueLabelFormat` | `string` | `"F0"` | Format string for value labels |

## Events

| Event | Type | Description |
|-------|------|-------------|
| `LowValueChanged` | `EventHandler<double>` | Fires when the low value changes |
| `HighValueChanged` | `EventHandler<double>` | Fires when the high value changes |
| `RangeChanged` | `EventHandler<(double Low, double High)>` | Fires when either value changes |
| `GotFocus` | `EventHandler` | Fires when the slider receives focus |
| `LostFocus` | `EventHandler` | Fires when the slider loses focus |

## Creating with Builder

```csharp
// Basic range slider
var range = Controls.RangeSlider()
    .WithValues(25, 75)
    .ShowValueLabel()
    .Build();

// Price range with constraints
var price = Controls.RangeSlider()
    .WithRange(0, 1000)
    .WithValues(200, 800)
    .WithStep(10)
    .WithMinRange(50)
    .ShowMinMaxLabels()
    .OnRangeChanged((s, r) => label.SetLines($"${r.Low} - ${r.High}"))
    .Build();
```

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Tab | Switch active thumb (Low/High) |
| Right/Up Arrow | Move active thumb by Step |
| Left/Down Arrow | Move active thumb by -Step |
| Shift+Arrow | Move by LargeStep |
| Page Up/Down | Move by LargeStep |
| Home | Move active thumb to its minimum bound |
| End | Move active thumb to its maximum bound |

## Mouse Interaction

- **Click near thumb**: Select and start dragging that thumb
- **Click between thumbs**: Jump nearest thumb to position
- **Overlapping thumbs**: High thumb takes priority
- **Drag**: Move active thumb, respecting MinRange

## Visual Layout

```
[0] │──────●━━━━━━●──────│ [100]  25-75
    ^cap   ^low   ^hi    ^cap      ^label
```

## Related Controls

- [SliderControl](SliderControl.md) - Single-value slider

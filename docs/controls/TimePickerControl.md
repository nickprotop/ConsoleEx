# TimePickerControl

Locale-aware time picker with segmented editing, 12h/24h modes, and min/max time constraints.

## Overview

The `TimePickerControl` displays segmented hour/minute (and optionally second) fields with an optional AM/PM segment. Users type digits directly or use arrow keys to adjust values. The 12h/24h format and AM/PM designators are derived from the configured `CultureInfo`.

See also: [DatePickerControl](DatePickerControl.md)

## Quick Start

```csharp
var timePicker = Controls.TimePicker("Alarm:")
    .WithSelectedTime(new TimeSpan(8, 30, 0))
    .With12HourFormat()
    .OnSelectedTimeChanged((s, time) =>
    {
        // time is TimeSpan?
    })
    .Build();

window.AddControl(timePicker);
```

## Builder API

Create a `TimePickerBuilder` through the `Controls` factory:

```csharp
var builder = Controls.TimePicker("Start time:");
```

### Value Methods

```csharp
.WithSelectedTime(TimeSpan time)           // Set the initial time
.WithMinTime(TimeSpan minTime)             // Set minimum allowed time
.WithMaxTime(TimeSpan maxTime)             // Set maximum allowed time
```

### Format Methods

```csharp
.WithCulture(CultureInfo culture)          // Set locale for time format and AM/PM designators
.With24HourFormat()                        // Force 24-hour display
.With12HourFormat()                        // Force 12-hour display with AM/PM
.WithSeconds(bool show = true)             // Show or hide the seconds segment
```

### Layout Methods

```csharp
.WithPrompt(string prompt)                 // Set the label text (default: "Time:")
.WithAlignment(HorizontalAlignment align)  // Horizontal alignment
.WithVerticalAlignment(VerticalAlignment)  // Vertical alignment
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int margin)                    // Uniform margin on all sides
.WithWidth(int width)                      // Fixed width
.Visible(bool visible)                     // Initial visibility
.Enabled(bool enabled)                     // Initial enabled state
```

### Identity Methods

```csharp
.WithName(string name)                     // Name for FindControl<T>() lookups
.WithTag(object tag)                       // Arbitrary user data
.WithStickyPosition(StickyPosition pos)    // Sticky positioning
.StickyTop()                               // Shorthand for StickyPosition.Top
.StickyBottom()                            // Shorthand for StickyPosition.Bottom
```

### Event Methods

```csharp
.OnSelectedTimeChanged(EventHandler<TimeSpan?> handler)
.OnSelectedTimeChanged(WindowEventHandler<TimeSpan?> handler)  // includes Window reference
.OnGotFocus(EventHandler handler)
.OnGotFocus(WindowEventHandler<EventArgs> handler)
.OnLostFocus(EventHandler handler)
.OnLostFocus(WindowEventHandler<EventArgs> handler)
```

### Building

```csharp
TimePickerControl control = builder.Build();

// Implicit conversion is also supported:
TimePickerControl control = Controls.TimePicker("Time:")
    .WithSelectedTime(new TimeSpan(14, 30, 0));
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `SelectedTime` | `TimeSpan?` | The currently selected time. `null` defaults to `TimeSpan.Zero` for display. |
| `MinTime` | `TimeSpan?` | Minimum selectable time. Values are clamped on set. |
| `MaxTime` | `TimeSpan?` | Maximum selectable time. Values are clamped on set. |
| `Culture` | `CultureInfo` | Locale controlling time separator and AM/PM designators. Defaults to `CultureInfo.CurrentCulture`. |
| `Use24HourFormat` | `bool?` | Override the culture's default. `null` = auto-detect from culture, `true` = 24h, `false` = 12h. |
| `ShowSeconds` | `bool` | Whether to display the seconds segment. Default: `false`. |
| `Prompt` | `string` | Label text displayed before the time segments. |
| `IsEnabled` | `bool` | Whether the control accepts input. |
| `BackgroundColor` | `Color` | Background color (unfocused). |
| `ForegroundColor` | `Color` | Foreground color (unfocused). |
| `FocusedBackgroundColor` | `Color` | Background color when focused. |
| `FocusedForegroundColor` | `Color` | Foreground color when focused. |
| `SegmentBackgroundColor` | `Color` | Background of the active time segment. |
| `SegmentForegroundColor` | `Color` | Foreground of the active time segment. |
| `DisabledForegroundColor` | `Color` | Foreground color when the control is disabled. |

## Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `SelectedTimeChanged` | `EventHandler<TimeSpan?>` | Fires when the selected time changes. |
| `GotFocus` | `EventHandler` | Fires when the control receives focus. |
| `LostFocus` | `EventHandler` | Fires when the control loses focus. |

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Left / Right | Move between time segments |
| Tab / Shift+Tab | Move between segments (passes focus if at first/last segment) |
| Up | Increment the focused segment by 1 |
| Down | Decrement the focused segment by 1 |
| Page Up | Increment the focused segment by a large step |
| Page Down | Decrement the focused segment by a large step |
| Home | Set the focused segment to its minimum value |
| End | Set the focused segment to its maximum value |
| 0-9 | Type digits directly into the focused segment |
| A | Set to AM (when AM/PM segment is focused) |
| P | Set to PM (when AM/PM segment is focused) |

Digit entry uses a two-keystroke model: the first digit is held as a pending value, and the second digit completes the entry. If the first digit is too large to be a valid tens place, it commits immediately. After a complete two-digit entry, focus auto-advances to the next segment.

## Mouse Interaction

- **Click on a segment** focuses that segment for editing
- **Mouse wheel** increments/decrements the focused segment

## 12-Hour and 24-Hour Modes

By default, the format is auto-detected from the culture's `ShortTimePattern`. You can override this explicitly:

```csharp
// 24-hour format
Controls.TimePicker("Departure:")
    .With24HourFormat()
    .WithSeconds()
    .Build();
// Displays: Departure: 14:30:00

// 12-hour format
Controls.TimePicker("Departure:")
    .With12HourFormat()
    .Build();
// Displays: Departure: 02:30 PM
```

In 12-hour mode, an additional AM/PM segment appears. Navigate to it with arrow keys and press A or P to toggle, or use Up/Down to cycle.

## Locale and CultureInfo

The time separator and AM/PM designators are derived from the culture:

```csharp
// US English: 2:30 PM (colon separator, AM/PM)
Controls.TimePicker()
    .WithCulture(new CultureInfo("en-US"))

// German: 14:30 (colon separator, 24h by default)
Controls.TimePicker()
    .WithCulture(new CultureInfo("de-DE"))

// Korean: 14:30 (24h, culture-specific designators available)
Controls.TimePicker()
    .WithCulture(new CultureInfo("ko-KR"))
```

## Theme Properties

| Theme Property | Description | ClassicTheme Default |
|---------------|-------------|---------------------|
| `TimePickerBackgroundColor` | Unfocused background | `null` (inherits from window) |
| `TimePickerForegroundColor` | Unfocused foreground | `null` (inherits from window) |
| `TimePickerFocusedBackgroundColor` | Focused background | `Color.Blue` |
| `TimePickerFocusedForegroundColor` | Focused foreground | `Color.White` |
| `TimePickerSegmentBackgroundColor` | Active segment background | `Color.DarkBlue` |
| `TimePickerSegmentForegroundColor` | Active segment foreground | `Color.White` |
| `TimePickerDisabledForegroundColor` | Disabled text | `Color.Grey` |

## Common Patterns

### Time Range with Business Hours

```csharp
var businessStart = new TimeSpan(8, 0, 0);
var businessEnd = new TimeSpan(18, 0, 0);

window.AddControl(Controls.TimePicker("Appointment:")
    .WithMinTime(businessStart)
    .WithMaxTime(businessEnd)
    .WithSelectedTime(new TimeSpan(9, 0, 0))
    .With24HourFormat()
    .Build());
```

### Combined Date and Time Form

```csharp
window.AddControl(Controls.DatePicker("Date:")
    .WithName("meetingDate")
    .WithMinDate(DateTime.Today)
    .Build());

window.AddControl(Controls.TimePicker("Start:")
    .WithName("startTime")
    .With24HourFormat()
    .WithSelectedTime(new TimeSpan(9, 0, 0))
    .Build());

window.AddControl(Controls.Button("Schedule")
    .OnClick((s, e, w) =>
    {
        var date = w.FindControl<DatePickerControl>("meetingDate")?.SelectedDate;
        var start = w.FindControl<TimePickerControl>("startTime")?.SelectedTime;
        if (date.HasValue && start.HasValue)
        {
            var dateTime = date.Value + start.Value;
            // Process...
        }
    })
    .Build());
```

---

[Back to Controls Reference](../CONTROLS.md) | [DatePickerControl](DatePickerControl.md)

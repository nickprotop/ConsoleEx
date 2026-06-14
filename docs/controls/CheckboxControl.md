# CheckboxControl

Toggleable checkbox control with a label, checked/unchecked state, and keyboard/mouse support.

## Overview

CheckboxControl displays a label next to a `[ ]` / `[X]` indicator and toggles its checked state when activated. It responds to keyboard (Space/Enter) and mouse clicks, and fires a `CheckedChanged` event whenever the state flips.

The checked and unchecked indicator characters are fully customizable, so the control can render anything from a classic `X` to a check mark (`✓`), a dot (`●`), or any other single visible character. Colors for the normal, focused, and disabled states, as well as the checkmark itself, can be overridden or left to inherit from the active theme.

The control measures itself to a single row sized to ` [X] Label `, but it also honors a fixed `Width` and `HorizontalAlignment` (including `Stretch`) when laid out inside containers.

See also: [ButtonControl](ButtonControl.md)

## Quick Start

```csharp
var checkbox = Controls.Checkbox("Enable notifications")
    .Checked(true)
    .OnCheckedChanged((sender, isChecked) =>
    {
        // React to the new state
    })
    .Build();

window.AddControl(checkbox);
```

## Builder API

### Content

```csharp
Controls.Checkbox("Label")           // Factory sets the initial label
    .WithLabel("Enable feature")     // Override the label text
    .Checked(true)                   // Set initial checked state (default: true when called)
    .Checked(false);                 // Explicitly start unchecked
```

### Indicator Characters

```csharp
Controls.Checkbox("Custom marks")
    .WithCheckedCharacter("✓")        // Character shown when checked (default "X")
    .WithUncheckedCharacter("·")      // Character shown when unchecked (default " ")
    .WithCheckmark("✓", "·");         // Set both at once (unchecked defaults to " ")
```

### Layout

```csharp
Controls.Checkbox("Label")
    .WithWidth(30)                              // Fixed width
    .WithAlignment(HorizontalAlignment.Center)  // Horizontal alignment
    .WithVerticalAlignment(VerticalAlignment.Top)
    .WithMargin(1)                              // Uniform margin
    .WithMargin(1, 0, 1, 0)                     // left, top, right, bottom
    .Visible(true)
    .StickyTop()                                // Stick to top of window
    .StickyBottom()                             // Stick to bottom of window
    .WithStickyPosition(StickyPosition.None);
```

### Identity

```csharp
Controls.Checkbox("Label")
    .WithName("myCheckbox")   // Name for FindControl<CheckboxControl>("myCheckbox")
    .WithTag(myData);         // Arbitrary tag object
```

### Events

```csharp
Controls.Checkbox("Label")
    .OnCheckedChanged((sender, isChecked) => { /* ... */ })
    .OnCheckedChanged((sender, isChecked, window) => { /* window-aware */ })
    .OnGotFocus((sender, e) => { /* ... */ })
    .OnGotFocus((sender, e, window) => { /* window-aware */ })
    .OnLostFocus((sender, e) => { /* ... */ })
    .OnLostFocus((sender, e, window) => { /* window-aware */ });
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Checked` | `bool` | `false` | The checked state; setting it fires `CheckedChanged` |
| `Label` | `string` | `"Checkbox"` | Text displayed next to the checkbox |
| `CheckedCharacter` | `string` | `"X"` | Character shown inside the box when checked (empty/null falls back to default) |
| `UncheckedCharacter` | `string` | `" "` | Character shown inside the box when unchecked (empty/null falls back to default) |
| `IsEnabled` | `bool` | `true` | Whether the checkbox can be interacted with |
| `Width` | `int?` | `null` | Fixed width (auto-sized to content if null) |
| `BackgroundColor` | `Color?` | `null` | Background color in the normal state (uses theme if null) |
| `ForegroundColor` | `Color` | theme `ButtonForegroundColor` / `Color.White` | Text color in the normal state |
| `FocusedBackgroundColor` | `Color?` | `null` | Background color when focused (uses theme if null) |
| `FocusedForegroundColor` | `Color` | theme `ButtonFocusedForegroundColor` / `Color.White` | Text color when focused |
| `DisabledBackgroundColor` | `Color?` | `null` | Background color when disabled (uses theme if null) |
| `DisabledForegroundColor` | `Color` | theme `ButtonDisabledForegroundColor` / `Color.DarkSlateGray1` | Text color when disabled |
| `CheckmarkColor` | `Color` | theme `ButtonFocusedForegroundColor` / `Color.Cyan1` | Color of the checkmark character when checked |
| `HasFocus` | `bool` | `false` | Whether the checkbox currently has keyboard focus |
| `CanReceiveFocus` | `bool` | `true` | Whether the checkbox can receive focus (mirrors `IsEnabled`) |
| `WantsMouseEvents` | `bool` | `true` | Whether the checkbox wants mouse events (mirrors `IsEnabled`) |
| `CanFocusWithMouse` | `bool` | `true` | Whether a mouse click can focus the checkbox (mirrors `IsEnabled`) |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `CheckedChanged` | `EventHandler<bool>` | Fired when the checked state changes; argument is the new state |
| `CheckedChangedAsync` | `AsyncEventHandler<bool>` | Async counterpart of `CheckedChanged` |
| `MouseClick` | `EventHandler<MouseEventArgs>` | Fired when the checkbox is left-clicked |
| `MouseDoubleClick` | `EventHandler<MouseEventArgs>` | Fired when the checkbox is double-clicked |
| `MouseRightClick` | `EventHandler<MouseEventArgs>` | Fired when the checkbox is right-clicked |
| `MouseEnter` | `EventHandler<MouseEventArgs>` | Fired when the mouse enters the checkbox area |
| `MouseLeave` | `EventHandler<MouseEventArgs>` | Fired when the mouse leaves the checkbox area |
| `MouseMove` | `EventHandler<MouseEventArgs>` | Fired when the mouse moves over the checkbox |
| `GotFocus` | `EventHandler` | Fired when the checkbox receives focus |
| `LostFocus` | `EventHandler` | Fired when the checkbox loses focus |

## Keyboard Support

| Key | Action |
|-----|--------|
| **Space** | Toggle checked state |
| **Enter** | Toggle checked state |
| **Tab** | Move focus to next control |
| **Shift+Tab** | Move focus to previous control |

Keys are only processed while the checkbox is enabled and focused.

## Mouse Support

| Action | Result |
|--------|--------|
| **Left Click** | Toggle checked state and fire `MouseClick` |
| **Double Click** | Fire `MouseDoubleClick` (does not toggle again) |
| **Right Click** | Fire `MouseRightClick` |
| **Click** | Give the checkbox keyboard focus |
| **Enter / Leave** | Fire `MouseEnter` / `MouseLeave` |

Mouse events are only processed when enabled, and clicks in the control's margin area are ignored.

## Examples

### Simple Checkbox

```csharp
window.AddControl(
    Controls.Checkbox("Enable notifications")
        .Checked(true)
        .Build()
);
```

### Reacting to State Changes

```csharp
window.AddControl(
    Controls.Checkbox("Auto-save on exit")
        .OnCheckedChanged((sender, isChecked) =>
        {
            windowSystem.NotificationStateService.ShowNotification(
                "Auto-save",
                isChecked ? "Enabled" : "Disabled",
                NotificationSeverity.Info
            );
        })
        .Build()
);
```

### Custom Checkmark Characters

```csharp
window.AddControl(
    Controls.Checkbox("Custom marks")
        .WithCheckmark("✓", "·")
        .Checked(true)
        .Build()
);
```

### Reading State From Another Control

```csharp
window.AddControl(
    Controls.Checkbox("Animate background gradient")
        .WithName("animToggle")
        .Checked(true)
        .Build()
);

// Elsewhere (e.g. an async window thread):
var toggle = window.FindControl<CheckboxControl>("animToggle");
if (toggle?.Checked == true)
{
    // run the animation
}
```

### Two-Way Data Binding

```csharp
var enabledCheckbox = Controls.Checkbox("Monitoring Enabled")
    .Build();

// Keep the view model and the checkbox in sync both ways
enabledCheckbox.BindTwoWay(vm, v => v.MonitoringEnabled, c => c.Checked);

window.AddControl(enabledCheckbox);
```

### Settings Panel

```csharp
panel.AddControl(Controls.Checkbox("Enable notifications").Checked(true).Build());
panel.AddControl(Controls.Checkbox("Auto-save on exit").Checked(true).Build());
panel.AddControl(Controls.Checkbox("Show status bar").Build());
panel.AddControl(Controls.Checkbox("Enable telemetry").Build());
```

## Best Practices

1. **Use clear labels**: The label should describe the option being toggled ("Enable notifications", not "Notifications?").
2. **Set the initial state explicitly**: Use `.Checked(true)` for options that should default to on so the UI matches your model.
3. **Prefer `CheckedChanged` over polling**: React to state changes through the event rather than reading `Checked` on a timer.
4. **Bind to your model**: Use `BindTwoWay` to keep a view-model property and the checkbox in sync without manual event wiring. See the [Data Binding guide](../binding.md).
5. **Name checkboxes you read later**: Use `.WithName(...)` so you can retrieve state with `FindControl<CheckboxControl>(...)`.
6. **Customize marks for clarity**: Use `WithCheckmark` to match your visual style, but keep characters single-width for clean alignment.

## See Also

- [ButtonControl](ButtonControl.md) - For click actions
- [PromptControl](PromptControl.md) - For text input

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

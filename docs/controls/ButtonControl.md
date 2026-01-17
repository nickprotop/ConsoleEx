# ButtonControl

Interactive button control with click events and keyboard/mouse support.

## Overview

ButtonControl is a clickable button that responds to keyboard (Space/Enter) and mouse clicks. It supports custom text, width, colors, and click event handlers.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | `"Button"` | Button text displayed |
| `Width` | `int?` | `null` | Fixed width (auto-sized if null) |
| `IsEnabled` | `bool` | `true` | Enable/disable button |
| `BackgroundColor` | `Color?` | `null` | Background color (uses theme if null) |
| `ForegroundColor` | `Color?` | `null` | Text color (uses theme if null) |
| `HasFocus` | `bool` | `false` | Whether button has keyboard focus |
| `CanReceiveFocus` | `bool` | `true` | Whether button can receive focus |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `OnClick` | `EventHandler` | Fired when button is clicked |
| `GotFocus` | `EventHandler` | Fired when button receives focus |
| `LostFocus` | `EventHandler` | Fired when button loses focus |

## Creating Buttons

### Using Builder (Recommended)

```csharp
var button = Controls.Button("Click Me")
    .WithWidth(20)
    .WithAlignment(HorizontalAlignment.Center)
    .OnClick((sender, e, window) =>
    {
        // Handle click - window parameter available
        windowSystem.NotificationStateService.ShowNotification(
            "Button Clicked",
            "You clicked the button!",
            NotificationSeverity.Info
        );
    })
    .Build();

window.AddControl(button);
```

### Using Constructor

```csharp
var button = new ButtonControl
{
    Text = "Submit",
    Width = 15,
    IsEnabled = true
};

button.OnClick += (sender, e) =>
{
    // Handle click
};

window.AddControl(button);
```

## Keyboard Support

| Key | Action |
|-----|--------|
| **Space** | Click button |
| **Enter** | Click button |
| **Tab** | Move focus to next control |
| **Shift+Tab** | Move focus to previous control |

## Mouse Support

| Action | Result |
|--------|--------|
| **Left Click** | Click button and fire OnClick event |
| **Click** | Give button keyboard focus |
| **Hover** | Visual highlight (if supported) |

## Visual States

Buttons have different appearances based on state:

- **Normal**: Default appearance
- **Focused**: Highlighted when has keyboard focus
- **Disabled**: Dimmed appearance when `IsEnabled = false`
- **Pressed**: Visual feedback during click (brief)

## Examples

### Simple Button

```csharp
window.AddControl(
    Controls.Button("OK")
        .OnClick((s, e, w) => w.Close())
        .Build()
);
```

### Button with Custom Width and Alignment

```csharp
window.AddControl(
    Controls.Button("Submit")
        .WithWidth(30)
        .WithAlignment(HorizontalAlignment.Center)
        .OnClick((s, e, w) =>
        {
            // Handle submission
        })
        .Build()
);
```

### Button that Interacts with Other Controls

```csharp
window.AddControl(
    Controls.Prompt("Enter name:")
        .WithName("nameInput")
        .Build()
);

window.AddControl(
    Controls.Button("Greet")
        .OnClick((sender, e, window) =>
        {
            var input = window.FindControl<PromptControl>("nameInput");
            var name = input?.Text ?? "stranger";

            windowSystem.NotificationStateService.ShowNotification(
                "Greeting",
                $"Hello, {name}!",
                NotificationSeverity.Success
            );
        })
        .Build()
);
```

### Disabled Button

```csharp
var submitButton = Controls.Button("Submit")
    .WithName("submitBtn")
    .OnClick((s, e, w) => { /* ... */ })
    .Build();

submitButton.IsEnabled = false;  // Disable initially
window.AddControl(submitButton);

// Enable when validation passes
window.AddControl(
    Controls.Prompt("Required field:")
        .OnInputChanged((sender, text, window) =>
        {
            var btn = window.FindControl<ButtonControl>("submitBtn");
            if (btn != null)
            {
                btn.IsEnabled = !string.IsNullOrWhiteSpace(text);
            }
        })
        .Build()
);
```

### Button Grid

```csharp
window.AddControl(
    Controls.HorizontalGrid()
        .Column(col => col.Add(
            Controls.Button("Save")
                .OnClick((s, e, w) => SaveData())
                .Build()
        ))
        .Column(col => col.Add(
            Controls.Button("Cancel")
                .OnClick((s, e, w) => w.Close())
                .Build()
        ))
        .Column(col => col.Add(
            Controls.Button("Reset")
                .OnClick((s, e, w) => ResetForm())
                .Build()
        ))
        .Build()
);
```

### Button with Colors

```csharp
var button = new ButtonControl
{
    Text = "Delete",
    Width = 15,
    BackgroundColor = Color.Red,
    ForegroundColor = Color.White
};

button.OnClick += (sender, e) =>
{
    // Show confirmation dialog
};

window.AddControl(button);
```

## Best Practices

1. **Use descriptive text**: Button text should clearly indicate the action ("Save", "Delete", not "OK")
2. **Consistent width**: Use fixed width for buttons in the same group
3. **Center important buttons**: Use alignment for visual emphasis
4. **Disable when invalid**: Disable buttons when actions can't be performed
5. **Provide feedback**: Show notifications or update UI after button clicks
6. **Use window parameter**: In OnClick handlers, use the window parameter to access other controls

## Common Patterns

### Confirm/Cancel Pair

```csharp
window.AddControl(
    Controls.HorizontalGrid()
        .Column(col => col.Add(
            Controls.Button("Confirm")
                .OnClick((s, e, w) =>
                {
                    // Perform action
                    w.Close();
                })
                .Build()
        ))
        .Column(col => col.Add(
            Controls.Button("Cancel")
                .OnClick((s, e, w) => w.Close())
                .Build()
        ))
        .Build()
);
```

### Submit Button with Validation

```csharp
// Add input
window.AddControl(
    Controls.Prompt("Email:")
        .WithName("email")
        .Build()
);

// Add submit button
window.AddControl(
    Controls.Button("Submit")
        .WithName("submitBtn")
        .OnClick((sender, e, window) =>
        {
            var email = window.FindControl<PromptControl>("email")?.Text;

            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                windowSystem.NotificationStateService.ShowNotification(
                    "Validation Error",
                    "Please enter a valid email address",
                    NotificationSeverity.Warning
                );
                return;
            }

            // Process valid email
            windowSystem.NotificationStateService.ShowNotification(
                "Success",
                "Form submitted successfully!",
                NotificationSeverity.Success
            );
        })
        .Build()
);
```

### Action Button

```csharp
window.AddControl(
    Controls.Button("Refresh Data")
        .OnClick(async (sender, e, window) =>
        {
            var button = sender as ButtonControl;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Text = "Loading...";
            }

            try
            {
                await LoadDataAsync();

                windowSystem.NotificationStateService.ShowNotification(
                    "Success",
                    "Data refreshed",
                    NotificationSeverity.Success
                );
            }
            finally
            {
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Text = "Refresh Data";
                }
            }
        })
        .Build()
);
```

## See Also

- [CheckboxControl](CheckboxControl.md) - For toggle/boolean input
- [PromptControl](PromptControl.md) - For text input
- [ToolbarControl](ToolbarControl.md) - For button toolbars

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

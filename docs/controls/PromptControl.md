# PromptControl

Single-line text input control with label, validation, and event support.

## Overview

PromptControl provides a labeled text input field for single-line user input. It supports max length, input validation, Enter key events, and real-time change notifications.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Prompt` | `string` | `""` | Label text displayed before input |
| `Text` | `string` | `""` | Current input text value |
| `MaxLength` | `int?` | `null` | Maximum input length (unlimited if null) |
| `IsEnabled` | `bool` | `true` | Enable/disable input |
| `HasFocus` | `bool` | `false` | Whether control has keyboard focus |
| `CanReceiveFocus` | `bool` | `true` | Whether control can receive focus |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `OnEnter` | `Func<string, bool>` | Fired when Enter key is pressed. Return true to clear input |
| `InputChanged` | `EventHandler<string>` | Fired when text changes |
| `GotFocus` | `EventHandler` | Fired when control receives focus |
| `LostFocus` | `EventHandler` | Fired when control loses focus |

## Creating Prompts

### Using Builder (Recommended)

```csharp
var prompt = Controls.Prompt("Enter name:")
    .WithInitialText("")
    .WithMaxLength(50)
    .WithName("nameInput")
    .OnEntered((sender, text, window) =>
    {
        // Handle Enter key - window parameter available
        windowSystem.NotificationStateService.ShowNotification(
            "Input Received",
            $"You entered: {text}",
            NotificationSeverity.Info
        );
        return true;  // Clear input after Enter
    })
    .OnInputChanged((sender, text, window) =>
    {
        // Handle text change
        var status = window.FindControl<MarkupControl>("status");
        status?.SetContent($"Length: {text.Length}");
    })
    .Build();

window.AddControl(prompt);
```

### Using Constructor

```csharp
var prompt = new PromptControl
{
    Prompt = "Email:",
    MaxLength = 100,
    Text = ""
};

prompt.OnEnter = (text) =>
{
    // Process input
    return false;  // Don't clear
};

prompt.InputChanged += (sender, text) =>
{
    // Handle change
};

window.AddControl(prompt);
```

## Keyboard Support

| Key | Action |
|-----|--------|
| **Enter** | Fire OnEnter event (optionally clear input) |
| **Backspace** | Delete character before cursor |
| **Delete** | Delete character at cursor |
| **Home** | Move cursor to start |
| **End** | Move cursor to end |
| **Left Arrow** | Move cursor left |
| **Right Arrow** | Move cursor right |
| **Ctrl+A** | Select all (if supported) |
| **Tab** | Move focus to next control |
| **Shift+Tab** | Move focus to previous control |
| **Any character** | Insert at cursor (if max length not reached) |

## Examples

### Simple Input

```csharp
window.AddControl(
    Controls.Prompt("Name:")
        .OnEntered((s, text, w) =>
        {
            windowSystem.NotificationStateService.ShowNotification(
                "Hello", $"Hello, {text}!", NotificationSeverity.Success);
            return true;  // Clear after Enter
        })
        .Build()
);
```

### Input with Max Length

```csharp
window.AddControl(
    Controls.Prompt("ZIP Code:")
        .WithMaxLength(5)
        .OnInputChanged((s, text, w) =>
        {
            // Only allow digits
            if (!string.IsNullOrEmpty(text) && !text.All(char.IsDigit))
            {
                var prompt = s as PromptControl;
                if (prompt != null)
                {
                    prompt.Text = new string(text.Where(char.IsDigit).ToArray());
                }
            }
        })
        .Build()
);
```

### Form with Multiple Inputs

```csharp
// Name input
window.AddControl(Controls.Prompt("Name:").WithName("name").Build());

// Email input
window.AddControl(
    Controls.Prompt("Email:")
        .WithName("email")
        .WithMaxLength(100)
        .Build()
);

// Phone input
window.AddControl(
    Controls.Prompt("Phone:")
        .WithName("phone")
        .WithMaxLength(15)
        .Build()
);

// Submit button
window.AddControl(
    Controls.Button("Submit")
        .OnClick((s, e, w) =>
        {
            var name = w.FindControl<PromptControl>("name")?.Text;
            var email = w.FindControl<PromptControl>("email")?.Text;
            var phone = w.FindControl<PromptControl>("phone")?.Text;

            // Validate and submit
            if (string.IsNullOrWhiteSpace(name))
            {
                windowSystem.NotificationStateService.ShowNotification(
                    "Error", "Name is required", NotificationSeverity.Warning);
                return;
            }

            // Process form...
        })
        .Build()
);
```

### Real-time Validation

```csharp
window.AddControl(
    Controls.Label("Status: Ready")
        .WithName("status")
        .Build()
);

window.AddControl(
    Controls.Prompt("Email:")
        .OnInputChanged((sender, text, window) =>
        {
            var status = window.FindControl<MarkupControl>("status");

            if (string.IsNullOrWhiteSpace(text))
            {
                status?.SetContent("[dim]Status: Enter email[/]");
            }
            else if (!text.Contains("@"))
            {
                status?.SetContent("[red]Status: Invalid email[/]");
            }
            else
            {
                status?.SetContent("[green]Status: Valid email[/]");
            }
        })
        .Build()
);
```

### Search Box

```csharp
var list = Controls.List()
    .AddItem("Apple")
    .AddItem("Banana")
    .AddItem("Cherry")
    .AddItem("Date")
    .WithName("resultsList")
    .Build();

window.AddControl(
    Controls.Prompt("Search:")
        .OnInputChanged((sender, text, window) =>
        {
            var list = window.FindControl<ListControl>("resultsList");
            if (list != null)
            {
                // Filter list items based on search text
                list.Items.Clear();

                var fruits = new[] { "Apple", "Banana", "Cherry", "Date" };
                foreach (var fruit in fruits)
                {
                    if (fruit.Contains(text, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Items.Add(new ListItem(fruit));
                    }
                }

                list.Invalidate();
            }
        })
        .Build()
);

window.AddControl(list);
```

### Password-style Input (Display Masking)

```csharp
var passwordControl = Controls.Prompt("Password:")
    .WithName("password")
    .OnInputChanged((sender, text, window) =>
    {
        // Custom rendering for password masking would need
        // custom control implementation
    })
    .Build();

window.AddControl(passwordControl);
```

### Clear Button

```csharp
window.AddControl(
    Controls.Prompt("Query:")
        .WithName("searchInput")
        .Build()
);

window.AddControl(
    Controls.Button("Clear")
        .OnClick((s, e, w) =>
        {
            var input = w.FindControl<PromptControl>("searchInput");
            if (input != null)
            {
                input.Text = "";
                input.Invalidate();
            }
        })
        .Build()
);
```

## Helper Methods

### Clear Input

```csharp
var prompt = window.FindControl<PromptControl>("myInput");
if (prompt != null)
{
    prompt.Text = "";
    prompt.Invalidate();
}
```

### Set Focus

```csharp
var prompt = window.FindControl<PromptControl>("myInput");
prompt?.SetFocus();
```

### Validate Input

```csharp
bool IsValidEmail(string email)
{
    return !string.IsNullOrWhiteSpace(email) &&
           email.Contains("@") &&
           email.Contains(".");
}

var prompt = window.FindControl<PromptControl>("emailInput");
if (prompt != null && !IsValidEmail(prompt.Text))
{
    windowSystem.NotificationStateService.ShowNotification(
        "Validation Error",
        "Please enter a valid email",
        NotificationSeverity.Warning
    );
}
```

## Best Practices

1. **Provide clear prompts**: Use descriptive labels ("Enter email:", not just "Email")
2. **Set max length**: Prevent excessive input with MaxLength
3. **Validate on change**: Use InputChanged for real-time feedback
4. **Handle Enter key**: Use OnEnter for form submission
5. **Clear after submission**: Return true from OnEnter to clear input
6. **Show validation state**: Update status labels based on input validity
7. **Disable when needed**: Set IsEnabled = false during processing

## Common Patterns

### Required Field Validation

```csharp
window.AddControl(Controls.Prompt("Name:*").WithName("name").Build());

window.AddControl(
    Controls.Button("Submit")
        .OnClick((s, e, w) =>
        {
            var name = w.FindControl<PromptControl>("name")?.Text;
            if (string.IsNullOrWhiteSpace(name))
            {
                windowSystem.NotificationStateService.ShowNotification(
                    "Validation Error",
                    "Name is required",
                    NotificationSeverity.Warning
                );
                return;
            }

            // Process...
        })
        .Build()
);
```

### Enter to Submit

```csharp
window.AddControl(
    Controls.Prompt("Command:")
        .OnEntered((s, text, w) =>
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Execute command
            ExecuteCommand(text);

            return true;  // Clear input
        })
        .Build()
);
```

### Auto-complete

```csharp
var suggestions = new[] { "apple", "application", "apply", "banana", "band" };

window.AddControl(
    Controls.Prompt("Type:")
        .OnInputChanged((sender, text, window) =>
        {
            if (string.IsNullOrEmpty(text))
                return;

            var matches = suggestions
                .Where(s => s.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Any())
            {
                var status = window.FindControl<MarkupControl>("suggestions");
                status?.SetContent($"[dim]Suggestions: {string.Join(", ", matches)}[/]");
            }
        })
        .Build()
);

window.AddControl(Controls.Label("").WithName("suggestions").Build());
```

## See Also

- [MultilineEditControl](MultilineEditControl.md) - For multi-line text input
- [ButtonControl](ButtonControl.md) - For form submission
- [CheckboxControl](CheckboxControl.md) - For boolean input

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

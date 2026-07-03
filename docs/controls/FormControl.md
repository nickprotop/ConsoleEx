# FormControl

A labeled-input form that composes real input controls in a two-column `GridControl` layout (label | editor).

## Overview

`FormControl` is an **honest subclass** of `GridControl` — it adds no custom paint or measure code. Each field is a real `MarkupControl` label placed in column 0 and a real input control placed in column 1, via the inherited `GridControl.Place`. Layout, focus, and rendering are handled entirely by the underlying grid.

Column 0 is `GridLength.Auto` (sized to the widest label); column 1 is `GridLength.Star` (takes the remaining width). One row per field is added automatically.

Value access is AOT-safe: each field carries a plain `Func<string?>` delegate — no reflection. `GetValues()` invokes every getter to produce a name→value snapshot.

The primary entry point is `Controls.Form()` which returns a `FormBuilder` for fluent configuration.

> **Note:** A JSON declarative loader (load a form from a descriptor file) is a planned follow-on (Spec #2) — it is not available yet.

See also: [CheckboxControl](CheckboxControl.md), [DropdownControl](DropdownControl.md), [RadioControl](RadioControl.md), [GridControl](GridControl.md)

## Quick Start

```csharp
var form = Controls.Form()
    .AddText("name", "Name:", required: true)
    .AddText("email", "Email:", validate: v => v?.Contains('@') == true ? null : "Must be a valid email")
    .AddDropdown("role", "Role:", new[] { "Admin", "User", "Guest" })
    .WithButtons()
    .OnSubmit(values =>
        windowSystem.NotificationStateService.ShowNotification(
            "Saved", $"Hello, {values["name"]}", NotificationSeverity.Success))
    .Build();

window.AddControl(form);
```

## Field Types

Each `Add*` method places a label in column 0 and an input control in column 1 and returns `this` for fluent chaining. All field methods accept an optional `hint` argument that renders a dim hint line beneath the editor.

### Text — `AddText`

Backed by `PromptControl`. Value is the typed string.

```csharp
form.AddText("username", "Username:", initial: "alice", required: true,
    validate: v => v?.Length >= 3 ? null : "Must be at least 3 characters");
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | `string` | — | Field key |
| `label` | `string` | — | Label text |
| `initial` | `string` | `""` | Initial text |
| `validate` | `Func<string?,string?>?` | `null` | Custom validator; return an error string or `null` for valid |
| `required` | `bool` | `false` | Empty/null value fails validation with `"Required"` |
| `hint` | `string?` | `null` | Dim hint shown below the editor |

### Multiline Text — `AddMultilineEdit`

Backed by `MultilineEditControl`. Value is the full content string.

```csharp
form.AddMultilineEdit("notes", "Notes:", initial: "", height: 4,
    hint: "Markdown accepted");
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | `string` | — | Field key |
| `label` | `string` | — | Label text |
| `initial` | `string` | `""` | Initial content |
| `height` | `int` | `3` | Editor viewport height in rows |
| `hint` | `string?` | `null` | Dim hint |

### Checkbox — `AddCheckbox`

Backed by `CheckboxControl`. Value is `"true"` or `"false"`. The checkbox carries its own label in the editor column; the form's label column is left blank.

```csharp
form.AddCheckbox("tls", "Use TLS", initial: true);
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | `string` | — | Field key |
| `label` | `string` | — | Checkbox label (in editor column) |
| `initial` | `bool` | `false` | Initial checked state |
| `hint` | `string?` | `null` | Dim hint |

### Dropdown — `AddDropdown`

Backed by `DropdownControl`. Value is the selected option string, or `null` when nothing is selected.

```csharp
form.AddDropdown("env", "Environment:", new[] { "dev", "staging", "prod" }, initial: "dev");
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | `string` | — | Field key |
| `label` | `string` | — | Label text |
| `options` | `IEnumerable<string>` | — | Selectable options |
| `initial` | `string?` | `null` | Initially selected value |
| `hint` | `string?` | `null` | Dim hint |

### Radio (typed) — `AddRadio<T>`

Adds a typed single-select field rendered as a group of `RadioControl<T>` instances hosted in a borderless `PanelControl`. `GetEditor("name")` returns the `RadioGroup<T>` for typed access. Value from `GetValues` is `group.SelectedValue?.ToString()`.

```csharp
form.AddRadio("size", "Size:", new[] {
    (Size.Small, "Small"),
    (Size.Medium, "Medium"),
    (Size.Large, "Large"),
});
var sizeGroup = (RadioGroup<Size>)form.GetEditor("size");
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | `string` | — | Field key |
| `label` | `string` | — | Label text |
| `options` | `IEnumerable<(T Value, string Label)>` | — | Option value/label pairs |
| `hint` | `string?` | `null` | Dim hint |

### Radio (string shorthand) — `AddRadio`

Adds a string radio field where each option string is both its value and display label.

```csharp
form.AddRadio("theme", "Theme:", "Light", "Dark", "System");
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `name` | `string` | Field key |
| `label` | `string` | Label text |
| `options` | `params string[]` | Option strings (value = label) |

### Slider — `AddSlider`

Backed by `SliderControl`. Value is the numeric value's string representation.

```csharp
form.AddSlider("timeout", "Timeout (s):", min: 1, max: 120, initial: 30);
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | `string` | — | Field key |
| `label` | `string` | — | Label text |
| `min` | `double` | — | Minimum slider value |
| `max` | `double` | — | Maximum slider value |
| `initial` | `double` | — | Initial value |
| `hint` | `string?` | `null` | Dim hint |

### Custom Field — `AddField`

The escape hatch for controls not covered by the typed overloads.

```csharp
var picker = Controls.DatePicker("").Build();
form.AddField("date", "Date:", picker, () => picker.SelectedDate?.ToString("yyyy-MM-dd"),
    validate: v => v == null ? "Required" : null, required: true);
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `name` | `string` | Field key |
| `label` | `string` | Label text |
| `editor` | `IWindowControl` | The editor control to place in column 1 |
| `valueGetter` | `Func<string?>` | Delegate that reads the editor's current value |
| `validate` | `Func<string?,string?>?` | Optional validator |
| `required` | `bool` | Whether empty/null fails validation |
| `hint` | `string?` | Dim hint |

## Sections

`AddSection` inserts a full-width, col-spanning header row in the flat grid. Every field added after the call belongs to that section until the next `AddSection`.

```csharp
form
    .AddText("host", "Host:")
    .AddSection("Advanced", collapsible: true, startCollapsed: true)
    .AddSlider("port", "Port:", 1, 65535, 5432)
    .AddCheckbox("tls", "Use TLS")
    .AddSection(null)  // end the section; subsequent fields belong to none
    .AddText("comment", "Comment:");
```

When `collapsible: true`, a ▸/▾ toggle button appears in column 1 of the header row. Clicking it toggles `IWindowControl.Visible` on every control of every field in that section and flips the glyph.

When `startCollapsed: true`, the section's fields start hidden and the glyph starts as ▸.

Passing `null` for `title` ends the current section without starting a new one.

## Multi-Field Rows

`AddRow` packs several fields side by side onto a single grid row, each occupying a label/editor column pair:

```csharp
form.AddRow(
    f => f.AddText("city", "City:"),
    f => f.AddText("zip", "ZIP:")
);
```

`MinFieldWidth` (default from `ControlDefaults.FormDefaultMinFieldWidth`) is the documented threshold a host can read to decide whether to stack rows on narrow windows: when the arranged form width is less than `N × MinFieldWidth` for an N-field row, that row is a candidate for stacking. The packed layout is always built.

## Buttons

`WithButtons` adds a right-aligned button row spanning the full form width. The OK button calls `Submit()`; the Cancel button raises `Cancelled`.

```csharp
form.WithButtons(ok: "Save", cancel: "Discard", showCancel: true);
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ok` | `string` | `"OK"` | OK button caption |
| `cancel` | `string` | `"Cancel"` | Cancel button caption |
| `showCancel` | `bool` | `true` | Whether to include the Cancel button |

## Validation

Each field has two validation sources that run in order inside `ValidateField`:

1. **Required check** — when `required: true` and the value is empty or `null`, the error is `"Required"`.
2. **Custom validator** — `Func<string?, string?>` receives the current value and returns an error message string, or `null` for valid.

Error messages are shown in a hidden col-spanning `MarkupControl` line (styled with `ColorRole.Danger`) placed beneath each field. The line becomes visible when the field has an error and is hidden when it passes.

`ValidateOnChange` (default `false`) subscribes each field to its editor's native value-changed event so validation fires immediately on every change. Supported editors: `PromptControl.InputChanged`, `MultilineEditControl.ContentChanged`, `CheckboxControl.CheckedChanged`, `DropdownControl.SelectedValueChanged`, `SliderControl.ValueChanged`, `RadioGroup<T>.SelectionChanged`.

```csharp
form.ValidateOnChange = true;
```

## Reading Values

### `GetValues()`

Returns a `IReadOnlyDictionary<string, string?>` snapshot of all field values at the time of the call.

```csharp
var values = form.GetValues();
string? host = values["host"];
string? port = values["port"];
```

### `GetEditor(string name)`

Returns the value-editor object for the named field. For most fields this is the placed input control; for radio fields it is the `RadioGroup<T>` (cast to the concrete type).

```csharp
var group = (RadioGroup<Size>)form.GetEditor("size");
Size? selected = group.SelectedValue;
```

## Submission and Cancellation

### `Validate()`

Validates all fields. Updates each field's error line. Returns `true` when every field passes. Idempotent.

### `Submit()`

Validates the form. If valid, raises `Submitted` with the current `GetValues()` snapshot. If any field fails, error lines are shown and `Submitted` is not raised.

### `Cancel()`

Raises `Cancelled` directly, without validation.

### Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `Submitted` | `EventHandler<IReadOnlyDictionary<string, string?>>` | Raised by `Submit()` when all fields are valid |
| `Cancelled` | `EventHandler` | Raised by `Cancel()` or by the Cancel button wired via `WithButtons` |

## Controls.Form() Builder API

`Controls.Form()` returns a `FormBuilder` that delegates all `Add*` / section / row / button methods to the underlying `FormControl` and returns `this` for chaining.

### All Builder Methods

| Method | Description |
|--------|-------------|
| `.AddText(name, label, initial, validate, required, hint)` | Single-line text field |
| `.AddMultilineEdit(name, label, initial, height, hint)` | Multi-line editor |
| `.AddCheckbox(name, label, initial, hint)` | Boolean checkbox |
| `.AddDropdown(name, label, options, initial, hint)` | Dropdown selection |
| `.AddRadio<T>(name, label, options, hint)` | Typed radio group |
| `.AddRadio(name, label, params string[] options)` | String radio shorthand |
| `.AddSlider(name, label, min, max, initial, hint)` | Numeric slider |
| `.AddField(name, label, editor, valueGetter, validate, required, hint)` | Custom editor |
| `.AddSection(title, collapsible, startCollapsed)` | Section header row |
| `.AddRow(params Action<FormControl>[] fieldAdders)` | Multi-field row |
| `.WithButtons(ok, cancel, showCancel)` | Action button row |
| `.WithColumnGap(int gap)` | Sets `GridControl.ColumnGap` |
| `.WithRowGap(int gap)` | Sets `GridControl.RowGap` |
| `.WithName(string name)` | Sets `BaseControl.Name` for lookups |
| `.OnSubmit(Action<IReadOnlyDictionary<string,string?>> handler)` | Subscribes to `Submitted` |
| `.OnCancel(Action handler)` | Subscribes to `Cancelled` |
| `.Build()` | Returns the configured `FormControl` |

`FormBuilder` also carries an implicit conversion to `FormControl`, so you can pass a builder directly where a `FormControl` is expected.

## Layout Properties

Because `FormControl` inherits `GridControl`, you can also set:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ColumnGap` | `int` | `0` | Space between label and editor columns |
| `RowGap` | `int` | `0` | Space between rows |
| `MinFieldWidth` | `int` | `ControlDefaults.FormDefaultMinFieldWidth` | Width threshold per field for narrow-wrap hinting |

## Examples

### Simple Contact Form

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

var form = Controls.Form()
    .AddText("name", "Name:", required: true)
    .AddText("email", "Email:",
        validate: v => v?.Contains('@') == true ? null : "Must be a valid email address")
    .AddDropdown("category", "Category:", new[] { "Bug", "Feature", "Question" })
    .AddMultilineEdit("message", "Message:", height: 5, hint: "Describe the issue in detail")
    .WithButtons()
    .OnSubmit(values =>
    {
        windowSystem.NotificationStateService.ShowNotification(
            "Submitted",
            $"{values["name"]} — {values["category"]}",
            NotificationSeverity.Success);
    })
    .OnCancel(() => window.Close())
    .Build();

window.AddControl(form);
```

### Connection Form with Collapsed Advanced Section

```csharp
var form = Controls.Form()
    .AddText("host", "Host:", initial: "localhost", required: true)
    .AddText("database", "Database:", required: true)
    .AddText("user", "User:", required: true)
    .AddText("password", "Password:")

    .AddSection("Advanced", collapsible: true, startCollapsed: true)
    .AddSlider("port", "Port:", min: 1, max: 65535, initial: 5432,
        hint: "Default PostgreSQL port is 5432")
    .AddCheckbox("tls", "Require TLS")
    .AddRadio("sslmode", "SSL Mode:", "disable", "allow", "prefer", "require")
    .AddText("connect_timeout", "Timeout (s):",
        initial: "10",
        validate: v => int.TryParse(v, out var n) && n > 0 ? null : "Must be a positive integer")
    .AddSection(null)   // end Advanced section

    .WithButtons(ok: "Connect", cancel: "Cancel")
    .OnSubmit(values =>
    {
        string connStr = $"Host={values["host"]};Database={values["database"]};" +
                         $"Username={values["user"]};Password={values["password"]};" +
                         $"Port={values["port"]};SSL Mode={values["sslmode"]}";
        Connect(connStr);
    })
    .OnCancel(() => window.Close())
    .Build();

// Read the radio group directly for typed access.
var sslGroup = (RadioGroup<string>)form.GetEditor("sslmode");
sslGroup.SelectedValue = "prefer";

window.AddControl(form);
```

## Composition Notes

`FormControl` is a `GridControl` subclass with **no** `PaintDOM` or `MeasureDOM` override. All layout and painting is inherited from `GridControl`. Fields are not synthetic: each is a real control (`MarkupControl`, `PromptControl`, `CheckboxControl`, etc.) placed via `GridControl.Place`. Sections are rows in the same flat grid, not nested containers. Collapsing a section toggles `IWindowControl.Visible` on each member field's controls and triggers a grid reflow.

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)

# Tutorial 3: Settings App

> **Difficulty:** Intermediate–Advanced | **Prerequisites:** Tutorial 2 or familiarity with windows and layout | **Estimated reading time:** ~20 minutes
>
> **←** [Tutorial 2: Live Dashboard](02-dashboard.md)

---

**What you'll build:** A two-window app with a main window and a Settings window. The Settings window uses `NavigationView` with two pages: Appearance (RGB sliders that change the window gradient live) and General (text inputs and a checkbox). A status bar shows Save/Cancel key hints.

## Step 1: Create the project

Scaffold a new console project and add the SharpConsoleUI package.

```bash
dotnet new console -n SettingsApp
cd SettingsApp
dotnet add package SharpConsoleUI
```

Replace `Program.cs` entirely with the code that follows.

## Step 2: Create the main window

The main window stays open and launches the settings window on demand.

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

var driver = new NetConsoleDriver(RenderMode.Buffer);
var windowSystem = new ConsoleWindowSystem(driver);

Window? settingsWindow = null; // built in Step 5, referenced by the click handler

var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("My App")
    .WithSize(60, 20)
    .Centered()
    .Build();

mainWindow.AddControl(Controls.Markup()
    .AddLine("[bold]Welcome![/] Press the button below to open Settings.")
    .Build());

mainWindow.AddControl(Controls.Button("⚙ Settings")
    .OnClick((s, e, win) =>
    {
        if (settingsWindow != null)
            windowSystem.AddWindow(settingsWindow);
    })
    .Build());

windowSystem.AddWindow(mainWindow);
```

The settings window is declared as `null` here and assigned in Step 5 — the click handler captures it by reference.

## Step 3: Create the settings window

Create the settings window. The `NavigationView` will be added to it in Step 5 once all pages are defined inline.

```csharp
settingsWindow = new WindowBuilder(windowSystem)
    .WithTitle("Settings")
    .WithSize(70, 22)
    .Centered()
    .Build();
```

## Step 4: Declare sliders before the NavigationView builder

The sliders must be declared outside the `NavigationView` builder so that `ValueChanged` handlers can capture them after the builder call. They are added to the Appearance page inline inside `AddItem()`.

```csharp
var redSlider   = Controls.Slider().Horizontal().WithName("r").WithRange(0, 255).WithValue(30).WithStep(1).Build();
var greenSlider = Controls.Slider().Horizontal().WithName("g").WithRange(0, 255).WithValue(60).WithStep(1).Build();
var blueSlider  = Controls.Slider().Horizontal().WithName("b").WithRange(0, 255).WithValue(120).WithStep(1).Build();
```

## Step 5: Build the NavigationView with inline page content

`AddItem()` accepts an optional `content:` factory — `NavigationView` calls it when the item is first selected, passing the content `ScrollablePanelControl` for you to populate. Both pages are defined inline here.

```csharp
var nav = Controls.NavigationView()
    .WithPaneHeader("[bold]Settings[/]")
    .AddItem("Appearance", "🎨", content: panel =>
    {
        panel.AddControl(Controls.Markup().AddLine("[bold]Background Color[/]").Build());
        panel.AddControl(Controls.Markup().AddLine("Red").Build());
        panel.AddControl(redSlider);
        panel.AddControl(Controls.Markup().AddLine("Green").Build());
        panel.AddControl(greenSlider);
        panel.AddControl(Controls.Markup().AddLine("Blue").Build());
        panel.AddControl(blueSlider);
    })
    .AddItem("General", "⚙", content: panel =>
    {
        panel.AddControl(Controls.Markup().AddLine("[bold]General[/]").Build());
        panel.AddControl(Controls.Markup().AddLine("Display name:").Build());
        panel.AddControl(Controls.Prompt("Display name:").WithName("displayName").Build());
        panel.AddControl(Controls.Markup().AddLine("API endpoint:").Build());
        panel.AddControl(Controls.Prompt("API endpoint:").WithName("apiEndpoint").Build());
        panel.AddControl(Controls.Checkbox("Enable notifications").Checked().Build());
    })
    .Fill()
    .Build();

settingsWindow.AddControl(nav);
```

`NavigationView` auto-selects the first item on load. `.Fill()` sets `VerticalAlignment.Fill` so the nav spans the full window height.

Alternatively, subscribe to `nav.SelectedItemChanged` and manipulate `nav.ContentPanel` directly for content that depends on external runtime state.

## Step 6: Wire sliders to the gradient

Each slider's `ValueChanged` event fires on every drag tick — use it to update the window background gradient immediately.

```csharp
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;

void ApplyGradient()
{
    var color = new Color(
        (byte)redSlider.Value,
        (byte)greenSlider.Value,
        (byte)blueSlider.Value);

    settingsWindow!.BackgroundGradient = new GradientBackground(
        ColorGradient.FromColors(color, Color.Black),
        GradientDirection.Vertical);
}

redSlider.ValueChanged   += (s, e) => ApplyGradient();
greenSlider.ValueChanged += (s, e) => ApplyGradient();
blueSlider.ValueChanged  += (s, e) => ApplyGradient();
```

`GradientBackground` is a record combining a `ColorGradient` and a `GradientDirection`. `ColorGradient.FromColors()` interpolates smoothly between the stops. See [Gradients & Alpha](../GRADIENTS.md) for predefined gradients and more patterns.

## Step 7: Add a status bar and keyboard shortcuts

`StatusBarControl` is display-and-click-only — it shows key hint text but does NOT intercept keyboard events; wire shortcuts separately via `window.PreviewKeyPressed`.

```csharp
settingsWindow.AddControl(Controls.StatusBar()
    .AddLeft("Ctrl+S", "Save")
    .AddLeft("Esc", "Cancel")
    .Build());

// PreviewKeyPressed fires before the focused control sees the key — correct for global shortcuts.
settingsWindow.PreviewKeyPressed += (s, e) =>
{
    if (e.KeyInfo.Key == ConsoleKey.Escape)
    {
        windowSystem.CloseWindow(settingsWindow);
        e.Handled = true;
    }
    else if (e.KeyInfo.Key == ConsoleKey.S && e.KeyInfo.Modifiers == ConsoleModifiers.Control)
    {
        SaveAndClose();
        e.Handled = true;
    }
};
```

Pressing Esc closes the settings window and returns focus to the main window. Ctrl+S calls `SaveAndClose()` which applies the gradient and closes the window.

## Step 8: Open and close the settings window

`windowSystem.AddWindow()` activates the settings window on top; closing it removes it from the stack and returns focus to the main window.

```csharp
void SaveAndClose()
{
    ApplyGradient(); // gradient is already live; call to ensure final state is applied
    windowSystem.CloseWindow(settingsWindow!);
}

// Add a Save button in the nav toolbar:
nav.AddContentToolbarButton("Save", (s, e) => SaveAndClose());
// Note: the handler type is EventHandler<ButtonControl> — e is the ButtonControl, not EventArgs.
```

The library also supports modal dialogs (`windowSystem.ShowDialog(...)`) that block the calling window — see [Dialogs](../DIALOGS.md). This tutorial uses a plain second window for simplicity.

## Complete Program.cs

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

var driver = new NetConsoleDriver(RenderMode.Buffer);
var windowSystem = new ConsoleWindowSystem(driver);

// ── Sliders declared before the NavigationView builder so ValueChanged can capture them ──
var redSlider   = Controls.Slider().Horizontal().WithName("r").WithRange(0, 255).WithValue(30).WithStep(1).Build();
var greenSlider = Controls.Slider().Horizontal().WithName("g").WithRange(0, 255).WithValue(60).WithStep(1).Build();
var blueSlider  = Controls.Slider().Horizontal().WithName("b").WithRange(0, 255).WithValue(120).WithStep(1).Build();

// ── Settings window (assigned below; captured by the Settings button click handler) ──
Window? settingsWindow = null;

// ── Main window ──
var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("My App")
    .WithSize(60, 20)
    .Centered()
    .Build();

mainWindow.AddControl(Controls.Markup()
    .AddLine("[bold]Welcome![/] Press the button below to open Settings.")
    .Build());

mainWindow.AddControl(Controls.Button("⚙ Settings")
    .OnClick((s, e, win) =>
    {
        if (settingsWindow != null)
            windowSystem.AddWindow(settingsWindow);
    })
    .Build());

windowSystem.AddWindow(mainWindow);

// ── Settings window ──
settingsWindow = new WindowBuilder(windowSystem)
    .WithTitle("Settings")
    .WithSize(70, 22)
    .Centered()
    .Build();

// ── NavigationView with inline page content ──
var nav = Controls.NavigationView()
    .WithPaneHeader("[bold]Settings[/]")
    .AddItem("Appearance", "🎨", content: panel =>
    {
        panel.AddControl(Controls.Markup().AddLine("[bold]Background Color[/]").Build());
        panel.AddControl(Controls.Markup().AddLine("Red").Build());
        panel.AddControl(redSlider);
        panel.AddControl(Controls.Markup().AddLine("Green").Build());
        panel.AddControl(greenSlider);
        panel.AddControl(Controls.Markup().AddLine("Blue").Build());
        panel.AddControl(blueSlider);
    })
    .AddItem("General", "⚙", content: panel =>
    {
        panel.AddControl(Controls.Markup().AddLine("[bold]General[/]").Build());
        panel.AddControl(Controls.Markup().AddLine("Display name:").Build());
        panel.AddControl(Controls.Prompt("Display name:").WithName("displayName").Build());
        panel.AddControl(Controls.Markup().AddLine("API endpoint:").Build());
        panel.AddControl(Controls.Prompt("API endpoint:").WithName("apiEndpoint").Build());
        panel.AddControl(Controls.Checkbox("Enable notifications").Checked().Build());
    })
    .Fill()
    .Build();

settingsWindow.AddControl(nav);

// ── Gradient helper + slider wiring ──
void ApplyGradient()
{
    var color = new Color(
        (byte)redSlider.Value,
        (byte)greenSlider.Value,
        (byte)blueSlider.Value);

    settingsWindow!.BackgroundGradient = new GradientBackground(
        ColorGradient.FromColors(color, Color.Black),
        GradientDirection.Vertical);
}

redSlider.ValueChanged   += (s, e) => ApplyGradient();
greenSlider.ValueChanged += (s, e) => ApplyGradient();
blueSlider.ValueChanged  += (s, e) => ApplyGradient();

// ── Status bar ──
settingsWindow.AddControl(Controls.StatusBar()
    .AddLeft("Ctrl+S", "Save")
    .AddLeft("Esc", "Cancel")
    .Build());

// ── Keyboard shortcuts via PreviewKeyPressed ──
settingsWindow.PreviewKeyPressed += (s, e) =>
{
    if (e.KeyInfo.Key == ConsoleKey.Escape)
    {
        windowSystem.CloseWindow(settingsWindow);
        e.Handled = true;
    }
    else if (e.KeyInfo.Key == ConsoleKey.S && e.KeyInfo.Modifiers == ConsoleModifiers.Control)
    {
        SaveAndClose();
        e.Handled = true;
    }
};

// ── Save toolbar button ──
void SaveAndClose()
{
    ApplyGradient();
    windowSystem.CloseWindow(settingsWindow!);
}

nav.AddContentToolbarButton("Save", (s, e) => SaveAndClose());

windowSystem.Run();
```

## What you learned

- `Controls.NavigationView()` builder — `WithPaneHeader()`, `AddItem(..., content:)`, and `.Fill()` replace manual `new NavigationView { ... }` + `SetItemContent()` calls
- `nav.ContentPanel` + `SelectedItemChanged` — alternative for dynamic content that depends on external runtime state
- `Controls.Slider()` builder — `.Horizontal()`, `.WithName()`, `.WithRange()`, `.WithValue()`, `.WithStep()`
- `SliderControl.ValueChanged` — fires on every drag tick for live updates
- `Controls.Markup().AddLine()` — replaces `new MarkupControl(new List<string> { ... })`
- `Controls.Prompt().WithName()` — replaces `new PromptControl { Name = ... }`
- `Controls.Checkbox().Checked()` — replaces `new CheckboxControl { IsChecked = true }`
- `Controls.StatusBar().AddLeft()` — replaces `new StatusBarControl()` + `AddItem()` calls
- `GradientBackground` + `ColorGradient.FromColors()` + `GradientDirection` — live window background gradients
- Form layout: `PromptControl` + `CheckboxControl` in a `ScrollablePanelControl` — no special form container needed
- `StatusBarControl` — display+click only; does not intercept key events
- `window.PreviewKeyPressed` — global shortcut wiring (fires before focused control sees the key)
- `e.KeyInfo.Key` — correct property on `KeyPressedEventArgs` (`.KeyInfo` is `ConsoleKeyInfo`)
- Multi-window: `windowSystem.AddWindow()` / `windowSystem.CloseWindow()` — stack-based window management

---

**←** [Tutorial 2: Live Dashboard](02-dashboard.md)

This is the final tutorial in the series.

You've now covered the core SharpConsoleUI patterns. Explore the [Controls Reference](../CONTROLS.md), [Builders Reference](../BUILDERS.md), and project templates (`dotnet new tui-app`) for next steps.

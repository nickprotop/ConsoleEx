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

Window? settingsWindow = null; // built in Step 3, referenced by the click handler

var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("My App")
    .WithSize(60, 20)
    .Centered()
    .Build();

mainWindow.AddControl(new MarkupControl(
    new List<string> { "[bold]Welcome![/] Press the button below to open Settings." }));

mainWindow.AddControl(Controls.Button("⚙ Settings")
    .OnClick((s, e, win) =>
    {
        if (settingsWindow != null)
            windowSystem.AddWindow(settingsWindow);
    })
    .Build());

windowSystem.AddWindow(mainWindow);
```

The settings window is declared as `null` here and assigned in Step 3 — the click handler captures it by reference.

## Step 3: Create the settings window and NavigationView

Create the settings window and add a `NavigationView` that fills it — the nav pane is on the left, the content panel on the right.

```csharp
settingsWindow = new WindowBuilder(windowSystem)
    .WithTitle("Settings")
    .WithSize(70, 22)
    .Centered()
    .Build();

var nav = new NavigationView
{
    PaneHeader = "[bold]Settings[/]",
    VerticalAlignment = VerticalAlignment.Fill
};
settingsWindow.AddControl(nav);
```

The `NavigationView` has two zones: a narrow pane on the left for nav items, and a content panel on the right. You'll populate both in the next two steps.

## Step 4: Add navigation items

Each `NavigationItem` represents a page — first argument is the display name, second is an optional icon.

```csharp
var appearanceItem = nav.AddItem("Appearance", "🎨");
var generalItem    = nav.AddItem("General",    "⚙");
```

`NavigationView` auto-selects the first item on load. You can pass a subtitle as an optional third argument.

## Step 5: Populate the Appearance page

`SetItemContent` registers a factory — `NavigationView` calls it when the item is selected, passing the content `ScrollablePanelControl` for you to populate.

```csharp
// Declared outside the factory so we can read them in ValueChanged:
var redSlider   = new SliderControl { Name = "r", MinValue = 0, MaxValue = 255, Value = 30,  Step = 1 };
var greenSlider = new SliderControl { Name = "g", MinValue = 0, MaxValue = 255, Value = 60,  Step = 1 };
var blueSlider  = new SliderControl { Name = "b", MinValue = 0, MaxValue = 255, Value = 120, Step = 1 };

nav.SetItemContent(appearanceItem, panel =>
{
    panel.AddControl(new MarkupControl(new List<string> { "[bold]Background Color[/]" }));
    panel.AddControl(new MarkupControl(new List<string> { "Red" }));
    panel.AddControl(redSlider);
    panel.AddControl(new MarkupControl(new List<string> { "Green" }));
    panel.AddControl(greenSlider);
    panel.AddControl(new MarkupControl(new List<string> { "Blue" }));
    panel.AddControl(blueSlider);
});
```

> **Alternative:** Skip `SetItemContent`, subscribe to `nav.SelectedItemChanged`, and manipulate `nav.ContentPanel` directly — useful when content depends on external state not known at registration time. See [NavigationView reference](../controls/NavigationView.md).

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

## Step 7: Populate the General page

The General page is a simple form — labels, text inputs, and a checkbox stacked vertically in the content panel.

```csharp
nav.SetItemContent(generalItem, panel =>
{
    panel.AddControl(new MarkupControl(new List<string> { "[bold]General[/]" }));

    panel.AddControl(new MarkupControl(new List<string> { "Display name:" }));
    panel.AddControl(new PromptControl { Name = "displayName", PlaceholderText = "Enter name..." });

    panel.AddControl(new MarkupControl(new List<string> { "API endpoint:" }));
    panel.AddControl(new PromptControl { Name = "apiEndpoint", PlaceholderText = "https://..." });

    panel.AddControl(new CheckboxControl { Text = "Enable notifications", IsChecked = true });
});
```

Form fields are standard controls inside a `ScrollablePanelControl` — no special form container needed.

## Step 8: Add a status bar and keyboard shortcuts

`StatusBarControl` is display-and-click-only — it shows key hint text but does NOT intercept keyboard events; wire shortcuts separately via `window.PreviewKeyPressed`.

```csharp
var statusBar = new StatusBarControl();
statusBar.AddItem(new StatusBarItem { Shortcut = "Ctrl+S", Label = "Save"   });
statusBar.AddItem(new StatusBarItem { Shortcut = "Esc",    Label = "Cancel" });
settingsWindow.AddControl(statusBar);

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

## Step 9: Open and close the settings window

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

// ── Sliders declared before SetItemContent factories so ValueChanged can capture them ──
var redSlider   = new SliderControl { Name = "r", MinValue = 0, MaxValue = 255, Value = 30,  Step = 1 };
var greenSlider = new SliderControl { Name = "g", MinValue = 0, MaxValue = 255, Value = 60,  Step = 1 };
var blueSlider  = new SliderControl { Name = "b", MinValue = 0, MaxValue = 255, Value = 120, Step = 1 };

// ── Settings window (assigned below; captured by the Settings button click handler) ──
Window? settingsWindow = null;

// ── Main window ──
var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("My App")
    .WithSize(60, 20)
    .Centered()
    .Build();

mainWindow.AddControl(new MarkupControl(
    new List<string> { "[bold]Welcome![/] Press the button below to open Settings." }));

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

var nav = new NavigationView
{
    PaneHeader = "[bold]Settings[/]",
    VerticalAlignment = VerticalAlignment.Fill
};
settingsWindow.AddControl(nav);

var appearanceItem = nav.AddItem("Appearance", "🎨");
var generalItem    = nav.AddItem("General",    "⚙");

// Appearance page
nav.SetItemContent(appearanceItem, panel =>
{
    panel.AddControl(new MarkupControl(new List<string> { "[bold]Background Color[/]" }));
    panel.AddControl(new MarkupControl(new List<string> { "Red" }));
    panel.AddControl(redSlider);
    panel.AddControl(new MarkupControl(new List<string> { "Green" }));
    panel.AddControl(greenSlider);
    panel.AddControl(new MarkupControl(new List<string> { "Blue" }));
    panel.AddControl(blueSlider);
});

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

// General page
nav.SetItemContent(generalItem, panel =>
{
    panel.AddControl(new MarkupControl(new List<string> { "[bold]General[/]" }));
    panel.AddControl(new MarkupControl(new List<string> { "Display name:" }));
    panel.AddControl(new PromptControl { Name = "displayName", PlaceholderText = "Enter name..." });
    panel.AddControl(new MarkupControl(new List<string> { "API endpoint:" }));
    panel.AddControl(new PromptControl { Name = "apiEndpoint", PlaceholderText = "https://..." });
    panel.AddControl(new CheckboxControl { Text = "Enable notifications", IsChecked = true });
});

// Status bar
var statusBar = new StatusBarControl();
statusBar.AddItem(new StatusBarItem { Shortcut = "Ctrl+S", Label = "Save"   });
statusBar.AddItem(new StatusBarItem { Shortcut = "Esc",    Label = "Cancel" });
settingsWindow.AddControl(statusBar);

// Keyboard shortcuts via PreviewKeyPressed
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

// Save toolbar button
void SaveAndClose()
{
    ApplyGradient();
    windowSystem.CloseWindow(settingsWindow!);
}

nav.AddContentToolbarButton("Save", (s, e) => SaveAndClose());

windowSystem.Run();
```

## What you learned

- `NavigationView` — pane + content panel layout
- `nav.AddItem()` — register navigation items with icon and optional subtitle
- `nav.SetItemContent()` — factory pattern for populating content panels
- `nav.ContentPanel` + `SelectedItemChanged` — alternative for dynamic content when content depends on external state not known at registration time
- `SliderControl` with `ValueChanged` — fires on every drag tick for live updates
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

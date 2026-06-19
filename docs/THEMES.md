# Theme System

SharpConsoleUI includes a powerful theme system that allows runtime theme switching and custom theme creation.

## Table of Contents

- [Built-in Themes](#built-in-themes)
- [Using Themes](#using-themes)
- [Deriving a Theme from Another (Recommended)](#deriving-a-theme-from-another-recommended)
- [Control Roles](#control-roles)
- [Creating Custom Themes from Scratch](#creating-custom-themes-from-scratch)
- [Theme Registry](#theme-registry)
- [Runtime Theme Switching](#runtime-theme-switching)

## Built-in Themes

`ModernGray` is the default theme. On top of it, the library registers a catalog of
palette-generated **seed themes** out of the box — **Ocean**, **Amber**, **Forest**, **Crimson**,
**Slate** (dark) and **Daylight** (light) — so every app gets a ready-made selection without any
setup. See [Built-in seed themes](#built-in-seed-themes) for details.

### ModernGray Theme (Default)

Modern dark theme with gray color scheme.

```csharp
windowSystem.ThemeStateService.SwitchTheme("ModernGray");
```

**Color Scheme:**
- Window Background: Grey11 (very dark gray)
- Window Foreground: Grey93 (light gray)
- Active Border: DeepSkyBlue1
- Inactive Border: Grey35
- Active Title: White
- Inactive Title: Grey50
- Desktop Background: Grey7
- Desktop Foreground: Grey70

## Using Themes

### Setting Theme at Startup

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Themes;

// Option 1: Use default theme (ModernGray)
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Option 2: Specify theme by name
var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    themeName: "Ocean"
);

// Option 3: Provide theme instance
var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    theme: new ModernGrayTheme()
);
```

### Changing Theme at Runtime

```csharp
// Switch by registered name
windowSystem.ThemeStateService.SwitchTheme("Ocean");

// Set a theme instance directly
windowSystem.ThemeStateService.SetTheme(new ModernGrayTheme());

// Using built-in theme selector dialog
windowSystem.ShowThemeSelectorDialog();
```

## Deriving a Theme from Another (Recommended)

Most of the time you don't want a theme from scratch — you want an existing theme with a few
colors changed. `Theme.From(...)` copies **every** member of a base theme into a new mutable theme,
then lets you override only what you care about with `.With(...)`:

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Themes;

var myDark = Theme.From(new ModernGrayTheme())   // copy all members from any ITheme
    .WithName("MyDark")
    .WithDescription("My dark variant")
    .With(t =>
    {
        t.ButtonBackgroundColor = Color.DarkRed;
        t.ActiveBorderForegroundColor = Color.Orange1;
        t.ScrollbarThumbColor = Color.Orange1;
    })
    .Build();                                      // returns the (mutable) theme

windowSystem.ThemeRegistryService.RegisterTheme("MyDark", "My dark variant", () => myDark);
windowSystem.ThemeStateService.SwitchTheme("MyDark");
```

- **`.With(Action<MutableTheme>)`** covers every theme member with full IntelliSense and
  compile-time safety — there are no per-property builder methods to memorize, and new theme
  members are automatically reachable. Call it multiple times to accumulate overrides.
- **The result is mutable by design.** `Build()` returns the working `MutableTheme` itself (no
  freeze, no copy). It's a single shared instance, so you can keep tweaking it after registration
  and the change flows through to the live theme.
- The derived theme is a normal registered theme: it appears in the theme selector dialog and is
  switchable by name like any built-in theme.

### Themes are mutable — set colors directly

Every theme color (including the built-in `ModernGrayTheme`) is a settable
property, so you can also mutate the live theme directly without the builder:

```csharp
windowSystem.Theme.ScrollbarThumbColor = Color.Red;   // takes effect on the next repaint
```

`Theme.From(...).With(...)` is just the convenient, named-and-registered way to do this in bulk
starting from a known base.

## Generating a Theme from a Palette

When you don't want to pick dozens of colors by hand, give the generator a small **palette** (often
just a primary accent and a background) and it derives a complete, contrast-checked theme. Every
foreground is chosen to read against the surface it actually lands on, so generated themes never
produce invisible text:

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Themes;

var ocean = Theme.FromPalette(new Palette
{
    Primary    = Color.FromHex("#2DD4BF"),  // accent (borders, highlights, focus)
    Background  = Color.FromHex("#0B1F2A"),  // window/desktop surface
});

windowSystem.ThemeRegistryService.RegisterTheme("Ocean", "Teal on deep dark", () => ocean);
windowSystem.ThemeStateService.SwitchTheme("Ocean");
```

`Palette` is a record; every member is optional and anything you omit is derived from what you
provide:

| Member | Role |
|--------|------|
| `Primary` | Main accent — borders, focus, highlights, progress fills |
| `Secondary`, `Tertiary` | Optional extra accents |
| `Background` | The base surface; control/desktop/modal surfaces are tinted/shaded from it |
| `Foreground` | Optional override for the default text color (otherwise derived for contrast) |
| `Success`, `Warning`, `Danger`, `Info` | Optional status colors (sensible defaults otherwise) |
| `Mode` | Optional `ThemeMode.Light`/`Dark` override (see below) |

### Light vs. dark detection

The generator infers light/dark from the `Background` luminance and flips text accordingly — a light
background gets dark text automatically. Set `Mode` explicitly only when you want to override that
inference:

```csharp
var daylight = Theme.FromPalette(new Palette
{
    Primary    = Color.FromHex("#2563EB"),
    Background  = Color.FromHex("#F8FAFC"),  // light surface → dark text, automatically
    Mode        = ThemeMode.Light,            // optional: pin the identity
});
```

`ThemeMode` is also surfaced on every theme via `ITheme.Mode`, so apps can read
`windowSystem.Theme.Mode` to adapt their own content (e.g. choose a light or dark logo).

### Built-in seed themes

The library registers a handful of palette-generated themes out of the box, so every app gets a
ready-made selection in the theme selector without any setup: **Ocean**, **Amber**, **Forest**,
**Crimson**, **Slate** (dark) and **Daylight** (light). `ModernGray` remains the default. These are
just normal registered themes — switch to them by name like any other.

## Control Roles

Instead of setting individual colors on a control, you can give it a **semantic role** and let the
active theme supply a coordinated set of colors. A role describes a control's *purpose* — a delete
button is `Danger`, a confirmation is `Success` — and the theme decides what those look like. Switch
themes and every roled control re-derives from the new palette automatically.

### The roles

```csharp
public enum ControlRole
{
    Default,    // no role — the control resolves colors as it normally would
    Primary, Secondary, Tertiary,
    Info, Success, Warning, Danger
}
```

`Default` (the default) leaves a control's color resolution exactly as it was — roles are fully
additive, so existing code is unaffected.

### Applying a role

Every control inherits two properties from `BaseControl`:

```csharp
button.Role = ControlRole.Danger;   // colors derived from the theme's Danger palette
button.Outline = true;              // outline style: role color on text + border, surface fill
```

Or fluently via the builders:

```csharp
// A danger button
new ButtonBuilder().WithText("Delete account").WithRole(ControlRole.Danger).Build();

// An outline success button (surface fill, green text + border)
new ButtonBuilder().WithText("Confirm").WithRole(ControlRole.Success).Outline().Build();

// Primary / secondary action pair
new ButtonBuilder().WithText("Save").WithRole(ControlRole.Primary).Build();
new ButtonBuilder().WithText("Cancel").WithRole(ControlRole.Secondary).Outline().Build();
```

A per-control explicit color always wins over the role, so you can override a single slot and let the
rest come from the role:

```csharp
new ButtonBuilder()
    .WithRole(ControlRole.Danger)        // danger text + border…
    .WithBackgroundColor(Color.Black)    // …but a specific black fill
    .Build();
```

The same code is theme-agnostic — `WithRole(ControlRole.Danger)` resolves to Ocean's danger red under
Ocean, Maroon under ModernGray, and so on.

### Where the colors come from

A role resolves to a coordinated set — `{ Text, Background, TextOnBackground, Border }` plus
focus/disabled state variants — derived from the theme's seed colors and its window foreground/
background anchors. The theme surfaces up to seven nullable seed colors; anything left unset is
derived (secondary/tertiary from primary, status colors from mode-tuned defaults):

```csharp
// On any ITheme (ThemeBase / MutableTheme / a palette-generated theme):
Color? PrimaryColor   { get; }
Color? SecondaryColor { get; }
Color? TertiaryColor  { get; }
Color? InfoColor      { get; }
Color? SuccessColor   { get; }
Color? WarningColor   { get; }
Color? DangerColor    { get; }
```

`Theme.FromPalette(...)` populates all seven from the palette, and `ModernGray` authors them
explicitly, so every built-in theme has a full, coherent role palette out of the box. A custom theme
that sets only `PrimaryColor` still gets every role — the rest are derived.

### Which controls honor roles

Controls that respond to roles implement the `IRoleableControl` capability interface (exposing the
`Role` and `Outline` properties). That covers the bulk of the library: interactive controls (Button,
Checkbox, Dropdown, Slider, the pickers…), containers/chrome (Panel, CollapsiblePanel, TabControl,
Toolbar, StatusBar…), indicators (ProgressBar, Spinner, the graphs), data controls (List, Table,
Tree) and text controls (Markup, FIGlet). For data controls the role themes the **whole item
surface** across states — normal rows take a role-tinted foreground, hover and selection take graded
role fills — so the control reads as its role even with nothing selected. For `MarkupControl` the
role sets the *default* foreground; inline `[color]` tags still win.

A few controls have no single themed surface — `CanvasControl`, `ImageControl`, `VideoControl`,
`HtmlControl`, `SpectreRenderableControl`, `LogViewerControl`, `HorizontalGridControl` — so they do
**not** implement `IRoleableControl` and have no `Role` property to set.

### Defaults by purpose

Built-in components with an inherent purpose set their own role by default — notifications map their
severity to a role (a `Danger` notification is themed by the Danger role) — so you get coherent,
role-consistent UI without setting anything.

## Creating Custom Themes from Scratch

If you need a fully independent theme, implement the `ITheme` interface directly. (Most members
have sensible interface defaults, so you only implement what differs.)

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Themes;

public class MyCustomTheme : ITheme
{
    public Color WindowBackgroundColor => Color.DarkSlateGray;
    public Color WindowForegroundColor => Color.White;
    public Color ActiveBorderForegroundColor => Color.Cyan;
    public Color InactiveBorderForegroundColor => Color.DarkGray;
    public Color ActiveTitleForegroundColor => Color.Yellow;
    public Color InactiveTitleForegroundColor => Color.Gray;
    public Color DesktopBackgroundColor => Color.Black;
    public Color DesktopForegroundColor => Color.DarkGray;
    public char DesktopBackroundChar => '░';
}
```

> **Tip:** Prefer deriving from `ThemeBase` instead of implementing `ITheme` by hand. `ThemeBase`
> provides settable `{ get; set; }` defaults for **all** members (blank/transparent where a value
> isn't meaningful), so you `override` only the handful you care about and stay forward-compatible
> when new members are added. The built-in `ModernGrayTheme` derives from it.

```csharp
public class MyCustomTheme : ThemeBase
{
    public override string Name { get; set; } = "MyCustom";
    public override Color WindowBackgroundColor { get; set; } = Color.DarkSlateGray;
    public override Color ActiveBorderForegroundColor { get; set; } = Color.Cyan;
    // ...override only what differs; everything else falls back to ThemeBase defaults
}
```

### Per-control colors and transparency

Controls (Dropdown, List, Checkbox, DatePicker, Html, …) have their **own** theme members rather
than borrowing the button's colors, so you can style each control independently. Control
**background** members are nullable (`Color?`): leave one `null` (or `Color.Default`) to let the
control inherit the window background and composite transparently, instead of painting an opaque
block. Generated themes use this so a control sits naturally on its window surface.

### Theme Properties

| Property | Description |
|----------|-------------|
| `WindowBackgroundColor` | Background color for window content area |
| `WindowForegroundColor` | Default text color for window content |
| `ActiveBorderForegroundColor` | Border color for focused window |
| `InactiveBorderForegroundColor` | Border color for non-focused windows |
| `ActiveTitleForegroundColor` | Title bar color for focused window |
| `InactiveTitleForegroundColor` | Title bar color for non-focused windows |
| `DesktopBackgroundColor` | Background color for empty desktop space |
| `DesktopForegroundColor` | Foreground color for desktop character |
| `DesktopBackgroundChar` | Character used to fill desktop background |
| `DesktopBackgroundGradient` | Optional gradient for the desktop area (default: `null`). See [Desktop Background](DESKTOP_BACKGROUND.md) |

### Registering Custom Themes

```csharp
// Register a custom theme
windowSystem.ThemeRegistryService.RegisterTheme("MyCustomTheme", "MyCustomTheme theme", () => new MyCustomTheme());

// Now you can switch to it
windowSystem.ThemeStateService.SwitchTheme("MyCustomTheme");
```

### Complete Custom Theme Example

```csharp
using SharpConsoleUI;
using SharpConsoleUI;
using SharpConsoleUI.Themes;
using SharpConsoleUI.Drivers;

public class SolarizedDarkTheme : ITheme
{
    // Base colors from Solarized Dark palette
    public Color WindowBackgroundColor => new Color(0, 43, 54);      // base03
    public Color WindowForegroundColor => new Color(131, 148, 150);  // base0
    public Color ActiveBorderForegroundColor => new Color(38, 139, 210);   // blue
    public Color InactiveBorderForegroundColor => new Color(88, 110, 117); // base01
    public Color ActiveTitleForegroundColor => new Color(181, 137, 0);     // yellow
    public Color InactiveTitleForegroundColor => new Color(88, 110, 117);  // base01
    public Color DesktopBackgroundColor => new Color(7, 54, 66);      // base02
    public Color DesktopForegroundColor => new Color(88, 110, 117);   // base01
    public char DesktopBackroundChar => '·';
}

// Usage
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
windowSystem.ThemeRegistryService.RegisterTheme("SolarizedDark", "SolarizedDark theme", () => new SolarizedDarkTheme());
windowSystem.ThemeStateService.SwitchTheme("SolarizedDark");
```

## Theme Registry

Each `ConsoleWindowSystem` has its own theme registry, `windowSystem.ThemeRegistryService`, which
manages theme registration and lookup. Themes registered here — including those contributed by a
loaded plugin — are scoped to that window system and do not leak to other instances. To *switch*
the active theme, use `windowSystem.ThemeStateService.SwitchTheme(name)`.

### Available Methods

```csharp
// Register a new theme
windowSystem.ThemeRegistryService.RegisterTheme("MyTheme", "MyTheme theme", () => new MyCustomTheme());

// Set active theme by name
windowSystem.ThemeStateService.SwitchTheme("MyTheme");

// Get a theme by name
ITheme? theme = windowSystem.ThemeRegistryService.GetTheme("Ocean");

// Get all registered theme names
IEnumerable<string> themes = windowSystem.ThemeRegistryService.GetAvailableThemeNames();

// Check if theme exists
bool exists = windowSystem.ThemeRegistryService.IsThemeRegistered("MyTheme");

// Get default theme
ITheme defaultTheme = windowSystem.ThemeRegistryService.GetDefaultTheme();

// Get theme or fallback to default
ITheme theme = windowSystem.ThemeRegistryService.GetThemeOrDefault("NonExistent", new ModernGrayTheme());
```

### List All Available Themes

```csharp
var themes = windowSystem.ThemeRegistryService.GetAvailableThemeNames();
foreach (var themeName in themes)
{
    Console.WriteLine($"- {themeName}");
}
```

## Runtime Theme Switching

Themes can be changed at any time and apply immediately to all windows.

### Using Theme Selector Dialog

The easiest way for users to change themes:

```csharp
windowSystem.ShowThemeSelectorDialog();
```

### Programmatic Theme Switching

```csharp
// Add a button to switch themes
mainWindow.AddControl(
    Controls.Button("Switch to Daylight")
        .OnClick((sender, e, window) =>
        {
            windowSystem.ThemeStateService.SwitchTheme("Daylight");
        })
        .Build()
);

// Cycle through themes
var themes = windowSystem.ThemeRegistryService.GetAvailableThemeNames().ToList();
int currentIndex = 0;

mainWindow.AddControl(
    Controls.Button("Next Theme")
        .OnClick((sender, e, window) =>
        {
            currentIndex = (currentIndex + 1) % themes.Count;
            windowSystem.ThemeStateService.SwitchTheme(themes[currentIndex]);

            windowSystem.NotificationStateService.ShowNotification(
                "Theme Changed",
                $"Switched to {themes[currentIndex]}",
                NotificationSeverity.Info
            );
        })
        .Build()
);
```

### Theme Switching with Keyboard Shortcut

```csharp
mainWindow.KeyPressed += (sender, e) =>
{
    if (e.KeyInfo.Key == ConsoleKey.F9)
    {
        windowSystem.ShowThemeSelectorDialog();
        e.Handled = true;
    }
};
```

## Best Practices

1. **Use high contrast**: Ensure good readability between background and foreground colors
2. **Test both states**: Verify both active and inactive window states look good
3. **Consider desktop**: Desktop colors should not distract from windows
4. **Name descriptively**: Use clear theme names (e.g., "DarkBlue" not "Theme1")
5. **Register early**: Register custom themes before showing any windows
6. **Provide selector**: Give users a way to change themes (F9 is recommended)

## Color Guidelines

### Recommended Color Combinations

**Dark Themes:**
- Background: Black, very dark gray (Grey7-Grey15)
- Foreground: White, light gray (Grey85-Grey93)
- Active Border: Bright colors (Cyan, Blue, Yellow)
- Inactive Border: Medium gray (Grey30-Grey50)

**Light Themes:**
- Background: White, very light gray (Grey93-Grey100)
- Foreground: Black, dark gray (Grey7-Grey15)
- Active Border: Medium-dark colors
- Inactive Border: Light gray (Grey70-Grey85)

### Color Support

SharpConsoleUI provides a 24-bit Color type. Available color types:

```csharp
// Named colors
Color.Red, Color.Blue, Color.Green, Color.Yellow, etc.

// RGB colors
new Color(255, 128, 0)  // Orange

// Hex colors
Color.FromInt32(0xFF8000)

// Gray scale (Grey0 to Grey100)
Color.Grey0   // Black
Color.Grey50  // Medium gray
Color.Grey100 // White
```

## Complete Theme Manager Example

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using SharpConsoleUI.Drivers;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Register custom themes
windowSystem.ThemeRegistryService.RegisterTheme("SolarizedDark", "SolarizedDark theme", () => new SolarizedDarkTheme());
windowSystem.ThemeRegistryService.RegisterTheme("Dracula", "Dracula theme", () => new DraculaTheme());

var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("Theme Manager")
    .WithSize(60, 20)
    .Centered()
    .Build();

// Add theme selector button
mainWindow.AddControl(new MarkupControl(new List<string>
{
    "[bold yellow]Available Themes:[/]",
    ""
}));

// Create buttons for each theme
foreach (var themeName in windowSystem.ThemeRegistryService.GetAvailableThemeNames())
{
    mainWindow.AddControl(
        Controls.Button($"Switch to {themeName}")
            .OnClick((sender, e, window) =>
            {
                windowSystem.ThemeStateService.SwitchTheme(themeName);
                windowSystem.NotificationStateService.ShowNotification(
                    "Theme Changed",
                    $"Active theme: {themeName}",
                    NotificationSeverity.Success
                );
            })
            .Build()
    );
}

// Add keyboard shortcut
mainWindow.KeyPressed += (sender, e) =>
{
    if (e.KeyInfo.Key == ConsoleKey.F9)
    {
        windowSystem.ShowThemeSelectorDialog();
        e.Handled = true;
    }
};

windowSystem.AddWindow(mainWindow);
windowSystem.Run();
```

---

[Back to Main Documentation](../README.md)

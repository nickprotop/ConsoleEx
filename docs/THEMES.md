# Theme System

SharpConsoleUI includes a powerful theme system that allows runtime theme switching and custom theme creation.

## Table of Contents

- [Built-in Themes](#built-in-themes)
- [Using Themes](#using-themes)
- [Creating Custom Themes](#creating-custom-themes)
- [Theme Registry](#theme-registry)
- [Runtime Theme Switching](#runtime-theme-switching)

## Built-in Themes

SharpConsoleUI includes three built-in themes:

### Classic Theme

Traditional navy blue windows with classic styling.

```csharp
windowSystem.ThemeRegistry.SetTheme("Classic");
```

**Color Scheme:**
- Window Background: Navy (DarkBlue)
- Window Foreground: White
- Active Border: Yellow
- Inactive Border: Gray
- Active Title: Yellow
- Inactive Title: Gray
- Desktop Background: Blue
- Desktop Foreground: White

### ModernGray Theme (Default)

Modern dark theme with gray color scheme.

```csharp
windowSystem.ThemeRegistry.SetTheme("ModernGray");
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

### DevDark Theme

Dark developer theme with green terminal-inspired accents. Requires the DeveloperTools plugin.

```csharp
// Load plugin first
windowSystem.LoadPlugin<DeveloperToolsPlugin>();

// Then switch to DevDark theme
windowSystem.ThemeRegistry.SetTheme("DevDark");
```

**Color Scheme:**
- Window Background: Black
- Window Foreground: Green
- Active Border: Lime
- Inactive Border: DarkGreen
- Active Title: Yellow
- Inactive Title: DarkOliveGreen1
- Desktop Background: Grey3
- Desktop Foreground: Green

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
    themeName: "Classic"
);

// Option 3: Provide theme instance
var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    theme: new ModernGrayTheme()
);
```

### Changing Theme at Runtime

```csharp
// Using ThemeRegistry
windowSystem.ThemeRegistry.SetTheme("Classic");

// Direct assignment
windowSystem.Theme = new ModernGrayTheme();

// Using built-in theme selector dialog
windowSystem.ShowThemeSelectorDialog();
```

## Creating Custom Themes

Implement the `ITheme` interface to create custom themes:

```csharp
using SharpConsoleUI.Themes;
using Spectre.Console;

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
| `DesktopBackroundChar` | Character used to fill desktop background |

### Registering Custom Themes

```csharp
// Register a custom theme
windowSystem.ThemeRegistry.RegisterTheme("MyCustomTheme", new MyCustomTheme());

// Now you can switch to it
windowSystem.ThemeRegistry.SetTheme("MyCustomTheme");
```

### Complete Custom Theme Example

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Themes;
using SharpConsoleUI.Drivers;
using Spectre.Console;

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
windowSystem.ThemeRegistry.RegisterTheme("SolarizedDark", new SolarizedDarkTheme());
windowSystem.ThemeRegistry.SetTheme("SolarizedDark");
```

## Theme Registry

The `ThemeRegistry` manages theme registration and switching.

### Available Methods

```csharp
// Register a new theme
windowSystem.ThemeRegistry.RegisterTheme("MyTheme", new MyCustomTheme());

// Set active theme by name
windowSystem.ThemeRegistry.SetTheme("MyTheme");

// Get a theme by name
ITheme? theme = windowSystem.ThemeRegistry.GetTheme("Classic");

// Get all registered theme names
IEnumerable<string> themes = windowSystem.ThemeRegistry.GetRegisteredThemes();

// Check if theme exists
bool exists = windowSystem.ThemeRegistry.HasTheme("MyTheme");

// Get default theme
ITheme defaultTheme = ThemeRegistry.GetDefaultTheme();

// Get theme or fallback to default
ITheme theme = ThemeRegistry.GetThemeOrDefault("NonExistent", new ClassicTheme());
```

### List All Available Themes

```csharp
var themes = windowSystem.ThemeRegistry.GetRegisteredThemes();
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
    Controls.Button("Switch to Classic")
        .OnClick((sender, e, window) =>
        {
            windowSystem.ThemeRegistry.SetTheme("Classic");
        })
        .Build()
);

// Cycle through themes
var themes = windowSystem.ThemeRegistry.GetRegisteredThemes().ToList();
int currentIndex = 0;

mainWindow.AddControl(
    Controls.Button("Next Theme")
        .OnClick((sender, e, window) =>
        {
            currentIndex = (currentIndex + 1) % themes.Count;
            windowSystem.ThemeRegistry.SetTheme(themes[currentIndex]);

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

### Spectre.Console Color Support

SharpConsoleUI uses Spectre.Console colors. Available color types:

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
windowSystem.ThemeRegistry.RegisterTheme("SolarizedDark", new SolarizedDarkTheme());
windowSystem.ThemeRegistry.RegisterTheme("Dracula", new DraculaTheme());

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
foreach (var themeName in windowSystem.ThemeRegistry.GetRegisteredThemes())
{
    mainWindow.AddControl(
        Controls.Button($"Switch to {themeName}")
            .OnClick((sender, e, window) =>
            {
                windowSystem.ThemeRegistry.SetTheme(themeName);
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

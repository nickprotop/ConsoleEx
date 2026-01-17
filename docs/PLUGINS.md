# Plugin Development Guide

SharpConsoleUI provides an extensible plugin architecture that allows you to add custom themes, controls, windows, and services without modifying the core library.

## Table of Contents

- [Plugin Architecture](#plugin-architecture)
- [Creating a Plugin](#creating-a-plugin)
- [Plugin Capabilities](#plugin-capabilities)
- [Loading Plugins](#loading-plugins)
- [Using Plugin Content](#using-plugin-content)
- [DeveloperTools Plugin](#developertools-plugin)
- [Best Practices](#best-practices)

## Plugin Architecture

Plugins in SharpConsoleUI can provide:

1. **Themes** - Custom color schemes and visual styles
2. **Controls** - Reusable UI components
3. **Windows** - Pre-configured window templates and dialogs
4. **Services** - Application-level services and functionality

All plugins implement the `IPlugin` interface or inherit from the `PluginBase` abstract class.

### Plugin Lifecycle

```
1. Create plugin instance
2. windowSystem.LoadPlugin<MyPlugin>()
3. Plugin.Initialize() called
4. Plugin.GetThemes() called - themes registered
5. Plugin.GetControls() called - control factories registered
6. Plugin.GetWindows() called - window factories registered
7. Plugin.GetServices() called - services registered
8. Plugin ready for use
9. windowSystem.Dispose() - Plugin.Dispose() called
```

## Creating a Plugin

### Option 1: Inherit from PluginBase (Recommended)

```csharp
using SharpConsoleUI.Plugins;

public class MyPlugin : PluginBase
{
    public override PluginInfo Info => new(
        Name: "MyPlugin",
        Version: "1.0.0",
        Author: "Your Name",
        Description: "Description of what your plugin provides"
    );

    public override void Initialize(ConsoleWindowSystem windowSystem)
    {
        // Optional: Initialize plugin with access to window system
        // Useful for creating services that need window system reference
    }

    public override IReadOnlyList<PluginTheme> GetThemes()
    {
        // Return themes your plugin provides
        return Array.Empty<PluginTheme>();
    }

    public override IReadOnlyList<PluginControl> GetControls()
    {
        // Return control factories
        return Array.Empty<PluginControl>();
    }

    public override IReadOnlyList<PluginWindow> GetWindows()
    {
        // Return window factories
        return Array.Empty<PluginWindow>();
    }

    public override IReadOnlyList<PluginService> GetServices()
    {
        // Return services
        return Array.Empty<PluginService>();
    }

    public override void Dispose()
    {
        // Clean up resources
    }
}
```

### Option 2: Implement IPlugin Interface

```csharp
using SharpConsoleUI.Plugins;

public class MyPlugin : IPlugin
{
    public PluginInfo Info => new("MyPlugin", "1.0.0", "Your Name", "Description");

    public void Initialize(ConsoleWindowSystem windowSystem) { }
    public IReadOnlyList<PluginTheme> GetThemes() => Array.Empty<PluginTheme>();
    public IReadOnlyList<PluginControl> GetControls() => Array.Empty<PluginControl>();
    public IReadOnlyList<PluginWindow> GetWindows() => Array.Empty<PluginWindow>();
    public IReadOnlyList<PluginService> GetServices() => Array.Empty<PluginService>();
    public void Dispose() { }
}
```

## Plugin Capabilities

### 1. Providing Themes

Create custom themes for your plugin:

```csharp
using SharpConsoleUI.Themes;
using Spectre.Console;

public class MyTheme : ITheme
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

public class MyPlugin : PluginBase
{
    public override PluginInfo Info => new("MyPlugin", "1.0.0", "Me", "Custom theme plugin");

    public override IReadOnlyList<PluginTheme> GetThemes() => new[]
    {
        new PluginTheme(
            Name: "MyAwesomeTheme",
            Description: "A beautiful custom theme",
            Theme: new MyTheme()
        )
    };
}
```

### 2. Providing Controls

Create reusable control factories:

```csharp
using SharpConsoleUI.Controls;

public class MyCustomControl : IWindowControl
{
    public IContainer? Container { get; set; }
    public string? Name { get; set; }
    public object? Tag { get; set; }
    public bool Visible { get; set; } = true;

    // Implement IWindowControl interface...
    public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect) { }
    public Size MeasureDOM(int availableWidth) => new Size(20, 5);
    public void Invalidate(bool recursive = false) { }
    public void Dispose() { }
}

public class MyPlugin : PluginBase
{
    public override PluginInfo Info => new("MyPlugin", "1.0.0", "Me", "Custom controls");

    public override IReadOnlyList<PluginControl> GetControls() => new[]
    {
        new PluginControl(
            Name: "MyCustomControl",
            Factory: () => new MyCustomControl()
        )
    };
}
```

### 3. Providing Windows

Create window templates and dialogs:

```csharp
using SharpConsoleUI.Builders;

public class MyPlugin : PluginBase
{
    public override PluginInfo Info => new("MyPlugin", "1.0.0", "Me", "Custom windows");

    public override IReadOnlyList<PluginWindow> GetWindows() => new[]
    {
        new PluginWindow(
            Name: "AboutDialog",
            Factory: (windowSystem) =>
            {
                var window = new WindowBuilder(windowSystem)
                    .WithTitle("About")
                    .WithSize(50, 15)
                    .Centered()
                    .AsModal()
                    .Build();

                window.AddControl(new MarkupControl(new List<string>
                {
                    "[bold yellow]My Application v1.0[/]",
                    "",
                    "Created by Your Name",
                    "",
                    "[dim]Press ESC to close[/]"
                }));

                window.KeyPressed += (s, e) =>
                {
                    if (e.KeyInfo.Key == ConsoleKey.Escape)
                    {
                        windowSystem.CloseWindow(window);
                        e.Handled = true;
                    }
                };

                return window;
            }
        )
    };
}
```

### 4. Providing Services

Create application-level services:

```csharp
public interface IMyService
{
    string GetData();
    void ProcessData(string data);
}

public class MyService : IMyService
{
    private readonly ConsoleWindowSystem _windowSystem;

    public MyService(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem;
    }

    public string GetData() => "Sample data";

    public void ProcessData(string data)
    {
        _windowSystem.NotificationStateService.ShowNotification(
            "Data Processed",
            $"Processed: {data}",
            NotificationSeverity.Success
        );
    }
}

public class MyPlugin : PluginBase
{
    private MyService? _myService;

    public override PluginInfo Info => new("MyPlugin", "1.0.0", "Me", "Custom services");

    public override void Initialize(ConsoleWindowSystem windowSystem)
    {
        _myService = new MyService(windowSystem);
    }

    public override IReadOnlyList<PluginService> GetServices()
    {
        if (_myService == null)
            return Array.Empty<PluginService>();

        return new[]
        {
            new PluginService(typeof(IMyService), _myService)
        };
    }

    public override void Dispose()
    {
        _myService = null;
    }
}
```

## Loading Plugins

### Load at Startup

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Plugins.DeveloperTools;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Load plugin
windowSystem.LoadPlugin<DeveloperToolsPlugin>();
windowSystem.LoadPlugin<MyPlugin>();

// Plugin content is now available
```

### Load at Runtime

```csharp
// Load plugin dynamically
windowSystem.LoadPlugin<MyPlugin>();

// Plugin themes, controls, windows, and services are immediately available
```

## Using Plugin Content

### Using Plugin Themes

```csharp
// After loading plugin, theme is registered
windowSystem.LoadPlugin<MyPlugin>();

// Switch to plugin theme
windowSystem.ThemeRegistry.SetTheme("MyAwesomeTheme");

// Or use theme selector dialog
windowSystem.ShowThemeSelectorDialog();
```

### Using Plugin Controls

```csharp
// After loading plugin
windowSystem.LoadPlugin<MyPlugin>();

// Create control using factory
var control = windowSystem.CreatePluginControl("MyCustomControl");

// Add to window
window.AddControl(control);
```

### Using Plugin Windows

```csharp
// After loading plugin
windowSystem.LoadPlugin<MyPlugin>();

// Create window using factory
var aboutWindow = windowSystem.CreatePluginWindow("AboutDialog");

// Show window
windowSystem.AddWindow(aboutWindow);
```

### Using Plugin Services

```csharp
// After loading plugin
windowSystem.LoadPlugin<MyPlugin>();

// Get service instance
var myService = windowSystem.GetService<IMyService>();

// Use service
if (myService != null)
{
    string data = myService.GetData();
    myService.ProcessData(data);
}
```

## DeveloperTools Plugin

SharpConsoleUI includes a built-in DeveloperTools plugin that provides development and debugging tools.

### Loading DeveloperTools

```csharp
using SharpConsoleUI.Plugins.DeveloperTools;

windowSystem.LoadPlugin<DeveloperToolsPlugin>();
```

### DeveloperTools Content

**Themes:**
- **DevDark** - Dark developer theme with green terminal-inspired accents

**Controls:**
- **LogExporter** - Export and filter application logs

**Windows:**
- **DebugConsole** - Interactive debug console for runtime inspection

**Services:**
- **IDiagnosticsService** - System diagnostics and performance metrics

### Using DeveloperTools

```csharp
// Load plugin
windowSystem.LoadPlugin<DeveloperToolsPlugin>();

// Switch to DevDark theme
windowSystem.ThemeRegistry.SetTheme("DevDark");

// Create debug console window
var debugWindow = windowSystem.CreatePluginWindow("DebugConsole");
windowSystem.AddWindow(debugWindow);

// Get diagnostics service
var diagnostics = windowSystem.GetService<IDiagnosticsService>();
var report = diagnostics?.GetDiagnosticsReport();

// Add log exporter control to a window
var logExporter = windowSystem.CreatePluginControl("LogExporter");
window.AddControl(logExporter);
```

## Complete Plugin Example

Here's a complete example of a plugin that provides everything:

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Plugins;
using SharpConsoleUI.Themes;
using Spectre.Console;

// Custom theme
public class CorporateTheme : ITheme
{
    public Color WindowBackgroundColor => new Color(0, 51, 102);  // Corporate blue
    public Color WindowForegroundColor => Color.White;
    public Color ActiveBorderForegroundColor => new Color(0, 153, 204);  // Light blue
    public Color InactiveBorderForegroundColor => Color.Grey50;
    public Color ActiveTitleForegroundColor => Color.Yellow;
    public Color InactiveTitleForegroundColor => Color.Grey70;
    public Color DesktopBackgroundColor => new Color(0, 26, 51);  // Dark blue
    public Color DesktopForegroundColor => Color.Grey30;
    public char DesktopBackroundChar => '·';
}

// Custom control
public class StatusIndicatorControl : IWindowControl
{
    public IContainer? Container { get; set; }
    public string? Name { get; set; }
    public object? Tag { get; set; }
    public bool Visible { get; set; } = true;
    public string Status { get; set; } = "Ready";
    public Color StatusColor { get; set; } = Color.Green;

    public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect)
    {
        buffer.SetText(bounds.X, bounds.Y, $"[{StatusColor}]■[/] {Status}", Color.White, Color.Black);
    }

    public Size MeasureDOM(int availableWidth) => new Size(20, 1);
    public void Invalidate(bool recursive = false) => Container?.Invalidate(recursive);
    public void Dispose() { }
}

// Custom service
public interface IStatusService
{
    void SetStatus(string status, Color color);
    string GetStatus();
}

public class StatusService : IStatusService
{
    private string _status = "Ready";
    private Color _color = Color.Green;

    public void SetStatus(string status, Color color)
    {
        _status = status;
        _color = color;
    }

    public string GetStatus() => _status;
}

// Plugin class
public class CorporatePlugin : PluginBase
{
    private StatusService? _statusService;
    private ConsoleWindowSystem? _windowSystem;

    public override PluginInfo Info => new(
        "CorporatePlugin",
        "1.0.0",
        "Your Company",
        "Corporate branding, custom controls, and status management"
    );

    public override void Initialize(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem;
        _statusService = new StatusService();
    }

    public override IReadOnlyList<PluginTheme> GetThemes() => new[]
    {
        new PluginTheme("Corporate", "Professional corporate theme", new CorporateTheme())
    };

    public override IReadOnlyList<PluginControl> GetControls() => new[]
    {
        new PluginControl("StatusIndicator", () => new StatusIndicatorControl())
    };

    public override IReadOnlyList<PluginWindow> GetWindows() => new[]
    {
        new PluginWindow("AboutCompany", ws =>
        {
            var window = new WindowBuilder(ws)
                .WithTitle("About Our Company")
                .WithSize(60, 15)
                .Centered()
                .AsModal()
                .Build();

            window.AddControl(new MarkupControl(new List<string>
            {
                "[bold yellow]Your Company Name[/]",
                "[dim]Version 1.0.0[/]",
                "",
                "We build amazing console applications!",
                "",
                "[grey]Press ESC to close[/]"
            }));

            window.KeyPressed += (s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow(window);
                    e.Handled = true;
                }
            };

            return window;
        })
    };

    public override IReadOnlyList<PluginService> GetServices()
    {
        if (_statusService == null)
            return Array.Empty<PluginService>();

        return new[]
        {
            new PluginService(typeof(IStatusService), _statusService)
        };
    }

    public override void Dispose()
    {
        _statusService = null;
        _windowSystem = null;
    }
}

// Usage
class Program
{
    static int Main(string[] args)
    {
        var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

        // Load plugin
        windowSystem.LoadPlugin<CorporatePlugin>();

        // Use plugin theme
        windowSystem.ThemeRegistry.SetTheme("Corporate");

        // Create window
        var mainWindow = new WindowBuilder(windowSystem)
            .WithTitle("Corporate Application")
            .WithSize(80, 25)
            .Centered()
            .Build();

        // Use plugin control
        var statusIndicator = windowSystem.CreatePluginControl("StatusIndicator");
        mainWindow.AddControl(statusIndicator);

        // Use plugin service
        var statusService = windowSystem.GetService<IStatusService>();
        statusService?.SetStatus("Application Started", Color.Green);

        // Button to show plugin window
        mainWindow.AddControl(
            Controls.Button("About")
                .OnClick((sender, e, window) =>
                {
                    var aboutWindow = windowSystem.CreatePluginWindow("AboutCompany");
                    windowSystem.AddWindow(aboutWindow);
                })
                .Build()
        );

        windowSystem.AddWindow(mainWindow);
        return windowSystem.Run();
    }
}
```

## Best Practices

1. **Inherit from PluginBase**: Unless you need custom behavior, use `PluginBase` for cleaner code
2. **Use Initialize()**: Perform initialization that requires the window system in `Initialize()`
3. **Dispose properly**: Clean up resources in `Dispose()` method
4. **Name uniquely**: Use unique names for themes, controls, windows to avoid conflicts
5. **Version your plugin**: Use semantic versioning in `PluginInfo`
6. **Document well**: Provide clear descriptions in `PluginInfo` and theme/control records
7. **Test integration**: Test your plugin with the core library before distribution
8. **Handle errors**: Check for null and handle exceptions gracefully
9. **Don't modify core**: Plugins should extend, not modify the core library
10. **Keep it simple**: Start small and add features incrementally

---

[Back to Main Documentation](../README.md)

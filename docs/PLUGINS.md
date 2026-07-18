# Plugin Development Guide

SharpConsoleUI provides an extensible plugin architecture that allows you to add custom themes, controls, windows, and services without modifying the core library.

## Table of Contents

- [Plugin Architecture](#plugin-architecture)
- [Creating a Plugin](#creating-a-plugin)
- [Plugin Capabilities](#plugin-capabilities)
- [Loading Plugins](#loading-plugins)
- [PluginStateService](#pluginstateservice)
- [Using Plugin Content](#using-plugin-content)
- [Example: authoring a plugin (PluginShowcase)](#example-authoring-a-plugin-pluginshowcase)
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
2. windowSystem.PluginStateService.LoadPlugin<MyPlugin>()
3. Plugin.Initialize() called
4. Plugin.GetThemes() called - themes registered
5. Plugin.GetControls() called - control factories registered
6. Plugin.GetWindows() called - window factories registered
7. Plugin.GetServicePlugins() called - services registered
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

    public override IReadOnlyList<IPluginService> GetServicePlugins()
    {
        // Return services (agnostic IPluginService pattern)
        return Array.Empty<IPluginService>();
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
    public IReadOnlyList<IPluginService> GetServicePlugins() => Array.Empty<IPluginService>();
    public void Dispose() { }
}
```

## Plugin Capabilities

### 1. Providing Themes

Create custom themes for your plugin:

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Themes;

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

// Derive from BaseControl — it supplies Container/Name/Tag/Visible/Margin/Invalidate/Dispose
// and leaves you the four layout members to override.
public class MyCustomControl : BaseControl
{
    public override int? ContentWidth => 20;

    public override Size GetLogicalContentSize() => new Size(20, 5);

    public override LayoutSize MeasureDOM(LayoutConstraints constraints) =>
        new LayoutSize(
            Math.Clamp(20, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(5, constraints.MinHeight, constraints.MaxHeight));

    public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
                                  Color defaultForeground, Color defaultBackground)
    {
        SetActualBounds(bounds);
    }
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

### 4. Providing Services (Agnostic Pattern)

Create application-level services using the **agnostic IPluginService pattern** - no shared interfaces required:

```csharp
using SharpConsoleUI.Plugins;

public class MyDataService : IPluginService
{
    private readonly ConsoleWindowSystem _windowSystem;

    public string ServiceName => "MyData";
    public string Description => "Provides data processing operations";

    public MyDataService(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem;
    }

    public IReadOnlyList<ServiceOperation> GetAvailableOperations()
    {
        return new[]
        {
            new ServiceOperation(
                Name: "GetData",
                Description: "Retrieves sample data",
                ReturnType: typeof(string),
                Parameters: Array.Empty<ServiceOperationParameter>()
            ),
            new ServiceOperation(
                Name: "ProcessData",
                Description: "Processes the provided data",
                ReturnType: null, // void
                Parameters: new[]
                {
                    new ServiceOperationParameter(
                        Name: "data",
                        Type: typeof(string),
                        Description: "The data to process",
                        Required: true
                    )
                }
            )
        };
    }

    public object? Execute(string operationName, Dictionary<string, object>? parameters = null)
    {
        switch (operationName)
        {
            case "GetData":
                return "Sample data from service";

            case "ProcessData":
                var data = parameters?["data"] as string ?? "";
                _windowSystem.NotificationStateService.ShowNotification(
                    "Data Processed",
                    $"Processed: {data}",
                    NotificationSeverity.Success
                );
                return null;

            default:
                throw new InvalidOperationException($"Unknown operation: {operationName}");
        }
    }
}

public class MyPlugin : PluginBase
{
    private MyDataService? _dataService;

    public override PluginInfo Info => new("MyPlugin", "1.0.0", "Me", "Custom services");

    public override void Initialize(ConsoleWindowSystem windowSystem)
    {
        _dataService = new MyDataService(windowSystem);
    }

    public override IReadOnlyList<IPluginService> GetServicePlugins()
    {
        if (_dataService == null)
            return Array.Empty<IPluginService>();

        return new[] { _dataService };
    }

    public override void Dispose()
    {
        _dataService = null;
    }
}
```

## Loading Plugins

### Load at Startup

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Drivers;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Load your plugin (defined in your own project)
windowSystem.PluginStateService.LoadPlugin<MyPlugin>();

// Plugin content is now available
```

### Load at Runtime

```csharp
// Load plugin dynamically
windowSystem.PluginStateService.LoadPlugin<MyPlugin>();

// Plugin themes, controls, windows, and services are immediately available
```

### Auto-loading with Configuration

```csharp
using SharpConsoleUI.Configuration;

// Configure auto-loading from plugins directory
var pluginConfig = new PluginConfiguration(
    AutoLoad: true,
    PluginsDirectory: "./plugins"
);

// Plugins are loaded automatically on startup
var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    pluginConfiguration: pluginConfig
);

// All plugins from ./plugins directory are now loaded
```

## PluginStateService

The `PluginStateService` manages all plugin-related functionality, including plugin loading, service registration, and factory management. This service follows the established state service pattern used throughout SharpConsoleUI.

### Accessing PluginStateService

```csharp
// Access through ConsoleWindowSystem
var pluginService = windowSystem.PluginStateService;

// Get current plugin system state
var state = pluginService.CurrentState;
Console.WriteLine($"Loaded plugins: {state.LoadedPluginCount}");
Console.WriteLine($"Registered services: {state.RegisteredServiceCount}");
Console.WriteLine($"Registered controls: {state.RegisteredControlCount}");
Console.WriteLine($"Registered windows: {state.RegisteredWindowCount}");
```

### Plugin State Management

The `PluginState` record provides an immutable snapshot of the plugin system:

```csharp
public record PluginState(
    int LoadedPluginCount,
    int RegisteredServiceCount,
    int RegisteredControlCount,
    int RegisteredWindowCount,
    IReadOnlyList<string> PluginNames,
    bool AutoLoadEnabled,
    string? PluginsDirectory
);
```

### Plugin Query Methods

```csharp
// Get all loaded plugins
IReadOnlyList<IPlugin> plugins = windowSystem.PluginStateService.LoadedPlugins;

// Get a specific plugin by name
IPlugin? myPlugin = windowSystem.PluginStateService.GetPlugin("MyPlugin");

// Check if a plugin is loaded
bool isLoaded = windowSystem.PluginStateService.IsPluginLoaded("ShowcasePlugin");

// Get registered service/control/window names
var serviceNames = windowSystem.PluginStateService.RegisteredServiceNames;
var controlNames = windowSystem.PluginStateService.RegisteredControlNames;
var windowNames = windowSystem.PluginStateService.RegisteredWindowNames;
```

### Plugin Events

Subscribe to plugin system events for real-time notifications:

```csharp
// Subscribe to plugin loaded event
windowSystem.PluginStateService.PluginLoaded += (sender, e) =>
{
    Console.WriteLine($"Plugin loaded: {e.Info.Name} v{e.Info.Version}");
    Console.WriteLine($"Author: {e.Info.Author}");
    Console.WriteLine($"Description: {e.Info.Description}");
};

// Subscribe to state changes
windowSystem.PluginStateService.StateChanged += (sender, e) =>
{
    Console.WriteLine($"Plugin count changed: {e.PreviousState.LoadedPluginCount} → {e.NewState.LoadedPluginCount}");
};

// Subscribe to service registration
windowSystem.PluginStateService.ServiceRegistered += (sender, e) =>
{
    Console.WriteLine($"Service registered: {e.ServiceName}");
};
```

### Thread Safety

The `PluginStateService` is thread-safe and uses internal locking for all operations:

```csharp
// Safe to call from multiple threads
Task.Run(() => windowSystem.PluginStateService.LoadPlugin<Plugin1>());
Task.Run(() => windowSystem.PluginStateService.LoadPlugin<Plugin2>());

// All state queries are also thread-safe
var state = windowSystem.PluginStateService.CurrentState; // Safe
```

### Configuration Management

```csharp
// Get current configuration
var config = windowSystem.PluginStateService.Configuration;

// Update configuration at runtime
var newConfig = new PluginConfiguration(
    AutoLoad: false,
    PluginsDirectory: "./custom-plugins"
);
windowSystem.PluginStateService.UpdateConfiguration(newConfig);
```

## Using Plugin Content

### Using Plugin Themes

```csharp
// After loading plugin, theme is registered
windowSystem.PluginStateService.LoadPlugin<MyPlugin>();

// Switch to plugin theme
windowSystem.ThemeStateService.SwitchTheme("MyAwesomeTheme");

// Or use theme selector dialog
windowSystem.ShowThemeSelectorDialog();
```

### Using Plugin Controls

```csharp
// After loading plugin
windowSystem.PluginStateService.LoadPlugin<MyPlugin>();

// Create control using factory
var control = windowSystem.PluginStateService.CreateControl("MyCustomControl");

// Add to window
window.AddControl(control);
```

### Using Plugin Windows

```csharp
// After loading plugin
windowSystem.PluginStateService.LoadPlugin<MyPlugin>();

// Create window using factory
var aboutWindow = windowSystem.PluginStateService.CreateWindow("AboutDialog");

// Show window
windowSystem.AddWindow(aboutWindow);
```

### Using Plugin Services (Agnostic Pattern)

```csharp
// After loading plugin
windowSystem.PluginStateService.LoadPlugin<MyPlugin>();

// Get service by name (agnostic - no type knowledge required!)
var myService = windowSystem.PluginStateService.GetService("MyData");

// Use service with reflection-free Execute method
if (myService != null)
{
    // Call operation without parameters
    string data = (string)myService.Execute("GetData")!;

    // Call operation with parameters
    myService.Execute("ProcessData", new Dictionary<string, object>
    {
        ["data"] = data
    });
}
```

## Example: authoring a plugin (PluginShowcase)

The `Examples/PluginShowcaseExample` project authors a complete plugin
(`ShowcasePlugin`) in its own code and loads it — the recommended way to see the
plugin system end-to-end. It is an *example*, not a built-in library plugin: you
write `ShowcasePlugin.cs` in your own project and reference it directly (or load
a compiled plugin assembly by path).

### Loading the example plugin

```csharp
using PluginShowcaseExample; // your own project's namespace

windowSystem.PluginStateService.LoadPlugin<ShowcasePlugin>();
```

### What ShowcasePlugin exposes

**Themes:**
- **DevDark** - Dark developer theme with green terminal-inspired accents

**Controls:**
- **LogExporter** - Export and filter application logs

**Windows:**
- **DebugConsole** - Interactive debug console for runtime inspection

**Services:**
- **Diagnostics** - System diagnostics and performance metrics (agnostic IPluginService)

### Using the plugin's contributions

```csharp
// Load your plugin
windowSystem.PluginStateService.LoadPlugin<ShowcasePlugin>();

// Switch to the DevDark theme the plugin provides
windowSystem.ThemeStateService.SwitchTheme("DevDark");

// Create the debug console window the plugin registered
var debugWindow = windowSystem.PluginStateService.CreateWindow("DebugConsole");
windowSystem.AddWindow(debugWindow);

// Get the diagnostics service (agnostic - no type knowledge required!)
var diagnostics = windowSystem.PluginStateService.GetService("Diagnostics");
if (diagnostics != null)
{
    // Call operations using reflection-free Execute method
    var report = (string)diagnostics.Execute("GetDiagnosticsReport")!;

    // Or with parameters
    var customReport = (string)diagnostics.Execute("GetDetailedReport", new Dictionary<string, object>
    {
        ["includeMemory"] = true,
        ["includeGC"] = true,
        ["includeUptime"] = false,
        ["includeWindows"] = true
    })!;
}

// Add the log exporter control the plugin registered to a window
var logExporter = windowSystem.PluginStateService.CreateControl("LogExporter");
window.AddControl(logExporter);
```

> See `Examples/PluginShowcaseExample/ShowcasePlugin.cs` for the full, self-contained
> implementation using only the public plugin SDK (`PluginBase`, `PluginServiceBase`,
> `PluginTheme`/`PluginControl`/`PluginWindow`, `ServiceOperation`).

## Complete Plugin Example

Here's a complete example of a plugin that provides everything:

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI;
using SharpConsoleUI.Plugins;
using SharpConsoleUI.Themes;

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
public class StatusIndicatorControl : BaseControl
{
    private string _status = "Ready";
    private Color _statusColor = Color.Green;

    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public Color StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }

    private string Markup => $"[{_statusColor}]■[/] {_status}";

    public override int? ContentWidth => MarkupParser.StripLength(Markup);

    public override Size GetLogicalContentSize() =>
        new Size(MarkupParser.StripLength(Markup) + Margin.Left + Margin.Right,
                 1 + Margin.Top + Margin.Bottom);

    public override LayoutSize MeasureDOM(LayoutConstraints constraints)
    {
        var size = GetLogicalContentSize();
        return new LayoutSize(
            Math.Clamp(size.Width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(size.Height, constraints.MinHeight, constraints.MaxHeight));
    }

    public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
                                  Color defaultForeground, Color defaultBackground)
    {
        SetActualBounds(bounds);

        // Parse markup to cells — never write raw "[red]…[/]" text into the buffer.
        var cells = MarkupParser.Parse(Markup, defaultForeground, defaultBackground);
        for (int i = 0; i < cells.Count; i++)
        {
            int x = bounds.X + Margin.Left + i;
            if (x < clipRect.X || x >= clipRect.Right) continue;
            buffer.SetCell(x, bounds.Y + Margin.Top, cells[i]);
        }
    }
}

// Custom service (agnostic IPluginService pattern)
public class StatusService : IPluginService
{
    public string ServiceName => "Status";
    public string Description => "Application status management";

    private string _status = "Ready";
    private Color _color = Color.Green;

    public IReadOnlyList<ServiceOperation> GetAvailableOperations()
    {
        return new[]
        {
            new ServiceOperation(
                Name: "SetStatus",
                Description: "Sets the application status",
                ReturnType: null,
                Parameters: new[]
                {
                    new ServiceOperationParameter("status", typeof(string), "Status message", required: true),
                    new ServiceOperationParameter("color", typeof(Color), "Status color", required: true)
                }
            ),
            new ServiceOperation(
                Name: "GetStatus",
                Description: "Gets the current status",
                ReturnType: typeof(string),
                Parameters: Array.Empty<ServiceOperationParameter>()
            )
        };
    }

    public object? Execute(string operationName, Dictionary<string, object>? parameters = null)
    {
        switch (operationName)
        {
            case "SetStatus":
                _status = parameters?["status"] as string ?? "Ready";
                _color = (Color)(parameters?["color"] ?? Color.Green);
                return null;

            case "GetStatus":
                return _status;

            default:
                throw new InvalidOperationException($"Unknown operation: {operationName}");
        }
    }
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

    public override IReadOnlyList<IPluginService> GetServicePlugins()
    {
        if (_statusService == null)
            return Array.Empty<IPluginService>();

        return new[] { _statusService };
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
        windowSystem.PluginStateService.LoadPlugin<CorporatePlugin>();

        // Use plugin theme
        windowSystem.ThemeStateService.SwitchTheme("Corporate");

        // Create window
        var mainWindow = new WindowBuilder(windowSystem)
            .WithTitle("Corporate Application")
            .WithSize(80, 25)
            .Centered()
            .Build();

        // Use plugin control
        var statusIndicator = windowSystem.PluginStateService.CreateControl("StatusIndicator");
        mainWindow.AddControl(statusIndicator);

        // Use plugin service (agnostic - no type knowledge required!)
        var statusService = windowSystem.PluginStateService.GetService("Status");
        statusService?.Execute("SetStatus", new Dictionary<string, object>
        {
            ["status"] = "Application Started",
            ["color"] = Color.Green
        });

        // Button to show plugin window
        mainWindow.AddControl(
            Controls.Button("About")
                .OnClick((sender, e, window) =>
                {
                    var aboutWindow = windowSystem.PluginStateService.CreateWindow("AboutCompany");
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

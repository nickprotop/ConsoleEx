# Registry — Persistent Key-Value Storage

The registry is a hierarchical persistent key-value store built into SharpConsoleUI. It survives application restarts and is designed for storing user preferences, window state, and application settings. The backing store is a JSON file by default, with a pluggable storage interface for custom backends.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
  - [RegistryConfiguration](#registryconfiguration)
  - [Flush Modes](#flush-modes)
- [Reading and Writing Values](#reading-and-writing-values)
  - [Primitive Types](#primitive-types)
  - [Generic Types (AOT-safe)](#generic-types-aot-safe)
- [Sections and Paths](#sections-and-paths)
- [Key Management](#key-management)
- [Thread Safety](#thread-safety)
- [Custom Storage Backends](#custom-storage-backends)
- [RegistryStateService](#registrystateservice)
- [Complete Example](#complete-example)

---

## Overview

The registry organizes data into a tree of **sections**, each containing key-value pairs. Sections map to nested JSON objects on disk. Paths use `/` as the separator:

```
app/
  ui/
    theme = "ModernGray"
    windowWidth = 120
  preferences/
    autoSave = true
    lastFile = "/home/user/document.txt"
```

The registry is backed by `AppRegistry`, which is exposed through `ConsoleWindowSystem.RegistryStateService` after being initialized with a `RegistryConfiguration`. It loads on startup and saves on shutdown automatically.

When no file path is specified, `RegistryConfiguration.Default` resolves to a platform-appropriate location derived from the process name:

| Platform | Path |
|----------|------|
| Windows | `%APPDATA%\<processname>\registry.json` |
| Linux/macOS | `~/.config/<processname>/registry.json` |

---

## Quick Start

Pass a `RegistryConfiguration` when constructing `ConsoleWindowSystem`:

```csharp
var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    registryConfiguration: RegistryConfiguration.ForFile("myapp.json")
);

// Access the registry through RegistryStateService
var registry = windowSystem.RegistryStateService;

// Open a section (created automatically if it doesn't exist)
var prefs = registry.OpenSection("app/preferences");

// Read a value (returns default if key is absent)
string theme = prefs.GetString("theme", "ModernGray");
bool autoSave = prefs.GetBool("autoSave", true);

// Write a value
prefs.SetString("theme", "Solarized");
prefs.SetBool("autoSave", false);

// Registry saves automatically on windowSystem.Dispose() / end of Run()
```

---

## Configuration

### RegistryConfiguration

```csharp
public record RegistryConfiguration(
    string FilePath = "registry.json",
    bool EagerFlush = false,
    TimeSpan? FlushInterval = null,
    IRegistryStorage? Storage = null
)
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `FilePath` | `string` | `"registry.json"` | Path to the JSON file. Relative paths resolve from the working directory. |
| `EagerFlush` | `bool` | `false` | Write to disk on every `Set*` call. |
| `FlushInterval` | `TimeSpan?` | `null` | Background timer flush interval. `null` disables timer flushing. |
| `Storage` | `IRegistryStorage?` | `null` | Custom storage backend. When `null`, uses `JsonFileStorage` with `FilePath`. |

#### Static factory helpers

```csharp
// Default — saves to a platform-appropriate path, manual flush only:
//   Windows:      %APPDATA%\<processname>\registry.json
//   Linux/macOS:  ~/.config/<processname>/registry.json
var config = RegistryConfiguration.Default;

// Save to a specific file path
var config = RegistryConfiguration.ForFile("data/settings.json");

// Eager flush — every Set writes immediately
var config = new RegistryConfiguration(EagerFlush: true);

// Timer-based flush every 30 seconds
var config = new RegistryConfiguration(FlushInterval: TimeSpan.FromSeconds(30));
```

### Flush Modes

The registry does not write to disk automatically on every change by default. Choose a flush strategy based on your needs:

| Mode | How to enable | When it writes |
|------|--------------|----------------|
| **Manual** (default) | `RegistryConfiguration.Default` | Only on explicit `Save()` or application shutdown |
| **Eager** | `EagerFlush: true` | After every `Set*` call |
| **Timer** | `FlushInterval: TimeSpan.FromSeconds(N)` | Every N seconds in the background |
| **Shutdown** | Always active | `RegistryStateService.Dispose()` — called by `ConsoleWindowSystem` on exit |

Modes can be combined: eager + timer is valid (though redundant). Shutdown-save always runs regardless of mode.

---

## Reading and Writing Values

All read/write operations are performed through a `RegistrySection` obtained via `OpenSection()`.

### Primitive Types

```csharp
var section = registry.OpenSection("app/ui");

// string
section.SetString("theme", "ModernGray");
string theme = section.GetString("theme", "ModernGray"); // default = ""

// int
section.SetInt("windowWidth", 120);
int width = section.GetInt("windowWidth", 80); // default = 0

// bool
section.SetBool("showToolbar", true);
bool show = section.GetBool("showToolbar", true); // default = false

// double
section.SetDouble("opacity", 0.95);
double opacity = section.GetDouble("opacity", 1.0); // default = 0.0

// DateTime (stored as ISO 8601)
section.SetDateTime("lastOpened", DateTime.UtcNow);
DateTime dt = section.GetDateTime("lastOpened", DateTime.MinValue);
```

All `Get*` methods return the `defaultValue` parameter if the key is absent or has an incompatible type — they never throw.

### Generic Types (AOT-safe)

For complex types, use the `JsonTypeInfo<T>` overloads. These are AOT-safe and work with source generation:

```csharp
// Define a source-generated context
[JsonSerializable(typeof(WindowPosition))]
public partial class AppJsonContext : JsonSerializerContext { }

// Usage
var pos = new WindowPosition { X = 10, Y = 20, Width = 80, Height = 24 };
section.Set("mainWindowPos", pos, AppJsonContext.Default.WindowPosition);

var loaded = section.Get("mainWindowPos", new WindowPosition(),
    AppJsonContext.Default.WindowPosition);
```

---

## Sections and Paths

Sections are hierarchical nodes in the registry tree. `OpenSection()` creates any missing intermediate nodes automatically.

```csharp
// Open a deeply nested section
var section = registry.OpenSection("app/windows/main/layout");

// "/" is the separator; leading and trailing slashes are trimmed
var same = registry.OpenSection("/app/windows/main/layout/");

// Empty path returns the root section
var root = registry.OpenSection("");

// Sections can be opened from another section (relative navigation)
var app = registry.OpenSection("app");
var ui = app.OpenSection("ui");       // equivalent to "app/ui"
var prefs = app.OpenSection("preferences"); // equivalent to "app/preferences"
```

**Path rules:**
- `/` is the path separator
- Leading and trailing slashes are ignored
- Empty segments (e.g. `"a//b"`) throw `ArgumentException`
- Empty path or `"/"` returns the current section (root if called on the registry)

---

## Key Management

```csharp
var section = registry.OpenSection("app/ui");

// Check if a key exists
bool hasTheme = section.HasKey("theme");

// Get all leaf value keys in this section (not sub-sections)
IReadOnlyList<string> keys = section.GetKeys();

// Get all direct child section names
IReadOnlyList<string> subSections = section.GetSubSectionNames();

// Delete a key
section.DeleteKey("theme");

// Delete a sub-section and all its contents
section.DeleteSection("oldSettings");
```

---

## Thread Safety

`AppRegistry.OpenSection()`, `Save()`, and `Load()` are thread-safe via `ReaderWriterLockSlim`.

**`RegistrySection` instances are NOT thread-safe.** Do not share a single `RegistrySection` across threads. Instead, call `OpenSection()` per thread to get an independent section view:

```csharp
// Safe — each thread opens its own section instance
Task.Run(() =>
{
    var section = registry.OpenSection("app/preferences");
    section.SetString("key", "value");
});

// Not safe — sharing one section instance across threads
var shared = registry.OpenSection("app/preferences");
Task.Run(() => shared.SetString("key", "value")); // race condition
```

---

## Custom Storage Backends

Implement `IRegistryStorage` to use any storage medium:

```csharp
public interface IRegistryStorage
{
    void Save(JsonNode root);
    JsonNode? Load();
}
```

Example — encrypted file storage:

```csharp
public class EncryptedFileStorage : IRegistryStorage
{
    private readonly string _path;
    private readonly byte[] _key;

    public EncryptedFileStorage(string path, byte[] key)
    {
        _path = path;
        _key = key;
    }

    public void Save(JsonNode root)
    {
        var json = root.ToJsonString();
        var encrypted = Encrypt(json, _key);
        File.WriteAllBytes(_path, encrypted);
    }

    public JsonNode? Load()
    {
        if (!File.Exists(_path)) return null;
        var encrypted = File.ReadAllBytes(_path);
        var json = Decrypt(encrypted, _key);
        return JsonNode.Parse(json);
    }

    // ... Encrypt / Decrypt implementation
}

// Use it
var config = new RegistryConfiguration(
    Storage: new EncryptedFileStorage("settings.enc", myKey)
);
```

The built-in `MemoryStorage` is available for testing — it stores data in memory only, with no file I/O:

```csharp
var config = new RegistryConfiguration(Storage: new MemoryStorage());
```

---

## RegistryStateService

`RegistryStateService` is the lifecycle wrapper that integrates `AppRegistry` with `ConsoleWindowSystem`. It:

- Calls `Load()` during system initialization
- Calls `Save()` on `Dispose()` (i.e., when `ConsoleWindowSystem` shuts down)
- Exposes `OpenSection()`, `Save()`, and `Load()` directly — no need to unwrap an inner object

Accessed via:

```csharp
RegistryStateService? registry = windowSystem.RegistryStateService;
```

`RegistryStateService` is `null` if no `RegistryConfiguration` was passed to `ConsoleWindowSystem`. Always null-check before use:

```csharp
var section = windowSystem.RegistryStateService?.OpenSection("app/ui");
section?.SetString("theme", "Solarized");
```

### Manual save

Call `Save()` explicitly at any point — for example, right after the user changes a setting:

```csharp
prefs.SetString("theme", selectedTheme);
windowSystem.RegistryStateService?.Save(); // flush to disk now
```

### Manual reload

```csharp
// Reload from disk — discards any unsaved in-memory changes
windowSystem.RegistryStateService?.Load();
```

---

## Complete Example

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;

// Initialize with registry support
var windowSystem = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    registryConfiguration: new RegistryConfiguration(
        FilePath: "myapp-settings.json",
        FlushInterval: TimeSpan.FromMinutes(1) // auto-flush every minute
    )
);

var registry = windowSystem.RegistryStateService!;

// Restore window position from last run
var windowPrefs = registry.OpenSection("app/windows/main");
int lastX = windowPrefs.GetInt("x", 5);
int lastY = windowPrefs.GetInt("y", 3);
int lastWidth = windowPrefs.GetInt("width", 80);
int lastHeight = windowPrefs.GetInt("height", 24);

var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("My App")
    .WithPosition(lastX, lastY)
    .WithSize(lastWidth, lastHeight)
    .Build();

// Save position when window moves
mainWindow.Moved += (s, e) =>
{
    windowPrefs.SetInt("x", mainWindow.X);
    windowPrefs.SetInt("y", mainWindow.Y);
};

mainWindow.Resized += (s, e) =>
{
    windowPrefs.SetInt("width", mainWindow.Width);
    windowPrefs.SetInt("height", mainWindow.Height);
};

// User preferences
var prefs = registry.OpenSection("app/preferences");
string theme = prefs.GetString("theme", "ModernGray");
windowSystem.ThemeRegistry.SetTheme(theme);

windowSystem.ThemeStateService.ThemeChanged += (s, e) =>
{
    prefs.SetString("theme", e.NewTheme.Name);
};

windowSystem.AddWindow(mainWindow);
windowSystem.Run();
// Registry saves automatically here (Dispose → RegistryStateService.Dispose → Save)
```

---

## See Also

- [Configuration](CONFIGURATION.md) — System-level configuration options
- [State Services](STATE-SERVICES.md) — All built-in state services

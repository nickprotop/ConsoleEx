# SharpConsoleUI Templates

Project templates for creating terminal UI applications with [SharpConsoleUI](https://github.com/nickprotop/ConsoleEx).

## Installation

```bash
dotnet new install SharpConsoleUI.Templates
```

## Templates

| Template | Short Name | Description |
|----------|------------|-------------|
| TUI Starter App | `tui-app` | Single window with list, button, and notification |
| TUI Dashboard | `tui-dashboard` | Fullscreen dashboard with tabs, table, and live metrics |
| TUI Multi-Window | `tui-multiwindow` | Two communicating windows with master-detail pattern |

## Usage

```bash
# Create a starter app (defaults to .NET 9)
dotnet new tui-app -n MyApp

# Create targeting .NET 8 (LTS)
dotnet new tui-app -n MyApp --Framework net8.0

# Create a dashboard
dotnet new tui-dashboard -n MyDashboard

# Create a multi-window app
dotnet new tui-multiwindow -n MyApp

# Build and run
cd MyApp
dotnet run
```

## Framework Options

All templates support the `--Framework` parameter:

| Value | Description |
|-------|-------------|
| `net10.0` | .NET 10 |
| `net9.0` | .NET 9 (default) |
| `net8.0` | .NET 8 (LTS) |

## Uninstall

```bash
dotnet new uninstall SharpConsoleUI.Templates
```

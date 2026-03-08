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
# Create a starter app
dotnet new tui-app -n MyApp

# Create a dashboard
dotnet new tui-dashboard -n MyDashboard

# Create a multi-window app
dotnet new tui-multiwindow -n MyApp

# Build and run
cd MyApp
dotnet run
```

## Uninstall

```bash
dotnet new uninstall SharpConsoleUI.Templates
```

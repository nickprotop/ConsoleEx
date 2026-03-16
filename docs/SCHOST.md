# schost — Desktop Host Tool

schost is a CLI tool that launches SharpConsoleUI apps in a configured terminal window and packages them for desktop distribution. End users double-click a launcher or shortcut — they never see a shell prompt.

## Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Commands](#commands)
  - [schost init](#schost-init)
  - [schost run](#schost-run)
  - [schost pack](#schost-pack)
  - [schost install](#schost-install)
- [Configuration (schost.json)](#configuration-schostjson)
- [Project Template](#project-template)
- [Terminal Support](#terminal-support)
- [End User Experience](#end-user-experience)
- [How It Works](#how-it-works)

## Overview

SharpConsoleUI apps require a terminal to run. Non-technical users don't know what a terminal is. schost bridges this gap:

- **Launches** apps in a configured terminal (Windows Terminal, Linux terminal emulators) with custom title, font, colors, and size
- **Packages** as single-file exe + portable zip + launcher shortcut
- **Optionally builds** an Inno Setup installer (Windows) or .desktop file (Linux)
- **Provides** a `dotnet new` template for quick project scaffolding

This is NOT a custom rendering driver. Apps use the real terminal, real `NetConsoleDriver`, 100% rendering fidelity. schost just handles the "open a nice terminal window" and "package for distribution" parts.

## Installation

Install from NuGet (recommended):

```bash
dotnet tool install -g SharpConsoleUI.Host
```

Then use `schost` directly from anywhere.

### Alternative: from source

If you're working from the ConsoleEx repository, you can run it directly:

```bash
dotnet run --project tools/schost/src/schost -- <command>
```

Or build and install locally:

```bash
cd tools/schost
dotnet pack src/schost -c Release
dotnet tool install --global --add-source src/schost/nupkg SharpConsoleUI.Host
```

## Quick Start

### New project

```bash
# Install the template
dotnet new install tools/schost/templates/schost-app

# Create a project
dotnet new schost-app -n MyApp --title "My App"
cd MyApp

# Run it
schost run

# Package it
schost pack --installer
```

### Existing project

```bash
# Initialize config (interactive prompts)
schost init path/to/MyApp.csproj

# Launch in configured terminal
schost run

# Package for distribution
schost pack
```

## Commands

### schost init

```
schost init [project]
```

Initializes a `schost.json` configuration file for an existing project.

- **project** — path to a `.csproj` file or directory containing one. Auto-detects if omitted; prompts if multiple found.

Interactive Spectre prompts ask for:
- Window title
- Font face and size
- Terminal columns and rows
- Color scheme (Windows Terminal scheme name)

**Example:**

```bash
schost init                              # Auto-detect .csproj in current directory
schost init MyApp/MyApp.csproj           # Specific project
schost init Examples/NavigationViewDemo  # Directory containing .csproj
```

### schost run

```
schost run [project] [--no-build] [--inline]
```

Builds and launches the app in a new terminal window with the configured settings.

| Option | Description |
|--------|-------------|
| `--no-build` | Skip building before launch |
| `--inline` | Run in the current terminal instead of opening a new one |

**Default behavior** opens a new terminal window — the developer sees exactly what the end user will see. The current terminal stays free for more commands.

**`--inline`** runs in the current terminal, equivalent to `dotnet run`. Useful for debugging or CI.

**Terminal selection:**
- **Windows**: Windows Terminal (preferred) → conhost (fallback)
- **Linux**: auto-detects best available terminal emulator

### schost pack

```
schost pack [project] [-o dir] [-r runtime] [--installer] [--no-trim] [--no-self-contained]
```

Publishes the app as a self-contained single-file executable and creates distribution artifacts.

| Option | Description |
|--------|-------------|
| `-o`, `--output` | Output directory (default: `publish/` in project dir) |
| `-r`, `--runtime` | Runtime identifier, e.g. `win-x64`, `linux-x64` (default: current platform) |
| `--installer` | Build an Inno Setup installer (Windows only) |
| `--no-trim` | Disable IL trimming |
| `--no-self-contained` | Don't bundle the .NET runtime |

**Output artifacts:**

| Artifact | Description |
|----------|-------------|
| `app/` directory | Published executable + dependencies |
| `.cmd` / `.desktop` | Launcher script (tries WT, falls back to direct) |
| `.svg` icon | Generated from app title initial (or copied custom icon) |
| Portable `.zip` | Ready-to-distribute archive |
| `*-setup.exe` | Inno Setup installer (if `--installer`) |

**Example:**

```bash
schost pack                          # Package for current platform
schost pack -r win-x64 --installer   # Windows installer
schost pack -o ./dist -r linux-x64   # Linux package to ./dist
```

### schost install

```
schost install [project] --exe <path> [--uninstall]
```

Registers or unregisters a terminal profile for the app.

- **Windows**: Installs a Windows Terminal fragment profile — the app appears in the WT dropdown alongside PowerShell and CMD
- **Linux**: Creates a `.desktop` file for application launchers

```bash
schost install --exe ./publish/app/MyApp.exe       # Register WT profile
schost install --uninstall                          # Remove WT profile
```

## Configuration (schost.json)

Created by `schost init`, this file lives next to the `.csproj`:

```json
{
  "title": "My App",
  "font": "Cascadia Code",
  "fontSize": 14,
  "columns": 120,
  "rows": 35,
  "colorScheme": "One Half Dark",
  "icon": null,
  "selfContained": true,
  "installer": false
}
```

| Field | Type | Description |
|-------|------|-------------|
| `title` | string | Window title (defaults to AssemblyName) |
| `font` | string | Font face for terminal |
| `fontSize` | int | Font size |
| `columns` | int | Terminal width in columns |
| `rows` | int | Terminal height in rows |
| `colorScheme` | string | Windows Terminal color scheme name |
| `icon` | string | Path to custom icon file (relative to project) |
| `target` | string | Default runtime target |
| `selfContained` | bool | Bundle .NET runtime (default: true) |
| `output` | string | Default output directory |
| `installer` | bool | Build installer by default |

## Project Template

Install the template:

```bash
dotnet new install tools/schost/templates/schost-app
```

Create a project:

```bash
dotnet new schost-app -n MyApp --title "My App"
```

The template creates a fullscreen NavigationView application with:
- Maximized window, no title bar, gradient background
- NavigationView with 3 pages: Home, Settings, About
- Progress bars, checkboxes, system info display
- Pre-configured `schost.json`
- Esc to quit

This provides a clean starting point that looks like a desktop app from the moment it launches.

## Terminal Support

### Windows

| Terminal | Priority | Notes |
|----------|----------|-------|
| **Windows Terminal** | Preferred | Fragment profiles, custom fonts/colors, tabbed |
| **conhost** | Fallback | Basic console host, always available |

Windows Terminal fragments are JSON files installed to `%LOCALAPPDATA%\Microsoft\Windows Terminal\Fragments\{app}\`. They create a profile that appears in the WT dropdown without modifying the user's settings.

### Linux

schost auto-detects the best available terminal emulator:

| Terminal | Detection |
|----------|-----------|
| ghostty | `which ghostty` |
| kitty | `which kitty` |
| alacritty | `which alacritty` |
| wezterm | `which wezterm` |
| foot | `which foot` |
| gnome-terminal | `which gnome-terminal` |
| konsole | `which konsole` |
| xfce4-terminal | `which xfce4-terminal` |
| xterm | `which xterm` |

## End User Experience

The end user never interacts with schost. They receive one of:

1. **Portable ZIP** — extract, double-click the `.cmd` launcher. Opens in Windows Terminal (or fallback console) with configured title, font, and colors. No install needed.

2. **Installer** — run `setup.exe`. No admin required. Installs to `%LOCALAPPDATA%`, creates Start Menu shortcut. Optional desktop shortcut. Post-install launch option.

3. **WT Profile** — app appears in Windows Terminal dropdown. Click to open in a new tab.

4. **Linux .desktop** — app appears in GNOME/KDE application launcher. Click to open in the best available terminal.

The app opens in a terminal window that looks like a desktop app — custom title, configured font, proper size, no shell prompt visible.

## How It Works

schost does not replace or wrap the terminal. It simply:

1. **Configures** terminal settings (font, colors, size) via `schost.json`
2. **Launches** the app executable inside a properly configured terminal window
3. **Packages** the executable with a launcher script that reproduces those settings

The app itself is a standard SharpConsoleUI application using `NetConsoleDriver`. No special API, no compatibility layer, no rendering differences. If it works in your terminal, it works through schost.

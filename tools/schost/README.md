# schost — SharpConsoleUI Desktop Host Tool

A CLI tool that launches SharpConsoleUI apps in a configured terminal and packages them for distribution.

## Install

```bash
cd tools/schost
dotnet pack src/schost -c Release
dotnet tool install --global --add-source src/schost/nupkg SharpConsoleUI.Host
```

Or run directly:
```bash
dotnet run --project src/schost -- <command>
```

## Quick Start

### New project
```bash
dotnet new install tools/schost/templates/schost-app
dotnet new schost-app -n MyApp --title "My App"
cd MyApp
schost run
schost pack --installer
```

### Existing project
```bash
schost init MyApp/MyApp.csproj
schost run
schost pack
```

## Commands

### `schost init [project]`
Interactive setup — creates `schost.json` with terminal preferences (title, font, size, colors).

### `schost run [project] [--no-build] [--inline]`
Builds and launches the app in a new terminal window with configured settings.
- Default: opens new Windows Terminal tab (or Linux terminal emulator)
- `--inline`: runs in current terminal (for debugging)

### `schost pack [project] [-o dir] [-r runtime] [--installer] [--no-trim]`
Publishes as single-file exe, creates portable ZIP, launcher script, and optional Inno Setup installer.

### `schost install [project] --exe <path> [--uninstall]`
Registers/unregisters a Windows Terminal profile or Linux .desktop file.

## Configuration (schost.json)

```json
{
  "title": "My App",
  "font": "Cascadia Code",
  "fontSize": 14,
  "columns": 120,
  "rows": 35,
  "colorScheme": "One Half Dark"
}
```

## Terminal Support

**Windows:** Windows Terminal (preferred) → conhost (fallback)

**Linux:** ghostty, kitty, alacritty, wezterm, foot, gnome-terminal, konsole, xfce4-terminal, xterm

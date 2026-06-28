# Built-in Dialogs

SharpConsoleUI provides built-in dialog windows for common tasks. This guide covers two categories:

- **[Message Dialogs](#message-dialogs)** — `Dialogs.ConfirmAsync`, `PromptAsync`, and
  `RunWithProgressAsync`: the typed, themed prompt/confirm/progress primitives (the missing
  `MessageBox` layer). These are standalone and work without any flow composition; they are also
  the building blocks the [Composable Flows](FLOWS.md) engine uses internally.
- **[File and System Dialogs](#file-dialogs)** — file pickers, save dialogs, theme selector, and
  other shell-level windows.

## Table of Contents

- [Message Dialogs](#message-dialogs)
  - [ConfirmAsync](#confirmAsync)
  - [PromptAsync](#promptasync)
  - [RunWithProgressAsync](#runwithprogressasync)
  - [The severity parameter](#the-severity-parameter)
  - [Cancel semantics](#cancel-semantics)
  - [Threading notes for message dialogs](#threading-notes-for-message-dialogs)
- [File Dialogs](#file-dialogs)
  - [Folder Picker](#folder-picker)
  - [File Picker (Open)](#file-picker-open)
  - [Save File Dialog](#save-file-dialog)
- [System Dialogs](#system-dialogs)
  - [Theme Selector](#theme-selector)

---

## Message Dialogs

`Dialogs` (`SharpConsoleUI.Dialogs`) provides three typed, themed modal dialogs — confirm,
prompt, and progress — that work from any async button handler without requiring any flow
composition setup.

```csharp
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Core;

// Confirm
bool ok = await Dialogs.ConfirmAsync(ws, "Save changes", "Save before closing?");

// Prompt
string? name = await Dialogs.PromptAsync(ws, "Your name", "What should we call you?",
    initial: "World");

// Progress
string? result = await Dialogs.RunWithProgressAsync<string>(ws,
    "Syncing", "Connecting…",
    async (ct, progress) =>
    {
        progress.Report("Downloading…");
        await Task.Delay(1000, ct);
        return "done";
    });
```

### ConfirmAsync

```csharp
public static Task<bool> Dialogs.ConfirmAsync(
    ConsoleWindowSystem windowSystem,
    string title,
    string message,
    string ok = "OK",
    string cancel = "Cancel",
    NotificationSeverityEnum severity = NotificationSeverityEnum.Info,
    Window? parent = null)
```

Shows a modal dialog with `message` and two buttons. Returns `true` when the user clicks the
affirmative button, `false` on Cancel or dismiss (Esc, title-bar close).

| Parameter | Default | Description |
|---|---|---|
| `windowSystem` | — | The `ConsoleWindowSystem` to host the dialog in. |
| `title` | — | Title in the window chrome and the bold glyph banner. |
| `message` | — | Body text shown to the user. |
| `ok` | `"OK"` | Label for the affirmative button. |
| `cancel` | `"Cancel"` | Label for the dismiss button. |
| `severity` | `Info` | Glyph, accent rule colour, and button color role. |
| `parent` | `null` | When provided, the dialog is modal to that window only. |

```csharp
bool confirmed = await Dialogs.ConfirmAsync(
    ws,
    "Delete project",
    "This permanently deletes the project. Continue?",
    ok: "Delete",
    cancel: "Keep",
    severity: NotificationSeverityEnum.Danger,
    parent: myWindow);

if (confirmed)
    await DeleteProjectAsync();
```

### PromptAsync

```csharp
public static Task<string?> Dialogs.PromptAsync(
    ConsoleWindowSystem windowSystem,
    string title,
    string message,
    string? initial = null,
    NotificationSeverityEnum severity = NotificationSeverityEnum.Info,
    Window? parent = null)
```

Shows a modal dialog with `message` and a single-line text input. Returns the entered text on
OK/Enter, or `null` on Cancel or dismiss.

| Parameter | Default | Description |
|---|---|---|
| `windowSystem` | — | The `ConsoleWindowSystem` to host the dialog in. |
| `title` | — | Title in the window chrome. |
| `message` | — | Question or label shown above the input. |
| `initial` | `null` | Pre-filled text (empty when `null`). |
| `severity` | `Info` | Glyph, accent rule colour, and button color role. |
| `parent` | `null` | When provided, the dialog is modal to that window only. |

```csharp
string? entered = await Dialogs.PromptAsync(
    ws,
    "Rename",
    "Enter a new name for the file:",
    initial: currentName,
    parent: myWindow);

if (entered is not null)
    RenameFile(currentName, entered);
```

### RunWithProgressAsync

```csharp
public static Task<T?> Dialogs.RunWithProgressAsync<T>(
    ConsoleWindowSystem windowSystem,
    string title,
    string description,
    Func<CancellationToken, IProgress<string>, Task<T>> work,
    Window? parent = null)
```

Shows a modal progress dialog while running `work` on a background `Task`. A live status line
is updated via `IProgress<string>`. Returns the work's result on success, `default(T)` on
cancellation, or re-throws if the work throws.

| Parameter | Description |
|---|---|
| `windowSystem` | The `ConsoleWindowSystem` to host the dialog in. |
| `title` | Title in the window chrome. |
| `description` | Initial status text below the accent rule. |
| `work` | Async function to run. Receives a `CancellationToken` (tripped on Cancel/dismiss) and an `IProgress<string>` for live status updates. |
| `parent` | When provided, the dialog is modal to that window only. |

```csharp
const int totalSteps = 5;

string? result = await Dialogs.RunWithProgressAsync<string>(
    ws,
    "Installing",
    "Preparing…",
    async (ct, progress) =>
    {
        for (int i = 1; i <= totalSteps; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress.Report($"Step {i}/{totalSteps}: copying files…");
            await Task.Delay(500, ct);
        }
        return "Installation complete";
    },
    parent: myWindow);

if (result is null)
    ShowStatus("Installation cancelled.");
else
    ShowStatus(result);
```

### The severity parameter

`ConfirmAsync` and `PromptAsync` accept a `NotificationSeverityEnum severity` parameter
controlling three visual elements: the glyph in the banner line, the accent rule colour, and
the color role applied to the affirmative button.

| `NotificationSeverityEnum` | Glyph | Color role |
|---|---|---|
| `Info` (default) | `●` (U+25CF) | `Primary` (blue) |
| `Success` | `✓` (U+2713) | `Success` (green) |
| `Warning` | `⚠` (U+26A0) | `Warning` (yellow/amber) |
| `Danger` | `✖` (U+2716) | `Danger` (red) |

The progress dialog always uses the `⟳` (U+27F3) glyph and `Primary` role (it has no
`severity` parameter).

### Cancel semantics

Cancel and dismiss never throw an exception — every method returns a language-level default:

| Method | Cancel / dismiss returns |
|---|---|
| `ConfirmAsync` | `false` |
| `PromptAsync` | `null` |
| `RunWithProgressAsync<T>` | `default(T)` (typically `null` for reference types) |

Dismissal covers Esc, the title-bar close button, and the Cancel button inside the progress
dialog. If you need to distinguish "cancelled" from "submitted empty input" in `PromptAsync`,
check the return for `null` — an empty string means the user clicked OK with nothing typed.

### Threading notes for message dialogs

All three methods must be called from the UI thread — typically from a `ClickAsync` or `Click`
handler. The returned `Task` can be `await`ed directly.

`RunWithProgressAsync` runs the `work` delegate on a background `Task.Run` thread. Each
`IProgress<string>.Report(msg)` call is automatically marshalled to the UI thread via
`EnqueueOnUIThread`, so status updates are safe without extra marshalling.

See [Threading & Async](THREADING_AND_ASYNC.md) for the full UI-thread model.

---

## File Dialogs

All file dialogs are async methods on `ConsoleWindowSystem` and support optional parent windows for modal behavior.

### Folder Picker

Select a directory from the file system.

```csharp
// Basic usage
string? selectedFolder = await windowSystem.ShowFolderPickerDialogAsync();

if (selectedFolder != null)
{
    Console.WriteLine($"Selected: {selectedFolder}");
}

// With starting path
string? folder = await windowSystem.ShowFolderPickerDialogAsync(
    startPath: "/home/user/documents"
);

// As modal dialog with parent window
string? folder = await windowSystem.ShowFolderPickerDialogAsync(
    startPath: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    parentWindow: mainWindow
);
```

**Parameters:**
- `startPath` (optional): Initial directory to display
- `parentWindow` (optional): Parent window for modal behavior

**Returns:** `Task<string?>` - Selected directory path, or null if cancelled

### File Picker (Open)

Select an existing file from the file system.

```csharp
// Basic usage
string? selectedFile = await windowSystem.ShowFilePickerDialogAsync();

// With starting path
string? file = await windowSystem.ShowFilePickerDialogAsync(
    startPath: "/home/user/documents"
);

// With file filter (extension filter)
string? file = await windowSystem.ShowFilePickerDialogAsync(
    startPath: "/home/user/documents",
    filter: ".txt"
);

// As modal dialog
string? file = await windowSystem.ShowFilePickerDialogAsync(
    startPath: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    filter: ".log",
    parentWindow: mainWindow
);
```

**Parameters:**
- `startPath` (optional): Initial directory to display
- `filter` (optional): File extension filter (e.g., ".txt", ".log")
- `parentWindow` (optional): Parent window for modal behavior

**Returns:** `Task<string?>` - Selected file path, or null if cancelled

### Save File Dialog

Select a location and filename for saving a file.

```csharp
// Basic usage
string? savePath = await windowSystem.ShowSaveFileDialogAsync();

// With starting path and default filename
string? savePath = await windowSystem.ShowSaveFileDialogAsync(
    startPath: "/home/user/documents",
    defaultFileName: "output.txt"
);

// With file filter
string? savePath = await windowSystem.ShowSaveFileDialogAsync(
    startPath: "/home/user/documents",
    filter: ".log",
    defaultFileName: "app.log"
);

// As modal dialog
string? savePath = await windowSystem.ShowSaveFileDialogAsync(
    startPath: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    filter: ".txt",
    defaultFileName: "document.txt",
    parentWindow: mainWindow
);
```

**Parameters:**
- `startPath` (optional): Initial directory to display
- `filter` (optional): File extension filter (e.g., ".txt", ".log")
- `defaultFileName` (optional): Pre-filled filename
- `parentWindow` (optional): Parent window for modal behavior

**Returns:** `Task<string?>` - Selected save path, or null if cancelled

## System Dialogs

System dialogs provide access to settings, configuration, and application information. These dialogs are accessible through the Start Menu > System category or can be called programmatically.

### Settings Dialog

Shows a dialog with links to various configuration dialogs (Theme, Performance, About).

```csharp
// Show settings dialog
using SharpConsoleUI.Dialogs;

SettingsDialog.Show(windowSystem);

// Or as modal to a parent window
SettingsDialog.Show(windowSystem, parentWindow);
```

The settings dialog provides access to:
- **Change Theme...** - Opens Theme Selector Dialog
- **Performance Settings...** - Opens Performance Dialog
- **About...** - Opens About Dialog

**Navigation:**
- Arrow keys to navigate options
- Enter or double-click to select
- Escape to close

### Theme Selector

Display a dialog for selecting and switching themes at runtime.

```csharp
// Show theme selector dialog
using SharpConsoleUI.Dialogs;

windowSystem.ShowThemeSelectorDialog();

// Or as modal to a parent window
ThemeSelectorDialog.Show(windowSystem, parentWindow);
```

The theme selector dialog displays all registered themes and allows the user to switch between them. The selected theme is applied immediately to all windows.

**Available Built-in Themes:**
- **ModernGray** - Modern dark theme with gray color scheme (default)
- **Daylight** - Light theme with a blue accent
- Plus palette-generated seed themes: Ocean, Amber, Forest, Crimson, Slate

**Navigation:**
- Arrow keys to select theme
- Enter or double-click to apply
- Escape to close

### Performance Dialog

Configure performance and rendering settings.

```csharp
// Show performance configuration dialog
using SharpConsoleUI.Dialogs;

PerformanceDialog.Show(windowSystem);

// Or as modal to a parent window
PerformanceDialog.Show(windowSystem, parentWindow);
```

The performance dialog allows runtime configuration of:
- **Performance Metrics Display** - Toggle FPS and metrics overlay
- **Frame Rate Limiting** - Toggle frame rate limiting on/off
- **Target FPS** - Set target FPS (30, 60, 120, or 144)

**Runtime Methods:**

```csharp
// Toggle performance metrics programmatically
windowSystem.SetPerformanceMetrics(true);
bool enabled = windowSystem.IsPerformanceMetricsEnabled();

// Toggle frame rate limiting
windowSystem.SetFrameRateLimiting(false);  // Unlimited FPS
bool limited = windowSystem.IsFrameRateLimitingEnabled();

// Set target FPS
windowSystem.SetTargetFPS(30);
int fps = windowSystem.GetTargetFPS();
```

**Navigation:**
- Arrow keys to navigate options
- Enter or double-click to toggle/configure
- Escape to close

### About Dialog

Display application information, version, and loaded plugins.

```csharp
// Show about dialog
using SharpConsoleUI.Dialogs;

AboutDialog.Show(windowSystem);

// Or as modal to a parent window
AboutDialog.Show(windowSystem, parentWindow);
```

The about dialog displays:
- Application name and version
- Description and core features
- Author and license information
- List of loaded plugins (if any)

**Navigation:**
- Enter or Escape to close

## Complete Example

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

var mainWindow = new WindowBuilder(windowSystem)
    .WithTitle("File Dialog Example")
    .WithSize(80, 25)
    .Centered()
    .Build();

mainWindow.AddControl(
    Controls.Button("Open File")
        .OnClick(async (sender, e, window) =>
        {
            var filePath = await windowSystem.ShowFilePickerDialogAsync(
                startPath: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                filter: ".txt",
                parentWindow: window
            );

            if (filePath != null)
            {
                window.AddControl(new MarkupControl(new List<string>
                {
                    $"[green]Selected file:[/] {filePath}"
                }));
            }
        })
        .Build()
);

mainWindow.AddControl(
    Controls.Button("Save File")
        .OnClick(async (sender, e, window) =>
        {
            var savePath = await windowSystem.ShowSaveFileDialogAsync(
                startPath: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                filter: ".log",
                defaultFileName: "app.log",
                parentWindow: window
            );

            if (savePath != null)
            {
                // Save your file here
                window.AddControl(new MarkupControl(new List<string>
                {
                    $"[green]File will be saved to:[/] {savePath}"
                }));
            }
        })
        .Build()
);

mainWindow.AddControl(
    Controls.Button("Select Folder")
        .OnClick(async (sender, e, window) =>
        {
            var folderPath = await windowSystem.ShowFolderPickerDialogAsync(
                startPath: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                parentWindow: window
            );

            if (folderPath != null)
            {
                window.AddControl(new MarkupControl(new List<string>
                {
                    $"[green]Selected folder:[/] {folderPath}"
                }));
            }
        })
        .Build()
);

mainWindow.AddControl(
    Controls.Button("Change Theme")
        .OnClick((sender, e, window) =>
        {
            windowSystem.ShowThemeSelectorDialog();
        })
        .Build()
);

windowSystem.AddWindow(mainWindow);
windowSystem.Run();
```

## Best Practices

1. **Always use await**: File dialogs are asynchronous operations
2. **Check for null**: User can cancel the dialog, always check return value
3. **Use parentWindow**: Pass parent window for proper modal behavior
4. **Provide startPath**: Help users by starting in a relevant directory
5. **Use filters**: When appropriate, filter files by extension
6. **Handle exceptions**: Wrap dialog calls in try-catch for file system errors

## Navigation Keys

All file dialogs support these keyboard shortcuts:

- **Arrow Keys**: Navigate files/folders
- **Enter**: Select current item / Open folder
- **Backspace**: Go to parent directory
- **Escape**: Cancel dialog
- **Type**: Quick search by typing filename

---

[Back to Main Documentation](../README.md)

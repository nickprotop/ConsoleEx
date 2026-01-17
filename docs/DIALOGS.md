# Built-in Dialogs

SharpConsoleUI provides built-in dialog windows for common tasks like file selection and theme switching.

## Table of Contents

- [File Dialogs](#file-dialogs)
  - [Folder Picker](#folder-picker)
  - [File Picker (Open)](#file-picker-open)
  - [Save File Dialog](#save-file-dialog)
- [System Dialogs](#system-dialogs)
  - [Theme Selector](#theme-selector)

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

### Theme Selector

Display a dialog for selecting and switching themes at runtime.

```csharp
// Show theme selector dialog
windowSystem.ShowThemeSelectorDialog();
```

The theme selector dialog displays all registered themes and allows the user to switch between them. The selected theme is applied immediately to all windows.

**Available Built-in Themes:**
- Classic - Navy blue windows with traditional styling
- ModernGray - Modern dark theme with gray color scheme
- DevDark - Dark developer theme (requires DeveloperTools plugin)

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

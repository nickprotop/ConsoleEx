using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace DemoApp.DemoWindows;

public static class CommandPaletteWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var info = Controls.Markup()
            .AddLine("[bold yellow]Command Palette Demo[/]")
            .AddEmptyLine()
            .AddLine("Press [bold green]Ctrl+P[/] to open the command palette.")
            .AddLine("Type to fuzzy-search commands.")
            .AddEmptyLine()
            .AddLine("[dim]The palette supports categories, shortcuts,")
            .AddLine("and icons for organizing commands.[/]")
            .WithMargin(2, 1, 2, 1)
            .Build();

        var window = new WindowBuilder(ws)
            .WithTitle("Command Palette Demo")
            .WithSize(60, 15)
            .Centered()
            .AddControl(info)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
                else if (e.KeyInfo.Key == ConsoleKey.P && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    ShowPalette(ws);
                    e.Handled = true;
                }
            })
            .BuildAndShow();

        ShowPalette(ws);
        return window;
    }

    private static void ShowPalette(ConsoleWindowSystem ws)
    {
        var palette = CommandPaletteControl.Create()
            .AddItem("New File", () => Notify(ws, "New File"), "Ctrl+N", "File")
            .AddItem("Open File", () => Notify(ws, "Open File"), "Ctrl+O", "File")
            .AddItem("Save", () => Notify(ws, "Save"), "Ctrl+S", "File")
            .AddItem("Find", () => Notify(ws, "Find"), "Ctrl+F", "Edit")
            .AddItem("Replace", () => Notify(ws, "Replace"), "Ctrl+H", "Edit")
            .AddItem("Undo", () => Notify(ws, "Undo"), "Ctrl+Z", "Edit")
            .AddItem("Redo", () => Notify(ws, "Redo"), "Ctrl+Y", "Edit")
            .AddItem("Toggle Terminal", () => Notify(ws, "Terminal"), "Ctrl+`", "View")
            .AddItem("Toggle Sidebar", () => Notify(ws, "Sidebar"), "Ctrl+B", "View")
            .AddItem("Run Build", () => Notify(ws, "Build"), "Ctrl+Shift+B", "Build")
            .AddItem("Run Tests", () => Notify(ws, "Tests"), "Ctrl+Shift+T", "Build")
            .AddItem("Git Commit", () => Notify(ws, "Commit"), null, "Git")
            .AddItem("Git Push", () => Notify(ws, "Push"), null, "Git")
            .WithShowCategories()
            .WithPlaceholder("Type to search commands...")
            .Build();

        // The palette is a portal control - add to any active window
        var activeWindow = ws.WindowStateService.ActiveWindow;
        if (activeWindow != null)
        {
            activeWindow.AddControl(palette);
            palette.Show();
        }
    }

    private static void Notify(ConsoleWindowSystem ws, string command)
    {
        ws.NotificationStateService.ShowNotification(
            "Command", $"Executed: {command}",
            SharpConsoleUI.Core.NotificationSeverity.Info);
    }
}

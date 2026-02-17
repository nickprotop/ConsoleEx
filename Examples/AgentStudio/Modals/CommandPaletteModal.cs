// -----------------------------------------------------------------------
// CommandPaletteModal - Command selection modal dialog
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace AgentStudio.Modals;

/// <summary>
/// Modal dialog for selecting commands
/// </summary>
public static class CommandPaletteModal
{
    /// <summary>
    /// Shows the command palette modal with a callback for the result
    /// </summary>
    /// <param name="windowSystem">The window system</param>
    /// <param name="onCommandSelected">Callback invoked with selected command (null if cancelled)</param>
    public static void Show(ConsoleWindowSystem windowSystem, Action<string?> onCommandSelected)
    {
        var modal = new WindowBuilder(windowSystem)
            .WithTitle("Command Palette")
            .Centered()
            .WithSize(60, 16)
            .AsModal()
            .Borderless()
            .Resizable(false)
            .Movable(false)
            .WithColors(Color.Grey93, Color.Grey15)
            .Build();

        // Header
        modal.AddControl(Controls.Markup()
            .AddLine("[cyan1 bold]Commands[/]")
            .AddLine("[grey50]Select a command to execute[/]")
            .WithAlignment(HorizontalAlignment.Left)
            .WithMargin(1, 0, 1, 0)
            .Build());

        modal.AddControl(Controls.RuleBuilder()
            .WithColor(Color.Grey23)
            .Build());

        // Command list
        var commandList = Controls.List()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithColors(Color.Grey93, Color.Grey15)
            .WithFocusedColors(Color.Grey93, Color.Grey15)
            .WithHighlightColors(Color.White, Color.Grey35)  // Subtle gray highlight instead of bright blue

            .WithDoubleClickActivation(true)  // Double-click selects and closes modal
            .Build();

        // Commands with descriptions
        var commands = new List<(string cmd, string desc)>
        {
            ("/analyze", "Run security analysis on code"),
            ("/diff", "Show code differences"),
            ("/test", "Execute test suite"),
            ("/explain", "Explain code functionality"),
            ("/refactor", "Suggest code improvements"),
            ("/debug", "Start debugging session")
        };

        foreach (var (cmd, desc) in commands)
        {
            var label = $"[cyan1]{cmd,-12}[/] [grey70]{desc}[/]";
            commandList.AddItem(new ListItem(label) { Tag = cmd });
        }

        modal.AddControl(commandList);

        modal.AddControl(Controls.RuleBuilder()
            .WithColor(Color.Grey23)
            .StickyBottom()
            .Build());

        // Footer with instructions
        modal.AddControl(Controls.Markup()
            .AddLine("[grey70]Enter/Double-click: Execute  •  Escape: Cancel[/]")
            .WithAlignment(HorizontalAlignment.Center)
            .WithMargin(0, 0, 0, 0)
            .StickyBottom()
            .Build());

        // Handle double-click activation
        commandList.ItemActivated += (sender, item) =>
        {
            if (item?.Tag is string command)
            {
                onCommandSelected(command);
                modal.Close();
            }
        };

        // Handle Enter and Escape keys
        modal.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Enter)
            {
                // Confirm selection with Enter key
                var selectedItem = commandList.SelectedItem;
                if (selectedItem?.Tag is string command)
                {
                    onCommandSelected(command);
                    modal.Close();
                }
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                // Cancel with Escape key
                onCommandSelected(null);
                modal.Close();
                e.Handled = true;
            }
        };

        // Add modal to window system - it will handle blocking automatically
        windowSystem.AddWindow(modal);
        windowSystem.SetActiveWindow(modal);

        // Focus the list after modal is active
        commandList.SetFocus(true, FocusReason.Programmatic);
    }
}

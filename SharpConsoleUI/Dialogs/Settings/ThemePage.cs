using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs.Settings;

internal static class ThemePage
{
    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
    {
        var currentTheme = windowSystem.ThemeStateService.CurrentTheme?.Name ?? "Unknown";

        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(120,180,255)]Theme[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Current Theme")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddLine($"[bold]Active:[/] [cyan1]{currentTheme}[/]")
            .AddLine($"[bold]Available:[/] {ThemeRegistry.Count}")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.Button("Change Theme...")
            .OnClick((sender, btn, window) =>
            {
                ThemeSelectorDialog.Show(windowSystem, window);
            })
            .WithMargin(0, 1, 0, 0)
            .Build());
    }
}

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs.Settings;

internal static class AnimationsPage
{
    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
    {
        var animationCount = windowSystem.Animations.ActiveCount;

        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(252,152,103)]Animations[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Settings")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Ctl.Checkbox("Enable animations")
            .Checked(windowSystem.Animations.IsEnabled)
            .OnCheckedChanged((sender, isChecked) =>
            {
                windowSystem.Animations.IsEnabled = isChecked;
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Status")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddLine($"[bold]Active Animations:[/] {animationCount}")
            .AddEmptyLine()
            .AddLine("[dim]Animations include window transitions,[/]")
            .AddLine("[dim]navigation pane resizing, and control effects.[/]")
            .Build());
    }
}

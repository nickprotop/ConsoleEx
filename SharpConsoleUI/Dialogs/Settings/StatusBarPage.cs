using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs.Settings;

internal static class StatusBarPage
{
    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
    {
        var statusService = windowSystem.StatusBarStateService;

        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(120,180,255)]Status Bar[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Visibility")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Ctl.Checkbox("Show top status bar")
            .Checked(statusService.ShowTopStatus)
            .OnCheckedChanged((sender, isChecked) =>
            {
                statusService.ShowTopStatus = isChecked;
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Ctl.Checkbox("Show bottom status bar")
            .Checked(statusService.ShowBottomStatus)
            .OnCheckedChanged((sender, isChecked) =>
            {
                statusService.ShowBottomStatus = isChecked;
            })
            .WithMargin(0, 1, 0, 0)
            .Build());
    }
}

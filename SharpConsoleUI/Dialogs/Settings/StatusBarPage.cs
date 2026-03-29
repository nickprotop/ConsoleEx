using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs.Settings;

internal static class StatusBarPage
{
    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
    {
        var panelService = windowSystem.PanelStateService;

        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(120,180,255)]Panels[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Visibility")
            .WithColor(new Color(60, 100, 160))
            .Build());

        // Read initial state from panel service
        bool topVisible = panelService.TopPanel?.Visible ?? panelService.ShowTopPanel;
        bool bottomVisible = panelService.BottomPanel?.Visible ?? panelService.ShowBottomPanel;

        panel.AddControl(Ctl.Checkbox("Show top panel")
            .Checked(topVisible)
            .OnCheckedChanged((sender, isChecked) =>
            {
                panelService.ShowTopPanel = isChecked;
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Ctl.Checkbox("Show bottom panel")
            .Checked(bottomVisible)
            .OnCheckedChanged((sender, isChecked) =>
            {
                panelService.ShowBottomPanel = isChecked;
            })
            .WithMargin(0, 1, 0, 0)
            .Build());
    }
}

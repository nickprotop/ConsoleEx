using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using System.Drawing;

var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer);

// Sample data: items with details
var items = new Dictionary<string, string[]>
{
    ["Server Alpha"] = new[] {
        "[bold]Server Alpha[/]",
        "",
        "Status:   [green]Online[/]",
        "CPU:      23%",
        "Memory:   4.2 / 8.0 GB",
        "Uptime:   14 days, 3 hours",
    },
    ["Server Beta"] = new[] {
        "[bold]Server Beta[/]",
        "",
        "Status:   [yellow]Degraded[/]",
        "CPU:      87%",
        "Memory:   7.1 / 8.0 GB",
        "Uptime:   2 days, 11 hours",
    },
    ["Server Gamma"] = new[] {
        "[bold]Server Gamma[/]",
        "",
        "Status:   [green]Online[/]",
        "CPU:      45%",
        "Memory:   3.8 / 16.0 GB",
        "Uptime:   30 days, 7 hours",
    },
    ["Server Delta"] = new[] {
        "[bold]Server Delta[/]",
        "",
        "Status:   [red]Offline[/]",
        "CPU:      0%",
        "Memory:   0 / 8.0 GB",
        "Uptime:   -",
    },
};

var itemNames = items.Keys.ToList();

// Detail markup control (updated on selection change)
var detailMarkup = Controls.Markup("[dim]Select a server from the list[/]")
    .Centered()
    .VerticallyCentered()
    .Build();

// Left window: server list
var listWindow = new WindowBuilder(windowSystem)
    .WithTitle("Servers")
    .WithBounds(2, 1, 35, 20)
    .AddControls(
        Controls.List()
            .AddItems(itemNames.ToArray())
            .OnSelectionChanged((sender, index) =>
            {
                if (index >= 0 && index < itemNames.Count)
                {
                    var detail = items[itemNames[index]];
                    detailMarkup.SetContent(new List<string>(detail));
                }
            })
            .WithName("serverList")
            .Build()
    )
    .BuildAndShow();

// Right window: server details
var detailWindow = new WindowBuilder(windowSystem)
    .WithTitle("Details")
    .WithBounds(39, 1, 50, 20)
    .AddControls(
        detailMarkup,

        Controls.Separator(),

        Controls.Button("Restart Server")
            .Centered()
            .StickyBottom()
            .OnClick((sender, btn) =>
            {
                windowSystem.NotificationStateService.ShowNotification(
                    "Action", "Restart command sent.", NotificationSeverity.Info);
            })
            .Build()
    )
    .BuildAndShow();

windowSystem.Run();

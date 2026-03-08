using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;

var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer);

var window = new WindowBuilder(windowSystem)
    .WithTitle("TuiApp")
    .WithSize(60, 20)
    .Centered()
    .AddControls(
        Controls.Markup("[bold yellow]Welcome to TuiApp[/]")
            .AddLine("")
            .AddLine("Use [green]↑↓[/] to navigate, [green]Enter[/] to select")
            .Centered()
            .Build(),

        Controls.Separator(),

        Controls.List("Items")
            .AddItems("First item", "Second item", "Third item", "Fourth item")
            .OnSelectionChanged((sender, index) =>
            {
                windowSystem.NotificationStateService.ShowNotification(
                    "Selected", $"Item index: {index}", NotificationSeverity.Info);
            })
            .WithName("items")
            .Build(),

        Controls.Separator(),

        Controls.Button("Show Notification")
            .Centered()
            .OnClick((sender, btn) =>
            {
                windowSystem.NotificationStateService.ShowNotification(
                    "Hello!", "Button clicked successfully.", NotificationSeverity.Success);
            })
            .Build()
    )
    .BuildAndShow();

windowSystem.Run();

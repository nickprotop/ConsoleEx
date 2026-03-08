using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;

namespace DemoApp.DemoWindows;

public static class NotificationsWindow
{
    private const int WindowWidth = 55;
    private const int WindowHeight = 22;
    private const int ButtonWidth = 30;
    private const int ButtonLeftMargin = 1;
    private const int SectionTopMargin = 1;

    public static Window Create(ConsoleWindowSystem ws)
    {
        var header = Controls.Markup("[bold underline]Notification System Demo[/]")
            .AddLine("")
            .AddLine("Click buttons to trigger notifications.")
            .AddLine("Dismiss via: [yellow]Close[/] button, title bar [yellow][[X]][/],")
            .AddLine("or press [yellow]Escape[/].")
            .Build();

        var infoBtn = CreateButton("Info Notification", SectionTopMargin, () =>
            ws.NotificationStateService.ShowNotification(
                "Information", "This is an informational message.",
                NotificationSeverity.Info));

        var successBtn = CreateButton("Success Notification", 0, () =>
            ws.NotificationStateService.ShowNotification(
                "Success", "Operation completed successfully!",
                NotificationSeverity.Success));

        var warningBtn = CreateButton("Warning Notification", 0, () =>
            ws.NotificationStateService.ShowNotification(
                "Warning", "Disk space is running low.",
                NotificationSeverity.Warning));

        var dangerBtn = CreateButton("Danger Notification", 0, () =>
            ws.NotificationStateService.ShowNotification(
                "Error", "Connection to server lost!",
                NotificationSeverity.Danger));

        var modalBtn = CreateButton("Modal Notification", SectionTopMargin, () =>
            ws.NotificationStateService.ShowNotification(
                "Confirm", "This is modal - dismiss to continue.",
                NotificationSeverity.Warning, blockUi: true, timeout: null));

        var persistentBtn = CreateButton("Persistent (no timeout)", 0, () =>
            ws.NotificationStateService.ShowNotification(
                "Persistent", "This won't auto-dismiss.",
                NotificationSeverity.Info, timeout: null));

        var multilineBtn = CreateButton("Multiline Message", 0, () =>
            ws.NotificationStateService.ShowNotification(
                "Details",
                "Line 1: Build started\nLine 2: Compiling sources\nLine 3: Build succeeded",
                NotificationSeverity.Success, timeout: null));

        var dismissBtn = CreateButton("Dismiss All", SectionTopMargin, () =>
            ws.NotificationStateService.DismissAll());

        return new WindowBuilder(ws)
            .WithTitle("Notifications")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .AddControls(header, infoBtn, successBtn, warningBtn, dangerBtn,
                modalBtn, persistentBtn, multilineBtn, dismissBtn)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }

    private static ButtonControl CreateButton(string text, int topMargin, Action onClick)
    {
        return Controls.Button(text)
            .WithWidth(ButtonWidth)
            .WithMargin(ButtonLeftMargin, topMargin, 0, 0)
            .OnClick((_, _) => onClick())
            .Build();
    }
}

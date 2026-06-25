using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;

namespace DemoApp.DemoWindows;

public static class ToastsWindow
{
	private const int WindowWidth = 55;
	private const int WindowHeight = 24;
	private const int ButtonWidth = 32;
	private const int ButtonLeftMargin = 1;
	private const int SectionTopMargin = 1;

	public static Window Create(ConsoleWindowSystem ws)
	{
		var header = Controls.Markup("[bold underline]Toast Notifications[/]")
			.AddLine("")
			.AddLine("Lightweight, non-blocking overlays that stack")
			.AddLine("in a corner and auto-dismiss. Click a toast to")
			.AddLine("dismiss it; sticky toasts stay until dismissed.")
			.Build();

		var successBtn = CreateButton("Success Toast", SectionTopMargin, () =>
			ws.ToastService.Show("Saved successfully", NotificationSeverity.Success));

		var infoBtn = CreateButton("Info Toast", 0, () =>
			ws.ToastService.Show("Sync started", NotificationSeverity.Info));

		var warningBtn = CreateButton("Warning Toast", 0, () =>
			ws.ToastService.Show("Disk space is low", NotificationSeverity.Warning));

		var dangerBtn = CreateButton("Danger Toast (sticky)", 0, () =>
			ws.ToastService.Show("Connection lost", NotificationSeverity.Danger,
				new ToastOptions(Sticky: true)));

		var topRightBtn = CreateButton("Top-Right Position", SectionTopMargin, () =>
			ws.ToastService.Show("Anchored top-right", NotificationSeverity.Info,
				new ToastOptions(Position: ToastPosition.TopRight)));

		var bottomLeftBtn = CreateButton("Bottom-Left Position", 0, () =>
			ws.ToastService.Show("Anchored bottom-left", NotificationSeverity.Success,
				new ToastOptions(Position: ToastPosition.BottomLeft)));

		var stackBtn = CreateButton("Stack Three", 0, () =>
		{
			ws.ToastService.Show("First", NotificationSeverity.Info);
			ws.ToastService.Show("Second", NotificationSeverity.Warning);
			ws.ToastService.Show("Third", NotificationSeverity.Success);
		});

		var dismissBtn = CreateButton("Dismiss All", SectionTopMargin, () =>
			ws.ToastService.DismissAll());

		return new WindowBuilder(ws)
			.WithTitle("Toasts")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControls(header, successBtn, infoBtn, warningBtn, dangerBtn,
				topRightBtn, bottomLeftBtn, stackBtn, dismissBtn)
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

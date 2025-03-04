// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Controls;
using ConsoleEx.Helpers;

namespace ConsoleEx.Services.NotificationsService
{
	public static class Notifications
	{
		public static void ShowNotification(ConsoleWindowSystem consoleWindowSystem, string title, string message, NotificationSeverity severity, bool? blockUi = false, int? timeout = 5000, Window? parentWindow = null)
		{
			var notificationWindow = new Window(consoleWindowSystem, parentWindow)
			{
				Title = string.IsNullOrWhiteSpace(title) ? severity.Name ?? "Notification" : title,
				Left = consoleWindowSystem.DesktopDimensions.Width / 2 - (AnsiConsoleHelper.StripSpectreLength(message) + 8) / 2,
				Top = consoleWindowSystem.DesktopDimensions.Height / 2 - 2,
				Width = AnsiConsoleHelper.StripSpectreLength(message) + 8,
				Height = title.Split('\n').ToList().Count + 5,
				BackgroundColor = severity.WindowBackgroundColor(consoleWindowSystem),
				ForegroundColor = consoleWindowSystem.Theme.WindowForegroundColor,
				ActiveBorderForegroundColor = severity.ActiveBorderForegroundColor(consoleWindowSystem),
				InactiveBorderForegroundColor = severity.InactiveBorderForegroundColor(consoleWindowSystem),
				ActiveTitleForegroundColor = severity.ActiveTitleForegroundColor(consoleWindowSystem),
				InactiveTitleForegroundColor = severity.InactiveTitleForegroundColor(consoleWindowSystem),
				IsResizable = false
			};

			consoleWindowSystem.AddWindow(notificationWindow);
			consoleWindowSystem.SetActiveWindow(notificationWindow);

			if (blockUi == true)
			{
				notificationWindow.Mode = WindowMode.Modal;
			}

			var notificationContent = new MarkupControl(new List<string>() { $"{severity.Icon}{(string.IsNullOrEmpty(severity.Icon) ? string.Empty : " ")}{message}" })
			{
				Alignment = Alignment.Left
			};
			notificationWindow.AddContent(notificationContent);

			var closeButton = new ButtonControl()
			{
				Text = "Close",
				StickyPosition = StickyPosition.Bottom,
				Margin = new Margin() { Left = 1 }
			};
			notificationWindow.AddContent(closeButton);
			closeButton.OnClick += (s) =>
			{
				consoleWindowSystem.CloseWindow(notificationWindow);
			};

			if (timeout.HasValue && timeout.Value != 0)
			{
				Task.Run(async () =>
				{
					await Task.Delay(timeout.Value);
					consoleWindowSystem.CloseWindow(notificationWindow);
				});
			}
		}
	}
}

using ConsoleEx.Contents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx.Services
{
	public static class Notifications
	{
		public static void ShowNotification(ConsoleWindowSystem consoleWindowSystem, string title, string message, NotificationSeverity severity, bool? blockUi = false, int? timeout = 5000)
		{
			var notificationWindow = new Window(consoleWindowSystem)
			{
				Title = string.IsNullOrWhiteSpace(title) ? severity.Name ?? "Notification" : title,
				Left = consoleWindowSystem.DesktopDimensions.Width / 2 - (AnsiConsoleHelper.StripSpectreLength(message) + 8) / 2,
				Top = consoleWindowSystem.DesktopDimensions.Height / 2 - 2,
				Width = AnsiConsoleHelper.StripSpectreLength(message) + 8,
				Height = title.Split('\n').ToList().Count + 5,
				BackgroundColor = severity.BackgroundColor(consoleWindowSystem),
				ForegroundColor = consoleWindowSystem.Theme.WindowForegroundColor,
				IsResizable = false
			};

			if (blockUi == true)
			{
				consoleWindowSystem.BlockUi.Enqueue(true);
			}

			consoleWindowSystem.AddWindow(notificationWindow);
			consoleWindowSystem.SetActiveWindow(notificationWindow);

			var notificationContent = new MarkupContent(new List<string>() { $"{severity.Icon}{(string.IsNullOrEmpty(severity.Icon) ? string.Empty : " ")}{message}" })
			{
				Alignment = Alignment.Left
			};
			notificationWindow.AddContent(notificationContent);

			var closeButton = new ButtonContent()
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

			if (blockUi == true)
			{
				notificationWindow.OnClosed += (s, e) =>
				{
					consoleWindowSystem.BlockUi.TryDequeue(out _);
				};
			}

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
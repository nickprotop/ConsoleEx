// -----------------------------------------------------------------------
// NotificationDemo - Showcases the notification system
// Demonstrates all severity levels, dismiss methods, modal notifications,
// and timeout behavior.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace NotificationDemo;

class Program
{
	static int Main(string[] args)
	{
		try
		{
			var driver = new NetConsoleDriver(RenderMode.Buffer);
			var windowSystem = new ConsoleWindowSystem(driver);

			var mainWindow = new Window(windowSystem)
			{
				Title = "Notification Demo",
				Left = 2,
				Top = 1,
				Width = 50,
				Height = 22,
				BackgroundColor = Color.Grey11,
				ForegroundColor = Color.White
			};

			var header = new MarkupControl(new List<string>
			{
				"[bold underline]Notification System Demo[/]",
				"",
				"Click buttons to trigger notifications.",
				"Dismiss via: [yellow]Close[/] button, title bar [yellow]\\[X][/],",
				"or press [yellow]Escape[/]."
			})
			{
				HorizontalAlignment = HorizontalAlignment.Left
			};
			mainWindow.AddControl(header);

			// Info notification
			var infoBtn = new ButtonControl
			{
				Text = "Info Notification",
				Margin = new Margin { Top = 1, Left = 1 }
			};
			infoBtn.Click += (_, _) =>
			{
				windowSystem.NotificationStateService.ShowNotification(
					"Information",
					"This is an informational message.",
					NotificationSeverity.Info);
			};
			mainWindow.AddControl(infoBtn);

			// Success notification
			var successBtn = new ButtonControl
			{
				Text = "Success Notification",
				Margin = new Margin { Left = 1 }
			};
			successBtn.Click += (_, _) =>
			{
				windowSystem.NotificationStateService.ShowNotification(
					"Success",
					"Operation completed successfully!",
					NotificationSeverity.Success);
			};
			mainWindow.AddControl(successBtn);

			// Warning notification
			var warningBtn = new ButtonControl
			{
				Text = "Warning Notification",
				Margin = new Margin { Left = 1 }
			};
			warningBtn.Click += (_, _) =>
			{
				windowSystem.NotificationStateService.ShowNotification(
					"Warning",
					"Disk space is running low.",
					NotificationSeverity.Warning);
			};
			mainWindow.AddControl(warningBtn);

			// Danger notification
			var dangerBtn = new ButtonControl
			{
				Text = "Danger Notification",
				Margin = new Margin { Left = 1 }
			};
			dangerBtn.Click += (_, _) =>
			{
				windowSystem.NotificationStateService.ShowNotification(
					"Error",
					"Connection to server lost!",
					NotificationSeverity.Danger);
			};
			mainWindow.AddControl(dangerBtn);

			// Modal notification (blocks UI)
			var modalBtn = new ButtonControl
			{
				Text = "Modal Notification",
				Margin = new Margin { Top = 1, Left = 1 }
			};
			modalBtn.Click += (_, _) =>
			{
				windowSystem.NotificationStateService.ShowNotification(
					"Confirm",
					"This is modal - dismiss to continue.",
					NotificationSeverity.Warning,
					blockUi: true,
					timeout: null);
			};
			mainWindow.AddControl(modalBtn);

			// No-timeout notification
			var persistentBtn = new ButtonControl
			{
				Text = "Persistent (no timeout)",
				Margin = new Margin { Left = 1 }
			};
			persistentBtn.Click += (_, _) =>
			{
				windowSystem.NotificationStateService.ShowNotification(
					"Persistent",
					"This won't auto-dismiss.",
					NotificationSeverity.Info,
					timeout: null);
			};
			mainWindow.AddControl(persistentBtn);

			// Multiline notification
			var multilineBtn = new ButtonControl
			{
				Text = "Multiline Message",
				Margin = new Margin { Left = 1 }
			};
			multilineBtn.Click += (_, _) =>
			{
				windowSystem.NotificationStateService.ShowNotification(
					"Details",
					"Line 1: Build started\nLine 2: Compiling sources\nLine 3: Build succeeded",
					NotificationSeverity.Success,
					timeout: null);
			};
			mainWindow.AddControl(multilineBtn);

			// Dismiss all
			var dismissAllBtn = new ButtonControl
			{
				Text = "Dismiss All",
				Margin = new Margin { Top = 1, Left = 1 }
			};
			dismissAllBtn.Click += (_, _) =>
			{
				windowSystem.NotificationStateService.DismissAll();
			};
			mainWindow.AddControl(dismissAllBtn);

			windowSystem.AddWindow(mainWindow);
			windowSystem.SetActiveWindow(mainWindow);

			return windowSystem.Run();
		}
		catch (Exception ex)
		{
			Console.Clear();
			Console.WriteLine($"Fatal error: {ex.Message}");
			Console.WriteLine(ex.StackTrace);
			return 1;
		}
	}
}

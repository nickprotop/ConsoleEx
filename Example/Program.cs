// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using ConsoleEx.Themes;
using ConsoleEx.Services.NotificationsService;

namespace ConsoleEx.Example
{
	internal class Program
	{
		private static void HandleException(Exception ex)
		{
			Console.Clear();
			AnsiConsole.WriteException(ex);
			Console.WriteLine(string.Empty);
			Console.CursorVisible = true;
		}

		private static ConsoleWindowSystem InitializeConsoleWindowSystem()
		{
			return new ConsoleWindowSystem
			{
				TopStatus = "ConsoleEx example application",
				BottomStatus = "Ctrl-Q Quit",
				RenderMode = RenderMode.Direct,
				Theme = new Theme
				{
					DesktopBackroundChar = '.',
					DesktopBackgroundColor = Color.Black,
					DesktopForegroundColor = Color.Grey,
				}
			};
		}

		private static void LogMessages(LogWindow logWindow)
		{
			for (var i = 0; i < 30; i++)
			{
				logWindow.AddLog($"{DateTime.Now:g}: Message [blue]{i}[/] from main thread. Output status: [yellow]{i * i}[/] from thread");
				Thread.Sleep(500);
			}
		}

		private static int Main(string[] args)
		{
			var consoleWindowSystem = InitializeConsoleWindowSystem();

			var logWindow = new LogWindow(consoleWindowSystem);
			var systemInfoWindow = new SystemInfoWindow(consoleWindowSystem);
			var userInfoWindow = new UserInfoWindow(consoleWindowSystem);
			var clockWindow = new ClockWindow(consoleWindowSystem);

			try
			{
				int exitCode = RunConsoleWindowSystem(consoleWindowSystem, logWindow);

				return exitCode;
			}
			catch (Exception ex)
			{
				HandleException(ex);
				return 1;
			}
		}

		private static int RunConsoleWindowSystem(ConsoleWindowSystem consoleWindowSystem, LogWindow logWindow)
		{
			bool quit = false;
			int exitCode = 0;

			consoleWindowSystem.SetActiveWindow(logWindow.Window);

			Task.Run(() =>
			{
				exitCode = consoleWindowSystem.Run().exitCode;
				quit = true;
			});

			ShowWelcomeNotification(consoleWindowSystem);

			Task.Run(() => LogMessages(logWindow));

			while (!quit) { }

			return exitCode;
		}

		private static void ShowWelcomeNotification(ConsoleWindowSystem consoleWindowSystem)
		{
			Notifications.ShowNotification(
				consoleWindowSystem,
				"Notification",
				"Welcome to ConsoleEx example application\nPress Ctrl-Q to quit",
				NotificationSeverity.Info,
				true,
				0);
		}
	}
}
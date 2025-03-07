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
using ConsoleEx.Drivers;

namespace ConsoleEx.Example
{
	internal class Program
	{
		private static LogWindow? _logWindow;
		private Window _commandWindow;

		private static void HandleException(Exception ex)
		{
			Console.Clear();
			AnsiConsole.WriteException(ex);
			Console.WriteLine(string.Empty);
			Console.CursorVisible = true;
		}

		private static ConsoleWindowSystem InitializeConsoleWindowSystem()
		{
			return new ConsoleWindowSystem(RenderMode.Buffer)
			{
				TopStatus = "ConsoleEx example application",
				BottomStatus = "Ctrl-Q Quit",
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

			//var commandWindow = new CommandWindow(consoleWindowSystem);
			//consoleWindowSystem.AddWindow(commandWindow.Window);

			//logWindow = new LogWindow(consoleWindowSystem);
			//var systemInfoWindow = new SystemInfoWindow(consoleWindowSystem);
			//var userInfoWindow = new UserInfoWindow(consoleWindowSystem);
			//var clockWindow = new ClockWindow(consoleWindowSystem);

			//var dropDownWindow = new DropDownWindow(consoleWindowSystem);
			//consoleWindowSystem.AddWindow(dropDownWindow.GetWindow());

			//var listViewWindow = new ListViewWindow(consoleWindowSystem);
			//consoleWindowSystem.AddWindow(listViewWindow.GetWindow());

			var fileExplorerWindow = new FileExplorerWindow(consoleWindowSystem);

			try
			{
				int exitCode = RunConsoleWindowSystem(consoleWindowSystem);

				Console.SetCursorPosition(0, 0);
				Console.WriteLine($"Console window system terminated with status: {exitCode}");

				return exitCode;
			}
			catch (Exception ex)
			{
				HandleException(ex);
				return 1;
			}
		}

		private static int RunConsoleWindowSystem(ConsoleWindowSystem consoleWindowSystem)
		{
			bool quit = false;
			int exitCode = 0;

			//consoleWindowSystem.SetActiveWindow(_logWindow.Window);

			Task.Run(() =>
			{
				exitCode = consoleWindowSystem.Run();
				quit = true;
			});

			//ShowWelcomeNotification(consoleWindowSystem);

			//Task.Run(() => LogMessages(_logWindow));

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
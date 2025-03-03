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
using ConsoleEx.Controls;

namespace ConsoleEx.Example
{
	public class MyWindow
	{
		private DropdownControl _countryDropdown;
		private Window _window;
		private ConsoleWindowSystem system;

		public MyWindow(ConsoleWindowSystem system)
		{
			// Create window
			_window = new Window(system) { Title = "Country Selection" };
			_window.Width = 50;
			_window.Height = 20;

			this.system = system;

			// Create title
			_window.AddContent(new MarkupControl(new List<string> { "[bold]Country Selection Form[/]" })
			{
				Alignment = Alignment.Center,
				StickyPosition = StickyPosition.Top
			});
			_window.AddContent(new RuleControl() { StickyPosition = StickyPosition.Top });

			// Create dropdown
			_countryDropdown = new DropdownControl("Select a country:");
			_countryDropdown.AddItem(new DropdownItem("USA"));
			_countryDropdown.AddItem(new DropdownItem("Canada"));
			_countryDropdown.AddItem(new DropdownItem("UK"));
			_countryDropdown.AddItem(new DropdownItem("France"));
			_countryDropdown.AddItem(new DropdownItem("Germany"));
			_countryDropdown.AddItem(new DropdownItem("Japan"));
			_countryDropdown.AddItem(new DropdownItem("Australia"));
			_countryDropdown.SelectedIndex = 0;

			// Add spacing
			_window.AddContent(new MarkupControl(new List<string> { " " }));

			// Add dropdown
			_window.AddContent(_countryDropdown);

			// Add some more spacing
			_window.AddContent(new MarkupControl(new List<string> { " " }));

			// Add action buttons
			var buttonsGrid = new HorizontalGridControl { Alignment = Alignment.Center, StickyPosition = StickyPosition.Bottom };

			var okButton = CreateButton("OK", OnOkClicked);
			var cancelButton = CreateButton("Cancel", OnCancelClicked);

			var okColumn = new ColumnContainer(buttonsGrid);
			okColumn.AddContent(okButton);
			buttonsGrid.AddColumn(okColumn);

			var cancelColumn = new ColumnContainer(buttonsGrid);
			cancelColumn.AddContent(cancelButton);
			buttonsGrid.AddColumn(cancelColumn);

			_window.AddContent(buttonsGrid);
			;

			// Handle selection change
			_countryDropdown.SelectedItemChanged += CountryDropdown_SelectedItemChanged;
		}

		public Window GetWindow() => _window;

		private void CountryDropdown_SelectedItemChanged(object? sender, DropdownItem? item)
		{
			if (item != null)
			{
				// Do something with the selected item
				_window.Title = $"Selected: {item.Text}";
			}
		}

		private ButtonControl CreateButton(string text, Action<object> onClick)
		{
			var button = new ButtonControl { Text = text };
			button.OnClick += onClick;
			return button;
		}

		private void OnCancelClicked(object obj)
		{
			_window.GetConsoleWindowSystem?.CloseWindow(_window);
		}

		private void OnOkClicked(object obj)
		{
			// Process the selected country
			var country = _countryDropdown.SelectedValue;
			_window.GetConsoleWindowSystem?.CloseWindow(_window);
		}
	}

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

			var logWindow = new LogWindow(consoleWindowSystem);
			var systemInfoWindow = new SystemInfoWindow(consoleWindowSystem);
			var userInfoWindow = new UserInfoWindow(consoleWindowSystem);
			//var clockWindow = new ClockWindow(consoleWindowSystem);

			var myWindow = new MyWindow(consoleWindowSystem);
			consoleWindowSystem.AddWindow(myWindow.GetWindow());

			try
			{
				int exitCode = RunConsoleWindowSystem(consoleWindowSystem, logWindow);

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

		private static int RunConsoleWindowSystem(ConsoleWindowSystem consoleWindowSystem, LogWindow logWindow)
		{
			bool quit = false;
			int exitCode = 0;

			consoleWindowSystem.SetActiveWindow(logWindow.Window);

			Task.Run(() =>
			{
				exitCode = consoleWindowSystem.Run();
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
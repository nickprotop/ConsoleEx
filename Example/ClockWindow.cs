// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI;
using Spectre.Console;

namespace SharpConsoleUI.Example
{
	public class ClockWindow
	{
		private FigleControl? _clockContent;
		private Window _window;

		public ClockWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			_window = new Window(consoleWindowSystem, WindowThread)
			{
				Title = "Clock",
				Left = consoleWindowSystem.DesktopDimensions.Width - 60,
				Top = 1,
				Width = 60,
				Height = 10,
				BackgroundColor = Color.Black
			};

			_clockContent = new FigleControl()
			{
				Text = $"{DateTime.Now:HH:mm:ss}",
				Alignment = Alignment.Center,
				Color = Color.Green
			};
			_window.AddContent(_clockContent);

			consoleWindowSystem.AddWindow(_window);
		}

		public Window Window => _window;

		public async void WindowThread(Window window)
		{
			while (true)
			{
				_clockContent?.SetText($"{DateTime.Now:HH:mm:ss}");
				await Task.Delay(1000);
			}
		}
	}
}
using ConsoleEx.Contents;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx
{
	public class ClockWindow
	{
		private Window _window;
		private FigletContent? _clockContent;
		public Window Window => _window;

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

			_clockContent = new FigletContent()
			{
				Text = $"{DateTime.Now:HH:mm:ss}",
				Alignment = Alignment.Center,
				Color = Color.Green
			};
			_window.AddContent(_clockContent);

			consoleWindowSystem.AddWindow(_window);
		}

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

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace ConsoleEx
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			var system = new ConsoleWindowSystem
			{
				TopStatus = "TOP STATUS BAR",
				BottomStatus = "BOTTOM STATUS BAR"
			};

			var window1 = new Window(system, new WindowOptions()
			{
				Title = "Window 1",
				Left = 2,
				Top = 2,
				Width = 40,
				Height = 10
			},
			(window) =>
			{
				window.KeyPressed += (sender, e) =>
				{
					if (e.KeyInfo.Key == ConsoleKey.Escape)
					{
						window.Close();
					}
				};
			});

			system.AddWindow(window1);

			// Example of creating window with markup content and it's own thread and handling user prompt
			var window2 = new Window(system, new WindowOptions()
			{
				Title = "System Info",
				Left = 12,
				Top = 4,
				Width = 40,
				Height = 10
			},
			(window) =>
			{
				MarkupContent systemInfoContent = new MarkupContent(GetSystemInfo(), false);
				window.AddContent(systemInfoContent);

				while(true)
				{
					systemInfoContent.SetMarkup(GetSystemInfo());
					Thread.Sleep(500);
				}
			});
			
			system.AddWindow(window2);

			// Example of creating window with markup content and it's own thread and handling user prompt
			var window3 = new Window(system, new WindowOptions()
			{
				Title = "User",
				Left = 22,
				Top = 6,
				Width = 40,
				Height = 10,
				IsResizable = false
			},
			(window) =>
			{
				window.AddContent(new MarkupContent(new List<string>() { "User Info", " " }, true));

				var ageInfo = new MarkupContent(new List<string>() { " " }, true);

				var namePrompt = new PromptContent("[yellow]Enter[/] [red]your[/] [blue]name[/]: ")
				{
					DisableOnEnter = false
				};
				namePrompt.OnInputChange += (s, e) =>
				{
					window.Title = $"User - {e}";
				};
				window.AddContent(namePrompt);

				var agePrompt = new PromptContent("[yellow]Enter[/] [red]your[/] [blue]age[/]: ", (sender, input) =>
				{
					ageInfo.SetMarkup(new List<string>() { $"[bold]Your age is {input}[/]" });
				})
				{
					DisableOnEnter = false
				};

				window.AddContent(agePrompt);
				window.AddContent(new MarkupContent(new List<string>() { " " }, true));				
				window.AddContent(ageInfo);
			});

			system.AddWindow(window3);

			// Example of creating window with Figlet content and it's own thread
			system.CreateWindow(new WindowOptions()
			{
				Title = "Clock",
				Left = Console.WindowWidth - 60,
				Top = 1,
				Width = 60,
				Height = 10
			},
			(window) =>
			{
				FigletContent figletContent = new FigletContent($"{DateTime.Now:HH:mm:ss}", Color.Red, Justify.Left);
				window.AddContent(figletContent);

				while (true)
				{
					figletContent.SetText($"{DateTime.Now:HH:mm:ss}");
					Thread.Sleep(1000);
				}
			});

			// Example of writing to a window from another thread
			Task.Run(() =>
			{
				for (var i = 0; i < 30; i++)
				{
					window1.AddContent(new MarkupContent(new List<string>() { $"Message [bold blue]{i}[/] from thread-Message [bold blue]{i}[/] from thread" }, true));
					Thread.Sleep(50);
				}
			});

			system.SetActiveWindow(window1);

			system.Run();
		}

		private static List<string> GetSystemInfo()
		{
			var systemInfo = new List<string>();

			// CPU usage
			systemInfo.Add($"[bold yellow]CPU Usage:[/] [bold green]{new Random().Next(0, 100)}%[/]");

			// Memory usage
			systemInfo.Add($"[bold yellow]Available Memory:[/] [bold green]{new Random().Next(0, 8000)} MB[/]");

			// Disk usage
			systemInfo.Add($"[bold yellow]Disk Free Space:[/] [bold green]{new Random().Next(1, 100)}%[/]");

			return systemInfo;
		}
	}
}

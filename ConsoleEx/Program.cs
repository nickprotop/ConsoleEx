// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Contents;
using Spectre.Console;
using ConsoleEx.Themes;

namespace ConsoleEx
{
	internal class Program
	{
		private static List<string> GetSystemInfo()
		{
			var systemInfo = new List<string>();

			// CPU usage
			systemInfo.Add($"[yellow]CPU Usage:[/] [green]{new Random().Next(0, 100)}%[/]");

			// Memory usage
			systemInfo.Add($"[yellow]Available Memory:[/] [green]{new Random().Next(0, 8000)} MB[/]");

			// Disk usage
			systemInfo.Add($"[yellow]Disk Free Space:[/] [green]{new Random().Next(1, 100)}%[/]");

			return systemInfo;
		}

		private static void Main(string[] args)
		{
			var consoleWindowSystem = new ConsoleWindowSystem
			{
				TopStatus = "TOP STATUS BAR",
				BottomStatus = "BOTTOM STATUS BAR",
				RenderMode = RenderMode.Direct,
				Theme = new Theme()
				{
					DesktopBackroundChar = '.',
					DesktopBackgroundColor = Color.Black,
					DesktopForegroundColor = Color.Grey,
				}
			};

			var window1 = new Window(consoleWindowSystem,
			(window) =>
			{
				window.KeyPressed += (sender, e) =>
				{
					if (e.KeyInfo.Key == ConsoleKey.Escape)
					{
						window.Close();
					}
				};
			})
			{
				Title = "Window 1",
				Left = 2,
				Top = 2,
				Width = 40,
				Height = 10
			};

			consoleWindowSystem.AddWindow(window1);

			// Example of creating window with markup content and it's own thread and handling user prompt
			var window2 = new Window(consoleWindowSystem,
			(window) =>
			{
				MarkupContent systemInfoContent = new MarkupContent(GetSystemInfo());
				window.AddContent(systemInfoContent);

				while (true)
				{
					systemInfoContent.SetContent(GetSystemInfo());
					Thread.Sleep(2000);
				}
			})
			{
				Title = "System Info",
				Left = 12,
				Top = 4,
				Width = 40,
				Height = 10
			};

			consoleWindowSystem.AddWindow(window2);

			// Example of creating window with markup content and it's own thread and handling user prompt
			var window3 = new Window(consoleWindowSystem,
			(window) =>
			{
				window.AddContent(new MarkupContent(new List<string>() { "User Info", " " }));

				var ageInfo = new MarkupContent(new List<string>() { " " });

				var namePrompt = new PromptContent()
				{
					Prompt = "[yellow]Enter[/] [red]your[/] [blue]name[/]: ",
					DisableOnEnter = false
				};
				namePrompt.OnInputChange += (s, e) =>
				{
					window.Title = $"User - {e}";
				};
				window.AddContent(namePrompt);

				var agePrompt = new PromptContent()
				{
					Prompt = "[yellow]Enter[/] [red]your[/] [blue]age[/]: ",
					DisableOnEnter = false,
					InputWidth = 10
				};
				agePrompt.OnEnter += (s, e) =>
				{
					ageInfo.SetContent(new List<string>() { $"[bold]Your age is {e}[/]" });
				};

				var closeButton = new ButtonContent()
				{
					Text = "[red]Close[/] window",
					StickyPosition = StickyPosition.Bottom,
					Margin = new Margin() { Top = 1, Left = 1 }
				};
				closeButton.OnClick += (sender) =>
				{
					window.Close();
				};

				var maximizeButton = new ButtonContent()
				{
					Text = "[yellow]Maximize[/] window",
					StickyPosition = StickyPosition.Bottom,
					Margin = new Margin() { Top = 1, Left = 1 }
				};
				maximizeButton.OnClick += (sender) =>
				{
					window.State = WindowState.Maximized;
				};

				window.AddContent(agePrompt);
				window.AddContent(new MarkupContent(new List<string>() { " " }));
				window.AddContent(ageInfo);
				window.AddContent(new MarkupContent(new List<string>() { " " }));

				window.AddContent(new RuleContent()
				{
					Color = Color.Yellow,
					Title = "[cyan]A[/][red]c[/][green]t[/][blue]i[/]o[white]n[/]s",
					TitleAlignment = Justify.Left,
					StickyPosition = StickyPosition.Bottom
				});

				window.AddContent(closeButton);
				window.AddContent(maximizeButton);
			})
			{
				Title = "User",
				Left = 22,
				Top = 6,
				Width = 40,
				Height = 10,
				IsResizable = true
			};

			consoleWindowSystem.AddWindow(window3);

			// Example of creating window with Figlet content and it's own thread
			consoleWindowSystem.AddWindow(new Window(consoleWindowSystem, (window) =>
			{
				FigletContent figletContent = new FigletContent()
				{
					Text = $"{DateTime.Now:HH:mm:ss}",
					Alignment = Alignment.Center,
					Color = Color.Green
				};
				window.AddContent(figletContent);

				while (true)
				{
					figletContent.SetText($"{DateTime.Now:HH:mm:ss}");
					Thread.Sleep(1000);
				}
			})
			{
				Title = "Clock",
				Left = Console.WindowWidth - 70,
				Top = 1,
				Width = 70,
				Height = 10,
				BackgroundColor = Color.Black
			});

			// Example of writing to a window from another thread
			Task.Run(() =>
			{
				for (var i = 0; i < 30; i++)
				{
					window1.AddContent(new MarkupContent(new List<string>() { $"Message [blue]{i}[/] from thread-Message [blue]{i}[/] from thread" }) { Alignment = Alignment.Center });
					Thread.Sleep(50);
				}
			});

			consoleWindowSystem.SetActiveWindow(window1);

			consoleWindowSystem.Run();
		}
	}
}

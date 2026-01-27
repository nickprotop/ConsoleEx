using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Plugins.DeveloperTools;
using Spectre.Console;

namespace StartMenuDemo;

class Program
{
	static async Task<int> Main(string[] args)
	{
		try
		{
			var driver = new NetConsoleDriver(RenderMode.Buffer);

			var options = new ConsoleWindowSystemOptions(
				EnablePerformanceMetrics: false,
				StatusBarOptions: new StatusBarOptions(
					ShowStartButton: true,
					StartButtonLocation: StatusBarLocation.Bottom,
					StartButtonPosition: StartButtonPosition.Left,
					ShowWindowListInMenu: true
				)
			);

			var windowSystem = new ConsoleWindowSystem(driver, options: options);
			windowSystem.TopStatus = "[bold cyan]Start Menu Demo[/] - Press [yellow]Ctrl+M[/] or click [yellow]☰ Start[/] button";
			windowSystem.BottomStatus = "";

			// Graceful shutdown
			Console.CancelKeyPress += (sender, e) =>
			{
				e.Cancel = true;
				windowSystem?.Shutdown(0);
			};

			// Load DeveloperTools plugin (provides windows and actions)
			try
			{
				windowSystem.PluginStateService.LoadPlugin<DeveloperToolsPlugin>();
			}
			catch (Exception ex)
			{
				AnsiConsole.MarkupLine($"[yellow]Note: DeveloperTools plugin not available: {ex.Message}[/]");
			}

			// Register user actions - File category
			windowSystem.RegisterStartMenuAction("New Document", () =>
			{
				var window = new Window(windowSystem)
				{
					Title = "New Document",
					Width = 60,
					Height = 20,
					Left = 10,
					Top = 5
				};
				window.AddControl(new MarkupControl(new List<string>
				{
					"[green]New document created![/]",
					"",
					"This demonstrates a user-registered action.",
					"Actions can be organized into categories."
				}));
				windowSystem.AddWindow(window);
				windowSystem.SetActiveWindow(window);
			}, category: "File", order: 10);

			windowSystem.RegisterStartMenuAction("Open File", () =>
			{
				var window = new Window(windowSystem)
				{
					Title = "Open File",
					Width = 60,
					Height = 20,
					Left = 15,
					Top = 7
				};
				window.AddControl(new MarkupControl(new List<string>
				{
					"[cyan]Open file dialog[/]",
					"",
					"In a real application, this would show a file picker."
				}));
				windowSystem.AddWindow(window);
				windowSystem.SetActiveWindow(window);
			}, category: "File", order: 20);

			windowSystem.RegisterStartMenuAction("Save File", () =>
			{
				windowSystem.NotificationStateService.ShowNotification(
					"Save",
					"File saved successfully!",
					NotificationSeverity.Success
				);
			}, category: "File", order: 30);

			// Register user actions - Tools category
			windowSystem.RegisterStartMenuAction("Calculator", () =>
			{
				var window = new Window(windowSystem)
				{
					Title = "Calculator",
					Width = 40,
					Height = 15,
					Left = 20,
					Top = 10
				};
				window.AddControl(new MarkupControl(new List<string>
				{
					"[yellow]Calculator Tool[/]",
					"",
					"2 + 2 = 4",
					"",
					"This demonstrates a tool action."
				}));
				windowSystem.AddWindow(window);
				windowSystem.SetActiveWindow(window);
			}, category: "Tools", order: 10);

			// Create main window with instructions
			var mainWindow = new Window(windowSystem)
			{
				Title = "Start Menu Demo - Main Window",
				Width = 90,
				Height = 30,
				Left = 5,
				Top = 3
			};

			mainWindow.AddControl(new MarkupControl(new List<string>
			{
				"[cyan bold]╔═══════════════════════════════════════════════════════════════════════════════════════╗[/]",
				"[cyan bold]║[/]                            [yellow bold]Start Menu System Demo[/]                              [cyan bold]║[/]",
				"[cyan bold]╚═══════════════════════════════════════════════════════════════════════════════════════╝[/]",
				"",
				"[white bold]Welcome to the Start Menu Demo![/]",
				"",
				"This demo showcases the new Windows-like Start menu system with:",
				"",
				"[cyan]System Actions:[/]",
				"  • Theme selection",
				"  • Settings configuration",
				"  • About dialog",
				"  • Performance toggles (metrics, frame rate limiting, FPS)",
				"",
				"[cyan]Plugin Integration:[/]",
				"  • DeveloperTools plugin windows (Debug Console, Log Exporter)",
				"  • Plugin actions (Clear Logs, Export Diagnostics, Toggle Performance Overlay)",
				"",
				"[cyan]User-Defined Actions:[/]",
				"  • File menu (New Document, Open File, Save File)",
				"  • Tools menu (Calculator)",
				"",
				"[cyan]Window Management:[/]",
				"  • Window list moved to Start menu",
				"  • Alt-1 through Alt-9 shortcuts still work",
				"",
				"[white bold]How to Use:[/]",
				"",
				"  [yellow]1. Press Ctrl+M[/] to open the Start menu",
				"  [yellow]2. Click the ☰ Start button[/] in the bottom-left corner",
				"  [yellow]3. Navigate with arrow keys[/]",
				"  [yellow]4. Press Enter[/] to select an item",
				"  [yellow]5. Press Escape[/] to close the menu",
				"",
				"[grey]Try creating multiple windows using the File menu, then access them via[/]",
				"[grey]the Windows category in the Start menu![/]"
			}));

			windowSystem.AddWindow(mainWindow);

			// Create a second window to demonstrate window list
			var window2 = new Window(windowSystem)
			{
				Title = "Secondary Window",
				Width = 50,
				Height = 15,
				Left = 100,
				Top = 5
			};
			window2.AddControl(new MarkupControl(new List<string>
			{
				"[green]Secondary Window[/]",
				"",
				"This window appears in the Start menu's",
				"Windows category.",
				"",
				"Try pressing [yellow]Ctrl+M[/] and navigating to",
				"[cyan]Windows > Secondary Window[/]"
			}));
			windowSystem.AddWindow(window2);

			// Set main window as active
			windowSystem.SetActiveWindow(mainWindow);

			// Run application
			await Task.Run(() => windowSystem.Run());

			return 0;
		}
		catch (Exception ex)
		{
			Console.Clear();
			AnsiConsole.WriteException(ex);
			return 1;
		}
	}
}

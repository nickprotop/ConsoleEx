// -----------------------------------------------------------------------
// SpectreMouseExample - Demonstrates mouse event support for SpectreRenderableControl
// Shows click, double-click, enter, leave, and move events
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace SpectreMouseExample;

/// <summary>
/// Demonstrates comprehensive mouse event handling for Spectre.Console renderables
/// </summary>
internal class Program
{
	private static ConsoleWindowSystem? _windowSystem;
	private static Window? _mainWindow;
	private static MarkupControl? _statusLabel;
	private static int _clickCount = 0;
	private static int _doubleClickCount = 0;
	private static int _enterCount = 0;
	private static int _leaveCount = 0;

	static async Task<int> Main(string[] args)
	{
		try
		{
			// Initialize console window system
			_windowSystem = new ConsoleWindowSystem(
				new NetConsoleDriver(RenderMode.Buffer),
				options: new ConsoleWindowSystemOptions(
					StatusBarOptions: new StatusBarOptions(ShowTaskBar: true)
				))
			{
				TopStatus = "Spectre Renderable Mouse Demo - Press ESC to close window",
			};

			// Setup graceful shutdown
			Console.CancelKeyPress += (sender, e) =>
			{
				e.Cancel = true;
				_windowSystem?.Shutdown(0);
			};

			// Create demo window
			CreateDemoWindow();

			// Run the application
			await Task.Run(() => _windowSystem.Run());

			return 0;
		}
		catch (Exception ex)
		{
			Console.Clear();
			AnsiConsole.WriteException(ex);
			return 1;
		}
	}

	private static void CreateDemoWindow()
	{
		if (_windowSystem == null) return;

		// Create window
		_mainWindow = new WindowBuilder(_windowSystem)
			.WithTitle("SpectreRenderableControl - Mouse Events Demo")
			.WithSize(80, 25)
			.Centered()
			.Build();

		// Header
		_mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[bold cyan]SpectreRenderableControl Mouse Support[/]",
			"[dim]Interactive demo showing all mouse events[/]"
		})
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			StickyPosition = StickyPosition.Top
		});

		_mainWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });

		// Create interactive panel with all mouse events
		var interactivePanel = SpectreRenderableControl.Create()
			.WithRenderable(new Panel(
				"[yellow bold]Click me![/]\n\n" +
				"Try [cyan]clicking[/], [green]double-clicking[/], and [magenta]hovering[/].\n" +
				"Mouse events will be logged below.\n\n" +
				"[dim]Move your mouse around and experiment![/]")
				.Header("[blue]Interactive Panel[/]")
				.Border(BoxBorder.Rounded)
				.BorderColor(Spectre.Console.Color.Blue))
			.WithMargin(2)
			.OnClick((sender, e) =>
			{
				_clickCount++;
				UpdateStatus($"[cyan]Single Click #{_clickCount}[/] at position ({e.Position.X}, {e.Position.Y})");
			})
			.OnDoubleClick((sender, e) =>
			{
				_doubleClickCount++;
				UpdateStatus($"[green bold]Double Click #{_doubleClickCount}![/] at position ({e.Position.X}, {e.Position.Y})");
			})
			.OnMouseEnter((sender, e) =>
			{
				_enterCount++;
				UpdateStatus($"[magenta]Mouse Entered[/] (total: {_enterCount})");
			})
			.OnMouseLeave((sender, e) =>
			{
				_leaveCount++;
				UpdateStatus($"[grey]Mouse Left[/] (total: {_leaveCount})");
			})
			.OnMouseMove((sender, e) =>
			{
				UpdateStatus($"[dim]Mouse position: ({e.Position.X}, {e.Position.Y})[/]");
			})
			.Build();

		_mainWindow.AddControl(interactivePanel);

		// Event counters
		var countersPanel = SpectreRenderableControl.Create()
			.WithRenderable(new Panel(
				"[bold]Event Counters:[/]\n\n" +
				"[cyan]Clicks:[/] 0\n" +
				"[green]Double-Clicks:[/] 0\n" +
				"[magenta]Enter Events:[/] 0\n" +
				"[grey]Leave Events:[/] 0")
				.Header("[yellow]Statistics[/]")
				.Border(BoxBorder.Rounded))
			.WithMargin(2, 0, 2, 0)
			.WithName("CountersPanel")
			.Build();

		_mainWindow.AddControl(countersPanel);

		// Status label
		_statusLabel = new MarkupControl(new List<string>
		{
			"[dim]Waiting for mouse events...[/]"
		})
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Margin(2, 0, 2, 0),
			Name = "StatusLabel"
		};
		_mainWindow.AddControl(_statusLabel);

		// Instructions
		_mainWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom });
		_mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[bold]Controls:[/] [yellow]ESC[/] Close Window | [yellow]R[/] Reset Counters"
		})
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			StickyPosition = StickyPosition.Bottom
		});

		// Handle keyboard
		_mainWindow.KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				_mainWindow?.Close();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.R)
			{
				ResetCounters();
				e.Handled = true;
			}
		};

		_windowSystem.AddWindow(_mainWindow);
	}

	private static void UpdateStatus(string message)
	{
		if (_statusLabel != null)
		{
			_statusLabel.SetContent(new List<string> { $"[bold]Last Event:[/] {message}" });
		}

		// Update counters panel
		var countersPanel = _mainWindow?.FindControl("CountersPanel") as SpectreRenderableControl;
		if (countersPanel != null)
		{
			countersPanel.SetRenderable(new Panel(
				$"[bold]Event Counters:[/]\n\n" +
				$"[cyan]Clicks:[/] {_clickCount}\n" +
				$"[green]Double-Clicks:[/] {_doubleClickCount}\n" +
				$"[magenta]Enter Events:[/] {_enterCount}\n" +
				$"[grey]Leave Events:[/] {_leaveCount}")
				.Header("[yellow]Statistics[/]")
				.Border(BoxBorder.Rounded));
		}
	}

	private static void ResetCounters()
	{
		_clickCount = 0;
		_doubleClickCount = 0;
		_enterCount = 0;
		_leaveCount = 0;
		UpdateStatus("[yellow]Counters reset![/]");
	}
}

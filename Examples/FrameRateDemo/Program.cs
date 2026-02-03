using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Spectre.Console;
using System.Timers;

namespace FrameRateDemo;

class Program
{
	private static MarkupControl? _statsControl;
	private static MarkupControl? _anim1Control;
	private static MarkupControl? _renderStatsControl;
	private static int _rotationState = 0;

	static void Main()
	{
		// Create window system with performance metrics enabled
		var driver = new NetConsoleDriver(RenderMode.Buffer);
		var options = new ConsoleWindowSystemOptions(
			EnablePerformanceMetrics: true,
			EnableFrameRateLimiting: true,
			TargetFPS: 60
		);
		var windowSystem = new ConsoleWindowSystem(driver, options: options);
		windowSystem.TopStatus = "[bold cyan]Frame Rate Demo[/] - Change FPS with 1-5, Toggle limiting with E/D, Toggle metrics with M, Quit with Ctrl+Q";

		// Create control panel window
		var controlWindow = CreateControlPanel(windowSystem);
		windowSystem.AddWindow(controlWindow);

		// Create animation window 1 - Rotating bar
		var animWindow1 = CreateAnimationWindow1(windowSystem);
		windowSystem.AddWindow(animWindow1);

		// Create render stats window
		var renderStatsWindow = CreateRenderStatsWindow(windowSystem);
		windowSystem.AddWindow(renderStatsWindow);

		// Create stats window
		var statsWindow = CreateStatsWindow(windowSystem);
		windowSystem.AddWindow(statsWindow);

		// Start animation timer
		StartAnimationTimer(windowSystem, animWindow1, renderStatsWindow, statsWindow);

		// Handle keyboard input on control window
		controlWindow.KeyPressed += (sender, e) =>
		{
			switch (e.KeyInfo.Key)
			{
				case ConsoleKey.D1:
					windowSystem.Performance.SetTargetFPS(15);
					e.Handled = true;
					break;
				case ConsoleKey.D2:
					windowSystem.Performance.SetTargetFPS(30);
					e.Handled = true;
					break;
				case ConsoleKey.D3:
					windowSystem.Performance.SetTargetFPS(60);
					e.Handled = true;
					break;
				case ConsoleKey.D4:
					windowSystem.Performance.SetTargetFPS(120);
					e.Handled = true;
					break;
				case ConsoleKey.D5:
					windowSystem.Performance.SetTargetFPS(144);
					e.Handled = true;
					break;
				case ConsoleKey.E:
					windowSystem.Performance.SetFrameRateLimiting(true);
					e.Handled = true;
					break;
				case ConsoleKey.D:
					windowSystem.Performance.SetFrameRateLimiting(false);
					e.Handled = true;
					break;
				case ConsoleKey.M:
					var current = windowSystem.Performance.IsPerformanceMetricsEnabled;
					windowSystem.Performance.SetPerformanceMetrics(!current);
					e.Handled = true;
					break;
			}
		};

		windowSystem.Run();
	}

	static Window CreateControlPanel(ConsoleWindowSystem windowSystem)
	{
		var window = new Window(windowSystem)
		{
			Title = "FPS Control Panel",
			Width = 50,
			Height = 22,
			Left = 2,
			Top = 2
		};

		var container = MarkupControl.Create()
			.AddLine("[bold yellow]Frame Rate Target:[/]")
			.AddEmptyLine()
			.AddLine("[white on blue] 1 [/] FPS: 15 (Low Power)")
			.AddLine("[white on blue] 2 [/] FPS: 30 (Balanced)")
			.AddLine("[white on blue] 3 [/] FPS: 60 (Default)")
			.AddLine("[white on blue] 4 [/] FPS: 120 (High Refresh)")
			.AddLine("[white on blue] 5 [/] FPS: 144 (Ultra)")
			.AddEmptyLine()
			.AddLine("[bold yellow]Frame Rate Limiting:[/]")
			.AddEmptyLine()
			.AddLine("[white on green] E [/] Enable Limiting")
			.AddLine("[white on red] D [/] Disable Limiting")
			.AddEmptyLine()
			.AddLine("[bold yellow]Metrics:[/]")
			.AddEmptyLine()
			.AddLine("[white on blue] M [/] Toggle Performance Metrics")
			.AddEmptyLine()
			.AddLine("[dim]Watch the animation windows")
			.AddLine("to see the effect of different")
			.AddLine("FPS settings![/]")
			.WithMargin(2, 1, 2, 1)
			.Build();

		window.AddControl(container);
		return window;
	}

	static Window CreateAnimationWindow1(ConsoleWindowSystem windowSystem)
	{
		var window = new Window(windowSystem)
		{
			Title = "Animation: Rotating Bar",
			Width = 40,
			Height = 12,
			Left = 54,
			Top = 2
		};

		_anim1Control = new MarkupControl(new List<string> { "" })
		{
			Margin = new Margin { Left = 2, Top = 1 }
		};
		window.AddControl(_anim1Control);

		return window;
	}

	static Window CreateRenderStatsWindow(ConsoleWindowSystem windowSystem)
	{
		var window = new Window(windowSystem)
		{
			Title = "Rendering Statistics",
			Width = 40,
			Height = 14,
			Left = 54,
			Top = 15
		};

		_renderStatsControl = new MarkupControl(new List<string> { "" })
		{
			Margin = new Margin { Left = 2, Top = 1 }
		};
		window.AddControl(_renderStatsControl);

		return window;
	}

	static Window CreateStatsWindow(ConsoleWindowSystem windowSystem)
	{
		var window = new Window(windowSystem)
		{
			Title = "Current Settings",
			Width = 50,
			Height = 7,
			Left = 2,
			Top = 25
		};

		_statsControl = new MarkupControl(new List<string> { "" })
		{
			Margin = new Margin { Left = 2, Top = 1 }
		};
		window.AddControl(_statsControl);

		return window;
	}

	static void StartAnimationTimer(ConsoleWindowSystem windowSystem, Window animWindow1, Window renderStatsWindow, Window statsWindow)
	{
		var timer = new System.Timers.Timer(50); // Update every 50ms
		timer.Elapsed += (sender, e) =>
		{
			_rotationState = (_rotationState + 1) % 8;

			// Update rotating bar animation using SetContent
			if (_anim1Control != null)
			{
				_anim1Control.SetContent(GetRotatingBarLines(_rotationState));
			}

			// Update rendering statistics using SetContent
			if (_renderStatsControl != null)
			{
				var actualFps = windowSystem.Performance.CurrentFPS;
				var frameTime = windowSystem.Performance.CurrentFrameTimeMs;
				var dirtyChars = windowSystem.Performance.CurrentDirtyChars;
				var targetFps = windowSystem.Performance.TargetFPS;

				_renderStatsControl.SetContent(new List<string>
				{
					$"[bold yellow]Actual FPS:[/] [cyan]{actualFps:F1}[/]",
					$"[bold yellow]Frame Time:[/] [cyan]{frameTime:F1}ms[/]",
					$"[bold yellow]Dirty Chars:[/] [cyan]{dirtyChars}[/]",
					"",
					$"[bold yellow]Target FPS:[/] {targetFps}",
					"",
					"[dim]Actual FPS shows real rendering[/]",
					"[dim]performance. Should match target[/]",
					"[dim]when limiting is enabled.[/]"
				});
			}

			// Update stats window using SetContent
			if (_statsControl != null)
			{
				var targetFps = windowSystem.Performance.TargetFPS;
				var limitingEnabled = windowSystem.Performance.IsFrameRateLimitingEnabled;
				var metricsEnabled = windowSystem.Performance.IsPerformanceMetricsEnabled;

				_statsControl.SetContent(new List<string>
				{
					$"[bold]Target FPS:[/] [yellow]{targetFps}[/]",
					$"[bold]Frame Limiting:[/] {(limitingEnabled ? "[green]Enabled[/]" : "[red]Disabled[/]")}",
					$"[bold]Perf Metrics:[/] {(metricsEnabled ? "[green]Enabled[/]" : "[red]Disabled[/]")}"
				});
			}
		};

		timer.Start();
	}

	static List<string> GetRotatingBarLines(int state)
	{
		var bars = new[] { "│", "/", "─", "\\", "│", "/", "─", "\\" };
		var bar = $"[bold cyan]{bars[state]}[/]";
		var line = string.Concat(Enumerable.Repeat(bar + " ", 15));

		return new List<string>
		{
			line,
			"",
			$"[bold yellow]State:[/] {state}/8",
			$"[bold yellow]Pattern:[/] {bar}",
			"",
			"[dim]This rotates continuously to[/]",
			"[dim]show the rendering speed.[/]"
		};
	}
}

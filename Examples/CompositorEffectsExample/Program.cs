using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Spectre.Console;

namespace CompositorEffectsExample;

class Program
{
	static void Main(string[] args)
	{
		// Use ClassicTheme for blue modals matching window colors
		var windowSystem = new ConsoleWindowSystem(
			new SharpConsoleUI.Drivers.NetConsoleDriver(SharpConsoleUI.Drivers.RenderMode.Buffer),
			new ClassicTheme());

		// Create main menu window
		var mainWindow = new Window(windowSystem)
		{
			Title = "Compositor Effects - Main Menu",
			Width = 75,
			Height = 28
		};

		// Center the window
		mainWindow.Left = (Console.WindowWidth - mainWindow.Width) / 2;
		mainWindow.Top = (Console.WindowHeight - mainWindow.Height) / 2;

		// Add title
		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[bold yellow]╔════════════════════════════════════════════════════════╗[/]",
			"[bold yellow]║      COMPOSITOR EFFECTS DEMONSTRATION                  ║[/]",
			"[bold yellow]║      CharacterBuffer Manipulation Examples             ║[/]",
			"[bold yellow]╚════════════════════════════════════════════════════════╝[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Description
		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[white]These examples demonstrate the new compositor-style buffer[/]",
			"[white]manipulation capabilities using PostBufferPaint event and[/]",
			"[white]BufferSnapshot API.[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Button 1: Fade-In Effect
		var fadeButton = new ButtonControl
		{
			Text = "► 1. Fade-In Transition Effect",
			Width = 65
		};
		fadeButton.Click += (sender, e) =>
		{
			var window = new FadeInWindow(windowSystem);
			window.Left = (Console.WindowWidth - window.Width) / 2;
			window.Top = (Console.WindowHeight - window.Height) / 2;
			windowSystem.AddWindow(window);
		};
		mainWindow.AddControl(fadeButton);

		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]   Demonstrates smooth color interpolation from black to full color[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Button 2: Blur Effect
		var blurButton = new ButtonControl
		{
			Text = "► 2. Blur Post-Processing Effect",
			Width = 65
		};
		blurButton.Click += (sender, e) =>
		{
			var window = new ModalBlurWindow(windowSystem);
			window.Left = (Console.WindowWidth - window.Width) / 2;
			window.Top = (Console.WindowHeight - window.Height) / 2;
			windowSystem.AddWindow(window);
		};
		mainWindow.AddControl(blurButton);

		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]   Demonstrates box blur algorithm applied to rendered content[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Button 3: Screenshot
		var screenshotButton = new ButtonControl
		{
			Text = "► 3. Screenshot Capture Demo",
			Width = 65
		};
		screenshotButton.Click += (sender, e) =>
		{
			var window = new ScreenshotWindow(windowSystem);
			window.Left = (Console.WindowWidth - window.Width) / 2;
			window.Top = (Console.WindowHeight - window.Height) / 2;
			windowSystem.AddWindow(window);
		};
		mainWindow.AddControl(screenshotButton);

		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]   Demonstrates BufferSnapshot API for capturing window state[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Button 4: Fractal Explorer
		var fractalButton = new ButtonControl
		{
			Text = "► 4. Animated Fractal Explorer",
			Width = 65
		};
		fractalButton.Click += (sender, e) =>
		{
			var window = new FractalWindow(windowSystem);
			window.Left = (Console.WindowWidth - window.Width) / 2;
			window.Top = (Console.WindowHeight - window.Height) / 2;
			windowSystem.AddWindow(window);
		};
		mainWindow.AddControl(fractalButton);

		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]   Real-time Mandelbrot/Julia fractal with color animation[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Button 5: Open All
		var openAllButton = new ButtonControl
		{
			Text = "► 5. Open All Examples (Cascading)",
			Width = 65
		};
		openAllButton.Click += (sender, e) =>
		{
			OpenAllExamples(windowSystem);
		};
		mainWindow.AddControl(openAllButton);

		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]   Opens all examples in a cascading layout[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Quit button
		var quitButton = new ButtonControl
		{
			Text = "► Q. Quit Application",
			Width = 65
		};
		quitButton.Click += (sender, e) =>
		{
			windowSystem.Shutdown();
		};
		mainWindow.AddControl(quitButton);

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Instructions
		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]Press number keys 1-5 to launch demos | Tab/arrows to navigate[/]",
			"[dim]Enter or click to activate button | Press Q to quit | Esc closes windows[/]"
		}));

		// Keyboard shortcuts
		mainWindow.KeyPressed += (sender, e) =>
		{
			switch (e.KeyInfo.KeyChar)
			{
				case '1':
					var fadeWindow = new FadeInWindow(windowSystem);
					fadeWindow.Left = (Console.WindowWidth - fadeWindow.Width) / 2;
					fadeWindow.Top = (Console.WindowHeight - fadeWindow.Height) / 2;
					windowSystem.AddWindow(fadeWindow);
					e.Handled = true;
					break;
				case '2':
					var blurWindow = new ModalBlurWindow(windowSystem);
					blurWindow.Left = (Console.WindowWidth - blurWindow.Width) / 2;
					blurWindow.Top = (Console.WindowHeight - blurWindow.Height) / 2;
					windowSystem.AddWindow(blurWindow);
					e.Handled = true;
					break;
				case '3':
					var screenshotWindow = new ScreenshotWindow(windowSystem);
					screenshotWindow.Left = (Console.WindowWidth - screenshotWindow.Width) / 2;
					screenshotWindow.Top = (Console.WindowHeight - screenshotWindow.Height) / 2;
					windowSystem.AddWindow(screenshotWindow);
					e.Handled = true;
					break;
				case '4':
					var fractalWin = new FractalWindow(windowSystem);
					fractalWin.Left = (Console.WindowWidth - fractalWin.Width) / 2;
					fractalWin.Top = (Console.WindowHeight - fractalWin.Height) / 2;
					windowSystem.AddWindow(fractalWin);
					e.Handled = true;
					break;
				case '5':
					OpenAllExamples(windowSystem);
					e.Handled = true;
					break;
				case 'q':
				case 'Q':
					windowSystem.Shutdown();
					e.Handled = true;
					break;
			}
		};

		windowSystem.AddWindow(mainWindow);
		windowSystem.Run();
	}

	static void OpenAllExamples(ConsoleWindowSystem windowSystem)
	{
		// Open all examples in a cascading layout
		var fadeWindow = new FadeInWindow(windowSystem);
		fadeWindow.Left = 5;
		fadeWindow.Top = 3;
		windowSystem.AddWindow(fadeWindow);

		var blurWindow = new ModalBlurWindow(windowSystem);
		blurWindow.Left = 10;
		blurWindow.Top = 6;
		windowSystem.AddWindow(blurWindow);

		var screenshotWindow = new ScreenshotWindow(windowSystem);
		screenshotWindow.Left = 15;
		screenshotWindow.Top = 9;
		windowSystem.AddWindow(screenshotWindow);
	}
}

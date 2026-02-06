using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Spectre.Console;

namespace FigleShowcaseExample;

class Program
{
	static void Main(string[] args)
	{
		var windowSystem = new ConsoleWindowSystem(
			new SharpConsoleUI.Drivers.NetConsoleDriver(SharpConsoleUI.Drivers.RenderMode.Buffer),
			new ClassicTheme());

		// Create main menu window
		var mainWindow = new Window(windowSystem)
		{
			Title = "FIGlet Text Showcase - Main Menu",
			Width = 75,
			Height = 28
		};

		// Center the window
		mainWindow.Left = (Console.WindowWidth - mainWindow.Width) / 2;
		mainWindow.Top = (Console.WindowHeight - mainWindow.Height) / 2;

		// Add title
		var titleMarkup = new MarkupControl(new List<string>
		{
			"[bold yellow]╔═══════════════════════════════════════════════════════╗[/]",
			"[bold yellow]║         FIGLE CONTROL SHOWCASE                        ║[/]",
			"[bold yellow]║         ASCII Art Text Features                       ║[/]",
			"[bold yellow]╚═══════════════════════════════════════════════════════╝[/]"
		});
		mainWindow.AddControl(titleMarkup);

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Description
		var descMarkup = new MarkupControl(new List<string>
		{
			"[white]Demonstrates FIGlet ASCII art text with:[/]",
			"[white]• Multiple font sizes (Small, Default, Large)[/]",
			"[white]• Shadow effects (Drop shadow, Outline, 3D extrusion)[/]",
			"[white]• Text alignment options[/]",
			"[white]• Animated transitions[/]"
		});
		mainWindow.AddControl(descMarkup);

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Button 1: Font Sizes
		var sizesButton = new ButtonControl
		{
			Text = "► 1. Font Sizes (Small, Default, Large)",
			Width = 65
		};
		sizesButton.Click += (sender, e) =>
		{
			var window = new FontSizesWindow(windowSystem);
			windowSystem.AddWindow(window);
		};
		mainWindow.AddControl(sizesButton);

		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]   Compare different FIGlet font sizes[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Button 2: Shadow Effects
		var shadowButton = new ButtonControl
		{
			Text = "► 2. Shadow Effects (Drop, Outline, 3D)",
			Width = 65
		};
		shadowButton.Click += (sender, e) =>
		{
			var window = new ShadowEffectsWindow(windowSystem);
			windowSystem.AddWindow(window);
		};
		mainWindow.AddControl(shadowButton);

		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]   See drop shadows, outlines, and 3D extrusion[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Button 3: Alignments
		var alignButton = new ButtonControl
		{
			Text = "► 3. Text Alignment Demo",
			Width = 65
		};
		alignButton.Click += (sender, e) =>
		{
			var window = new AlignmentWindow(windowSystem);
			windowSystem.AddWindow(window);
		};
		mainWindow.AddControl(alignButton);

		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]   Left, Center, and Right alignment[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Button 4: Animated Rainbow
		var rainbowButton = new ButtonControl
		{
			Text = "► 4. Animated Rainbow Colors",
			Width = 65
		};
		rainbowButton.Click += (sender, e) =>
		{
			var window = new RainbowAnimationWindow(windowSystem);
			windowSystem.AddWindow(window);
		};
		mainWindow.AddControl(rainbowButton);

		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]   Color cycling animation with FIGlet text[/]"
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
		var instructionMarkup = new MarkupControl(new List<string>
		{
			"[dim]Press number keys 1-4 to launch demos | Tab/arrows to navigate[/]",
			"[dim]Enter or click to activate button | Press Q to quit | Esc closes windows[/]"
		});
		mainWindow.AddControl(instructionMarkup);

		// Keyboard shortcuts
		mainWindow.KeyPressed += (sender, e) =>
		{
			switch (e.KeyInfo.KeyChar)
			{
				case '1':
					var sizesWin = new FontSizesWindow(windowSystem);
					windowSystem.AddWindow(sizesWin);
					e.Handled = true;
					break;
				case '2':
					var shadowWin = new ShadowEffectsWindow(windowSystem);
					windowSystem.AddWindow(shadowWin);
					e.Handled = true;
					break;
				case '3':
					var alignWin = new AlignmentWindow(windowSystem);
					windowSystem.AddWindow(alignWin);
					e.Handled = true;
					break;
				case '4':
					var rainbowWin = new RainbowAnimationWindow(windowSystem);
					windowSystem.AddWindow(rainbowWin);
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
}

// Window 1: Font Sizes Showcase
class FontSizesWindow : Window
{
	public FontSizesWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "FIGlet Font Sizes";
		Width = 70;
		Height = 25;
		Left = (Console.WindowWidth - Width) / 2;
		Top = (Console.WindowHeight - Height) / 2;

		// Header
		AddControl(new MarkupControl(new List<string>
		{
			"[bold cyan]Font Size Comparison[/]",
			""
		}));

		// Small font
		AddControl(new MarkupControl(new List<string> { "[yellow]Small Font:[/]" }));
		var smallFigle = new FigleControl
		{
			Text = "SMALL",
			Size = FigletSize.Small,
			Color = Spectre.Console.Color.Green
		};
		AddControl(smallFigle);

		AddControl(new MarkupControl(new List<string> { "" }));

		// Default font
		AddControl(new MarkupControl(new List<string> { "[yellow]Default Font (Standard):[/]" }));
		var defaultFigle = new FigleControl
		{
			Text = "DEFAULT",
			Size = FigletSize.Default,
			Color = Spectre.Console.Color.Blue
		};
		AddControl(defaultFigle);

		AddControl(new MarkupControl(new List<string> { "" }));

		// Large font
		AddControl(new MarkupControl(new List<string> { "[yellow]Large Font:[/]" }));
		var largeFigle = new FigleControl
		{
			Text = "LARGE",
			Size = FigletSize.Large,
			Color = Spectre.Console.Color.Red
		};
		AddControl(largeFigle);

		AddControl(new MarkupControl(new List<string> { "", "[dim]Press ESC to close[/]" }));
	}
}

// Window 2: Shadow Effects Showcase
class ShadowEffectsWindow : Window
{
	public ShadowEffectsWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "FIGlet Shadow Effects";
		Width = 70;
		Height = 32;
		Left = (Console.WindowWidth - Width) / 2;
		Top = (Console.WindowHeight - Height) / 2;

		// Header
		AddControl(new MarkupControl(new List<string>
		{
			"[bold cyan]Shadow & 3D Effects[/]",
			""
		}));

		// No shadow
		AddControl(new MarkupControl(new List<string> { "[yellow]No Shadow:[/]" }));
		var noShadow = new FigleControl
		{
			Text = "PLAIN",
			Size = FigletSize.Small,
			Color = Spectre.Console.Color.White,
			ShadowStyle = ShadowStyle.None
		};
		AddControl(noShadow);

		AddControl(new MarkupControl(new List<string> { "" }));

		// Drop shadow
		AddControl(new MarkupControl(new List<string> { "[yellow]Drop Shadow:[/]" }));
		var dropShadow = new FigleControl
		{
			Text = "SHADOW",
			Size = FigletSize.Small,
			Color = Spectre.Console.Color.Aqua,
			ShadowStyle = ShadowStyle.DropShadow,
			ShadowOffsetX = 2,
			ShadowOffsetY = 1,
			ShadowColor = Spectre.Console.Color.Grey
		};
		AddControl(dropShadow);

		AddControl(new MarkupControl(new List<string> { "" }));

		// Outline
		AddControl(new MarkupControl(new List<string> { "[yellow]Outline:[/]" }));
		var outline = new FigleControl
		{
			Text = "OUTLINE",
			Size = FigletSize.Small,
			Color = Spectre.Console.Color.Yellow,
			ShadowStyle = ShadowStyle.Outline,
			ShadowColor = Spectre.Console.Color.Blue
		};
		AddControl(outline);

		AddControl(new MarkupControl(new List<string> { "" }));

		// 3D Extrusion
		AddControl(new MarkupControl(new List<string> { "[yellow]3D Extrusion:[/]" }));
		var extrude3D = new FigleControl
		{
			Text = "3D TEXT",
			Size = FigletSize.Small,
			Color = Spectre.Console.Color.Red,
			ShadowStyle = ShadowStyle.Extrude3D,
			ShadowOffsetX = 3,
			ShadowOffsetY = 2,
			ShadowColor = Spectre.Console.Color.Maroon
		};
		AddControl(extrude3D);

		AddControl(new MarkupControl(new List<string> { "", "[dim]Press ESC to close[/]" }));
	}
}

// Window 3: Alignment Showcase
class AlignmentWindow : Window
{
	public AlignmentWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "FIGlet Text Alignment";
		Width = 70;
		Height = 25;
		Left = (Console.WindowWidth - Width) / 2;
		Top = (Console.WindowHeight - Height) / 2;

		// Header
		AddControl(new MarkupControl(new List<string>
		{
			"[bold cyan]Text Alignment Options[/]",
			""
		}));

		// Left aligned
		AddControl(new MarkupControl(new List<string> { "[yellow]Left Aligned:[/]" }));
		var leftAlign = new FigleControl
		{
			Text = "LEFT",
			Size = FigletSize.Small,
			Color = Spectre.Console.Color.Green,
			HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment.Left,
			Width = 65
		};
		AddControl(leftAlign);

		AddControl(new MarkupControl(new List<string> { "" }));

		// Center aligned
		AddControl(new MarkupControl(new List<string> { "[yellow]Center Aligned:[/]" }));
		var centerAlign = new FigleControl
		{
			Text = "CENTER",
			Size = FigletSize.Small,
			Color = Spectre.Console.Color.Blue,
			HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment.Center,
			Width = 65
		};
		AddControl(centerAlign);

		AddControl(new MarkupControl(new List<string> { "" }));

		// Right aligned
		AddControl(new MarkupControl(new List<string> { "[yellow]Right Aligned:[/]" }));
		var rightAlign = new FigleControl
		{
			Text = "RIGHT",
			Size = FigletSize.Small,
			Color = Spectre.Console.Color.Red,
			HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment.Right,
			Width = 65
		};
		AddControl(rightAlign);

		AddControl(new MarkupControl(new List<string> { "", "[dim]Press ESC to close[/]" }));
	}
}

// Window 4: Rainbow Animation
class RainbowAnimationWindow : Window
{
	private FigleControl? _animatedText;
	private System.Timers.Timer? _animationTimer;
	private int _colorIndex = 0;
	private readonly Spectre.Console.Color[] _rainbowColors = new[]
	{
		Spectre.Console.Color.Red,
		Spectre.Console.Color.Orange1,
		Spectre.Console.Color.Yellow,
		Spectre.Console.Color.Green,
		Spectre.Console.Color.Blue,
		Spectre.Console.Color.Purple,
		Spectre.Console.Color.Magenta1
	};

	public RainbowAnimationWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "Animated Rainbow FIGlet";
		Width = 70;
		Height = 18;
		Left = (Console.WindowWidth - Width) / 2;
		Top = (Console.WindowHeight - Height) / 2;

		// Header
		AddControl(new MarkupControl(new List<string>
		{
			"[bold cyan]Animated Color Cycling[/]",
			""
		}));

		// Animated FIGlet text
		_animatedText = new FigleControl
		{
			Text = "RAINBOW",
			Size = FigletSize.Default,
			Color = _rainbowColors[0],
			HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment.Center,
			Width = 65,
			ShadowStyle = ShadowStyle.DropShadow,
			ShadowOffsetX = 2,
			ShadowOffsetY = 1
		};
		AddControl(_animatedText);

		AddControl(new MarkupControl(new List<string> { "", "[dim]Colors cycle automatically | Press ESC to close[/]" }));

		// Start animation timer (using window's thread support)
		_animationTimer = new System.Timers.Timer(300); // Change color every 300ms
		_animationTimer.Elapsed += (sender, e) =>
		{
			_colorIndex = (_colorIndex + 1) % _rainbowColors.Length;
			if (_animatedText != null)
			{
				_animatedText.Color = _rainbowColors[_colorIndex];
			}
		};
		_animationTimer.Start();

		// Clean up timer when window closes
		OnClosing += (sender, e) =>
		{
			_animationTimer?.Stop();
			_animationTimer?.Dispose();
		};
	}
}

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
			"[white]• Custom font loading from files[/]",
			"[white]• Text alignment options[/]",
			"[white]• Animated colors + PostBufferPaint effects[/]"
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

		// Button 2: Alignments
		var alignButton = new ButtonControl
		{
			Text = "► 2. Text Alignment Demo",
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

		// Button 3: Custom Fonts
		var customFontsButton = new ButtonControl
		{
			Text = "► 3. Custom Font Loading",
			Width = 65
		};
		customFontsButton.Click += (sender, e) =>
		{
			var window = new CustomFontsWindow(windowSystem);
			windowSystem.AddWindow(window);
		};
		mainWindow.AddControl(customFontsButton);

		mainWindow.AddControl(new MarkupControl(new List<string>
		{
			"[dim]   Load fonts from .flf files with FontPath[/]"
		}));

		mainWindow.AddControl(new MarkupControl(new List<string> { "" }));

		// Button 4: Animated Rainbow
		var rainbowButton = new ButtonControl
		{
			Text = "► 4. Animated Rainbow + Background",
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
			"[dim]   Color cycling + PostBufferPaint wave animation[/]"
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
					var alignWin = new AlignmentWindow(windowSystem);
					windowSystem.AddWindow(alignWin);
					e.Handled = true;
					break;
				case '3':
					var customWin = new CustomFontsWindow(windowSystem);
					windowSystem.AddWindow(customWin);
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

// Window 2: Alignment Showcase
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

// Window 3: Custom Font Loading
class CustomFontsWindow : Window
{
	public CustomFontsWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "Custom Font Loading Demo";
		Width = 70;
		Height = 28;
		Left = (Console.WindowWidth - Width) / 2;
		Top = (Console.WindowHeight - Height) / 2;

		// Header
		AddControl(new MarkupControl(new List<string>
		{
			"[bold cyan]Loading Custom Fonts from Files[/]",
			"[dim]Demonstrates FontPath property[/]",
			""
		}));

		// Cyberlarge font
		AddControl(new MarkupControl(new List<string> { "[yellow]Cyberlarge Font:[/]" }));
		var cyberFont = new FigleControl
		{
			Text = "CYBER",
			FontPath = "Fonts/cyberlarge.flf",
			Color = Spectre.Console.Color.Green
		};
		AddControl(cyberFont);

		AddControl(new MarkupControl(new List<string> { "" }));

		// Isometric font
		AddControl(new MarkupControl(new List<string> { "[yellow]Isometric1 Font:[/]" }));
		var isoFont = new FigleControl
		{
			Text = "ISO",
			FontPath = "Fonts/isometric1.flf",
			Color = Spectre.Console.Color.Blue
		};
		AddControl(isoFont);

		AddControl(new MarkupControl(new List<string> { "" }));

		// Star Wars font
		AddControl(new MarkupControl(new List<string> { "[yellow]Star Wars Font:[/]" }));
		var swFont = new FigleControl
		{
			Text = "WARS",
			FontPath = "Fonts/starwars.flf",
			Color = Spectre.Console.Color.Yellow
		};
		AddControl(swFont);

		AddControl(new MarkupControl(new List<string> { "", "[dim]Fonts loaded from Fonts/*.flf files | Press ESC to close[/]" }));
	}
}

// Window 4: Rainbow Animation
class RainbowAnimationWindow : Window
{
	private FigleControl? _animatedText;
	private System.Timers.Timer? _animationTimer;
	private int _colorIndex = 0;
	private float _waveOffset = 0;
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
			Width = 65
		};
		AddControl(_animatedText);

		AddControl(new MarkupControl(new List<string> { "", "[dim]Colors cycle + animated background | Press ESC to close[/]" }));

		// Add animated background using PostBufferPaint
		PostBufferPaint += (buffer, dirtyRegion, clipRect) =>
		{
				// Create animated wave pattern in background
				for (int y = 0; y < buffer.Height; y++)
				{
					for (int x = 0; x < buffer.Width; x++)
					{
						var cell = buffer.GetCell(x, y);

						// Only modify background of empty cells
						if (cell.Character == ' ')
						{
							// Create wave pattern
							float wave = (float)Math.Sin((x * 0.2) + (y * 0.3) + _waveOffset) * 0.5f + 0.5f;
							int colorIdx = (int)(wave * _rainbowColors.Length) % _rainbowColors.Length;
							var bgColor = _rainbowColors[colorIdx];

							// Darken the background color
							var darkBg = new Spectre.Console.Color(
								(byte)(bgColor.R * 0.2),
								(byte)(bgColor.G * 0.2),
								(byte)(bgColor.B * 0.2)
							);

							buffer.SetCell(x, y, ' ', cell.Foreground, darkBg);
						}
					}
				}
		};

		// Start animation timer (using window's thread support)
		_animationTimer = new System.Timers.Timer(50); // Update every 50ms for smooth animation
		_animationTimer.Elapsed += (sender, e) =>
		{
			_colorIndex = (_colorIndex + 1) % _rainbowColors.Length;
			_waveOffset += 0.1f;

			if (_animatedText != null)
			{
				_animatedText.Color = _rainbowColors[_colorIndex];
			}

			// Invalidate to trigger redraw with new background
			Invalidate(true);
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

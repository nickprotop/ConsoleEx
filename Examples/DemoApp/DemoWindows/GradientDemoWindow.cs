using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class GradientDemoWindow
{
	#region Constants

	private const int WindowWidth = 80;
	private const int WindowHeight = 38;
	private const int ButtonWidth = 22;
	private const int LeftMargin = 2;
	private const int SectionTopMargin = 1;
	private const int GradientBarLength = 50;

	#endregion

	private static readonly GradientDirection[] Directions =
	{
		GradientDirection.Horizontal,
		GradientDirection.Vertical,
		GradientDirection.DiagonalDown,
		GradientDirection.DiagonalUp
	};

	private static readonly string[] DirectionLabels =
	{
		"Horizontal",
		"Vertical",
		"Diagonal Down",
		"Diagonal Up"
	};

	public static Window Create(ConsoleWindowSystem ws)
	{
		int directionIndex = 1; // Start with Vertical

		var gradient = ColorGradient.FromColors(
			new Color(0, 40, 120),
			new Color(0, 0, 0));

		var directionLabel = Controls.Markup()
			.AddLine($"[dim]Background direction:[/] [bold white]{DirectionLabels[directionIndex]}[/]")
			.WithMargin(LeftMargin, 0, 0, 0)
			.Build();

		Window? window = null;

		var cycleBtn = Controls.Button("Cycle Direction")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) =>
			{
				directionIndex = (directionIndex + 1) % Directions.Length;
				var newGradient = new GradientBackground(gradient, Directions[directionIndex]);
				window!.BackgroundGradient = newGradient;
				directionLabel.SetContent(new List<string>
				{
					$"[dim]Background direction:[/] [bold white]{DirectionLabels[directionIndex]}[/]"
				});
				window.Invalidate(true);
			})
			.Build();
		cycleBtn.Margin = new Margin { Left = LeftMargin };

		// --- Gradient text markup section ---
		var textSection = Controls.Markup()
			.AddLine("[bold white underline]Gradient Text Markup[/]")
			.AddEmptyLine()
			.AddLine("[gradient=spectrum]Rainbow Spectrum Text[/]")
			.AddLine("[gradient=warm]Warm Sunset Gradient[/]")
			.AddLine("[gradient=cool]Cool Ocean Gradient[/]")
			.AddLine("[gradient=red->yellow->green]Custom Color Stops[/]")
			.WithMargin(LeftMargin, SectionTopMargin, 0, 0)
			.Build();

		// --- Decorative gradient bars ---
		string barBlock = new string('\u2588', GradientBarLength);
		var bars = Controls.Markup()
			.AddLine("[bold cyan]Decorative Gradient Bars[/]")
			.AddLine($"[gradient=spectrum]{barBlock}[/]")
			.AddLine($"[gradient=warm]{barBlock}[/]")
			.AddLine($"[gradient=cool]{barBlock}[/]")
			.WithMargin(LeftMargin, SectionTopMargin, 0, 0)
			.Build();

		// --- Rule separator (tests RuleControl on gradient) ---
		var rule = Controls.RuleBuilder()
			.WithTitle("Interactive Controls")
			.TitleCenter()
			.WithColor(Color.Cyan1)
			.WithMargin(LeftMargin, SectionTopMargin, LeftMargin, 0)
			.Build();

		// --- Checkboxes (tests CheckboxControl on gradient) ---
		var checkbox1 = Controls.Checkbox("Enable transparency")
			.Checked(true)
			.WithMargin(LeftMargin, 0, 0, 0)
			.Build();

		var checkbox2 = Controls.Checkbox("Show gradient bars")
			.WithMargin(LeftMargin, 0, 0, 0)
			.Build();

		// --- Progress bar (tests ProgressBarControl on gradient) ---
		var progressBar = Controls.ProgressBar()
			.WithHeader("Download progress")
			.WithPercentage(65)
			.WithFilledColor(Color.Cyan1)
			.WithUnfilledColor(Color.Grey23)
			.Stretch()
			.ShowPercentage()
			.WithMargin(LeftMargin, SectionTopMargin, LeftMargin, 0)
			.Build();

		// --- Separator (tests SeparatorControl / second rule) ---
		var rule2 = Controls.RuleBuilder()
			.WithTitle("Two-Column Layout")
			.TitleCenter()
			.WithColor(Color.Cyan1)
			.WithMargin(LeftMargin, SectionTopMargin, LeftMargin, 0)
			.Build();

		// --- HorizontalGrid with two columns (tests grid + columns on gradient) ---
		var grid = Controls.HorizontalGrid()
			.Column(col => col
				.Add(Controls.Markup()
					.AddLine("[bold yellow]Left Column[/]")
					.AddEmptyLine()
					.AddLine("[dim]This column has no explicit[/]")
					.AddLine("[dim]background \u2014 gradient shows[/]")
					.AddLine("[dim]through from the window.[/]")
					.WithMargin(LeftMargin, 0, 0, 0)
					.Build()))
			.Column(col => col
				.Add(Controls.Markup()
					.AddLine("[bold green]Right Column[/]")
					.AddEmptyLine()
					.AddLine("[dim]Same here \u2014 the gradient[/]")
					.AddLine("[dim]propagates through the[/]")
					.AddLine("[dim]grid and column containers.[/]")
					.WithMargin(LeftMargin, 0, 0, 0)
					.Build()))
			.Build();

		// --- Solid-bg column for contrast (tests gradient blocking) ---
		var solidNote = Controls.Markup()
			.AddLine("[dim italic]Gradient preserved in margins and text. Controls with explicit bg block it.[/]")
			.WithMargin(LeftMargin, SectionTopMargin, 0, 0)
			.Build();

		window = new WindowBuilder(ws)
			.WithTitle("Gradient Demo")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.WithBackgroundGradient(gradient, GradientDirection.Vertical)
			.WithColors(Color.White, Color.Black)
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow(window!);
					e.Handled = true;
				}
			})
			.AddControl(directionLabel)
			.AddControl(cycleBtn)
			.AddControl(textSection)
			.AddControl(bars)
			.AddControl(rule)
			.AddControl(checkbox1)
			.AddControl(checkbox2)
			.AddControl(progressBar)
			.AddControl(rule2)
			.AddControl(grid)
			.AddControl(solidNote)
			.BuildAndShow();

		return window;
	}
}

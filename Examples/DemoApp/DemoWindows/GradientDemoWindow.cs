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

	private const int WindowWidth = 75;
	private const int WindowHeight = 30;
	private const int ButtonWidth = 22;
	private const int ButtonLeftMargin = 2;
	private const int SectionLeftMargin = 2;
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
			.WithMargin(SectionLeftMargin, 0, 0, 0)
			.Build();

		var textSection = Controls.Markup()
			.AddLine("[bold white underline]Gradient Text Markup[/]")
			.AddEmptyLine()
			.AddLine("[gradient=spectrum]Rainbow Spectrum Text[/]")
			.AddEmptyLine()
			.AddLine("[gradient=warm]Warm Sunset Gradient[/]")
			.AddEmptyLine()
			.AddLine("[gradient=cool]Cool Ocean Gradient[/]")
			.AddEmptyLine()
			.AddLine("[gradient=red->yellow->green]Custom Color Stops[/]")
			.AddEmptyLine()
			.AddLine("[gradient=grayscale]Grayscale Gradient Text[/]")
			.WithMargin(SectionLeftMargin, SectionTopMargin, 0, 0)
			.Build();

		var barLabel = Controls.Label("[bold cyan]Decorative Gradient Bars[/]");
		barLabel.Margin = new Margin { Left = SectionLeftMargin, Top = SectionTopMargin };

		string barBlock = new string('\u2588', GradientBarLength);
		var bars = Controls.Markup()
			.AddLine($"[gradient=spectrum]{barBlock}[/]")
			.AddLine($"[gradient=warm]{barBlock}[/]")
			.AddLine($"[gradient=cool]{barBlock}[/]")
			.WithMargin(SectionLeftMargin, 0, 0, 0)
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
		cycleBtn.Margin = new Margin { Left = ButtonLeftMargin, Top = 0 };

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
			.AddControl(barLabel)
			.AddControl(bars)
			.BuildAndShow();

		return window;
	}
}

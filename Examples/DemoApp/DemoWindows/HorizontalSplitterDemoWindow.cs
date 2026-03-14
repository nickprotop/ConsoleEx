using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class HorizontalSplitterDemoWindow
{
	private const int WindowWidth = 90;
	private const int WindowHeight = 30;

	public static Window Create(ConsoleWindowSystem ws)
	{
		// Create a two-column layout showing different horizontal splitter scenarios
		var grid = Controls.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 0, 1, 0)
			// LEFT COLUMN: Two Fill panels with splitter between them
			.Column(col =>
			{
				col.Add(Controls.Markup()
					.AddLine("[bold cyan]Both Fill[/]")
					.AddLine("[dim]Drag the ═══ bar to resize[/]")
					.WithMargin(0, 0, 0, 0).Build());

				var topPanel = Controls.ScrollablePanel()
					.WithBorderStyle(BorderStyle.Rounded)
					.WithHeader("Top Panel")
					.WithBorderColor(Color.Green)
					.WithVerticalAlignment(VerticalAlignment.Fill)
					.WithHeight(10)
					.AddControl(Controls.Markup()
						.AddLine("[green]This panel grows/shrinks[/]")
						.AddLine("")
						.AddLine("Use the splitter bar below")
						.AddLine("to resize this panel.")
						.AddLine("")
						.AddLine("Try keyboard: focus the bar,")
						.AddLine("then Up/Down arrows.")
						.AddLine("Shift+Up/Down for 5-row jumps.")
						.WithMargin(1, 0, 1, 0).Build())
					.Build();
				col.Add(topPanel);

				col.Add(Controls.HorizontalSplitter()
					.Build());

				var bottomPanel = Controls.ScrollablePanel()
					.WithBorderStyle(BorderStyle.Rounded)
					.WithHeader("Bottom Panel")
					.WithBorderColor(Color.Cyan1)
					.WithVerticalAlignment(VerticalAlignment.Fill)
					.AddControl(Controls.Markup()
						.AddLine("[cyan]This panel adjusts too[/]")
						.AddLine("")
						.AddLine("When the top panel grows,")
						.AddLine("this one shrinks, and")
						.AddLine("vice versa.")
						.AddLine("")
						.AddLine("Min height is clamped to 3.")
						.WithMargin(1, 0, 1, 0).Build())
					.Build();
				col.Add(bottomPanel);
			})
			// RIGHT COLUMN: Panel with explicit height + Fill panel
			.Column(col =>
			{
				col.Add(Controls.Markup()
					.AddLine("[bold yellow]Explicit + Fill[/]")
					.AddLine("[dim]Top has explicit Height[/]")
					.WithMargin(0, 0, 0, 0).Build());

				var fixedPanel = Controls.ScrollablePanel()
					.WithBorderStyle(BorderStyle.Rounded)
					.WithHeader("Fixed Height (8)")
					.WithBorderColor(Color.Yellow)
					.WithVerticalAlignment(VerticalAlignment.Fill)
					.WithHeight(8)
					.AddControl(Controls.Markup()
						.AddLine("[yellow]This panel has Height=8[/]")
						.AddLine("")
						.AddLine("Dragging the splitter")
						.AddLine("changes this height.")
						.WithMargin(1, 0, 1, 0).Build())
					.Build();
				col.Add(fixedPanel);

				col.Add(Controls.HorizontalSplitter()
					.WithMinHeights(4, 4)
					.Build());

				var fillPanel = Controls.ScrollablePanel()
					.WithBorderStyle(BorderStyle.Rounded)
					.WithHeader("Fill Panel")
					.WithBorderColor(Color.Orange1)
					.WithVerticalAlignment(VerticalAlignment.Fill)
					.AddControl(Controls.Markup()
						.AddLine("[orange1]This panel uses Fill[/]")
						.AddLine("")
						.AddLine("It takes remaining space.")
						.AddLine("Drag to see it adjust.")
						.WithMargin(1, 0, 1, 0).Build())
					.Build();
				col.Add(fillPanel);
			})
			.Build();

		var gradient = ColorGradient.FromColors(
			new Color(20, 10, 30),
			new Color(10, 25, 20));

		var bottomControl = Controls.ScrollablePanel()
			.WithBorderStyle(BorderStyle.Rounded)
			.WithHeader("Bottom Control")
			.WithBorderColor(Color.Magenta1)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 0, 1, 0)
			.AddControl(Controls.Markup()
				.AddLine("[magenta1]Below the grid[/]")
				.AddLine("")
				.AddLine("This control sits below the HorizontalGrid.")
				.AddLine("The splitter above resizes the grid and this panel.")
				.WithMargin(1, 0, 1, 0).Build())
			.Build();

		var splitter = Controls.HorizontalSplitter().Build();

		return new WindowBuilder(ws)
			.WithTitle("Horizontal Splitter Demo")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.WithBackgroundGradient(gradient, GradientDirection.Vertical)
			.WithForegroundColor(Color.White)
			.AddControl(grid)
			.AddControl(splitter)
			.AddControl(bottomControl)
			.BuildAndShow();
	}
}

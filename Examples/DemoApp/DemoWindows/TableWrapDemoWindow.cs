using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

/// <summary>
/// A table whose Summary column wraps, beside columns that do not.
/// </summary>
/// <remarks>
/// The case this exists for is a narrow identifier next to a long description in a
/// space too narrow for both: without wrapping the description is clipped at the
/// column's edge and cannot be read without widening the window.
/// </remarks>
public static class TableWrapDemoWindow
{
	private const int WindowWidth = 96;
	private const int WindowHeight = 30;
	private const int IdColumnWidth = 5;
	private const int SeverityColumnWidth = 10;
	private const int SummaryColumnWidth = 34;
	private const int ClippedMaxLines = 2;

	public static Window Create(ConsoleWindowSystem ws)
	{
		var issues = BuildIssues();
		int maxLines = 0;

		var header = Controls.Markup(
				"[dim]Wrapping cells | W: toggle wrap | L: cap at 2 lines | ↑↓: Navigate | Esc: Close[/]")
			.StickyTop()
			.Build();

		var statusBar = Controls.Markup("[dim]Summary wraps; ID and Severity sit on the row's first line.[/]")
			.StickyBottom()
			.Build();

		var table = Controls.Table()
			.WithTitle("Issue Queue")
			.AddColumn("ID", TextJustification.Right, IdColumnWidth)
			.AddColumn("Severity", TextJustification.Left, SeverityColumnWidth)
			// The one wrapping column. A width is what makes wrapping useful: an auto-width
			// column sized to its content has nothing to wrap at.
			.AddColumn("Summary", TextJustification.Left, SummaryColumnWidth, null, wrap: true)
			.AddColumn("Owner")
			// Interactive, so the arrow keys move the selection: without it the table is
			// read-only and ignores the keyboard, which is the point of the Up/Down claim below.
			.Interactive()
			.WithSorting()
			.WithColumnResize()
			.Rounded()
			.WithHeaderColors(Color.White, Color.DarkBlue)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithHorizontalAlignment(HorizontalAlignment.Stretch)
			.Build();

		foreach (var issue in issues)
			table.AddRow(issue.Id.ToString(), issue.Severity, issue.Summary, issue.Owner);

		// Centre the short cells, so a one-line value sits in the middle of a four-line row
		// rather than at its top. Set on the table rather than per column: the Summary column
		// fills its row, so there is nowhere to move it to and the default reaches it harmlessly.
		table.CellVerticalAlignment = VerticalAlignment.Center;

		void Describe()
		{
			var summary = table.Columns[2];
			statusBar.SetContent(new List<string>
			{
				summary.Wrap
					? $"[dim]Wrap [green]on[/][dim], MaxLines "
						+ (summary.MaxLines == 0 ? "unlimited" : summary.MaxLines.ToString())
						+ " | W: toggle wrap | L: cap at 2 lines[/]"
					: "[dim]Wrap [yellow]off[/][dim] - the Summary column is clipped. W: toggle wrap[/]",
			});
		}

		Describe();

		var window = new WindowBuilder(ws)
			.WithTitle("Table Cell Wrapping")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.Resizable()
			.AddControls(header, table, statusBar)
			.BuildAndShow();

		// PreviewKeyPressed, not OnKeyPressed: the table has the focus and swallows a plain
		// letter as filter or navigation input, so a window-level handler behind it never runs.
		// The preview sees the key first, which is what a demo toggle needs.
		window.PreviewKeyPressed += (_, e) =>
		{
			switch (e.KeyInfo.Key)
			{
				case ConsoleKey.W:
					table.Columns[2].Wrap = !table.Columns[2].Wrap;
					Describe();
					e.Handled = true;
					break;

				case ConsoleKey.L:
					maxLines = maxLines == 0 ? ClippedMaxLines : 0;
					table.Columns[2].MaxLines = maxLines;
					Describe();
					e.Handled = true;
					break;
			}
		};

		return window;
	}

	private record Issue(int Id, string Severity, string Summary, string Owner);

	private static List<Issue> BuildIssues() => new()
	{
		new(411, "[red]high[/]",
			"Hidden panels kept the status bar dirty forever, so an idle application rendered at the frame cap and burned a core doing nothing visible.",
			"nick"),
		new(412, "[yellow]medium[/]",
			"A spinner in a hidden panel held its animation registered and pinned the whole application at sixty frames a second for a glyph nobody could see.",
			"nick"),
		new(413, "[green]low[/]",
			"Short summary.",
			"derpeit"),
		new(414, "[yellow]medium[/]",
			"The clock element scheduled a repaint every second while its panel was invisible, redrawing a time that was never on screen.",
			"nick"),
		new(415, "[red]high[/]",
			"Draining the UI action queue claimed a desktop repaint for every action, which pinned any application posting work each frame: a progress pump, or a continuation that re-enqueues itself.",
			"derpeit"),
	};
}

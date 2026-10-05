using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

/// <summary>
/// A <see cref="TreeTableControl"/> holding a backlog of work items: epics, features, stories and
/// tasks, sorted among siblings, filtered by the hierarchy, and one epic whose children load on
/// demand, after a delay, the way a remote backlog would.
/// </summary>
public static class TreeTableDemoWindow
{
	private const int WindowWidth = 100;
	private const int WindowHeight = 34;
	private const int OwnerColumnWidth = 15;
	private const int PointsColumnWidth = 4;
	private const int StateColumnWidth = 8;
	private const int LoadDelayMilliseconds = 800;

	private static readonly TreeGuide[] Guides = [TreeGuide.Line, TreeGuide.Ascii, TreeGuide.DoubleLine, TreeGuide.BoldLine];

	public static Window Create(ConsoleWindowSystem ws)
	{
		var tabs = Controls.TabControl()
			.AddTab("Backlog", BuildBacklogTab(ws))
			.Fill()
			.Build();

		return new WindowBuilder(ws)
			.WithTitle("Tree Table")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControl(tabs)
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					if (e.AlreadyHandled)
						return;
					ws.CloseWindow((Window)sender!);
					e.Handled = true;
				}
			})
			.BuildAndShow();
	}

	#region Tab 1 — a backlog built row by row

	private static IWindowControl BuildBacklogTab(ConsoleWindowSystem ws)
	{
		var header = Controls.Markup()
			.AddLine("[bold]A backlog whose rows nest[/]")
			.AddLine("[yellow]Right[/]/[yellow]Left[/] open and close, or step to a child or the parent. [yellow]Space[/] toggles, [yellow]*[/] opens a subtree.")
			.AddLine("Click a header to sort among siblings. [yellow]/[/] filters: a match keeps the rows above it.")
			.AddLine("[dim]Try [/][yellow]radar[/][dim], [/][yellow]State:new[/][dim] or [/][yellow]Owner:otto task[/][dim]. Open [/][yellow]Ideas[/][dim] to load it. Esc closes.[/]")
			.StickyTop()
			.Build();

		var status = Controls.Markup("[dim]Select a row.[/]")
			.StickyBottom()
			.Build();

		var table = Controls.TreeTable()
			.AddColumn("Work item")
			.AddColumn("Owner", TextJustification.Left, OwnerColumnWidth)
			.AddColumn(new TableColumn("Pts", TextJustification.Right, PointsColumnWidth) { CustomComparer = PointsComparer })
			.AddColumn("State", TextJustification.Center, StateColumnWidth)
			.Interactive()
			.WithSorting()
			.WithFiltering()
			.Rounded()
			.WithHeaderColors(Color.White, Color.DarkBlue)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithHorizontalAlignment(HorizontalAlignment.Stretch)
			.WithName("backlog")
			.OnRowExpansionChanging((sender, e) => LoadOnDemand(ws, (TreeTableControl)sender!, e))
			.OnRowExpansionChanged((_, e) =>
				status.SetContent(new List<string>
				{
					$"[dim]{(e.IsExpanded ? "Opened" : "Closed")} {TitleOf(e.Row)}{(e.IsFilteredView ? " in the filtered view" : "")}.[/]"
				}))
			.Build();

		table.SelectedRowItemChanged += (_, row) =>
		{
			if (row is TreeTableRow item)
				status.SetContent(new List<string> { $"[dim]{TitleOf(item)}: depth {item.Depth}, {item.Children.Count} children.[/]" });
		};

		FillBacklog(table);

		var guideButton = Controls.Button($"Guide: {Guides[0]}").Build();
		guideButton.Click += (_, _) =>
		{
			table.Guide = Guides[(Array.IndexOf(Guides, table.Guide) + 1) % Guides.Length];
			guideButton.Text = $"Guide: {table.Guide}";
		};

		var indentButton = Controls.Button("Indent: 2").Build();
		indentButton.Click += (_, _) =>
		{
			table.Indent = table.Indent.Length == 2 ? "    " : "  ";
			indentButton.Text = $"Indent: {table.Indent.Length}";
		};

		var descendantsButton = Controls.Button("Filter shows: matches").Build();
		descendantsButton.Click += (_, _) =>
		{
			table.FilterIncludesDescendants = !table.FilterIncludesDescendants;
			descendantsButton.Text = table.FilterIncludesDescendants ? "Filter shows: subtrees" : "Filter shows: matches";
		};

		var toolbar = Controls.Toolbar()
			.WithSpacing(1)
			.AddButton(guideButton)
			.AddButton(indentButton)
			.AddButton(descendantsButton)
			.AddButton(Controls.Button("Expand all").OnClick((_, _) => table.ExpandAll()))
			.AddButton(Controls.Button("Collapse all").OnClick((_, _) => table.CollapseAll()))
			.StickyTop()
			.Build();

		return Controls.ScrollablePanel()
			.AddControl(header)
			.AddControl(toolbar)
			.AddControl(table)
			.AddControl(status)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
	}

	/// <summary>Epics, features, stories and tasks, built as one batch.</summary>
	private static void FillBacklog(TreeTableControl table)
	{
		table.BatchUpdate(() =>
		{
			var weather = table.AddRootRow("Epic: Weather-proof picnics", "Wanda Widget", "21", Active);
			var radar = weather.AddChild("Feature: Rain radar", "Otto Overflow", "8", Active);
			var nextHour = radar.AddChild("Story: Rain in the next hour", "Otto Overflow", "5", Active);
			nextHour.AddChild("Task: Fetch radar tiles", "Otto Overflow", "2", Done);
			nextHour.AddChild("Task: Draw the cloud overlay", "Pia Pixel", "3", Active);
			radar.AddChild("Story: Warn before the first drop", "Sid Semicolon", "3", New);
			var umbrellas = weather.AddChild("Feature: Umbrella sharing", "Bea Breakpoint", "13", New);
			umbrellas.AddChild("Story: Lend an umbrella", "Bea Breakpoint", "8", New);
			umbrellas.AddChild("Story: Return it, eventually", "Sid Semicolon", "5", New);

			var snacks = table.AddRootRow("Epic: Snacks", "Pia Pixel", "10", Active);
			var basket = snacks.AddChild("Feature: Basket builder", "Wanda Widget", "10", Active);
			var sandwiches = basket.AddChild("Story: Suggest sandwiches", "Wanda Widget", "5", Active);
			sandwiches.AddChild("Task: Rank by crumb factor", "Wanda Widget", "2", New);
			sandwiches.AddChild("Task: Hide the anchovies", "Otto Overflow", "1", Done);
			basket.AddChild("Story: Ant-proof packing list", "Sid Semicolon", "2", Done);

			table.AddRootRow(new TreeTableRow("Epic: Ideas (loads on demand)", "Bea Breakpoint", "?", New)
			{
				IsExpanded = false,
				HasUnrealizedChildren = true,
			});
		});
	}

	/// <summary>
	/// The first time the Ideas epic opens, shows a placeholder at once and fetches the children in
	/// the background, as a remote backlog would.
	/// </summary>
	private static void LoadOnDemand(ConsoleWindowSystem ws, TreeTableControl table, TreeTableRowExpansionChangingEventArgs e)
	{
		if (!e.IsExpanded || !e.Row.HasUnrealizedChildren) return;

		var epic = e.Row;
		epic.HasUnrealizedChildren = false;
		var placeholder = epic.AddChild("[dim]Loading…[/]", "", "", "");

		_ = Task.Run(async () =>
		{
			await Task.Delay(LoadDelayMilliseconds);
			ws.EnqueueOnUIThread(() => table.BatchUpdate(() =>
			{
				epic.RemoveChild(placeholder);
				epic.AddChild("Story: Picnic by drone", "Pia Pixel", "13", New);
				epic.AddChild("Story: Self-folding blanket", "Otto Overflow", "8", New);
				epic.AddChild("Story: Wasp negotiation API", "Sid Semicolon", "21", New);
			}));
		});
	}

	#endregion

	private const string New = "[cyan]New[/]";
	private const string Active = "[yellow]Active[/]";
	private const string Done = "[green]Done[/]";

	/// <summary>Orders points numerically, with an unknown "?" last.</summary>
	private static readonly IComparer<string> PointsComparer = Comparer<string>.Create((x, y) =>
		(int.TryParse(x, out int a) ? a : int.MaxValue).CompareTo(int.TryParse(y, out int b) ? b : int.MaxValue));

	private static string TitleOf(TableRow row) => SharpConsoleUI.Parsing.MarkupParser.Escape(row.Cells[0]);
}

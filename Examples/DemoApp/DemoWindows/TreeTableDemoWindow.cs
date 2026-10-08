using System.Collections.ObjectModel;
using System.ComponentModel;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace DemoApp.DemoWindows;

/// <summary>
/// A <see cref="TreeTableControl"/> holding a backlog of work items: epics, features, stories and
/// tasks, sorted among siblings, filtered by the hierarchy, and one epic whose children load on
/// demand, after a delay, the way a remote backlog would. A second tab binds the same control to
/// view models with BindItems, and changes only the view models.
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
			.AddTab("Bound to view models", BuildBoundTab())
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

	#region Tab 2 — the same control bound to view models

	private const int EventLogLines = 6;

	private static IWindowControl BuildBoundTab()
	{
		var header = Controls.Markup()
			.AddLine("[bold]Rows that follow their view models[/]")
			.AddLine("The table is bound with [yellow]BindItems[/] to ObservableCollections of moving boxes. The buttons")
			.AddLine("change only the view models; the rows follow, keeping the selection and what is open.")
			.StickyTop()
			.Build();

		var house = new MoveVm("House",
			new MoveVm("Kitchen", new MoveVm("Plates"), new MoveVm("Pans"), new MoveVm("Spice rack")),
			new MoveVm("Garage", new MoveVm("Tools", new MoveVm("Hammer"), new MoveVm("Spirit level"))),
			new MoveVm("Attic", new MoveVm("Christmas lights")));
		var roots = new ObservableCollection<MoveVm> { house };

		var log = new List<string>();
		var logView = Controls.Markup("[dim]Changes are logged here.[/]")
			.StickyBottom()
			.Build();

		void Log(string entry)
		{
			log.Add(entry);
			if (log.Count > EventLogLines)
				log.RemoveAt(0);
			logView.SetContent(log.Select(line => $"[dim]{line}[/]").ToList());
		}

		var table = Controls.TreeTable()
			.AddColumn("Box")
			.AddColumn("Packed", TextJustification.Center, StateColumnWidth)
			.Interactive()
			.WithSorting()
			.WithFiltering()
			.Rounded()
			.WithHeaderColors(Color.White, Color.DarkGreen)
			.WithName("boxes")
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithHorizontalAlignment(HorizontalAlignment.Stretch)
			.Build();

		table.BindItems(roots,
			childrenOf: box => box.Children,
			cellsOf: box => [MarkupParser.Escape(box.Name), box.IsPacked ? Packed : Unpacked],
			configure: options =>
			{
				options.IsExpanded = box => box.IsOpen;
				options.IsExpandedChanged = (box, isOpen) => box.IsOpen = isOpen;
			});

		// Subscribed after BindItems, so the binding has written IsOpen back by the time this runs.
		table.RowExpansionChanged += (_, e) =>
			Log($"{(e.IsExpanded ? "Opened" : "Closed")} {((MoveVm)e.Row.Tag!).Name}; IsOpen is now {((MoveVm)e.Row.Tag!).IsOpen}");

		MoveVm? Selected() => table.SelectedRow?.Tag as MoveVm;

		void Act(string label, Action<MoveVm, MoveVm?> change)
		{
			if (Selected() is not { } box) return;

			change(box, house.FindParentOf(box));
			Log(label + " " + box.Name);
		}

		int added = 0;
		var toolbar = Controls.Toolbar()
			.WithSpacing(1)
			.AddButton(Controls.Button("Add box").OnClick((_, _) => Act("Added a box to", (box, _) => box.Children.Add(new MoveVm($"Box {++added}")))))
			.AddButton(Controls.Button("Remove").OnClick((_, _) => Act("Removed", (box, parent) => parent?.Children.Remove(box))))
			.AddButton(Controls.Button("Move up").OnClick((_, _) => Act("Moved up", (box, parent) =>
			{
				int index = parent?.Children.IndexOf(box) ?? -1;
				if (index > 0)
					parent!.Children.Move(index, index - 1);
			})))
			.AddButton(Controls.Button("Pack").OnClick((_, _) => Act("Toggled packing of", (box, _) => box.IsPacked = !box.IsPacked)))
			.AddButton(Controls.Button("Sort contents").OnClick((_, _) => Act("Sorted the contents of", (box, _) =>
				box.Children = new ObservableCollection<MoveVm>(box.Children.OrderBy(child => child.Name)))))
			.StickyTop()
			.Build();

		return Controls.ScrollablePanel()
			.AddControl(header)
			.AddControl(toolbar)
			.AddControl(table)
			.AddControl(logView)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
	}

	/// <summary>A moving box: a name, whether it is packed and open, and the boxes inside it.</summary>
	private sealed class MoveVm : INotifyPropertyChanged
	{
		private bool _isPacked;
		private bool _isOpen = true;
		private ObservableCollection<MoveVm> _children;

		public MoveVm(string name, params MoveVm[] children)
		{
			Name = name;
			_children = new ObservableCollection<MoveVm>(children);
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public string Name { get; }

		public bool IsPacked
		{
			get => _isPacked;
			set => Set(ref _isPacked, value, nameof(IsPacked));
		}

		public bool IsOpen
		{
			get => _isOpen;
			set => Set(ref _isOpen, value, nameof(IsOpen));
		}

		public ObservableCollection<MoveVm> Children
		{
			get => _children;
			set => Set(ref _children, value, nameof(Children));
		}

		/// <summary>The box directly holding <paramref name="box"/>, searching from this one.</summary>
		public MoveVm? FindParentOf(MoveVm box)
		{
			foreach (var child in Children)
			{
				if (ReferenceEquals(child, box))
					return this;
				if (child.FindParentOf(box) is { } parent)
					return parent;
			}
			return null;
		}

		private void Set<TValue>(ref TValue field, TValue value, string name)
		{
			if (EqualityComparer<TValue>.Default.Equals(field, value)) return;
			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}

	#endregion

	private const string New = "[cyan]New[/]";
	private const string Active = "[yellow]Active[/]";
	private const string Done = "[green]Done[/]";
	private const string Packed = "[green]Yes[/]";
	private const string Unpacked = "[dim]No[/]";

	/// <summary>Orders points numerically, with an unknown "?" last.</summary>
	private static readonly IComparer<string> PointsComparer = Comparer<string>.Create((x, y) =>
		(int.TryParse(x, out int a) ? a : int.MaxValue).CompareTo(int.TryParse(y, out int b) ? b : int.MaxValue));

	private static string TitleOf(TableRow row) => MarkupParser.Escape(row.Cells[0]);
}

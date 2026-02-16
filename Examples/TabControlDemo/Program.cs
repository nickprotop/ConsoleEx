// -----------------------------------------------------------------------
// TabControl Demo - Demonstrates the TabControl features
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
windowSystem.StatusBarStateService.TopStatus = "TabControl Demo - Press Ctrl+Tab to switch tabs";
windowSystem.StatusBarStateService.BottomStatus = "Ctrl+Tab: Next Tab | Ctrl+Shift+Tab: Previous Tab | Click tab headers to switch | ESC: Close";

// Create the main demo window
var window = new WindowBuilder(windowSystem)
	.WithTitle("TabControl Demo - Interactive Tab Navigation")
	.WithSize(100, 35)
	.AtPosition(2, 1)
	.WithBorderStyle(BorderStyle.DoubleLine)
	.AddControl(CreateTabControl())
	.Build();

// Handle keyboard shortcuts
window.KeyPressed += (s, e) =>
{
	if (e.KeyInfo.Key == ConsoleKey.Escape)
	{
		windowSystem.Shutdown();
	}
};

windowSystem.AddWindow(window);
windowSystem.Run();

// Create the TabControl with all demo tabs
static IWindowControl CreateTabControl()
{
	var tabControl = new TabControlBuilder()
		.AddTab("Overview", CreateOverviewTab())
		.AddTab("ScrollPanel", CreateScrollPanelTab())
		.AddTab("Interactive", CreateInteractiveTab())
		.AddTab("Data Table", CreateDataTableTab())
		.AddTab("Help", CreateHelpTab())
		.WithHeight(30)
		.Fill()
		.Build();

	return tabControl;
}

// Tab 1: Overview - Introduction to TabControl
static IWindowControl CreateOverviewTab()
{
	var panel = new ScrollablePanelBuilder()
		.AddControl(new MarkupBuilder()
			.AddLine("[bold cyan]Welcome to the TabControl Demo![/]")
			.AddLine("")
			.AddLine("[yellow]What is TabControl?[/]")
			.AddLine("TabControl is a powerful UI component that allows you to organize content into multiple pages")
			.AddLine("(tabs) that users can switch between. Only one tab is visible at a time, making it perfect for")
			.AddLine("organizing complex interfaces.")
			.AddLine("")
			.AddLine("[yellow]Features Demonstrated:[/]")
			.AddLine("• [green]Multiple tabs[/] with different content types")
			.AddLine("• [green]Mouse click[/] navigation on tab headers")
			.AddLine("• [green]Keyboard shortcuts[/] (Ctrl+Tab / Ctrl+Shift+Tab)")
			.AddLine("• [green]ScrollablePanel[/] integration")
			.AddLine("• [green]Interactive controls[/] within tabs")
			.AddLine("• [green]Dynamic content[/] sizing")
			.AddLine("")
			.AddLine("[yellow]How to Navigate:[/]")
			.AddLine("• [bold]Click[/] on tab headers to switch tabs")
			.AddLine("• Press [bold]Ctrl+Tab[/] to go to the next tab")
			.AddLine("• Press [bold]Ctrl+Shift+Tab[/] to go to the previous tab")
			.AddLine("• Use [bold]mouse wheel[/] to scroll within tabs")
			.AddLine("• Press [bold]Tab[/] to navigate between controls")
			.AddLine("")
			.AddLine("[yellow]Try it out![/]")
			.AddLine("Explore the different tabs to see various TabControl features in action.")
			.AddLine("Each tab showcases different content types and interactions.")
			.AddLine("")
			.AddLine("[dim]───────────────────────────────────────────────────────────────────────[/]")
			.AddLine("")
			.AddLine("[bold yellow]Technical Details:[/]")
			.AddLine("The TabControl uses a hybrid layout approach where all tab content remains in the DOM tree")
			.AddLine("but is toggled visible/invisible. This provides:")
			.AddLine("  • Automatic event routing")
			.AddLine("  • State preservation when switching tabs")
			.AddLine("  • Efficient rendering (only active tab painted)")
			.AddLine("  • Seamless integration with other controls")
			.Build())
		.Build();

	return panel;
}

// Tab 2: ScrollablePanel - Demonstrate scrolling content
static IWindowControl CreateScrollPanelTab()
{
	var markup = new MarkupBuilder();

	markup.AddLine("[bold cyan]ScrollablePanel Integration[/]");
	markup.AddLine("");
	markup.AddLine("[yellow]This tab demonstrates how TabControl works seamlessly with ScrollablePanel.[/]");
	markup.AddLine("");

	// Add lots of content to demonstrate scrolling
	for (int i = 1; i <= 50; i++)
	{
		var color = i % 3 == 0 ? "green" : i % 3 == 1 ? "blue" : "yellow";
		markup.AddLine($"[{color}]Line {i,3}:[/] This is scrollable content. " +
		              $"Use mouse wheel or arrow keys to scroll. {new string('═', 20)}");
	}

	return new ScrollablePanelBuilder()
		.AddControl(markup.Build())
		.WithVerticalScroll()
		.Build();
}

// Tab 3: Interactive - Buttons and controls
static IWindowControl CreateInteractiveTab()
{
	var clickCount = 0;

	var content = new MarkupBuilder()
		.AddLine("[bold cyan]Interactive Controls Demo[/]")
		.AddLine("")
		.AddLine("This tab contains interactive elements that maintain their state when")
		.AddLine("you switch to other tabs and come back.")
		.AddLine("")
		.Build();

	var statusLabel = new MarkupBuilder()
		.AddLine("[yellow]Click the button below![/]")
		.Build();

	var button1 = new ButtonBuilder()
		.WithText("Click Me!")
		.Centered()
		.WithMargin(0, 2, 0, 1)
		.OnClick((sender, btn) =>
		{
			clickCount++;
			(statusLabel as MarkupControl)?.SetContent(new List<string>
			{
				$"[green]Button clicked {clickCount} time(s)![/]"
			});
		})
		.Build();

	var button2 = new ButtonBuilder()
		.WithText("Reset Counter")
		.Centered()
		.WithMargin(0, 1, 0, 1)
		.OnClick((sender, btn) =>
		{
			clickCount = 0;
			(statusLabel as MarkupControl)?.SetContent(new List<string>
			{
				"[yellow]Counter reset! Click the button above.[/]"
			});
		})
		.Build();

	var separator = new RuleControl { Title = "Try the buttons" };

	return new ScrollablePanelBuilder()
		.AddControl(content)
		.AddControl(separator)
		.AddControl(statusLabel)
		.AddControl(button1)
		.AddControl(button2)
		.Build();
}

// Tab 4: Data Table - Show table integration
static IWindowControl CreateDataTableTab()
{
	var header = new MarkupBuilder()
		.AddLine("[bold cyan]Data Table Example[/]")
		.AddLine("[dim]Demonstrating TableControl within a tab[/]")
		.Build();

	var table = TableControl.Create()
		.AddColumn("ID", Justify.Right, 8)
		.AddColumn("Name", Justify.Left, 20)
		.AddColumn("Status", Justify.Center, 12)
		.AddColumn("Score", Justify.Right, 10)
		.AddRow("001", "Alice Johnson", "[green]Active[/]", "95.5")
		.AddRow("002", "Bob Smith", "[yellow]Pending[/]", "87.2")
		.AddRow("003", "Charlie Brown", "[green]Active[/]", "92.8")
		.AddRow("004", "Diana Prince", "[red]Inactive[/]", "78.3")
		.AddRow("005", "Eve Anderson", "[green]Active[/]", "98.1")
		.AddRow("006", "Frank Miller", "[yellow]Pending[/]", "84.7")
		.AddRow("007", "Grace Lee", "[green]Active[/]", "91.4")
		.AddRow("008", "Henry Davis", "[green]Active[/]", "89.9")
		.AddRow("009", "Iris Wilson", "[red]Inactive[/]", "76.5")
		.AddRow("010", "Jack Taylor", "[green]Active[/]", "93.2")
		.WithTitle("User Performance Report")
		.Rounded()
		.WithMargin(0, 1, 0, 0)
		.Build();

	var footer = new MarkupBuilder()
		.AddLine("")
		.AddLine("[dim]This table is fully integrated within the TabControl.[/]")
		.AddLine("[dim]Switch to other tabs and back - the table state is preserved.[/]")
		.Build();

	return new ScrollablePanelBuilder()
		.AddControl(header)
		.AddControl(table)
		.AddControl(footer)
		.Build();
}

// Tab 5: Help - Usage information
static IWindowControl CreateHelpTab()
{
	return new ScrollablePanelBuilder()
		.AddControl(new MarkupBuilder()
			.AddLine("[bold cyan]TabControl Usage Guide[/]")
			.AddLine("")
			.AddLine("[yellow]═══ Basic Usage ═══[/]")
			.AddLine("")
			.AddLine("[bold]1. Creating a TabControl:[/]")
			.AddLine("   [green]var tabControl = new TabControlBuilder()[/]")
			.AddLine("       [green].AddTab(\"Tab 1\", content1)[/]")
			.AddLine("       [green].AddTab(\"Tab 2\", content2)[/]")
			.AddLine("       [green].WithHeight(20)[/]")
			.AddLine("       [green].Build();[/]")
			.AddLine("")
			.AddLine("[bold]2. Adding tabs programmatically:[/]")
			.AddLine("   [green]tabControl.AddTab(\"New Tab\", someControl);[/]")
			.AddLine("")
			.AddLine("[bold]3. Switching tabs:[/]")
			.AddLine("   [green]tabControl.ActiveTabIndex = 1;[/]")
			.AddLine("")
			.AddLine("[yellow]═══ Keyboard Shortcuts ═══[/]")
			.AddLine("")
			.AddLine("• [bold]Ctrl+Tab[/]       - Switch to next tab")
			.AddLine("• [bold]Ctrl+Shift+Tab[/] - Switch to previous tab")
			.AddLine("• [bold]Tab[/]            - Navigate between controls")
			.AddLine("• [bold]ESC[/]            - Close window (in this app)")
			.AddLine("")
			.AddLine("[yellow]═══ Mouse Actions ═══[/]")
			.AddLine("")
			.AddLine("• [bold]Click[/] on tab headers to switch tabs")
			.AddLine("• [bold]Scroll wheel[/] to scroll within tab content")
			.AddLine("• [bold]Click[/] on interactive elements (buttons, etc.)")
			.AddLine("")
			.AddLine("[yellow]═══ Builder API Methods ═══[/]")
			.AddLine("")
			.AddLine("[bold]Tab Management:[/]")
			.AddLine("• [green]AddTab(title, content)[/]      - Add a tab")
			.AddLine("• [green]WithActiveTab(index)[/]        - Set initial active tab")
			.AddLine("")
			.AddLine("[bold]Sizing:[/]")
			.AddLine("• [green]WithHeight(height)[/]          - Set explicit height")
			.AddLine("• [green]WithWidth(width)[/]            - Set explicit width")
			.AddLine("• [green]Fill()[/]                      - Fill vertical space")
			.AddLine("")
			.AddLine("[bold]Positioning:[/]")
			.AddLine("• [green]Centered()[/]                  - Center horizontally")
			.AddLine("• [green]WithMargin(l,t,r,b)[/]         - Set margins")
			.AddLine("")
			.AddLine("[bold]Styling:[/]")
			.AddLine("• [green]WithBackgroundColor(color)[/]  - Set background")
			.AddLine("• [green]WithForegroundColor(color)[/]  - Set foreground")
			.AddLine("")
			.AddLine("[yellow]═══ Integration Examples ═══[/]")
			.AddLine("")
			.AddLine("[bold]With ScrollablePanel:[/]")
			.AddLine("  [green]new TabControlBuilder()[/]")
			.AddLine("      [green].AddTab(\"Scrollable\", new ScrollablePanelBuilder()[/]")
			.AddLine("          [green].AddControl(longContent)[/]")
			.AddLine("          [green].Build())[/]")
			.AddLine("      [green].Build();[/]")
			.AddLine("")
			.AddLine("[bold]With Interactive Controls:[/]")
			.AddLine("  [green]new TabControlBuilder()[/]")
			.AddLine("      [green].AddTab(\"Buttons\", column[/]")
			.AddLine("          [green].AddControl(button1)[/]")
			.AddLine("          [green].AddControl(button2))[/]")
			.AddLine("      [green].Build();[/]")
			.AddLine("")
			.AddLine("[bold]With Tables:[/]")
			.AddLine("  [green]new TabControlBuilder()[/]")
			.AddLine("      [green].AddTab(\"Data\", TableControl.Create()[/]")
			.AddLine("          [green].AddColumn(\"Name\")[/]")
			.AddLine("          [green].AddRow(\"Alice\")[/]")
			.AddLine("          [green].Build())[/]")
			.AddLine("      [green].Build();[/]")
			.AddLine("")
			.AddLine("[yellow]═══ Tips & Best Practices ═══[/]")
			.AddLine("")
			.AddLine("• Use descriptive tab titles (keep them short)")
			.AddLine("• Set an explicit height for consistent tab sizing")
			.AddLine("• Tab content can be any IWindowControl")
			.AddLine("• State is preserved when switching tabs")
			.AddLine("• Only the active tab is rendered (efficient)")
			.AddLine("• Tab headers automatically adjust to fit content")
			.AddLine("")
			.AddLine("[dim]───────────────────────────────────────────────────────────────[/]")
			.AddLine("")
			.AddLine("[bold green]For more information, see the SharpConsoleUI documentation![/]")
			.Build())
		.Build();
}

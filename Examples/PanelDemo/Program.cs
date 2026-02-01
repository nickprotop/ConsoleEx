using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer))
{
	TopStatus = "PanelControl Demo - Mouse Event Handling",
	BottomStatus = "Click panels to see events | Mouse wheel bubbles to scrollable parent | ESC: Close"
};

// Status display
var statusControl = new MarkupControl(new List<string> { "[dim]Waiting for interaction...[/]" })
{
	Margin = new Margin(1, 0, 1, 1)
};

void UpdateStatus(string message, string color = "cyan")
{
	statusControl.SetContent(new List<string> { $"[{color}]● {message}[/]" });
}

// Create a panel that handles mouse events
var panel1 = new PanelControl("[cyan]Click me![/]\n\nPanel handles:\n• Click events\n• Double-click\n• Mouse enter/leave/move")
{
	BorderStyle = BorderStyle.Rounded,
	Margin = new Margin(1, 0, 1, 0),
	Width = 35,
	Height = 10
};

// Wire up panel 1 events
panel1.MouseClick += (s, e) => UpdateStatus("Panel 1: Click received (handled, not bubbled)", "yellow");
panel1.MouseDoubleClick += (s, e) => UpdateStatus("Panel 1: Double-click received (handled)", "magenta");
panel1.MouseEnter += (s, e) => UpdateStatus("Panel 1: Mouse entered", "green");
panel1.MouseLeave += (s, e) => UpdateStatus("Panel 1: Mouse left", "red");
panel1.MouseMove += (s, e) => UpdateStatus($"Panel 1: Mouse move at ({e.Position.X}, {e.Position.Y})", "grey");

// Create a second panel
var panel2 = new PanelControl("[yellow]Click me too![/]\n\nScroll events bubble\nto scrollable parents\n(try mouse wheel)")
{
	BorderStyle = BorderStyle.DoubleLine,
	Margin = new Margin(1, 0, 1, 0),
	Width = 35,
	Height = 10
};

// Wire up panel 2 events
panel2.MouseClick += (s, e) => UpdateStatus("Panel 2: Click received (handled, not bubbled)", "yellow");
panel2.MouseDoubleClick += (s, e) => UpdateStatus("Panel 2: Double-click received (handled)", "magenta");
panel2.MouseEnter += (s, e) => UpdateStatus("Panel 2: Mouse entered", "green");
panel2.MouseLeave += (s, e) => UpdateStatus("Panel 2: Mouse left", "red");

// Create window
var window = new WindowBuilder(windowSystem)
	.WithTitle("PanelControl Demo - Event Handling")
	.WithSize(85, 28)
	.AtPosition(2, 1)
	.AddControl(new MarkupBuilder()
		.AddLine("[bold cyan]PanelControl Mouse Event Handling[/]")
		.AddLine("")
		.AddLine("[yellow]Behavior:[/]")
		.AddLine("• Panels [green]handle[/] click, double-click, enter, leave, and move events")
		.AddLine("• Scroll/wheel events [cyan]bubble[/] to scrollable containers")
		.AddLine("")
		.WithMargin(1, 1, 1, 0)
		.Build())
	.AddControl(panel1)
	.AddControl(panel2)
	.AddControl(statusControl)
	.Build();

windowSystem.AddWindow(window);
windowSystem.Run();

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;

var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer))
{
	TopStatus = "TableControl Demo - Press F1-F3 to switch themes",
	BottomStatus = "Read-only table display | F1=ModernGray, F2=Classic, F3=DevDark | ESC: Close"
};

// Create table with sample data
var table = TableControl.Create()
	.AddColumn("ID", Justify.Right, 8)
	.AddColumn("Name", Justify.Left, 20)
	.AddColumn("Status", Justify.Center, 15)
	.AddColumn("Score", Justify.Right, 10)
	.AddRow("1", "Alice Johnson", "[green]Active[/]", "95")
	.AddRow("2", "Bob Smith", "[yellow]Pending[/]", "87")
	.AddRow("3", "Charlie Brown", "[green]Active[/]", "92")
	.AddRow("4", "Diana Prince", "[red]Inactive[/]", "78")
	.AddRow("5", "Eve Adams", "[green]Active[/]", "98")
	.WithTitle("User Status Report")
	.Rounded()
	.WithMargin(1, 0, 1, 0)
	.Build();

// Create window using fluent builder
var window = new WindowBuilder(windowSystem)
	.WithTitle("TableControl Demo - Theme Support")
	.WithSize(70, 20)
	.AtPosition(2, 1)
	.AddControl(new MarkupBuilder()
		.AddLine("[bold cyan]TableControl - Read-Only Tabular Data Display[/]")
		.AddLine("")
		.AddLine("This control wraps Spectre.Console's Table widget with theme support.")
		.AddLine("")
		.AddLine("[yellow]Themes:[/] F1=ModernGray (cyan), F2=Classic (blue/yellow), F3=DevDark (green)")
		.AddLine("")
		.WithMargin(1, 1, 1, 0)
		.Build())
	.AddControl(table)
	.Build();

// Theme switching
window.KeyPressed += (s, e) =>
{
	switch (e.KeyInfo.Key)
	{
		case ConsoleKey.F1:
			windowSystem.Theme = new SharpConsoleUI.Themes.ModernGrayTheme();
			break;

		case ConsoleKey.F2:
			windowSystem.Theme = new SharpConsoleUI.Themes.ClassicTheme();
			break;

		case ConsoleKey.F3:
			var devDarkType = Type.GetType("SharpConsoleUI.Plugins.DeveloperTools.DevDarkTheme, SharpConsoleUI");
			if (devDarkType != null && Activator.CreateInstance(devDarkType) is SharpConsoleUI.Themes.ITheme theme)
			{
				windowSystem.Theme = theme;
			}
			break;
	}
};

windowSystem.AddWindow(window);
windowSystem.Run();

// -----------------------------------------------------------------------
// PluginShowcaseExample - Demonstrates the DeveloperTools plugin usage
//
// This example shows how to:
// - Load the built-in DeveloperTools plugin
// - Switch to the DevDark theme
// - Create the Debug Console window from the plugin
// - Use the LogExporter control from the plugin
// - Access the Diagnostics service from the plugin
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Plugins.DeveloperTools;

// Create window system (testing without status bars to verify bug fix)
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

// Enable all log levels for the demo (default is Warning)
windowSystem.LogService.MinimumLevel = LogLevel.Trace;

// Load the built-in developer tools plugin
windowSystem.LoadPlugin<DeveloperToolsPlugin>();

// Switch to DevDark theme (provided by the plugin)
windowSystem.SwitchTheme("DevDark");

// Create the main window (not closable - this is the main app window)
var mainWindow = new WindowBuilder(windowSystem)
	.WithTitle("Plugin Showcase")
	.WithSize(70, 22)
	.Centered()
	.Closable(false)
	.Build();

// Add header
mainWindow.AddControl(Controls.Header("DeveloperTools Plugin Demo", "green"));
mainWindow.AddControl(Controls.Separator());

// Add description
mainWindow.AddControl(Controls.Markup()
	.AddLine("This example demonstrates the built-in [green]DeveloperTools[/] plugin.")
	.AddLine("")
	.AddLine("The plugin provides:")
	.AddLine("  [cyan1]•[/] [yellow]DevDark[/] theme (currently active)")
	.AddLine("  [cyan1]•[/] [yellow]LogExporter[/] control")
	.AddLine("  [cyan1]•[/] [yellow]DebugConsole[/] window")
	.AddLine("  [cyan1]•[/] [yellow]DiagnosticsService[/]")
	.Build());

mainWindow.AddControl(Controls.Separator());

// Create log level dropdown for changing log level on the fly
var logLevelDropdown = Controls.Dropdown()
	.WithName("LogLevel")
	.AddItem(new DropdownItem("Trace") { Tag = LogLevel.Trace })
	.AddItem(new DropdownItem("Debug") { Tag = LogLevel.Debug })
	.AddItem(new DropdownItem("Info") { Tag = LogLevel.Information })
	.AddItem(new DropdownItem("Warning") { Tag = LogLevel.Warning })
	.AddItem(new DropdownItem("Error") { Tag = LogLevel.Error })
	.AddItem(new DropdownItem("Critical") { Tag = LogLevel.Critical })
	.SelectedIndex(0) // Start at Trace (matching initial MinimumLevel)
	.OnSelectionChanged((sender, e) =>
	{
		if (sender is DropdownControl dropdown && dropdown.SelectedItem?.Tag is LogLevel level)
		{
			windowSystem.LogService.MinimumLevel = level;
			windowSystem.LogService?.LogInfo($"Log level changed to {level}", "Settings");
		}
	})
	.Build();

// Settings toolbar with log level control
var settingsToolbar = Controls.Toolbar()
	.Add(Controls.Label("[grey50]Log Level:[/]"))
	.Add(logLevelDropdown)
	.Build();

mainWindow.AddControl(settingsToolbar);
mainWindow.AddControl(Controls.Separator());

// Helper to create Log Exporter window
Window? logExporterWindow = null;
void OpenLogExporter()
{
	// Check if window already exists and is open
	if (logExporterWindow != null && windowSystem.Windows.Values.Contains(logExporterWindow))
	{
		windowSystem.SetActiveWindow(logExporterWindow);
		return;
	}

	// Create new Log Exporter window
	logExporterWindow = new WindowBuilder(windowSystem)
		.WithTitle("Log Exporter")
		.WithSize(45, 8)
		.AtPosition(2, 2)
		.Build();

	var logExporter = windowSystem.CreatePluginControl("LogExporter");
	if (logExporter != null)
	{
		logExporterWindow.AddControl(logExporter);
	}

	windowSystem.AddWindow(logExporterWindow);
	windowSystem.SetActiveWindow(logExporterWindow);
}

// Create toolbar with buttons
var toolbar = Controls.Toolbar()
	.AddButton("Debug Console", (sender, btn) =>
	{
		// Create debug console window from plugin
		var debugWindow = windowSystem.CreatePluginWindow("DebugConsole");
		if (debugWindow != null)
		{
			windowSystem.AddWindow(debugWindow);
			windowSystem.SetActiveWindow(debugWindow);
		}
	})
	.AddSeparator(1)
	.AddButton("Log Exporter", (sender, btn) =>
	{
		OpenLogExporter();
	})
	.AddSeparator(1)
	.AddButton("Diagnostics", (sender, btn) =>
	{
		// Get diagnostics service from plugin
		var diagnostics = windowSystem.GetService<IDiagnosticsService>();
		if (diagnostics != null)
		{
			// Create a modal window to show diagnostics
			var diagWindow = new WindowBuilder(windowSystem)
				.WithTitle("Diagnostics Report")
				.WithSize(50, 18)
				.Centered()
				.AsModal()
				.Build();

			var report = diagnostics.GetDiagnosticsReport();
			var lines = report.Split('\n');
			var markup = Controls.Markup();
			foreach (var line in lines)
			{
				markup.AddLine(line.Replace("[", "[[").Replace("]", "]]"));
			}
			diagWindow.AddControl(markup.Build());

			var closeBtn = Controls.Button("Close")
				.Centered()
				.StickyBottom()
				.OnClick((s, b) => diagWindow.Close())
				.Build();
			diagWindow.AddControl(closeBtn);

			windowSystem.AddWindow(diagWindow);
			windowSystem.SetActiveWindow(diagWindow);
		}
	})
	.AddSeparator(1)
	.AddButton("Test Log", (sender, btn) =>
	{
		// Log a test message (will appear in Debug Console)
		windowSystem.LogService?.LogInfo("Test message from Plugin Showcase!", "Test");
	})
	.StickyBottom()
	.Build();

mainWindow.AddControl(toolbar);

// Add the main window
windowSystem.AddWindow(mainWindow);
windowSystem.SetActiveWindow(mainWindow);

// Open the Log Exporter window initially
OpenLogExporter();

// Log some initial messages
windowSystem.LogService?.LogInfo("Plugin Showcase started", "App");
windowSystem.LogService?.LogDebug("DevDark theme applied", "Theme");
windowSystem.LogService?.LogInfo("DeveloperTools plugin loaded successfully", "Plugin");

// Run the application
windowSystem.Run();

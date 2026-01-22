// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Plugins.DeveloperTools;

/// <summary>
/// Built-in developer tools plugin providing debugging and diagnostics capabilities.
/// Includes DevDark theme, log exporter control, debug console window, and diagnostics service.
/// </summary>
public class DeveloperToolsPlugin : PluginBase
{
	private DiagnosticsService? _diagnosticsService;
	private ConsoleWindowSystem? _windowSystem;

	/// <inheritdoc />
	public override PluginInfo Info => new(
		"DeveloperTools",
		"1.0.0",
		"ConsoleEx Team",
		"Developer tools: DevDark theme, Log Exporter, Debug Console, Diagnostics Service"
	);

	/// <inheritdoc />
	public override void Initialize(ConsoleWindowSystem windowSystem)
	{
		_windowSystem = windowSystem;

		// Create and configure diagnostics service
		_diagnosticsService = new DiagnosticsService();
		_diagnosticsService.SetWindowSystem(windowSystem);
	}

	/// <inheritdoc />
	public override IReadOnlyList<PluginTheme> GetThemes() => new[]
	{
		new PluginTheme("DevDark", "Dark developer theme with green terminal-inspired accents", new DevDarkTheme())
	};

	/// <inheritdoc />
	public override IReadOnlyList<PluginControl> GetControls() => new[]
	{
		new PluginControl("LogExporter", () =>
		{
			var control = new LogExporterControl();
			if (_windowSystem?.LogService != null)
				control.SetLogService(_windowSystem.LogService);
			return control;
		})
	};

	/// <inheritdoc />
	public override IReadOnlyList<PluginWindow> GetWindows() => new[]
	{
		new PluginWindow("DebugConsole", ws => DebugConsoleWindow.Create(ws))
	};

	/// <inheritdoc />
	public override IReadOnlyList<IPluginService> GetServicePlugins()
	{
		if (_diagnosticsService == null)
			return Array.Empty<IPluginService>();

		return new IPluginService[]
		{
			_diagnosticsService
		};
	}

	/// <inheritdoc />
	public override void Dispose()
	{
		_diagnosticsService = null;
		_windowSystem = null;
	}
}

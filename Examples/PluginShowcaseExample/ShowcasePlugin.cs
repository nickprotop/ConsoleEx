// -----------------------------------------------------------------------
// PluginShowcaseExample - Self-contained showcase plugin
//
// This file recreates, INSIDE the example project, the functionality that
// used to live in the (now removed) SharpConsoleUI.Plugins.DeveloperTools
// namespace. It demonstrates the public plugin SDK end-to-end:
//   - A theme        ("DevDark")
//   - A control      ("LogExporter")
//   - A window       ("DebugConsole")
//   - A service      ("Diagnostics")
//
// Everything here uses ONLY the public plugin SDK
// (PluginBase, PluginServiceBase, PluginTheme/Control/Window, ServiceOperation).
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Plugins;
using SharpConsoleUI.Themes;

namespace PluginShowcaseExample;

/// <summary>
/// Self-contained showcase plugin used by this example. Provides a theme,
/// a control, a window, and a diagnostics service.
/// </summary>
public sealed class ShowcasePlugin : PluginBase
{
	private ConsoleWindowSystem? _windowSystem;

	/// <inheritdoc/>
	public override PluginInfo Info => new(
		Name: "DeveloperTools",
		Version: "1.0.0",
		Author: "PluginShowcaseExample",
		Description: "Example developer-tools plugin: DevDark theme, LogExporter control, DebugConsole window, and a Diagnostics service.");

	/// <inheritdoc/>
	public override void Initialize(ConsoleWindowSystem windowSystem)
	{
		_windowSystem = windowSystem;
	}

	/// <inheritdoc/>
	public override IReadOnlyList<PluginTheme> GetThemes() => new[]
	{
		new PluginTheme("DevDark", "Dark developer theme (example plugin).", new DevDarkTheme())
	};

	/// <inheritdoc/>
	public override IReadOnlyList<PluginControl> GetControls() => new[]
	{
		new PluginControl("LogExporter", () => (IWindowControl)CreateLogExporterControl())
	};

	/// <inheritdoc/>
	public override IReadOnlyList<PluginWindow> GetWindows() => new[]
	{
		new PluginWindow("DebugConsole", CreateDebugConsoleWindow)
	};

	/// <inheritdoc/>
	public override IReadOnlyList<IPluginService> GetServicePlugins() => new[]
	{
		(IPluginService)new DiagnosticsService(_windowSystem)
	};

	/// <summary>
	/// Builds a minimal "log exporter" control. In this didactic example it is just
	/// a MarkupControl that reports the current recent-log count and exposes the idea.
	/// </summary>
	private IWindowControl CreateLogExporterControl()
	{
		int recent = _windowSystem?.LogService?.GetRecentLogs(100).Count ?? 0;
		return new MarkupControl(new List<string>
		{
			"[yellow]Log Exporter[/]",
			"",
			$"Captured log entries: [cyan]{recent}[/]",
			"[grey]Use the Diagnostics service to export a report.[/]"
		});
	}

	/// <summary>
	/// Builds a simple "debug console" window that shows the most recent log entries.
	/// </summary>
	private Window CreateDebugConsoleWindow(ConsoleWindowSystem windowSystem)
	{
		var window = new WindowBuilder(windowSystem)
			.WithTitle("Debug Console")
			.WithSize(70, 18)
			.Centered()
			.Build();

		var lines = new List<string> { "[green]Debug Console[/]", "" };
		var logs = windowSystem.LogService?.GetRecentLogs(50);
		if (logs != null && logs.Count > 0)
		{
			foreach (var entry in logs)
			{
				lines.Add(entry.ToMarkup());
			}
		}
		else
		{
			lines.Add("[grey]No log entries yet.[/]");
		}

		window.AddControl(new MarkupControl(lines));
		return window;
	}
}

/// <summary>
/// Example "DevDark" theme. Reuses the library's ModernGray dark theme as a base
/// and only renames it, keeping this example minimal while still demonstrating
/// that a plugin can contribute a named theme.
/// </summary>
public sealed class DevDarkTheme : ModernGrayTheme
{
	/// <inheritdoc/>
	public override string Name => "DevDark";

	/// <inheritdoc/>
	public override string Description => "Dark developer theme contributed by the example showcase plugin.";
}

/// <summary>
/// Example diagnostics service exposed via the reflection-free IPluginService pattern.
/// Supports "GetDiagnosticsReport" and a parameterized "GetDetailedReport".
/// </summary>
public sealed class DiagnosticsService : PluginServiceBase
{
	private readonly ConsoleWindowSystem? _windowSystem;

	/// <inheritdoc/>
	public override string ServiceName => "Diagnostics";

	/// <inheritdoc/>
	public override string Description => "Provides runtime diagnostics (memory, GC, uptime, window count).";

	/// <summary>
	/// Creates the diagnostics service and registers its operations.
	/// </summary>
	/// <param name="windowSystem">The owning window system (used for window counts).</param>
	public DiagnosticsService(ConsoleWindowSystem? windowSystem)
	{
		_windowSystem = windowSystem;

		RegisterOperation(
			"GetDiagnosticsReport",
			"Returns a full diagnostics report as text.",
			GetDiagnosticsReport);

		RegisterOperation(
			"GetDetailedReport",
			"Returns a customizable diagnostics report.",
			new[]
			{
				new ServiceParameter("includeMemory", typeof(bool), false, true, "Include managed memory usage."),
				new ServiceParameter("includeGC", typeof(bool), false, true, "Include garbage-collection counts."),
				new ServiceParameter("includeUptime", typeof(bool), false, true, "Include process uptime."),
				new ServiceParameter("includeWindows", typeof(bool), false, true, "Include the open-window count.")
			},
			GetDetailedReport,
			typeof(string));
	}

	private string GetDiagnosticsReport()
	{
		return BuildReport(includeMemory: true, includeGC: true, includeUptime: true, includeWindows: true);
	}

	private object? GetDetailedReport(Dictionary<string, object>? parameters)
	{
		bool includeMemory = GetParameter(parameters, "includeMemory", true);
		bool includeGC = GetParameter(parameters, "includeGC", true);
		bool includeUptime = GetParameter(parameters, "includeUptime", true);
		bool includeWindows = GetParameter(parameters, "includeWindows", true);
		return BuildReport(includeMemory, includeGC, includeUptime, includeWindows);
	}

	private string BuildReport(bool includeMemory, bool includeGC, bool includeUptime, bool includeWindows)
	{
		var sb = new StringBuilder();
		sb.AppendLine("=== Diagnostics Report ===");

		if (includeMemory)
		{
			sb.AppendLine($"Managed memory: {GC.GetTotalMemory(false) / 1024} KB");
		}

		if (includeGC)
		{
			sb.AppendLine($"GC collections (gen0/1/2): {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
		}

		if (includeUptime)
		{
			var uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
			sb.AppendLine($"Process uptime: {uptime:hh\\:mm\\:ss}");
		}

		if (includeWindows)
		{
			int windowCount = _windowSystem?.Windows.Count ?? 0;
			sb.AppendLine($"Open windows: {windowCount}");
		}

		return sb.ToString().TrimEnd();
	}
}

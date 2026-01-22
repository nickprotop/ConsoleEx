// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Text;

namespace SharpConsoleUI.Plugins.DeveloperTools;

/// <summary>
/// Diagnostics service providing system performance metrics.
/// Implements IPluginService for reflection-free invocation from external DLLs.
/// </summary>
public class DiagnosticsService : IPluginService
{
	private readonly Stopwatch _uptime;
	private ConsoleWindowSystem? _windowSystem;

	/// <summary>
	/// Initializes a new instance of the <see cref="DiagnosticsService"/> class.
	/// </summary>
	public DiagnosticsService()
	{
		_uptime = Stopwatch.StartNew();
	}

	#region IPluginService Implementation

	/// <inheritdoc />
	public string ServiceName => "Diagnostics";

	/// <inheritdoc />
	public string Description => "System diagnostics and performance monitoring service";

	/// <inheritdoc />
	public IReadOnlyList<ServiceOperation> GetAvailableOperations()
	{
		return new[]
		{
			new ServiceOperation(
				"GetMemoryUsage",
				"Gets the current memory usage of the process in bytes",
				Array.Empty<ServiceParameter>(),
				typeof(long)
			),
			new ServiceOperation(
				"GetGCHeapSize",
				"Gets the current GC heap size in bytes",
				Array.Empty<ServiceParameter>(),
				typeof(long)
			),
			new ServiceOperation(
				"GetWindowCount",
				"Gets the number of windows currently managed by the window system",
				Array.Empty<ServiceParameter>(),
				typeof(int)
			),
			new ServiceOperation(
				"GetUptime",
				"Gets the uptime of the window system since initialization",
				Array.Empty<ServiceParameter>(),
				typeof(TimeSpan)
			),
			new ServiceOperation(
				"GetDiagnosticsReport",
				"Gets a formatted diagnostics report with all metrics",
				Array.Empty<ServiceParameter>(),
				typeof(string)
			),
			new ServiceOperation(
				"GetGCGen0Count",
				"Gets the current GC generation 0 collection count",
				Array.Empty<ServiceParameter>(),
				typeof(int)
			),
			new ServiceOperation(
				"GetGCGen1Count",
				"Gets the current GC generation 1 collection count",
				Array.Empty<ServiceParameter>(),
				typeof(int)
			),
			new ServiceOperation(
				"GetGCGen2Count",
				"Gets the current GC generation 2 collection count",
				Array.Empty<ServiceParameter>(),
				typeof(int)
			),
			new ServiceOperation(
				"ForceGC",
				"Forces a garbage collection (use sparingly)",
				Array.Empty<ServiceParameter>(),
				null // void operation
			),
			new ServiceOperation(
				"GetDetailedReport",
				"Generate detailed diagnostics report with customizable sections",
				new[]
				{
					new ServiceParameter("includeMemory", typeof(bool), false, true, "Include memory statistics"),
					new ServiceParameter("includeGC", typeof(bool), false, true, "Include GC collection counts"),
					new ServiceParameter("includeUptime", typeof(bool), false, true, "Include uptime information"),
					new ServiceParameter("includeWindows", typeof(bool), false, true, "Include window count")
				},
				typeof(string)
			)
		};
	}

	/// <inheritdoc />
	public object? Execute(string operationName, Dictionary<string, object>? parameters = null)
	{
		return operationName switch
		{
			"GetMemoryUsage" => GetMemoryUsage(),
			"GetGCHeapSize" => GetGCHeapSize(),
			"GetWindowCount" => GetWindowCount(),
			"GetUptime" => GetUptime(),
			"GetDiagnosticsReport" => GetDiagnosticsReport(),
			"GetGCGen0Count" => GetGCGen0Count(),
			"GetGCGen1Count" => GetGCGen1Count(),
			"GetGCGen2Count" => GetGCGen2Count(),
			"ForceGC" => ExecuteForceGC(),
			"GetDetailedReport" => GetDetailedReport(parameters),
			_ => throw new InvalidOperationException($"Unknown operation: {operationName}")
		};
	}

	private object? ExecuteForceGC()
	{
		ForceGC();
		return null; // void operation
	}

	private string GetDetailedReport(Dictionary<string, object>? parameters)
	{
		bool includeMemory = GetBoolParameter(parameters, "includeMemory", true);
		bool includeGC = GetBoolParameter(parameters, "includeGC", true);
		bool includeUptime = GetBoolParameter(parameters, "includeUptime", true);
		bool includeWindows = GetBoolParameter(parameters, "includeWindows", true);

		var sb = new StringBuilder();
		sb.AppendLine("=== Detailed System Diagnostics ===");
		sb.AppendLine();

		if (includeUptime)
		{
			sb.AppendLine($"Uptime: {FormatTimeSpan(GetUptime())}");
		}

		if (includeWindows)
		{
			sb.AppendLine($"Windows: {GetWindowCount()}");
		}

		if (includeMemory || includeGC)
		{
			sb.AppendLine();
			sb.AppendLine("Memory:");
		}

		if (includeMemory)
		{
			sb.AppendLine($"  Working Set: {FormatBytes(GetMemoryUsage())}");
		}

		if (includeGC)
		{
			sb.AppendLine($"  GC Heap: {FormatBytes(GetGCHeapSize())}");
			sb.AppendLine();
			sb.AppendLine("GC Collections:");
			sb.AppendLine($"  Gen 0: {GetGCGen0Count()}");
			sb.AppendLine($"  Gen 1: {GetGCGen1Count()}");
			sb.AppendLine($"  Gen 2: {GetGCGen2Count()}");
		}

		return sb.ToString();
	}

	private static bool GetBoolParameter(Dictionary<string, object>? parameters, string name, bool defaultValue)
	{
		if (parameters == null || !parameters.TryGetValue(name, out var value))
			return defaultValue;

		return value switch
		{
			bool b => b,
			string s => bool.TryParse(s, out var result) && result,
			_ => defaultValue
		};
	}

	#endregion

	/// <summary>
	/// Sets the window system reference for window-related metrics.
	/// </summary>
	/// <param name="windowSystem">The window system instance.</param>
	internal void SetWindowSystem(ConsoleWindowSystem windowSystem)
	{
		_windowSystem = windowSystem;
	}

	/// <inheritdoc />
	public long GetMemoryUsage()
	{
		return Process.GetCurrentProcess().WorkingSet64;
	}

	/// <summary>
	/// Gets the current GC heap size in bytes.
	/// </summary>
	public long GetGCHeapSize()
	{
		return GC.GetTotalMemory(false);
	}

	/// <summary>
	/// Gets the number of windows currently managed by the window system.
	/// </summary>
	public int GetWindowCount()
	{
		return _windowSystem?.Windows.Count ?? 0;
	}

	/// <summary>
	/// Gets the uptime of the window system since initialization.
	/// </summary>
	public TimeSpan GetUptime()
	{
		return _uptime.Elapsed;
	}

	/// <summary>
	/// Gets the current GC generation 0 collection count.
	/// </summary>
	public int GetGCGen0Count()
	{
		return GC.CollectionCount(0);
	}

	/// <summary>
	/// Gets the current GC generation 1 collection count.
	/// </summary>
	public int GetGCGen1Count()
	{
		return GC.CollectionCount(1);
	}

	/// <summary>
	/// Gets the current GC generation 2 collection count.
	/// </summary>
	public int GetGCGen2Count()
	{
		return GC.CollectionCount(2);
	}

	/// <summary>
	/// Forces a garbage collection. Use sparingly.
	/// </summary>
	public void ForceGC()
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
	}

	/// <summary>
	/// Gets a formatted diagnostics report with all metrics.
	/// </summary>
	public string GetDiagnosticsReport()
	{
		var sb = new StringBuilder();
		sb.AppendLine("=== System Diagnostics ===");
		sb.AppendLine();
		sb.AppendLine($"Uptime: {FormatTimeSpan(GetUptime())}");
		sb.AppendLine($"Windows: {GetWindowCount()}");
		sb.AppendLine();
		sb.AppendLine("Memory:");
		sb.AppendLine($"  Working Set: {FormatBytes(GetMemoryUsage())}");
		sb.AppendLine($"  GC Heap: {FormatBytes(GetGCHeapSize())}");
		sb.AppendLine();
		sb.AppendLine("GC Collections:");
		sb.AppendLine($"  Gen 0: {GetGCGen0Count()}");
		sb.AppendLine($"  Gen 1: {GetGCGen1Count()}");
		sb.AppendLine($"  Gen 2: {GetGCGen2Count()}");

		return sb.ToString();
	}

	#region Helper Methods

	private static string FormatBytes(long bytes)
	{
		string[] sizes = { "B", "KB", "MB", "GB", "TB" };
		double len = bytes;
		int order = 0;
		while (len >= 1024 && order < sizes.Length - 1)
		{
			order++;
			len /= 1024;
		}
		return $"{len:0.##} {sizes[order]}";
	}

	private static string FormatTimeSpan(TimeSpan ts)
	{
		if (ts.TotalDays >= 1)
			return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m";
		if (ts.TotalHours >= 1)
			return $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
		if (ts.TotalMinutes >= 1)
			return $"{ts.Minutes}m {ts.Seconds}s";
		return $"{ts.Seconds}.{ts.Milliseconds:D3}s";
	}

	#endregion
}

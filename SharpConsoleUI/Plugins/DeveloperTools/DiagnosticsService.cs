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
/// Provides system diagnostics and performance metrics.
/// </summary>
public interface IDiagnosticsService
{
	/// <summary>
	/// Gets the current memory usage of the process in bytes.
	/// </summary>
	long GetMemoryUsage();

	/// <summary>
	/// Gets the current GC heap size in bytes.
	/// </summary>
	long GetGCHeapSize();

	/// <summary>
	/// Gets the number of windows currently managed by the window system.
	/// </summary>
	int GetWindowCount();

	/// <summary>
	/// Gets the uptime of the window system since initialization.
	/// </summary>
	TimeSpan GetUptime();

	/// <summary>
	/// Gets a formatted diagnostics report with all metrics.
	/// </summary>
	string GetDiagnosticsReport();

	/// <summary>
	/// Gets the current GC generation 0 collection count.
	/// </summary>
	int GetGCGen0Count();

	/// <summary>
	/// Gets the current GC generation 1 collection count.
	/// </summary>
	int GetGCGen1Count();

	/// <summary>
	/// Gets the current GC generation 2 collection count.
	/// </summary>
	int GetGCGen2Count();

	/// <summary>
	/// Forces a garbage collection. Use sparingly.
	/// </summary>
	void ForceGC();
}

/// <summary>
/// Implementation of diagnostics service providing system performance metrics.
/// </summary>
public class DiagnosticsService : IDiagnosticsService
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

	/// <inheritdoc />
	public long GetGCHeapSize()
	{
		return GC.GetTotalMemory(false);
	}

	/// <inheritdoc />
	public int GetWindowCount()
	{
		return _windowSystem?.Windows.Count ?? 0;
	}

	/// <inheritdoc />
	public TimeSpan GetUptime()
	{
		return _uptime.Elapsed;
	}

	/// <inheritdoc />
	public int GetGCGen0Count()
	{
		return GC.CollectionCount(0);
	}

	/// <inheritdoc />
	public int GetGCGen1Count()
	{
		return GC.CollectionCount(1);
	}

	/// <inheritdoc />
	public int GetGCGen2Count()
	{
		return GC.CollectionCount(2);
	}

	/// <inheritdoc />
	public void ForceGC()
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
	}

	/// <inheritdoc />
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
}

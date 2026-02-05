// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Tests.Infrastructure;

/// <summary>
/// Helper class for creating ConsoleWindowSystem instances for testing.
/// Provides preconfigured test systems with diagnostics enabled.
/// </summary>
public static class TestWindowSystemBuilder
{
	/// <summary>
	/// Creates a test window system with diagnostics enabled and a mock console driver.
	/// </summary>
	/// <param name="configure">Optional configuration callback.</param>
	/// <returns>A configured ConsoleWindowSystem instance ready for testing.</returns>
	public static ConsoleWindowSystem CreateTestSystem(Action<ConsoleWindowSystemOptions>? configure = null)
	{
		// Disable status bars by default in tests to isolate core rendering logic
		var statusBarOptions = new Configuration.StatusBarOptions(
			ShowTopStatus: false,
			ShowBottomStatus: false,
			ShowTaskBar: false,
			ShowStartButton: false
		);

		var options = new ConsoleWindowSystemOptions(
			EnableDiagnostics: true,
			DiagnosticsRetainFrames: 10,
			DiagnosticsLayers: DiagnosticsLayers.All,
			EnableQualityAnalysis: true,
			EnableFrameRateLimiting: false,
			EnablePerformanceMetrics: false,
			StatusBarOptions: statusBarOptions
		);

		// Allow caller to customize options
		if (configure != null)
		{
			// Create a modified options instance
			var customOptions = options;
			configure(customOptions);
			options = customOptions;
		}

		// Create mock console driver
		var mockDriver = new MockConsoleDriver();

		// Create window system
		return new ConsoleWindowSystem(mockDriver, options: options);
	}

	/// <summary>
	/// Creates a test window system with specific width and height.
	/// </summary>
	public static ConsoleWindowSystem CreateTestSystem(int width, int height)
	{
		var mockDriver = new MockConsoleDriver(width, height);

		// Disable status bars by default in tests to isolate core rendering logic
		var statusBarOptions = new Configuration.StatusBarOptions(
			ShowTopStatus: false,
			ShowBottomStatus: false,
			ShowTaskBar: false,
			ShowStartButton: false
		);

		var options = new ConsoleWindowSystemOptions(
			EnableDiagnostics: true,
			DiagnosticsRetainFrames: 10,
			DiagnosticsLayers: DiagnosticsLayers.All,
			EnableQualityAnalysis: true,
			EnableFrameRateLimiting: false,
			EnablePerformanceMetrics: false,
			StatusBarOptions: statusBarOptions
		);

		return new ConsoleWindowSystem(mockDriver, options: options);
	}

	/// <summary>
	/// Creates a test window system with diagnostics disabled (for performance testing).
	/// </summary>
	public static ConsoleWindowSystem CreateTestSystemWithoutDiagnostics()
	{
		var mockDriver = new MockConsoleDriver();

		var options = new ConsoleWindowSystemOptions(
			EnableDiagnostics: false,
			EnableFrameRateLimiting: false,
			EnablePerformanceMetrics: false
		);

		return new ConsoleWindowSystem(mockDriver, options: options);
	}

	/// <summary>
	/// Creates a test window system with line-level dirty tracking mode.
	/// </summary>
	public static ConsoleWindowSystem CreateTestSystemWithLineMode()
	{
		// Disable status bars by default in tests to isolate core rendering logic
		var statusBarOptions = new Configuration.StatusBarOptions(
			ShowTopStatus: false,
			ShowBottomStatus: false,
			ShowTaskBar: false,
			ShowStartButton: false
		);

		var options = new ConsoleWindowSystemOptions(
			EnableDiagnostics: true,
			DiagnosticsRetainFrames: 10,
			DiagnosticsLayers: DiagnosticsLayers.All,
			EnableQualityAnalysis: true,
			EnableFrameRateLimiting: false,
			EnablePerformanceMetrics: false,
			StatusBarOptions: statusBarOptions,
			DirtyTrackingMode: Configuration.DirtyTrackingMode.Line  // LINE MODE
		);

		// Create mock console driver
		var mockDriver = new MockConsoleDriver();

		// Create window system
		return new ConsoleWindowSystem(mockDriver, options: options);
	}

	/// <summary>
	/// Creates a test window system with cell-level dirty tracking mode.
	/// </summary>
	public static ConsoleWindowSystem CreateTestSystemWithCellMode()
	{
		// Disable status bars by default in tests to isolate core rendering logic
		var statusBarOptions = new Configuration.StatusBarOptions(
			ShowTopStatus: false,
			ShowBottomStatus: false,
			ShowTaskBar: false,
			ShowStartButton: false
		);

		var options = new ConsoleWindowSystemOptions(
			EnableDiagnostics: true,
			DiagnosticsRetainFrames: 10,
			DiagnosticsLayers: DiagnosticsLayers.All,
			EnableQualityAnalysis: true,
			EnableFrameRateLimiting: false,
			EnablePerformanceMetrics: false,
			StatusBarOptions: statusBarOptions,
			DirtyTrackingMode: Configuration.DirtyTrackingMode.Cell  // CELL MODE
		);

		// Create mock console driver
		var mockDriver = new MockConsoleDriver();

		// Create window system
		return new ConsoleWindowSystem(mockDriver, options: options);
	}
}

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
using SharpConsoleUI.Input;

namespace SharpConsoleUI.Tests.Infrastructure;

/// <summary>
/// Helper class for creating ConsoleWindowSystem instances for testing.
/// Provides preconfigured test systems with diagnostics enabled.
/// </summary>
public static class TestWindowSystemBuilder
{
	/// <summary>
	/// Registers input event handlers on the system so that mouse and keyboard
	/// events are processed during tests (normally this happens inside Run()).
	/// </summary>
	private static ConsoleWindowSystem WithInputHandlers(ConsoleWindowSystem system)
	{
		EventHandler<ConsoleKeyInfo> keyHandler = (sender, key) =>
		{
			system.InputStateService.EnqueueKey(key);
		};
		system.Input.RegisterEventHandlers(keyHandler);
		return system;
	}

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
			StatusBarOptions: statusBarOptions,

			// CRITICAL: Disable periodic redraw for tests - breaks zero-output guarantee
			// Fix27 forces full redraw every second to clear terminal echo leaks,
			// but this breaks the critical static content = zero bytes optimization
			Fix27_PeriodicFullRedraw: false
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

		// Create window system with input handlers registered
		return WithInputHandlers(new ConsoleWindowSystem(mockDriver, options: options));
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
			StatusBarOptions: statusBarOptions,

			// CRITICAL: Disable periodic redraw for tests - breaks zero-output guarantee
			// Fix27 forces full redraw every second to clear terminal echo leaks,
			// but this breaks the critical static content = zero bytes optimization
			Fix27_PeriodicFullRedraw: false
		);

		return WithInputHandlers(new ConsoleWindowSystem(mockDriver, options: options));
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

		return WithInputHandlers(new ConsoleWindowSystem(mockDriver, options: options));
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
			DirtyTrackingMode: Configuration.DirtyTrackingMode.Line,  // LINE MODE

			// CRITICAL: Disable periodic redraw for tests - breaks zero-output guarantee
			Fix27_PeriodicFullRedraw: false
		);

		// Create mock console driver
		var mockDriver = new MockConsoleDriver();

		// Create window system
		return WithInputHandlers(new ConsoleWindowSystem(mockDriver, options: options));
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
			DirtyTrackingMode: Configuration.DirtyTrackingMode.Cell,  // CELL MODE

			// CRITICAL: Disable periodic redraw for tests - breaks zero-output guarantee
			Fix27_PeriodicFullRedraw: false
		);

		// Create mock console driver
		var mockDriver = new MockConsoleDriver();

		// Create window system
		return WithInputHandlers(new ConsoleWindowSystem(mockDriver, options: options));
	}
}

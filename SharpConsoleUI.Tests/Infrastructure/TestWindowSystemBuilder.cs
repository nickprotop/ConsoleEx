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
	/// <param name="configure">Optional configuration callback that returns a modified options record.</param>
	/// <returns>A configured ConsoleWindowSystem instance ready for testing.</returns>
	public static ConsoleWindowSystem CreateTestSystem(Func<ConsoleWindowSystemOptions, ConsoleWindowSystemOptions>? configure = null)
	{
		var options = new ConsoleWindowSystemOptions(
			EnableDiagnostics: true,
			DiagnosticsRetainFrames: 10,
			DiagnosticsLayers: DiagnosticsLayers.All,
			EnableQualityAnalysis: true,
			EnableFrameRateLimiting: false,
			EnablePerformanceMetrics: false,
			ShowTopPanel: false,
			ShowBottomPanel: false
		);

		// Allow caller to customize options via record 'with' expressions
		if (configure != null)
		{
			options = configure(options);
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

		var options = new ConsoleWindowSystemOptions(
			EnableDiagnostics: true,
			DiagnosticsRetainFrames: 10,
			DiagnosticsLayers: DiagnosticsLayers.All,
			EnableQualityAnalysis: true,
			EnableFrameRateLimiting: false,
			EnablePerformanceMetrics: false,
			ShowTopPanel: false,
			ShowBottomPanel: false
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
			EnablePerformanceMetrics: false,
			ShowTopPanel: false,
			ShowBottomPanel: false
		);

		return WithInputHandlers(new ConsoleWindowSystem(mockDriver, options: options));
	}

	/// <summary>
	/// Creates a test window system with line-level dirty tracking mode.
	/// </summary>
	public static ConsoleWindowSystem CreateTestSystemWithLineMode()
	{
		var options = new ConsoleWindowSystemOptions(
			EnableDiagnostics: true,
			DiagnosticsRetainFrames: 10,
			DiagnosticsLayers: DiagnosticsLayers.All,
			EnableQualityAnalysis: true,
			EnableFrameRateLimiting: false,
			EnablePerformanceMetrics: false,
			DirtyTrackingMode: Configuration.DirtyTrackingMode.Line,  // LINE MODE
			ShowTopPanel: false,
			ShowBottomPanel: false
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
		var options = new ConsoleWindowSystemOptions(
			EnableDiagnostics: true,
			DiagnosticsRetainFrames: 10,
			DiagnosticsLayers: DiagnosticsLayers.All,
			EnableQualityAnalysis: true,
			EnableFrameRateLimiting: false,
			EnablePerformanceMetrics: false,
			DirtyTrackingMode: Configuration.DirtyTrackingMode.Cell,  // CELL MODE
			ShowTopPanel: false,
			ShowBottomPanel: false
		);

		// Create mock console driver
		var mockDriver = new MockConsoleDriver();

		// Create window system
		return WithInputHandlers(new ConsoleWindowSystem(mockDriver, options: options));
	}
}

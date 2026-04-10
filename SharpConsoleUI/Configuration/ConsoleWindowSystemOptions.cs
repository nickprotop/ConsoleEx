using SharpConsoleUI.Rendering;

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Flags for controlling which diagnostics layers are captured.
/// </summary>
[Flags]
public enum DiagnosticsLayers
{
	/// <summary>No diagnostics captured.</summary>
	None = 0,
	/// <summary>CharacterBuffer snapshots (DOM layer).</summary>
	CharacterBuffer = 1 << 0,
	/// <summary>ANSI lines snapshots (ANSI generation layer).</summary>
	AnsiLines = 1 << 1,
	/// <summary>ConsoleBuffer snapshots (double-buffering layer).</summary>
	ConsoleBuffer = 1 << 2,
	/// <summary>Console output snapshots (final output).</summary>
	ConsoleOutput = 1 << 3,
	/// <summary>All diagnostics layers.</summary>
	All = CharacterBuffer | AnsiLines | ConsoleBuffer | ConsoleOutput
}

/// <summary>
/// Dirty tracking granularity for double-buffering optimization.
/// </summary>
public enum DirtyTrackingMode
{
	/// <summary>
	/// Track dirty at line-level granularity.
	/// When any cell in a line changes, render the entire line.
	/// Pros: Simpler, fewer cursor moves, proven stable.
	/// Cons: Outputs entire line (~200 cells) even for single character change.
	/// </summary>
	Line = 0,

	/// <summary>
	/// Track dirty at cell/region-level granularity.
	/// Only render the specific changed regions within lines.
	/// Pros: Minimal output, optimal for small changes.
	/// Cons: More cursor moves, slightly more complex.
	/// </summary>
	Cell = 1,

	/// <summary>
	/// Smart adaptive mode - chooses LINE or CELL strategy per line based on heuristics.
	/// Analyzes dirty pattern (coverage + fragmentation) and selects optimal rendering strategy.
	/// Pros: Best of both worlds, automatic optimization, no configuration needed.
	/// Cons: Slight decision overhead (optimized single-pass), more complex logic.
	/// </summary>
	Smart = 2
}

/// <summary>
/// Controls how semi-transparent windows behave when the desktop background is transparent (A=0).
/// </summary>
public enum TerminalTransparencyMode
{
	/// <summary>
	/// Semi-transparent window colors blend against black (the RGB of Color.Transparent).
	/// The window shows a dark tinted color. Terminal transparency is lost under the window.
	/// This is the default — predictable, color-preserving behavior.
	/// </summary>
	PreserveWindowColor,

	/// <summary>
	/// Semi-transparent windows over a transparent desktop emit ANSI 49 (terminal default bg).
	/// The window's tint color is lost, but terminal-level transparency shows through.
	/// Use this when terminal transparency is more important than window tinting.
	/// </summary>
	PreserveTerminalTransparency
}

/// <summary>
/// Configuration options for ConsoleWindowSystem behavior.
/// </summary>
public record ConsoleWindowSystemOptions(
    bool EnablePerformanceMetrics = false,
    bool EnableFrameRateLimiting = true,
    int TargetFPS = 60,
    bool ClampToWindowWidth = false,

    // Dirty tracking granularity (Smart = adaptive [default], Cell = minimal output, Line = fewer cursor moves)
    DirtyTrackingMode DirtyTrackingMode = DirtyTrackingMode.Smart,

    // Smart mode tuning parameters (only used when DirtyTrackingMode = Smart)
    float SmartModeCoverageThreshold = 0.6f,      // >60% dirty cells → use LINE mode
    int SmartModeFragmentationThreshold = 5,      // >5 separate regions → use LINE mode

    // Animation system
    bool EnableAnimations = true,

    // Window rendering optimizations
    bool ClearDestinationOnWindowMove = true,

    // ===== DIAGNOSTICS & TESTING =====
    // Rendering diagnostics for testing and debugging (default: false, zero overhead when disabled)
    bool EnableDiagnostics = false,
    int DiagnosticsRetainFrames = 1,
    DiagnosticsLayers DiagnosticsLayers = DiagnosticsLayers.All,
    bool EnableQualityAnalysis = false,
    bool EnablePerformanceProfiling = false,

    // Panel system configuration
    Func<SharpConsoleUI.Panel.PanelBuilder, SharpConsoleUI.Panel.PanelBuilder>? TopPanelConfig = null,
    Func<SharpConsoleUI.Panel.PanelBuilder, SharpConsoleUI.Panel.PanelBuilder>? BottomPanelConfig = null,

    // Panel visibility (both visible by default)
    bool ShowTopPanel = true,
    bool ShowBottomPanel = true,

    // Desktop background configuration (gradient, pattern, animated). Null uses theme defaults.
    DesktopBackgroundConfig? DesktopBackground = null,

    // Terminal transparency behavior for semi-transparent windows over transparent desktop
    TerminalTransparencyMode TerminalTransparencyMode = TerminalTransparencyMode.PreserveWindowColor
)
{
    private const string PerfMetricsEnvVar = "SHARPCONSOLEUI_PERF_METRICS";

    /// <summary>
    /// Gets the minimum time between frames in milliseconds based on TargetFPS.
    /// </summary>
    public int MinFrameTime => TargetFPS > 0 ? 1000 / TargetFPS : 16;

    /// <summary>
    /// Gets the default configuration with frame rate limiting enabled at 60 FPS.
    /// </summary>
    public static ConsoleWindowSystemOptions Default => new();

    /// <summary>
    /// Creates a new configuration, checking environment variable SHARPCONSOLEUI_PERF_METRICS for override.
    /// </summary>
    /// <param name="enableMetrics">Explicit enable flag, or null to check environment variable.</param>
    /// <param name="enableFrameRateLimiting">Enable frame rate limiting (default: true).</param>
    /// <param name="targetFPS">Target frames per second (default: 60).</param>
    /// <returns>A new ConsoleWindowSystemOptions instance.</returns>
    public static ConsoleWindowSystemOptions Create(
        bool? enableMetrics = null,
        bool? enableFrameRateLimiting = null,
        int? targetFPS = null)
    {
        bool metricsEnabled = enableMetrics ?? GetEnvironmentOverride();
        bool frameRateLimitingEnabled = enableFrameRateLimiting ?? true;
        int fps = targetFPS ?? 60;

        return new ConsoleWindowSystemOptions(
            EnablePerformanceMetrics: metricsEnabled,
            EnableFrameRateLimiting: frameRateLimitingEnabled,
            TargetFPS: fps
        );
    }

    private static bool GetEnvironmentOverride()
    {
        var envValue = Environment.GetEnvironmentVariable(PerfMetricsEnvVar);
        if (string.IsNullOrWhiteSpace(envValue))
            return false;

        return envValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               envValue.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a configuration with performance metrics enabled.
    /// </summary>
    public static ConsoleWindowSystemOptions WithMetrics => new(EnablePerformanceMetrics: true);

    /// <summary>
    /// Gets a configuration with frame rate limiting disabled (renders as fast as possible).
    /// </summary>
    public static ConsoleWindowSystemOptions WithoutFrameRateLimiting => new(EnableFrameRateLimiting: false);

    /// <summary>
    /// Gets a configuration with custom target FPS.
    /// </summary>
    public static ConsoleWindowSystemOptions WithTargetFPS(int fps) => new(TargetFPS: fps);
}

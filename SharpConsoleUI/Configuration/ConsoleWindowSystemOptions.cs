namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration options for ConsoleWindowSystem behavior.
/// </summary>
public record ConsoleWindowSystemOptions(
    bool EnablePerformanceMetrics = false,
    bool EnableFrameRateLimiting = true,
    int TargetFPS = 60,
    StatusBarOptions? StatusBarOptions = null
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
    /// Gets the status bar configuration, using defaults if not specified.
    /// </summary>
    public StatusBarOptions StatusBar => StatusBarOptions ?? Configuration.StatusBarOptions.Default;

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

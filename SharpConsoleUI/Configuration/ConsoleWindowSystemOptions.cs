namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration options for ConsoleWindowSystem behavior.
/// </summary>
public record ConsoleWindowSystemOptions(
    bool EnablePerformanceMetrics = false,
    int PerformanceAverageFrames = 30
)
{
    private const string PerfMetricsEnvVar = "SHARPCONSOLEUI_PERF_METRICS";

    /// <summary>
    /// Gets the default configuration with all features disabled.
    /// </summary>
    public static ConsoleWindowSystemOptions Default => new();

    /// <summary>
    /// Creates a new configuration, checking environment variable SHARPCONSOLEUI_PERF_METRICS for override.
    /// </summary>
    /// <param name="enableMetrics">Explicit enable flag, or null to check environment variable.</param>
    /// <returns>A new ConsoleWindowSystemOptions instance.</returns>
    public static ConsoleWindowSystemOptions Create(bool? enableMetrics = null)
    {
        bool enabled = enableMetrics ?? GetEnvironmentOverride();
        return new ConsoleWindowSystemOptions(EnablePerformanceMetrics: enabled);
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
}

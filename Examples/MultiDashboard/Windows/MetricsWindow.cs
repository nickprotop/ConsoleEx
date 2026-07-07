using MultiDashboard.Services;
using SharpConsoleUI;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace MultiDashboard.Windows;

/// <summary>
/// The real-time Metrics dashboard. A normal, movable (non-maximized) window that visualises live
/// system telemetry with the flagship chart controls: a stacked <see cref="SparklineControl"/> row
/// (CPU / memory / network), a per-core <see cref="BarGraphControl"/> column, and a Braille
/// <see cref="LineGraphControl"/> tracking a latency trend. All charts are created once and fed on
/// this window's own async thread from <see cref="SystemStatsService"/>; nothing is rebuilt per
/// frame. Implements <see cref="IDashboardWindow"/> so the Control Center can toggle it.
/// </summary>
public class MetricsWindow : IDashboardWindow
{
    private const int UpdateIntervalMs = 250;
    private const int CoreCount = 4;
    private const double MaxCpuValue = 100.0;

    private readonly ConsoleWindowSystem _windowSystem;
    private readonly SystemStatsService _stats = new();
    private readonly Random _random = new();
    private Window? _window;
    private bool _disposed = false;

    public MetricsWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        CreateWindow();
    }

    /// <summary>The built window (a normal, movable window — never maximized).</summary>
    public Window? Window => _window;

    private void CreateWindow()
    {
        // ── Header (row 0) — spans both columns; [gradient=cool] accent over the ocean theme. ─────
        var header = Controls.Markup(
                "[gradient=cool]  Real-Time Metrics[/]   [dim]CPU · memory · network · per-core · latency[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();

        // ── Sparkline stack (row 1, col 0) — CPU / memory / network, each named + fed live. ───────
        var sparkPanel = Controls.ScrollablePanel()
            .AddControl(Controls.Markup("[dim]CPU load[/]").WithMargin(1, 1, 1, 0).Build())
            .AddControl(Controls.Sparkline()
                .WithMode(SparklineMode.Block)
                .WithHeight(3)
                .WithMaxValue(MaxCpuValue)
                .WithName("cpu")
                .WithMargin(1, 0, 1, 0)
                .Build())
            .AddControl(Controls.Markup("[dim]Memory used[/]").WithMargin(1, 1, 1, 0).Build())
            .AddControl(Controls.Sparkline()
                .WithMode(SparklineMode.Block)
                .WithHeight(3)
                .WithMaxValue(MaxCpuValue)
                .WithName("mem")
                .WithMargin(1, 0, 1, 0)
                .Build())
            .AddControl(Controls.Markup("[dim]Network I/O[/]").WithMargin(1, 1, 1, 0).Build())
            .AddControl(Controls.Sparkline()
                .WithMode(SparklineMode.Braille)
                .WithHeight(3)
                .WithName("net")
                .WithMargin(1, 0, 1, 1)
                .Build())
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        // ── Per-core BarGraph column (row 1, col 1) — one BarGraph per core, updated via .Value. ──
        var corePanel = Controls.ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();
        corePanel.AddControl(Controls.Markup("[bold]Per-core load[/]").WithMargin(1, 1, 1, 0).Build());
        for (int i = 0; i < CoreCount; i++)
        {
            int bottom = i == CoreCount - 1 ? 1 : 0;
            corePanel.AddControl(Controls.BarGraph()
                .WithLabel($"Core {i}")
                .WithValue(0)
                .WithMaxValue(MaxCpuValue)
                .WithColorRole(ColorRole.Info)
                .ShowValue()
                .WithName($"core{i}")
                .WithMargin(1, 1, 1, bottom)
                .Build());
        }

        // ── Latency LineGraph (row 2) — Braille, spans both columns. ──────────────────────────────
        var latency = Controls.LineGraph()
            .WithTitle("Latency (ms)", Color.Cyan1)
            .WithMode(LineGraphMode.Braille)
            .WithMaxValue(MaxCpuValue)
            .AddSeries("latency", Color.Cyan1, "cool")
            .WithYAxisLabels()
            .WithBaseline()
            .WithName("latency")
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithMargin(1, 0, 1, 0)
            .Build();

        var grid = Controls.Grid()
            .Columns(GridLength.Star(1), GridLength.Star(1))
            .Rows(GridLength.Auto(), GridLength.Star(1), GridLength.Star(1))
            .RowGap(1)
            .ColumnGap(2)
            .WithColorRole(ColorRole.Primary)
            .WithPadding(1, 0, 1, 0)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Place(header, 0, 0, colSpan: 2)
            .Place(sparkPanel, 1, 0)
            .Place(corePanel, 1, 1)
            .Place(latency, 2, 0, colSpan: 2)
            .Build();

        _window = new WindowBuilder(_windowSystem)
            .WithTitle("Metrics")
            .WithName("metrics")
            .WithBackgroundGradient(ColorGradient.FromColors(new Color(16, 42, 66), new Color(9, 24, 40)), GradientDirection.Vertical)
            .WithSize(60, 20)
            .AtPosition(64, 2)
            .AddControl(grid)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .Build();
    }

    /// <summary>
    /// Async feed: every <see cref="UpdateIntervalMs"/> ms reads a fresh sample from
    /// <see cref="SystemStatsService"/> and pushes it into the named charts. Feeds go through
    /// <see cref="Window.FindControl{T}(string)"/> — <c>AddDataPoint</c> and the <c>Value</c> setter
    /// are self-invalidating, so no <c>EnqueueOnUIThread</c> is needed for these.
    /// </summary>
    private async Task UpdateLoopAsync(Window window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(UpdateIntervalMs, ct);

            var sample = _stats.GetCurrentStats();
            var cpu = sample.CpuUser + sample.CpuSystem + sample.CpuIo;
            var net = sample.DiskReadMbps + sample.DiskWriteMbps;

            window.FindControl<SparklineControl>("cpu")?.AddDataPoint(cpu);
            window.FindControl<SparklineControl>("mem")?.AddDataPoint(sample.MemoryUsed);
            window.FindControl<SparklineControl>("net")?.AddDataPoint(net);

            window.FindControl<LineGraphControl>("latency")?
                .AddDataPoint("latency", Math.Clamp(cpu * 0.8 + _random.Next(-5, 6), 0, MaxCpuValue));

            // Per-core load: spread the total CPU across cores with a little jitter.
            for (int i = 0; i < CoreCount; i++)
            {
                var core = window.FindControl<BarGraphControl>($"core{i}");
                if (core != null)
                    core.Value = Math.Clamp(cpu + _random.Next(-15, 16), 0, MaxCpuValue);
            }
        }
    }

    public void Show()
    {
        if (_window != null)
            _windowSystem.AddWindow(_window);
    }

    public void Hide()
    {
        if (_window != null)
            _windowSystem.CloseWindow(_window);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _window?.Close();
        _window = null;
        _disposed = true;
    }
}

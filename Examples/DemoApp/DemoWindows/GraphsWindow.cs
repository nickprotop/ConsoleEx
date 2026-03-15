using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace DemoApp.DemoWindows;

internal static class GraphsWindow
{
    private const int UpdateIntervalMs = 500;
    private const int WindowWidth = 90;
    private const int WindowHeight = 38;
    private const int SparklineHeight = 5;
    private const int BarGraphWidth = 50;
    private const int BarLabelWidth = 10;
    private const double MaxCpuValue = 100.0;
    private const double MaxNetworkValue = 50.0;
    private const double ProgressMaxValue = 100.0;
    private const int RandomFluctuationRange = 15;
    private const int ProgressFluctuationRange = 8;

    public static Window Create(ConsoleWindowSystem ws)
    {
        var header = Controls.Header("Data Visualization", "cyan");

        var cpuSparkline = Controls.Sparkline()
            .WithTitle("CPU Load")
            .WithTitleColor(Color.Yellow)
            .WithMode(SparklineMode.Block)
            .WithHeight(SparklineHeight)
            .WithMaxValue(MaxCpuValue)
            .WithBarColor(Color.Cyan1)
            .WithGradient(Color.Green, Color.Yellow, Color.Red)
            .WithBaseline()
            .WithName("cpuSpark")
            .Build();

        var networkSparkline = Controls.Sparkline()
            .WithTitle("Network I/O")
            .WithTitleColor(Color.Yellow)
            .WithMode(SparklineMode.Braille)
            .WithHeight(SparklineHeight)
            .WithMaxValue(MaxNetworkValue)
            .WithBarColor(Color.Blue)
            .WithGradient(Color.DodgerBlue1, Color.Cyan1, Color.Green)
            .WithBaseline()
            .WithName("netSpark")
            .Build();

        var lineGraphBraille = Controls.LineGraph()
            .WithTitle("Response Time (Braille)")
            .WithTitle("Response Time (Braille)", Color.Yellow)
            .WithMode(LineGraphMode.Braille)
            .WithHeight(SparklineHeight)
            .WithMaxValue(MaxCpuValue)
            .AddSeries("latency", Color.Cyan1, "cool")
            .WithYAxisLabels()
            .WithBaseline()
            .WithName("lineGraphBraille")
            .Stretch()
            .Build();

        var lineGraphAscii = Controls.LineGraph()
            .WithTitle("Throughput (ASCII)", Color.Yellow)
            .WithMode(LineGraphMode.Ascii)
            .WithHeight(SparklineHeight)
            .WithMaxValue(MaxNetworkValue)
            .AddSeries("throughput", Color.Green)
            .WithYAxisLabels()
            .WithBaseline()
            .WithName("lineGraphAscii")
            .Stretch()
            .Build();

        var separator = Controls.Separator();

        var barWeb = Controls.BarGraph()
            .WithLabel("Web")
            .WithLabelWidth(BarLabelWidth)
            .WithValue(72)
            .WithBarWidth(BarGraphWidth)
            .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
            .WithName("barWeb")
            .Build();

        var barApi = Controls.BarGraph()
            .WithLabel("API")
            .WithLabelWidth(BarLabelWidth)
            .WithValue(45)
            .WithBarWidth(BarGraphWidth)
            .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
            .WithName("barApi")
            .Build();

        var barDatabase = Controls.BarGraph()
            .WithLabel("Database")
            .WithLabelWidth(BarLabelWidth)
            .WithValue(88)
            .WithBarWidth(BarGraphWidth)
            .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
            .WithName("barDb")
            .Build();

        var barCache = Controls.BarGraph()
            .WithLabel("Cache")
            .WithLabelWidth(BarLabelWidth)
            .WithValue(31)
            .WithBarWidth(BarGraphWidth)
            .WithSmoothGradient(Color.Green, Color.Yellow, Color.Red)
            .WithName("barCache")
            .Build();

        var progressHeader = Controls.Header("System Resources", "yellow");

        var cpuProgress = Controls.ProgressBar()
            .WithHeader("CPU")
            .WithPercentage(35)
            .Stretch()
            .ShowPercentage()
            .WithFilledColor(Color.Green)
            .WithName("cpuProg")
            .Build();

        var memProgress = Controls.ProgressBar()
            .WithHeader("Memory")
            .WithPercentage(62)
            .Stretch()
            .ShowPercentage()
            .WithFilledColor(Color.DodgerBlue1)
            .WithName("memProg")
            .Build();

        var diskProgress = Controls.ProgressBar()
            .WithHeader("Disk")
            .WithPercentage(78)
            .Stretch()
            .ShowPercentage()
            .WithFilledColor(Color.Orange1)
            .WithName("diskProg")
            .Build();

        var footer = Controls.Markup("[dim]Live data updates every 500ms | Press [bold]ESC[/] to close[/]")
            .Centered()
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Graphs & Charts")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .AddControls(
                header,
                cpuSparkline,
                networkSparkline,
                lineGraphBraille,
                lineGraphAscii,
                separator,
                barWeb, barApi, barDatabase, barCache,
                separator,
                progressHeader,
                cpuProgress, memProgress, diskProgress,
                footer)
            .WithAsyncWindowThread(async (window, ct) =>
            {
                var random = new Random();
                double cpuBase = 50;
                double netBase = 25;

                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(UpdateIntervalMs, ct);

                    cpuBase = Math.Clamp(cpuBase + random.Next(-RandomFluctuationRange, RandomFluctuationRange + 1), 0, MaxCpuValue);
                    netBase = Math.Clamp(netBase + random.Next(-RandomFluctuationRange, RandomFluctuationRange + 1), 0, MaxNetworkValue);

                    var cpu = window.FindControl<SparklineControl>("cpuSpark");
                    cpu?.AddDataPoint(cpuBase);

                    var net = window.FindControl<SparklineControl>("netSpark");
                    net?.AddDataPoint(netBase);

                    var lineBraille = window.FindControl<LineGraphControl>("lineGraphBraille");
                    lineBraille?.AddDataPoint("latency", cpuBase * 0.8 + random.Next(-5, 6));

                    var lineAscii = window.FindControl<LineGraphControl>("lineGraphAscii");
                    lineAscii?.AddDataPoint("throughput", netBase * 0.6 + random.Next(-3, 4));

                    var cpuProg = window.FindControl<ProgressBarControl>("cpuProg");
                    if (cpuProg != null)
                        cpuProg.Value = Math.Clamp(cpuProg.Value + random.Next(-ProgressFluctuationRange, ProgressFluctuationRange + 1), 0, ProgressMaxValue);

                    var memProg = window.FindControl<ProgressBarControl>("memProg");
                    if (memProg != null)
                        memProg.Value = Math.Clamp(memProg.Value + random.Next(-ProgressFluctuationRange, ProgressFluctuationRange + 1), 0, ProgressMaxValue);

                    var diskProg = window.FindControl<ProgressBarControl>("diskProg");
                    if (diskProg != null)
                        diskProg.Value = Math.Clamp(diskProg.Value + random.Next(-ProgressFluctuationRange, ProgressFluctuationRange + 1), 0, ProgressMaxValue);
                }
            })
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)s!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }
}

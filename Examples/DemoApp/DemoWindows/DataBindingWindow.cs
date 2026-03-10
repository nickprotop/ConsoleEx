using System.ComponentModel;
using System.Runtime.CompilerServices;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

internal static class DataBindingWindow
{
    // Simple ViewModel — standard .NET INotifyPropertyChanged, nothing framework-specific
    private class SystemMonitorVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private double _cpuUsage;
        private double _memoryUsage;
        private double _networkKBps;
        private string _statusText = "Initializing...";
        private bool _monitoringEnabled = true;
        private int _updateCount;

        public double CpuUsage
        {
            get => _cpuUsage;
            set { _cpuUsage = value; OnPropertyChanged(); }
        }

        public double MemoryUsage
        {
            get => _memoryUsage;
            set { _memoryUsage = value; OnPropertyChanged(); }
        }

        public double NetworkKBps
        {
            get => _networkKBps;
            set { _networkKBps = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool MonitoringEnabled
        {
            get => _monitoringEnabled;
            set { _monitoringEnabled = value; OnPropertyChanged(); }
        }

        public int UpdateCount
        {
            get => _updateCount;
            set { _updateCount = value; OnPropertyChanged(); }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static Window Create(ConsoleWindowSystem ws)
    {
        var vm = new SystemMonitorVM();

        // --- Header ---
        var header = Controls.Header("MVVM Data Binding Demo", "cyan");

        var description = Controls.Markup()
            .AddLine("[dim]ViewModel properties drive all controls below via Bind/BindTwoWay.[/]")
            .AddLine("[dim]No manual control updates — bindings handle everything.[/]")
            .Build();

        var rule1 = Controls.Rule("One-Way Bindings (VM → Control)");

        // --- One-way: VM.CpuUsage → ProgressBar.Value ---
        var cpuBar = Controls.ProgressBar()
            .WithHeader("CPU")
            .Stretch()
            .ShowPercentage()
            .WithFilledColor(Color.Green)
            .Build()
            .Bind(vm, v => v.CpuUsage, c => c.Value);

        // --- One-way: VM.MemoryUsage → ProgressBar.Value ---
        var memBar = Controls.ProgressBar()
            .WithHeader("Memory")
            .Stretch()
            .ShowPercentage()
            .WithFilledColor(Color.DodgerBlue1)
            .Build()
            .Bind(vm, v => v.MemoryUsage, c => c.Value);

        // --- One-way with converter: VM.NetworkKBps → MarkupControl.Text ---
        var networkLabel = Controls.Markup()
            .Centered()
            .Build()
            .Bind(vm, v => v.NetworkKBps, c => c.Text,
                v => $"[bold yellow]Network:[/] {v:F1} KB/s");

        // --- One-way with converter: VM.UpdateCount → MarkupControl.Text ---
        var counterLabel = Controls.Markup()
            .Centered()
            .Build()
            .Bind(vm, v => v.UpdateCount, c => c.Text,
                v => $"[dim]Updates received: {v}[/]");

        // --- One-way: VM.StatusText → MarkupControl.Text ---
        var statusLabel = Controls.Markup()
            .Centered()
            .Build()
            .Bind(vm, v => v.StatusText, c => c.Text);

        var rule2 = Controls.Rule("Two-Way Binding (VM ↔ Control)");

        // --- Two-way: VM.MonitoringEnabled ↔ Checkbox.Checked ---
        var enabledCheckbox = Controls.Checkbox("Monitoring Enabled")
            .Build();
        enabledCheckbox.BindTwoWay(vm, v => v.MonitoringEnabled, c => c.Checked);

        // Show the VM state driven by the checkbox
        var checkboxStatus = Controls.Markup()
            .Build()
            .Bind(vm, v => v.MonitoringEnabled, c => c.Text,
                v => v ? "[green]  VM.MonitoringEnabled = true[/]  [dim](uncheck to pause updates)[/]"
                       : "[red]  VM.MonitoringEnabled = false[/] [dim](check to resume)[/]");

        var rule3 = Controls.Rule();

        var footer = Controls.Markup(
            "[dim]Async thread updates VM properties | Bindings push changes to controls | Press [bold]ESC[/] to close[/]")
            .Centered()
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Data Binding (MVVM)")
            .WithSize(80, 24)
            .Centered()
            .AddControls(
                header, description, rule1,
                cpuBar, memBar,
                networkLabel, counterLabel, statusLabel,
                rule2,
                enabledCheckbox, checkboxStatus,
                rule3, footer)
            .WithAsyncWindowThread(async (window, ct) =>
            {
                var random = new Random();
                double cpuBase = 35, memBase = 55, netBase = 120;

                vm.StatusText = "[green]Monitoring active[/]";

                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(500, ct);

                    if (!vm.MonitoringEnabled)
                    {
                        vm.StatusText = "[yellow]Monitoring paused[/]";
                        continue;
                    }

                    vm.StatusText = "[green]Monitoring active[/]";

                    cpuBase = Math.Clamp(cpuBase + random.Next(-8, 9), 0, 100);
                    memBase = Math.Clamp(memBase + random.Next(-3, 4), 0, 100);
                    netBase = Math.Clamp(netBase + random.Next(-30, 31), 0, 500);

                    // Just set VM properties — bindings push to controls automatically
                    vm.CpuUsage = cpuBase;
                    vm.MemoryUsage = memBase;
                    vm.NetworkKBps = netBase;
                    vm.UpdateCount++;
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

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;

var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer);

// Build the overview tab content
var overviewTable = Controls.Table()
    .AddColumn("Service")
    .AddColumn("Status")
    .AddColumn("Uptime")
    .AddRow("API Gateway", "[green]Online[/]", "14d 3h")
    .AddRow("Database", "[green]Online[/]", "14d 3h")
    .AddRow("Cache", "[yellow]Degraded[/]", "2d 11h")
    .AddRow("Worker", "[green]Online[/]", "7d 1h")
    .Rounded()
    .WithTitle("Services")
    .Build();

// Build the metrics tab content with progress bars
var metricsMarkup = Controls.Markup("[bold]System Metrics[/]")
    .AddLine("")
    .Build();

var cpuBar = Controls.ProgressBar()
    .WithHeader("CPU Usage")
    .WithValue(35)
    .Stretch()
    .ShowPercentage()
    .WithName("cpu")
    .Build();

var memoryBar = Controls.ProgressBar()
    .WithHeader("Memory")
    .WithValue(62)
    .Stretch()
    .ShowPercentage()
    .WithName("memory")
    .Build();

var diskBar = Controls.ProgressBar()
    .WithHeader("Disk I/O")
    .WithValue(18)
    .Stretch()
    .ShowPercentage()
    .WithName("disk")
    .Build();

var metricsTab = Controls.ScrollablePanel()
    .AddControl(metricsMarkup)
    .AddControl(cpuBar)
    .AddControl(memoryBar)
    .AddControl(diskBar)
    .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
    .Build();

// Build the about tab
var aboutContent = Controls.Markup("[bold yellow]TuiDashboard[/]")
    .AddLine("")
    .AddLine("A fullscreen terminal dashboard built with [blue]SharpConsoleUI[/].")
    .AddLine("")
    .AddLine("[dim]Press [green]Tab[/] to switch tabs.[/]")
    .AddLine("[dim]Press [green]Ctrl+Q[/] to exit.[/]")
    .Centered()
    .Build();

// Build the tab control
var tabs = Controls.TabControl()
    .AddTab("Overview", overviewTable)
    .AddTab("Metrics", metricsTab)
    .AddTab("About", aboutContent)
    .Fill()
    .Build();

// Status bar at the bottom
var statusBar = Controls.Markup("[dim]TuiDashboard | Ctrl+Q: Exit | Tab: Switch tabs[/]")
    .StickyBottom()
    .Build();

// Create fullscreen borderless window with async update thread
var window = new WindowBuilder(windowSystem)
    .WithTitle("TuiDashboard")
    .Maximized()
    .Borderless()
    .Resizable(false)
    .Movable(false)
    .Closable(false)
    .Minimizable(false)
    .Maximizable(false)
    .AddControls(tabs, statusBar)
    .WithAsyncWindowThread(async (win, ct) =>
    {
        var random = new Random();
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);

            var cpu = win.FindControl<ProgressBarControl>("cpu");
            var memory = win.FindControl<ProgressBarControl>("memory");
            var disk = win.FindControl<ProgressBarControl>("disk");

            if (cpu != null) cpu.Value = Math.Clamp(cpu.Value + random.Next(-5, 6), 0, 100);
            if (memory != null) memory.Value = Math.Clamp(memory.Value + random.Next(-2, 3), 0, 100);
            if (disk != null) disk.Value = Math.Clamp(disk.Value + random.Next(-3, 4), 0, 100);
        }
    })
    .BuildAndShow();

windowSystem.Run();

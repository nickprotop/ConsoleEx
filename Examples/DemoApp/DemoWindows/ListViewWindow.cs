using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

public static class ListViewWindow
{
    private const int WindowWidth = 100;
    private const int WindowHeight = 30;
    private const int ListColumnWidth = 55;

    public static Window Create(ConsoleWindowSystem ws)
    {
        var packages = BuildPackageList();

        var detailMarkup = Controls.Markup()
            .AddLines("[bold]Select a package to view details[/]")
            .WithMargin(1, 1, 1, 0)
            .Build();

        var helpMarkup = Controls.Markup()
            .AddLines(
                "",
                "[dim]Keyboard Shortcuts:[/]",
                "  [yellow]Space[/]     Toggle install",
                "  [yellow]Enter[/]     Install/activate",
                "  [yellow]Ctrl+A[/]    Select all",
                "  [yellow]Ctrl+D[/]    Deselect all",
                "  [yellow]Type[/]      Search by name",
                "  [yellow]Esc[/]       Close window")
            .WithMargin(1, 0, 1, 0)
            .Build();

        var statusBar = Controls.Markup("[dim]0 of 25 selected | Type to search | Space: Toggle | Esc: Close[/]")
            .StickyBottom()
            .Build();

        var header = Controls.Markup("[bold cyan]  NuGet Package Manager[/]")
            .StickyTop()
            .Build();

        var list = Controls.List("Available Packages")
            .WithCheckboxMode()
            .WithAutoHighlightOnFocus(true)
            .WithHoverHighlighting(true)
            .WithScrollbarVisibility(ScrollbarVisibility.Auto)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .OnSelectionChanged((sender, idx) =>
            {
                if (idx >= 0 && idx < packages.Count)
                    UpdateDetailPanel(detailMarkup, packages[idx]);
            })
            .OnCheckedItemsChanged((sender, _) =>
            {
                if (sender is ListControl lc)
                {
                    int checkedCount = lc.GetCheckedItems().Count;
                    statusBar.SetContent(new List<string>
                    {
                        $"[dim]{checkedCount} of {packages.Count} selected | Type to search | Space: Toggle | Esc: Close[/]"
                    });
                }
            })
            .OnItemActivated((sender, item) =>
            {
                ws.NotificationStateService.ShowNotification(
                    "Package Action",
                    $"Installing {item.Text}...",
                    SharpConsoleUI.Core.NotificationSeverity.Info);
            })
            .Build();

        foreach (var pkg in packages)
        {
            var li = new ListItem(pkg.Name, pkg.Icon, pkg.IconColor) { Tag = pkg, IsChecked = pkg.Installed };
            list.AddItem(li);
        }

        var detailPanel = Controls.ScrollablePanel()
            .AddControl(detailMarkup)
            .AddControl(helpMarkup)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var grid = Controls.HorizontalGrid()
            .Column(col => col.Width(ListColumnWidth).Add(list))
            .Column(col => col.Flex().Add(detailPanel))
            .WithSplitterAfter(0)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Package Manager")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .AddControls(header, grid, statusBar)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
                else if (e.KeyInfo.Key == ConsoleKey.A && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    list.SetAllChecked(true);
                    e.Handled = true;
                }
                else if (e.KeyInfo.Key == ConsoleKey.D && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    list.SetAllChecked(false);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }

    private static void UpdateDetailPanel(MarkupControl detail, PackageInfo pkg)
    {
        detail.SetContent(new List<string>
        {
            $"[bold cyan]{pkg.Name}[/]  [dim]v{pkg.Version}[/]",
            "",
            $"[dim]Author:[/]    {pkg.Author}",
            $"[dim]Downloads:[/] {pkg.Downloads:N0}",
            $"[dim]License:[/]   {pkg.License}",
            $"[dim]Category:[/]  {pkg.Category}",
            "",
            $"[dim]{pkg.Description}[/]",
        });
    }

    private static List<PackageInfo> BuildPackageList()
    {
        return new List<PackageInfo>
        {
            new("Newtonsoft.Json", "13.0.3", "[cyan]\u25cf[/]", Color.Cyan1, "Data", "James Newton-King", 2_145_000_000, "MIT", "Popular JSON framework for .NET", true),
            new("Dapper", "2.1.28", "[green]\u25cf[/]", Color.Green, "Data", "Sam Saffron", 312_000_000, "Apache-2.0", "Simple object mapper for .NET"),
            new("EntityFramework", "6.5.1", "[green]\u25cf[/]", Color.Green, "Data", "Microsoft", 890_000_000, "MIT", "Object-relational mapper for .NET", true),
            new("AutoMapper", "13.0.1", "[green]\u25cf[/]", Color.Green, "Data", "Jimmy Bogard", 520_000_000, "MIT", "Convention-based object-object mapper"),
            new("Serilog", "3.1.1", "[cyan]\u25cf[/]", Color.Cyan1, "Logging", "Serilog Contributors", 456_000_000, "Apache-2.0", "Structured logging for .NET applications", true),
            new("NLog", "5.2.8", "[cyan]\u25cf[/]", Color.Cyan1, "Logging", "NLog Contributors", 312_000_000, "BSD-3", "Flexible logging platform for .NET"),
            new("log4net", "2.0.17", "[cyan]\u25cf[/]", Color.Cyan1, "Logging", "Apache Foundation", 198_000_000, "Apache-2.0", "Port of the log4j logging framework"),
            new("xUnit", "2.7.0", "[yellow]\u25cf[/]", Color.Yellow, "Testing", "Brad Wilson", 645_000_000, "Apache-2.0", "Unit testing framework for .NET"),
            new("NUnit", "4.1.0", "[yellow]\u25cf[/]", Color.Yellow, "Testing", "Charlie Poole", 412_000_000, "MIT", "Testing framework for all .NET languages"),
            new("Moq", "4.20.70", "[yellow]\u25cf[/]", Color.Yellow, "Testing", "Daniel Cazzulino", 389_000_000, "BSD-3", "Most popular mocking framework for .NET"),
            new("FluentAssertions", "6.12.0", "[yellow]\u25cf[/]", Color.Yellow, "Testing", "Dennis Doomen", 278_000_000, "Apache-2.0", "Fluent API for asserting test results"),
            new("Polly", "8.3.0", "[magenta1]\u25cf[/]", Color.Magenta1, "Web", "Michael Wolfenden", 367_000_000, "BSD-3", "Resilience and transient-fault-handling library"),
            new("RestSharp", "110.2.0", "[magenta1]\u25cf[/]", Color.Magenta1, "Web", "RestSharp Contributors", 234_000_000, "Apache-2.0", "Simple REST and HTTP client library"),
            new("MediatR", "12.2.0", "[magenta1]\u25cf[/]", Color.Magenta1, "Web", "Jimmy Bogard", 198_000_000, "Apache-2.0", "Simple mediator implementation in .NET"),
            new("FluentValidation", "11.9.0", "[magenta1]\u25cf[/]", Color.Magenta1, "Web", "Jeremy Skinner", 287_000_000, "Apache-2.0", "Validation library with fluent interface"),
            new("StackExchange.Redis", "2.7.33", "[green]\u25cf[/]", Color.Green, "Data", "Stack Exchange", 198_000_000, "MIT", "High-performance Redis client for .NET"),
            new("Npgsql", "8.0.2", "[green]\u25cf[/]", Color.Green, "Data", "Npgsql Contributors", 234_000_000, "PostgreSQL", "PostgreSQL data provider for .NET"),
            new("MassTransit", "8.1.3", "[magenta1]\u25cf[/]", Color.Magenta1, "Web", "Chris Patterson", 145_000_000, "Apache-2.0", "Distributed application framework for .NET"),
            new("Hangfire", "1.8.9", "[orange1]\u25cf[/]", Color.Orange1, "Infra", "Sergey Odinokov", 178_000_000, "LGPL-3.0", "Background job processing for .NET"),
            new("BenchmarkDotNet", "0.13.12", "[yellow]\u25cf[/]", Color.Yellow, "Testing", "BDN Contributors", 156_000_000, "MIT", "Benchmarking framework for .NET"),
            new("Swashbuckle", "6.5.0", "[magenta1]\u25cf[/]", Color.Magenta1, "Web", "Richard Morris", 412_000_000, "MIT", "Swagger tools for ASP.NET Core APIs"),
            new("CsvHelper", "31.0.2", "[green]\u25cf[/]", Color.Green, "Data", "Josh Close", 234_000_000, "MS-PL", "Library for reading and writing CSV files"),
            new("Quartz.NET", "3.8.1", "[orange1]\u25cf[/]", Color.Orange1, "Infra", "Marko Lahma", 167_000_000, "Apache-2.0", "Job scheduling system for .NET"),
            new("SignalR", "8.0.2", "[magenta1]\u25cf[/]", Color.Magenta1, "Web", "Microsoft", 345_000_000, "MIT", "Real-time web functionality framework"),
            new("MailKit", "4.3.0", "[orange1]\u25cf[/]", Color.Orange1, "Infra", "Jeffrey Stedfast", 189_000_000, "MIT", "Cross-platform mail client library"),
        };
    }

    private record PackageInfo(
        string Name, string Version, string Icon, Color IconColor,
        string Category, string Author, long Downloads, string License,
        string Description, bool Installed = false);
}

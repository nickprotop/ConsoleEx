using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

public static class TableDemoWindow
{
    private const int WindowWidth = 120;
    private const int WindowHeight = 32;
    private const int IdColumnWidth = 5;
    private const int SalaryColumnWidth = 9;
    private const int YearsColumnWidth = 5;
    private const int StatusColumnWidth = 10;

    public static Window Create(ConsoleWindowSystem ws)
    {
        var employees = BuildEmployeeData();

        var header = Controls.Markup("[dim]Employee Directory | Sort: Click Header | Filter: / | Edit: F2 | Nav: Tab | Resize: Drag[/]")
            .StickyTop()
            .Build();

        var statusBar = Controls.Markup($"[dim]{employees.Count} employees | \u2191\u2193: Navigate | Esc: Close[/]")
            .StickyBottom()
            .Build();

        var table = Controls.Table()
            .WithTitle("Employee Directory")
            .AddColumn("ID", TextJustification.Right, IdColumnWidth)
            .AddColumn("Name")
            .AddColumn("Department")
            .AddColumn("Title")
            .AddColumn("Salary", TextJustification.Right, SalaryColumnWidth)
            .AddColumn("Years", TextJustification.Right, YearsColumnWidth)
            .AddColumn("Status", TextJustification.Center, StatusColumnWidth)
            .Interactive()
            .WithSorting()
            .WithFiltering()
            .WithFuzzyFilter()
            .WithInlineEditing()
            .WithCellNavigation()
            .WithColumnResize()
            .Rounded()
            .ShowRowSeparators()
            .WithHeaderColors(Color.White, Color.DarkBlue)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithHorizontalAlignment(HorizontalAlignment.Stretch)
            .OnSelectedRowChanged((sender, rowIdx) =>
            {
                if (rowIdx >= 0 && rowIdx < employees.Count)
                {
                    var emp = employees[rowIdx];
                    statusBar.SetContent(new List<string>
                    {
                        $"[dim]Row {rowIdx + 1}: {emp.Name} - {emp.Department} | Sort: Header | Filter: / | Edit: F2 | Esc: Close[/]"
                    });
                }
            })
            .OnRowActivated((sender, rowIdx) =>
            {
                if (rowIdx >= 0 && rowIdx < employees.Count)
                {
                    var emp = employees[rowIdx];
                    ws.NotificationStateService.ShowNotification(
                        emp.Name,
                        $"{emp.Title} \u2022 {emp.Department}\n${emp.Salary:N0}/yr \u2022 {emp.Years}yr tenure",
                        SharpConsoleUI.Core.NotificationSeverity.Info);
                }
            })
            .OnCellEditCompleted((sender, e) =>
            {
                ws.NotificationStateService.ShowNotification(
                    "Cell Edited",
                    $"Row {e.Row + 1}, Col {e.Column + 1}: \"{e.OldValue}\" \u2192 \"{e.NewValue}\"",
                    SharpConsoleUI.Core.NotificationSeverity.Success);
            })
            .Build();

        foreach (var emp in employees)
        {
            table.AddRow(
                emp.Id.ToString(),
                emp.Name,
                emp.Department,
                emp.Title,
                $"${emp.Salary:N0}",
                emp.Years.ToString(),
                emp.StatusMarkup);
        }

        return new WindowBuilder(ws)
            .WithTitle("Employee Directory Demo")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .AddControls(header, table, statusBar)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    if (e.AlreadyHandled)
                        return;
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }

    private static List<EmployeeRecord> BuildEmployeeData()
    {
        return new List<EmployeeRecord>
        {
            new(1, "Alice Chen", "Engineering", "Staff Engineer", 185000, 8, "[green]Active[/]"),
            new(2, "Bob Martinez", "Engineering", "Senior Engineer", 162000, 5, "[green]Active[/]"),
            new(3, "Carol Wang", "Engineering", "Tech Lead", 195000, 10, "[cyan]Remote[/]"),
            new(4, "Dave Kumar", "Engineering", "Junior Engineer", 95000, 1, "[green]Active[/]"),
            new(5, "Eve Johnson", "Design", "Design Director", 175000, 12, "[green]Active[/]"),
            new(6, "Frank Lee", "Design", "Senior Designer", 140000, 6, "[yellow]On Leave[/]"),
            new(7, "Grace Park", "Design", "UX Researcher", 125000, 3, "[green]Active[/]"),
            new(8, "Hank Brown", "Product", "VP of Product", 210000, 15, "[green]Active[/]"),
            new(9, "Iris Taylor", "Product", "Product Manager", 155000, 4, "[cyan]Remote[/]"),
            new(10, "Jack Wilson", "Product", "Associate PM", 105000, 2, "[green]Active[/]"),
            new(11, "Karen Davis", "Marketing", "CMO", 220000, 14, "[green]Active[/]"),
            new(12, "Leo Garcia", "Marketing", "Content Lead", 120000, 5, "[green]Active[/]"),
            new(13, "Mia Thompson", "Marketing", "SEO Specialist", 98000, 2, "[red]Contract[/]"),
            new(14, "Noah Harris", "Marketing", "Brand Manager", 135000, 7, "[yellow]On Leave[/]"),
            new(15, "Olivia Clark", "Finance", "CFO", 240000, 18, "[green]Active[/]"),
            new(16, "Pete Robinson", "Finance", "Senior Accountant", 115000, 9, "[green]Active[/]"),
            new(17, "Quinn Lewis", "Finance", "Financial Analyst", 95000, 2, "[cyan]Remote[/]"),
            new(18, "Rachel Hall", "Finance", "Payroll Manager", 108000, 6, "[green]Active[/]"),
            new(19, "Sam Young", "HR", "HR Director", 165000, 11, "[green]Active[/]"),
            new(20, "Tina King", "HR", "Recruiter", 88000, 3, "[green]Active[/]"),
            new(21, "Uma Wright", "HR", "Benefits Specialist", 92000, 4, "[red]Contract[/]"),
            new(22, "Victor Scott", "Engineering", "DevOps Lead", 170000, 7, "[green]Active[/]"),
            new(23, "Wendy Adams", "Engineering", "QA Engineer", 118000, 4, "[cyan]Remote[/]"),
            new(24, "Xander Moore", "Design", "Motion Designer", 130000, 3, "[green]Active[/]"),
            new(25, "Yuki Tanaka", "Engineering", "Security Engineer", 175000, 6, "[green]Active[/]"),
        };
    }

    private record EmployeeRecord(
        int Id, string Name, string Department, string Title,
        int Salary, int Years, string StatusMarkup);
}

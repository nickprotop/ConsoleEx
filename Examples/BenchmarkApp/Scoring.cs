using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace BenchmarkApp;

/// <summary>
/// Represents the result of a single benchmark test.
/// </summary>
public record TestResult(string Name, double FPS, double Weight)
{
    /// <summary>Weighted score for this test.</summary>
    public double Score => FPS * Weight;

    /// <summary>Star rating (1-5) based on FPS.</summary>
    public int Stars => ScoringEngine.GetStars(FPS);
}

/// <summary>
/// Calculates scores, star ratings, and overall benchmark ratings.
/// </summary>
public static class ScoringEngine
{
    private const double ExcellentThreshold = 500;
    private const double GreatThreshold = 300;
    private const double GoodThreshold = 200;
    private const double FairThreshold = 100;

    private const double FiveStarFps = 60;
    private const double FourStarFps = 30;
    private const double ThreeStarFps = 15;
    private const double TwoStarFps = 8;

    private const int MaxStars = 5;

    /// <summary>
    /// Returns a star rating (1-5) based on frames per second.
    /// </summary>
    public static int GetStars(double fps) => fps switch
    {
        > FiveStarFps => 5,
        > FourStarFps => 4,
        > ThreeStarFps => 3,
        > TwoStarFps => 2,
        _ => 1
    };

    /// <summary>
    /// Returns an overall rating label based on the total weighted score.
    /// </summary>
    public static string GetOverallRating(double totalScore) => totalScore switch
    {
        > ExcellentThreshold => "EXCELLENT",
        > GreatThreshold => "GREAT",
        > GoodThreshold => "GOOD",
        > FairThreshold => "FAIR",
        _ => "POOR"
    };

    /// <summary>
    /// Returns a markup string rendering filled and empty stars.
    /// Uses '*' character with [green] for filled and [dim] for empty.
    /// </summary>
    public static string StarString(int stars)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < MaxStars; i++)
        {
            if (i < stars)
                sb.Append("[green]*[/]");
            else
                sb.Append("[dim]*[/]");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns a color appropriate for the given star rating.
    /// </summary>
    public static Color ColorForStars(int stars) => stars switch
    {
        >= 5 => Color.Green,
        4 => Color.GreenYellow,
        3 => Color.Yellow,
        2 => Color.Orange1,
        _ => Color.Red
    };
}

/// <summary>
/// Test names and descriptions used across screens.
/// </summary>
internal static class TestInfo
{
    internal static readonly string[] Names =
    {
        "Static Content",
        "Text Scrolling",
        "Alpha Blending",
        "Window Overlap",
        "Deep Controls",
        "Full Redraw"
    };

    internal static readonly string[] Descriptions =
    {
        "Baseline single-frame render",
        "Scrolling large text buffers",
        "Alpha compositing with transparency",
        "Overlapping window z-order cycling",
        "Deeply nested control hierarchies",
        "Full screen invalidation"
    };
}

/// <summary>
/// Builds benchmark screens using real SharpConsoleUI controls.
/// Each method clears the window and adds appropriate controls.
/// </summary>
public static class ScreenBuilder
{
    private const int BarGraphLabelWidth = 18;

    /// <summary>
    /// Clears the window and populates the welcome/start screen.
    /// </summary>
    public static void BuildWelcomeScreen(
        Window window,
        string version,
        string system,
        string terminal,
        Action onStart,
        Action onExit)
    {
        window.ClearControls();

        // Scrollable content area
        var content = Controls.ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithBorderStyle(BorderStyle.None)
            .WithBackgroundColor(Color.Transparent)
            .Build();

        content.AddControl(Controls.Figlet("SHARP")
            .WithColor(Color.MediumPurple)
            .Centered()
            .Build());

        var subtitle = Controls.Markup()
            .AddLine("[bold]ConsoleUI Benchmark[/]")
            .Build();
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddControl(subtitle);

        var descBuilder = Controls.Markup()
            .AddLine("")
            .AddLine("Measures terminal rendering performance:")
            .AddLine("");
        for (int i = 0; i < TestInfo.Names.Length; i++)
            descBuilder.AddLine($"  [cyan]{i + 1}.[/] {TestInfo.Names[i]}  [dim]{TestInfo.Descriptions[i]}[/]");
        descBuilder.AddLine("");
        descBuilder.AddLine($"  [dim]Version:  {version}[/]");
        descBuilder.AddLine($"  [dim]System:   {system}[/]");
        descBuilder.AddLine($"  [dim]Terminal: {terminal}[/]");
        content.AddControl(descBuilder.Build());

        window.AddControl(content);

        // Sticky bottom toolbar
        var toolbar = Controls.Toolbar()
            .AddButton(
                Controls.Button(" START BENCHMARK ")
                    .WithBorder(ButtonBorderStyle.Rounded)
                    .WithBackgroundColor(Color.Green)
                    .WithForegroundColor(Color.Black)
                    .WithFocusedBackgroundColor(Color.Lime)
                    .WithFocusedForegroundColor(Color.Black)
                    .OnClick((s, e) => onStart()))
            .WithMargin(2, 0, 2, 0)
            .StickyBottom()
            .Build();
        window.AddControl(toolbar);
    }

    /// <summary>
    /// Clears the window and populates the running/in-progress screen.
    /// Returns the ProgressBarControl so the caller can update its Value.
    /// </summary>
    public static ProgressBarControl BuildRunningScreen(
        Window window,
        int testIndex,
        int totalTests,
        string testName,
        List<TestResult> completed,
        Action onStop)
    {
        window.ClearControls();

        // Scrollable content area
        var content = Controls.ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithBorderStyle(BorderStyle.None)
            .WithBackgroundColor(Color.Transparent)
            .Build();

        content.AddControl(Controls.Markup()
            .AddLine($"  Test {testIndex}/{totalTests}: [bold yellow]{testName}[/]")
            .AddLine("")
            .Build());

        var progressBar = Controls.ProgressBar()
            .WithValue(0)
            .WithMaxValue(100)
            .WithFilledColor(Color.Yellow)
            .Stretch()
            .ShowPercentage()
            .WithMargin(2, 0, 2, 0)
            .Build();
        content.AddControl(progressBar);

        var tableBuilder = Controls.Markup().AddLine("");
        foreach (var result in completed)
        {
            string stars = ScoringEngine.StarString(result.Stars);
            tableBuilder.AddLine($"  [green]>[/] {result.Name,-18} {result.FPS,8:F0} {result.Score,8:F0}  {stars}");
        }
        tableBuilder.AddLine($"  [yellow]>[/] {testName,-18} [yellow]running...[/]");

        int remaining = totalTests - testIndex;
        for (int i = 0; i < remaining; i++)
        {
            int idx = testIndex + i;
            if (idx < TestInfo.Names.Length)
                tableBuilder.AddLine($"  [dim]o {TestInfo.Names[idx]}[/]");
        }
        content.AddControl(tableBuilder.Build());

        window.AddControl(content);

        // Sticky bottom toolbar
        var toolbar = Controls.Toolbar()
            .AddButton(
                Controls.Button(" STOP ")
                    .WithBorder(ButtonBorderStyle.Rounded)
                    .WithBackgroundColor(Color.Red)
                    .WithForegroundColor(Color.White)
                    .WithFocusedBackgroundColor(Color.Red1)
                    .WithFocusedForegroundColor(Color.White)
                    .OnClick((s, e) => onStop()))
            .WithMargin(2, 0, 2, 0)
            .StickyBottom()
            .Build();
        window.AddControl(toolbar);

        return progressBar;
    }

    /// <summary>
    /// Clears the window and populates the final results screen with bar graphs.
    /// </summary>
    public static void BuildResultsScreen(
        Window window,
        List<TestResult> results,
        string system,
        string terminal,
        string version,
        Action onRunAgain,
        Action onExit)
    {
        window.ClearControls();

        double totalScore = 0;
        foreach (var r in results)
            totalScore += r.Score;

        string rating = ScoringEngine.GetOverallRating(totalScore);
        string scoreColor = rating switch
        {
            "EXCELLENT" or "GREAT" => "green",
            "GOOD" => "yellow",
            _ => "red"
        };

        Color ratingColor = scoreColor switch
        {
            "green" => Color.Green,
            "yellow" => Color.Yellow,
            _ => Color.Red
        };

        // Scrollable content area
        var content = Controls.ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithBorderStyle(BorderStyle.None)
            .WithBackgroundColor(Color.Transparent)
            .Build();

        // Score as Figlet
        content.AddControl(Controls.Figlet($"{totalScore:F0}")
            .WithColor(ratingColor)
            .Centered()
            .Build());

        var ratingLabel = Controls.Markup()
            .AddLine($"[bold {scoreColor}]{rating}[/]")
            .Build();
        ratingLabel.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddControl(ratingLabel);

        content.AddControl(new RuleControl());

        // Results table
        const int NameColWidth = 18;
        const int FpsColWidth = 10;
        const int ScoreColWidth = 10;
        const int RatingColWidth = 12;

        var table = Controls.Table()
            .WithTitle("Benchmark Results")
            .AddColumn("Test", TextJustification.Left, NameColWidth)
            .AddColumn("FPS", TextJustification.Right, FpsColWidth)
            .AddColumn("Score", TextJustification.Right, ScoreColWidth)
            .AddColumn("Rating", TextJustification.Center, RatingColWidth)
            .Rounded()
            .ShowRowSeparators()
            .WithHeaderColors(Color.White, Color.MediumPurple)
            .WithHorizontalAlignment(HorizontalAlignment.Stretch)
            .Build();

        foreach (var result in results)
        {
            var stars = ScoringEngine.StarString(result.Stars);
            table.AddRow(result.Name, $"{result.FPS:F0}", $"{result.Score:F0}", stars);
        }
        content.AddControl(table);

        // Bar graphs
        double maxFps = 1;
        foreach (var r in results)
            if (r.FPS > maxFps) maxFps = r.FPS;

        foreach (var result in results)
        {
            var barColor = ScoringEngine.ColorForStars(result.Stars);
            content.AddControl(Controls.BarGraph()
                .WithLabel(result.Name)
                .WithLabelWidth(NameColWidth)
                .WithValue(result.FPS)
                .WithMaxValue(maxFps)
                .WithFilledColor(barColor)
                .ShowValue()
                .WithValueFormat("F0")
                .WithMargin(1, 0, 1, 0)
                .Build());
        }

        content.AddControl(Controls.Markup()
            .AddLine("")
            .AddLine($"  [dim]v{version} | {system} | {terminal}[/]")
            .Build());

        window.AddControl(content);

        // Separator before toolbar
        var sepMarkup = Controls.Markup().AddLine("").Build();
        sepMarkup.StickyPosition = StickyPosition.Bottom;
        window.AddControl(sepMarkup);
        var rule = new RuleControl() { StickyPosition = StickyPosition.Bottom };
        window.AddControl(rule);
        var sepMarkup2 = Controls.Markup().AddLine("").Build();
        sepMarkup2.StickyPosition = StickyPosition.Bottom;
        window.AddControl(sepMarkup2);

        // Sticky bottom toolbar
        var toolbar = Controls.Toolbar()
            .AddButton(
                Controls.Button(" RUN AGAIN ")
                    .WithBorder(ButtonBorderStyle.Rounded)
                    .WithBackgroundColor(Color.Green)
                    .WithForegroundColor(Color.Black)
                    .WithFocusedBackgroundColor(Color.Lime)
                    .WithFocusedForegroundColor(Color.Black)
                    .OnClick((s, e) => onRunAgain()))
            .AddSeparator(2)
            .AddButton(
                Controls.Button(" EXIT ")
                    .WithBorder(ButtonBorderStyle.Rounded)
                    .WithBackgroundColor(Color.Grey)
                    .WithForegroundColor(Color.Black)
                    .WithFocusedBackgroundColor(Color.White)
                    .WithFocusedForegroundColor(Color.Black)
                    .OnClick((s, e) => onExit()))
            .WithMargin(2, 0, 2, 0)
            .StickyBottom()
            .Build();
        window.AddControl(toolbar);
    }
}

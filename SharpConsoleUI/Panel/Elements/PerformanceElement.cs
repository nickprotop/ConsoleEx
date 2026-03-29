using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Panel;

/// <summary>
/// A panel element that displays performance metrics (FPS and dirty characters).
/// </summary>
public class PerformanceElement : PanelElement
{
    private const int EstimatedWidth = 15;

    /// <summary>
    /// Initializes a new PerformanceElement.
    /// </summary>
    /// <param name="name">Optional element name. Defaults to "performance".</param>
    public PerformanceElement(string? name = null)
        : base(name ?? "performance")
    {
    }

    /// <summary>
    /// Gets or sets whether to show FPS metric.
    /// </summary>
    public bool ShowFPS { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show dirty character count.
    /// </summary>
    public bool ShowDirtyChars { get; set; } = true;

    /// <inheritdoc/>
    public override int? FixedWidth => EstimatedWidth;

    /// <inheritdoc/>
    public override void Render(CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg)
    {
        if (WindowSystem?.Performance == null)
            return;

        var perf = WindowSystem.Performance;
        string text;

        if (perf.IsPerformanceMetricsEnabled)
        {
            var parts = new List<string>();
            if (ShowFPS)
                parts.Add($"FPS:{perf.CurrentFPS:F0}");
            if (ShowDirtyChars)
                parts.Add($"D:{perf.CurrentDirtyChars}");
            text = $"[dim]{string.Join(" ", parts)}[/]";

            // Metrics change every frame
            Invalidate();
        }
        else
        {
            text = "[dim]Metrics:OFF[/]";
        }

        var cells = MarkupParser.Parse(text, fg, bg);
        var clipRect = new LayoutRect(x, y, width, 1);
        buffer.WriteCellsClipped(x, y, cells, clipRect);
    }
}

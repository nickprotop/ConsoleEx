using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Panel;

/// <summary>
/// A panel element that displays a clickable list of windows (task bar).
/// </summary>
public class TaskBarElement : PanelElement
{
    private const int MaxTitleLength = 15;
    private const int TitleEllipsisLength = 7;
    private List<(Window window, int startX, int endX)> _windowPositions = new();
    private int _lastStateHash;

    /// <summary>
    /// Initializes a new TaskBarElement.
    /// </summary>
    /// <param name="name">Optional element name. Defaults to "taskbar".</param>
    public TaskBarElement(string? name = null)
        : base(name ?? "taskbar")
    {
    }

    /// <summary>
    /// Gets or sets the highlight color for the active window.
    /// </summary>
    public Color? ActiveColor { get; set; }

    /// <summary>
    /// Gets or sets the color for inactive windows.
    /// </summary>
    public Color? InactiveColor { get; set; }

    /// <summary>
    /// Gets or sets whether minimized windows are displayed with dimmed text.
    /// </summary>
    public bool MinimizedDim { get; set; } = true;

    /// <inheritdoc/>
    public override int FlexGrow => 1;

    /// <inheritdoc/>
    public override void Render(CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg)
    {
        if (WindowSystem == null || width <= 0)
            return;

        _windowPositions.Clear();

        // Get top-level taskbar windows sorted by creation order
        var windows = new List<Window>();
        foreach (var w in WindowSystem.Windows.Values)
        {
            if (w.ParentWindow == null && w.ShowInTaskbar)
                windows.Add(w);
        }
        windows.Sort((a, b) => a.CreationOrder.CompareTo(b.CreationOrder));

        // Check if state changed
        int stateHash = ComputeStateHash(windows);
        if (stateHash != _lastStateHash)
        {
            _lastStateHash = stateHash;
            // State changed — will render fresh
        }

        var activeWindow = WindowSystem.WindowStateService.ActiveWindow;
        int writeX = x;
        int endX = x + width;

        for (int i = 0; i < windows.Count; i++)
        {
            if (writeX >= endX)
                break;

            var w = windows[i];
            bool isActive = w == activeWindow;
            bool isMinimized = w.State == WindowState.Minimized;

            // Build entry markup
            string title = StringHelper.TrimWithEllipsis(w.Title, MaxTitleLength, TitleEllipsisLength);
            string markup;
            if (isActive)
            {
                markup = $"[bold]Alt-{i + 1}[/] {title}";
            }
            else if (isMinimized && MinimizedDim)
            {
                markup = $"[bold]Alt-{i + 1}[/] [dim]{title}[/]";
            }
            else
            {
                markup = $"[bold]Alt-{i + 1}[/] {title}";
            }

            // Add separator
            if (i < windows.Count - 1)
                markup += " | ";

            var cells = MarkupParser.Parse(markup, fg, bg);
            int entryWidth = cells.Count;
            int availableWidth = endX - writeX;

            if (entryWidth > availableWidth)
            {
                // Truncate to fit
                cells = cells.GetRange(0, availableWidth);
                entryWidth = availableWidth;
            }

            int entryStartX = writeX;
            var clipRect = new LayoutRect(writeX, y, entryWidth, 1);
            buffer.WriteCellsClipped(writeX, y, cells, clipRect);

            _windowPositions.Add((w, entryStartX, writeX + entryWidth));
            writeX += entryWidth;
        }
    }

    /// <inheritdoc/>
    public override bool ProcessMouseEvent(Events.MouseEventArgs args, int elementX, int elementWidth)
    {
        if (WindowSystem == null)
            return false;

        if (!args.HasFlag(Drivers.MouseFlags.Button1Pressed) && !args.HasFlag(Drivers.MouseFlags.Button1Clicked))
            return false;

        int mouseX = args.Position.X;
        foreach (var (window, startX, endX) in _windowPositions)
        {
            if (mouseX >= startX && mouseX < endX)
            {
                if (window.State == WindowState.Minimized)
                {
                    window.Restore();
                }
                WindowSystem.WindowStateService.ActivateWindow(window);
                return true;
            }
        }
        return false;
    }

    private static int ComputeStateHash(List<Window> windows)
    {
        unchecked
        {
            int hash = 17;
            foreach (var w in windows)
            {
                hash = hash * 31 + w.Title.GetHashCode();
                hash = hash * 31 + (int)w.State;
                hash = hash * 31 + (w == w.GetConsoleWindowSystem?.WindowStateService.ActiveWindow ? 1 : 0);
            }
            return hash;
        }
    }
}

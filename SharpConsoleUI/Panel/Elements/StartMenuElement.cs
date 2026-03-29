using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Panel;

/// <summary>
/// A panel element that renders a start menu button and opens the StartMenuDialog on click.
/// </summary>
public class StartMenuElement : PanelElement
{
    private const int ButtonPadding = 1;
    private string _text = "\u2630 Start";
    private int? _cachedWidth;

    /// <summary>
    /// Initializes a new StartMenuElement.
    /// </summary>
    /// <param name="name">Optional element name. Defaults to "startmenu".</param>
    public StartMenuElement(string? name = null)
        : base(name ?? "startmenu")
    {
    }

    /// <summary>
    /// Gets or sets the button text (supports markup).
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                _cachedWidth = null;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the button foreground color.
    /// </summary>
    public Color? ButtonForeground { get; set; }

    /// <summary>
    /// Gets or sets the button background color.
    /// </summary>
    public Color? ButtonBackground { get; set; }

    /// <inheritdoc/>
    public override int? FixedWidth => MeasureWidth();

    /// <inheritdoc/>
    public override int MeasureWidth()
    {
        _cachedWidth ??= MarkupParser.StripLength(_text) + ButtonPadding;
        return _cachedWidth.Value;
    }

    /// <inheritdoc/>
    public override void Render(CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg)
    {
        var effectiveFg = ButtonForeground ?? fg;
        var effectiveBg = ButtonBackground ?? bg;

        string markup = $"[bold cyan]{_text}[/] ";
        var cells = MarkupParser.Parse(markup, effectiveFg, effectiveBg);
        var clipRect = new LayoutRect(x, y, width, 1);
        buffer.WriteCellsClipped(x, y, cells, clipRect);
    }

    /// <inheritdoc/>
    public override bool ProcessMouseEvent(Events.MouseEventArgs args, int elementX, int elementWidth)
    {
        if (WindowSystem == null)
            return false;

        if (args.HasFlag(Drivers.MouseFlags.Button1Pressed) || args.HasFlag(Drivers.MouseFlags.Button1Clicked))
        {
            WindowSystem.PanelStateService.ShowStartMenu();
            return true;
        }
        return false;
    }
}

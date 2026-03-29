using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Panel;

/// <summary>
/// A panel element that displays markup text. Text can be updated at runtime.
/// </summary>
public class StatusTextElement : PanelElement
{
    private string _text;
    private int? _cachedWidth;

    /// <summary>
    /// Initializes a new StatusTextElement with the given text.
    /// </summary>
    /// <param name="text">The markup text to display.</param>
    /// <param name="name">Optional element name. Defaults to "statustext".</param>
    public StatusTextElement(string text, string? name = null)
        : base(name ?? "statustext")
    {
        _text = text;
    }

    /// <summary>
    /// Gets or sets the markup text to display. Setting triggers a redraw.
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
    /// Gets or sets an optional text color override.
    /// </summary>
    public Color? TextColor { get; set; }

    /// <summary>
    /// Gets or sets an optional click handler.
    /// </summary>
    public Action? ClickHandler { get; set; }

    /// <inheritdoc/>
    public override int MeasureWidth()
    {
        _cachedWidth ??= MarkupParser.StripLength(_text);
        return _cachedWidth.Value;
    }

    /// <inheritdoc/>
    public override void Render(CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg)
    {
        var effectiveFg = TextColor ?? fg;
        var cells = MarkupParser.Parse(_text, effectiveFg, bg);
        var clipRect = new LayoutRect(x, y, width, 1);
        buffer.WriteCellsClipped(x, y, cells, clipRect);
    }

    /// <inheritdoc/>
    public override bool ProcessMouseEvent(Events.MouseEventArgs args, int elementX, int elementWidth)
    {
        if (ClickHandler != null &&
            (args.HasFlag(Drivers.MouseFlags.Button1Pressed) || args.HasFlag(Drivers.MouseFlags.Button1Clicked)))
        {
            ClickHandler();
            return true;
        }
        return false;
    }
}

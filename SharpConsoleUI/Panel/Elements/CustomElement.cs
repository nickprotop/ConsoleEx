using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Panel;

/// <summary>
/// A panel element with user-provided render and click callbacks.
/// </summary>
public class CustomElement : PanelElement
{
    private readonly int? _fixedWidth;
    private readonly int _flexGrow;

    /// <summary>
    /// Initializes a new CustomElement.
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <param name="fixedWidth">Optional fixed width.</param>
    /// <param name="flexGrow">Optional flex grow factor.</param>
    public CustomElement(string name, int? fixedWidth = null, int flexGrow = 0)
        : base(name)
    {
        _fixedWidth = fixedWidth;
        _flexGrow = flexGrow;
    }

    /// <summary>
    /// Gets or sets the render callback.
    /// </summary>
    public Action<CharacterBuffer, int, int, int, Color, Color>? RenderCallback { get; set; }

    /// <summary>
    /// Gets or sets the click handler.
    /// </summary>
    public Action? ClickHandler { get; set; }

    /// <inheritdoc/>
    public override int? FixedWidth => _fixedWidth;

    /// <inheritdoc/>
    public override int FlexGrow => _flexGrow;

    /// <inheritdoc/>
    public override void Render(CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg)
    {
        RenderCallback?.Invoke(buffer, x, y, width, fg, bg);
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

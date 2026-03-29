using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Panel;

/// <summary>
/// A panel element that displays a single separator character.
/// </summary>
public class SeparatorElement : PanelElement
{
    /// <summary>
    /// Initializes a new SeparatorElement.
    /// </summary>
    /// <param name="name">Optional element name. Defaults to "separator".</param>
    public SeparatorElement(string? name = null)
        : base(name ?? "separator")
    {
    }

    /// <summary>
    /// Gets or sets the separator character to display.
    /// </summary>
    public char SeparatorChar { get; set; } = '│';

    /// <summary>
    /// Gets or sets an optional separator color override.
    /// </summary>
    public Color? SeparatorColor { get; set; }

    /// <inheritdoc/>
    public override int? FixedWidth => 1;

    /// <inheritdoc/>
    public override void Render(CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg)
    {
        var effectiveFg = SeparatorColor ?? fg;
        buffer.SetNarrowCell(x, y, SeparatorChar, effectiveFg, bg);
    }
}

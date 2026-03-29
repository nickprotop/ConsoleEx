namespace SharpConsoleUI.Panel.Builders;

/// <summary>
/// Fluent builder for SeparatorElement.
/// </summary>
public class SeparatorElementBuilder : IPanelElementBuilder
{
    private string? _name;
    private char _char = '│';
    private Color? _color;

    /// <summary>
    /// Sets the element name.
    /// </summary>
    public SeparatorElementBuilder WithName(string name) { _name = name; return this; }

    /// <summary>
    /// Sets the separator character.
    /// </summary>
    public SeparatorElementBuilder WithChar(char c) { _char = c; return this; }

    /// <summary>
    /// Sets the separator color.
    /// </summary>
    public SeparatorElementBuilder WithColor(Color color) { _color = color; return this; }

    /// <inheritdoc/>
    public IPanelElement Build()
    {
        return new SeparatorElement(_name)
        {
            SeparatorChar = _char,
            SeparatorColor = _color
        };
    }
}

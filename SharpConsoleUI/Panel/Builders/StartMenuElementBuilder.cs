namespace SharpConsoleUI.Panel.Builders;

/// <summary>
/// Fluent builder for StartMenuElement.
/// </summary>
public class StartMenuElementBuilder : IPanelElementBuilder
{
    private string? _name;
    private string _text = "\u2630 Start";
    private Color? _foreground;
    private Color? _background;

    /// <summary>
    /// Sets the element name.
    /// </summary>
    public StartMenuElementBuilder WithName(string name) { _name = name; return this; }

    /// <summary>
    /// Sets the button text.
    /// </summary>
    public StartMenuElementBuilder WithText(string text) { _text = text; return this; }

    /// <summary>
    /// Sets the button colors.
    /// </summary>
    public StartMenuElementBuilder WithColors(Color fg, Color bg)
    {
        _foreground = fg;
        _background = bg;
        return this;
    }

    /// <inheritdoc/>
    public IPanelElement Build()
    {
        return new StartMenuElement(_name)
        {
            Text = _text,
            ButtonForeground = _foreground,
            ButtonBackground = _background
        };
    }
}

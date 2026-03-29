namespace SharpConsoleUI.Panel.Builders;

/// <summary>
/// Fluent builder for StatusTextElement.
/// </summary>
public class StatusTextElementBuilder : IPanelElementBuilder
{
    private readonly string _text;
    private string? _name;
    private Color? _color;
    private Action? _clickHandler;

    /// <summary>
    /// Initializes a new builder with the specified text.
    /// </summary>
    /// <param name="text">The markup text to display.</param>
    public StatusTextElementBuilder(string text)
    {
        _text = text;
    }

    /// <summary>
    /// Sets the element name.
    /// </summary>
    public StatusTextElementBuilder WithName(string name) { _name = name; return this; }

    /// <summary>
    /// Sets the text color.
    /// </summary>
    public StatusTextElementBuilder WithColor(Color color) { _color = color; return this; }

    /// <summary>
    /// Sets the click handler.
    /// </summary>
    public StatusTextElementBuilder OnClick(Action handler) { _clickHandler = handler; return this; }

    /// <inheritdoc/>
    public IPanelElement Build()
    {
        return new StatusTextElement(_text, _name)
        {
            TextColor = _color,
            ClickHandler = _clickHandler
        };
    }
}

namespace SharpConsoleUI.Panel.Builders;

/// <summary>
/// Fluent builder for ClockElement.
/// </summary>
public class ClockElementBuilder : IPanelElementBuilder
{
    private string? _name;
    private string _format = "HH:mm";
    private int _updateIntervalMs = 1000;
    private Color? _textColor;

    /// <summary>
    /// Sets the element name.
    /// </summary>
    public ClockElementBuilder WithName(string name) { _name = name; return this; }

    /// <summary>
    /// Sets the time format string.
    /// </summary>
    public ClockElementBuilder WithFormat(string format) { _format = format; return this; }

    /// <summary>
    /// Sets the update interval in milliseconds.
    /// </summary>
    public ClockElementBuilder WithUpdateInterval(int ms) { _updateIntervalMs = ms; return this; }

    /// <summary>
    /// Sets the text color.
    /// </summary>
    public ClockElementBuilder WithColor(Color color) { _textColor = color; return this; }

    /// <inheritdoc/>
    public IPanelElement Build()
    {
        return new ClockElement(_name)
        {
            Format = _format,
            UpdateIntervalMs = _updateIntervalMs,
            TextColor = _textColor
        };
    }
}

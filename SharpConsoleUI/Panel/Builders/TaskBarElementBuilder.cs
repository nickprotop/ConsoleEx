namespace SharpConsoleUI.Panel.Builders;

/// <summary>
/// Fluent builder for TaskBarElement.
/// </summary>
public class TaskBarElementBuilder : IPanelElementBuilder
{
    private string? _name;
    private Color? _activeColor;
    private Color? _inactiveColor;
    private bool _minimizedDim = true;

    /// <summary>
    /// Sets the element name.
    /// </summary>
    public TaskBarElementBuilder WithName(string name) { _name = name; return this; }

    /// <summary>
    /// Sets the highlight color for the active window.
    /// </summary>
    public TaskBarElementBuilder WithActiveColor(Color color) { _activeColor = color; return this; }

    /// <summary>
    /// Sets the color for inactive windows.
    /// </summary>
    public TaskBarElementBuilder WithInactiveColor(Color color) { _inactiveColor = color; return this; }

    /// <summary>
    /// Sets whether minimized windows are dimmed.
    /// </summary>
    public TaskBarElementBuilder WithMinimizedDim(bool dim) { _minimizedDim = dim; return this; }

    /// <inheritdoc/>
    public IPanelElement Build()
    {
        return new TaskBarElement(_name)
        {
            ActiveColor = _activeColor,
            InactiveColor = _inactiveColor,
            MinimizedDim = _minimizedDim
        };
    }
}

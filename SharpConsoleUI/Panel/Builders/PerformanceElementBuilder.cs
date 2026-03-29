namespace SharpConsoleUI.Panel.Builders;

/// <summary>
/// Fluent builder for PerformanceElement.
/// </summary>
public class PerformanceElementBuilder : IPanelElementBuilder
{
    private string? _name;
    private bool _showFPS = true;
    private bool _showDirtyChars = true;

    /// <summary>
    /// Sets the element name.
    /// </summary>
    public PerformanceElementBuilder WithName(string name) { _name = name; return this; }

    /// <summary>
    /// Sets whether to show FPS.
    /// </summary>
    public PerformanceElementBuilder ShowFPS(bool show) { _showFPS = show; return this; }

    /// <summary>
    /// Sets whether to show dirty character count.
    /// </summary>
    public PerformanceElementBuilder ShowDirtyChars(bool show) { _showDirtyChars = show; return this; }

    /// <inheritdoc/>
    public IPanelElement Build()
    {
        return new PerformanceElement(_name)
        {
            ShowFPS = _showFPS,
            ShowDirtyChars = _showDirtyChars
        };
    }
}

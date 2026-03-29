using SharpConsoleUI.Panel.Builders;

namespace SharpConsoleUI.Panel;

/// <summary>
/// Static factory for creating panel element builders.
/// Usage: Elements.StartMenu(), Elements.TaskBar(), Elements.Clock(), etc.
/// </summary>
public static class Elements
{
    /// <summary>
    /// Creates a StatusTextElement builder with the specified markup text.
    /// </summary>
    /// <param name="text">The markup text to display.</param>
    public static StatusTextElementBuilder StatusText(string text) => new(text);

    /// <summary>
    /// Creates a SeparatorElement builder.
    /// </summary>
    public static SeparatorElementBuilder Separator() => new();

    /// <summary>
    /// Creates a StartMenuElement builder.
    /// </summary>
    public static StartMenuElementBuilder StartMenu() => new();

    /// <summary>
    /// Creates a TaskBarElement builder.
    /// </summary>
    public static TaskBarElementBuilder TaskBar() => new();

    /// <summary>
    /// Creates a ClockElement builder.
    /// </summary>
    public static ClockElementBuilder Clock() => new();

    /// <summary>
    /// Creates a PerformanceElement builder.
    /// </summary>
    public static PerformanceElementBuilder Performance() => new();

    /// <summary>
    /// Creates a CustomElement builder with the specified name.
    /// </summary>
    /// <param name="name">The element name.</param>
    public static CustomElementBuilder Custom(string name) => new(name);
}

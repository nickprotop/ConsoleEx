using SharpConsoleUI.Configuration;

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
    private StartMenuOptions? _options;
    private ConsoleKey? _shortcutKey;
    private ConsoleModifiers? _shortcutModifiers;

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

    /// <summary>
    /// Sets the start menu options (layout, colors, categories, etc.).
    /// </summary>
    public StartMenuElementBuilder WithOptions(StartMenuOptions options)
    {
        _options = options;
        return this;
    }

    /// <summary>
    /// Sets the keyboard shortcut for toggling this start menu.
    /// </summary>
    public StartMenuElementBuilder WithShortcutKey(ConsoleKey key, ConsoleModifiers modifiers = ConsoleModifiers.Control)
    {
        _shortcutKey = key;
        _shortcutModifiers = modifiers;
        return this;
    }

    /// <inheritdoc/>
    public IPanelElement Build()
    {
        var element = new StartMenuElement(_name)
        {
            Text = _text,
            ButtonForeground = _foreground,
            ButtonBackground = _background
        };

        if (_options != null)
            element.Options = _options;
        if (_shortcutKey.HasValue)
            element.ShortcutKey = _shortcutKey.Value;
        if (_shortcutModifiers.HasValue)
            element.ShortcutModifiers = _shortcutModifiers.Value;

        return element;
    }
}

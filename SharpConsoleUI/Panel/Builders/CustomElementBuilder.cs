using SharpConsoleUI.Layout;
namespace SharpConsoleUI.Panel.Builders;

/// <summary>
/// Fluent builder for CustomElement.
/// </summary>
public class CustomElementBuilder : IPanelElementBuilder
{
    private readonly string _name;
    private int? _fixedWidth;
    private int _flexGrow;
    private Action<CharacterBuffer, int, int, int, Color, Color>? _renderCallback;
    private Action? _clickHandler;

    /// <summary>
    /// Initializes a new builder with the specified name.
    /// </summary>
    /// <param name="name">The element name.</param>
    public CustomElementBuilder(string name)
    {
        _name = name;
    }

    /// <summary>
    /// Sets a fixed width.
    /// </summary>
    public CustomElementBuilder WithFixedWidth(int width) { _fixedWidth = width; return this; }

    /// <summary>
    /// Sets the flex grow factor.
    /// </summary>
    public CustomElementBuilder WithFlexGrow(int grow) { _flexGrow = grow; return this; }

    /// <summary>
    /// Sets the render callback.
    /// </summary>
    public CustomElementBuilder WithRenderCallback(Action<CharacterBuffer, int, int, int, Color, Color> callback)
    {
        _renderCallback = callback;
        return this;
    }

    /// <summary>
    /// Sets the click handler.
    /// </summary>
    public CustomElementBuilder WithClickHandler(Action handler)
    {
        _clickHandler = handler;
        return this;
    }

    /// <inheritdoc/>
    public IPanelElement Build()
    {
        return new CustomElement(_name, _fixedWidth, _flexGrow)
        {
            RenderCallback = _renderCallback,
            ClickHandler = _clickHandler
        };
    }
}

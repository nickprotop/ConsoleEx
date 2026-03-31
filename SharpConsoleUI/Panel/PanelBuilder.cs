namespace SharpConsoleUI.Panel;

/// <summary>
/// Fluent builder for constructing Panel instances.
/// </summary>
public class PanelBuilder
{
    private readonly List<IPanelElement> _left = new();
    private readonly List<IPanelElement> _center = new();
    private readonly List<IPanelElement> _right = new();
    private Color? _backgroundColor;
    private Color? _foregroundColor;
    private bool _visible = true;

    /// <summary>
    /// Adds a single element to the left zone.
    /// </summary>
    public PanelBuilder Left(IPanelElement element) { _left.Add(element); return this; }

    /// <summary>
    /// Adds a single builder's element to the left zone.
    /// </summary>
    public PanelBuilder Left(IPanelElementBuilder builder) { _left.Add(builder.Build()); return this; }

    /// <summary>
    /// Adds elements to the left zone. Accepts IPanelElement or IPanelElementBuilder instances.
    /// </summary>
    public PanelBuilder Left(params object[] items)
    {
        foreach (var item in items)
            _left.Add(Resolve(item));
        return this;
    }

    /// <summary>
    /// Adds a single element to the center zone.
    /// </summary>
    public PanelBuilder Center(IPanelElement element) { _center.Add(element); return this; }

    /// <summary>
    /// Adds a single builder's element to the center zone.
    /// </summary>
    public PanelBuilder Center(IPanelElementBuilder builder) { _center.Add(builder.Build()); return this; }

    /// <summary>
    /// Adds elements to the center zone. Accepts IPanelElement or IPanelElementBuilder instances.
    /// </summary>
    public PanelBuilder Center(params object[] items)
    {
        foreach (var item in items)
            _center.Add(Resolve(item));
        return this;
    }

    /// <summary>
    /// Adds a single element to the right zone.
    /// </summary>
    public PanelBuilder Right(IPanelElement element) { _right.Add(element); return this; }

    /// <summary>
    /// Adds a single builder's element to the right zone.
    /// </summary>
    public PanelBuilder Right(IPanelElementBuilder builder) { _right.Add(builder.Build()); return this; }

    /// <summary>
    /// Adds elements to the right zone. Accepts IPanelElement or IPanelElementBuilder instances.
    /// </summary>
    public PanelBuilder Right(params object[] items)
    {
        foreach (var item in items)
            _right.Add(Resolve(item));
        return this;
    }

    /// <summary>
    /// Sets the panel background color.
    /// </summary>
    /// <param name="color">The background color.</param>
    /// <returns>This builder for chaining.</returns>
    public PanelBuilder WithBackgroundColor(Color color)
    {
        _backgroundColor = color;
        return this;
    }

    /// <summary>
    /// Sets the panel foreground color.
    /// </summary>
    /// <param name="color">The foreground color.</param>
    /// <returns>This builder for chaining.</returns>
    public PanelBuilder WithForegroundColor(Color color)
    {
        _foregroundColor = color;
        return this;
    }

    /// <summary>
    /// Sets whether the panel is visible.
    /// </summary>
    /// <param name="visible">True to show the panel.</param>
    /// <returns>This builder for chaining.</returns>
    public PanelBuilder Visible(bool visible = true)
    {
        _visible = visible;
        return this;
    }

    /// <summary>
    /// Builds the panel with the configured settings.
    /// </summary>
    /// <returns>A new Panel instance.</returns>
    public Panel Build()
    {
        var panel = new Panel
        {
            Visible = _visible,
            BackgroundColor = _backgroundColor,
            ForegroundColor = _foregroundColor
        };

        if (_left.Count > 0)
            panel.AddLeft(_left.ToArray());
        if (_center.Count > 0)
            panel.AddCenter(_center.ToArray());
        if (_right.Count > 0)
            panel.AddRight(_right.ToArray());

        return panel;
    }

    private static IPanelElement Resolve(object item)
    {
        return item switch
        {
            IPanelElement element => element,
            IPanelElementBuilder builder => builder.Build(),
            _ => throw new ArgumentException($"Expected IPanelElement or IPanelElementBuilder, got {item.GetType().Name}")
        };
    }
}

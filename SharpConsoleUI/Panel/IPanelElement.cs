namespace SharpConsoleUI.Panel;

/// <summary>
/// Contract for anything that can live inside a Panel.
/// Elements are self-contained widgets (start menu, taskbar, clock, etc.)
/// that handle their own rendering and mouse input.
/// </summary>
public interface IPanelElement
{
    /// <summary>
    /// Gets the unique name of this element.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets or sets whether this element is visible.
    /// </summary>
    bool Visible { get; set; }

    /// <summary>
    /// Gets the fixed width of this element, or null if it uses MeasureWidth() or flex sizing.
    /// </summary>
    int? FixedWidth { get; }

    /// <summary>
    /// Gets the flex grow factor. 0 = fixed sizing, &gt;0 = flex proportional to other flex elements.
    /// </summary>
    int FlexGrow { get; }

    /// <summary>
    /// Gets the minimum width constraint for flex elements.
    /// </summary>
    int? MinWidth { get; }

    /// <summary>
    /// Gets the maximum width constraint for flex elements.
    /// </summary>
    int? MaxWidth { get; }

    /// <summary>
    /// Measures the natural content width of this element.
    /// Used when FixedWidth is null and FlexGrow is 0.
    /// </summary>
    /// <returns>The measured width in columns.</returns>
    int MeasureWidth();

    /// <summary>
    /// Renders the element into the character buffer at the specified position.
    /// </summary>
    /// <param name="buffer">The character buffer to render into.</param>
    /// <param name="x">The x position to render at.</param>
    /// <param name="y">The y position to render at.</param>
    /// <param name="width">The allocated width for this element.</param>
    /// <param name="fg">The default foreground color.</param>
    /// <param name="bg">The default background color.</param>
    void Render(Layout.CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg);

    /// <summary>
    /// Processes a mouse event that occurred within this element's bounds.
    /// </summary>
    /// <param name="args">The mouse event arguments.</param>
    /// <param name="elementX">The x position of this element on screen.</param>
    /// <param name="elementWidth">The allocated width of this element.</param>
    /// <returns>True if the event was handled.</returns>
    bool ProcessMouseEvent(Events.MouseEventArgs args, int elementX, int elementWidth);

    /// <summary>
    /// Called when this element is added to a panel.
    /// </summary>
    /// <param name="panel">The parent panel.</param>
    void OnAttached(Panel panel);

    /// <summary>
    /// Called when this element is removed from a panel.
    /// </summary>
    void OnDetached();
}

/// <summary>
/// Interface for panel element builders that produce IPanelElement instances.
/// </summary>
public interface IPanelElementBuilder
{
    /// <summary>
    /// Builds and returns the configured panel element.
    /// </summary>
    IPanelElement Build();
}

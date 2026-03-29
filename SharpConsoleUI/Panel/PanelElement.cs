namespace SharpConsoleUI.Panel;

/// <summary>
/// Abstract base class implementing IPanelElement with sensible defaults.
/// Elements that don't need to inherit from something else should extend this.
/// </summary>
public abstract class PanelElement : IPanelElement
{
    /// <summary>
    /// Initializes a new PanelElement with the given name.
    /// </summary>
    /// <param name="name">The unique name of this element.</param>
    protected PanelElement(string name)
    {
        Name = name;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool Visible { get; set; } = true;

    /// <inheritdoc/>
    public virtual int? FixedWidth => null;

    /// <inheritdoc/>
    public virtual int FlexGrow => 0;

    /// <inheritdoc/>
    public virtual int? MinWidth => null;

    /// <inheritdoc/>
    public virtual int? MaxWidth => null;

    /// <summary>
    /// Gets the parent panel this element is attached to, or null if detached.
    /// </summary>
    protected Panel? Owner { get; private set; }

    /// <summary>
    /// Gets the console window system via the parent panel, or null if not attached.
    /// </summary>
    protected ConsoleWindowSystem? WindowSystem => Owner?.WindowSystem;

    /// <inheritdoc/>
    public virtual int MeasureWidth() => FixedWidth ?? 0;

    /// <inheritdoc/>
    public abstract void Render(Layout.CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg);

    /// <inheritdoc/>
    public virtual bool ProcessMouseEvent(Events.MouseEventArgs args, int elementX, int elementWidth) => false;

    /// <inheritdoc/>
    public void OnAttached(Panel panel)
    {
        Owner = panel;
    }

    /// <inheritdoc/>
    public void OnDetached()
    {
        Owner = null;
    }

    /// <summary>
    /// Notifies the parent panel that this element needs to be redrawn.
    /// </summary>
    protected void Invalidate()
    {
        Owner?.MarkDirty();
    }
}

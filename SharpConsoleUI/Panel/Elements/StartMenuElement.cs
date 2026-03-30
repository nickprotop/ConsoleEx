using SharpConsoleUI.Configuration;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Models;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Panel;

/// <summary>
/// A panel element that renders a start menu button and opens the StartMenuDialog on click.
/// Owns its own configuration, keyboard shortcut, and registered actions.
/// </summary>
public class StartMenuElement : PanelElement
{
    private const int ButtonPadding = 1;
    private string _text = "\u2630 Start";
    private int? _cachedWidth;
    private readonly List<StartMenuAction> _actions = new();

    /// <summary>
    /// Initializes a new StartMenuElement.
    /// </summary>
    /// <param name="name">Optional element name. Defaults to "startmenu".</param>
    public StartMenuElement(string? name = null)
        : base(name ?? "startmenu")
    {
    }

    #region Configuration

    /// <summary>
    /// Gets or sets the start menu options (layout, colors, categories, etc.).
    /// </summary>
    public StartMenuOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the keyboard shortcut key for toggling this start menu.
    /// </summary>
    public ConsoleKey ShortcutKey { get; set; } = ConsoleKey.Spacebar;

    /// <summary>
    /// Gets or sets the keyboard shortcut modifier keys for toggling this start menu.
    /// </summary>
    public ConsoleModifiers ShortcutModifiers { get; set; } = ConsoleModifiers.Control;

    #endregion

    #region Button Appearance

    /// <summary>
    /// Gets or sets the button text (supports markup).
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                _cachedWidth = null;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the button foreground color.
    /// </summary>
    public Color? ButtonForeground { get; set; }

    /// <summary>
    /// Gets or sets the button background color.
    /// </summary>
    public Color? ButtonBackground { get; set; }

    #endregion

    #region Menu Window Tracking

    /// <summary>
    /// Gets or sets the currently open start menu window for this element, if any.
    /// Used for toggle behavior — if non-null, the menu is open.
    /// </summary>
    internal Window? MenuWindow { get; set; }

    #endregion

    #region Actions

    /// <summary>
    /// Registers a new action in this start menu.
    /// </summary>
    /// <param name="name">Display name of the action.</param>
    /// <param name="callback">Callback to execute when action is selected.</param>
    /// <param name="category">Optional category for grouping actions.</param>
    /// <param name="order">Display order (lower values appear first).</param>
    public void RegisterAction(string name, Action callback, string? category = null, int order = 0)
    {
        _actions.Add(new StartMenuAction(name, callback, category, order));
    }

    /// <summary>
    /// Removes an action from this start menu by name.
    /// </summary>
    /// <param name="name">Name of the action to remove.</param>
    public void UnregisterAction(string name)
    {
        _actions.RemoveAll(a => a.Name == name);
    }

    /// <summary>
    /// Gets all registered actions for this start menu.
    /// </summary>
    public IReadOnlyList<StartMenuAction> GetActions() => _actions.AsReadOnly();

    #endregion

    #region Positioning

    /// <summary>
    /// Gets the rendered bounds (x, width) of this element on screen.
    /// Returns null if not yet rendered.
    /// </summary>
    public (int x, int width)? GetBounds() => Owner?.GetElementBounds<StartMenuElement>();

    /// <summary>
    /// Gets whether this element is in the bottom panel.
    /// </summary>
    public bool IsInBottomPanel =>
        Owner != null && WindowSystem != null &&
        ReferenceEquals(Owner, WindowSystem.PanelStateService.BottomPanel);

    // Make Owner accessible to the dialog for positioning
    internal new Panel? Owner => base.Owner;

    #endregion

    #region Show / Toggle

    /// <summary>
    /// Shows or toggles the start menu dialog for this element.
    /// </summary>
    public void Show()
    {
        if (WindowSystem is ConsoleWindowSystem ws)
        {
            StartMenuDialog.Show(ws, this);
        }
    }

    #endregion

    #region IPanelElement

    /// <inheritdoc/>
    public override int? FixedWidth => MeasureWidth();

    /// <inheritdoc/>
    public override int MeasureWidth()
    {
        _cachedWidth ??= MarkupParser.StripLength(_text) + ButtonPadding;
        return _cachedWidth.Value;
    }

    /// <inheritdoc/>
    public override void Render(CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg)
    {
        var effectiveFg = ButtonForeground ?? fg;
        var effectiveBg = ButtonBackground ?? bg;

        string markup = $"[bold cyan]{_text}[/] ";
        var cells = MarkupParser.Parse(markup, effectiveFg, effectiveBg);
        var clipRect = new LayoutRect(x, y, width, 1);
        buffer.WriteCellsClipped(x, y, cells, clipRect);
    }

    /// <inheritdoc/>
    public override bool ProcessMouseEvent(Events.MouseEventArgs args, int elementX, int elementWidth)
    {
        if (WindowSystem == null)
            return false;

        if (args.HasFlag(Drivers.MouseFlags.Button1Pressed) || args.HasFlag(Drivers.MouseFlags.Button1Clicked))
        {
            Show();
            return true;
        }
        return false;
    }

    #endregion
}

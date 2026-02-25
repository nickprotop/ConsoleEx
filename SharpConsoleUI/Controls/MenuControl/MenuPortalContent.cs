using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Internal portal content control for rendering menu dropdowns as overlays.
/// This control is created by MenuControl when a dropdown opens and is added as a portal child
/// to render outside the normal bounds of the menu control.
/// </summary>
internal class MenuPortalContent : PortalContentBase
{
    private readonly MenuControl _owner;
    private readonly MenuDropdown _dropdown;

    public MenuPortalContent(MenuControl owner, MenuDropdown dropdown)
    {
        _owner = owner;
        _dropdown = dropdown;
    }

    /// <inheritdoc/>
    public override Rectangle GetPortalBounds() => _dropdown.Bounds;

    /// <inheritdoc/>
    public override bool ProcessMouseEvent(MouseEventArgs args)
    {
        return _owner.ProcessDropdownMouseEvent(_dropdown, args);
    }

    /// <inheritdoc/>
    protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
        LayoutRect clipRect, Color defaultFg, Color defaultBg)
    {
        _owner.PaintDropdownInternal(buffer, _dropdown, clipRect);
    }
}

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls;

public partial class MenuControl
{
    #region IDOMPaintable Implementation

    /// <inheritdoc/>
    public override LayoutSize MeasureDOM(LayoutConstraints constraints)
    {
        int width, height;

        if (_orientation == MenuOrientation.Horizontal)
        {
            // Calculate total width of all menu items
            int totalWidth = 0;
            foreach (var item in _items)
            {
                if (!item.IsSeparator)
                    totalWidth += MeasureText(item.Text) + Configuration.ControlDefaults.MenuItemHorizontalPadding;
            }

            width = Width ?? totalWidth;
            height = 1;
        }
        else // Vertical
        {
            // Calculate max width and total height
            int maxWidth = 0;
            foreach (var item in _items)
            {
                if (!item.IsSeparator)
                    maxWidth = Math.Max(maxWidth, MeasureText(item.Text));
            }

            width = Width ?? (maxWidth + Configuration.ControlDefaults.MenuItemHorizontalPadding);
            height = _items.Count;
        }

        // Add margins
        width += Margin.Left + Margin.Right;
        height += Margin.Top + Margin.Bottom;

        return new LayoutSize(
            Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
        );
    }

    /// <inheritdoc/>
    public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
    {
        SetActualBounds(bounds);

        _lastBounds = bounds;

        Color windowBg = Container?.BackgroundColor ?? defaultBg;
        Color windowFg = Container?.ForegroundColor ?? defaultFg;

        // 1. Paint top-level menu items
        if (_orientation == MenuOrientation.Horizontal)
        {
            PaintHorizontalMenuBar(buffer, bounds, clipRect, windowFg, windowBg);
        }
        else
        {
            PaintVerticalMenu(buffer, bounds, clipRect, windowFg, windowBg);
        }

        // Note: Dropdowns now render via portal system, not here
    }

    #endregion

    #region Rendering - Menu Bar

    private void PaintHorizontalMenuBar(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color fg, Color bg)
    {
        int x = bounds.X + Margin.Left;
        int y = bounds.Y + Margin.Top;

        foreach (var item in _items)
        {
            if (item.IsSeparator)
                continue;

            int itemWidth = MeasureText(item.Text) + Configuration.ControlDefaults.MenuItemHorizontalPadding;
            var itemBounds = new Rectangle(x, y, itemWidth, 1);
            item.Bounds = itemBounds;

            var state = GetItemState(item);
            PaintMenuItem(buffer, item, x, y, itemWidth, state, true);

            x += itemWidth;
        }
    }

    private void PaintVerticalMenu(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color fg, Color bg)
    {
        int x = bounds.X + Margin.Left;
        int y = bounds.Y + Margin.Top;
        int width = bounds.Width - Margin.Left - Margin.Right;

        foreach (var item in _items)
        {
            if (item.IsSeparator)
            {
                // Draw separator line
                if (y >= clipRect.Y && y < clipRect.Bottom)
                {
                    buffer.FillRect(new LayoutRect(x, y, width, 1), '─', fg, bg);
                }
            }
            else
            {
                var itemBounds = new Rectangle(x, y, width, 1);
                item.Bounds = itemBounds;

                var state = GetItemState(item);
                PaintMenuItem(buffer, item, x, y, width, state, false);
            }

            y++;
        }
    }

    #endregion

    #region Rendering - Dropdown

    /// <summary>
    /// Internal method for portal content to paint dropdowns.
    /// Called by MenuPortalContent.PaintDOM() to render dropdown overlays.
    /// </summary>
    internal void PaintDropdownInternal(CharacterBuffer buffer, MenuDropdown dropdown, LayoutRect clipRect)
    {
        PaintDropdown(buffer, dropdown, clipRect);
    }

    private void PaintDropdown(CharacterBuffer buffer, MenuDropdown dropdown, LayoutRect clipRect)
    {
        var bounds = dropdown.Bounds;

        // Draw border (rounded box)
        DrawBox(buffer, bounds);

        // Draw items
        int y = bounds.Y + 1;
        var (start, end) = dropdown.GetVisibleRange();

        for (int i = start; i < end; i++)
        {
            var item = dropdown.VisibleItems[i];
            int x = bounds.X + 1;
            int width = bounds.Width - 2;

            if (item.IsSeparator)
            {
                // Draw separator line
                buffer.FillRect(new LayoutRect(x, y, width, 1), '─', Color.Grey, ResolvedDropdownBackground);
            }
            else
            {
                var itemBounds = new Rectangle(x, y, width, 1);
                item.Bounds = itemBounds;

                var state = GetItemState(item);
                PaintMenuItem(buffer, item, x, y, width, state, false);
            }

            y++;
        }

        // Draw scroll indicators inside content area (not on border)
        if (dropdown.CanScrollUp)
        {
            buffer.SetCell(bounds.X + bounds.Width / 2, bounds.Y + 1, '▲', ResolvedDropdownForeground, ResolvedDropdownBackground);
        }

        if (dropdown.CanScrollDown)
        {
            buffer.SetCell(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height - 2, '▼', ResolvedDropdownForeground, ResolvedDropdownBackground);
        }
    }

    private void PaintMenuItem(CharacterBuffer buffer, MenuItem item, int x, int y, int width, MenuItemState state, bool isTopLevel)
    {
        // Determine colors based on state and whether this is a top-level item or dropdown item
        Color bg, fg;
        if (isTopLevel)
        {
            (bg, fg) = state switch
            {
                MenuItemState.Disabled => (ResolvedMenuBarBackground, Color.Grey),
                MenuItemState.Pressed => (Color.DarkBlue, Color.White),
                MenuItemState.Highlighted => (ResolvedMenuBarHighlightBackground, ResolvedMenuBarHighlightForeground),
                MenuItemState.Open => (ResolvedMenuBarHighlightBackground, ResolvedMenuBarHighlightForeground),
                _ => (ResolvedMenuBarBackground, ResolvedMenuBarForeground)
            };
        }
        else
        {
            (bg, fg) = state switch
            {
                MenuItemState.Disabled => (ResolvedDropdownBackground, Color.Grey),
                MenuItemState.Pressed => (Color.DarkBlue, Color.White),
                MenuItemState.Highlighted => (ResolvedDropdownHighlightBackground, ResolvedDropdownHighlightForeground),
                MenuItemState.Open => (ResolvedDropdownHighlightBackground, ResolvedDropdownHighlightForeground),
                _ => (ResolvedDropdownBackground, ResolvedDropdownForeground)
            };
        }

        // Apply custom foreground color if set (only for normal states)
        if (item.ForegroundColor.HasValue &&
            state != MenuItemState.Disabled &&
            state != MenuItemState.Pressed &&
            state != MenuItemState.Highlighted &&
            state != MenuItemState.Open)
        {
            fg = item.ForegroundColor.Value;
        }

        // Build item text
        string text = item.Text;

        // Render
        if (isTopLevel)
        {
            // Top-level items: simple text with padding (no truncation needed)
            string displayText = $" {text} ";
            buffer.WriteString(x, y, displayText, fg, bg);
        }
        else
        {
            // Dropdown items: full width with alignment
            string shortcut = item.Shortcut ?? "";
            int shortcutWidth = MeasureText(shortcut);
            int indicatorWidth = item.HasChildren ? 2 : 0;
            int textWidth = MeasureText(text);
            int availableForText = width - shortcutWidth - indicatorWidth - 4; // Padding

            // Truncate text if needed
            if (availableForText > 0)
            {
                text = TextTruncationHelper.Truncate(text, availableForText);
            }

            buffer.FillRect(new LayoutRect(x, y, width, 1), ' ', fg, bg);
            buffer.WriteString(x + 2, y, text, fg, bg);

            if (!string.IsNullOrEmpty(shortcut))
            {
                int shortcutX = x + width - shortcutWidth - indicatorWidth - 2;
                buffer.WriteString(shortcutX, y, shortcut, Color.Grey, bg);
            }

            if (item.HasChildren)
            {
                buffer.WriteString(x + width - 2, y, "►", fg, bg);
            }
        }
    }

    private void DrawBox(CharacterBuffer buffer, Rectangle bounds)
    {
        var fg = ResolvedDropdownForeground;
        var bg = ResolvedDropdownBackground;

        // Use BoxChars abstraction for rounded dropdown borders
        var chars = BoxChars.Rounded;

        // Corners
        buffer.SetCell(bounds.X, bounds.Y, chars.TopLeft, fg, bg);
        buffer.SetCell(bounds.Right - 1, bounds.Y, chars.TopRight, fg, bg);
        buffer.SetCell(bounds.X, bounds.Bottom - 1, chars.BottomLeft, fg, bg);
        buffer.SetCell(bounds.Right - 1, bounds.Bottom - 1, chars.BottomRight, fg, bg);

        // Horizontal lines
        for (int x = bounds.X + 1; x < bounds.Right - 1; x++)
        {
            buffer.SetCell(x, bounds.Y, chars.Horizontal, fg, bg);
            buffer.SetCell(x, bounds.Bottom - 1, chars.Horizontal, fg, bg);
        }

        // Vertical lines
        for (int y = bounds.Y + 1; y < bounds.Bottom - 1; y++)
        {
            buffer.SetCell(bounds.X, y, chars.Vertical, fg, bg);
            buffer.SetCell(bounds.Right - 1, y, chars.Vertical, fg, bg);
        }

        // Fill interior
        for (int y = bounds.Y + 1; y < bounds.Bottom - 1; y++)
        {
            buffer.FillRect(new LayoutRect(bounds.X + 1, y, bounds.Width - 2, 1), ' ', fg, bg);
        }
    }

    #endregion

    #region Rendering - Item State

    private enum MenuItemState
    {
        Normal,
        Highlighted,  // Hovered or keyboard focused
        Pressed,      // Mouse down
        Open,         // Has open dropdown/submenu
        Disabled
    }

    private MenuItemState GetItemState(MenuItem item)
    {
        if (!item.IsEnabled)
            return MenuItemState.Disabled;
        if (item == _pressedItem)
            return MenuItemState.Pressed;

        // Priority: hover takes precedence when mouse is active
        if (_hoveredItem != null)
        {
            if (item == _hoveredItem)
                return MenuItemState.Highlighted;
        }
        else if (item == _focusedItem)
        {
            return MenuItemState.Highlighted;
        }

        if (item.IsOpen)
            return MenuItemState.Open;
        return MenuItemState.Normal;
    }

    #endregion
}

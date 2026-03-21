// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Helpers
{
    /// <summary>
    /// Shared rendering utilities for controls to avoid code duplication.
    /// Pass <see cref="Color.Transparent"/> as <c>background</c> to preserve whatever
    /// is already in the buffer (gradient, parent background, etc.).
    /// </summary>
    public static class ControlRenderingHelpers
    {
        /// <summary>Fills the top margin rows (from <paramref name="bounds"/>.Y up to <paramref name="startY"/>) with spaces.</summary>
        public static void FillTopMargin(
            CharacterBuffer buffer,
            LayoutRect bounds,
            LayoutRect clipRect,
            int startY,
            Color foreground,
            Color background)
        {
            for (int y = bounds.Y; y < startY && y < bounds.Bottom; y++)
            {
                if (y >= clipRect.Y && y < clipRect.Bottom)
                {
                    var lineRect = new LayoutRect(bounds.X, y, bounds.Width, 1);
                    buffer.FillRect(lineRect, ' ', foreground, background);
                }
            }
        }

        /// <summary>Fills the bottom margin rows (from <paramref name="endY"/> to <paramref name="bounds"/>.Bottom) with spaces.</summary>
        public static void FillBottomMargin(
            CharacterBuffer buffer,
            LayoutRect bounds,
            LayoutRect clipRect,
            int endY,
            Color foreground,
            Color background)
        {
            for (int y = endY; y < bounds.Bottom; y++)
            {
                if (y >= clipRect.Y && y < clipRect.Bottom)
                {
                    var lineRect = new LayoutRect(bounds.X, y, bounds.Width, 1);
                    buffer.FillRect(lineRect, ' ', foreground, background);
                }
            }
        }

        /// <summary>Fills the left and right horizontal margin columns on a single row with spaces.</summary>
        public static void FillHorizontalMargins(
            CharacterBuffer buffer,
            LayoutRect bounds,
            LayoutRect clipRect,
            int y,
            int contentStartX,
            int contentWidth,
            Color foreground,
            Color background)
        {
            if (contentStartX > bounds.X)
            {
                var leftRect = new LayoutRect(bounds.X, y, contentStartX - bounds.X, 1);
                buffer.FillRect(leftRect, ' ', foreground, background);
            }

            int contentEndX = contentStartX + contentWidth;
            if (contentEndX < bounds.Right)
            {
                var rightRect = new LayoutRect(contentEndX, y, bounds.Right - contentEndX, 1);
                buffer.FillRect(rightRect, ' ', foreground, background);
            }
        }

        /// <summary>Fills the entire <paramref name="rect"/> with spaces using the given colors.</summary>
        public static void FillRect(
            CharacterBuffer buffer,
            LayoutRect rect,
            Color foreground,
            Color background)
        {
            buffer.FillRect(rect, ' ', foreground, background);
        }

        /// <summary>Fills a single horizontal row with the given character, clipped to <paramref name="clipRect"/>.</summary>
        public static void FillLineCharacter(
            CharacterBuffer buffer,
            LayoutRect rect,
            LayoutRect clipRect,
            char lineChar,
            Color lineColor,
            Color background)
        {
            int y = rect.Y;
            for (int x = rect.X; x < rect.Right; x++)
            {
                if (x >= clipRect.X && x < clipRect.Right)
                    buffer.SetNarrowCell(x, y, lineChar, lineColor, background);
            }
        }
    }
}

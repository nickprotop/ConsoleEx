// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Helpers
{
    /// <summary>
    /// Shared rendering utilities for controls to avoid code duplication.
    /// Extracted from 14 controls that had identical margin/padding rendering logic.
    /// </summary>
    public static class ControlRenderingHelpers
    {
        /// <summary>
        /// Fills the top margin area of a control with the specified background.
        /// </summary>
        /// <param name="buffer">The character buffer to render to.</param>
        /// <param name="bounds">The control's bounding rectangle.</param>
        /// <param name="clipRect">The clipping rectangle for rendering.</param>
        /// <param name="startY">The Y coordinate where content starts (margin ends).</param>
        /// <param name="foreground">The foreground color for the fill character.</param>
        /// <param name="background">The background color for the fill character.</param>
        /// <param name="preserveBackground">When true, keeps existing buffer background (for gradient preservation).</param>
        public static void FillTopMargin(
            CharacterBuffer buffer,
            LayoutRect bounds,
            LayoutRect clipRect,
            int startY,
            Color foreground,
            Color background,
            bool preserveBackground = false)
        {
            for (int y = bounds.Y; y < startY && y < bounds.Bottom; y++)
            {
                if (y >= clipRect.Y && y < clipRect.Bottom)
                {
                    var lineRect = new LayoutRect(bounds.X, y, bounds.Width, 1);
                    if (preserveBackground)
                        buffer.FillRectPreservingBackground(lineRect, foreground);
                    else
                        buffer.FillRect(lineRect, ' ', foreground, background);
                }
            }
        }

        /// <summary>
        /// Fills the bottom margin area of a control with the specified background.
        /// </summary>
        /// <param name="buffer">The character buffer to render to.</param>
        /// <param name="bounds">The control's bounding rectangle.</param>
        /// <param name="clipRect">The clipping rectangle for rendering.</param>
        /// <param name="endY">The Y coordinate where content ends (margin begins).</param>
        /// <param name="foreground">The foreground color for the fill character.</param>
        /// <param name="background">The background color for the fill character.</param>
        /// <param name="preserveBackground">When true, keeps existing buffer background (for gradient preservation).</param>
        public static void FillBottomMargin(
            CharacterBuffer buffer,
            LayoutRect bounds,
            LayoutRect clipRect,
            int endY,
            Color foreground,
            Color background,
            bool preserveBackground = false)
        {
            for (int y = endY; y < bounds.Bottom; y++)
            {
                if (y >= clipRect.Y && y < clipRect.Bottom)
                {
                    var lineRect = new LayoutRect(bounds.X, y, bounds.Width, 1);
                    if (preserveBackground)
                        buffer.FillRectPreservingBackground(lineRect, foreground);
                    else
                        buffer.FillRect(lineRect, ' ', foreground, background);
                }
            }
        }

        /// <summary>
        /// Fills horizontal margins (left and right) for a single line.
        /// </summary>
        /// <param name="buffer">The character buffer to render to.</param>
        /// <param name="bounds">The control's bounding rectangle.</param>
        /// <param name="clipRect">The clipping rectangle for rendering.</param>
        /// <param name="y">The Y coordinate of the line to fill margins for.</param>
        /// <param name="contentStartX">The X coordinate where content starts.</param>
        /// <param name="contentWidth">The width of the content area.</param>
        /// <param name="foreground">The foreground color for the fill character.</param>
        /// <param name="background">The background color for the fill character.</param>
        /// <param name="preserveBackground">When true, keeps existing buffer background (for gradient preservation).</param>
        public static void FillHorizontalMargins(
            CharacterBuffer buffer,
            LayoutRect bounds,
            LayoutRect clipRect,
            int y,
            int contentStartX,
            int contentWidth,
            Color foreground,
            Color background,
            bool preserveBackground = false)
        {
            // Left margin
            if (contentStartX > bounds.X)
            {
                var leftRect = new LayoutRect(bounds.X, y, contentStartX - bounds.X, 1);
                if (preserveBackground)
                    buffer.FillRectPreservingBackground(leftRect, foreground);
                else
                    buffer.FillRect(leftRect, ' ', foreground, background);
            }

            // Right margin
            int contentEndX = contentStartX + contentWidth;
            if (contentEndX < bounds.Right)
            {
                var rightRect = new LayoutRect(contentEndX, y, bounds.Right - contentEndX, 1);
                if (preserveBackground)
                    buffer.FillRectPreservingBackground(rightRect, foreground);
                else
                    buffer.FillRect(rightRect, ' ', foreground, background);
            }
        }

        /// <summary>
        /// Fills a rectangle with either solid color or preserving existing background.
        /// Convenience wrapper for controls that need conditional gradient preservation.
        /// </summary>
        public static void FillRect(
            CharacterBuffer buffer,
            LayoutRect rect,
            Color foreground,
            Color background,
            bool preserveBackground)
        {
            if (preserveBackground)
                buffer.FillRectPreservingBackground(rect, foreground);
            else
                buffer.FillRect(rect, ' ', foreground, background);
        }
        /// <summary>
        /// Fills a single-row rect with a repeated line character, optionally preserving the existing background color per cell.
        /// </summary>
        /// <param name="buffer">The character buffer to write to.</param>
        /// <param name="rect">The rectangle to fill (must be 1 row high).</param>
        /// <param name="clipRect">The clipping rectangle for horizontal bounds.</param>
        /// <param name="lineChar">The character to fill with.</param>
        /// <param name="lineColor">The foreground color for the line character.</param>
        /// <param name="background">The background color to use when not preserving.</param>
        /// <param name="preserveBackground">If true, reads and preserves the existing background color per cell.</param>
        public static void FillLineCharacter(
            CharacterBuffer buffer,
            LayoutRect rect,
            LayoutRect clipRect,
            char lineChar,
            Color lineColor,
            Color background,
            bool preserveBackground)
        {
            if (preserveBackground)
            {
                int y = rect.Y;
                for (int x = rect.X; x < rect.Right; x++)
                {
                    if (x >= clipRect.X && x < clipRect.Right)
                    {
                        var existingBg = buffer.GetCell(x, y).Background;
                        buffer.SetNarrowCell(x, y, lineChar, lineColor, existingBg);
                    }
                }
            }
            else
            {
                buffer.FillRect(rect, lineChar, lineColor, background);
            }
        }
    }
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;

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
                    buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', foreground, background);
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
                    buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', foreground, background);
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
            // Left margin
            if (contentStartX > bounds.X)
            {
                buffer.FillRect(
                    new LayoutRect(bounds.X, y, contentStartX - bounds.X, 1),
                    ' ', foreground, background);
            }

            // Right margin
            int contentEndX = contentStartX + contentWidth;
            if (contentEndX < bounds.Right)
            {
                buffer.FillRect(
                    new LayoutRect(contentEndX, y, bounds.Right - contentEndX, 1),
                    ' ', foreground, background);
            }
        }
    }
}

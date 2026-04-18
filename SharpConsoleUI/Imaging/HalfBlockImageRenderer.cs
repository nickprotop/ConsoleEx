// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Imaging
{
    /// <summary>
    /// Renders images using the half-block character technique (U+2580).
    /// Each cell represents 2 vertical pixels: foreground = top pixel, background = bottom pixel.
    /// This is the universal fallback renderer that works in all terminals.
    /// </summary>
    internal sealed class HalfBlockImageRenderer : IImageRenderer
    {
        /// <inheritdoc />
        public void Paint(
            CharacterBuffer buffer,
            LayoutRect destRect,
            LayoutRect clipRect,
            PixelBuffer source,
            ImageScaleMode scaleMode,
            int cropOffsetX,
            int cropOffsetY,
            int renderCols,
            int renderRows,
            Color windowBackground)
        {
            Cell[,] cells;
            if (scaleMode == ImageScaleMode.None)
                cells = HalfBlockRenderer.Render(source, windowBackground);
            else
                cells = HalfBlockRenderer.RenderScaled(source, renderCols, renderRows, windowBackground);

            int actualW = cells.GetLength(0);
            int actualH = cells.GetLength(1);

            int displayWidth = Math.Min(actualW - cropOffsetX, destRect.Width);
            int displayHeight = Math.Min(actualH - cropOffsetY, destRect.Height);

            for (int cy = 0; cy < displayHeight && destRect.Y + cy < destRect.Bottom; cy++)
            {
                int y = destRect.Y + cy;
                if (y < clipRect.Y || y >= clipRect.Bottom) continue;

                for (int cx = 0; cx < displayWidth && destRect.X + cx < destRect.Right; cx++)
                {
                    int x = destRect.X + cx;
                    if (x < clipRect.X || x >= clipRect.Right) continue;
                    buffer.SetCell(x, y, cells[cropOffsetX + cx, cropOffsetY + cy]);
                }
            }
        }

        /// <inheritdoc />
        public void OnSourceChanged()
        {
            // No-op: half-block rendering has no transmitted state to clean up.
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // No-op: no unmanaged resources.
        }
    }
}

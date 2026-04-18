// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Video
{
    /// <summary>
    /// Cell-based video frame sink. Converts each decoded RGB24 frame into a Cell[,] grid
    /// using <see cref="VideoFrameRenderer"/> (HalfBlock / Ascii / Braille) and paints the grid,
    /// centered within the content area with background-filled gaps. This is the universal
    /// fallback used on terminals without Kitty graphics support.
    /// </summary>
    internal sealed class CellVideoFrameSink : IVideoFrameSink
    {
        private readonly VideoRenderMode _mode;
        private readonly object _lock = new();

        private Cell[,]? _cells;
        private int _cellWidth;
        private int _cellHeight;

        public CellVideoFrameSink(VideoRenderMode mode)
        {
            // Auto / Kitty collapse to HalfBlock in this sink — callers that want true Kitty
            // should construct a KittyVideoFrameSink directly.
            _mode = mode switch
            {
                VideoRenderMode.Ascii => VideoRenderMode.Ascii,
                VideoRenderMode.Braille => VideoRenderMode.Braille,
                _ => VideoRenderMode.HalfBlock,
            };
        }

        public void IngestFrame(byte[] rgb24, int pixelWidth, int pixelHeight, int targetCols, int targetRows, Color windowBackground)
        {
            _ = targetCols; _ = targetRows; // Cell sink derives cell grid from frame dimensions.

            var (expectedW, expectedH) = VideoFrameRenderer.GetCellDimensions(pixelWidth, pixelHeight, _mode);

            lock (_lock)
            {
                if (_cells == null
                    || _cells.GetLength(0) < expectedW
                    || _cells.GetLength(1) < expectedH)
                {
                    _cells = new Cell[expectedW, expectedH];
                }

                VideoFrameRenderer.RenderFrameInto(
                    _cells, rgb24, pixelWidth, pixelHeight,
                    _mode, windowBackground,
                    out _cellWidth, out _cellHeight);
            }
        }

        public void Paint(
            CharacterBuffer buffer,
            LayoutRect contentRect,
            LayoutRect clipRect,
            Color windowForeground,
            Color windowBackground)
        {
            Cell[,]? cells;
            int cellW, cellH;
            lock (_lock)
            {
                cells = _cells;
                cellW = _cellWidth;
                cellH = _cellHeight;
            }

            if (cells == null)
            {
                FillRect(buffer, contentRect, clipRect, windowForeground, windowBackground);
                return;
            }

            int availW = contentRect.Width;
            int availH = contentRect.Height;
            int offsetX = Math.Max(0, (availW - cellW) / 2);
            int offsetY = Math.Max(0, (availH - cellH) / 2);
            int displayW = Math.Min(cellW, availW);
            int displayH = Math.Min(cellH, availH);

            int contentX = contentRect.X;
            int contentY = contentRect.Y;
            int contentBottom = contentRect.Bottom;
            int contentRight = contentRect.Right;

            for (int y = contentY; y < contentY + offsetY && y < contentBottom; y++)
            {
                if (y < clipRect.Y || y >= clipRect.Bottom) continue;
                for (int x = contentX; x < contentX + availW && x < contentRight; x++)
                {
                    if (x < clipRect.X || x >= clipRect.Right) continue;
                    buffer.SetNarrowCell(x, y, ' ', windowForeground, windowBackground);
                }
            }

            for (int cy = 0; cy < displayH; cy++)
            {
                int y = contentY + offsetY + cy;
                if (y >= contentBottom || y < clipRect.Y || y >= clipRect.Bottom) continue;

                for (int x = contentX; x < contentX + offsetX && x < contentRight; x++)
                {
                    if (x >= clipRect.X && x < clipRect.Right)
                        buffer.SetNarrowCell(x, y, ' ', windowForeground, windowBackground);
                }

                for (int cx = 0; cx < displayW && cx < cellW; cx++)
                {
                    int x = contentX + offsetX + cx;
                    if (x >= contentRight) break;
                    if (x >= clipRect.X && x < clipRect.Right)
                        buffer.SetCell(x, y, cells[cx, cy]);
                }

                int rightStart = contentX + offsetX + displayW;
                for (int x = rightStart; x < contentX + availW && x < contentRight; x++)
                {
                    if (x >= clipRect.X && x < clipRect.Right)
                        buffer.SetNarrowCell(x, y, ' ', windowForeground, windowBackground);
                }
            }

            int belowFrame = contentY + offsetY + displayH;
            for (int y = belowFrame; y < contentY + availH && y < contentBottom; y++)
            {
                if (y < clipRect.Y || y >= clipRect.Bottom) continue;
                for (int x = contentX; x < contentX + availW && x < contentRight; x++)
                {
                    if (x < clipRect.X || x >= clipRect.Right) continue;
                    buffer.SetNarrowCell(x, y, ' ', windowForeground, windowBackground);
                }
            }
        }

        public void OnStopped()
        {
            // Cell sink holds no terminal-side state; nothing to release.
        }

        public (int Width, int Height) GetPreferredPixelSize(int cellCols, int cellRows)
        {
            return VideoFrameRenderer.GetRequiredPixelSize(cellCols, cellRows, _mode);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _cells = null;
                _cellWidth = 0;
                _cellHeight = 0;
            }
        }

        private static void FillRect(
            CharacterBuffer buffer, LayoutRect rect, LayoutRect clipRect,
            Color fg, Color bg)
        {
            for (int y = rect.Y; y < rect.Bottom; y++)
            {
                if (y < clipRect.Y || y >= clipRect.Bottom) continue;
                for (int x = rect.X; x < rect.Right; x++)
                {
                    if (x < clipRect.X || x >= clipRect.Right) continue;
                    buffer.SetNarrowCell(x, y, ' ', fg, bg);
                }
            }
        }
    }
}

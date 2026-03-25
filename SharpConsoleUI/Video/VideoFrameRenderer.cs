using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Video
{
    /// <summary>
    /// Converts raw RGB24 frame bytes into Cell arrays for terminal display.
    /// Supports three render modes: HalfBlock, Ascii, and Braille.
    /// </summary>
    public static class VideoFrameRenderer
    {
        /// <summary>
        /// Renders a raw RGB24 frame into a pre-allocated Cell array.
        /// Caller must ensure <paramref name="target"/> is at least [cellWidth, cellHeight].
        /// Use <see cref="GetCellDimensions"/> to compute the required array size.
        /// </summary>
        /// <param name="target">Pre-allocated Cell array to write into. Avoids per-frame GC allocation.</param>
        /// <param name="rgb24">Raw pixel data: width * height * 3 bytes, row-major RGB.</param>
        /// <param name="frameWidth">Source frame width in pixels.</param>
        /// <param name="frameHeight">Source frame height in pixels.</param>
        /// <param name="mode">Rendering mode.</param>
        /// <param name="background">Background color for unfilled areas.</param>
        /// <param name="cellWidth">Output cell array width actually written (columns).</param>
        /// <param name="cellHeight">Output cell array height actually written (rows).</param>
        public static void RenderFrameInto(
            Cell[,] target, byte[] rgb24, int frameWidth, int frameHeight,
            VideoRenderMode mode, Color background,
            out int cellWidth, out int cellHeight)
        {
            switch (mode)
            {
                case VideoRenderMode.Ascii:
                    RenderAsciiInto(target, rgb24, frameWidth, frameHeight, background, out cellWidth, out cellHeight);
                    break;
                case VideoRenderMode.Braille:
                    RenderBrailleInto(target, rgb24, frameWidth, frameHeight, background, out cellWidth, out cellHeight);
                    break;
                default:
                    RenderHalfBlockInto(target, rgb24, frameWidth, frameHeight, background, out cellWidth, out cellHeight);
                    break;
            }
        }

        /// <summary>
        /// Computes the cell array dimensions for a given pixel frame size and render mode.
        /// Use this to pre-allocate the target array for <see cref="RenderFrameInto"/>.
        /// </summary>
        /// <param name="frameWidth">Source frame width in pixels.</param>
        /// <param name="frameHeight">Source frame height in pixels.</param>
        /// <param name="mode">Rendering mode.</param>
        /// <returns>Tuple of (CellWidth, CellHeight) for the target array dimensions.</returns>
        public static (int CellWidth, int CellHeight) GetCellDimensions(int frameWidth, int frameHeight, VideoRenderMode mode)
        {
            return mode switch
            {
                VideoRenderMode.HalfBlock => (frameWidth, (frameHeight + 1) / VideoDefaults.HalfBlockPixelsPerCell),
                VideoRenderMode.Ascii => (frameWidth, frameHeight),
                VideoRenderMode.Braille => (frameWidth / VideoDefaults.BrailleCellPixelWidth,
                                            frameHeight / VideoDefaults.BrailleCellPixelHeight),
                _ => (frameWidth, (frameHeight + 1) / VideoDefaults.HalfBlockPixelsPerCell),
            };
        }

        /// <summary>
        /// Computes the required pixel dimensions for the given render mode and target cell size.
        /// FFmpeg should scale to these dimensions.
        /// </summary>
        /// <param name="cellCols">Target cell columns.</param>
        /// <param name="cellRows">Target cell rows.</param>
        /// <param name="mode">Rendering mode.</param>
        /// <returns>Tuple of (Width, Height) in pixels for FFmpeg scaling.</returns>
        public static (int Width, int Height) GetRequiredPixelSize(int cellCols, int cellRows, VideoRenderMode mode)
        {
            return mode switch
            {
                VideoRenderMode.HalfBlock => (cellCols, cellRows * VideoDefaults.HalfBlockPixelsPerCell),
                VideoRenderMode.Ascii => (cellCols, cellRows),
                VideoRenderMode.Braille => (cellCols * VideoDefaults.BrailleCellPixelWidth,
                                            cellRows * VideoDefaults.BrailleCellPixelHeight),
                _ => (cellCols, cellRows * VideoDefaults.HalfBlockPixelsPerCell),
            };
        }

        #region HalfBlock Rendering

        /// <summary>
        /// Half-block: each cell = 2 vertical pixels. fg = top pixel, bg = bottom pixel, char = U+2580.
        /// Frame dimensions: (cellCols, cellRows * 2) pixels.
        /// </summary>
        private static void RenderHalfBlockInto(
            Cell[,] cells, byte[] rgb24, int frameWidth, int frameHeight, Color background,
            out int cellWidth, out int cellHeight)
        {
            cellWidth = frameWidth;
            cellHeight = (frameHeight + 1) / VideoDefaults.HalfBlockPixelsPerCell;

            for (int cy = 0; cy < cellHeight; cy++)
            {
                int topRow = cy * VideoDefaults.HalfBlockPixelsPerCell;
                int botRow = topRow + 1;
                bool hasBot = botRow < frameHeight;

                for (int cx = 0; cx < cellWidth; cx++)
                {
                    int topIdx = (topRow * frameWidth + cx) * 3;
                    Color fg = new Color(rgb24[topIdx], rgb24[topIdx + 1], rgb24[topIdx + 2]);

                    Color bg;
                    if (hasBot)
                    {
                        int botIdx = (botRow * frameWidth + cx) * 3;
                        bg = new Color(rgb24[botIdx], rgb24[botIdx + 1], rgb24[botIdx + 2]);
                    }
                    else
                    {
                        bg = background;
                    }

                    cells[cx, cy] = new Cell('\u2580', fg, bg);
                }
            }
        }

        #endregion

        #region ASCII Rendering

        /// <summary>
        /// ASCII: each cell = 1 pixel. Brightness mapped to density ramp character.
        /// Frame dimensions: (cellCols, cellRows) pixels.
        /// </summary>
        private static void RenderAsciiInto(
            Cell[,] cells, byte[] rgb24, int frameWidth, int frameHeight, Color background,
            out int cellWidth, out int cellHeight)
        {
            cellWidth = frameWidth;
            cellHeight = frameHeight;
            string ramp = VideoDefaults.AsciiDensityRamp;
            int rampMax = ramp.Length - 1;

            for (int y = 0; y < frameHeight; y++)
            {
                for (int x = 0; x < frameWidth; x++)
                {
                    int idx = (y * frameWidth + x) * 3;
                    byte r = rgb24[idx], g = rgb24[idx + 1], b = rgb24[idx + 2];

                    // Weighted luminance: (3R + 4G + 1B) / 8
                    int gray = (r * 3 + g * 4 + b) >> 3;
                    int charIdx = gray * rampMax / 255;

                    cells[x, y] = new Cell(ramp[charIdx], new Color(r, g, b), background);
                }
            }
        }

        #endregion

        #region Braille Rendering

        // Braille dot bit layout for a 2x4 cell:
        //   col0  col1
        //   0x01  0x08   row0
        //   0x02  0x10   row1
        //   0x04  0x20   row2
        //   0x40  0x80   row3
        private static readonly int[,] BrailleBits =
        {
            { 0x01, 0x08 },
            { 0x02, 0x10 },
            { 0x04, 0x20 },
            { 0x40, 0x80 },
        };

        /// <summary>
        /// Braille: each cell covers a 2x4 pixel region. Threshold-based dot activation.
        /// Frame dimensions: (cellCols * 2, cellRows * 4) pixels.
        /// </summary>
        private static void RenderBrailleInto(
            Cell[,] cells, byte[] rgb24, int frameWidth, int frameHeight, Color background,
            out int cellWidth, out int cellHeight)
        {
            cellWidth = frameWidth / VideoDefaults.BrailleCellPixelWidth;
            cellHeight = frameHeight / VideoDefaults.BrailleCellPixelHeight;
            int threshold = VideoDefaults.BrailleBrightnessThreshold;

            for (int cy = 0; cy < cellHeight; cy++)
            {
                for (int cx = 0; cx < cellWidth; cx++)
                {
                    int bits = 0;
                    int totalR = 0, totalG = 0, totalB = 0;
                    int pixelCount = 0;

                    int baseX = cx * VideoDefaults.BrailleCellPixelWidth;
                    int baseY = cy * VideoDefaults.BrailleCellPixelHeight;

                    for (int dy = 0; dy < VideoDefaults.BrailleCellPixelHeight; dy++)
                    {
                        for (int dx = 0; dx < VideoDefaults.BrailleCellPixelWidth; dx++)
                        {
                            int px = baseX + dx;
                            int py = baseY + dy;
                            if (px >= frameWidth || py >= frameHeight) continue;

                            int idx = (py * frameWidth + px) * 3;
                            byte r = rgb24[idx], g = rgb24[idx + 1], b = rgb24[idx + 2];

                            totalR += r;
                            totalG += g;
                            totalB += b;
                            pixelCount++;

                            int gray = (r * 3 + g * 4 + b) >> 3;
                            if (gray > threshold)
                                bits |= BrailleBits[dy, dx];
                        }
                    }

                    char brailleChar = (char)(VideoDefaults.BrailleBaseCodepoint + bits);

                    Color fg;
                    if (pixelCount > 0)
                        fg = new Color(
                            (byte)(totalR / pixelCount),
                            (byte)(totalG / pixelCount),
                            (byte)(totalB / pixelCount));
                    else
                        fg = background;

                    cells[cx, cy] = new Cell(brailleChar, fg, background);
                }
            }
        }

        #endregion
    }
}

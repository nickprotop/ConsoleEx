// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Video
{
    /// <summary>
    /// Kitty graphics protocol video frame sink. Uses the protocol's <b>animation frame
    /// update</b> path (<c>a=f,r=1</c>) for real-time playback: the image is transmitted
    /// once with a virtual placement (<c>a=T,U=1,c=…,r=…</c>), and subsequent frames edit
    /// frame 1 in place. This is the only sequence that avoids the "retransmit with same id
    /// deletes all placements" behaviour specified in the Kitty graphics protocol — without
    /// it, in-place video updates flicker or go black.
    /// </summary>
    /// <remarks>
    /// <para>Per the Kitty spec, sending <c>a=T</c> with an existing image id deletes the image
    /// and every placement referencing it; <c>a=f</c> with <c>r=1</c> replaces the root
    /// frame's pixel data without disturbing the placement.</para>
    /// <para>However, Kitty does not automatically redraw the placement when the underlying
    /// frame data changes — the client has to write the placeholder cells again to prompt
    /// the terminal to re-render them against the new pixels. (Observable as "frames only
    /// appear when the window moves," because a window move is the one path that naturally
    /// re-emits every cell.) We force that redraw by flipping one invisible bit of the cell
    /// background on every frame, which makes the buffer diff re-emit the cells without
    /// tearing down the placement — no strobing, no work beyond a diff that was going to
    /// happen anyway.</para>
    /// </remarks>
    internal sealed class KittyVideoFrameSink : IVideoFrameSink
    {
        private static uint _nextImageId;

        private readonly IGraphicsProtocol _protocol;
        private readonly object _lock = new();

        private uint _imageId;
        private bool _imageCreated;
        // The pixel dimensions of the image as originally transmitted. If FFmpeg's output
        // size changes mid-playback we have to tear down and recreate the placement
        // (a=f,r=1 expects the same s/v as the initial transmit).
        private int _transmittedPixelWidth;
        private int _transmittedPixelHeight;
        private int _placementCols;
        private int _placementRows;
        // Toggled on every IngestFrame; Paint uses it to flip the low bit of the cell
        // background so the buffer diff re-emits the placeholder cells each frame. That
        // re-emit is what prompts Kitty to redraw the placement against the freshly
        // uploaded frame data (a=f,r=1 alone doesn't trigger a redraw).
        private bool _frameParity;
        private bool _disposed;

        public KittyVideoFrameSink(IGraphicsProtocol protocol)
        {
            _protocol = protocol;
        }

        public void IngestFrame(byte[] rgb24, int pixelWidth, int pixelHeight, int targetCols, int targetRows, Color windowBackground)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0) return;
            if (targetCols <= 0 || targetRows <= 0) return;

            // Snapshot state under lock; decide whether this is the initial transmit or an
            // in-place update. Do the actual protocol I/O outside the lock so a slow terminal
            // can't stall Paint.
            uint id;
            bool needsCreate;
            uint oldIdToDelete = 0;
            lock (_lock)
            {
                if (_disposed) return;

                bool dimensionsChanged = _imageCreated && (
                    _transmittedPixelWidth != pixelWidth ||
                    _transmittedPixelHeight != pixelHeight ||
                    _placementCols != targetCols ||
                    _placementRows != targetRows);

                if (dimensionsChanged)
                {
                    // FFmpeg resolution or the on-screen placement changed — the existing
                    // image's frame-1 size is fixed, so we have to delete and recreate.
                    oldIdToDelete = _imageId;
                    _imageCreated = false;
                }

                if (!_imageCreated)
                {
                    _imageId = System.Threading.Interlocked.Increment(ref _nextImageId);
                    _imageCreated = true;
                    needsCreate = true;
                }
                else
                {
                    needsCreate = false;
                }

                _transmittedPixelWidth = pixelWidth;
                _transmittedPixelHeight = pixelHeight;
                _placementCols = targetCols;
                _placementRows = targetRows;
                _frameParity = !_frameParity;
                id = _imageId;
            }

            if (oldIdToDelete != 0)
                _protocol.DeleteImage(oldIdToDelete);

            if (needsCreate)
                _protocol.TransmitRawRgb(id, rgb24, pixelWidth, pixelHeight, targetCols, targetRows);
            else
                _protocol.UpdateRawRgbFrame(id, rgb24, pixelWidth, pixelHeight);
        }

        public void Paint(
            CharacterBuffer buffer,
            LayoutRect contentRect,
            LayoutRect clipRect,
            Color windowForeground,
            Color windowBackground)
        {
            int availW = contentRect.Width;
            int availH = contentRect.Height;
            if (availW <= 0 || availH <= 0) return;

            uint id;
            int cols, rows;
            bool parity;
            lock (_lock)
            {
                if (_disposed) return;
                id = _imageCreated ? _imageId : 0;
                cols = _placementCols;
                rows = _placementRows;
                parity = _frameParity;
            }

            // Before the first frame is ingested, fill with background so stale cells don't
            // bleed through while FFmpeg spins up.
            if (id == 0 || cols <= 0 || rows <= 0)
            {
                for (int y = contentRect.Y; y < contentRect.Bottom; y++)
                {
                    if (y < clipRect.Y || y >= clipRect.Bottom) continue;
                    for (int x = contentRect.X; x < contentRect.Right; x++)
                    {
                        if (x < clipRect.X || x >= clipRect.Right) continue;
                        buffer.SetNarrowCell(x, y, ' ', windowForeground, windowBackground);
                    }
                }
                return;
            }

            // Write placeholder cells for the placement grid. a=f,r=1 updates the image data
            // but Kitty does not redraw placements on its own — we force a redraw by flipping
            // one invisible bit of the cell background on every frame, so the buffer diff
            // re-emits the placeholder cells and the terminal renders them against the
            // freshly uploaded frame data. (Opaque Kitty pixels cover the cell's background,
            // so the 1-bit perturbation is not visible to the user.)
            int drawCols = Math.Min(cols, availW);
            int drawRows = Math.Min(rows, availH);
            Color idFg = KittyProtocol.ImageIdToForegroundColor(id);
            byte bgR = (byte)(windowBackground.R ^ (parity ? 1 : 0));
            Color cellBg = new Color(bgR, windowBackground.G, windowBackground.B);

            for (int cy = 0; cy < drawRows; cy++)
            {
                int y = contentRect.Y + cy;
                if (y >= contentRect.Bottom) break;
                if (y < clipRect.Y || y >= clipRect.Bottom) continue;

                for (int cx = 0; cx < drawCols; cx++)
                {
                    int x = contentRect.X + cx;
                    if (x >= contentRect.Right) break;
                    if (x < clipRect.X || x >= clipRect.Right) continue;

                    string combiners = KittyProtocol.BuildPlaceholderCombiners(cy, cx);
                    var cell = new Cell(ImagingDefaults.KittyPlaceholder, idFg, cellBg)
                    {
                        Combiners = combiners
                    };
                    buffer.SetCell(x, y, cell);
                }
            }

            // Fill any contentRect area outside the placement grid with background (happens
            // briefly during resize between bounds change and FFmpeg restart).
            for (int y = contentRect.Y; y < contentRect.Bottom; y++)
            {
                if (y < clipRect.Y || y >= clipRect.Bottom) continue;
                for (int x = contentRect.X; x < contentRect.Right; x++)
                {
                    if (x < clipRect.X || x >= clipRect.Right) continue;
                    int cx = x - contentRect.X;
                    int cy = y - contentRect.Y;
                    if (cx < drawCols && cy < drawRows) continue;
                    buffer.SetNarrowCell(x, y, ' ', windowForeground, windowBackground);
                }
            }
        }

        public void OnStopped()
        {
            uint toDelete = 0;
            lock (_lock)
            {
                if (_imageCreated)
                {
                    toDelete = _imageId;
                    _imageCreated = false;
                    _transmittedPixelWidth = 0;
                    _transmittedPixelHeight = 0;
                    _placementCols = 0;
                    _placementRows = 0;
                }
            }

            if (toDelete != 0)
                _protocol.DeleteImage(toDelete);
        }

        public (int Width, int Height) GetPreferredPixelSize(int cellCols, int cellRows)
        {
            int w = cellCols * VideoDefaults.KittyPixelsPerCellX;
            int h = cellRows * VideoDefaults.KittyPixelsPerCellY;
            if (w > VideoDefaults.KittyMaxPixelWidth) w = VideoDefaults.KittyMaxPixelWidth;
            if (h > VideoDefaults.KittyMaxPixelHeight) h = VideoDefaults.KittyMaxPixelHeight;
            return (Math.Max(2, w), Math.Max(2, h));
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
            }
            OnStopped();
        }
    }
}

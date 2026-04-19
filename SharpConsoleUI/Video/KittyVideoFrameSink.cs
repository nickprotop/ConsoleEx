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
    /// Kitty graphics protocol video frame sink. Transmits every frame with
    /// <c>a=T,U=1</c> (full image transmission that atomically recreates the
    /// image and its virtual-placement spec), alternating between two image ids
    /// so the placeholder cells' foreground color changes frame-to-frame and
    /// the buffer diff always re-emits them.
    /// </summary>
    /// <remarks>
    /// <para>This is the same strategy mpv's <c>vo_kitty</c> uses — see
    /// <see href="https://github.com/mpv-player/mpv/blob/master/video/out/vo_kitty.c">
    /// mpv/video/out/vo_kitty.c</see>, which retransmits with <c>a=T</c> on
    /// every <c>flip_page</c> and never issues <c>a=f</c>. The in-place edit
    /// path (<c>a=f,r=1</c>), while cheaper on paper, is unreliable: Kitty
    /// has no "redraw placements now" primitive, and empirically the
    /// placements keep showing stale pixel data even after <c>a=f,r=1</c>
    /// updates the underlying frame — the user-visible symptom was a rare
    /// black frame that would "stick" until R (<c>ForceRefresh</c>) forced a
    /// fresh image id, which then worked because it happened to do what
    /// we now do every frame. Retransmitting with <c>a=T</c> deletes the
    /// image + its placements and creates them fresh in one atomic APC
    /// sequence, so the placement is guaranteed to render against the
    /// newly-uploaded pixel data on the next cell re-emission.</para>
    /// <para>Two image ids are used and alternated. Placeholder cells encode
    /// the image id in their foreground color, so alternating ids means the
    /// cells switch fg every frame → the SharpConsoleUI buffer diff always
    /// detects a change and re-emits the cells, which is what prompts Kitty
    /// to render the (now-fresh) placement. Two ids are also what lets the
    /// just-retransmitted slot's placement be ready before Paint writes cells
    /// referencing it: the previously-active slot keeps rendering its image
    /// until Paint flips the cells to the new slot.</para>
    /// <para>The slot flip is <i>published</i> to Paint only after the
    /// protocol I/O completes. That prevents a race where Paint reads the
    /// new slot id, re-emits cells referencing it, and the terminal tries to
    /// render a placement whose <c>a=T</c> command hasn't yet arrived —
    /// which was the original cause of the rare stuck-black-frame state this
    /// design replaces.</para>
    /// </remarks>
    internal sealed class KittyVideoFrameSink : IVideoFrameSink
    {
        private static uint _nextImageId;

        private enum Slot { None, A, B }

        private readonly IGraphicsProtocol _protocol;
        private readonly object _lock = new();

        // Two image-id slots. Each IngestFrame alternates which slot carries
        // the new frame, and transmits with a=T so the slot's image +
        // virtual-placement spec is atomically recreated every frame.
        private uint _imageIdA;
        private uint _imageIdB;

        // The slot whose id Paint currently references. Updated AFTER the
        // protocol I/O for the new slot has completed, so placeholder cells
        // are never re-emitted against an id whose a=T is still in flight
        // to the terminal.
        private Slot _currentSlot;
        private uint _currentImageId;

        // Placement dimensions (shared across both slots).
        private int _transmittedPixelWidth;
        private int _transmittedPixelHeight;
        private int _placementCols;
        private int _placementRows;

        // Ids retired by a dimension change or ForceRefresh, to be deleted on
        // the NEXT IngestFrame — the existing placeholder cells keep
        // rendering against the old ids for one more frame while Paint
        // migrates them, avoiding a one-frame black flash during the
        // transition. Bounded to two (both slots at once).
        private readonly List<uint> _pendingDeletionIds = new(2);

        // Set by ForceRefresh(); consumed on the next IngestFrame, which
        // retires both slots and allocates fresh ids. User-triggered recovery
        // via the R keybind — kept as a zero-cost safety hatch.
        private bool _refreshRequested;
        private bool _disposed;

        public KittyVideoFrameSink(IGraphicsProtocol protocol)
        {
            _protocol = protocol;
        }

        public void IngestFrame(byte[] rgb24, int pixelWidth, int pixelHeight, int targetCols, int targetRows, Color windowBackground)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0) return;
            if (targetCols <= 0 || targetRows <= 0) return;

            Slot nextSlot;
            uint nextId;
            uint[] deletionsNow;
            lock (_lock)
            {
                if (_disposed) return;

                // Allocate slot ids lazily on first use. Once allocated they
                // stay the same for the sink's lifetime — subsequent a=T
                // calls reuse the id (deleting and recreating the underlying
                // image atomically).
                if (_imageIdA == 0) _imageIdA = System.Threading.Interlocked.Increment(ref _nextImageId);
                if (_imageIdB == 0) _imageIdB = System.Threading.Interlocked.Increment(ref _nextImageId);

                bool anySlotUsed = _currentSlot != Slot.None;

                bool dimensionsChanged = anySlotUsed && (
                    _transmittedPixelWidth != pixelWidth ||
                    _transmittedPixelHeight != pixelHeight ||
                    _placementCols != targetCols ||
                    _placementRows != targetRows);

                bool refreshRequested = anySlotUsed && _refreshRequested;
                _refreshRequested = false;

                // Drain ids retired on the previous IngestFrame — Paint has
                // had a full frame to transition cells off of them by now.
                deletionsNow = _pendingDeletionIds.ToArray();
                _pendingDeletionIds.Clear();

                if (dimensionsChanged || refreshRequested)
                {
                    // Retire both slot ids: queue them for deletion one frame
                    // later, and allocate fresh ids so new a=T calls don't
                    // collide with the still-displayed old placements.
                    _pendingDeletionIds.Add(_imageIdA);
                    _pendingDeletionIds.Add(_imageIdB);
                    _imageIdA = System.Threading.Interlocked.Increment(ref _nextImageId);
                    _imageIdB = System.Threading.Interlocked.Increment(ref _nextImageId);
                    _currentSlot = Slot.None;
                    _currentImageId = 0;
                }

                // Alternate slots each frame so the cells' fg color switches
                // and the buffer diff re-emits them (which is what triggers
                // the terminal to render the freshly-uploaded placement).
                nextSlot = _currentSlot == Slot.A ? Slot.B : Slot.A;
                nextId = nextSlot == Slot.A ? _imageIdA : _imageIdB;

                _transmittedPixelWidth = pixelWidth;
                _transmittedPixelHeight = pixelHeight;
                _placementCols = targetCols;
                _placementRows = targetRows;
                // NOTE: _currentSlot / _currentImageId are intentionally NOT
                // updated here. They're published below, after the protocol
                // I/O returns.
            }

            // Full a=T transmission — recreates the slot's image and its
            // virtual-placement spec atomically. Same strategy as mpv's
            // vo_kitty (video/out/vo_kitty.c, flip_page): a=T per frame, not
            // a=f in-place edits. See class remarks for the full rationale.
            _protocol.TransmitRawRgb(nextId, rgb24, pixelWidth, pixelHeight, targetCols, targetRows);

            // Publication point. Once this is committed, the next Paint
            // writes placeholder cells referencing nextId; its fg color
            // differs from the previous frame's cells, so the buffer diff
            // re-emits them, and Kitty renders the new slot's placement
            // against the a=T data we just uploaded.
            lock (_lock)
            {
                if (_disposed) return;
                _currentSlot = nextSlot;
                _currentImageId = nextId;
            }

            // Delete ids retired on the previous IngestFrame.
            foreach (uint oldId in deletionsNow)
                _protocol.DeleteImage(oldId);
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
            lock (_lock)
            {
                if (_disposed) return;
                id = _currentImageId;
                cols = _placementCols;
                rows = _placementRows;
            }

            // Before the first frame is ingested, fill with background so
            // stale cells don't bleed through while FFmpeg spins up.
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

            // Placeholder cells for the placement grid. The image id encoded
            // in the cell foreground alternates between two slot ids each
            // time a new frame is ingested, so the buffer diff re-emits the
            // cells on every frame — and Kitty redraws the placement against
            // the pixel data we just uploaded via a=T to the new slot.
            int drawCols = Math.Min(cols, availW);
            int drawRows = Math.Min(rows, availH);
            Color idFg = KittyProtocol.ImageIdToForegroundColor(id);

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
                    var cell = new Cell(ImagingDefaults.KittyPlaceholder, idFg, windowBackground)
                    {
                        Combiners = combiners
                    };
                    buffer.SetCell(x, y, cell);
                }
            }

            // Fill any contentRect area outside the placement grid with
            // background (happens briefly during resize between bounds
            // change and FFmpeg restart).
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
            uint[] allIds;
            lock (_lock)
            {
                var ids = new List<uint>(2 + _pendingDeletionIds.Count);
                // Only emit deletes for slots that have actually had an a=T
                // transmission — an un-transmitted slot id is just a number
                // we reserved, Kitty doesn't know about it.
                if (_currentSlot != Slot.None)
                {
                    if (_imageIdA != 0) ids.Add(_imageIdA);
                    if (_imageIdB != 0) ids.Add(_imageIdB);
                }
                ids.AddRange(_pendingDeletionIds);
                allIds = ids.ToArray();

                _imageIdA = 0;
                _imageIdB = 0;
                _currentSlot = Slot.None;
                _currentImageId = 0;
                _transmittedPixelWidth = 0;
                _transmittedPixelHeight = 0;
                _placementCols = 0;
                _placementRows = 0;
                _pendingDeletionIds.Clear();
            }

            foreach (uint id in allIds)
                _protocol.DeleteImage(id);
        }

        public void ForceRefresh()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _refreshRequested = true;
            }
            // Actual recreation happens on the next IngestFrame — we need
            // fresh pixel data to transmit, and we don't cache the last frame.
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

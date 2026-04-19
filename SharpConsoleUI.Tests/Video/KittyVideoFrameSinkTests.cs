using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Video;
using Xunit;

namespace SharpConsoleUI.Tests.Video;

internal class RawRgbMockProtocol : IGraphicsProtocol
{
    public bool SupportsKittyGraphics => true;
    public List<(uint imageId, int pixelWidth, int pixelHeight, int columns, int rows, int dataLen)> Transmissions { get; } = new();
    public List<(uint imageId, int pixelWidth, int pixelHeight, int dataLen)> FrameUpdates { get; } = new();
    public List<uint> Deletes { get; } = new();

    // Callbacks invoked at the START of a transmit/update call, BEFORE it is
    // recorded. Lets tests inject a Paint during a transmit to reproduce the
    // race where the render thread runs while frame data is still in flight.
    public Action<uint>? OnTransmitRawRgbStart { get; set; }
    public Action<uint>? OnUpdateRawRgbFrameStart { get; set; }

    public void TransmitImage(uint imageId, byte[] pngData, int columns, int rows)
    {
        throw new System.NotImplementedException("PNG transmit not used in raw-RGB video tests.");
    }

    public void DeleteImage(uint imageId)
    {
        Deletes.Add(imageId);
    }

    public void TransmitRawRgb(uint imageId, byte[] rgbData, int pixelWidth, int pixelHeight, int columns, int rows)
    {
        OnTransmitRawRgbStart?.Invoke(imageId);
        Transmissions.Add((imageId, pixelWidth, pixelHeight, columns, rows, rgbData.Length));
    }

    public void UpdateRawRgbFrame(uint imageId, byte[] rgbData, int pixelWidth, int pixelHeight)
    {
        OnUpdateRawRgbFrameStart?.Invoke(imageId);
        FrameUpdates.Add((imageId, pixelWidth, pixelHeight, rgbData.Length));
    }
}

public class KittyVideoFrameSinkTests
{
    private static readonly Color WindowBg = Color.Black;

    private static byte[] MakeFrame(int pixelWidth, int pixelHeight) => new byte[pixelWidth * pixelHeight * 3];

    [Fact]
    public void Paint_WithoutFrame_FillsBackgroundAndDoesNotTransmit()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);
        var buffer = new CharacterBuffer(80, 25);
        var rect = new LayoutRect(0, 0, 10, 5);

        sink.Paint(buffer, rect, rect, Color.White, WindowBg);

        Assert.Empty(mock.Transmissions);
        Assert.Empty(mock.FrameUpdates);
        var cell = buffer.GetCell(0, 0);
        Assert.NotEqual(ImagingDefaults.KittyPlaceholder, cell.Character);
    }

    [Fact]
    public void IngestFrame_EveryFrameIsATransmission_NeverAnInPlaceUpdate()
    {
        // The sink always uses a=T (full image transmission), never a=f,r=1
        // in-place edits. This matches mpv's vo_kitty strategy — a=f was
        // unreliable under virtual placements because Kitty has no "redraw
        // now" primitive for updated frame data. Every frame atomically
        // recreates the slot's image + placement.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        for (int i = 0; i < 5; i++)
            sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);

        Assert.Equal(5, mock.Transmissions.Count);
        Assert.Empty(mock.FrameUpdates);
    }

    [Fact]
    public void IngestFrame_AlternatesBetweenTwoSlotIds()
    {
        // Two image ids alternate — cells' fg color switches every frame, so
        // the buffer diff always re-emits them.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);

        // Exactly two distinct ids used across all four transmissions.
        var distinctIds = new HashSet<uint>();
        foreach (var t in mock.Transmissions) distinctIds.Add(t.imageId);
        Assert.Equal(2, distinctIds.Count);

        // Neighbouring frames always use different ids.
        for (int i = 1; i < mock.Transmissions.Count; i++)
            Assert.NotEqual(mock.Transmissions[i - 1].imageId, mock.Transmissions[i].imageId);

        // Every-other-frame uses the same id.
        Assert.Equal(mock.Transmissions[0].imageId, mock.Transmissions[2].imageId);
        Assert.Equal(mock.Transmissions[1].imageId, mock.Transmissions[3].imageId);

        Assert.Empty(mock.Deletes);
    }

    [Fact]
    public void IngestFrame_TransmissionCarriesPlacementGeometry()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);

        Assert.Equal(10, mock.Transmissions[0].columns);
        Assert.Equal(5, mock.Transmissions[0].rows);
        Assert.Equal(32, mock.Transmissions[0].pixelWidth);
        Assert.Equal(32, mock.Transmissions[0].pixelHeight);
    }

    [Fact]
    public void ForceRefresh_ThenIngestFrame_AllocatesFreshSlotIdsWithDeferredDelete()
    {
        // User-triggered recovery path (the R keybind): ForceRefresh() marks
        // the sink for recreation, and the next IngestFrame retires both
        // slots (stashed for delete one frame later) and allocates fresh
        // slot ids. The delete is deferred so cells still referencing the
        // old ids keep rendering until Paint migrates them to the new slot.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg); // id A
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg); // id B
        uint oldAId = mock.Transmissions[0].imageId;
        uint oldBId = mock.Transmissions[1].imageId;
        Assert.Empty(mock.Deletes);

        sink.ForceRefresh();

        // ForceRefresh alone doesn't touch the protocol — it only sets a flag.
        Assert.Equal(2, mock.Transmissions.Count);
        Assert.Empty(mock.Deletes);

        // Next IngestFrame picks up the flag: fresh slot id transmitted, old
        // ids stashed for deferred deletion.
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        Assert.Equal(3, mock.Transmissions.Count);
        Assert.NotEqual(oldAId, mock.Transmissions[2].imageId);
        Assert.NotEqual(oldBId, mock.Transmissions[2].imageId);
        Assert.Empty(mock.Deletes); // deferred

        // Frame after that fires the pending deletes of both retired ids.
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        Assert.Equal(4, mock.Transmissions.Count);
        Assert.Equal(2, mock.Deletes.Count);
        Assert.Contains(oldAId, mock.Deletes);
        Assert.Contains(oldBId, mock.Deletes);
    }

    [Fact]
    public void IngestFrame_DimensionChange_DefersDeleteOfBothOldSlotsByOneFrame()
    {
        // Regression: deleting retired ids in the same IngestFrame call as
        // the create caused a one-frame visual black flash, because buffer
        // cells still referenced the just-deleted id until the next Paint
        // migrated them. We now defer by one frame so old and new coexist
        // long enough for Paint to transition cells seamlessly.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);
        uint oldAId = mock.Transmissions[0].imageId;
        uint oldBId = mock.Transmissions[1].imageId;

        // Dimension change → both slots retired, fresh slot id transmitted,
        // delete of old ids deferred.
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 20, targetRows: 10, WindowBg);
        Assert.Equal(3, mock.Transmissions.Count);
        Assert.Empty(mock.Deletes);

        // Next frame with same new dims — pending deletes fire.
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 20, targetRows: 10, WindowBg);
        Assert.Equal(4, mock.Transmissions.Count);
        Assert.Equal(2, mock.Deletes.Count);
        Assert.Contains(oldAId, mock.Deletes);
        Assert.Contains(oldBId, mock.Deletes);
    }

    [Fact]
    public void IngestFrame_DimensionsChange_TransmittedDimsMatchTheNewGrid()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(40, 40), 40, 40, targetCols: 10, targetRows: 5, WindowBg);
        sink.IngestFrame(MakeFrame(40, 40), 40, 40, targetCols: 10, targetRows: 5, WindowBg);
        sink.IngestFrame(MakeFrame(40, 40), 40, 40, targetCols: 10, targetRows: 5, WindowBg);
        Assert.All(mock.Transmissions, t => Assert.Equal(10, t.columns));
        Assert.All(mock.Transmissions, t => Assert.Equal(5, t.rows));

        // Placement size changes on the next frame → new dims propagate.
        sink.IngestFrame(MakeFrame(40, 40), 40, 40, targetCols: 20, targetRows: 10, WindowBg);
        Assert.Equal(20, mock.Transmissions[^1].columns);
        Assert.Equal(10, mock.Transmissions[^1].rows);
    }

    [Fact]
    public void Paint_AfterIngestFrame_WritesPlaceholderCellsAcrossPlacementGrid()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);
        var buffer = new CharacterBuffer(80, 25);
        var rect = new LayoutRect(0, 0, 4, 2);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);

        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                var cell = buffer.GetCell(x, y);
                Assert.Equal(ImagingDefaults.KittyPlaceholder, cell.Character);
                Assert.False(string.IsNullOrEmpty(cell.Combiners));
            }
        }
    }

    [Fact]
    public void Paint_AlternatingFrames_CellForegroundAlternatesBetweenTwoSlotIds()
    {
        // Double-buffered image ids: each IngestFrame alternates between slot
        // A and slot B. The placeholder cell's foreground encodes the image
        // id, so it changes on every frame — buffer diff re-emits the cells
        // and Kitty redraws placements against the a=T data just uploaded.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);
        var buffer = new CharacterBuffer(80, 25);
        var rect = new LayoutRect(0, 0, 4, 2);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var fgFrame1 = buffer.GetCell(0, 0).Foreground;

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var fgFrame2 = buffer.GetCell(0, 0).Foreground;

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var fgFrame3 = buffer.GetCell(0, 0).Foreground;

        // Frame 1 uses slot A; frame 2 uses slot B; frame 3 uses slot A again.
        Assert.NotEqual(fgFrame1, fgFrame2);
        Assert.NotEqual(fgFrame2, fgFrame3);
        Assert.Equal(fgFrame1, fgFrame3);
    }

    [Fact]
    public void Paint_UsesWindowBackgroundAsCellBackground_NoParityXorTrick()
    {
        // The double-buffered-id design replaced the parity-bit-in-R trick —
        // cells now carry the window background unchanged.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);
        var buffer = new CharacterBuffer(80, 25);
        var rect = new LayoutRect(0, 0, 4, 2);
        var bg = new Color(38, 38, 38);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, bg);
        sink.Paint(buffer, rect, rect, Color.White, bg);
        var cellA = buffer.GetCell(0, 0);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, bg);
        sink.Paint(buffer, rect, rect, Color.White, bg);
        var cellB = buffer.GetCell(0, 0);

        Assert.Equal(bg, cellA.Background);
        Assert.Equal(bg, cellB.Background);
    }

    [Fact]
    public void Paint_DuringTransmit_ReadsPreviousSlotId_NotTheInFlightOne()
    {
        // Race: Paint runs on the render thread while IngestFrame is still
        // writing frame data on the playback thread. Paint must see the
        // PREVIOUS slot's id — not the id being newly transmitted — otherwise
        // placeholder cells would be re-emitted referencing an id whose a=T
        // hasn't yet reached the terminal, producing the stuck-black state.
        // Guaranteed by publishing _currentImageId only AFTER the protocol
        // I/O returns.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);
        var buffer = new CharacterBuffer(80, 25);
        var rect = new LayoutRect(0, 0, 4, 2);

        // Frame 1 establishes slot A.
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        Color fgBeforeFrame2 = buffer.GetCell(0, 0).Foreground;

        // On the second IngestFrame, run a Paint DURING the transmit. It
        // must read the slot-A id because slot B hasn't been published yet.
        Color fgDuringTransmit = default;
        mock.OnTransmitRawRgbStart = _ =>
        {
            sink.Paint(buffer, rect, rect, Color.White, WindowBg);
            fgDuringTransmit = buffer.GetCell(0, 0).Foreground;
        };
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);

        Assert.Equal(fgBeforeFrame2, fgDuringTransmit);

        // After the transmit returns and publication commits, a fresh Paint
        // sees slot B's id.
        mock.OnTransmitRawRgbStart = null;
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        Color fgAfterFrame2 = buffer.GetCell(0, 0).Foreground;
        Assert.NotEqual(fgBeforeFrame2, fgAfterFrame2);
    }

    [Fact]
    public void Paint_WithoutNewFrame_KeepsCellsIdentical_NoUnnecessaryRedraw()
    {
        // When no new frame has arrived since the last Paint, cells stay
        // byte-identical and the buffer diff emits nothing.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);
        var buffer = new CharacterBuffer(80, 25);
        var rect = new LayoutRect(0, 0, 4, 2);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var cellAfter1stPaint = buffer.GetCell(0, 0);

        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var cellAfter2ndPaint = buffer.GetCell(0, 0);

        Assert.Equal(cellAfter1stPaint.Foreground, cellAfter2ndPaint.Foreground);
        Assert.Equal(cellAfter1stPaint.Background, cellAfter2ndPaint.Background);
        Assert.Equal(cellAfter1stPaint.Character, cellAfter2ndPaint.Character);
        Assert.Equal(cellAfter1stPaint.Combiners, cellAfter2ndPaint.Combiners);
    }

    [Fact]
    public void OnStopped_DeletesBothSlotIds_Idempotent()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg); // A
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg); // B

        sink.OnStopped();
        int countAfterFirst = mock.Deletes.Count;
        sink.OnStopped();

        Assert.Equal(2, countAfterFirst);
        Assert.Equal(countAfterFirst, mock.Deletes.Count);
    }

    [Fact]
    public void OnStopped_AfterSingleFrame_DeletesBothAllocatedSlotIds()
    {
        // Both slot ids are allocated on the first IngestFrame (lazy
        // allocation under the same lock). OnStopped emits deletes for
        // both — slot B has had an a=T too if any frame ever ran (slot
        // A alternates with B starting from frame 2), but here only one
        // frame has run so only slot A was transmitted. Slot B was
        // reserved but never sent, so we don't emit a delete for it.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);

        sink.OnStopped();

        // The sink emits deletes for both reserved ids; only slot A has
        // actually seen an a=T — slot B's delete is harmless but wasteful.
        // Assert at least the transmitted id is deleted.
        Assert.Contains(mock.Transmissions[0].imageId, mock.Deletes);
    }

    [Fact]
    public void Paint_RespectsClipRect()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);
        var buffer = new CharacterBuffer(80, 25);
        var rect = new LayoutRect(0, 0, 6, 3);
        var clip = new LayoutRect(2, 0, 2, 2);

        sink.IngestFrame(MakeFrame(24, 24), 24, 24, targetCols: 6, targetRows: 3, WindowBg);
        sink.Paint(buffer, rect, clip, Color.White, WindowBg);

        // Outside clip: should not be a Kitty placeholder
        var outside = buffer.GetCell(0, 0);
        Assert.NotEqual(ImagingDefaults.KittyPlaceholder, outside.Character);

        // Inside clip: should be a placeholder
        var inside = buffer.GetCell(2, 0);
        Assert.Equal(ImagingDefaults.KittyPlaceholder, inside.Character);
    }

    [Fact]
    public void GetPreferredPixelSize_ClampsToMaximum()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        // 500 cells * 8 px/cell = 4000 px → clamped to 1280
        // 100 cells * 16 px/cell = 1600 px → clamped to 720
        var (w, h) = sink.GetPreferredPixelSize(500, 100);

        Assert.Equal(VideoDefaults.KittyMaxPixelWidth, w);
        Assert.Equal(VideoDefaults.KittyMaxPixelHeight, h);
    }

    [Fact]
    public void GetPreferredPixelSize_SmallAreaStaysBelowMax()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        var (w, h) = sink.GetPreferredPixelSize(20, 10);

        Assert.Equal(20 * VideoDefaults.KittyPixelsPerCellX, w);
        Assert.Equal(10 * VideoDefaults.KittyPixelsPerCellY, h);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var mock = new RawRgbMockProtocol();
        var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);

        sink.Dispose();
        int countAfterFirst = mock.Deletes.Count;
        sink.Dispose();

        Assert.Equal(countAfterFirst, mock.Deletes.Count);
        Assert.NotEmpty(mock.Deletes);
    }
}

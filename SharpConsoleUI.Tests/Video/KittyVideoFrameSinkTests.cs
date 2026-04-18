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
        Transmissions.Add((imageId, pixelWidth, pixelHeight, columns, rows, rgbData.Length));
    }

    public void UpdateRawRgbFrame(uint imageId, byte[] rgbData, int pixelWidth, int pixelHeight)
    {
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
    public void IngestFrame_FirstFrame_UsesTransmitToCreateImageAndPlacement()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);

        Assert.Single(mock.Transmissions);
        Assert.Empty(mock.FrameUpdates);
        Assert.Equal(10, mock.Transmissions[0].columns);
        Assert.Equal(5, mock.Transmissions[0].rows);
        Assert.Equal(32, mock.Transmissions[0].pixelWidth);
        Assert.Equal(32, mock.Transmissions[0].pixelHeight);
    }

    [Fact]
    public void IngestFrame_SubsequentFramesWithSameSize_UseFrameUpdateNotTransmit()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);

        // Exactly one transmit (creation); two subsequent frames are in-place updates.
        Assert.Single(mock.Transmissions);
        Assert.Equal(2, mock.FrameUpdates.Count);
        // All operations must target the same image id — the placement stays alive.
        Assert.Equal(mock.Transmissions[0].imageId, mock.FrameUpdates[0].imageId);
        Assert.Equal(mock.Transmissions[0].imageId, mock.FrameUpdates[1].imageId);
        Assert.Empty(mock.Deletes);
    }

    [Fact]
    public void ForceRefresh_ThenIngestFrame_RecreatesImageWithDeferredDelete()
    {
        // User-triggered recovery path (the R keybind): ForceRefresh() marks the sink for
        // recreation, and the next IngestFrame transmits a fresh image + stashes the old
        // id for deletion one frame later — same sequencing as the dimension-change path
        // so the cell id transition stays flash-free.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        uint firstId = mock.Transmissions[0].imageId;
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        Assert.Single(mock.Transmissions);
        Assert.Single(mock.FrameUpdates);
        Assert.Empty(mock.Deletes);

        sink.ForceRefresh();

        // ForceRefresh alone doesn't touch the protocol — it only sets a flag.
        Assert.Single(mock.Transmissions);
        Assert.Empty(mock.Deletes);

        // Next IngestFrame picks up the flag: new image transmitted, old image stashed.
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        Assert.Equal(2, mock.Transmissions.Count);
        Assert.NotEqual(firstId, mock.Transmissions[1].imageId);
        Assert.Empty(mock.Deletes); // deferred so cells can migrate

        // Frame after that fires the pending delete.
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        Assert.Single(mock.Deletes);
        Assert.Equal(firstId, mock.Deletes[0]);
    }

    [Fact]
    public void IngestFrame_OldImageAfterDimensionChange_DeletedOneFrameLater_NotImmediately()
    {
        // Regression: deleting the old image in the same IngestFrame call as the create
        // caused a one-frame visual black flash, because the buffer-level cells still
        // referenced the just-deleted id until the next Paint migrated them. We now
        // defer the delete to the NEXT IngestFrame so the old and new images coexist
        // for one frame — long enough for Paint to transition cells seamlessly.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 10, targetRows: 5, WindowBg);

        // Dimension change triggers a recreate — but the delete must be deferred.
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 20, targetRows: 10, WindowBg);
        Assert.Equal(2, mock.Transmissions.Count);
        Assert.Empty(mock.Deletes);

        // Next frame (same new dims) — now the pending delete fires.
        sink.IngestFrame(MakeFrame(32, 32), 32, 32, targetCols: 20, targetRows: 10, WindowBg);
        Assert.Single(mock.Deletes);
        Assert.Equal(mock.Transmissions[0].imageId, mock.Deletes[0]);
    }

    [Fact]
    public void IngestFrame_DimensionsChange_DeletesOldImageAndCreatesFresh()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(40, 40), 40, 40, targetCols: 10, targetRows: 5, WindowBg);
        uint firstId = mock.Transmissions[0].imageId;

        // Same dimensions → frame update path
        sink.IngestFrame(MakeFrame(40, 40), 40, 40, targetCols: 10, targetRows: 5, WindowBg);
        Assert.Single(mock.Transmissions);
        Assert.Single(mock.FrameUpdates);

        // Placement size changes → transmit fresh image (a=f requires fixed s/v), and
        // STASH the old image's id for deletion on the NEXT IngestFrame — not immediate,
        // so cells currently referencing the old id don't show black before Paint has
        // had a chance to migrate them to the new id.
        sink.IngestFrame(MakeFrame(40, 40), 40, 40, targetCols: 20, targetRows: 10, WindowBg);
        Assert.Equal(2, mock.Transmissions.Count);
        Assert.Equal(20, mock.Transmissions[1].columns);
        Assert.Equal(10, mock.Transmissions[1].rows);
        Assert.Empty(mock.Deletes); // deferred — old image still alive for one frame
        Assert.NotEqual(firstId, mock.Transmissions[1].imageId);

        // Next frame actually fires the pending delete of the old image.
        sink.IngestFrame(MakeFrame(40, 40), 40, 40, targetCols: 20, targetRows: 10, WindowBg);
        Assert.Single(mock.Deletes);
        Assert.Equal(firstId, mock.Deletes[0]);
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
    public void Paint_FlipsCellBackgroundBitWhenNewFrameArrived_ToForceRedraw()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);
        var buffer = new CharacterBuffer(80, 25);
        var rect = new LayoutRect(0, 0, 4, 2);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var cellFrame1 = buffer.GetCell(0, 0);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var cellFrame2 = buffer.GetCell(0, 0);

        // Image id (fg), combiners, and character stay constant — the placement is stable.
        Assert.Equal(cellFrame1.Character, cellFrame2.Character);
        Assert.Equal(cellFrame1.Foreground, cellFrame2.Foreground);
        Assert.Equal(cellFrame1.Combiners, cellFrame2.Combiners);

        // Background R channel differs by exactly one bit between consecutive frames —
        // invisible to the user (opaque Kitty pixels cover the cell background) but enough
        // for the buffer diff to re-emit the placeholder cells, which prompts Kitty to
        // repaint the updated frame data.
        Assert.NotEqual(cellFrame1.Background.R, cellFrame2.Background.R);
        Assert.Equal(1, Math.Abs(cellFrame1.Background.R - cellFrame2.Background.R));
        Assert.Equal(cellFrame1.Background.G, cellFrame2.Background.G);
        Assert.Equal(cellFrame1.Background.B, cellFrame2.Background.B);
    }

    [Fact]
    public void Paint_MultipleIngestFramesBetweenPaints_StillForcesRedrawOnNextPaint()
    {
        // Regression: the earlier toggle-on-IngestFrame design failed when an even number
        // of IngestFrames slipped between Paints — parity would land on its previous
        // value, cells would be byte-identical, the buffer diff would emit nothing, and
        // Kitty would hold stale pixels until something else disturbed the placement
        // (window move, resize). Now parity is decided inside Paint based on "has a new
        // frame landed since our last Paint?" so the outcome is correct regardless of
        // how many IngestFrames ran in between.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);
        var buffer = new CharacterBuffer(80, 25);
        var rect = new LayoutRect(0, 0, 4, 2);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var cellAfter1stPaint = buffer.GetCell(0, 0);

        // Two IngestFrames before the next Paint — a pure toggle would land back on the
        // previous parity here.
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var cellAfter2ndPaint = buffer.GetCell(0, 0);

        Assert.NotEqual(cellAfter1stPaint.Background.R, cellAfter2ndPaint.Background.R);
    }

    [Fact]
    public void Paint_WithoutNewFrame_KeepsCellsIdentical_NoUnnecessaryRedraw()
    {
        // When no new frame has arrived since the last Paint (e.g. Paint triggered by an
        // unrelated invalidation), we don't want to force Kitty to redraw — just leave the
        // cells exactly as they were so the buffer diff emits nothing.
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);
        var buffer = new CharacterBuffer(80, 25);
        var rect = new LayoutRect(0, 0, 4, 2);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var cellAfter1stPaint = buffer.GetCell(0, 0);

        // No IngestFrame between paints.
        sink.Paint(buffer, rect, rect, Color.White, WindowBg);
        var cellAfter2ndPaint = buffer.GetCell(0, 0);

        Assert.Equal(cellAfter1stPaint.Background, cellAfter2ndPaint.Background);
    }

    [Fact]
    public void OnStopped_DeletesTheStableImageExactlyOnce()
    {
        var mock = new RawRgbMockProtocol();
        using var sink = new KittyVideoFrameSink(mock);

        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);
        sink.IngestFrame(MakeFrame(16, 16), 16, 16, targetCols: 4, targetRows: 2, WindowBg);

        sink.OnStopped();
        sink.OnStopped();

        Assert.Single(mock.Deletes);
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

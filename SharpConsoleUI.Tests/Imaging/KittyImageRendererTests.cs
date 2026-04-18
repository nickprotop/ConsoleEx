using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Imaging;

internal class MockGraphicsProtocol : IGraphicsProtocol
{
    public bool SupportsKittyGraphics => true;
    public List<(uint imageId, byte[] pngData, int columns, int rows)> TransmittedImages { get; } = new();
    public List<uint> DeletedImages { get; } = new();

    public void TransmitImage(uint imageId, byte[] pngData, int columns, int rows)
    {
        TransmittedImages.Add((imageId, pngData, columns, rows));
    }

    public void DeleteImage(uint imageId)
    {
        DeletedImages.Add(imageId);
    }
}

public class KittyImageRendererTests
{
    private static readonly Color WindowBg = Color.Black;

    /// <summary>
    /// Paints twice with a delay to allow async PNG encoding to complete.
    /// First paint triggers encoding + shows "Loading...", second paint transmits.
    /// </summary>
    private static void PaintWithAsyncWait(
        KittyImageRenderer renderer,
        CharacterBuffer buffer,
        LayoutRect bounds,
        LayoutRect clipRect,
        PixelBuffer pixels,
        ImageScaleMode scaleMode,
        int cropOffsetX, int cropOffsetY,
        int renderCols, int renderRows)
    {
        // First paint: starts async encoding, renders loading placeholder
        renderer.Paint(buffer, bounds, clipRect, pixels, scaleMode,
            cropOffsetX, cropOffsetY, renderCols, renderRows, WindowBg);

        // Wait for async encoding to complete
        Thread.Sleep(500);

        // Second paint: PNG is ready, transmits and writes placeholder cells
        renderer.Paint(buffer, bounds, clipRect, pixels, scaleMode,
            cropOffsetX, cropOffsetY, renderCols, renderRows, WindowBg);
    }

    [Fact]
    public void Paint_FirstCall_ShowsLoadingPlaceholder()
    {
        var mock = new MockGraphicsProtocol();
        using var renderer = new KittyImageRenderer(mock);
        var pixels = new PixelBuffer(4, 4);
        var buffer = new CharacterBuffer(80, 25);
        var bounds = new LayoutRect(0, 0, 4, 2);

        // First paint should NOT transmit — shows loading instead
        renderer.Paint(buffer, bounds, bounds, pixels, ImageScaleMode.None,
            cropOffsetX: 0, cropOffsetY: 0, renderCols: 4, renderRows: 2, windowBackground: WindowBg);

        Assert.Empty(mock.TransmittedImages);
    }

    [Fact]
    public void Paint_TransmitsImageAfterAsyncEncoding()
    {
        var mock = new MockGraphicsProtocol();
        using var renderer = new KittyImageRenderer(mock);
        var pixels = new PixelBuffer(4, 4);
        var buffer = new CharacterBuffer(80, 25);
        var bounds = new LayoutRect(0, 0, 4, 2);

        PaintWithAsyncWait(renderer, buffer, bounds, bounds, pixels,
            ImageScaleMode.None, 0, 0, 4, 2);

        Assert.Single(mock.TransmittedImages);
        Assert.Equal(4, mock.TransmittedImages[0].columns);
        Assert.Equal(2, mock.TransmittedImages[0].rows);
        Assert.True(mock.TransmittedImages[0].pngData.Length > 0);
    }

    [Fact]
    public void Paint_WritesPlaceholderCellsAfterEncoding()
    {
        var mock = new MockGraphicsProtocol();
        using var renderer = new KittyImageRenderer(mock);
        var pixels = new PixelBuffer(4, 4);
        var buffer = new CharacterBuffer(80, 25);
        var bounds = new LayoutRect(0, 0, 4, 2);

        PaintWithAsyncWait(renderer, buffer, bounds, bounds, pixels,
            ImageScaleMode.None, 0, 0, 4, 2);

        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                var cell = buffer.GetCell(x, y);
                Assert.Equal(ImagingDefaults.KittyPlaceholder, cell.Character);
                Assert.NotNull(cell.Combiners);
                Assert.True(cell.Combiners.Length > 0);
            }
        }
    }

    [Fact]
    public void Paint_DoesNotRetransmitOnSecondCall_SameSourceAndDimensions()
    {
        var mock = new MockGraphicsProtocol();
        using var renderer = new KittyImageRenderer(mock);
        var pixels = new PixelBuffer(4, 4);
        var buffer = new CharacterBuffer(80, 25);
        var bounds = new LayoutRect(0, 0, 4, 2);

        PaintWithAsyncWait(renderer, buffer, bounds, bounds, pixels,
            ImageScaleMode.None, 0, 0, 4, 2);

        // Third paint with same source/dimensions
        renderer.Paint(buffer, bounds, bounds, pixels, ImageScaleMode.None,
            cropOffsetX: 0, cropOffsetY: 0, renderCols: 4, renderRows: 2, windowBackground: WindowBg);

        Assert.Single(mock.TransmittedImages); // Only transmitted once
    }

    [Fact]
    public void Paint_ResizeRetransmitsCachedPng_NoReencode()
    {
        var mock = new MockGraphicsProtocol();
        using var renderer = new KittyImageRenderer(mock);
        var pixels = new PixelBuffer(10, 10);
        var buffer = new CharacterBuffer(80, 25);

        // First: encode + transmit at 10x5
        PaintWithAsyncWait(renderer, buffer,
            new LayoutRect(0, 0, 10, 5), new LayoutRect(0, 0, 10, 5), pixels,
            ImageScaleMode.Fit, 0, 0, 10, 5);

        // Resize to 5x3 — should retransmit cached PNG (no re-encode), instant
        renderer.Paint(buffer, new LayoutRect(0, 0, 5, 3), new LayoutRect(0, 0, 5, 3), pixels,
            ImageScaleMode.Fit, 0, 0, renderCols: 5, renderRows: 3, windowBackground: WindowBg);

        Assert.Equal(2, mock.TransmittedImages.Count);
        Assert.Single(mock.DeletedImages); // Old image deleted before retransmit
        // Both transmissions used the same PNG data (same source, just different c/r)
        Assert.Equal(mock.TransmittedImages[0].pngData, mock.TransmittedImages[1].pngData);
    }

    [Fact]
    public void OnSourceChanged_DeletesCurrentImage()
    {
        var mock = new MockGraphicsProtocol();
        using var renderer = new KittyImageRenderer(mock);
        var pixels = new PixelBuffer(4, 4);
        var buffer = new CharacterBuffer(80, 25);
        var bounds = new LayoutRect(0, 0, 4, 2);

        PaintWithAsyncWait(renderer, buffer, bounds, bounds, pixels,
            ImageScaleMode.None, 0, 0, 4, 2);

        renderer.OnSourceChanged();

        Assert.Single(mock.DeletedImages);
    }

    [Fact]
    public void Dispose_DeletesCurrentImage()
    {
        var mock = new MockGraphicsProtocol();
        var renderer = new KittyImageRenderer(mock);
        var pixels = new PixelBuffer(4, 4);
        var buffer = new CharacterBuffer(80, 25);
        var bounds = new LayoutRect(0, 0, 4, 2);

        PaintWithAsyncWait(renderer, buffer, bounds, bounds, pixels,
            ImageScaleMode.None, 0, 0, 4, 2);

        renderer.Dispose();

        Assert.Single(mock.DeletedImages);
    }

    [Fact]
    public void Paint_RespectsClipRect()
    {
        var mock = new MockGraphicsProtocol();
        using var renderer = new KittyImageRenderer(mock);
        var pixels = new PixelBuffer(4, 4);
        var buffer = new CharacterBuffer(80, 25);
        var bounds = new LayoutRect(0, 0, 4, 2);
        var clip = new LayoutRect(1, 0, 2, 1);

        PaintWithAsyncWait(renderer, buffer, bounds, clip, pixels,
            ImageScaleMode.None, 0, 0, 4, 2);

        var outsideCell = buffer.GetCell(0, 0);
        Assert.NotEqual(ImagingDefaults.KittyPlaceholder, outsideCell.Character);

        var insideCell = buffer.GetCell(1, 0);
        Assert.Equal(ImagingDefaults.KittyPlaceholder, insideCell.Character);
    }
}

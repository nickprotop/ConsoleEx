// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Imaging
{
    /// <summary>
    /// Renders images using the Kitty graphics protocol with virtual placements.
    /// Transmits image data as PNG via <see cref="IGraphicsProtocol"/> and writes
    /// U+10EEEE placeholder cells into the <see cref="CharacterBuffer"/>.
    /// The terminal replaces placeholders with actual image pixels.
    /// PNG encoding happens asynchronously to avoid blocking the UI thread.
    /// </summary>
    internal sealed class KittyImageRenderer : IImageRenderer
    {
        private static uint _nextImageId;

        private readonly IGraphicsProtocol _protocol;
        private uint _currentImageId;
        private bool _hasTransmittedImage;

        // PNG cache — encode once per source, reuse across resizes
        private PixelBuffer? _cachedPngSource;
        private volatile byte[]? _cachedPngData;
        private volatile bool _isEncoding;

        // Transmission tracking — retransmit when dimensions change
        private int _transmittedCols;
        private int _transmittedRows;

        // Container reference for async invalidation
        private IContainer? _container;

        public KittyImageRenderer(IGraphicsProtocol protocol)
        {
            _protocol = protocol;
        }

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
            int displayWidth = Math.Min(renderCols - cropOffsetX, destRect.Width);
            int displayHeight = Math.Min(renderRows - cropOffsetY, destRect.Height);

            // Check if we need to encode a new PNG (source changed)
            bool sourceChanged = !ReferenceEquals(_cachedPngSource, source);
            if (sourceChanged)
            {
                // Invalidate cached PNG
                _cachedPngData = null;
                _cachedPngSource = source;

                // Delete old transmitted image
                if (_hasTransmittedImage)
                {
                    _protocol.DeleteImage(_currentImageId);
                    _hasTransmittedImage = false;
                }
            }

            // If PNG not yet encoded, start async encoding and show loading placeholder
            if (_cachedPngData == null)
            {
                if (!_isEncoding)
                {
                    _isEncoding = true;
                    var sourceRef = source;
                    Task.Run(() =>
                    {
                        var pngData = KittyProtocol.EncodePng(sourceRef);
                        _cachedPngData = pngData;
                        _isEncoding = false;
                        _container?.Invalidate(true);
                    });
                }

                // Render "Loading..." placeholder
                RenderLoadingPlaceholder(buffer, destRect, clipRect, displayWidth, displayHeight, windowBackground);
                return;
            }

            // PNG is ready — transmit if needed (new image or dimensions changed)
            bool needsTransmit = !_hasTransmittedImage
                || _transmittedCols != renderCols
                || _transmittedRows != renderRows;

            if (needsTransmit)
            {
                if (_hasTransmittedImage)
                    _protocol.DeleteImage(_currentImageId);

                _currentImageId = Interlocked.Increment(ref _nextImageId);
                _protocol.TransmitImage(_currentImageId, _cachedPngData, renderCols, renderRows);

                _transmittedCols = renderCols;
                _transmittedRows = renderRows;
                _hasTransmittedImage = true;
            }

            // Write placeholder cells — image ID encoded as foreground color per Kitty spec
            Color idFg = KittyProtocol.ImageIdToForegroundColor(_currentImageId);

            for (int cy = 0; cy < displayHeight && destRect.Y + cy < destRect.Bottom; cy++)
            {
                int y = destRect.Y + cy;
                if (y < clipRect.Y || y >= clipRect.Bottom) continue;

                for (int cx = 0; cx < displayWidth && destRect.X + cx < destRect.Right; cx++)
                {
                    int x = destRect.X + cx;
                    if (x < clipRect.X || x >= clipRect.Right) continue;

                    int imageRow = cropOffsetY + cy;
                    int imageCol = cropOffsetX + cx;
                    string combiners = KittyProtocol.BuildPlaceholderCombiners(imageRow, imageCol);

                    var cell = new Cell(ImagingDefaults.KittyPlaceholder, idFg, windowBackground)
                    {
                        Combiners = combiners
                    };
                    buffer.SetCell(x, y, cell);
                }
            }
        }

        /// <summary>
        /// Renders a centered "Loading..." text in the image area while PNG encoding is in progress.
        /// </summary>
        private static void RenderLoadingPlaceholder(
            CharacterBuffer buffer,
            LayoutRect destRect,
            LayoutRect clipRect,
            int displayWidth,
            int displayHeight,
            Color windowBackground)
        {
            const string loadingText = "Loading\u2026";
            int textLen = loadingText.Length;

            int textX = destRect.X + Math.Max(0, (displayWidth - textLen) / 2);
            int textY = destRect.Y + displayHeight / 2;

            Color fg = new Color(160, 160, 160);

            // Fill area with background
            for (int cy = 0; cy < displayHeight && destRect.Y + cy < destRect.Bottom; cy++)
            {
                int y = destRect.Y + cy;
                if (y < clipRect.Y || y >= clipRect.Bottom) continue;

                for (int cx = 0; cx < displayWidth && destRect.X + cx < destRect.Right; cx++)
                {
                    int x = destRect.X + cx;
                    if (x < clipRect.X || x >= clipRect.Right) continue;

                    if (y == textY && x >= textX && x < textX + textLen)
                    {
                        int charIdx = x - textX;
                        buffer.SetNarrowCell(x, y, loadingText[charIdx], fg, windowBackground);
                    }
                    else
                    {
                        buffer.SetNarrowCell(x, y, ' ', fg, windowBackground);
                    }
                }
            }
        }

        /// <summary>
        /// Stores the container reference for async invalidation callbacks.
        /// Called by the renderer when the container is available.
        /// </summary>
        internal void SetContainer(IContainer? container)
        {
            _container = container;
        }

        /// <inheritdoc />
        public void OnSourceChanged()
        {
            if (_hasTransmittedImage)
            {
                _protocol.DeleteImage(_currentImageId);
                _hasTransmittedImage = false;
            }

            _cachedPngData = null;
            _cachedPngSource = null;
            _transmittedCols = 0;
            _transmittedRows = 0;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_hasTransmittedImage)
            {
                _protocol.DeleteImage(_currentImageId);
                _hasTransmittedImage = false;
            }
        }
    }
}

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
    /// Strategy interface for rendering a PixelBuffer into a CharacterBuffer.
    /// Implementations provide different rendering backends (half-block characters,
    /// Kitty graphics protocol, etc.).
    /// </summary>
    internal interface IImageRenderer : IDisposable
    {
        /// <summary>
        /// Paint the image into the character buffer at the given bounds.
        /// </summary>
        /// <param name="buffer">The target character buffer.</param>
        /// <param name="destRect">Destination rectangle within the buffer (after margins).</param>
        /// <param name="clipRect">Clipping rectangle.</param>
        /// <param name="source">The pixel data to render.</param>
        /// <param name="scaleMode">How the image should be scaled.</param>
        /// <param name="cropOffsetX">Horizontal crop offset into the rendered image (for Fill mode).</param>
        /// <param name="cropOffsetY">Vertical crop offset into the rendered image (for Fill mode).</param>
        /// <param name="renderCols">Target render width in cell columns.</param>
        /// <param name="renderRows">Target render height in cell rows.</param>
        /// <param name="windowBackground">Background color for transparency/odd-height handling.</param>
        void Paint(
            CharacterBuffer buffer,
            LayoutRect destRect,
            LayoutRect clipRect,
            PixelBuffer source,
            ImageScaleMode scaleMode,
            int cropOffsetX,
            int cropOffsetY,
            int renderCols,
            int renderRows,
            Color windowBackground);

        /// <summary>
        /// Called when the image source changes. Allows cleanup of transmitted images.
        /// </summary>
        void OnSourceChanged();
    }
}

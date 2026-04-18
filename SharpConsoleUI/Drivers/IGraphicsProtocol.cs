// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Drivers
{
    /// <summary>
    /// Interface for terminal graphics protocol support (e.g. Kitty graphics protocol).
    /// Implemented by console drivers that can transmit and display pixel-based images
    /// directly in the terminal, bypassing character-cell rendering.
    /// </summary>
    public interface IGraphicsProtocol
    {
        /// <summary>
        /// Whether the terminal supports Kitty graphics protocol.
        /// </summary>
        bool SupportsKittyGraphics { get; }

        /// <summary>
        /// Transmit an image to the terminal for virtual placement.
        /// The image is assigned the given ID and sized to span the specified number
        /// of terminal columns and rows.
        /// </summary>
        /// <param name="imageId">Unique image identifier.</param>
        /// <param name="pngData">PNG-encoded image data.</param>
        /// <param name="columns">Number of terminal columns the image spans.</param>
        /// <param name="rows">Number of terminal rows the image spans.</param>
        void TransmitImage(uint imageId, byte[] pngData, int columns, int rows);

        /// <summary>
        /// Delete a previously transmitted image from the terminal.
        /// </summary>
        /// <param name="imageId">The image identifier to delete.</param>
        void DeleteImage(uint imageId);

        /// <summary>
        /// Transmit a raw RGB24 frame to the terminal, sized to span the given cell area.
        /// Retransmitting with the same <paramref name="imageId"/> updates the image in place,
        /// which is how high-frame-rate video playback works without per-frame delete churn.
        /// The default implementation throws <see cref="NotSupportedException"/>; drivers that
        /// support streaming raw frames (such as <c>NetConsoleDriver</c> for Kitty graphics)
        /// override it.
        /// </summary>
        /// <param name="imageId">Unique image identifier. Reuse across frames for in-place updates.</param>
        /// <param name="rgbData">Raw RGB24 pixel data, <paramref name="pixelWidth"/> * <paramref name="pixelHeight"/> * 3 bytes, row-major.</param>
        /// <param name="pixelWidth">Pixel width of the frame.</param>
        /// <param name="pixelHeight">Pixel height of the frame.</param>
        /// <param name="columns">Number of terminal columns the image spans.</param>
        /// <param name="rows">Number of terminal rows the image spans.</param>
        void TransmitRawRgb(uint imageId, byte[] rgbData, int pixelWidth, int pixelHeight, int columns, int rows)
        {
            throw new NotSupportedException(
                "This driver does not support transmitting raw RGB frames. " +
                "Use TransmitImage with PNG data or implement TransmitRawRgb in your driver.");
        }

        /// <summary>
        /// Update the root-frame pixel data of a previously transmitted image with fresh raw
        /// RGB24 bytes, without affecting any active placements. This is the correct primitive
        /// for streaming video: the existing virtual placements keep referencing the image and
        /// the terminal redraws them against the new pixels. Issuing <see cref="TransmitRawRgb"/>
        /// with the same id would instead <b>delete</b> all existing placements per the Kitty
        /// protocol spec, which would cause the image to disappear until placeholder cells are
        /// re-emitted.
        /// </summary>
        void UpdateRawRgbFrame(uint imageId, byte[] rgbData, int pixelWidth, int pixelHeight)
        {
            throw new NotSupportedException(
                "This driver does not support in-place raw RGB frame updates. " +
                "Implement UpdateRawRgbFrame in your driver to enable smooth video playback.");
        }
    }
}

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
    }
}

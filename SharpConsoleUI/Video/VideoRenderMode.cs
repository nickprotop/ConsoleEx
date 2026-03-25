namespace SharpConsoleUI.Video
{
    /// <summary>
    /// Determines how video frames are rendered to terminal cells.
    /// </summary>
    public enum VideoRenderMode
    {
        /// <summary>
        /// Half-block rendering using U+2580 (upper half block).
        /// Each cell encodes 2 vertical pixels: fg = top, bg = bottom.
        /// Best color fidelity, 2x vertical resolution.
        /// </summary>
        HalfBlock,

        /// <summary>
        /// ASCII density rendering. Maps pixel brightness to characters
        /// from the density ramp " .:-=+*#%@". Foreground colored.
        /// </summary>
        Ascii,

        /// <summary>
        /// Braille dot pattern rendering. Each cell covers a 2x4 pixel
        /// region using Unicode braille characters (U+2800-U+28FF).
        /// Highest spatial resolution.
        /// </summary>
        Braille
    }
}

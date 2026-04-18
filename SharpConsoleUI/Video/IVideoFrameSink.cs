// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Video
{
    /// <summary>
    /// Strategy for delivering decoded video frames to the terminal. Implementations
    /// provide different rendering backends — character-cell approximations
    /// (half-block, ASCII, braille) or the Kitty graphics protocol for pixel-accurate
    /// playback on supporting terminals.
    /// </summary>
    /// <remarks>
    /// Lifecycle:
    /// <para><see cref="IngestFrame"/> is called on the playback background thread whenever
    /// FFmpeg produces a new frame. It may do heavy work (cell conversion, terminal
    /// transmission). Implementations must be thread-safe against concurrent <see cref="Paint"/>
    /// calls from the render thread.</para>
    /// <para><see cref="Paint"/> is called on the render thread from inside PaintDOM. It should
    /// be lightweight — blit cells, write placeholder cells.</para>
    /// <para><see cref="OnStopped"/> is called when playback stops so the sink can release any
    /// terminal-side state (delete transmitted Kitty images).</para>
    /// </remarks>
    public interface IVideoFrameSink : IDisposable
    {
        /// <summary>
        /// Ingests a freshly decoded RGB24 frame. The caller must guarantee the byte array
        /// holds <paramref name="pixelWidth"/> * <paramref name="pixelHeight"/> * 3 bytes, row-major.
        /// Implementations must not retain the byte array beyond this call — copy out or consume
        /// synchronously, because the playback loop reuses the same buffer across frames.
        /// </summary>
        /// <param name="targetCols">Terminal columns the playback loop intends the frame to occupy.</param>
        /// <param name="targetRows">Terminal rows the playback loop intends the frame to occupy.</param>
        void IngestFrame(byte[] rgb24, int pixelWidth, int pixelHeight, int targetCols, int targetRows, Color windowBackground);

        /// <summary>
        /// Paints the most recently ingested frame into the character buffer, centered within
        /// <paramref name="contentRect"/>, respecting <paramref name="clipRect"/>. Also fills any
        /// letterbox/pillarbox gaps with the window background so stale cells underneath don't
        /// bleed through. Safe to call even if no frame has been ingested yet (fills with
        /// background in that case).
        /// </summary>
        void Paint(
            CharacterBuffer buffer,
            LayoutRect contentRect,
            LayoutRect clipRect,
            Color windowForeground,
            Color windowBackground);

        /// <summary>
        /// Called when playback stops or the sink is being replaced. Releases any terminal-side
        /// resources (e.g. deletes transmitted Kitty images). Safe to call multiple times.
        /// </summary>
        void OnStopped();

        /// <summary>
        /// Returns the preferred pixel dimensions for FFmpeg to decode into, given the target
        /// cell area. Cell-mode sinks typically want (cellCols, cellRows * N) where N is the
        /// vertical pixel density of the mode; Kitty mode wants much higher resolution.
        /// </summary>
        (int Width, int Height) GetPreferredPixelSize(int cellCols, int cellRows);

        /// <summary>
        /// Requests that the sink rebuild any terminal-side state on the next ingested frame.
        /// No-op for cell-mode sinks; for the Kitty sink this triggers a full image+placement
        /// recreation, which is the same reset that happens on window resize and is the one
        /// reliable way to recover from the (rare) stuck-black state where the terminal
        /// stops redrawing after in-place frame updates. Exposed so a user-facing keybind
        /// can force recovery without having to resize the window.
        /// </summary>
        void ForceRefresh() { /* default: no-op */ }
    }
}

using SharpConsoleUI.Video;

namespace SharpConsoleUI.Configuration
{
    /// <summary>
    /// Default constants for VideoControl. Separated from ControlDefaults
    /// to avoid a dependency from Configuration on the Video namespace.
    /// </summary>
    public static class VideoDefaults
    {
        /// <summary>
        /// Default render mode for VideoControl. Auto picks Kitty graphics when the
        /// terminal supports it, otherwise falls back to HalfBlock (identical to the
        /// previous default on non-Kitty terminals).
        /// </summary>
        public const VideoRenderMode DefaultRenderMode = VideoRenderMode.Auto;

        /// <summary>Target frames per second for video playback.</summary>
        public const int DefaultTargetFps = 30;

        /// <summary>Maximum frames to skip when playback falls behind.</summary>
        public const int MaxFrameSkip = 5;

        /// <summary>
        /// Number of frames behind before frame-skipping activates.
        /// </summary>
        public const int FrameSkipThreshold = 2;

        /// <summary>Seek jump in seconds for Left/Right arrow keys.</summary>
        public const double SeekStepSeconds = 5.0;

        /// <summary>FFmpeg process start timeout in milliseconds.</summary>
        public const int FfmpegStartTimeoutMs = 5000;

        /// <summary>FFmpeg read timeout per frame in milliseconds.</summary>
        public const int FfmpegReadTimeoutMs = 2000;

        /// <summary>Delay in milliseconds between pause-state polls.</summary>
        public const int PausePollDelayMs = 50;

        /// <summary>Minimum sleep threshold in milliseconds before Task.Delay is worthwhile.</summary>
        public const double MinSleepThresholdMs = 1.0;

        /// <summary>Half-block pixels per cell (vertical).</summary>
        public const int HalfBlockPixelsPerCell = 2;

        /// <summary>ASCII brightness-to-density character ramp (darkest to brightest).</summary>
        public const string AsciiDensityRamp = " .:-=+*#%@";

        /// <summary>Braille Unicode base codepoint (U+2800).</summary>
        public const int BrailleBaseCodepoint = 0x2800;

        /// <summary>Braille cell width in source pixels.</summary>
        public const int BrailleCellPixelWidth = 2;

        /// <summary>Braille cell height in source pixels.</summary>
        public const int BrailleCellPixelHeight = 4;

        /// <summary>Braille brightness threshold (0-255) for dot activation.</summary>
        public const int BrailleBrightnessThreshold = 64;

        /// <summary>Default fallback cell columns when control hasn't been laid out.</summary>
        public const int FallbackCellCols = 80;

        /// <summary>Default fallback cell rows when control hasn't been laid out.</summary>
        public const int FallbackCellRows = 24;

        /// <summary>Minimum FPS clamp.</summary>
        public const int MinFps = 1;

        /// <summary>Maximum FPS clamp.</summary>
        public const int MaxFps = 120;

        /// <summary>Overlay auto-hide timeout in milliseconds after last interaction.</summary>
        public const int OverlayAutoHideMs = 3000;

        /// <summary>Overlay height in cell rows.</summary>
        public const int OverlayHeight = 1;

        /// <summary>FFmpeg not found error message displayed inside the control.</summary>
        public const string FfmpegNotFoundMessage =
            "FFmpeg not found. Install it to play videos:\n" +
            "  Linux:   sudo apt install ffmpeg\n" +
            "  macOS:   brew install ffmpeg\n" +
            "  Windows: winget install ffmpeg";

        /// <summary>
        /// Approximate terminal pixel density per cell column for Kitty mode. Lower values
        /// reduce wire bandwidth at a modest quality cost (Kitty upscales the transmitted
        /// buffer into the placement rectangle). 4 gives a good balance: ~4x less data than
        /// a full native-density decode, virtually imperceptible quality loss for video.
        /// </summary>
        public const int KittyPixelsPerCellX = 4;

        /// <summary>Approximate terminal pixel density per cell row for Kitty mode. See <see cref="KittyPixelsPerCellX"/>.</summary>
        public const int KittyPixelsPerCellY = 8;

        /// <summary>Upper bound on the FFmpeg decode width when Kitty mode is active (protects against excessive bandwidth).</summary>
        public const int KittyMaxPixelWidth = 960;

        /// <summary>Upper bound on the FFmpeg decode height when Kitty mode is active.</summary>
        public const int KittyMaxPixelHeight = 540;
    }
}

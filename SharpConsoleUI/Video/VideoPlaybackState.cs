namespace SharpConsoleUI.Video
{
    /// <summary>
    /// Represents the current state of video playback.
    /// </summary>
    public enum VideoPlaybackState
    {
        /// <summary>No video loaded or playback stopped.</summary>
        Stopped,

        /// <summary>Video is actively playing frames.</summary>
        Playing,

        /// <summary>Video is paused at the current frame.</summary>
        Paused
    }
}

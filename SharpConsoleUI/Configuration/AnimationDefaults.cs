namespace SharpConsoleUI.Configuration;

/// <summary>
/// Centralized constants for animation timing and limits.
/// </summary>
public static class AnimationDefaults
{
	/// <summary>
	/// Default duration for fade animations in milliseconds.
	/// </summary>
	public const int DefaultFadeDurationMs = 300;

	/// <summary>
	/// Default duration for slide animations in milliseconds.
	/// </summary>
	public const int DefaultSlideDurationMs = 400;

	/// <summary>
	/// Maximum number of concurrent animations allowed.
	/// </summary>
	public const int MaxConcurrentAnimations = 50;

	/// <summary>
	/// Maximum delta time per animation frame in milliseconds.
	/// Prevents animations from completing instantly when the system was idle
	/// and the elapsed time since last render is very large.
	/// </summary>
	public const double MaxFrameDeltaMs = 33.0;

	/// <summary>
	/// Default easing function name (EaseInOut).
	/// The actual default is applied via EasingFunctions.EaseInOut.
	/// </summary>
	public const string DefaultEasingName = "EaseInOut";

	/// <summary>
	/// Default duration of a single modal flash pulse in milliseconds (fade up + fade down).
	/// </summary>
	public const int DefaultFlashPulseDurationMs = 300;

	/// <summary>
	/// Delay between consecutive flash pulses in milliseconds.
	/// </summary>
	public const int FlashInterPulseDelayMs = 150;

	/// <summary>
	/// Maximum overlay intensity for the modal flash effect (0.0 to 1.0).
	/// </summary>
	public const float FlashMaxIntensity = 0.3f;

	/// <summary>
	/// Intensity threshold below which the flash overlay is skipped for performance.
	/// </summary>
	public const float FlashIntensityEpsilon = 0.001f;

	/// <summary>
	/// Foreground blend factor relative to background intensity during flash overlay.
	/// </summary>
	public const float FlashForegroundBlendRatio = 0.5f;

	/// <summary>
	/// Foreground blend ratio for fade animations.
	/// Set to 1.0 so foreground fades at the same rate as background for a true fade effect.
	/// </summary>
	public const float FadeForegroundBlendRatio = 1.0f;
}

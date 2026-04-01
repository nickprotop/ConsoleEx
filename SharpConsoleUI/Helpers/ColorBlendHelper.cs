using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Helpers;

/// <summary>
/// Shared color blending utilities used by flash overlay, fade animations, and other effects.
/// Extracted to avoid code duplication between WindowStateService and WindowAnimations.
/// </summary>
public static class ColorBlendHelper
{
	/// <summary>
	/// Blends two colors together by the specified amount.
	/// </summary>
	/// <param name="original">The original color.</param>
	/// <param name="target">The target color to blend toward.</param>
	/// <param name="amount">The blend amount (0.0 = original, 1.0 = target).</param>
	public static Color BlendColor(Color original, Color target, float amount)
	{
		return new Color(
			(byte)(original.R + (target.R - original.R) * amount),
			(byte)(original.G + (target.G - original.G) * amount),
			(byte)(original.B + (target.B - original.B) * amount));
	}

	/// <summary>
	/// Applies a color overlay to the entire buffer, blending each cell's foreground and background
	/// toward the overlay color at the given intensity.
	/// Used by flash effects, fade animations, and similar overlay-based transitions.
	/// </summary>
	/// <param name="buffer">The character buffer to modify.</param>
	/// <param name="overlayColor">The color to blend toward.</param>
	/// <param name="intensity">The background blend intensity (0.0 to 1.0).</param>
	/// <param name="foregroundBlendRatio">
	/// Ratio applied to intensity for foreground blending.
	/// Defaults to AnimationDefaults.FlashForegroundBlendRatio.
	/// </param>
	public static void ApplyColorOverlay(
		CharacterBuffer buffer,
		Color overlayColor,
		float intensity,
		float foregroundBlendRatio = AnimationDefaults.FlashForegroundBlendRatio)
	{
		if (intensity <= AnimationDefaults.FlashIntensityEpsilon) return;

		for (int y = 0; y < buffer.Height; y++)
		{
			for (int x = 0; x < buffer.Width; x++)
			{
				var cell = buffer.GetCell(x, y);

				var newBg = BlendColor(cell.Background, overlayColor, intensity);
				var newFg = BlendColor(cell.Foreground, overlayColor, intensity * foregroundBlendRatio);

				// Use color-only update to avoid CleanupWideCharAt destroying
				// wide character pairs (emoji, CJK) during overlay passes
				buffer.SetCellColors(x, y, newFg, newBg);
			}
		}
	}
}

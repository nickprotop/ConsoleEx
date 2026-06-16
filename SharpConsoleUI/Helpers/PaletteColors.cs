// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Color derivation helpers for palette-based theme generation: tint (toward white), shade (toward
	/// black), mix (toward another color), relative luminance, and a readable contrast color. All are
	/// reflection-free and built on <see cref="ColorBlendHelper.BlendColor"/>.
	/// </summary>
	public static class PaletteColors
	{
		/// <summary>Lightens <paramref name="color"/> by blending it toward white. amount 0..1.</summary>
		public static Color Tint(this Color color, double amount)
			=> ColorBlendHelper.BlendColor(color, Color.White, (float)System.Math.Clamp(amount, 0.0, 1.0));

		/// <summary>Darkens <paramref name="color"/> by blending it toward black. amount 0..1.</summary>
		public static Color Shade(this Color color, double amount)
			=> ColorBlendHelper.BlendColor(color, Color.Black, (float)System.Math.Clamp(amount, 0.0, 1.0));

		/// <summary>Blends <paramref name="color"/> toward <paramref name="other"/>. amount 0..1.</summary>
		public static Color Mix(this Color color, Color other, double amount)
			=> ColorBlendHelper.BlendColor(color, other, (float)System.Math.Clamp(amount, 0.0, 1.0));

		/// <summary>Relative luminance (0..255, Rec.709 weights).</summary>
		public static double Luminance(this Color color)
			=> 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;

		/// <summary>Whether the color reads as dark (luminance below mid-grey).</summary>
		public static bool IsDark(this Color color) => color.Luminance() < 128.0;

		/// <summary>A readable foreground for the given background: near-white on dark, near-black on light.</summary>
		public static Color ContrastOn(Color background)
			=> background.IsDark() ? new Color(240, 240, 240) : new Color(20, 20, 20);

		/// <summary>
		/// A readable foreground for <paramref name="background"/> that stays palette-dependent: starts
		/// from <see cref="ContrastOn"/> (near-white/near-black) then blends a small amount of the
		/// background back in so the text picks up the surface's hue. High contrast is preserved while the
		/// result genuinely tracks the palette rather than snapping to one of two fixed constants.
		/// </summary>
		public static Color ReadableOn(Color background)
			=> ContrastOn(background).Mix(background, 0.12);

		/// <summary>
		/// Returns <paramref name="color"/> unchanged if it already has at least <paramref name="minGap"/>
		/// luminance separation from <paramref name="background"/>; otherwise nudges it away from the
		/// background (lighter on a dark bg, darker on a light bg) until it reaches the gap, preserving its
		/// hue as much as possible. Guarantees the color stays visible against the background.
		/// </summary>
		/// <param name="color">The color to keep visible.</param>
		/// <param name="background">The surface it sits on.</param>
		/// <param name="minGap">Minimum luminance gap (0..255). 80 is a reasonable readable minimum.</param>
		public static Color EnsureContrast(Color color, Color background, double minGap = 80.0)
		{
			double gap = System.Math.Abs(color.Luminance() - background.Luminance());
			if (gap >= minGap)
				return color;

			// Move the color away from the background. On a dark bg, lighten (tint toward white); on a
			// light bg, darken (shade toward black). Step up the amount until the gap is met or we hit
			// the extreme. This preserves hue better than snapping straight to white/black.
			bool bgDark = background.IsDark();
			for (double amt = 0.15; amt <= 1.0; amt += 0.15)
			{
				Color candidate = bgDark ? color.Tint(amt) : color.Shade(amt);
				if (System.Math.Abs(candidate.Luminance() - background.Luminance()) >= minGap)
					return candidate;
			}
			// Fallback: maximum contrast.
			return ContrastOn(background);
		}

		/// <summary>
		/// The opaque surface a control actually composites onto: its own background if opaque, otherwise
		/// the supplied window/parent background (transparent or default backgrounds show that through).
		/// </summary>
		public static Color EffectiveSurface(Color? controlBackground, Color windowBackground)
			=> (controlBackground is { } bg && bg.A == 255 && !bg.IsDefault) ? bg : windowBackground;
	}
}

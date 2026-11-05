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

		/// <summary>
		/// A readable foreground for <paramref name="surface"/>, chosen by measured WCAG contrast
		/// rather than by a light/dark guess, and kept palette-tinted where that costs nothing.
		/// </summary>
		/// <remarks>
		/// <see cref="ReadableOn"/> picks near-white or near-black from <see cref="IsDark"/>, a raw
		/// luminance midpoint test. On a MID-TONE fill, which is what a themed button or a saturated
		/// accent usually is, that test is close to a coin flip and frequently picks the side with
		/// less real contrast: a #EF4444 Crimson button took white text at 3.30:1. This measures both
		/// candidates and takes the better one, then blends a little surface back in for hue, keeping
		/// the blend only while it still clears <paramref name="minRatio"/>.
		/// </remarks>
		/// <param name="surface">The surface the text sits on.</param>
		/// <param name="minRatio">The contrast floor to hold. Defaults to the text floor.</param>
		public static Color ReadableOnMeasured(Color surface, double minRatio = TextContrast)
		{
			Color light = new(240, 240, 240);
			Color dark = new(20, 20, 20);
			Color best = ContrastRatio(light, surface) >= ContrastRatio(dark, surface) ? light : dark;

			// Tint toward the surface so the text carries the palette's hue, but only while that
			// still clears the floor; on a mid-tone surface there is no headroom and the flat
			// near-white/near-black is what keeps it readable.
			Color tinted = best.Mix(surface, 0.12);
			if (ContrastRatio(tinted, surface) >= minRatio)
				return tinted;

			// Neither the blend nor the plain extreme may reach the floor against a mid-tone surface.
			// Push the better extreme further away rather than returning something unreadable.
			return EnsureContrastRatio(best, surface, minRatio);
		}

		/// <summary>WCAG AA contrast floor for normal-size text. A terminal cell is normal-size text.</summary>
		public const double TextContrast = 4.5;

		/// <summary>WCAG AA contrast floor for user interface components and graphical objects.</summary>
		public const double GraphicContrast = 3.0;

		/// <summary>One sRGB channel (0..255), linearized per the WCAG relative-luminance definition.</summary>
		private static double Linearize(double channel)
		{
			double c = channel / 255.0;
			return c <= 0.03928 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
		}

		/// <summary>
		/// Relative luminance in 0..1, as WCAG defines it: sRGB channels linearized before weighting.
		/// </summary>
		/// <remarks>
		/// Distinct from <see cref="Luminance"/>, which is a weighted sum of the RAW channels on a
		/// 0..255 scale. sRGB is gamma-encoded, so a fixed raw-channel gap buys far less perceived
		/// contrast down in the dark end of the range than up in the light end. Use this one whenever
		/// the question is "can a person actually see this against that".
		/// </remarks>
		public static double RelativeLuminance(this Color color)
			=> (0.2126 * Linearize(color.R))
				+ (0.7152 * Linearize(color.G))
				+ (0.0722 * Linearize(color.B));

		/// <summary>The WCAG contrast ratio between two opaque colors, from 1.0 (identical) to 21.0.</summary>
		public static double ContrastRatio(Color a, Color b)
		{
			double la = a.RelativeLuminance();
			double lb = b.RelativeLuminance();
			return (System.Math.Max(la, lb) + 0.05) / (System.Math.Min(la, lb) + 0.05);
		}

		/// <summary>
		/// Returns <paramref name="color"/> unchanged when it already meets <paramref name="minRatio"/>
		/// against <paramref name="surface"/>; otherwise moves it away from the surface, preserving hue
		/// as far as possible, until it does.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The WCAG counterpart to <see cref="EnsureContrast"/>, which measures a raw-channel gap and so
		/// reports colors as separated that a person cannot tell apart. Prefer this one for anything a
		/// user has to actually read or pick out.
		/// </para>
		/// <para>
		/// The search direction is chosen by measuring, not by assuming the surface is dark: on a
		/// mid-tone surface, lightening and darkening do not buy the same contrast, and on a very light
		/// or very dark surface only one direction has any room left at all. Whichever direction can
		/// reach the target first wins; if neither does, the better of the two extremes is returned, so
		/// the result is always at least as readable as the input.
		/// </para>
		/// </remarks>
		/// <param name="color">The color to keep visible.</param>
		/// <param name="surface">The opaque surface it composites onto.</param>
		/// <param name="minRatio">The contrast ratio to reach. Defaults to the graphical-object floor.</param>
		public static Color EnsureContrastRatio(Color color, Color surface, double minRatio = GraphicContrast)
		{
			if (ContrastRatio(color, surface) >= minRatio)
				return color;

			// Try both directions in step, and take the first that clears the bar. Stepping together
			// rather than committing to a direction up front matters on mid-tone surfaces, where the
			// nearer extreme is not always the reachable one.
			Color lightBest = color;
			Color darkBest = color;

			for (double amount = 0.1; amount <= 1.0; amount += 0.1)
			{
				lightBest = color.Tint(amount);
				if (ContrastRatio(lightBest, surface) >= minRatio)
					return lightBest;

				darkBest = color.Shade(amount);
				if (ContrastRatio(darkBest, surface) >= minRatio)
					return darkBest;
			}

			// Neither direction reaches the target (a mid-tone surface has limited headroom both ways).
			// Return whichever extreme is furthest from the surface rather than giving up on the input.
			return ContrastRatio(lightBest, surface) >= ContrastRatio(darkBest, surface) ? lightBest : darkBest;
		}
	}
}

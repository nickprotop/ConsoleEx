// -----------------------------------------------------------------------
// Breakout - the particle palettes.
//
// Art direction, not mechanism: the particle pool itself lives in
// Games.Common.Effects, which every example game shares. Colours are what
// makes each game look like itself, so they stay here.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Helpers;

namespace Breakout;

/// <summary>Gradients particles are shaded along as they age.</summary>
internal static class Palettes
{
	public static readonly ColorGradient Spark = ColorGradient.FromColors(
		new Color(255, 255, 220), new Color(255, 180, 70), new Color(140, 40, 20));

	public static readonly ColorGradient Ice = ColorGradient.FromColors(
		new Color(240, 250, 255), new Color(130, 200, 255), new Color(40, 70, 150));

	/// <summary>
	/// A gradient built from one colour, so debris from a broken brick matches
	/// the brick: a bright lead-in, the colour itself, then a dark tail.
	/// </summary>
	public static ColorGradient FromColor(Color c) => ColorGradient.FromColors(
		new Color(
			(byte)Math.Min(255, c.R + 60),
			(byte)Math.Min(255, c.G + 60),
			(byte)Math.Min(255, c.B + 60)),
		c,
		new Color((byte)(c.R / 3), (byte)(c.G / 3), (byte)(c.B / 3)));
}

// -----------------------------------------------------------------------
// SpaceInvaders - the particle palettes.
//
// Art direction, not mechanism: the particle pool itself lives in
// Games.Common.Effects, which every example game shares. Colours are what
// makes each game look like itself, so they stay here.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Helpers;

namespace SpaceInvaders;

/// <summary>Gradients particles are shaded along as they age.</summary>
internal static class Palettes
{
	public static readonly ColorGradient Fire = ColorGradient.FromColors(
		new Color(255, 255, 230), new Color(255, 210, 90), new Color(255, 110, 30), new Color(150, 30, 20), new Color(40, 10, 10));

	public static readonly ColorGradient Plasma = ColorGradient.FromColors(
		new Color(240, 255, 255), new Color(90, 230, 255), new Color(40, 90, 220), new Color(20, 20, 70));

	/// <summary>Cyclic rainbow (first and last stops match) for colour cycling.</summary>
	public static readonly ColorGradient Rainbow = ColorGradient.FromColors(
		new Color(255, 60, 60), new Color(255, 170, 40), new Color(255, 240, 70), new Color(70, 255, 120),
		new Color(60, 220, 255), new Color(110, 90, 255), new Color(255, 70, 220), new Color(255, 60, 60));

	public static readonly ColorGradient Toxic = ColorGradient.FromColors(
		new Color(230, 255, 210), new Color(120, 255, 90), new Color(30, 140, 50), new Color(10, 40, 20));
}

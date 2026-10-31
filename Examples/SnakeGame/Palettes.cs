// -----------------------------------------------------------------------
// SnakeGame - the particle palettes.
//
// Art direction, not mechanism: the particle pool itself lives in
// Games.Common.Effects, which every example game shares. Colours are what
// makes each game look like itself, so they stay here.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Helpers;

namespace SnakeGame;

/// <summary>Gradients particles are shaded along as they age.</summary>
internal static class Palettes
{
	/// <summary>Fruit juice: warm and sweet.</summary>
	public static readonly ColorGradient Juice = ColorGradient.FromColors(
		new Color(255, 250, 220), new Color(255, 190, 70), new Color(240, 90, 60), new Color(90, 20, 30));

	/// <summary>The snake's own colours, shed when it dies.</summary>
	public static readonly ColorGradient Scale = ColorGradient.FromColors(
		new Color(230, 255, 230), new Color(90, 230, 130), new Color(30, 140, 90), new Color(10, 40, 30));

	/// <summary>Cold and electric, for the bonus fruit.</summary>
	public static readonly ColorGradient Spark = ColorGradient.FromColors(
		new Color(240, 255, 255), new Color(120, 230, 255), new Color(60, 110, 230), new Color(20, 20, 70));

	/// <summary>Cyclic rainbow (first and last stops match) for colour cycling.</summary>
	public static readonly ColorGradient Rainbow = ColorGradient.FromColors(
		new Color(255, 60, 60), new Color(255, 170, 40), new Color(255, 240, 70), new Color(70, 255, 120),
		new Color(60, 220, 255), new Color(110, 90, 255), new Color(255, 70, 220), new Color(255, 60, 60));
}

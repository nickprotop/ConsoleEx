// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Rendering
{
	/// <summary>
	/// Describes a gradient background for a window, combining a color gradient with a direction.
	/// </summary>
	/// <param name="Gradient">The color gradient to apply.</param>
	/// <param name="Direction">The direction of the gradient.</param>
	public sealed record GradientBackground(ColorGradient Gradient, GradientDirection Direction);
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Interface for controls that support DOM-based painting.
	/// Controls implementing this interface can paint directly to a CharacterBuffer.
	/// All controls must implement this interface for the DOM layout system.
	/// </summary>
	public interface IDOMPaintable
	{
		/// <summary>
		/// Measures the control's desired size given the available constraints.
		/// </summary>
		/// <param name="constraints">The layout constraints (min/max width/height).</param>
		/// <returns>The desired size of the control.</returns>
		LayoutSize MeasureDOM(LayoutConstraints constraints);

		/// <summary>
		/// Paints the control's content directly to a CharacterBuffer.
		/// </summary>
		/// <param name="buffer">The buffer to paint to.</param>
		/// <param name="bounds">The absolute bounds where the control should paint.</param>
		/// <param name="clipRect">The clipping rectangle (visible area).</param>
		/// <param name="defaultForeground">The default foreground color from the container.</param>
		/// <param name="defaultBackground">The default background color from the container.</param>
		void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultForeground, Color defaultBackground);
	}

	/// <summary>
	/// Interface for controls that support native DOM-based measurement only.
	/// Controls implementing this interface provide optimized size calculation.
	/// </summary>
	public interface IDOMMeasurable
	{
		/// <summary>
		/// Measures the control's desired size given the available constraints.
		/// </summary>
		/// <param name="constraints">The layout constraints (min/max width/height).</param>
		/// <returns>The desired size of the control.</returns>
		LayoutSize MeasureDOM(LayoutConstraints constraints);
	}
}

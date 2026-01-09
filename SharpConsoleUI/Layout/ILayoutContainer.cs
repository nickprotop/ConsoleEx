// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Interface for layout algorithms that determine how children are measured and arranged.
	/// Different implementations provide different layout strategies (vertical stack, horizontal, grid, etc.)
	/// </summary>
	public interface ILayoutContainer
	{
		/// <summary>
		/// Measures all children within the container and returns the desired size.
		/// Called during the measure pass (bottom-up).
		/// </summary>
		/// <param name="node">The container node being measured</param>
		/// <param name="constraints">The constraints from the parent</param>
		/// <returns>The desired size for this container based on its children</returns>
		LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints);

		/// <summary>
		/// Arranges all children within the container's final bounds.
		/// Called during the arrange pass (top-down).
		/// </summary>
		/// <param name="node">The container node being arranged</param>
		/// <param name="finalRect">The final bounds allocated to this container</param>
		void ArrangeChildren(LayoutNode node, LayoutRect finalRect);
	}

	/// <summary>
	/// Vertical alignment options for controls within their container.
	/// </summary>
	public enum VerticalAlignment
	{
		/// <summary>
		/// Align to the top of the available space.
		/// </summary>
		Top,

		/// <summary>
		/// Center vertically in the available space.
		/// </summary>
		Center,

		/// <summary>
		/// Align to the bottom of the available space.
		/// </summary>
		Bottom,

		/// <summary>
		/// Fill the available vertical space.
		/// Controls with this alignment share remaining space proportionally.
		/// </summary>
		Fill
	}

	/// <summary>
	/// Horizontal alignment options for controls within their container.
	/// </summary>
	public enum HorizontalAlignment
	{
		/// <summary>
		/// Align to the left of the available space.
		/// </summary>
		Left,

		/// <summary>
		/// Center horizontally in the available space.
		/// </summary>
		Center,

		/// <summary>
		/// Align to the right of the available space.
		/// </summary>
		Right,

		/// <summary>
		/// Stretch to fill the available horizontal space.
		/// </summary>
		Stretch
	}
}

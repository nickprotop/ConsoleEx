// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Represents a container that can host window controls and provides shared properties for rendering.
	/// </summary>
	public interface IContainer
	{
		/// <summary>Gets or sets the background color for the container and its child controls.</summary>
		Color BackgroundColor { get; set; }

		/// <summary>Gets or sets the foreground (text) color for the container and its child controls.</summary>
		Color ForegroundColor { get; set; }

		/// <summary>Gets the console window system instance, or null if not attached to a window system.</summary>
		ConsoleWindowSystem? GetConsoleWindowSystem { get; }

		/// <summary>Gets or sets whether this container needs to be redrawn.</summary>
		bool IsDirty { get; set; }

		/// <summary>
		/// Marks this container as needing to be redrawn.
		/// </summary>
		/// <param name="redrawAll">If true, forces a complete redraw of all content.</param>
		/// <param name="callerControl">The control that triggered the invalidation, if any.</param>
		void Invalidate(bool redrawAll, IWindowControl? callerControl = null);

		/// <summary>
		/// Gets the actual visible height for a control within the container viewport.
		/// Returns null if the control is not found or visibility cannot be determined.
		/// </summary>
		/// <param name="control">The control to check</param>
		/// <returns>The number of visible lines, or null if unknown</returns>
		int? GetVisibleHeightForControl(IWindowControl control);
	}
}
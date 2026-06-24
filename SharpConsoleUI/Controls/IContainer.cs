// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------


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

		/// <summary>
		/// Marks this container as needing the specified work on the next frame. The request propagates up the
		/// container chain and folds into the owning window's frame-intent accumulator.
		/// </summary>
		/// <param name="work">The kind of work requested: <see cref="Invalidation.Repaint"/> (appearance-only,
		/// Measure skipped) or <see cref="Invalidation.Relayout"/> (full layout).</param>
		/// <param name="callerControl">The control that triggered the invalidation, if any (cycle guard).</param>
		void Invalidate(Invalidation work, IWindowControl? callerControl = null);

		/// <summary>
		/// Marks this container as needing work on the next frame. Compatibility overload preserving the
		/// previous boolean signature: <c>true</c> maps to <see cref="Invalidation.Relayout"/>,
		/// <c>false</c> to <see cref="Invalidation.Repaint"/>.
		/// </summary>
		/// <param name="redrawAll"><c>true</c> for a full re-layout, <c>false</c> for an appearance-only repaint.</param>
		/// <param name="callerControl">The control that triggered the invalidation, if any (cycle guard).</param>
		void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
			=> Invalidate(redrawAll ? Invalidation.Relayout : Invalidation.Repaint, callerControl);

		/// <summary>
		/// Gets the actual visible height for a control within the container viewport.
		/// Returns null if the control is not found or visibility cannot be determined.
		/// </summary>
		/// <param name="control">The control to check</param>
		/// <returns>The number of visible lines, or null if unknown</returns>
		int? GetVisibleHeightForControl(IWindowControl control);

	}
}

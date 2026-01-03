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
	public interface IContainer
	{
		public Color BackgroundColor { get; set; }
		public Color ForegroundColor { get; set; }

		public ConsoleWindowSystem? GetConsoleWindowSystem { get; }

		public bool IsDirty { get; set; }

		public void Invalidate(bool redrawAll, IWindowControl? callerControl = null);

		/// <summary>
		/// Gets the actual visible height for a control within the container viewport.
		/// Returns null if the control is not found or visibility cannot be determined.
		/// </summary>
		/// <param name="control">The control to check</param>
		/// <returns>The number of visible lines, or null if unknown</returns>
		int? GetVisibleHeightForControl(IWindowControl control);
	}
}
// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Events
{
	/// <summary>
	/// Event arguments for canvas mouse events with canvas-local coordinates.
	/// </summary>
	public sealed class CanvasMouseEventArgs : EventArgs
	{
		/// <summary>
		/// The X coordinate of the mouse event in canvas-local space (0-based).
		/// </summary>
		public int CanvasX { get; }

		/// <summary>
		/// The Y coordinate of the mouse event in canvas-local space (0-based).
		/// </summary>
		public int CanvasY { get; }

		/// <summary>
		/// The original mouse event args for access to flags, absolute position, etc.
		/// </summary>
		public MouseEventArgs OriginalArgs { get; }

		internal CanvasMouseEventArgs(int canvasX, int canvasY, MouseEventArgs originalArgs)
		{
			CanvasX = canvasX;
			CanvasY = canvasY;
			OriginalArgs = originalArgs;
		}
	}
}

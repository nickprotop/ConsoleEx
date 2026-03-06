// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;

namespace SharpConsoleUI.Events
{
	/// <summary>
	/// Event arguments for the <see cref="Controls.CanvasControl.Paint"/> event,
	/// carrying a <see cref="CanvasGraphics"/> context that draws to the window buffer.
	/// </summary>
	public sealed class CanvasPaintEventArgs : EventArgs
	{
		/// <summary>
		/// The drawing context for rendering onto the canvas during the paint event.
		/// </summary>
		public CanvasGraphics Graphics { get; }

		/// <summary>
		/// The current width of the canvas in characters.
		/// </summary>
		public int CanvasWidth { get; }

		/// <summary>
		/// The current height of the canvas in characters.
		/// </summary>
		public int CanvasHeight { get; }

		internal CanvasPaintEventArgs(CanvasGraphics graphics, int canvasWidth, int canvasHeight)
		{
			Graphics = graphics;
			CanvasWidth = canvasWidth;
			CanvasHeight = canvasHeight;
		}
	}
}

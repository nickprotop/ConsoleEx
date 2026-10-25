// -----------------------------------------------------------------------
// SnakeGame — the play-field control.
//
// A CanvasControl subclass that adds one thing the stock control does not
// surface: the mouse button PRESS. CanvasMouseClick is synthesized on
// release, which is late when the press is what starts a game, so this
// re-implements IMouseAwareControl.ProcessMouseEvent, catches
// Button1Pressed, and forwards everything else to the base implementation
// unchanged.
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;

namespace SnakeGame;

internal sealed class GameCanvas : CanvasControl, IMouseAwareControl
{
	/// <summary>Raised on the input thread when the left button goes down, with the canvas cell.</summary>
	public event Action<int, int>? PrimaryPressed;

	bool IMouseAwareControl.ProcessMouseEvent(MouseEventArgs args)
	{
		if (IsEnabled && args.HasFlag(MouseFlags.Button1Pressed))
		{
			int canvasX = args.Position.X - Margin.Left;
			int canvasY = args.Position.Y - Margin.Top;
			if (canvasX >= 0 && canvasX < CanvasWidth && canvasY >= 0 && canvasY < CanvasHeight)
				PrimaryPressed?.Invoke(canvasX, canvasY);
			args.Handled = true;
			return true;
		}

		return ProcessMouseEvent(args);
	}
}

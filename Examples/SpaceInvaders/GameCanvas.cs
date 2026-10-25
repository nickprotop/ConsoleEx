// -----------------------------------------------------------------------
// SpaceInvaders — the play-field control.
//
// A CanvasControl subclass that adds one thing the stock control does not
// surface: the mouse button PRESS. CanvasMouseClick is synthesized on
// release, which feels late for a fire button, so this re-implements
// IMouseAwareControl.ProcessMouseEvent, catches Button1Pressed, and forwards
// everything else to the base implementation unchanged.
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;

namespace SpaceInvaders;

internal sealed class GameCanvas : CanvasControl, IMouseAwareControl
{
	/// <summary>Raised on the input thread when the left button goes down, with the canvas column.</summary>
	public event Action<int>? PrimaryPressed;

	bool IMouseAwareControl.ProcessMouseEvent(MouseEventArgs args)
	{
		if (IsEnabled && args.HasFlag(MouseFlags.Button1Pressed))
		{
			int canvasX = args.Position.X - Margin.Left;
			if (canvasX >= 0 && canvasX < CanvasWidth)
				PrimaryPressed?.Invoke(canvasX);
			args.Handled = true;
			return true;
		}

		return ProcessMouseEvent(args);
	}
}

// -----------------------------------------------------------------------
// Breakout — input layer.
//
// Terminals report key PRESSES only; there is no release event. Space
// Invaders solves that with three keyboard modes and a model that learns
// the OS auto-repeat rate, because a ship that stops dead mid-dodge is
// fatal there.
//
// Breakout does not need any of it. A left or right key sets a direction and
// the paddle keeps going while that key's auto-repeat keeps arriving; once the
// presses stop for longer than a repeat interval the key is treated as let go
// and the paddle halts. So a hold slides smoothly and a tap nudges once, with
// no release event and no learned repeat rate - just a short grace period.
//
// Mouse events arrive on the driver's input thread while keys arrive on the
// UI thread, so MouseInput is the only lock-guarded state here.
// -----------------------------------------------------------------------

namespace Breakout;

/// <summary>Pointer position and clicks, recorded off-thread and read once per frame.</summary>
internal sealed class MouseInput
{
	private readonly object _lock = new();
	private int _pixelX = -1;
	private bool _moved;
	private int _pendingClicks;

	public void Moved(int pixelX)
	{
		lock (_lock)
		{
			_pixelX = pixelX;
			_moved = true;
		}
	}

	public void Pressed(int pixelX)
	{
		lock (_lock)
		{
			_pixelX = pixelX;
			_moved = true;
			_pendingClicks++;
		}
	}

	/// <summary>Takes the pointer column, whether it moved, and any clicks since the last call.</summary>
	public (int PixelX, bool Moved, int Clicks) Consume()
	{
		lock (_lock)
		{
			var result = (_pixelX, _moved, _pendingClicks);
			_moved = false;
			_pendingClicks = 0;
			return result;
		}
	}
}

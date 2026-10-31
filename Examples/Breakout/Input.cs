// -----------------------------------------------------------------------
// Breakout — input layer.
//
// Terminals report key PRESSES only; there is no release event. Space
// Invaders solves that with three keyboard modes and a model that learns
// the OS auto-repeat rate, because a ship that stops dead mid-dodge is
// fatal there.
//
// Breakout does not need any of it. The paddle latches: a left or right key
// sets a direction and it keeps going until the opposite key, a stop key, or
// the mouse takes over. One press is enough to express the whole intent, so
// the auto-repeat pause nobody can avoid is simply never in the way.
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

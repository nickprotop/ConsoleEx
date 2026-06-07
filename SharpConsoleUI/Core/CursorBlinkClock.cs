// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Rate-driven on/off state for the opt-in software cursor. The loop feeds elapsed time via
	/// Advance(); IsOn(rate) returns whether the caret should currently be drawn. Reset() forces a
	/// solid "on" phase (called when the caret moves, so it is solid right after typing).
	/// </summary>
	public sealed class CursorBlinkClock
	{
		private double _accumulatedMs;

		/// <summary>Advances the clock by the elapsed milliseconds of a loop iteration.</summary>
		public void Advance(double elapsedMs)
		{
			if (elapsedMs > 0) _accumulatedMs += elapsedMs;
		}

		/// <summary>Forces a fresh solid-on phase.</summary>
		public void Reset() => _accumulatedMs = 0;

		/// <summary>True when the caret should be drawn for the given blink rate (ms per half-cycle).</summary>
		public bool IsOn(int rateMs)
		{
			if (rateMs <= 0) return true;
			long cycle = (long)(_accumulatedMs / rateMs);
			return (cycle % 2) == 0;
		}
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// The phase of a captured mouse gesture as routed by <see cref="MouseGestureCapture{TRegion}"/>.
	/// </summary>
	public enum GesturePhase
	{
		/// <summary>Fresh press that captured a region.</summary>
		Down,

		/// <summary>Press/drag while a region is captured (resent press mid-drag or an explicit drag).</summary>
		Move,

		/// <summary>Release/click that ends the gesture.</summary>
		Up,

		/// <summary>No active gesture and no fresh press (a stray drag/release).</summary>
		None
	}

	/// <summary>
	/// The routing result of a mouse event through <see cref="MouseGestureCapture{TRegion}"/>: the gesture
	/// phase and the region the event is routed to.
	/// </summary>
	/// <typeparam name="TRegion">The control's sub-region enum.</typeparam>
	/// <param name="Phase">The gesture phase.</param>
	/// <param name="Region">The region the event is routed to (the captured region during a gesture).</param>
	public readonly record struct GestureRoute<TRegion>(GesturePhase Phase, TRegion Region)
		where TRegion : struct, System.Enum;

	/// <summary>
	/// Sub-region gesture ownership for a control's mouse handling. On a fresh Button1 press (nothing
	/// captured) it hit-tests the region and captures it; every subsequent Button1 press/drag routes to the
	/// captured region without re-hit-testing (SGR re-sends Button1Pressed on motion, so a resent press mid-
	/// drag must NOT be treated as a fresh interaction with whatever region the cursor is now over); release
	/// ends the gesture. This is the same capture idea WindowEventDispatcher applies at the control level,
	/// applied at the sub-region level.
	/// </summary>
	/// <typeparam name="TRegion">The control's sub-region enum.</typeparam>
	public sealed class MouseGestureCapture<TRegion> where TRegion : struct, System.Enum
	{
		private TRegion? _captured;

		/// <summary>
		/// Whether a gesture is currently captured.
		/// </summary>
		public bool IsCapturing => _captured.HasValue;

		/// <summary>
		/// The region currently captured, or <c>null</c> if no gesture is active.
		/// </summary>
		public TRegion? CapturedRegion => _captured;

		/// <summary>
		/// Routes a mouse event to the correct region for the current gesture state.
		/// </summary>
		/// <param name="args">The mouse event arguments.</param>
		/// <param name="hitTest">
		/// Resolves the region under the cursor. Called only on a fresh press (when nothing is captured); never
		/// re-invoked mid-gesture.
		/// </param>
		/// <returns>The gesture phase and the region the event routes to.</returns>
		public GestureRoute<TRegion> Route(MouseEventArgs args, System.Func<MouseEventArgs, TRegion> hitTest)
		{
			if (_captured.HasValue)
			{
				if (args.HasAnyFlag(MouseFlags.Button1Released, MouseFlags.Button1Clicked))
				{
					var ended = _captured.Value;
					_captured = null;
					return new GestureRoute<TRegion>(GesturePhase.Up, ended);
				}

				return new GestureRoute<TRegion>(GesturePhase.Move, _captured.Value);
			}

			if (args.HasFlag(MouseFlags.Button1Pressed))
			{
				var region = hitTest(args);
				_captured = region;
				return new GestureRoute<TRegion>(GesturePhase.Down, region);
			}

			return new GestureRoute<TRegion>(GesturePhase.None, default);
		}

		/// <summary>
		/// Clears any active capture, abandoning the current gesture.
		/// </summary>
		public void Reset() => _captured = null;
	}
}

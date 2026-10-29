// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Input;

/// <summary>
/// Derives click, double-click and triple-click from a stream of presses and releases.
/// </summary>
/// <remarks>
/// <para>
/// The drivers report what physically happened - a button went down, a button came
/// up, the pointer moved. Everything above that is a gesture, and a gesture is a
/// judgement about timing and position that belongs in one place rather than being
/// reimplemented per platform.
/// </para>
/// <para>
/// The rule that matters: derived flags are ADDED, never substituted. A second press
/// inside the double-click window emits <c>Button1Pressed | Button1DoubleClicked</c>,
/// not <c>Button1DoubleClicked</c> alone. A control that wants every press can have
/// every press; a control that wants word-select on a double-click still gets it;
/// neither has to know about the other. Before this, the press was replaced, so a
/// fast run of clicks reached a control as press, double, triple, press - and
/// anything watching for a plain press silently lost half of them.
/// </para>
/// <para>
/// The clock is injectable so the whole thing is testable without sleeping. The old
/// per-driver machines used background <c>Task.Delay</c> timers to expire their
/// state, which made their behaviour untestable except by waiting.
/// </para>
/// <para>
/// Locked, because it is NOT driven from a single thread. The terminal's own
/// reports arrive on the input thread, and the auto-repeat for a held button
/// arrives on a pool thread - both reaching the same run state. The lock is
/// held for the length of one recognition, which is a handful of comparisons.
/// </para>
/// </remarks>
internal sealed class MouseGestureRecognizer
{
	/// <summary>A release this far from the press is a drag, not a click.</summary>
	/// <remarks>Zero: terminals report cell coordinates, so a click does not drift.</remarks>
	private const int ClickSlopCells = 0;

	private readonly Func<DateTime> _now;
	private readonly TimeSpan _multiClickWindow;

	private MouseFlags _pressedButton;
	private Point _pressPosition;
	private Point _lastClickPosition;
	private DateTime _lastClickTime = DateTime.MinValue;

	/// <summary>How many clicks deep the current run is: 1 single, 2 double, 3 triple.</summary>
	private int _clickRun;

	/// <summary>Which button the run belongs to. A run is per-button.</summary>
	private MouseFlags _clickRunButton;

	/// <summary>
	/// Creates a recogniser.
	/// </summary>
	/// <param name="multiClickWindow">
	/// How long after a click a further press still counts as part of the same run.
	/// </param>
	/// <param name="now">The clock, for tests. Defaults to <see cref="DateTime.UtcNow"/>.</param>
	/// <param name="time">
	/// The clock, for tests. A <see cref="TimeProvider"/> rather than the older
	/// <c>Func&lt;DateTime&gt;</c> because that is what .NET 8 standardised and what
	/// <c>FakeTimeProvider</c> implements, so a test can move time instead of
	/// sleeping through the multi-click window. Wins over <paramref name="now"/>
	/// when both are given; defaults to <see cref="TimeProvider.System"/>.
	/// </param>
	public MouseGestureRecognizer(
		TimeSpan? multiClickWindow = null,
		Func<DateTime>? now = null,
		TimeProvider? time = null)
	{
		_multiClickWindow = multiClickWindow
			?? TimeSpan.FromMilliseconds(Configuration.ControlDefaults.DefaultDebounceMs);
		_now = time is not null
			? () => time.GetUtcNow().UtcDateTime
			: now ?? (() => DateTime.UtcNow);
	}

	/// <summary>True while a button is down, so the caller can treat motion as a drag.</summary>
	public bool IsButtonDown { get { lock (_gate) return _pressedButton != 0; } }

	/// <summary>The button currently held, or 0.</summary>
	public MouseFlags HeldButton { get { lock (_gate) return _pressedButton; } }

	/// <summary>
	/// Takes what the driver reported and returns it with any derived gestures added.
	/// </summary>
	/// <param name="flags">The driver's flags. Not modified.</param>
	/// <param name="position">Where the event happened, in cells.</param>
	/// <returns>
	/// A new list: the input flags, plus <c>Clicked</c> / <c>DoubleClicked</c> /
	/// <c>TripleClicked</c> / <c>Dragged</c> where the gesture applies.
	/// </returns>
	public List<MouseFlags> Recognize(IReadOnlyList<MouseFlags> flags, Point position)
	{
		lock (_gate) return RecognizeCore(flags, position);
	}

	/// <summary>Guards the run state, which two threads reach.</summary>
	private readonly object _gate = new();

	private List<MouseFlags> RecognizeCore(IReadOnlyList<MouseFlags> flags, Point position)
	{
		var result = new List<MouseFlags>(flags);

		// A press is only a press the FIRST time. Two things restate one that is
		// already down: a motion report carries the held button beside
		// ReportMousePosition, and the auto-repeat for a held button raises a
		// bare press every hundred milliseconds. Taking either as new would move
		// the remembered press position to wherever the pointer is now, and the
		// release would then land "near" that and read as a click - so dragging
		// across a control would click whatever it stopped over.
		//
		// Checking the button is already held covers both, and needs nothing
		// from the caller: the repeat is indistinguishable from a press on the
		// wire, and should be.
		bool restated = flags.Contains(MouseFlags.ReportMousePosition) || _pressedButton != 0;

		var pressed = restated ? 0 : PressedButton(flags);
		if (pressed != 0)
		{
			OnPress(pressed, position, result);
			return result;
		}

		var released = ReleasedButton(flags);
		if (released != 0)
		{
			OnRelease(released, position, result);
			return result;
		}

		// Motion with a button held is a drag. The drivers vary in whether they say
		// so, and one of them only says so on some platforms, so it is settled here.
		if (_pressedButton != 0 && flags.Contains(MouseFlags.ReportMousePosition))
		{
			var dragged = DraggedFor(_pressedButton);
			if (dragged != 0 && !result.Contains(dragged)) result.Add(dragged);
		}

		return result;
	}

	private void OnPress(MouseFlags button, Point position, List<MouseFlags> result)
	{
		_pressedButton = button;
		_pressPosition = position;

		// A press continues a run only if it is close in time AND in place to the
		// last click. Moving between clicks means two separate clicks, which is what
		// a user who re-aims expects.
		// Same button, close in time, close in place. Left-then-right is two
		// separate first clicks, not a double-click of either.
		bool continues = _clickRun > 0
			&& _clickRunButton == button
			&& Near(position, _lastClickPosition)
			&& _now() - _lastClickTime <= _multiClickWindow;

		_clickRun = continues ? Math.Min(3, _clickRun + 1) : 1;
		_clickRunButton = button;

		// The press itself is already in result, from the driver. Only the derived
		// flag is added - this is the substitution the old machines made.
		if (_clickRun == 2) AddOnce(result, DoubleFor(button));
		else if (_clickRun == 3) AddOnce(result, TripleFor(button));
	}

	private void OnRelease(MouseFlags button, Point position, List<MouseFlags> result)
	{
		// Compare like with like: _pressedButton holds a PRESS flag and this is a
		// RELEASE flag, and they are different bits of the same [Flags] enum.
		bool wasDown = _pressedButton != 0 && _pressedButton == PressFor(button);
		_pressedButton = 0;

		// A release without its press is not a click: it is the tail of a gesture
		// that began somewhere else, or a duplicate the terminal sent twice.
		if (!wasDown) return;

		// Released away from the press means a drag ended, not a click.
		if (!Near(position, _pressPosition))
		{
			_clickRun = 0;
			return;
		}

		AddOnce(result, ClickedFor(button));

		_lastClickPosition = position;
		_lastClickTime = _now();
	}

	/// <summary>
	/// Adds a flag unless it is already there.
	/// </summary>
	/// <remarks>
	/// Keeps the recogniser idempotent for a caller that already supplied a gesture
	/// - the diagnostics socket sends <c>Button1Clicked</c> directly, and so do
	/// tests. Emitting it twice would make any control counting events double-count.
	/// </remarks>
	private static void AddOnce(List<MouseFlags> flags, MouseFlags flag)
	{
		if (flag != 0 && !flags.Contains(flag)) flags.Add(flag);
	}

	private static bool Near(Point a, Point b) =>
		Math.Abs(a.X - b.X) <= ClickSlopCells && Math.Abs(a.Y - b.Y) <= ClickSlopCells;

	/// <summary>Forgets any run in progress, for a driver restart or a focus loss.</summary>
	public void Reset()
	{
		lock (_gate) ResetCore();
	}

	private void ResetCore()
	{
		_pressedButton = 0;
		_clickRun = 0;
		_clickRunButton = 0;
		_lastClickTime = DateTime.MinValue;
	}

	#region Flag tables

	/// <summary>
	/// Everything that is not a button or an action: Shift, Alt, Ctrl.
	/// </summary>
	/// <remarks>
	/// The Windows decoder ORs the modifiers into the same value as the button, so a
	/// Ctrl+click arrives as <c>Button1Pressed | ButtonCtrl</c> in ONE flag rather
	/// than two. Comparing that for equality against <c>Button1Pressed</c> fails, and
	/// the click is silently dropped - which is exactly the bug that was fixed twice
	/// before, once per decoder, and would have come straight back here.
	/// </remarks>
	private const MouseFlags ModifierMask =
		MouseFlags.ButtonShift | MouseFlags.ButtonAlt | MouseFlags.ButtonCtrl;

	private static MouseFlags PressedButton(IReadOnlyList<MouseFlags> flags)
	{
		foreach (var raw in flags)
		{
			var f = raw & ~ModifierMask;
			if (f == MouseFlags.Button1Pressed) return MouseFlags.Button1Pressed;
			if (f == MouseFlags.Button2Pressed) return MouseFlags.Button2Pressed;
			if (f == MouseFlags.Button3Pressed) return MouseFlags.Button3Pressed;
			if (f == MouseFlags.Button4Pressed) return MouseFlags.Button4Pressed;
		}
		return 0;
	}

	private static MouseFlags ReleasedButton(IReadOnlyList<MouseFlags> flags)
	{
		foreach (var raw in flags)
		{
			var f = raw & ~ModifierMask;
			if (f == MouseFlags.Button1Released) return MouseFlags.Button1Released;
			if (f == MouseFlags.Button2Released) return MouseFlags.Button2Released;
			if (f == MouseFlags.Button3Released) return MouseFlags.Button3Released;
			if (f == MouseFlags.Button4Released) return MouseFlags.Button4Released;
		}
		return 0;
	}

	/// <summary>The press flag that matches a release flag.</summary>
	private static MouseFlags PressFor(MouseFlags released) => released switch
	{
		MouseFlags.Button1Released => MouseFlags.Button1Pressed,
		MouseFlags.Button2Released => MouseFlags.Button2Pressed,
		MouseFlags.Button3Released => MouseFlags.Button3Pressed,
		MouseFlags.Button4Released => MouseFlags.Button4Pressed,
		_ => 0,
	};

	private static MouseFlags ClickedFor(MouseFlags released) => released switch
	{
		MouseFlags.Button1Released => MouseFlags.Button1Clicked,
		MouseFlags.Button2Released => MouseFlags.Button2Clicked,
		MouseFlags.Button3Released => MouseFlags.Button3Clicked,
		MouseFlags.Button4Released => MouseFlags.Button4Clicked,
		_ => 0,
	};

	private static MouseFlags DoubleFor(MouseFlags pressed) => pressed switch
	{
		MouseFlags.Button1Pressed => MouseFlags.Button1DoubleClicked,
		MouseFlags.Button2Pressed => MouseFlags.Button2DoubleClicked,
		MouseFlags.Button3Pressed => MouseFlags.Button3DoubleClicked,
		MouseFlags.Button4Pressed => MouseFlags.Button4DoubleClicked,
		_ => 0,
	};

	private static MouseFlags TripleFor(MouseFlags pressed) => pressed switch
	{
		MouseFlags.Button1Pressed => MouseFlags.Button1TripleClicked,
		MouseFlags.Button2Pressed => MouseFlags.Button2TripleClicked,
		MouseFlags.Button3Pressed => MouseFlags.Button3TripleClicked,
		MouseFlags.Button4Pressed => MouseFlags.Button4TripleClicked,
		_ => 0,
	};

	/// <remarks>Button 4 has no drag flag; it is a wheel-adjacent button, not a drag source.</remarks>
	private static MouseFlags DraggedFor(MouseFlags pressed) => pressed switch
	{
		MouseFlags.Button1Pressed => MouseFlags.Button1Dragged,
		MouseFlags.Button2Pressed => MouseFlags.Button2Dragged,
		MouseFlags.Button3Pressed => MouseFlags.Button3Dragged,
		_ => 0,
	};

	#endregion
}

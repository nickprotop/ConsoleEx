// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;

namespace SharpConsoleUI.Core;

/// <summary>
/// What the system is still working on, and what that leaves outstanding.
/// </summary>
/// <remarks>
/// <para>
/// The question has one answer and two shapes: a boolean for a caller that
/// wants to know whether to look yet, and a sentence for one that has to say
/// why it gave up waiting. Those were two parallel transcriptions of the same
/// conditions, so adding one meant editing both, and nothing checked they
/// agreed. Here the sentence IS the list of what is blocking, and settled IS
/// that list being empty, so they cannot disagree.
/// </para>
/// <para>
/// Each condition is a question about work OUTSTANDING. Two of them are about
/// work a queue cannot report: a mouse event is dispatched synchronously on
/// whichever thread raised it and never enters a queue, and a UI action is
/// taken out of its queue before it runs. Both are counted while they run.
/// </para>
/// <para>
/// This is a momentary read, not a snapshot. Each condition belongs to a
/// subsystem on its own thread, so they are answered one after another and
/// clearing all of them does not promise that nothing is about to arrive.
/// Waiting is the caller's business, and
/// <see cref="ConsoleWindowSystem.WaitUntilSettledAsync"/> adds a composed
/// frame to it for that reason.
/// </para>
/// </remarks>
internal sealed class WorkingState
{
	private readonly InputStateService _input;
	private readonly ConcurrentQueue<(Action Action, string? Label)> _uiActions;
	private readonly WorkInFlight _uiActionsRunning;
	private readonly Func<Drivers.IConsoleDriver> _driver;
	private readonly Func<bool> _animating;
	private readonly Func<IEnumerable<Window>> _windows;

	/// <remarks>
	/// The driver arrives as a function rather than an instance:
	/// <see cref="ConsoleWindowSystem.ConsoleDriver"/> has a setter, and a
	/// captured instance would go on answering for a driver that has been
	/// replaced. The windows and the animation flag are read late for the same
	/// reason, since both can change after this is built.
	/// </remarks>
	internal WorkingState(
		InputStateService input,
		ConcurrentQueue<(Action Action, string? Label)> uiActions,
		WorkInFlight uiActionsRunning,
		Func<Drivers.IConsoleDriver> driver,
		Func<bool> animating,
		Func<IEnumerable<Window>> windows)
	{
		_input = input;
		_uiActions = uiActions;
		_uiActionsRunning = uiActionsRunning;
		_driver = driver;
		_animating = animating;
		_windows = windows;
	}

	/// <summary>Whether nothing is outstanding.</summary>
	internal bool IsSettled => Blocking().Count == 0;

	/// <summary>What is outstanding, named, or empty when nothing is.</summary>
	internal string Blockers => string.Join(", ", Blocking());

	/// <summary>
	/// The conditions that are holding it, in the order worth reading.
	/// </summary>
	/// <remarks>
	/// One list, which both members above are derived from. A condition added
	/// here is answered by both without being written twice.
	/// </remarks>
	private List<string> Blocking()
	{
		var parts = new List<string>(6);

		if (_input.HasPendingInput) parts.Add("input pending");
		if (_driver().IsDispatchingMouse) parts.Add("a mouse handler is running");
		if (!_uiActions.IsEmpty) parts.Add($"{_uiActions.Count} queued action(s)");
		if (_uiActionsRunning.IsRunning) parts.Add("a queued action is running");
		if (_animating()) parts.Add("animations running");

		int dirty = 0;
		foreach (var w in _windows())
		{
			if (w.PendingWork != FrameWork.None) dirty++;
		}
		if (dirty > 0) parts.Add($"{dirty} window(s) with work pending");

		return parts;
	}
}

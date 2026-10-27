// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using Cxdbg.Protocol;

namespace Cxdbg.Session;

/// <summary>What the session last learned about the target.</summary>
/// <param name="Frame">The target's frame number, or the last one seen.</param>
/// <param name="Tree">Its control tree.</param>
/// <param name="Screen">What its screen shows, as lines.</param>
/// <param name="Gone">Whether the target has stopped answering.</param>
internal sealed record TargetState(
	long Frame,
	JsonNode? Tree,
	IReadOnlyList<string> Screen,
	bool Gone)
{
	internal static TargetState Empty { get; } = new(0, null, [], false);
}

/// <summary>
/// Keeps asking the target what it looks like.
/// </summary>
/// <remarks>
/// <para>
/// <c>frame</c> is cheap and unmarshalled, so it leads: when the counter has
/// not moved, nothing has changed and the expensive questions are skipped.
/// <c>snapshot</c> takes <c>sinceFrame</c> and answers <c>unchanged</c>
/// itself, so even the screen costs nothing on a still app.
/// </para>
/// <para>
/// <c>measure</c> is never polled. It forces a relayout, so polling it would
/// make the debugger the cause of the churn its own panes report - the
/// observer changing what it observes. It runs on selection only.
/// </para>
/// </remarks>
internal sealed class TargetPoller : IDisposable
{
	private readonly string _socket;
	private readonly CancellationTokenSource _stop = new();
	private readonly Task _loop;

	private TargetState _state = TargetState.Empty;
	private readonly Lock _gate = new();

	/// <summary>Raised after each poll that changed something.</summary>
	internal event Action<TargetState>? Changed;

	/// <summary>Whether polling is paused, so the view holds still.</summary>
	internal bool Frozen { get; set; }

	internal TargetPoller(string socket, TimeSpan? interval = null)
	{
		_socket = socket;
		_loop = Task.Run(() => Loop(interval ?? TimeSpan.FromMilliseconds(120)));
	}

	/// <summary>The last thing the target said.</summary>
	internal TargetState State
	{
		get { lock (_gate) return _state; }
	}

	/// <summary>
	/// Asks the target one question, now, off the polling loop.
	/// </summary>
	/// <param name="verb">The protocol verb.</param>
	/// <param name="args">Its fields.</param>
	/// <returns>The reply, or null when the target did not answer.</returns>
	/// <remarks>
	/// This is how the expensive, select-only questions are asked: a cascade
	/// when the selection moves, not on every frame.
	/// </remarks>
	internal JsonNode? AskNow(string verb, Dictionary<string, JsonNode?>? args = null)
	{
		try
		{
			using var client = DiagnosticsClient.Connect(_socket);
			return client.Send(verb, args);
		}
		catch (CxdbgException)
		{
			return null;
		}
	}

	private void Loop(TimeSpan interval)
	{
		while (!_stop.IsCancellationRequested)
		{
			if (!Frozen) PollOnce();

			try { Task.Delay(interval, _stop.Token).Wait(_stop.Token); }
			catch (OperationCanceledException) { return; }
			catch (AggregateException) { return; }
		}
	}

	private void PollOnce()
	{
		TargetState next;

		try
		{
			using var client = DiagnosticsClient.Connect(_socket);

			long frame = client.Send("frame")["frame"]?.GetValue<long>() ?? 0;
			var current = State;

			// Nothing has moved: do not pay for the tree or the screen, and do
			// not raise a change nobody can see.
			if (!current.Gone && frame == current.Frame && current.Tree != null) return;

			var tree = client.Send("tree");
			var snapshot = client.Send("snapshot");

			var lines = snapshot["lines"]?.AsArray();
			var screen = lines == null
				? current.Screen
				: lines.Select(l => l?.GetValue<string>() ?? "").ToList();

			next = new TargetState(frame, tree, screen, Gone: false);
		}
		catch (CxdbgException)
		{
			// The target stopped answering. Keep the last picture - what it
			// looked like when it died is usually the reason you attached -
			// and say so rather than clearing the panes or hanging.
			var current = State;
			if (current.Gone) return;

			next = current with { Gone = true };
		}

		lock (_gate) _state = next;
		Changed?.Invoke(next);
	}

	public void Dispose()
	{
		_stop.Cancel();
		try { _loop.Wait(2000); } catch { /* going away anyway */ }
		_stop.Dispose();
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.Json.Nodes;
using Cxdbg.Protocol;
using SharpConsoleUI;
using SharpConsoleUI.Layout;

namespace Cxdbg.Session;

/// <summary>What the session last learned about the target.</summary>
/// <param name="Frame">The target's frame number, or the last one seen.</param>
/// <param name="Tree">Its control tree.</param>
/// <param name="Screen">Its screen, one list of cells per row.</param>
/// <param name="Gone">Whether the target has stopped answering.</param>
internal sealed record TargetState(
	long Frame,
	JsonNode? Tree,
	JsonNode? Focus,
	IReadOnlyList<IReadOnlyList<Cell>> Screen,
	bool Gone)
{
	internal static TargetState Empty { get; } = new(0, null, null, [], false);
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

	/// <summary>The target's process id, from its greeting.</summary>
	/// <remarks>
	/// In the header, because a session that does not say which application it
	/// is looking at is one you cannot tell from a session looking at another.
	/// </remarks>
	internal int Pid { get; private set; }

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

	/// <summary>
	/// Turns a cells reply into rows of cells.
	/// </summary>
	/// <param name="snapshot">The reply from <c>snapshot format:cells</c>.</param>
	/// <returns>The rows, or null when the reply carried none.</returns>
	/// <remarks>
	/// A continuation cell is written back as one, so a wide glyph still
	/// occupies the two columns it occupied in the application. Reconstructing
	/// it any other way - from the character's own width, say - would be a
	/// second opinion about something the server already stated.
	/// </remarks>
	private static List<IReadOnlyList<Cell>>? ReadCells(JsonNode? snapshot)
	{
		var rows = snapshot?["rows"]?.AsArray();
		if (rows == null) return null;

		var result = new List<IReadOnlyList<Cell>>(rows.Count);

		foreach (var row in rows)
		{
			var cells = new List<Cell>();

			foreach (var c in row?.AsArray() ?? [])
			{
				var fg = Rgb(c?["fg"], Color.White);
				var bg = Rgb(c?["bg"], Color.Black);

				var dec = TextDecoration.None;
				if (c?["dec"]?.GetValue<string>() is { } names
					&& Enum.TryParse<TextDecoration>(names, true, out var parsed))
				{
					dec = parsed;
				}

				if (c?["cont"]?.GetValue<bool>() == true)
				{
					cells.Add(new Cell(' ', fg, bg, dec) { IsWideContinuation = true });
					continue;
				}

				string ch = c?["ch"]?.GetValue<string>() ?? " ";
				var rune = ch.Length > 0
					? System.Text.Rune.GetRuneAt(ch, 0)
					: new System.Text.Rune(' ');

				cells.Add(new Cell(rune, fg, bg, dec)
				{
					Combiners = c?["comb"]?.GetValue<string>(),
				});
			}

			result.Add(cells);
		}

		return result;
	}

	/// <summary>Three numbers back into a colour.</summary>
	private static Color Rgb(JsonNode? node, Color fallback)
	{
		var a = node?.AsArray();
		if (a == null || a.Count < 3) return fallback;

		return new Color(
			(byte)(a[0]?.GetValue<int>() ?? 0),
			(byte)(a[1]?.GetValue<int>() ?? 0),
			(byte)(a[2]?.GetValue<int>() ?? 0));
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

			Pid = client.Pid;

			long frame = client.Send("frame")["frame"]?.GetValue<long>() ?? 0;
			var current = State;

			// Nothing has moved: do not pay for the tree or the screen, and do
			// not raise a change nobody can see.
			if (!current.Gone && frame == current.Frame && current.Tree != null) return;

			var tree = client.Send("tree");
			var focus = client.Send("focus");

			// Cells out, cells in. The target has cells and the pane paints
			// cells, so nothing is serialised as text and parsed back: no
			// format sits in the middle to lose a continuation cell or a
			// combining mark, and no second parser can drift from the first.
			var snapshot = client.Send("snapshot",
				new Dictionary<string, JsonNode?> { ["format"] = "cells" });

			var screen = ReadCells(snapshot) ?? current.Screen;

			next = new TargetState(frame, tree, focus, screen, Gone: false);
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

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using Cxdbg.Format;
using Cxdbg.Protocol;
using Spectre.Console;

namespace Cxdbg.Commands;

/// <summary>
/// Asks the same question over and over until interrupted.
/// </summary>
/// <remarks>
/// <para>
/// Polling, not a subscription. The application serves one client at a time, so
/// a stream would have to share the connection with every other verb; and the
/// figures worth watching are already windowed on the application's side, so
/// nothing is missed between two polls - the window covers the gap. What this
/// adds is a person watching it change.
/// </para>
/// <para>
/// Any verb, not just the performance ones: watching <c>frame</c> says whether
/// the loop is running at all, and watching <c>windows</c> shows a dialog open
/// and close.
/// </para>
/// </remarks>
internal static class WatchCommand
{
	/// <summary>How often to ask, when nobody says.</summary>
	/// <remarks>
	/// A second. Fast enough to feel live, slow enough that the numbers are
	/// readable rather than a blur, and far slower than the window being
	/// summarised - so consecutive answers genuinely differ.
	/// </remarks>
	private const double DefaultIntervalSeconds = 1.0;

	/// <summary>
	/// Watches a verb on a connection the caller owns, for the prompt.
	/// </summary>
	/// <remarks>
	/// The prompt already holds the one connection the application allows, so a
	/// watch typed there has to borrow it rather than open its own - which would
	/// be refused as a second client.
	/// </remarks>
	/// <param name="client">The prompt's connection.</param>
	/// <param name="words">The command as typed, starting with <c>watch</c>.</param>
	internal static void RunOn(DiagnosticsClient client, string[] words)
	{
		var line = CommandLineParser.Parse(words.Skip(1).ToArray());

		string verb = string.IsNullOrEmpty(line.Verb) ? "performance" : line.Verb;

		double interval = Number(line, "interval") ?? DefaultIntervalSeconds;

		if (interval <= 0)
		{
			AnsiConsole.MarkupLine("[red]--interval must be more than zero seconds.[/]");
			return;
		}

		int? count = (int?)Number(line, "count");

		var fields = new Dictionary<string, JsonNode?>(line.Fields);
		fields.Remove("interval");
		fields.Remove("count");

		Output.WarnAboutLikelyTypos(verb, fields.Keys);

		using var stop = new CancellationTokenSource();

		ConsoleCancelEventHandler onCancel = (_, e) =>
		{
			if (stop.IsCancellationRequested) return;
			e.Cancel = true;
			stop.Cancel();
		};

		Console.CancelKeyPress += onCancel;

		try { Loop(client, verb, fields, interval, count, line, stop.Token); }
		catch (CxdbgException ex) { AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]"); }
		finally { Console.CancelKeyPress -= onCancel; }
	}

	/// <summary>Watches a verb until Esc is pressed, or the count runs out.</summary>
	internal static int Execute(CommandLine line)
	{
		string verb = line.Rest.Count > 0 ? line.Rest[0] : "performance";

		double interval = Number(line, "interval") ?? DefaultIntervalSeconds;

		if (interval <= 0)
		{
			AnsiConsole.MarkupLine("[red]--interval must be more than zero seconds.[/]");
			return 3;
		}

		int? count = (int?)Number(line, "count");

		// The verb's own fields are whatever is left: `watch performance --reset`
		// resets once, on the first ask, and then watches what follows.
		var fields = new Dictionary<string, JsonNode?>(line.Fields);
		fields.Remove("interval");
		fields.Remove("count");

		Output.WarnAboutLikelyTypos(verb, fields.Keys);

		using var stop = new CancellationTokenSource();

		// Esc stops the watch. Not ctrl-c: that quits a terminal application, and
		// inside `cxdbg shell` it reads as "quit cxdbg" - a key that means stop
		// watching must not be the same key that means stop everything. Ctrl-c
		// still works, because a watch piped into something has no keyboard to
		// read and a habit is a habit.
		ConsoleCancelEventHandler onCancel = (_, e) =>
		{
			if (stop.IsCancellationRequested) return;
			e.Cancel = true;
			stop.Cancel();
		};

		Console.CancelKeyPress += onCancel;

		try
		{
			return CommandRunner.Run(line, client => Loop(client, verb, fields, interval, count, line, stop.Token));
		}
		finally
		{
			Console.CancelKeyPress -= onCancel;
		}
	}

	/// <summary>Whether a key meaning "stop" has been pressed since the last look.</summary>
	/// <remarks>
	/// <para>
	/// Esc or q. Esc is what this prompt uses to back out of things, and q is what
	/// every pager has meant for forty years - someone watching a screen of
	/// numbers will reach for one or the other without being told. Ctrl-c works
	/// too, through the cancel handler, but it is not the advertised key: it
	/// means "quit the application" everywhere else, and inside `cxdbg shell` it
	/// would read as "quit cxdbg".
	/// </para>
	/// <para>
	/// Drained rather than peeked: every key waiting is taken, so a run of arrow
	/// keys does not leave escape sequences behind for the shell prompt to read
	/// as commands once the watch ends. An arrow key arrives AS an escape
	/// sequence, so a bare Escape is only believed when nothing follows it.
	/// </para>
	/// </remarks>
	private static bool StopPressed()
	{
		// No keyboard to read: a watch in a pipe ends on its count, or on ctrl-c.
		if (Console.IsInputRedirected) return false;

		bool stop = false;

		while (Console.KeyAvailable)
		{
			var key = Console.ReadKey(intercept: true);

			if (key.Key == ConsoleKey.Q) stop = true;

			// Ctrl-c arrives as a key now that the terminal is in single-key mode,
			// so it is honoured here rather than by the cancel handler - someone
			// pressing it still expects the watch to stop.
			if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
				stop = true;

			// Only a bare Escape, since every arrow and function key arrives as
			// one followed by more.
			if (key.Key == ConsoleKey.Escape && !Console.KeyAvailable) stop = true;
		}

		return stop;
	}

	private static string Loop(
		DiagnosticsClient client,
		string verb,
		Dictionary<string, JsonNode?> fields,
		double interval,
		int? count,
		CommandLine line,
		CancellationToken stop)
	{
		var wait = TimeSpan.FromSeconds(interval);

		// Redraw in place only when there is a terminal to move a cursor around
		// in. In JSON or down a pipe each sample is appended, because a log of
		// samples is what a script wants.
		bool live = !line.Json && !Console.IsOutputRedirected;
		int asked = 0;
		int drawn = 0;
		JsonNode? previous = null;

		using var keyboard = RawKeyboard.Enter();

		while (!stop.IsCancellationRequested && (count == null || asked < count))
		{
			var reply = client.Send(verb, fields);
			asked++;

			// --reset is a one-off: leaving it on would empty the window every
			// tick and every answer would be the first frame after a reset.
			fields.Remove("reset");

			// Each number shown with how far it moved since the last tick. Done
			// here rather than in the reply, because a delta is a thing a WATCH
			// knows - a single ask has nothing to compare against, and a caller
			// piping to jq wants the number, not a number with a note on it.
			// Kept BEFORE annotating: Annotate rewrites each number as a string
			// carrying its change, and comparing against that next tick would
			// find no numbers at all - the deltas would stop after the first one.
			var pristine = reply.DeepClone();

			if (live && previous != null) Deltas.Annotate(reply, previous);

			previous = pristine;

			string text = Output.Render(verb, reply, line);

			string block = text + Footer(verb, interval, line.NoColor);

			if (live)
			{
				// Up over what this watch drew last time, and over that only.
				// Console.Clear would wipe the whole screen - at the prompt that
				// takes the session's scrollback with it, which is the history
				// someone is watching these numbers AGAINST.
				if (drawn > 0) Console.Write($"\u001b[{drawn}A");

				// Each line cleared to its end as it is rewritten, so a shorter
				// reply cannot leave the tail of a longer one behind it.
				var rows = Lines(block);

				foreach (string row in rows)
					Console.Write(row + "\u001b[K\n");

				drawn = rows.Count;
			}
			else
			{
				// Appending: the footer still goes out, so a watch in a pipe says
				// what it is rather than being an unexplained column of numbers.
				Console.Write(line.Json ? text : block);
			}

			if (count != null && asked >= count) break;

			// Slept in slices so Esc is noticed within a tick of being pressed
			// rather than at the end of the interval - at a one-second poll, waiting
			// out the full interval makes the key feel ignored. Cancellation still
			// ends the wait at once.
			if (WaitOrStop(wait, stop)) break;
		}

		// Everything was written as it arrived; the runner has nothing left to print.
		return string.Empty;
	}

	/// <summary>A block split into the lines it will occupy on screen.</summary>
	/// <remarks>
	/// Counted rather than guessed, because the cursor has to come back up by
	/// exactly as many lines as went down. A trailing newline produces an empty
	/// final entry, which is dropped: it is a line terminator, not a line.
	/// </remarks>
	private static List<string> Lines(string block)
	{
		var rows = block.Replace("\r\n", "\n").Split('\n').ToList();

		if (rows.Count > 0 && rows[^1].Length == 0) rows.RemoveAt(rows.Count - 1);

		return rows;
	}

	/// <summary>Waits out one interval, stopping early for Esc or cancellation.</summary>
	/// <returns>True when the watch should end.</returns>
	private static bool WaitOrStop(TimeSpan interval, CancellationToken stop)
	{
		var slice = TimeSpan.FromMilliseconds(80);
		var remaining = interval;

		while (remaining > TimeSpan.Zero)
		{
			if (StopPressed()) return true;

			var step = remaining < slice ? remaining : slice;

			if (stop.WaitHandle.WaitOne(step)) return true;

			remaining -= step;
		}

		return StopPressed();
	}

	/// <summary>The line under a live view, saying how to leave it.</summary>
	private static string Footer(string verb, double interval, bool noColour)
	{
		string text = $"watching {verb} every {interval:0.##}s - esc or q to stop";

		// Both branches end in a newline. Surface.Render does not add one, and
		// without it the next sample starts on the same line as the footer -
		// "esc or q to stoprate ----", which is how it read at the prompt, where
		// samples append instead of replacing a cleared screen.
		string body = Surface.Colour(noColour)
			? Surface.Render(new Spectre.Console.Markup($"[dim]{text}[/]"), true).TrimEnd('\n')
			: text;

		return $"\n{body}\n";
	}

	/// <summary>Reads a numeric field, if it is there and is a number.</summary>
	private static double? Number(CommandLine line, string name)
	{
		if (!line.Fields.TryGetValue(name, out var node) || node == null) return null;

		try { return node.GetValue<double>(); }
		catch { return double.TryParse(node.ToString(), out double d) ? d : null; }
	}
}

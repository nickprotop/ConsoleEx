// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;

namespace Cxdbg.Commands;

/// <summary>
/// Reads a line, with history and completion.
/// </summary>
/// <remarks>
/// <para>
/// Written rather than taken from a package: the published options are
/// unmaintained - the obvious one has had no commit since 2021 and
/// twenty-odd open issues - and a dotnet tool we ship owns its dependencies
/// in practice. What is needed here is small and does not change: a line
/// buffer, a cursor, a history list, and Tab.
/// </para>
/// <para>
/// Falls back to <see cref="Console.ReadLine"/> whenever input is redirected.
/// That is not a convenience: the prompt is driven by a pipe in its tests and
/// in anyone's script, and a key loop reading a pipe gets nothing.
/// </para>
/// </remarks>
internal sealed class LineEditor
{
	private readonly List<string> _history = new();
	private readonly Func<string, int, IReadOnlyList<string>> _complete;

	/// <summary>Creates an editor.</summary>
	/// <param name="complete">
	/// Given the line so far and the cursor, the words that could follow.
	/// </param>
	internal LineEditor(Func<string, int, IReadOnlyList<string>> complete) =>
		_complete = complete;

	/// <summary>Reads one line.</summary>
	/// <param name="prompt">What to show, already written.</param>
	/// <returns>The line, or null at end of input.</returns>
	internal string? Read(string prompt)
	{
		// A pipe has no keys to read and no cursor to move.
		if (Console.IsInputRedirected) return Console.ReadLine();

		var line = new StringBuilder();
		int cursor = 0;
		int history = _history.Count;
		string pending = "";

		while (true)
		{
			var key = Console.ReadKey(intercept: true);

			switch (key.Key)
			{
				case ConsoleKey.Enter:
					Console.WriteLine();
					string text = line.ToString();

					// Only what is worth recalling, and never twice running.
					if (text.Trim().Length > 0
						&& (_history.Count == 0 || _history[^1] != text))
					{
						_history.Add(text);
					}

					return text;

				case ConsoleKey.Backspace when cursor > 0:
					line.Remove(--cursor, 1);
					break;

				case ConsoleKey.Delete when cursor < line.Length:
					line.Remove(cursor, 1);
					break;

				case ConsoleKey.LeftArrow when cursor > 0:
					cursor--;
					break;

				case ConsoleKey.RightArrow when cursor < line.Length:
					cursor++;
					break;

				case ConsoleKey.Home:
					cursor = 0;
					break;

				case ConsoleKey.End:
					cursor = line.Length;
					break;

				case ConsoleKey.UpArrow when history > 0:
					// What was being typed is kept, so coming back down
					// returns it rather than losing it.
					if (history == _history.Count) pending = line.ToString();

					line.Clear().Append(_history[--history]);
					cursor = line.Length;
					break;

				case ConsoleKey.DownArrow when history < _history.Count:
					history++;
					line.Clear().Append(history == _history.Count ? pending : _history[history]);
					cursor = line.Length;
					break;

				case ConsoleKey.Tab:
					cursor = Complete(line, cursor, prompt);
					break;

				default:
					if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
					{
						switch (key.Key)
						{
							case ConsoleKey.A: cursor = 0; break;
							case ConsoleKey.E: cursor = line.Length; break;
							case ConsoleKey.U: line.Remove(0, cursor); cursor = 0; break;
							case ConsoleKey.K: line.Remove(cursor, line.Length - cursor); break;

							// Ctrl+C and Ctrl+D leave, which is what every
							// prompt does and what fingers expect.
							case ConsoleKey.C:
							case ConsoleKey.D:
								Console.WriteLine();
								return null;
						}

						break;
					}

					if (!char.IsControl(key.KeyChar))
					{
						line.Insert(cursor++, key.KeyChar);
					}

					break;
			}

			Draw(prompt, line.ToString(), cursor);
		}
	}

	/// <summary>
	/// Completes the word under the cursor.
	/// </summary>
	/// <returns>Where the cursor ended up.</returns>
	/// <remarks>
	/// One candidate completes in place. Several print as a list and complete
	/// as far as they agree, which is what every shell does and what makes Tab
	/// worth pressing twice.
	/// </remarks>
	private int Complete(StringBuilder line, int cursor, string prompt)
	{
		string text = line.ToString();
		var candidates = _complete(text, cursor);

		if (candidates.Count == 0) return cursor;

		int start = cursor;
		while (start > 0 && text[start - 1] != ' ') start--;

		string shared = Shared(candidates);

		if (candidates.Count > 1)
		{
			Console.WriteLine();
			Console.WriteLine("  " + string.Join("  ", candidates));
		}

		// A shared prefix of nothing but punctuation is not a completion. The
		// fields of a verb all begin "--", so Tab on an empty word inserted
		// those two characters and then the dashes the user typed next landed
		// after them: "logs ----level". Offer the list and leave the line be.
		if (shared.Trim('-').Length == 0 && cursor == start) return cursor;

		if (shared.Length > cursor - start)
		{
			line.Remove(start, cursor - start).Insert(start, shared);
			cursor = start + shared.Length;

			// One answer is a finished word, so the space is part of it.
			if (candidates.Count == 1) { line.Insert(cursor++, ' '); }
		}

		return cursor;
	}

	/// <summary>
	/// What Tab would leave in place of the last word, for tests.
	/// </summary>
	/// <param name="typed">The line so far.</param>
	/// <returns>What the word being typed became.</returns>
	/// <remarks>
	/// The splice is where a completion bug lives - the candidates can be
	/// right while what reaches the line is wrong - and driving a pty to find
	/// that out is slow and flaky.
	/// </remarks>
	internal string CompleteForTest(string typed)
	{
		var line = new StringBuilder(typed);
		Complete(line, typed.Length, "");

		// The word the cursor is on, from the last space BEFORE where typing
		// started - so an empty word stays empty rather than reaching back
		// and picking up the verb.
		int start = typed.LastIndexOf(' ') + 1;

		return line.ToString()[start..];
	}

	/// <summary>The longest beginning every candidate shares.</summary>
	private static string Shared(IReadOnlyList<string> candidates)
	{
		string first = candidates[0];
		int length = first.Length;

		foreach (string c in candidates)
		{
			int i = 0;
			while (i < length && i < c.Length && first[i] == c[i]) i++;
			length = i;
		}

		return first[..length];
	}

	/// <summary>Repaints the line and puts the cursor back.</summary>
	private static void Draw(string prompt, string line, int cursor)
	{
		Console.Write('\r');
		Console.Write(prompt);
		Console.Write(line);
		Console.Write("\u001b[K");   // to end of line, for what was deleted

		Console.Write('\r');
		Console.Write("\u001b[" + (prompt.Length + cursor) + "C");
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace Cxdbg.Commands;

/// <summary>
/// What could come next at the prompt.
/// </summary>
/// <remarks>
/// Three places a word can be: the verb, a field of that verb, or a path
/// after a redirect. Fields come from each verb's own example in
/// <see cref="ProtocolVerbs.All"/>, which is the only list cxdbg keeps of
/// what a verb takes - and is why an application's unknown fields still reach
/// it unchallenged. Tab is a convenience, never a gate.
/// </remarks>
internal static class Completion
{
	/// <summary>The prompt's own commands.</summary>
	private static readonly string[] Dots =
		[".help", ".show", ".why", ".last", ".socket"];

	/// <summary>Everything that could follow what has been typed.</summary>
	/// <param name="line">The line so far.</param>
	/// <param name="cursor">Where the cursor is.</param>
	internal static IReadOnlyList<string> For(string line, int cursor)
	{
		string head = line[..cursor];
		var words = head.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		// The word being typed, which is empty right after a space.
		string word = head.EndsWith(' ') ? "" : words.Length > 0 ? words[^1] : "";
		int position = head.EndsWith(' ') ? words.Length : words.Length - 1;

		// After a redirect, a path - which the shell completes against the
		// filesystem, not against anything cxdbg knows.
		if (position > 0 && words[position - 1] is ">" or ">>") return Paths(word);

		if (position <= 0) return Match(First(), word);

		// `watch` takes a verb and then that verb's fields, so completion has to
		// look one word further along: after `watch` comes a verb, and after
		// `watch performance` come performance's fields plus watch's own.
		if (string.Equals(words[0], "watch", StringComparison.OrdinalIgnoreCase))
		{
			if (position == 1) return Match(Watchable(), word);

			return Match(Fields(words[1]).Concat(WatchFields), word);
		}

		return Match(Fields(words[0]), word);
	}

	/// <summary>`watch`'s own fields, which any watched verb also accepts.</summary>
	private static readonly string[] WatchFields = ["--interval", "--count"];

	/// <summary>The verbs worth watching: the ones that answer with a reading.</summary>
	/// <remarks>
	/// Every verb is offered - watching any of them is legal, and a verb this
	/// build has never heard of still works - but the ones whose answer changes
	/// on its own are what someone reaches for, so they come first.
	/// </remarks>
	private static IEnumerable<string> Watchable()
	{
		string[] live = ["performance", "metrics", "frame", "invalidations", "windows", "focus"];

		return live.Concat(ProtocolVerbs.All.Select(v => v.Verb).Where(v => !live.Contains(v)));
	}

	/// <summary>Verbs, cxdbg's own commands, and the words that leave.</summary>
	private static IEnumerable<string> First() =>
		ProtocolVerbs.All.Select(v => v.Verb)
			.Concat(Dots)
			.Concat(["connect", "watch", "exit"]);

	/// <summary>The fields a verb is known to take.</summary>
	/// <remarks>
	/// From the verb's declared fields, which are what the server reads. A
	/// field listed nowhere still works - the protocol decides what exists -
	/// it is simply not offered.
	/// </remarks>
	private static IEnumerable<string> Fields(string verb)
	{
		foreach (var (name, _, fields, _) in ProtocolVerbs.All)
		{
			if (!string.Equals(name, verb, StringComparison.OrdinalIgnoreCase)) continue;

			return fields.Select(f => "--" + f.Name);
		}

		return [];
	}

	/// <summary>Files and directories a path could mean.</summary>
	private static IReadOnlyList<string> Paths(string word)
	{
		try
		{
			string directory = Path.GetDirectoryName(word) is { Length: > 0 } d ? d : ".";
			string start = Path.GetFileName(word);

			var found = new List<string>();

			foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
			{
				string name = Path.GetFileName(entry);
				if (!name.StartsWith(start, StringComparison.Ordinal)) continue;

				found.Add(directory == "." ? name : Path.Combine(directory, name));
			}

			found.Sort(StringComparer.Ordinal);
			return found;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
			or ArgumentException)
		{
			// A path that cannot be read is not a failure worth interrupting
			// someone's typing for.
			return [];
		}
	}

	/// <summary>Those that start with what has been typed.</summary>
	private static IReadOnlyList<string> Match(IEnumerable<string> all, string word)
	{
		var found = all
			.Where(c => c.StartsWith(word, StringComparison.OrdinalIgnoreCase))
			.ToList();

		found.Sort(StringComparer.Ordinal);
		return found;
	}
}

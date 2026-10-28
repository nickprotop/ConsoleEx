// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using Cxdbg.Protocol;

namespace Cxdbg.Commands;

/// <summary>
/// A command line, read the one way cxdbg reads them.
/// </summary>
/// <param name="Verb">The first word: a protocol verb, or one of cxdbg's own.</param>
/// <param name="Rest">What followed it, minus the options read out below.</param>
/// <param name="Fields">Protocol fields, from <c>--name value</c>.</param>
/// <param name="Socket">The socket, from <c>-s</c> or <c>--socket</c>.</param>
/// <param name="Json">Whether <c>--json</c> was given.</param>
/// <param name="NoColor">Whether colour was refused.</param>
/// <param name="AppArgs">Everything after <c>--</c>, which belongs to an application.</param>
internal sealed record CommandLine(
	string Verb,
	IReadOnlyList<string> Rest,
	Dictionary<string, JsonNode?> Fields,
	string? Socket,
	bool Json,
	bool NoColor,
	IReadOnlyList<string> AppArgs);

/// <summary>
/// Reads a command line into a verb and the protocol fields that go with it.
/// </summary>
/// <remarks>
/// <para>
/// ONE parser, for the command line and for the prompt. They used to be two -
/// a validating one on the CLI and this one at the prompt - and they disagreed:
/// <c>--y 7</c> was an error on the command line and worked at the prompt,
/// while <c>--arg y=7</c> worked on the command line and at the prompt quietly
/// set a field called "arg" and returned the wrong row. Two parsers for one
/// syntax cannot be kept in step by discipline, so there is one.
/// </para>
/// <para>
/// The rule is <c>verb --field value</c>, where the field names are the
/// protocol's own. Nothing is declared in advance: a verb that gains a field
/// gains an option, and a verb this build has never heard of still reaches the
/// application. A parser that validated against a declared list could not do
/// that, which is why there is no such list.
/// </para>
/// </remarks>
internal static class CommandLineParser
{
	/// <summary>cxdbg's own options, which are never protocol fields.</summary>
	private static readonly string[] Own = ["--socket", "-s", "--json", "--no-color"];

	/// <summary>Reads argv.</summary>
	/// <param name="args">The words, verb first.</param>
	/// <returns>What they said.</returns>
	/// <exception cref="CxdbgException">When a value is missing or malformed.</exception>
	internal static CommandLine Parse(IReadOnlyList<string> args)
	{
		string verb = args.Count > 0 ? args[0] : "";

		var rest = new List<string>();
		var fields = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
		var appArgs = new List<string>();

		string? socket = null;
		bool json = false;
		bool noColor = Environment.GetEnvironmentVariable("NO_COLOR") is { Length: > 0 };

		for (int i = 1; i < args.Count; i++)
		{
			string word = args[i];

			// Everything past the separator belongs to an application, and is
			// never read: a flag of cxdbg's own on that side is a mistake worth
			// naming rather than quietly obeying.
			if (word == "--")
			{
				for (int j = i + 1; j < args.Count; j++) appArgs.Add(args[j]);
				break;
			}

			if (word is "--socket" or "-s")
			{
				socket = Next(args, ref i, word);
				continue;
			}

			if (word == "--json") { json = true; continue; }
			if (word == "--no-color") { noColor = true; continue; }

			if (!word.StartsWith("--", StringComparison.Ordinal))
			{
				rest.Add(word);
				continue;
			}

			// --name=value, so a value may start with a dash: --y=-1.
			int eq = word.IndexOf('=', StringComparison.Ordinal);
			if (eq > 2)
			{
				fields[word[2..eq]] = Value(word[(eq + 1)..]);
				continue;
			}

			string name = word[2..];
			if (name.Length == 0)
			{
				throw CxdbgException.Usage("'--' on its own separates cxdbg's arguments from the application's.");
			}

			// A bare flag is true; anything that is not the next option is its
			// value. --props means {"props":true}, --depth 2 means {"depth":2}.
			bool hasValue = i + 1 < args.Count
				&& args[i + 1] != "--"
				&& !args[i + 1].StartsWith("--", StringComparison.Ordinal)
				&& !Own.Contains(args[i + 1]);

			fields[name] = hasValue ? Value(args[++i]) : true;
		}

		return new CommandLine(verb, rest, fields, socket, json, noColor, appArgs);
	}

	/// <summary>The word after an option that needs one.</summary>
	private static string Next(IReadOnlyList<string> args, ref int i, string option)
	{
		if (++i >= args.Count) throw CxdbgException.Usage($"{option} needs a value.");
		return args[i];
	}

	/// <summary>
	/// A word as the protocol would have it: a number, a bool, or a string.
	/// </summary>
	/// <remarks>
	/// Inferred rather than declared, because nothing here knows what a field
	/// is meant to be - the protocol does. A server reading "depth" as a number
	/// gets a number; one reading "window" as a title gets the title.
	/// </remarks>
	private static JsonNode? Value(string word) =>
		long.TryParse(word, out long n) ? n
		: bool.TryParse(word, out bool b) ? b
		: word;
}

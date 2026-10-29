// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;

namespace Cxdbg.Commands;

/// <summary>
/// Reads the bytes a <c>seq</c> is to send, from the line or from a file.
/// </summary>
/// <remarks>
/// <para>
/// <c>seq &lt; capture.txt</c> is the useful case: somebody's terminal did
/// something strange, they captured what it sent, and that file replays into a
/// running application without being retyped or escaped by hand.
/// </para>
/// <para>
/// One place for both the command line and the prompt, so the two cannot come
/// to disagree about what a redirect means.
/// </para>
/// </remarks>
internal static class SeqInput
{
	/// <summary>How big a capture may be before it is refused.</summary>
	/// <remarks>
	/// A terminal's input is small: even a long paste is kilobytes. A file far
	/// past that is a mistake - the wrong path, or a binary - and sending it
	/// byte by byte through a parser would take a while to go wrong.
	/// </remarks>
	internal const int MostBytes = 64 * 1024;

	/// <summary>
	/// Splits a redirect off a command line, returning the words before it.
	/// </summary>
	/// <param name="words">The line as typed.</param>
	/// <param name="from">The file named after <c>&lt;</c>, or null.</param>
	/// <returns>The words with the redirect removed.</returns>
	/// <remarks>
	/// Only the redirect itself is removed, not everything after it. A shell
	/// puts a redirect wherever it likes, so <c>seq &lt; capture.txt -s app.sock</c>
	/// has to keep the <c>-s</c> - taking the tail as well silently dropped
	/// whichever options were typed on the far side.
	/// </remarks>
	internal static string[] SplitRedirect(string[] words, out string? from)
	{
		from = null;

		int arrow = Array.IndexOf(words, "<");
		if (arrow < 0) return words;

		if (arrow + 1 < words.Length) from = words[arrow + 1];

		// The arrow and its path; the path is only there to skip when one was given.
		int after = arrow + (from == null ? 1 : 2);

		return words.Take(arrow).Concat(words.Skip(after)).ToArray();
	}

	/// <summary>
	/// Reads a capture piped in on standard input.
	/// </summary>
	/// <param name="fields">The request's fields, which gain <c>text</c>.</param>
	/// <returns>An error to show, or null when it worked.</returns>
	/// <remarks>
	/// <c>cxdbg seq &lt; capture.txt</c> is what a person types, and a shell
	/// consumes the redirect before cxdbg ever sees it - so the file arrives
	/// here as stdin rather than as a word on the line. The quoted form
	/// (<c>seq '&lt;' capture.txt</c>) and the prompt's own redirect go through
	/// <see cref="Load"/>; this is the same capture by the other route, and
	/// both end in the same field.
	/// </remarks>
	internal static string? LoadStdin(Dictionary<string, JsonNode?> fields)
	{
		try
		{
			// Capped the same way a file is, and for the same reason: a pipe
			// from the wrong command is a mistake, not a sequence.
			var buffer = new char[MostBytes + 1];
			int read = 0;

			using var input = Console.In;

			while (read < buffer.Length)
			{
				int got = input.Read(buffer, read, buffer.Length - read);
				if (got <= 0) break;
				read += got;
			}

			if (read > MostBytes)
			{
				return $"the piped input is over {MostBytes} bytes; a terminal sequence "
					+ "is not that long";
			}

			string text = new string(buffer, 0, read).TrimEnd('\n', '\r');

			if (text.Length == 0) return "nothing was piped in";

			fields["text"] = JsonValue.Create(text);
			return null;
		}
		catch (IOException ex)
		{
			return $"standard input could not be read: {ex.Message}";
		}
	}

	/// <summary>
	/// Puts a file's contents into the fields a <c>seq</c> will send.
	/// </summary>
	/// <param name="path">The file to read.</param>
	/// <param name="fields">The request's fields, which gain <c>text</c>.</param>
	/// <returns>An error to show, or null when it worked.</returns>
	/// <remarks>
	/// Read as TEXT, not as raw bytes, so a capture written with the escapes a
	/// person can read - <c>\e[&lt;0;5;5M</c> - works as typed. A file of real
	/// bytes is sent with <c>--raw</c> and a hex dump, which is what a capture
	/// tool produces.
	/// </remarks>
	internal static string? Load(string path, Dictionary<string, JsonNode?> fields)
	{
		try
		{
			var info = new FileInfo(path);

			if (!info.Exists) return $"no such file: {path}";

			if (info.Length > MostBytes)
			{
				return $"{path} is {info.Length} bytes; a terminal sequence is not that long "
					+ $"(the limit is {MostBytes})";
			}

			// Trailing newline dropped: an editor adds one, and sending it would
			// deliver an Enter the capture never contained.
			string text = File.ReadAllText(path).TrimEnd('\n', '\r');

			fields["text"] = JsonValue.Create(text);
			return null;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
			or ArgumentException or NotSupportedException)
		{
			return $"{path} could not be read: {ex.Message}";
		}
	}
}

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
/// The last reply, and what to do with one too big to print.
/// </summary>
/// <remarks>
/// <para>
/// A screenshot is 75KB of base64 and a cells snapshot 51KB. Printed at a
/// prompt either one takes the scrollback with it, and neither says anything
/// on the way past. Above a threshold the prompt describes the reply instead
/// and keeps it here, where `.last` can print it, write it, or render it
/// another way.
/// </para>
/// <para>
/// Re-rendering rather than re-asking matters: by the time you decide you
/// wanted the cells formatted, the screen has moved on and the same question
/// has a different answer.
/// </para>
/// </remarks>
internal sealed class Held
{
	/// <summary>How much output goes to a terminal without being asked twice.</summary>
	/// <remarks>
	/// Eight kilobytes is about a screen and a half at eighty columns - enough
	/// that nothing ordinary trips it, little enough that nothing drowns you.
	/// </remarks>
	internal const int Threshold = 8 * 1024;

	/// <summary>The verb that produced it, for rendering it again.</summary>
	internal string Verb { get; private set; } = "";

	/// <summary>The reply itself.</summary>
	internal JsonNode? Reply { get; private set; }

	/// <summary>Keeps a reply, whether or not it was printed.</summary>
	internal void Keep(string verb, JsonNode reply)
	{
		Verb = verb;
		Reply = reply;
	}

	/// <summary>
	/// What to say instead of printing a reply this big.
	/// </summary>
	/// <param name="text">What would have been printed.</param>
	/// <returns>The summary, or null when it is small enough to print.</returns>
	internal string? TooBig(string text)
	{
		if (text.Length <= Threshold) return null;

		return $"{text.Length} characters, held.\n"
			+ "  .last show      print it anyway\n"
			+ "  .last > <file>  write it\n";
	}

	/// <summary>
	/// Writes a reply to a file.
	/// </summary>
	/// <param name="path">Where to write it.</param>
	/// <param name="append">Whether to add to the file rather than replace it.</param>
	/// <returns>What was written, to say so.</returns>
	/// <remarks>
	/// <para>
	/// A reply carrying <c>data</c> and an <c>encoding</c> is a file that
	/// happens to have travelled as JSON - a png, a tga, a jpeg - so what lands
	/// on disk is the DECODED bytes, and the file opens. Anything else is
	/// written as the protocol's own words, because a file with a formatter's
	/// output in it cannot be read back by anything.
	/// </para>
	/// <para>
	/// The reply says which format it is, not the filename: asking for a jpeg
	/// and writing it to shot.png gives you jpeg bytes in a file called png,
	/// and saying so is better than renaming someone's file or lying about it.
	/// </para>
	/// </remarks>
	internal string Write(string path, bool append)
	{
		if (Reply is not JsonObject body) throw CxdbgException.Usage("nothing to write yet.");

		string? encoded = body["data"]?.GetValue<string>();
		if (encoded != null && body["encoding"]?.GetValue<string>() is { } encoding)
		{
			if (!encoding.StartsWith("base64", StringComparison.OrdinalIgnoreCase))
				throw CxdbgException.Usage($"cannot decode '{encoding}'; use --json and decode it yourself.");

			byte[] bytes = Decode(encoded);

			if (append)
			{
				using var file = new FileStream(path, FileMode.Append);
				file.Write(bytes);
			}
			else
			{
				File.WriteAllBytes(path, bytes);
			}

			string format = body["format"]?.GetValue<string>() ?? "bytes";
			string named = Path.GetExtension(path).TrimStart('.');

			string note = named.Length > 0
				&& !named.Equals(format, StringComparison.OrdinalIgnoreCase)
				&& !(format == "jpeg" && named.Equals("jpg", StringComparison.OrdinalIgnoreCase))
					? $"  (it is {format}, whatever the name says)"
					: "";

			return $"wrote {bytes.Length} bytes to {path}{note}\n";
		}

		string text = Output.Json(Reply);

		if (append) File.AppendAllText(path, text);
		else File.WriteAllText(path, text);

		return $"wrote {text.Length} characters to {path}\n";
	}

	/// <summary>base64, in either of the spellings the protocol uses.</summary>
	private static byte[] Decode(string data) =>
		Convert.FromBase64String(data.Replace('-', '+').Replace('_', '/')
			.PadRight((data.Length + 3) / 4 * 4, '='));
}

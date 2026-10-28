// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Cxdbg.Protocol;

namespace Cxdbg.Commands;

/// <summary>
/// Asks the application a question and prints the answer, formatted.
/// </summary>
/// <remarks>
/// <c>cxdbg tree</c> and <c>cxdbg show tree</c> now differ only in emphasis:
/// both render through <see cref="Output"/>, and both take <c>--json</c> for
/// the reply as the protocol sent it. <c>show</c> remains because it reads as
/// a sentence, and because it is where someone looks first.
/// </remarks>
internal static class ShowCommand
{
	/// <summary>Runs <c>show &lt;verb&gt;</c>.</summary>
	internal static int Execute(CommandLine line)
	{
		if (line.Rest.Count == 0)
		{
			throw CxdbgException.Usage(
				"show needs something to show, e.g. `cxdbg show tree`. "
				+ "Run `cxdbg help` for the list.");
		}

		string verb = line.Rest[0];

		return CommandRunner.Run(line, client =>
			Output.Render(verb, client.Send(verb, line.Fields), line));
	}
}

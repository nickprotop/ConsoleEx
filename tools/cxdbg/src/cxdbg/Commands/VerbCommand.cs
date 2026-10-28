// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace Cxdbg.Commands;

/// <summary>
/// Sends one protocol verb and prints what came back.
/// </summary>
/// <remarks>
/// <para>
/// Every verb the protocol has is a cxdbg command, and so is every verb it
/// gains: nothing here enumerates them. An unknown first word is sent as a
/// verb, and an application that does not know it answers with the list it
/// does - which is better discovery than anything cxdbg could hard-code, and
/// means a newer application is never out of reach of an older debugger.
/// </para>
/// <para>
/// The fields are the protocol's own, parsed by
/// <see cref="CommandLineParser"/> - the same parser the prompt uses, so the
/// two cannot drift.
/// </para>
/// </remarks>
internal static class VerbCommand
{
	/// <summary>Sends the verb on the command line.</summary>
	internal static int Execute(CommandLine line) =>
		CommandRunner.Run(line, client =>
			Output.Render(line.Verb, client.Send(line.Verb, line.Fields), line));
}

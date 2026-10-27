// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Nodes;
using Cxdbg.Protocol;
using Spectre.Console;

namespace Cxdbg.Commands;

/// <summary>
/// The connect-ask-print-or-explain shape every reporting command shares.
/// </summary>
/// <remarks>
/// Exit codes: 0 success, 1 the application refused, 2 nothing to talk to,
/// 3 the command line was wrong. A caller scripting cxdbg can branch on those
/// without parsing English.
/// </remarks>
internal static class CommandRunner
{
	internal static int Run(SocketSettings settings, bool json, Func<DiagnosticsClient, string> body)
	{
		string? path = settings.SocketPath ?? SoleCandidate();

		if (string.IsNullOrEmpty(path))
		{
			AnsiConsole.MarkupLine(
				"[red]no socket given, and none found.[/] Pass --socket, or start the app with "
				+ "SHARPCONSOLEUI_DIAGNOSTICS_SOCKET set.");
			return 3;
		}

		try
		{
			using var client = DiagnosticsClient.Connect(path);
			Console.Write(body(client));
			return 0;
		}
		catch (CxdbgException ex)
		{
			AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");

			// 3 means cxdbg rejected the command line and sent nothing; 2 means
			// there was nothing to talk to; 1 means the application answered and
			// said no. A script branching on these needs the difference: only 1
			// says anything about the application.
			if (ex.Code == CxdbgException.BadUsage) return 3;
			return ex.Message.Contains("listening", StringComparison.Ordinal) ? 2 : 1;
		}
	}

	/// <summary>Runs a verb and prints its raw reply, for --json.</summary>
	internal static string Raw(JsonNode reply) =>
		reply.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

	/// <summary>The only socket on this machine, when there is exactly one.</summary>
	/// <remarks>
	/// Only when there is exactly one: guessing between several would attach to
	/// an app the user did not mean, silently.
	/// </remarks>
	private static string? SoleCandidate()
	{
		var found = SocketDiscovery.FindCandidates();
		return found.Count == 1 ? found[0] : null;
	}
}

/// <summary>Renders a reply nobody has written a formatter for yet.</summary>
internal static class Plain
{
	internal static string Render(JsonNode reply) =>
		reply.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
}

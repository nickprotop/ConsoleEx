// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using Cxdbg.Protocol;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cxdbg.Commands;

/// <summary>Settings shared by every command that talks to an application.</summary>
public class SocketSettings : CommandSettings
{
	[Description("Path to the application's diagnostics socket")]
	[CommandOption("-s|--socket <PATH>")]
	public string? SocketPath { get; set; }
}

/// <summary>Checks that an application is listening, and says which one.</summary>
public sealed class PingCommand : Command<SocketSettings>
{
	protected override int Execute(CommandContext context, SocketSettings settings, CancellationToken ct)
	{
		if (string.IsNullOrEmpty(settings.SocketPath))
		{
			AnsiConsole.MarkupLine("[red]--socket is required[/]");
			return 3;
		}

		try
		{
			using var client = DiagnosticsClient.Connect(settings.SocketPath);
			client.Send("ping");

			AnsiConsole.MarkupLine(
				$"[green]pong[/] from pid {client.Pid} (protocol {client.Protocol})");
			return 0;
		}
		catch (CxdbgException ex)
		{
			// The server's own message, not a flattened one: it knows why it
			// refused and that is the only part the user can act on.
			AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
			return ex.Code == null ? 2 : 1;
		}
	}
}

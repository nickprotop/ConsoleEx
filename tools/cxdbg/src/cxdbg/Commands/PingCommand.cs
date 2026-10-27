// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
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
			using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
			socket.Connect(new UnixDomainSocketEndPoint(settings.SocketPath));

			using var stream = new NetworkStream(socket, ownsSocket: false);
			using var reader = new StreamReader(stream, Encoding.UTF8);
			using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

			// The server greets first, with the protocol version and its pid.
			var hello = JsonNode.Parse(reader.ReadLine() ?? "")!;

			writer.WriteLine(new JsonObject { ["cmd"] = "ping" }.ToJsonString());
			var reply = JsonNode.Parse(reader.ReadLine() ?? "")!;

			if (reply["ok"]?.GetValue<bool>() != true)
			{
				AnsiConsole.MarkupLine($"[red]{reply["error"]?.GetValue<string>()}[/]");
				return 1;
			}

			AnsiConsole.MarkupLine(
				$"[green]pong[/] from pid {hello["pid"]?.GetValue<int>()}"
				+ $" (protocol {hello["protocol"]?.GetValue<int>()})");
			return 0;
		}
		catch (SocketException)
		{
			AnsiConsole.MarkupLine($"[red]nothing is listening at[/] {settings.SocketPath}");
			return 2;
		}
	}
}

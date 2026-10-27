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

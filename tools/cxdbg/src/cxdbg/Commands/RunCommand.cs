// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using Cxdbg.Launch;
using Cxdbg.Protocol;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cxdbg.Commands;

/// <summary>Settings for starting an application under cxdbg.</summary>
public class RunSettings : CommandSettings
{
	[Description("The application to start. Omit it with --again")]
	[CommandArgument(0, "[APP]")]
	public string? App { get; set; }

	[Description("Start the last application cxdbg ran, with the same arguments")]
	[CommandOption("--again")]
	public bool Again { get; set; }

	[Description("Run the app as a child of cxdbg rather than opening a terminal for it")]
	[CommandOption("--no-terminal")]
	public bool NoTerminal { get; set; }

	[Description("Seconds to wait for the application to open its socket")]
	[CommandOption("--timeout <SECONDS>")]
	public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Starts an application with a socket, instead of attaching to one.
/// </summary>
/// <remarks>
/// <para>
/// Everything after <c>--</c> belongs to the application:
/// <c>cxdbg run ./MyApp -- --headless --size 100x30</c>. cxdbg takes nothing
/// from that list and passes it through unchanged, so an app never has to know
/// it was started by a debugger.
/// </para>
/// <para>
/// cxdbg does not supervise what it starts: when run returns, the application
/// is still going. A debugger that killed its subject on exit would make every
/// session end by restarting the thing you were looking at.
/// </para>
/// </remarks>
public sealed class RunCommand : Command<RunSettings>
{
	protected override int Execute(CommandContext context, RunSettings settings, CancellationToken ct)
	{
		try
		{
			string app;
			List<string> appArgs;

			if (settings.Again)
			{
				// A restart is a NEW process at a new socket. Nothing is handed
				// back; the command is run again, which is the only thing that
				// honestly links the two.
				var last = LastRun.Require();
				app = last.App;
				appArgs = last.Args.ToList();
			}
			else
			{
				app = settings.App ?? throw CxdbgException.Usage(
					"cxdbg run needs an application, or --again to repeat the last one");
				appArgs = context.Remaining.Raw.ToList();
			}

			// A headless app has no screen to own, so it runs as a child of cxdbg
			// and its output lands here. Anything else needs a terminal of its
			// own: two programs drawing on one screen produce neither.
			bool headless = appArgs.Any(a =>
				a.Equals("--headless", StringComparison.OrdinalIgnoreCase));

			string? terminal = headless || settings.NoTerminal ? null : TerminalFinder.Find();

			if (terminal == null && !headless && !settings.NoTerminal)
				return HandOff(app, appArgs);

			var result = AppLauncher.Launch(
				app, appArgs, terminal,
				TimeSpan.FromSeconds(settings.TimeoutSeconds));

			LastRun.Remember(app, appArgs);

			Report(result);
			return 0;
		}
		catch (CxdbgException ex)
		{
			AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
			return ex.Code == CxdbgException.BadUsage ? 3 : 1;
		}
	}

	/// <summary>Says what started and how to reach it.</summary>
	private static void Report(LaunchResult result)
	{
		var console = AnsiConsole.Console;

		using var client = DiagnosticsClient.Connect(result.SocketPath);

		console.MarkupLine(result.Terminal == null
			? $"[green]started[/] pid {client.Pid}"
			: $"[green]started[/] pid {client.Pid} in {result.Terminal}");

		// Plain writes, not markup: Spectre wraps to the console width, and a
		// command line broken across two lines is one a person cannot paste.
		Console.WriteLine($"  socket  {result.SocketPath}");
		Console.WriteLine($"  ask     cxdbg show tree --socket {result.SocketPath}");
	}

	/// <summary>
	/// Prints the two lines to paste, when there is no terminal to open.
	/// </summary>
	/// <remarks>
	/// Over SSH, in a container, in CI there is no emulator, and a terminal app
	/// needs a terminal cxdbg cannot conjure. Refusing would be unhelpful and
	/// guessing would be worse, so cxdbg says exactly what to type - the
	/// variable and the command - and the user runs it where they do have a
	/// terminal.
	/// </remarks>
	private static int HandOff(string app, IReadOnlyList<string> appArgs)
	{
		string socket = Path.Combine(
			Path.GetTempPath(), "sharpconsoleui", $"cxdbg-{Guid.NewGuid():N}.sock");

		string command = string.Join(' ', new[] { app }.Concat(appArgs));

		var console = AnsiConsole.Console;
		console.MarkupLine("[yellow]no terminal emulator here[/], and this app wants a terminal.");
		console.WriteLine();
		console.MarkupLine("Run these two lines where you do have one:");
		console.WriteLine();
		Console.WriteLine($"  export SHARPCONSOLEUI_DIAGNOSTICS_SOCKET={socket}");
		Console.WriteLine($"  {command}");
		console.WriteLine();
		console.MarkupLine("The app will ask you to allow the socket. Then, back here:");
		console.WriteLine();
		Console.WriteLine($"  cxdbg show tree --socket {socket}");

		// Not a failure: cxdbg did the part it could do, and said what is left.
		return 0;
	}
}

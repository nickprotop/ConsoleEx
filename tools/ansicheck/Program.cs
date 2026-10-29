// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;

namespace AnsiCheck;

/// <summary>
/// An application that exists to be sent bytes.
/// </summary>
/// <remarks>
/// <para>
/// Headless, so there is no terminal to own raw mode or stdin, and a fixed
/// size, so a coordinate in the corpus means the same thing on every machine
/// and in CI. The socket is set in code rather than through the environment:
/// a property cannot be inherited from a shell profile, so it opens without
/// asking, which is what lets a script drive this unattended.
/// </para>
/// <para>
/// Deliberately not a demo. A real application's layout moves as it grows,
/// and then a corpus of coordinates fails for reasons that have nothing to do
/// with the decoder. One window, one size, nothing that redraws itself.
/// </para>
/// </remarks>
internal static class Program
{
	/// <summary>Columns, fixed so a coordinate means one thing everywhere.</summary>
	private const int Columns = 100;

	/// <summary>Rows, fixed for the same reason.</summary>
	private const int Rows = 30;

	private static int Main(string[] args)
	{
		string socket = args.Length > 0
			? args[0]
			: Path.Combine(Path.GetTempPath(), $"ansicheck-{Environment.ProcessId}.sock");

		if (File.Exists(socket)) File.Delete(socket);

		var driver = new HeadlessConsoleDriver(Columns, Rows);

		var system = new ConsoleWindowSystem(driver, ConsoleWindowSystemOptions.Create() with
		{
			DiagnosticsSocket = socket,

			// No panels: they offset the desktop, and a window reported at y 0
			// then has its title bar on screen row 1. Nothing here should need
			// that explained to it.
			ShowTopPanel = false,
			ShowBottomPanel = false,
		});

		new WindowBuilder(system)
			.WithTitle("ansicheck")
			.Maximized()
			.AddControl(Controls.Markup("send bytes to the socket").Build())
			.BuildAndShow();

		using var session = system.RunInBackground();

		// Printed so a script knows the socket is accepting rather than guessing
		// at a sleep. Standard output, because the UI is headless and owns no
		// terminal to corrupt.
		Console.WriteLine($"ansicheck listening {socket}");
		Console.Out.Flush();

		// Until it is killed. There is nothing for it to do on its own; every
		// frame it draws is one a sequence asked for.
		while (session.Fault == null) Thread.Sleep(100);

		Console.Error.WriteLine($"ansicheck faulted: {session.Fault}");
		return 1;
	}
}

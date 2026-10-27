// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using Cxdbg.Protocol;

namespace Cxdbg.Launch;

/// <summary>What a launch produced.</summary>
/// <param name="SocketPath">Where the application is listening.</param>
/// <param name="Process">The child, when cxdbg started it directly.</param>
/// <param name="Terminal">The emulator it was opened in, when one was used.</param>
internal sealed record LaunchResult(string SocketPath, Process? Process, string? Terminal);

/// <summary>
/// Starts an application with a diagnostics socket already set up.
/// </summary>
/// <remarks>
/// <para>
/// The workflow this removes: pick a socket path, export the variable, start
/// the app, find the socket it actually opened, attach. Five steps that are
/// the same five steps every time, and the fourth one is a guess when more
/// than one app is running.
/// </para>
/// <para>
/// cxdbg names the socket, so there is no guess: it knows the path before the
/// application exists.
/// </para>
/// </remarks>
internal static class AppLauncher
{
	/// <summary>
	/// Starts an application and waits for its socket to answer.
	/// </summary>
	/// <param name="app">The application to run.</param>
	/// <param name="appArgs">Arguments for the application itself.</param>
	/// <param name="terminal">
	/// An emulator to open it in, or null to run it as a child of cxdbg.
	/// </param>
	/// <param name="timeout">How long to wait for the socket.</param>
	/// <returns>Where it is listening, and the child when there is one.</returns>
	/// <exception cref="CxdbgException">
	/// When the application is not there, exits before listening, or never opens
	/// the socket.
	/// </exception>
	internal static LaunchResult Launch(
		string app,
		IReadOnlyList<string> appArgs,
		string? terminal = null,
		TimeSpan? timeout = null)
	{
		string resolved = Resolve(app);
		string socket = NewSocketPath();

		var psi = terminal == null
			? Direct(resolved, appArgs)
			: ViaTerminal(terminal, resolved, appArgs);

		psi.Environment["SHARPCONSOLEUI_DIAGNOSTICS_SOCKET"] = socket;
		psi.UseShellExecute = false;

		Process child;
		try
		{
			child = Process.Start(psi)
				?? throw CxdbgException.Usage($"could not start {resolved}");
		}
		catch (Exception ex) when (ex is not CxdbgException)
		{
			throw CxdbgException.Usage($"could not start {resolved}: {ex.Message}");
		}

		WaitForSocket(socket, child, resolved, timeout ?? TimeSpan.FromSeconds(30));

		// With an emulator the process cxdbg started is the emulator, not the
		// app - saying otherwise would hand back a pid that dies when the window
		// closes rather than when the app does.
		return new LaunchResult(socket, terminal == null ? child : null, terminal);
	}

	/// <summary>The application as a child of cxdbg, sharing its streams.</summary>
	/// <remarks>
	/// For a headless app this is right: it paints into a buffer, so its stdout
	/// carries only what it logs, and letting that reach the terminal is how a
	/// crash becomes visible. A terminal-driven app must NOT come through here;
	/// it would paint over cxdbg.
	/// </remarks>
	private static ProcessStartInfo Direct(string app, IReadOnlyList<string> appArgs)
	{
		var psi = new ProcessStartInfo(app);
		foreach (string a in appArgs) psi.ArgumentList.Add(a);
		return psi;
	}

	/// <summary>The application in its own terminal window.</summary>
	private static ProcessStartInfo ViaTerminal(
		string terminal, string app, IReadOnlyList<string> appArgs)
	{
		var psi = new ProcessStartInfo(terminal);
		foreach (string a in TerminalFinder.ArgsFor(terminal, app, appArgs))
			psi.ArgumentList.Add(a);

		return psi;
	}

	/// <summary>
	/// Waits for the application to listen, and says why when it does not.
	/// </summary>
	/// <remarks>
	/// Exiting is checked alongside the socket. Without that, an app that dies
	/// on a bad argument produces the full timeout and then "no socket", when
	/// what happened is on the line above in its own output.
	/// </remarks>
	private static void WaitForSocket(string socket, Process child, string app, TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow + timeout;

		while (DateTime.UtcNow < deadline)
		{
			if (File.Exists(socket)) return;

			if (child.HasExited && child.ExitCode != 0)
			{
				throw new CxdbgException(
					$"{Path.GetFileName(app)} exited with {child.ExitCode} before opening a "
					+ "socket. Its own output is above.");
			}

			Thread.Sleep(50);
		}

		// The process is still alive - it just has not listened yet. Say that,
		// and give the three reasons in the order they are actually likely,
		// starting with the one that is not the app's fault at all. An earlier
		// version led with "built before diagnostics sockets existed", which
		// reads as a verdict and was wrong every time a loaded machine was
		// simply slow.
		throw new CxdbgException(
			$"{Path.GetFileName(app)} is running but had not opened a socket after "
			+ $"{timeout.TotalSeconds:0}s. It may just be slow to start - try "
			+ "--timeout. Otherwise: an app with a terminal asks for consent on a "
			+ "screen cxdbg cannot see, and an app that never calls "
			+ "ConsoleWindowSystem will not open one at all.");
	}

	/// <summary>A socket path for this run, where discovery already looks.</summary>
	/// <remarks>
	/// Named by cxdbg rather than generated by the app, which is the whole point:
	/// the path is known before the application exists, so nothing has to be
	/// found afterwards.
	/// </remarks>
	private static string NewSocketPath()
	{
		string? runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

		string dir = string.IsNullOrEmpty(runtime)
			? Path.Combine(Path.GetTempPath(), "sharpconsoleui")
			: runtime;

		Directory.CreateDirectory(dir);
		return Path.Combine(dir, $"cxdbg-{Guid.NewGuid():N}.sock");
	}

	/// <summary>Finds the application, saying so when it is not there.</summary>
	private static string Resolve(string app)
	{
		if (File.Exists(app)) return Path.GetFullPath(app);

		throw CxdbgException.Usage(
			$"no application at '{app}'. Give the path to a built executable, "
			+ "e.g. Examples/DemoApp/bin/Debug/net10.0/DemoApp");
	}
}

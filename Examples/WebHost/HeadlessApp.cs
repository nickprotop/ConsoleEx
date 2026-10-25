// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;

namespace WebHost;

/// <summary>
/// Runs a SharpConsoleUI application with no terminal, alongside the web server.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ConsoleWindowSystem.RunInBackground"/> does the work: it owns the loop, runs it
/// on its own thread, and returns once a frame has been rendered - so by the time the
/// constructor finishes the socket is accepting connections and there is something to show.
/// ASP.NET keeps the thread it needs.
/// </para>
/// <para>
/// Headless is a requirement, not a convenience. A real driver would hold raw mode, the
/// alternate screen and stdin, which a browser cannot share; and
/// <c>NetConsoleDriver.ScreenSize</c> is read-only, so the browser could not set the
/// application's dimensions.
/// </para>
/// </remarks>
public sealed class HeadlessApp : IDisposable
{
	private readonly BackgroundSession _session;

	/// <summary>Where the application is listening.</summary>
	public string SocketPath { get; }

	/// <summary>The exception that ended the loop, if one did.</summary>
	public Exception? Fault => _session.Fault;

	/// <summary>
	/// Starts the application headless, and returns once it has painted.
	/// </summary>
	/// <param name="width">Initial columns.</param>
	/// <param name="height">Initial rows.</param>
	/// <param name="options">
	/// The application's own options - panels and their contents are fixed at construction,
	/// so they cannot be applied afterwards.
	/// </param>
	/// <param name="build">Builds the UI into the system.</param>
	public HeadlessApp(int width, int height, ConsoleWindowSystemOptions options,
		Action<ConsoleWindowSystem> build)
	{
		SocketPath = Path.Combine(Path.GetTempPath(), $"scui-webhost-{Environment.ProcessId}.sock");

		var driver = new HeadlessConsoleDriver(width, height);

		// The socket is requested in code, which does not prompt - a property cannot be
		// inherited from a shell profile the way the environment variable can, so it is
		// only ever set by someone writing it into the program. That is us, here.
		var system = new ConsoleWindowSystem(driver, options with
		{
			DiagnosticsSocket = SocketPath,
		});

		build(system);

		_session = system.RunInBackground();
	}

	public void Dispose()
	{
		_session.Dispose();

		try { if (File.Exists(SocketPath)) File.Delete(SocketPath); } catch { }
	}
}

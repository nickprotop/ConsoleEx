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
/// Runs a SharpConsoleUI application with no terminal, on a thread of its own.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HostedSession.Tick"/> must be called on the thread that called
/// <see cref="ConsoleWindowSystem.BeginHosted"/>, which is captured as the UI thread. ASP.NET
/// handles requests on pool threads, so the loop gets a dedicated thread and the web layer
/// never touches it directly - it goes through the diagnostics socket like any other client.
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
	private readonly Thread _thread;
	private readonly CancellationTokenSource _stopping = new();
	private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

	private HostedSession? _session;

	/// <summary>Where the application is listening.</summary>
	public string SocketPath { get; }

	/// <summary>Completes once the socket is accepting connections.</summary>
	public Task Ready => _ready.Task;

	/// <summary>
	/// Starts the application headless.
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

		_thread = new Thread(() => Run(width, height, options, build))
		{
			IsBackground = true,
			Name = "SharpConsoleUI UI thread",
		};
		_thread.Start();
	}

	private void Run(int width, int height, ConsoleWindowSystemOptions options,
		Action<ConsoleWindowSystem> build)
	{
		try
		{
			var driver = new HeadlessConsoleDriver(width, height);

			// The socket is requested in code, which does not prompt - a property cannot be
			// inherited from a shell profile the way the environment variable can, so it is
			// only ever set by someone writing it into the program. That is us, here.
			var system = new ConsoleWindowSystem(driver, options with
			{
				DiagnosticsSocket = SocketPath,
			});

			build(system);

			using var session = _session = system.BeginHosted();
			_ready.TrySetResult();

			while (!_stopping.IsCancellationRequested && session.Tick())
			{
			}
		}
		catch (Exception ex)
		{
			// Surface a startup failure to whoever awaits Ready, rather than leaving them
			// waiting on a thread that has already died.
			_ready.TrySetException(ex);
		}
	}

	public void Dispose()
	{
		_stopping.Cancel();

		// The loop checks the token once per frame, so it leaves promptly; the join is a
		// courtesy to let teardown finish before the process exits.
		_thread.Join(TimeSpan.FromSeconds(2));

		_session?.Dispose();
		_stopping.Dispose();

		try { if (File.Exists(SocketPath)) File.Delete(SocketPath); } catch { }
	}
}

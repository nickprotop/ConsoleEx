// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace Cxdbg.Tests;

/// <summary>
/// A real SharpConsoleUI application, running headless, with a socket.
/// </summary>
/// <remarks>
/// <para>
/// Every test in this suite talks to one of these rather than to a stub. A
/// debugger tested against a fake protocol proves only that the fake agrees
/// with itself, and the live socket has already caught two bugs the unit tests
/// could not - a verb reading a thread-local from the wrong thread, and an id
/// that resolved to nothing inside a container the walk could not enter.
/// </para>
/// <para>
/// The app is DemoApp with <c>--headless</c>, which honours the socket
/// environment variable without a prompt: there is no terminal, so there is
/// nobody to ask.
/// </para>
/// </remarks>
public sealed class LiveAppFixture : IDisposable
{
	private readonly Process _app;
	private bool _disposed;

	/// <summary>Where this app's socket lives. Unique per fixture.</summary>
	public string SocketPath { get; }

	/// <summary>Everything the app wrote to stdout, for diagnosing a failure.</summary>
	public string Output => _output.ToString();

	private readonly System.Text.StringBuilder _output = new();

	/// <summary>Starts a headless app and waits for its socket.</summary>
	/// <param name="demo">A demo window to open, or null for the launcher.</param>
	/// <param name="size">Screen size, as WxH.</param>
	public LiveAppFixture(string? demo = null, string size = "120x40")
	{
		SocketPath = Path.Combine(Path.GetTempPath(), $"cxdbg-test-{Guid.NewGuid():N}.sock");

		var psi = new ProcessStartInfo(DemoAppPath())
		{
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		psi.ArgumentList.Add("--headless");
		psi.ArgumentList.Add("--size");
		psi.ArgumentList.Add(size);
		if (demo != null)
		{
			psi.ArgumentList.Add("--demo");
			psi.ArgumentList.Add(demo);
		}
		psi.Environment["SHARPCONSOLEUI_DIAGNOSTICS_SOCKET"] = SocketPath;

		_app = Process.Start(psi)
			?? throw new InvalidOperationException("could not start DemoApp");

		_app.OutputDataReceived += (_, e) => { if (e.Data != null) _output.AppendLine(e.Data); };
		_app.ErrorDataReceived += (_, e) => { if (e.Data != null) _output.AppendLine(e.Data); };
		_app.BeginOutputReadLine();
		_app.BeginErrorReadLine();
		_app.StandardInput.Close();

		WaitForSocket();
	}

	/// <summary>
	/// Waits for the socket to appear, and says why if it does not.
	/// </summary>
	/// <remarks>
	/// Loudly, because the alternative is every later assertion failing with
	/// "connection refused" and the reader working backwards to an app that
	/// never started.
	/// </remarks>
	private void WaitForSocket()
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		while (DateTime.UtcNow < deadline)
		{
			if (File.Exists(SocketPath)) return;
			if (_app.HasExited)
			{
				throw new InvalidOperationException(
					$"DemoApp exited with {_app.ExitCode} before opening a socket.\n{Output}");
			}
			Thread.Sleep(50);
		}

		throw new TimeoutException($"no socket at {SocketPath} after 20s.\n{Output}");
	}

	/// <summary>Where the built DemoApp is, relative to the test assembly.</summary>
	private static string DemoAppPath()
	{
		// tools/cxdbg/tests/cxdbg.Tests/bin/<cfg>/<tfm>/ -> repo root
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Examples")))
			dir = dir.Parent;

		if (dir == null)
			throw new InvalidOperationException("could not find the repo root from " + AppContext.BaseDirectory);

		string path = Path.Combine(dir.FullName, "Examples", "DemoApp", "bin", "Debug", "net10.0", "DemoApp");
		if (!File.Exists(path))
			throw new InvalidOperationException($"DemoApp is not built at {path}; run dotnet build Examples/DemoApp");

		return path;
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		try { if (!_app.HasExited) _app.Kill(entireProcessTree: true); } catch { /* already gone */ }
		try { _app.WaitForExit(5000); } catch { /* best effort */ }
		try { if (File.Exists(SocketPath)) File.Delete(SocketPath); } catch { /* best effort */ }
		_app.Dispose();
	}
}

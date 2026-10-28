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

	/// <summary>The application's process id, as the socket reports it.</summary>
	public int Pid => _app.Id;

	/// <summary>Whether the application is still running.</summary>
	public bool IsRunning => !_app.HasExited;
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
		WaitForFirstFrame(demo);
	}

	/// <summary>
	/// Waits until the app has rendered and has an active window.
	/// </summary>
	/// <remarks>
	/// The socket opens at the end of the constructor, BEFORE the first frame
	/// and before a demo window is activated. A test that asks immediately gets
	/// "no active window" from a verb that photographs one, which reads like a
	/// broken verb and is really a race against startup.
	/// </remarks>
	private void WaitForFirstFrame(string? demo)
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		string? lastSeen = null;

		while (DateTime.UtcNow < deadline)
		{
			try
			{
				using var client = Protocol.DiagnosticsClient.Connect(SocketPath);
				var windows = client.Send("windows")["windows"]!.AsArray();

				foreach (var w in windows)
				{
					if (w?["active"]?.GetValue<bool>() != true) continue;

					lastSeen = w["title"]?.GetValue<string>();

					// With a demo requested, waiting for ANY active window is not
					// enough: the launcher is active first and the demo opens on
					// top of it a moment later. A test that proceeded on the
					// launcher would look for controls that are in the other
					// window.
					if (demo == null || lastSeen == demo) return;
				}
			}
			catch (Protocol.CxdbgException) { /* not up yet */ }

			Thread.Sleep(50);
		}

		throw new TimeoutException(
			$"waited 20s for {(demo == null ? "an active window" : $"'{demo}' to open")}; "
			+ $"active window is '{lastSeen}'.\n{Output}");
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

	/// <summary>The repo root, found by walking up from the test assembly.</summary>
	internal static DirectoryInfo RepoRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Examples")))
			dir = dir.Parent;

		return dir ?? throw new InvalidOperationException(
			"could not find the repo root from " + AppContext.BaseDirectory);
	}

	/// <summary>
	/// A built binary, in whichever configuration exists.
	/// </summary>
	/// <param name="what">What to say it is when nothing is found.</param>
	/// <param name="parts">Path to it under the repo root, with the configuration left out.</param>
	/// <remarks>
	/// Shared, because this was written three times - twice for the demo app
	/// and once for cxdbg - and fixing the hardcoded "Debug" in one of them
	/// left the others failing on CI exactly as before. Rule 1: if it appears
	/// in two files, it belongs in a helper.
	/// </remarks>
	internal static string BuiltBinary(string what, params string[] parts)
	{
		var root = RepoRoot();
		var tried = new List<string>();

		foreach (string cfg in Configurations())
		{
			var segments = new List<string> { root.FullName };
			segments.AddRange(parts[..^1]);
			segments.Add("bin");
			segments.Add(cfg);
			segments.Add("net10.0");
			segments.Add(parts[^1]);

			string candidate = Path.Combine([.. segments]);
			if (File.Exists(candidate)) return candidate;

			tried.Add(candidate);
		}

		throw new InvalidOperationException(
			$"{what} is not built at any of: {string.Join(", ", tried)}");
	}

	/// <summary>Build configurations to look in, this assembly's first.</summary>
	private static IEnumerable<string> Configurations()
	{
		// Taken from where this assembly was built rather than from a compile
		// symbol, so it stays right if the test project is ever built in a
		// configuration nobody anticipated.
		string here = AppContext.BaseDirectory;

		if (here.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
				StringComparison.Ordinal))
		{
			return ["Release", "Debug"];
		}

		return ["Debug", "Release"];
	}

	/// <summary>Where the built DemoApp is, relative to the test assembly.</summary>
	private static string DemoAppPath() =>
		BuiltBinary("DemoApp", "Examples", "DemoApp", "DemoApp");

	/// <summary>Stops the application, for tests about it going away.</summary>
	public void Kill()
	{
		try { if (!_app.HasExited) _app.Kill(entireProcessTree: true); } catch { /* already gone */ }
		try { _app.WaitForExit(5000); } catch { /* best effort */ }
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

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
/// A running cxdbg session, itself driven over a diagnostics socket.
/// </summary>
/// <remarks>
/// cxdbg's session is a SharpConsoleUI application like any other, so it opens
/// a socket like any other and can be read and driven like any other. That is
/// what makes these tests possible at all: the tool is tested through the same
/// interface it exists to provide.
/// </remarks>
public sealed class SessionFixture : IDisposable
{
	private readonly Process _cxdbg;
	private bool _disposed;

	/// <summary>The session's OWN socket, not the target's.</summary>
	public string SocketPath { get; }

	public SessionFixture(string targetSocket, string size = "120x40")
	{
		SocketPath = Path.Combine(Path.GetTempPath(), $"cxdbg-session-{Guid.NewGuid():N}.sock");

		var psi = new ProcessStartInfo(BinaryPath())
		{
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		psi.ArgumentList.Add("attach");
		psi.ArgumentList.Add("--socket");
		psi.ArgumentList.Add(targetSocket);
		psi.ArgumentList.Add("--headless");
		psi.ArgumentList.Add("--size");
		psi.ArgumentList.Add(size);

		// The session's own socket. A debugger that can be debugged is not a
		// trick here - it is how this is tested.
		psi.Environment["SHARPCONSOLEUI_DIAGNOSTICS_SOCKET"] = SocketPath;

		_cxdbg = Process.Start(psi) ?? throw new InvalidOperationException("could not start cxdbg");
		_cxdbg.StandardInput.Close();

		WaitForSocket();
		Settle();
	}

	/// <summary>What the session is showing, as text.</summary>
	public string Screen()
	{
		using var client = Protocol.DiagnosticsClient.Connect(SocketPath);
		var lines = client.Send("snapshot")["lines"]?.AsArray();

		return lines == null
			? ""
			: string.Join("\n", lines.Select(l => l?.GetValue<string>() ?? ""));
	}

	/// <summary>The target frame number the session is reporting.</summary>
	public long ReportedTargetFrame()
	{
		// Read from the session's own screen rather than by asking the target:
		// the claim under test is what the SESSION says, and asking the target
		// directly would pass even if the header never updated.
		foreach (string line in Screen().Split('\n'))
		{
			int at = line.IndexOf("frame ", StringComparison.OrdinalIgnoreCase);
			if (at < 0) continue;

			string rest = new string(line[(at + 6)..].TakeWhile(char.IsDigit).ToArray());
			if (long.TryParse(rest, out long n)) return n;
		}

		return -1;
	}

	/// <summary>Waits until the session reports a target frame past this one.</summary>
	public void WaitForTargetFrameBeyond(long frame, int seconds = 10)
	{
		var deadline = DateTime.UtcNow.AddSeconds(seconds);
		while (DateTime.UtcNow < deadline)
		{
			if (ReportedTargetFrame() > frame) return;
			Thread.Sleep(100);
		}
	}

	/// <summary>Presses a key in the session.</summary>
	public void Key(string spec)
	{
		using var client = Protocol.DiagnosticsClient.Connect(SocketPath);
		client.Send("key", new Dictionary<string, System.Text.Json.Nodes.JsonNode?> { ["spec"] = spec });
	}

	/// <summary>Resizes the session's headless screen.</summary>
	public void Resize(int w, int h)
	{
		using var client = Protocol.DiagnosticsClient.Connect(SocketPath);
		client.Send("size", new Dictionary<string, System.Text.Json.Nodes.JsonNode?>
		{
			["w"] = w,
			["h"] = h,
		});
	}

	/// <summary>Waits for the session to go round its loop a few times.</summary>
	/// <remarks>
	/// Input and polling are both asynchronous. The frame counter is the
	/// session saying it has been round since; a fixed sleep would be a bet on
	/// the machine's speed.
	/// </remarks>
	public void Settle(int frames = 3)
	{
		using var client = Protocol.DiagnosticsClient.Connect(SocketPath);
		long start = client.Send("frame")["frame"]!.GetValue<long>();
		var deadline = DateTime.UtcNow.AddSeconds(10);

		while (DateTime.UtcNow < deadline)
		{
			if (client.Send("frame")["frame"]!.GetValue<long>() >= start + frames) return;
			Thread.Sleep(50);
		}
	}

	private void WaitForSocket()
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		while (DateTime.UtcNow < deadline)
		{
			if (File.Exists(SocketPath)) return;
			if (_cxdbg.HasExited)
				throw new InvalidOperationException($"cxdbg attach exited with {_cxdbg.ExitCode}");

			Thread.Sleep(50);
		}

		throw new TimeoutException($"the session opened no socket at {SocketPath} within 20s");
	}

	private static string BinaryPath()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Examples")))
			dir = dir.Parent;

		return Path.Combine(dir!.FullName,
			"tools", "cxdbg", "src", "cxdbg", "bin", "Debug", "net10.0", "cxdbg");
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		try { if (!_cxdbg.HasExited) _cxdbg.Kill(entireProcessTree: true); } catch { }
		try { _cxdbg.WaitForExit(5000); } catch { }
		try { if (File.Exists(SocketPath)) File.Delete(SocketPath); } catch { }
		_cxdbg.Dispose();
	}
}

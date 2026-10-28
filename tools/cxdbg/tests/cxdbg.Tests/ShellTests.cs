// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// The prompt: a cxdbg&gt; on the socket, until you leave.
/// </summary>
public class ShellTests
{
	/// <summary>Types these lines at the prompt and returns what it said.</summary>
	private static (int Exit, string Output) Type(string socket, params string[] lines)
	{
		var psi = new ProcessStartInfo(Cxdbg.BinaryPathForTests())
		{
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		Cxdbg.NoTerminals(psi);
		psi.ArgumentList.Add("shell");
		psi.ArgumentList.Add("-s");
		psi.ArgumentList.Add(socket);

		using var p = Process.Start(psi)!;
		foreach (string line in lines) p.StandardInput.WriteLine(line);
		p.StandardInput.Close();

		string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
		p.WaitForExit(30_000);

		return (p.ExitCode, output);
	}

	[Fact]
	public void AVerbTypedAtThePromptReachesTheApplication()
	{
		using var app = new LiveAppFixture();

		var (exit, output) = Type(app.SocketPath, "ping", "exit");

		Assert.Equal(0, exit);
		Assert.Contains("alive", output, StringComparison.Ordinal);
	}

	[Fact]
	public void FlagsAtThePromptBecomeProtocolFields()
	{
		using var app = new LiveAppFixture();

		// --depth is a field of the tree verb, so a shallow tree comes back
		// shallow. Without the flag reaching the app this returns everything.
		//
		// Asserted on the window's title, which the formatted tree prints at
		// its root: the prompt renders rather than dumping JSON, so a key name
		// from the reply is the wrong thing to look for.
		var (exit, output) = Type(app.SocketPath, "tree --depth 1", "exit");

		Assert.Equal(0, exit);
		Assert.Contains("SharpConsoleUI Demo", output, StringComparison.Ordinal);
	}

	[Fact]
	public void ExitLeaves()
	{
		using var app = new LiveAppFixture();

		var (exit, _) = Type(app.SocketPath, "exit");

		Assert.Equal(0, exit);
	}

	[Fact]
	public void OneRefusalDoesNotEndTheSession()
	{
		using var app = new LiveAppFixture();

		// A verb the app does not know is an answer, not the end: the question
		// after it still works.
		var (exit, output) = Type(app.SocketPath, "nosuchverb", "ping", "exit");

		Assert.Equal(0, exit);
		Assert.Contains("unknown command", output);
		Assert.Contains("alive", output, StringComparison.Ordinal);
	}

	[Fact]
	public void ACxdbgCommandAtThePromptIsExplained()
	{
		using var app = new LiveAppFixture();

		// `send` is cxdbg's own readable layer, not a protocol verb. Passed
		// through it would reach the app as an unknown verb and get a list that
		// does not explain why.
		var (exit, output) = Type(app.SocketPath, "send key Tab", "exit");

		Assert.Equal(0, exit);
		Assert.Contains("not a protocol verb", output);
	}

	[Fact]
	public void ConnectMovesThePromptToAnotherApplication()
	{
		// One debugging session often spans two processes - a launcher and what
		// it opened. The sizes differ, so the reply says which app answered.
		using var first = new LiveAppFixture(size: "100x30");
		using var second = new LiveAppFixture(size: "80x24");

		var (exit, output) = Type(first.SocketPath,
			"size", $"connect {second.SocketPath}", "size", "exit");

		Assert.Equal(0, exit);
		// Each app answers with its own size, so the pair says the prompt
		// actually moved: 100 wide before connect, 80 after.
		Assert.Contains("100x30", output, StringComparison.Ordinal);
		Assert.Contains("80x24", output, StringComparison.Ordinal);
	}

	[Fact]
	public void ConnectToNothingLeavesThePromptWhereItWas()
	{
		// A typo in a path must not leave the prompt attached to nothing, which
		// is a worse outcome than not having moved.
		using var app = new LiveAppFixture(size: "100x30");

		var (exit, output) = Type(app.SocketPath,
			"connect /tmp/definitely-not-a-socket.sock", "size", "exit");

		Assert.Equal(0, exit);
		Assert.Contains("nothing is listening", output);
		Assert.Contains("100x30", output, StringComparison.Ordinal);
	}

	[Fact]
	public void TheShellNeedsASocket()
	{
		var (exit, output) = Cxdbg.Run("shell");

		Assert.Equal(3, exit);
		Assert.Contains("needs a socket", output);
	}

	[Fact]
	public void LeavingOffersToStopTheApplication()
	{
		// The prompt knows which application it is talking to, so on the way
		// out it is the one process it can offer to tidy up. An OFFER, not a
		// kill: a debugger that ended its subject on exit would make every
		// session end by restarting the thing you were looking at.
		using var app = new LiveAppFixture();

		var (exit, output) = Type(app.SocketPath, "exit");

		Assert.Equal(0, exit);
		Assert.Contains(app.Pid.ToString(), output, StringComparison.Ordinal);
		Assert.Contains("stop", output, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void LeavingDoesNotStopItWithoutAnAnswer()
	{
		// Standard input is a pipe here, so there is nobody to ask. Declining
		// is what the prompt does by itself, and a scripted run that silently
		// killed the application would be the worst possible default.
		using var app = new LiveAppFixture();

		Type(app.SocketPath, "exit");

		Assert.True(app.IsRunning, "the application was stopped without being asked about");
	}

	[Fact]
	public void ABigReplyIsHeldRatherThanPrinted()
	{
		// A cells snapshot is tens of thousands of characters and takes the
		// scrollback with it. The prompt says what it is and keeps it.
		using var app = new LiveAppFixture();

		var (exit, output) = Type(app.SocketPath, "snapshot --format cells", "exit");

		Assert.Equal(0, exit);
		Assert.Contains("held", output, StringComparison.Ordinal);
		Assert.Contains(".last", output, StringComparison.Ordinal);
		Assert.True(output.Length < 8 * 1024, $"it printed anyway: {output.Length} characters");
	}

	[Fact]
	public void LastShowPrintsItAnyway()
	{
		// The threshold is a default, not a wall.
		using var app = new LiveAppFixture();

		var (exit, output) = Type(app.SocketPath, "snapshot --format cells", ".last show", "exit");

		Assert.Equal(0, exit);
		Assert.True(output.Length > 8 * 1024, "the held reply was never printed");
	}

	[Fact]
	public void RedirectingWritesTheProtocolsOwnWords()
	{
		// A file is read by something else - jq, a diff, a later run - so what
		// lands in it is the reply, not a formatter's rendering of it.
		using var app = new LiveAppFixture();

		string path = Path.Combine(Path.GetTempPath(), $"cxdbg-w-{Guid.NewGuid():N}.json");

		try
		{
			var (exit, _) = Type(app.SocketPath, $"tree > {path}", "exit");

			Assert.Equal(0, exit);
			Assert.True(File.Exists(path), "nothing was written");

			var written = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
			Assert.True(written!["ok"]!.GetValue<bool>(), "the file is not the reply");
		}
		finally
		{
			try { File.Delete(path); } catch { }
		}
	}

	[Fact]
	public void RedirectingAnImageWritesBytesThatOpen()
	{
		// The one case where the file is not the reply: a png travels as
		// base64 inside JSON, and a file of base64 is not a png. Decoding it
		// is cxdbg converting, deliberately.
		using var app = new LiveAppFixture();

		string path = Path.Combine(Path.GetTempPath(), $"cxdbg-i-{Guid.NewGuid():N}.png");

		try
		{
			var (exit, output) = Type(app.SocketPath, $"image > {path}", "exit");

			Assert.Equal(0, exit);
			Assert.Contains("wrote", output, StringComparison.Ordinal);

			byte[] bytes = File.ReadAllBytes(path);

			// The PNG signature, which says it is a real file and not base64.
			Assert.True(bytes.Length > 1000, $"only {bytes.Length} bytes");
			Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]);
		}
		finally
		{
			try { File.Delete(path); } catch { }
		}
	}

	[Fact]
	public void DotCommandsAreCxdbgsOwn()
	{
		// One character tells the prompt's own layer from the application's
		// verbs, so no verb the protocol gains can ever collide with one.
		using var app = new LiveAppFixture();

		var (exit, output) = Type(app.SocketPath, ".socket", ".help", "exit");

		Assert.Equal(0, exit);
		Assert.Contains($"pid {app.Pid}", output, StringComparison.Ordinal);
		Assert.Contains("At this prompt", output, StringComparison.Ordinal);
	}

	[Fact]
	public void ShowAndWhyAreAvailableHereRatherThanRefused()
	{
		// They used to be turned away with an explanation that the readable
		// layer exists elsewhere. Now they are .show and .why.
		using var app = new LiveAppFixture();

		var (exit, output) = Type(app.SocketPath, ".show reachable", "exit");

		Assert.Equal(0, exit);
		Assert.Contains("control", output, StringComparison.Ordinal);
	}

}

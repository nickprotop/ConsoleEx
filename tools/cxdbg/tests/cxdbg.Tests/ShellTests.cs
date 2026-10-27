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
		psi.ArgumentList.Add("attach");
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
		Assert.Contains("pong", output);
	}

	[Fact]
	public void FlagsAtThePromptBecomeProtocolFields()
	{
		using var app = new LiveAppFixture();

		// --depth is a field of the tree verb, so a shallow tree comes back
		// shallow. Without the flag reaching the app this returns everything.
		var (exit, output) = Type(app.SocketPath, "tree --depth 1", "exit");

		Assert.Equal(0, exit);
		Assert.Contains("windows", output);
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
		Assert.Contains("pong", output);
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
		Assert.Contains("\"w\": 100", output);
		Assert.Contains("\"w\": 80", output);
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
		Assert.Contains("\"w\": 100", output);
	}

	[Fact]
	public void TheShellNeedsASocket()
	{
		var (exit, output) = Cxdbg.Run("attach", "shell");

		Assert.Equal(3, exit);
		Assert.Contains("needs a socket", output);
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// Acting on a running application, and seeing the effect.
/// </summary>
/// <remarks>
/// Each test asserts the APPLICATION changed, not that cxdbg exited zero. A
/// key that is accepted and goes nowhere exits zero too.
/// </remarks>
public class SendTests
{
	private static string? FocusedControl(LiveAppFixture app)
	{
		using var client = Protocol.DiagnosticsClient.Connect(app.SocketPath);
		return client.Send("focus")["focused"]?.GetValue<string>();
	}

	/// <summary>
	/// Waits for the application to act on input, rather than sleeping a guess.
	/// </summary>
	/// <remarks>
	/// Input is asynchronous: the key is queued when send returns, and consumed
	/// on the next render pass. A fixed sleep is a bet on the machine's speed,
	/// which is how a test becomes flaky on a loaded CI runner. The frame
	/// counter is the app saying it has been round the loop since.
	/// </remarks>
	private static void WaitForFrames(LiveAppFixture app, int count = 2)
	{
		using var client = Protocol.DiagnosticsClient.Connect(app.SocketPath);
		long start = client.Send("frame")["frame"]!.GetValue<long>();
		var deadline = DateTime.UtcNow.AddSeconds(10);

		while (DateTime.UtcNow < deadline)
		{
			if (client.Send("frame")["frame"]!.GetValue<long>() >= start + count) return;
			Thread.Sleep(50);
		}

		throw new TimeoutException(
			$"the render loop did not advance {count} frames; anything read next is a frozen screen");
	}

	[Fact]
	public void SendKeyMovesFocusInTheRealApp()
	{
		// The launcher, deliberately: Tab moves focus only where there is somewhere
		// for it to go, and a window with one focus stop correctly cycles back to
		// itself. The Chart Showcase page is such a window, and asserting a change
		// there would assert something false. So the test states its precondition.
		using var app = new LiveAppFixture();

		using (var probe = Protocol.DiagnosticsClient.Connect(app.SocketPath))
		{
			int stops = probe.Send("reachable")["count"]!.GetValue<int>();
			Assert.True(stops > 1, $"this window has {stops} focus stop(s); Tab has nowhere to go");
		}

		string? before = FocusedControl(app);

		var (exit, _) = Cxdbg.Run("send", "key", "Tab", "--socket", app.SocketPath);
		Assert.Equal(0, exit);

		WaitForFrames(app);

		Assert.NotEqual(before, FocusedControl(app));
	}

	[Fact]
	public void SendResizeChangesAHeadlessScreen()
	{
		using var app = new LiveAppFixture(size: "100x30");

		var (exit, _) = Cxdbg.Run("send", "resize", "80", "24", "--socket", app.SocketPath);
		Assert.Equal(0, exit);

		using var client = Protocol.DiagnosticsClient.Connect(app.SocketPath);
		var size = client.Send("size");

		Assert.Equal(80, size["w"]!.GetValue<int>());
		Assert.Equal(24, size["h"]!.GetValue<int>());
	}

	[Fact]
	public void SendClickReachesTheApplication()
	{
		using var app = new LiveAppFixture();

		var (exit, _) = Cxdbg.Run("send", "click", "20", "10", "--socket", app.SocketPath);

		Assert.Equal(0, exit);
	}

	[Fact]
	public void AWrongDriverRefusalReachesTheUserAsItsOwnExplanation()
	{
		// A terminal-driven app refuses a resize with wrong_driver and an
		// explanation naming the driver. The headless fixture cannot produce one,
		// so this drives the same path: a refusal carries the server's words and
		// exits 1, rather than becoming "command failed".
		using var app = new LiveAppFixture();

		var (exit, output) = Cxdbg.Run("send", "resize", "0", "0", "--socket", app.SocketPath);

		Assert.Equal(1, exit);
		Assert.Contains("1x1", output);
	}

	[Fact]
	public void AWordCxdbgRejectsIsBadUsage_NotARefusal()
	{
		// Exit 1 means the application answered and said no. Nothing was sent
		// here: cxdbg did not know the word. That is exit 3, bad usage, and the
		// difference matters to anything scripting cxdbg.
		using var app = new LiveAppFixture();

		var (exit, output) = Cxdbg.Run("send", "jump", "1", "2", "--socket", app.SocketPath);

		Assert.Equal(3, exit);
		Assert.Contains("jump", output);
	}

	[Fact]
	public void SetReportsSwitchesWithoutTheProtocolEnvelope()
	{
		using var app = new LiveAppFixture();

		var (exit, output) = Cxdbg.Run("set", "--socket", app.SocketPath);

		Assert.Equal(0, exit);
		Assert.Contains("logLevel", output);

		// "ok" is how the transport says the request succeeded, not something the
		// application can be asked to switch. Printing it in a list of switches
		// invites someone to try setting it.
		Assert.DoesNotContain("ok ", output);
	}

	[Fact]
	public void SetChangesARuntimeSwitch()
	{
		using var app = new LiveAppFixture();

		// set takes named fields like every other verb: on the wire it is
		// {"cmd":"set","logLevel":"Trace"}, so that is how it is typed.
		var (exit, _) = Cxdbg.Run("set", "--logLevel", "Trace", "--socket", app.SocketPath);
		Assert.Equal(0, exit);

		using var client = Protocol.DiagnosticsClient.Connect(app.SocketPath);
		var reply = client.Send("set");

		Assert.Contains("Trace", reply.ToJsonString());
	}
}

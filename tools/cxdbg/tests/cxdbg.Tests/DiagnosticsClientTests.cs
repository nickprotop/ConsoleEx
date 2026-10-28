// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Cxdbg.Protocol;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// Talking to a real application over its real socket.
/// </summary>
[Trait("Kind", "LiveApp")]
public class DiagnosticsClientTests
{
	[Fact]
	public void SendingAVerbReturnsItsReply()
	{
		using var app = new LiveAppFixture();
		using var client = DiagnosticsClient.Connect(app.SocketPath);

		var reply = client.Send("frame");

		Assert.True(reply["frame"]!.GetValue<int>() >= 0);
		Assert.True(client.Pid > 0);
	}

	[Fact]
	public void AFailedVerbThrowsWithItsCode()
	{
		// The server answers ok:false with a code and its own explanation. A
		// client that flattened that into "command failed" would throw away the
		// only part the user can act on.
		using var app = new LiveAppFixture();
		using var client = DiagnosticsClient.Connect(app.SocketPath);

		var ex = Assert.Throws<CxdbgException>(() => client.Send("nosuchverb"));

		Assert.Contains("nosuchverb", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void ALargeReplyIsReadWhole()
	{
		// `image` answers with a base64 PNG on ONE line, tens of kilobytes long.
		// A client reading a fixed buffer truncates it and the failure looks like
		// a corrupt image rather than a short read.
		// With a demo open, because `image` photographs a window and the launcher
		// has none - "no active window" is the server being right, not a short read.
		using var app = new LiveAppFixture(demo: "Chart Showcase");
		using var client = DiagnosticsClient.Connect(app.SocketPath);

		var reply = client.Send("image");
		string data = reply["data"]!.GetValue<string>();

		Assert.True(data.Length > 20_000, $"only {data.Length} characters came back");

		byte[] png = Convert.FromBase64String(
			data.Replace('-', '+').Replace('_', '/').PadRight((data.Length + 3) / 4 * 4, '='));

		Assert.Equal(0x89, png[0]);
		Assert.Equal((byte)'P', png[1]);
		Assert.Equal((byte)'N', png[2]);
		Assert.Equal((byte)'G', png[3]);
	}

	[Fact]
	public void TwoClientsCanBeConnectedAtOnce()
	{
		// The server's own comment says "one client at a time is deliberate", but
		// the accept loop hands every client to Task.Run and serves them all. Two
		// connections really are answered concurrently - verified against a live
		// app with socat as well as here.
		//
		// So cxdbg does not need to handle a "busy" refusal, and must not pretend
		// to: a live session holding a connection does not lock anyone out. The
		// library comment is wrong and is reported separately.
		using var app = new LiveAppFixture();
		using var first = DiagnosticsClient.Connect(app.SocketPath);
		Assert.True(first.Send("ping")["pong"]!.GetValue<bool>());

		using var second = DiagnosticsClient.Connect(app.SocketPath, TimeSpan.FromSeconds(5));
		Assert.True(second.Send("ping")["pong"]!.GetValue<bool>());

		// And the first is still usable afterwards.
		Assert.True(first.Send("ping")["pong"]!.GetValue<bool>());
	}

	[Fact]
	public void AStaleSocketFileSaysNobodyIsListening()
	{
		// A crashed app leaves the file behind. "Connection refused" is true and
		// useless; the path and the fact that nothing is there is what helps.
		string stale = Path.Combine(Path.GetTempPath(), $"cxdbg-stale-{Guid.NewGuid():N}.sock");
		File.WriteAllText(stale, "");

		try
		{
			var ex = Assert.Throws<CxdbgException>(() => DiagnosticsClient.Connect(stale));

			Assert.Contains(stale, ex.Message);
			Assert.Contains("listening", ex.Message, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			File.Delete(stale);
		}
	}

	[Fact]
	public void DiscoveryFindsALiveAppsSocket()
	{
		using var app = new LiveAppFixture();

		// The fixture puts its socket in the temp directory, which is one of the
		// two places an app's own generated path lands.
		var found = SocketDiscovery.FindCandidates(extraDirectories: new[] { Path.GetTempPath() });

		Assert.Contains(app.SocketPath, found);
	}
}

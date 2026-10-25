// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.DiagnosticsSocket;

/// <summary>
/// A request may carry an id, which the reply echoes, so a client can match the two once
/// replies stop arriving strictly in order.
/// </summary>
/// <remarks>
/// Driven over a real unix socket rather than by calling the dispatcher directly: the
/// framing and the JSON round trip are the part a client depends on.
/// </remarks>
public class SocketRequestIdTests : IDisposable
{
	private readonly string _socketPath;
	private readonly ConsoleWindowSystem _system;
	private readonly HostedSession _session;
	private readonly CancellationTokenSource _pump = new();

	public SocketRequestIdTests()
	{
		_socketPath = Path.Combine(Path.GetTempPath(), $"scui-id-{Guid.NewGuid():N}.sock");

		var driver = new HeadlessConsoleDriver(40, 10);
		_system = new ConsoleWindowSystem(driver,
			ConsoleWindowSystemOptions.Create() with { DiagnosticsSocket = _socketPath });

		new WindowBuilder(_system)
			.WithTitle("Id")
			.WithSize(20, 5)
			.Centered()
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("x").Build())
			.BuildAndShow();

		_session = _system.BeginHosted();

		// The socket's marshalling verbs need the UI loop running; pump it in the
		// background so a request cannot deadlock against a stopped loop.
		_ = Task.Run(() =>
		{
			while (!_pump.IsCancellationRequested && _session.Tick()) { }
		});

		WaitForSocket();
	}

	private void WaitForSocket()
	{
		for (int i = 0; i < 100 && !File.Exists(_socketPath); i++)
			Thread.Sleep(50);
	}

	/// <summary>Sends one request and returns the reply, skipping the hello banner.</summary>
	private JsonObject Ask(string request)
	{
		using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
		socket.Connect(new UnixDomainSocketEndPoint(_socketPath));

		using var stream = new NetworkStream(socket, ownsSocket: false);
		using var reader = new StreamReader(stream);
		using var writer = new StreamWriter(stream) { AutoFlush = true };

		reader.ReadLine();                   // hello banner, unsolicited and id-less
		writer.WriteLine(request);

		string? reply = reader.ReadLine();
		Assert.NotNull(reply);
		return JsonNode.Parse(reply!)!.AsObject();
	}

	[Fact]
	public void StringId_IsEchoed()
	{
		var reply = Ask("""{"id":"a7","cmd":"ping"}""");

		Assert.Equal("a7", reply["id"]!.GetValue<string>());
		Assert.True(reply["ok"]!.GetValue<bool>());
	}

	[Fact]
	public void NumericId_IsEchoed()
	{
		var reply = Ask("""{"id":42,"cmd":"ping"}""");

		Assert.Equal(42, reply["id"]!.GetValue<long>());
	}

	[Fact]
	public void NoId_LeavesTheReplyUnchanged()
	{
		// The compatibility promise: a client that never sends an id sees exactly what it
		// saw before the field existed.
		var reply = Ask("""{"cmd":"ping"}""");

		Assert.False(reply.ContainsKey("id"));
		Assert.True(reply["ok"]!.GetValue<bool>());
	}

	[Fact]
	public void IdIsEchoed_OnAFailedRequestToo()
	{
		// An error is the reply a client most needs to match to its request.
		var reply = Ask("""{"id":"e1","cmd":"nope"}""");

		Assert.Equal("e1", reply["id"]!.GetValue<string>());
		Assert.False(reply["ok"]!.GetValue<bool>());
	}

	[Fact]
	public void OverlongId_IsIgnored_ButTheRequestStillWorks()
	{
		// The value is reflected straight back, so an unbounded one would let a client
		// inflate a reply at the server's expense. Dropping the label is the right trade
		// against failing a request that is otherwise fine.
		string huge = new('x', 500);
		var reply = Ask($$"""{"id":"{{huge}}","cmd":"ping"}""");

		Assert.False(reply.ContainsKey("id"));
		Assert.True(reply["ok"]!.GetValue<bool>());
	}

	[Fact]
	public void ObjectId_IsIgnored_ButTheRequestStillWorks()
	{
		// An object would let a client choose the reply's shape.
		var reply = Ask("""{"id":{"nested":true},"cmd":"ping"}""");

		Assert.False(reply.ContainsKey("id"));
		Assert.True(reply["ok"]!.GetValue<bool>());
	}

	[Fact]
	public void MalformedRequest_HasNoIdToEcho()
	{
		// Documented gap: there is no id to read from text that did not parse.
		var reply = Ask("""{"id":"m1","cmd":""");

		Assert.False(reply.ContainsKey("id"));
		Assert.False(reply["ok"]!.GetValue<bool>());
	}

	public void Dispose()
	{
		_pump.Cancel();
		_session.Dispose();
		_pump.Dispose();
		try { if (File.Exists(_socketPath)) File.Delete(_socketPath); } catch { }
	}
}

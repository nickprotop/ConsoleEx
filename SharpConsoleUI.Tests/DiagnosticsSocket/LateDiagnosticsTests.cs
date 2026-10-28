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
/// Turning render diagnostics on over the socket, in an application that was
/// not built with them.
/// </summary>
/// <remarks>
/// <para>
/// The question a debugger asks is "what is this app doing", and the app is
/// already running by the time anyone asks. Diagnostics fixed at construction
/// meant the one app you most wanted to interrogate - a release build someone
/// is sitting in front of, misbehaving - was the one that could not answer, and
/// the only remedy was a restart, which loses the state you were looking at.
/// </para>
/// <para>
/// The cost that made it opt-in is per FRAME, not per object: every capture
/// path returns immediately on <c>IsEnabled</c> being false. So the recorder
/// always exists and the option decides whether it STARTS enabled, which keeps
/// an app that never asks exactly as cheap as before.
/// </para>
/// </remarks>
public class LateDiagnosticsTests : IDisposable
{
	private readonly string _socketPath;
	private readonly ConsoleWindowSystem _system;
	private readonly HostedSession _session;
	private readonly CancellationTokenSource _pump = new();

	public LateDiagnosticsTests()
	{
		_socketPath = Path.Combine(Path.GetTempPath(), $"scui-late-{Guid.NewGuid():N}.sock");

		var driver = new HeadlessConsoleDriver(40, 10);

		// EnableDiagnostics deliberately left at its default of false: this is
		// the app that previously could not be asked.
		_system = new ConsoleWindowSystem(driver,
			ConsoleWindowSystemOptions.Create() with { DiagnosticsSocket = _socketPath });

		new WindowBuilder(_system)
			.WithTitle("Late")
			.WithSize(20, 5)
			.Centered()
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("x").Build())
			.BuildAndShow();

		_session = _system.BeginHosted();

		_ = Task.Run(() =>
		{
			while (!_pump.IsCancellationRequested && _session.Tick()) { }
		});

		for (int i = 0; i < 100 && !File.Exists(_socketPath); i++) Thread.Sleep(50);
	}

	/// <summary>Sends one request and returns the reply, skipping the hello banner.</summary>
	private JsonObject Ask(string request)
	{
		using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
		socket.Connect(new UnixDomainSocketEndPoint(_socketPath));

		using var stream = new NetworkStream(socket, ownsSocket: false);
		using var reader = new StreamReader(stream);
		using var writer = new StreamWriter(stream) { AutoFlush = true };

		reader.ReadLine();
		writer.WriteLine(request);

		string? reply = reader.ReadLine();
		Assert.NotNull(reply);
		return JsonNode.Parse(reply!)!.AsObject();
	}

	[Fact]
	public void DiagnosticsStartOffWhenTheOptionIsOff()
	{
		// The promise to an app that never asks: nothing is recording.
		var reply = Ask("""{"cmd":"set"}""");

		Assert.True(reply["ok"]!.GetValue<bool>());
		Assert.False(reply["diagnostics"]!.GetValue<bool>());
	}

	[Fact]
	public void TheyCanBeTurnedOnOverTheSocket()
	{
		// The whole point: an app built without them can still be asked.
		var reply = Ask("""{"cmd":"set","diagnostics":true}""");

		Assert.True(reply["ok"]!.GetValue<bool>());
		Assert.True(reply["diagnostics"]!.GetValue<bool>());
		Assert.Contains("diagnostics", reply["changed"]!.ToJsonString(), StringComparison.Ordinal);
	}

	[Fact]
	public void TurningThemOnMakesMetricsAnswer()
	{
		// Reporting "on" while the verb that needs them still refuses would be
		// a switch that lights its own lamp. Asserted on what the switch is FOR.
		Ask("""{"cmd":"set","diagnostics":true}""");

		var deadline = DateTime.UtcNow.AddSeconds(10);
		JsonObject reply;

		do
		{
			reply = Ask("""{"cmd":"metrics"}""");
			if (reply["ok"]?.GetValue<bool>() == true) break;
			Thread.Sleep(100);
		}
		while (DateTime.UtcNow < deadline);

		Assert.True(reply["ok"]?.GetValue<bool>() == true,
			$"metrics still refused after diagnostics were enabled: {reply.ToJsonString()}");
	}

	[Fact]
	public void AndCanBeTurnedBackOff()
	{
		// A probe that cannot stop costs the app every frame from then on.
		Ask("""{"cmd":"set","diagnostics":true}""");

		var reply = Ask("""{"cmd":"set","diagnostics":false}""");

		Assert.False(reply["diagnostics"]!.GetValue<bool>());
	}

	[Fact]
	public void TurningThemOnStartsTheInputTapeToo()
	{
		// `set diagnostics:true` has to mean everything diagnostics covers.
		// The render recorder switched on and the input tape did not, so
		// `events` kept answering "not recording" and pointed at the very
		// command that had just been run.
		Ask("""{"cmd":"set","diagnostics":true}""");

		var reply = Ask("""{"cmd":"events"}""");

		Assert.True(reply["ok"]!.GetValue<bool>());
		Assert.True(reply["recording"]!.GetValue<bool>(),
			$"the tape is still off: {reply.ToJsonString()}");
	}

	[Fact]
	public void TurningThemOffStopsTheInputTape()
	{
		// And back: a tape that cannot be stopped costs every keystroke from
		// then on.
		Ask("""{"cmd":"set","diagnostics":true}""");
		Ask("""{"cmd":"set","diagnostics":false}""");

		Assert.False(Ask("""{"cmd":"events"}""")["recording"]!.GetValue<bool>());
	}

	public void Dispose()
	{
		_pump.Cancel();
		_session.Dispose();
		_pump.Dispose();
		try { if (File.Exists(_socketPath)) File.Delete(_socketPath); } catch { }
	}
}

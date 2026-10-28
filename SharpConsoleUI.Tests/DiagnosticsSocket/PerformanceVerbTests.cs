// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.DiagnosticsSocket;

/// <summary>
/// The performance verb, asked of a real application over a real socket.
/// </summary>
/// <remarks>
/// The whole path, not the ring in isolation: a window is built, the loop runs,
/// frames are sampled from the render branch, and the answer comes back over the
/// socket as JSON. The isolated ring tests cannot see the part that was actually
/// hard - that the frame interval must be sampled where the frame is RENDERED,
/// not where the loop ticks, because the loop ticks far more often and a rate
/// taken there is one nobody is seeing.
/// </remarks>
public class PerformanceVerbTests : IDisposable
{
	private readonly string _socket =
		Path.Combine(Path.GetTempPath(), $"scui-perf-{Guid.NewGuid():N}.sock");

	private JsonObject Ask(JsonObject request)
	{
		using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
		client.Connect(new UnixDomainSocketEndPoint(_socket));

		using var stream = new NetworkStream(client, ownsSocket: false);
		using var reader = new StreamReader(stream, Encoding.UTF8);
		using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

		reader.ReadLine();   // the greeting
		writer.WriteLine(request.ToJsonString());

		string line = reader.ReadLine() ?? throw new InvalidOperationException("no reply");

		return (JsonObject)JsonNode.Parse(line)!;
	}

	private ConsoleWindowSystem Start(int targetFps = 60)
	{
		var driver = new HeadlessConsoleDriver(80, 24);

		var system = new ConsoleWindowSystem(driver, ConsoleWindowSystemOptions.Create() with
		{
			DiagnosticsSocket = _socket,
			TargetFPS = targetFps,
			EnableFrameRateLimiting = true,
		});

		new WindowBuilder(system)
			.WithTitle("Perf")
			.WithSize(40, 8)
			.Centered()
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("measuring").Build())
			.BuildAndShow();

		return system;
	}

	/// <summary>Renders until the window holds frames, or gives up.</summary>
	private JsonObject AskAfterFrames(ConsoleWindowSystem system, int wanted)
	{
		for (int i = 0; i < 400; i++)
		{
			system.TickCore();

			if (i % 20 != 0) continue;

			var reply = Ask(new JsonObject { ["cmd"] = "performance" });

			if (reply["frames"]?.GetValue<int>() >= wanted) return reply;
		}

		return Ask(new JsonObject { ["cmd"] = "performance" });
	}

	[Fact]
	public void It_reports_the_rate_that_was_configured()
	{
		var system = Start(targetFps: 30);
		using var session = system.RunInBackground();

		var reply = AskAfterFrames(system, wanted: 2);

		Assert.True(reply["ok"]!.GetValue<bool>());
		Assert.Equal(30, reply["targetFps"]!.GetValue<int>());

		// 1000/30 is 33.33, not the 33 that integer division used to give - the
		// interval is derived from the rate and has to survive the derivation.
		Assert.Equal(33.33, reply["minFrameMs"]!.GetValue<double>(), 1);
	}

	[Fact]
	public void It_reports_frames_it_actually_rendered()
	{
		var system = Start();
		using var session = system.RunInBackground();

		var reply = AskAfterFrames(system, wanted: 2);

		Assert.True(reply["frames"]!.GetValue<int>() > 0,
			"the window should hold frames once the application has rendered");

		Assert.True(reply["achievedFps"]!.GetValue<double>() > 0,
			"a rendered frame means a measurable rate");
	}

	[Fact]
	public void It_says_so_rather_than_reporting_zero_before_anything_renders()
	{
		// A freshly started application has rendered nothing. A row of zeroes
		// there is indistinguishable from a stalled render loop, which is the one
		// thing someone asking this question most needs to tell apart.
		var system = Start();

		var reply = Ask(new JsonObject { ["cmd"] = "performance" });

		Assert.Equal(0, reply["frames"]!.GetValue<int>());
		Assert.NotNull(reply["note"]);
	}

	[Fact]
	public void Reset_empties_the_window_so_a_measurement_can_start_here()
	{
		var system = Start();
		using var session = system.RunInBackground();

		AskAfterFrames(system, wanted: 2);

		var reset = Ask(new JsonObject { ["cmd"] = "performance", ["reset"] = true });

		Assert.True(reset["reset"]!.GetValue<bool>());

		// Straight after a reset the window is empty again, whatever had
		// accumulated before.
		var after = Ask(new JsonObject { ["cmd"] = "performance" });
		Assert.Equal(0, after["frames"]!.GetValue<int>());
	}

	[Fact]
	public void Stage_costs_are_omitted_rather_than_reported_as_zero_when_unmeasured()
	{
		// The renderer records them only while diagnostics are on. Reporting a
		// cost of zero would read as "this stage is free" when it means nobody
		// measured it.
		var system = Start();
		using var session = system.RunInBackground();

		var reply = AskAfterFrames(system, wanted: 2);

		Assert.Null(reply["layoutMs"]);
		Assert.NotNull(reply["costNote"]);
	}

	public void Dispose()
	{
		try { if (File.Exists(_socket)) File.Delete(_socket); } catch { }
	}
}

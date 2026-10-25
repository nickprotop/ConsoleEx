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
/// The <c>image</c> verb renders in the format the caller asks for.
/// </summary>
/// <remarks>
/// Driven over a real unix socket, like <see cref="SocketRequestIdTests"/>: the dispatch,
/// the base64url framing and the reply's <c>format</c> field are what a client actually
/// depends on, and none of them are exercised by calling the renderer directly.
/// </remarks>
public class SocketImageFormatTests : IDisposable
{
	private readonly string _socketPath;
	private readonly ConsoleWindowSystem _system;
	private readonly HostedSession _session;
	private readonly CancellationTokenSource _pump = new();

	public SocketImageFormatTests()
	{
		_socketPath = Path.Combine(Path.GetTempPath(), $"scui-img-{Guid.NewGuid():N}.sock");

		var driver = new HeadlessConsoleDriver(40, 12);
		_system = new ConsoleWindowSystem(driver,
			ConsoleWindowSystemOptions.Create() with { DiagnosticsSocket = _socketPath });

		new WindowBuilder(_system)
			.WithTitle("Shot")
			.WithSize(24, 6)
			.Centered()
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("[green]hello[/] [red]world[/]").Build())
			.BuildAndShow();

		_session = _system.BeginHosted();

		_ = Task.Run(() =>
		{
			while (!_pump.IsCancellationRequested && _session.Tick()) { }
		});

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

		reader.ReadLine();                   // hello banner
		writer.WriteLine(request);

		string? reply = reader.ReadLine();
		Assert.NotNull(reply);
		return JsonNode.Parse(reply!)!.AsObject();
	}

	/// <summary>The reply of a request that must have succeeded.</summary>
	/// <remarks>
	/// The handler's fields are flattened onto the reply beside <c>ok</c>; there is no
	/// nested body object.
	/// </remarks>
	private JsonObject Body(string request)
	{
		var reply = Ask(request);
		Assert.True(reply["ok"]!.GetValue<bool>(), reply.ToJsonString());
		return reply;
	}

	/// <summary>The image bytes, undoing the base64url alphabet the reply uses.</summary>
	private static byte[] Data(JsonObject body)
	{
		string data = body["data"]!.GetValue<string>();
		return Convert.FromBase64String(data.Replace('-', '+').Replace('_', '/'));
	}

	[Fact]
	public void ImageVerb_DefaultsToPng()
	{
		// PNG is the default because it is the right one for everything but streaming,
		// and because it is the only format that is byte-identical run to run.
		var body = Body("""{"cmd":"image","glyphs":false}""");

		Assert.Equal("png", body["format"]!.GetValue<string>());

		byte[] bytes = Data(body);
		Assert.Equal(0x89, bytes[0]);
		Assert.Equal((byte)'P', bytes[1]);
	}

	[Fact]
	public void TheReplyNamesTheFormatItProduced()
	{
		// There is one image verb, so the reply is the only thing that says what came
		// back. A client picking a file extension or a decoder reads this field.
		foreach (string asked in new[] { "png", "tga", "jpeg" })
		{
			var body = Body($$"""{"cmd":"image","format":"{{asked}}","glyphs":false}""");
			Assert.Equal(asked, body["format"]!.GetValue<string>());
		}
	}

	[Fact]
	public void ThereIsNoPngVerb()
	{
		// Removed rather than kept as an alias: it shipped the same day as this verb, had
		// no external clients, and a second name for a subset of one verb is a thing to
		// keep accurate forever for no gain.
		var reply = Ask("""{"cmd":"png"}""");

		Assert.False(reply["ok"]!.GetValue<bool>());
		Assert.Contains("image", reply.ToJsonString(), StringComparison.Ordinal);
	}

	[Fact]
	public void ImageVerb_ProducesTga()
	{
		var body = Body("""{"cmd":"image","format":"tga","glyphs":false}""");

		Assert.Equal("tga", body["format"]!.GetValue<string>());

		// The reply's own geometry must match the file's header, or a client sizing a
		// canvas from the JSON would draw the frame wrong.
		byte[] bytes = Data(body);
		Assert.Equal(body["w"]!.GetValue<int>(), bytes[12] | (bytes[13] << 8));
		Assert.Equal(body["h"]!.GetValue<int>(), bytes[14] | (bytes[15] << 8));
	}

	[Fact]
	public void ImageVerb_ProducesJpeg()
	{
		var body = Body("""{"cmd":"image","format":"jpeg","glyphs":false}""");

		Assert.Equal("jpeg", body["format"]!.GetValue<string>());

		byte[] bytes = Data(body);
		Assert.Equal(0xFF, bytes[0]);
		Assert.Equal(0xD8, bytes[1]);
	}

	[Fact]
	public void ImageVerb_AcceptsJpgAsAnAlias()
	{
		var body = Body("""{"cmd":"image","format":"jpg","glyphs":false}""");
		Assert.Equal("jpeg", body["format"]!.GetValue<string>());
	}

	[Fact]
	public void ImageVerb_RejectsAnUnknownFormat()
	{
		// Named rather than silently falling back to PNG: a client asking for a format it
		// cannot have should hear so, not receive a different one.
		var reply = Ask("""{"cmd":"image","format":"webp","glyphs":false}""");

		Assert.False(reply["ok"]!.GetValue<bool>());
		Assert.Contains("webp", reply.ToJsonString(), StringComparison.Ordinal);
	}

	[Fact]
	public void ImageVerb_RejectsAQualityOutsideTheAllowedRange()
	{
		var reply = Ask("""{"cmd":"image","format":"jpeg","quality":0,"glyphs":false}""");

		Assert.False(reply["ok"]!.GetValue<bool>());
		Assert.Contains("quality", reply.ToJsonString(), StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void ImageVerb_LowerQualityProducesFewerBytes()
	{
		int big = Body("""{"cmd":"image","format":"jpeg","quality":95,"glyphs":false}""")
			["bytes"]!.GetValue<int>();
		int small = Body("""{"cmd":"image","format":"jpeg","quality":30,"glyphs":false}""")
			["bytes"]!.GetValue<int>();

		Assert.True(small < big, $"q30 gave {small} bytes, q95 gave {big}");
	}

	[Fact]
	public void TgaTradesSizeForSpeed_AndIsLargerThanPng()
	{
		// The trade the format exists to make, asserted on the half that is deterministic.
		// Encode TIME is the actual reason to choose TGA, but timing is not a thing to
		// assert on a shared CI machine, so the size relationship stands in for it.
		int png = Body("""{"cmd":"image","format":"png","glyphs":false}""")["bytes"]!.GetValue<int>();
		int tga = Body("""{"cmd":"image","format":"tga","glyphs":false}""")["bytes"]!.GetValue<int>();

		Assert.True(tga > png, $"expected tga ({tga}) to be larger than png ({png})");
	}

	[Fact]
	public void ImageIsAdvertised_AndPngIsNot()
	{
		var body = Body("""{"cmd":"commands"}""");
		string known = body["known"]!.ToJsonString();

		Assert.Contains("\"image\"", known, StringComparison.Ordinal);
		Assert.DoesNotContain("\"png\"", known, StringComparison.Ordinal);
	}

	public void Dispose()
	{
		_pump.Cancel();
		_session.Dispose();
		_pump.Dispose();
		try { if (File.Exists(_socketPath)) File.Delete(_socketPath); }
		catch (IOException) { /* the socket file is a courtesy, not a result */ }
	}
}

// -----------------------------------------------------------------------
// WebHost - serves a headless SharpConsoleUI application to a browser.
//
// The TUI runs with no terminal at all. Each frame is rendered to PNG and
// polled by the browser; clicks and keystrokes come back as real input. The
// web layer reaches the application ONLY through its diagnostics socket, so
// everything here would work just as well against an app in another process
// that someone else started.
//
// Run:  dotnet run --project Examples/WebHost
// Then: http://127.0.0.1:5088
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using WebHost;

const int Columns = 120;
const int Rows = 36;
const string Url = "http://127.0.0.1:5088";

// Start the TUI first: the web layer is useless without something to show, and
// a failure here should stop the process rather than serve a broken page.
// DemoApp unchanged and unaware it has no terminal: the same CreateOptions and
// Configure its own Main calls. Only the driver and the loop differ.
using var app = new HeadlessApp(
    Columns, Rows,
    DemoApp.Program.CreateOptions(),
    ws => DemoApp.Program.Configure(ws, args));

using var client = new DiagnosticsClient(app.SocketPath);

var builder = WebApplication.CreateBuilder(args);

// Quiet, not silent. The TUI is headless and this process owns its stdout, so
// writing here is safe - but ASP.NET's startup banner would bury the one line
// that matters.
builder.Logging.ClearProviders();
builder.WebHost.UseUrls(Url);

var web = builder.Build();
// Order matters: UseDefaultFiles rewrites "/" to "/index.html", and the
// rewrite has to happen before the static handler looks for a file.
web.UseDefaultFiles();
web.UseStaticFiles();

// --- The frame ------------------------------------------------------------
// "since" lets the server answer "nothing new" in a few bytes instead of
// re-sending an identical 34KB PNG. A TUI is idle between keystrokes, so this
// is the common case rather than an optimisation for a rare one.
web.MapGet("/frame", async (long? since, int? cellHeight, CancellationToken ct) =>
{
	var request = new JsonObject
	{
		["cmd"] = "image",
		["composed"] = true,
		["cellHeight"] = cellHeight ?? 16,
	};

	if (since is > 0) request["sinceFrame"] = since.Value;

	var reply = await client.AskAsync(request, ct);

	if (reply["ok"]?.GetValue<bool>() != true)
		return Results.Json(new { error = reply["error"]?.GetValue<string>() }, statusCode: 502);

	if (reply["unchanged"]?.GetValue<bool>() == true)
		return Results.Json(new { unchanged = true, frame = reply["frame"]!.GetValue<long>() });

	// base64url in, standard base64 out: the browser's atob() wants the latter,
	// and the socket uses the former because System.Text.Json escapes '+'.
	string data = reply["data"]!.GetValue<string>().Replace('-', '+').Replace('_', '/');

	return Results.Json(new
	{
		frame = reply["frame"]!.GetValue<long>(),
		width = reply["w"]!.GetValue<int>(),
		height = reply["h"]!.GetValue<int>(),
		cols = reply["cols"]!.GetValue<int>(),
		rows = reply["rows"]!.GetValue<int>(),
		cell = reply["cell"]!.GetValue<string>(),
		png = data,
	});
});

// --- Input ----------------------------------------------------------------
// Everything here goes through the same dispatch a real keypress or click
// takes, so hit-testing, focus and z-order behave exactly as they would in a
// terminal. A probe that reached past dispatch could report hits a user could
// never produce.
web.MapPost("/input", async (InputMessage message, CancellationToken ct) =>
{
	JsonObject request = message.Kind switch
	{
		// Fire and forget: the browser polls continuously, so it picks the result up on
		// the next frame. Nothing here waits for the UI, which is the right shape for a
		// client that is already watching the screen.
		"key" => new JsonObject
		{
			["cmd"] = "key",
			["spec"] = message.Spec ?? "",
		},
		"mouse" => new JsonObject
		{
			["cmd"] = "mouse",
			["x"] = message.X ?? 0,
			["y"] = message.Y ?? 0,
			["action"] = message.Action ?? "click",
			["button"] = message.Button ?? "left",
		},
		"resize" => new JsonObject
		{
			["cmd"] = "size",
			["w"] = message.X ?? Columns,
			["h"] = message.Y ?? Rows,
		},
		_ => new JsonObject(),
	};

	if (request.Count == 0)
		return Results.BadRequest(new { error = $"unknown input kind '{message.Kind}'" });

	var reply = await client.AskAsync(request, ct);

	// An input that failed is worth saying out loud: a silently dropped key
	// looks identical to a key the application chose to ignore.
	return reply["ok"]?.GetValue<bool>() == true
		? Results.Ok(new { ok = true })
		: Results.Json(new { error = reply["error"]?.GetValue<string>() }, statusCode: 502);
});

Console.WriteLine();
Console.WriteLine("  SharpConsoleUI WebHost");
Console.WriteLine();
Console.WriteLine($"  DemoApp is running headless, with no terminal, at {Columns}x{Rows} cells.");
Console.WriteLine($"  Open  {Url}  to see it and click around.");
Console.WriteLine();
Console.WriteLine("  Local only: anyone who can reach that port has full control of the app.");
Console.WriteLine("  Ctrl+C to stop.");
Console.WriteLine();

web.Run();

/// <summary>One browser event, before translation to a socket verb.</summary>
/// <param name="Kind">"key", "mouse" or "resize".</param>
/// <param name="Spec">For a key: tmux notation, such as "Down", "C-c" or "\"abc\"".</param>
/// <param name="X">Column, or width for a resize.</param>
/// <param name="Y">Row, or height for a resize.</param>
/// <param name="Action">For a mouse: click, press, release, move, wheelup, wheeldown.</param>
/// <param name="Button">For a mouse: left, right, middle.</param>
internal sealed record InputMessage(
	string Kind,
	string? Spec = null,
	int? X = null,
	int? Y = null,
	string? Action = null,
	string? Button = null);

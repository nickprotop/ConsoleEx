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
/// `why` — the cascades, against a real application.
/// </summary>
[Trait("Kind", "LiveApp")]
public class WhyTests
{
	/// <summary>An id from the live tree, for an unnamed control of a given type.</summary>
	private static string IdOfType(LiveAppFixture app, string type)
	{
		using var client = Protocol.DiagnosticsClient.Connect(app.SocketPath);
		var tree = client.Send("tree");

		// The ACTIVE window, not windows[0]: a demo opens on top of the launcher,
		// so the first entry is the one nobody is looking at.
		var active = tree["windows"]!.AsArray()
			.FirstOrDefault(w => w?["state"]?["active"]?.GetValue<bool>() == true)
			?? tree["windows"]!.AsArray()[0];

		string? found = null;
		void Walk(JsonNode? node)
		{
			if (found != null || node is not JsonObject o) return;

			if (o["id"]?.GetValue<string>() is { } id && id.StartsWith(type + "#"))
			{
				found = id;
				return;
			}

			if (o["children"] is JsonArray kids)
				foreach (var k in kids) Walk(k);
		}
		Walk(active!["node"]);

		return found ?? throw new InvalidOperationException(
			$"no unnamed {type} in '{active["title"]}'");
	}

	[Fact]
	public void WhySizeNamesTheContainerThatDecided()
	{
		using var app = new LiveAppFixture(demo: "Chart Showcase");
		string id = IdOfType(app, "ChartControl");

		var (exit, output) = Cxdbg.Run("why", "size", id, "--socket", app.SocketPath);

		Assert.Equal(0, exit);
		Assert.Contains("offered", output, StringComparison.OrdinalIgnoreCase);

		// The cascade, and a mark on the link that set the width. Without the
		// mark it is a list of numbers rather than an answer.
		Assert.Contains("Layout", output);
		Assert.Contains("sets width", output, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void WhyColorShowsTheStepsFromAnchorToColour()
	{
		using var app = new LiveAppFixture(demo: "Chart Showcase");

		var (exit, output) = Cxdbg.Run("why", "color", "Danger", "--state", "Disabled",
			"--socket", app.SocketPath);

		Assert.Equal(0, exit);
		Assert.Contains("DangerColor", output);
		Assert.Contains("disabled", output, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void WhyFrameAndWhyInputReportTheirOwnAbsence()
	{
		// DemoApp starts with diagnostics off, so both are empty - and must SAY
		// so. "No input happened" and "nothing was recording" are different
		// answers, and the second is the likelier one.
		using var app = new LiveAppFixture(demo: "Chart Showcase");

		var (frameExit, frameOut) = Cxdbg.Run("why", "frame", "--socket", app.SocketPath);
		var (inputExit, inputOut) = Cxdbg.Run("why", "input", "--socket", app.SocketPath);

		Assert.Equal(0, frameExit);
		Assert.Equal(0, inputExit);
		Assert.Contains("recording", inputOut, StringComparison.OrdinalIgnoreCase);
	}
}

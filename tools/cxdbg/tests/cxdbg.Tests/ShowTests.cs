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
/// `show` against a real application.
/// </summary>
/// <remarks>
/// Asserts on shape and content, never on exact spacing. A formatter test
/// that pins whitespace breaks on every cosmetic change and teaches nothing
/// about whether the output is right.
/// </remarks>
public class ShowTests
{
	[Fact]
	public void ShowTreePrintsAnIndentedTree()
	{
		using var app = new LiveAppFixture(demo: "Chart Showcase");

		var (exit, output) = Cxdbg.Run("show", "tree", "--socket", app.SocketPath);

		Assert.Equal(0, exit);
		Assert.Contains("Chart Showcase", output);

		// Indented children, and an id for something unnamed - the two things
		// that make the output navigable rather than a wall.
		Assert.Contains("\n  ", output);
		Assert.Matches(@"\w+Control#[0-9a-f]+", output);
	}

	[Fact]
	public void ShowTreeWithJsonPrintsTheRawReply()
	{
		using var app = new LiveAppFixture(demo: "Chart Showcase");

		var (exit, output) = Cxdbg.Run("show", "tree", "--json", "--socket", app.SocketPath);

		Assert.Equal(0, exit);

		var parsed = JsonNode.Parse(output);
		Assert.NotNull(parsed!["windows"]);
	}

	[Fact]
	public void ShowFocusAndReachableWork()
	{
		using var app = new LiveAppFixture(demo: "Chart Showcase");

		var (focusExit, _) = Cxdbg.Run("show", "focus", "--socket", app.SocketPath);
		var (reachExit, reachOut) = Cxdbg.Run("show", "reachable", "--socket", app.SocketPath);

		Assert.Equal(0, focusExit);
		Assert.Equal(0, reachExit);
		Assert.Contains("Control", reachOut);
	}

	[Fact]
	public void AnUnreachableSocketExitsTwoWithAReadableMessage()
	{
		var (exit, output) = Cxdbg.Run("show", "tree", "--socket", "/tmp/cxdbg-nope.sock");

		Assert.Equal(2, exit);
		Assert.Contains("listening", output, StringComparison.OrdinalIgnoreCase);
	}
}

/// <summary>
/// Every protocol verb is a cxdbg verb.
/// </summary>
/// <remarks>
/// The protocol has twenty-three verbs and will grow more. Hand-writing a
/// command class for each would drift the day one is added, so cxdbg accepts
/// any of them directly and asks the application itself which exist.
/// <para>
/// <c>show</c> and <c>why</c> are the readable layer on top, not the only way
/// in: <c>cxdbg tree</c> and <c>cxdbg show tree</c> reach the same verb, one
/// raw and one formatted.
/// </para>
/// </remarks>
public class VerbTests
{
	[Fact]
	public void AnyProtocolVerbWorksAsACommand()
	{
		using var app = new LiveAppFixture(demo: "Chart Showcase");

		// Verbs this app can actually answer. `metrics` is deliberately absent:
		// DemoApp runs without EnableDiagnostics, so refusing is correct and a
		// test that demanded success would be asserting a bug.
		foreach (string verb in new[] { "frame", "focus", "windows", "reachable", "commands" })
		{
			var (exit, output) = Cxdbg.Run(verb, "--socket", app.SocketPath);

			Assert.Equal(0, exit);
			Assert.False(string.IsNullOrWhiteSpace(output), $"'{verb}' printed nothing");
		}
	}

	[Fact]
	public void AVerbTakesTheProtocolsOwnOptions()
	{
		using var app = new LiveAppFixture(demo: "Chart Showcase");

		// The protocol's own field name, as the option: `depth` is what the tree
		// verb takes, so --depth is what cxdbg takes.
		// --json because a bare `tree` is formatted for reading; the point here is
		// that --depth reached the protocol, which the reply's shape shows.
		var (exit, output) = Cxdbg.Run("tree", "--depth", "2", "--json",
			"--socket", app.SocketPath);

		Assert.Equal(0, exit);

		var node = System.Text.Json.Nodes.JsonNode.Parse(output)!["windows"]!
			.AsArray()[0]!["node"];

		int Depth(System.Text.Json.Nodes.JsonNode? n, int d = 0)
		{
			if (n is not System.Text.Json.Nodes.JsonObject o) return d;
			int max = d;
			foreach (var c in o["children"]?.AsArray() ?? [])
				max = Math.Max(max, Depth(c, d + 1));
			return max;
		}

		Assert.Equal(2, Depth(node));
	}

	[Fact]
	public void AnUnknownVerbSaysWhichOnesExist()
	{
		// The server answers an unknown verb with the list it knows. That is the
		// discoverability this design relies on, so it must reach the user.
		using var app = new LiveAppFixture(demo: "Chart Showcase");

		var (exit, output) = Cxdbg.Run("nosuchverb", "--socket", app.SocketPath);

		Assert.NotEqual(0, exit);
		Assert.Contains("tree", output, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void VerbsAppearInHelp()
	{
		// Discoverable without an application to ask: --help lists them, so a
		// reader learns the surface before attaching to anything.
		var (exit, output) = Cxdbg.Run("--help");

		Assert.Equal(0, exit);
		Assert.Contains("tree", output, StringComparison.OrdinalIgnoreCase);
	}
}

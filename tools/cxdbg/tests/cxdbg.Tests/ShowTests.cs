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
[Trait("Kind", "LiveApp")]
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
[Trait("Kind", "LiveApp")]
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
	[Fact]
	public void HelpWorksAtEveryLevel()
	{
		// `cxdbg tree --help` asks about the verb, not the application: without
		// the check, --help is read as a protocol field and sent, which answers
		// a question nobody asked and needs a socket to do it.
		var (verbExit, verbHelp) = Cxdbg.Run("tree", "--help");

		Assert.Equal(0, verbExit);
		Assert.Contains("--depth", verbHelp, StringComparison.Ordinal);
		Assert.DoesNotContain("no socket", verbHelp, StringComparison.OrdinalIgnoreCase);

		// And cxdbg's own commands say what THEY take, rather than pointing at
		// the command that was just run.
		var (ownExit, ownHelp) = Cxdbg.Run("run", "--help");

		Assert.Equal(0, ownExit);
		Assert.Contains("--no-terminal", ownHelp, StringComparison.Ordinal);
	}

	[Fact]
	public void ControlAnswersAboutOneControl()
	{
		// The question four verbs could not answer: what IS this control.
		// Before it, reading a control's state meant fetching the whole tree
		// with props and walking it client-side.
		using var app = new LiveAppFixture();

		var (exit, output) = Cxdbg.Run(
			"control", "--control", "BodyMarker", "--socket", app.SocketPath);

		Assert.Equal(0, exit);
		Assert.Contains("BodyMarker", output, StringComparison.Ordinal);

		// Its size, and what contains it - the part a tree node alone cannot
		// carry. Asserted on the facts, not on field names: the reply is
		// rendered, so "bounds" reads as "got 88x28 at 0,0".
		Assert.Contains("wants", output, StringComparison.Ordinal);
		Assert.Contains("got", output, StringComparison.Ordinal);
		Assert.Contains("inside", output, StringComparison.Ordinal);
	}

	[Fact]
	public void ControlSaysSoWhenThereIsNoSuchControl()
	{
		// A typo and a control that is really absent read the same otherwise,
		// and the first is likelier.
		using var app = new LiveAppFixture();

		var (exit, output) = Cxdbg.Run(
			"control", "--control", "NoSuchControl", "--socket", app.SocketPath);

		Assert.Equal(1, exit);
		Assert.Contains("NoSuchControl", output, StringComparison.Ordinal);
		Assert.Contains("tree", output, StringComparison.Ordinal);
	}

	[Fact]
	public void InspectAnswersWhatIsUnderAPoint()
	{
		// The inspector's question: you have coordinates, not a name, because
		// you are looking at a screenshot and something is wrong there.
		using var app = new LiveAppFixture();

		var (exit, output) = Cxdbg.Run(
			"inspect", "--x", "20", "--y", "10", "--socket", app.SocketPath);

		Assert.Equal(0, exit);
		Assert.Contains("at 20,10", output, StringComparison.Ordinal);
		Assert.Contains("region", output, StringComparison.Ordinal);
	}

	[Fact]
	public void InspectSaysWhenAPointIsWindowChrome()
	{
		// The answer to "my click does nothing here". A point on a border
		// looks exactly like a point on a control that ignored the click, and
		// only this distinguishes them.
		using var app = new LiveAppFixture();

		// The window's own top-left corner, which is its frame.
		var (exit, output) = Cxdbg.Run(
			"inspect", "--x", "0", "--y", "1", "--socket", app.SocketPath);

		Assert.Equal(0, exit);
		Assert.Contains("chrome", output, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void WhatInspectPrintsCanBeHandedBackToControl()
	{
		// The contract behind printing ids instead of types: every name in the
		// chain is something another verb will find. Printed types read fine
		// and went nowhere.
		using var app = new LiveAppFixture();

		var (_, inspected) = Cxdbg.Run(
			"inspect", "--x", "20", "--y", "10", "--socket", app.SocketPath);

		// An id from the "inside" chain, which is the line this is about.
		var id = System.Text.RegularExpressions.Regex.Match(inspected, @"(\w+#[0-9a-f]+)");
		Assert.True(id.Success, $"nothing to hand back in:\n{inspected}");

		var (exit, output) = Cxdbg.Run(
			"control", "--control", id.Groups[1].Value, "--socket", app.SocketPath);

		Assert.Equal(0, exit);
		Assert.Contains(id.Groups[1].Value, output, StringComparison.Ordinal);
	}

	[Fact]
	public void NoColourReachesAPipe()
	{
		// Colour goes to a terminal and nowhere else: in a file it breaks grep
		// and diff, and a reply redirected with `>` has to stay machine
		// readable. The formatter tests call the renderer directly and so
		// never exercise this decision - only driving the real binary does.
		using var app = new LiveAppFixture();

		var (exit, output) = Cxdbg.Run("show", "tree", "--depth", "1", "--socket", app.SocketPath);

		Assert.Equal(0, exit);
		Assert.DoesNotContain("\u001b[", output, StringComparison.Ordinal);

		// And it is still a tree, not plain indentation.
		Assert.Contains("\u2514\u2500\u2500", output, StringComparison.Ordinal);
	}

}

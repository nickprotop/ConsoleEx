// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Cxdbg.Commands;
using Cxdbg.Protocol;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// One parser, for the command line and the prompt alike.
/// </summary>
/// <remarks>
/// These are the rules a person types against. They were two parsers once, and
/// they disagreed in both directions - which is the failure this file exists to
/// make impossible.
/// </remarks>
public class CommandLineTests
{
	[Fact]
	public void AFieldAndItsValueBecomeAProtocolField()
	{
		var line = CommandLineParser.Parse(["tree", "--depth", "2"]);

		Assert.Equal("tree", line.Verb);
		Assert.Equal(2, line.Fields["depth"]!.GetValue<long>());
	}

	[Fact]
	public void ABareFieldIsTrue()
	{
		// --props is {"props":true}, the way a flag reads.
		var line = CommandLineParser.Parse(["tree", "--props"]);

		Assert.True(line.Fields["props"]!.GetValue<bool>());
	}

	[Fact]
	public void ValuesAreTypedAsTheProtocolWouldHaveThem()
	{
		var line = CommandLineParser.Parse(
			["snapshot", "--y", "7", "--composed", "true", "--format", "cells"]);

		Assert.Equal(7, line.Fields["y"]!.GetValue<long>());
		Assert.True(line.Fields["composed"]!.GetValue<bool>());
		Assert.Equal("cells", line.Fields["format"]!.GetValue<string>());
	}

	[Fact]
	public void TheSnapshotLineFromTheReadmeParses()
	{
		// The one that used to need --arg on the command line and broke at the
		// prompt. It is now the same seven words in both places.
		var line = CommandLineParser.Parse(
			["snapshot", "--format", "cells", "--y", "7", "--h", "1"]);

		Assert.Equal("snapshot", line.Verb);
		Assert.Equal("cells", line.Fields["format"]!.GetValue<string>());
		Assert.Equal(7, line.Fields["y"]!.GetValue<long>());
		Assert.Equal(1, line.Fields["h"]!.GetValue<long>());
		Assert.DoesNotContain("arg", line.Fields.Keys);
	}

	[Fact]
	public void SetTakesItsSwitchesLikeEveryOtherVerb()
	{
		// On the wire set is {"cmd":"set","logLevel":"Trace"} - named fields,
		// like everything else. Its old two-bare-word form was a second mental
		// model for no gain, and could only ever send one switch.
		var line = CommandLineParser.Parse(
			["set", "--diagnostics", "true", "--logLevel", "Trace"]);

		Assert.True(line.Fields["diagnostics"]!.GetValue<bool>());
		Assert.Equal("Trace", line.Fields["logLevel"]!.GetValue<string>());
	}

	[Fact]
	public void AValueMayStartWithADashWhenAttached()
	{
		// --y -1 would read -1 as another option, so there is the attached form.
		var line = CommandLineParser.Parse(["snapshot", "--y=-1"]);

		Assert.Equal(-1, line.Fields["y"]!.GetValue<long>());
	}

	[Fact]
	public void CxdbgsOwnOptionsAreNotProtocolFields()
	{
		var line = CommandLineParser.Parse(
			["tree", "--depth", "2", "-s", "/tmp/app.sock", "--json"]);

		Assert.Equal("/tmp/app.sock", line.Socket);
		Assert.True(line.Json);
		Assert.Equal(["depth"], line.Fields.Keys);
	}

	[Fact]
	public void AFieldBeforeCxdbgsOwnOptionIsStillABareFlag()
	{
		// --props --json must not swallow --json as the value of props.
		var line = CommandLineParser.Parse(["tree", "--props", "--json"]);

		Assert.True(line.Fields["props"]!.GetValue<bool>());
		Assert.True(line.Json);
	}

	[Fact]
	public void EverythingAfterTheSeparatorBelongsToAnApplication()
	{
		var line = CommandLineParser.Parse(
			["run", "./MyApp", "--attach", "--", "--headless", "--size", "80x24"]);

		Assert.Equal(["./MyApp"], line.Rest);
		Assert.True(line.Fields["attach"]!.GetValue<bool>());
		Assert.Equal(["--headless", "--size", "80x24"], line.AppArgs);
	}

	[Fact]
	public void AnOptionWithoutItsValueIsRefused()
	{
		var ex = Assert.Throws<CxdbgException>(
			() => CommandLineParser.Parse(["tree", "--socket"]));

		Assert.Contains("needs a value", ex.Message, StringComparison.Ordinal);
	}
}

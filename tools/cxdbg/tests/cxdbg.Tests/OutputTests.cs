// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Cxdbg.Commands;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// Turning a reply into something to read.
/// </summary>
/// <remarks>
/// A formatter is chosen by the SHAPE of the reply, not from a list of verbs,
/// so a verb the protocol gains tomorrow is rendered sensibly by a build that
/// has never heard of it. These pin the shapes.
/// </remarks>
public class OutputTests
{
	private static string Render(string json, string verb = "x") =>
		Output.Pretty(verb, JsonNode.Parse(json)!);

	[Fact]
	public void AFrozenClockIsCalledFrozen()
	{
		// "kind": "fake" is how the wire says it; a reader wants the state, and
		// wants to spot it among three lines without reading carefully.
		string text = Render(
			"""{"ok":true,"clock":"mouse","kind":"fake","now":"2026-01-01T12:00:00.0000000Z"}""",
			"clock");

		Assert.Contains("mouse", text);
		Assert.Contains("frozen", text);
		Assert.Contains("12:00:00", text);
	}

	[Fact]
	public void AllThreeClocksReadAsThreeLines()
	{
		string text = Render(
			"""
			{"ok":true,"clocks":{
			  "mouse":{"clock":"mouse","kind":"fake","now":"2026-01-01T12:00:00.0000000Z"},
			  "repaint":{"clock":"repaint","kind":"system","now":"2026-01-01T12:00:01.0000000Z"},
			  "animation":{"clock":"animation","kind":"system","now":"2026-01-01T12:00:02.0000000Z"}}}
			""",
			"clock");

		Assert.Equal(3, text.TrimEnd().Split('\n').Length);
		Assert.Contains("frozen", text);
		Assert.Contains("system", text);
	}

	[Fact]
	public void SettledReadsAsAWordNotABoolean()
	{
		// "settled: True" makes a reader translate. The word is the answer.
		string text = Render("""{"ok":true,"settled":true,"frame":1204,"blockers":null}""",
			"settled");

		Assert.Contains("settled", text);
		Assert.Contains("1204", text);
		Assert.DoesNotContain("True", text);
	}

	[Fact]
	public void NotSettledSaysWhatIsHoldingIt()
	{
		// A bare "working" gives a script that times out nothing to report.
		string text = Render(
			"""{"ok":true,"settled":false,"frame":1204,"blockers":"2 window(s) with work pending"}""",
			"settled");

		Assert.Contains("working", text);
		Assert.Contains("2 window(s) with work pending", text);
	}

	[Fact]
	public void OneAnswerIsOneLine()
	{
		// {"ok":true,"frame":5176} as four lines of JSON is noise around a
		// number.
		string text = Render("""{"ok":true,"frame":5176}""");

		Assert.Equal("frame 5176\n", text);
	}

	[Fact]
	public void TheEnvelopeIsNotPartOfTheAnswer()
	{
		// ok:true says the call worked, which the absence of an error already
		// said. It is never a row.
		Assert.DoesNotContain("ok", Render("""{"ok":true,"w":100,"h":30}"""),
			StringComparison.Ordinal);
	}


	[Fact]
	public void RecordsBecomeATable()
	{
		string text = Render("""
		{"ok":true,"count":2,"stops":[
			{"control":"ButtonControl","type":"ButtonControl"},
			{"control":"DropdownControl","type":"DropdownControl"}]}
		""");

		// A header from the records' own keys, and a numbered row each.
		Assert.Contains("control", text, StringComparison.Ordinal);
		Assert.Contains("1  ButtonControl", text, StringComparison.Ordinal);
		Assert.Contains("2  DropdownControl", text, StringComparison.Ordinal);
	}

	[Fact]
	public void WhatTheReplySaidBesideTheListIsAHeading()
	{
		string text = Render("""
		{"ok":true,"window":"Settings","stops":[{"control":"ButtonControl"}]}
		""");

		Assert.StartsWith("window Settings", text, StringComparison.Ordinal);
	}

	[Fact]
	public void PlainValuesAreAListNotATable()
	{
		// `commands` answers with an array of verb NAMES. A table of one
		// unnamed column is a column of waste; they read as a paragraph.
		string text = Render("""{"ok":true,"known":["tree","focus","ping"]}""");

		Assert.Contains("tree, focus, ping", text, StringComparison.Ordinal);
	}


	[Fact]
	public void ARefusalReadsAsOne()
	{
		// Burying the message in an object with ok:false makes a refusal read
		// like output.
		string text = Render(
			"""{"ok":false,"code":"diagnostics_disabled","error":"no metrics yet"}""");

		Assert.StartsWith("no metrics yet", text, StringComparison.Ordinal);
		Assert.Contains("diagnostics_disabled", text, StringComparison.Ordinal);
	}

	[Fact]
	public void AHintIsKeptBecauseItSaysWhatToDoNext()
	{
		string text = Render(
			"""{"ok":false,"error":"not recording","hint":"turn it on with set"}""");

		Assert.Contains("turn it on with set", text, StringComparison.Ordinal);
	}

	[Fact]
	public void AnImageIsDescribedRatherThanPrinted()
	{
		// 75KB of base64 destroys a terminal's scrollback and tells you
		// nothing. The metadata says what was captured.
		string text = Output.Pretty("image", JsonNode.Parse(
			"""{"ok":true,"w":704,"h":448,"format":"png","bytes":75610,"data":"iVBOR"}""")!);

		Assert.Contains("75610", text, StringComparison.Ordinal);
		Assert.Contains("704x448", text, StringComparison.Ordinal);
		Assert.DoesNotContain("iVBOR", text, StringComparison.Ordinal);
	}

	[Fact]
	public void JsonIsTheProtocolsOwnWords()
	{
		// The escape hatch: whatever a formatter decides, --json is what came
		// off the socket.
		string text = Output.Json(JsonNode.Parse("""{"ok":true,"frame":7}""")!);

		Assert.Contains("\"frame\": 7", text, StringComparison.Ordinal);
		Assert.Contains("\"ok\": true", text, StringComparison.Ordinal);
	}
	[Fact]
	public void AVerbWhoseAnswerIsASentenceGetsOne()
	{
		// "pong true" says less than the word it stands for, and "w 100 / h 30"
		// is two facts where the pair is one.
		Assert.Equal("alive\n", Output.Pretty("ping", JsonNode.Parse("""{"ok":true,"pong":true}""")!));
		Assert.Equal("100x30\n", Output.Pretty("size", JsonNode.Parse("""{"ok":true,"w":100,"h":30}""")!));
		Assert.Equal("sent 1 key\n", Output.Pretty("key", JsonNode.Parse("""{"ok":true,"sent":1}""")!));
	}

	[Fact]
	public void AListSaysWhichFieldItCameFrom()
	{
		// focus answers with a chain. An unlabelled block of names under two
		// fields does not say what they are a chain OF.
		string text = Render("""{"ok":true,"focused":"Button","chain":["Window","Grid","Button"]}""");

		Assert.Contains("chain", text, StringComparison.Ordinal);
		Assert.Contains("Window, Grid, Button", text, StringComparison.Ordinal);
	}


	[Fact]
	public void OtherNestedValuesBecomeTheirOwnColumns()
	{
		// Only a rectangle is special. Anything else nested still shows,
		// rather than vanishing.
		string text = Render("""
		{"ok":true,"rows":[{"name":"a","state":{"active":true,"modal":false}}]}
		""");

		Assert.Contains("state.active", text, StringComparison.Ordinal);
		Assert.Contains("true", text, StringComparison.Ordinal);
	}

	[Fact]
	public void AControlIsNamedByWhatCanBeHandedBack()
	{
		// The id leads, because it is what `control`, `measure` and `focusctl`
		// accept. The type follows in parentheses because it is what a reader
		// recognises - a bare hash says nothing, and a bare type cannot be
		// handed back once an application has two of them.
		string text = Output.Pretty("control", JsonNode.Parse("""
		{"ok":true,"control":"MarkupControl","id":"MarkupControl#338574f","bounds":{"x":0,"y":5,"w":28,"h":1}}
		""")!);

		// The id leads the line; the word "control" was a label saying what
		// everyone already knew they had asked for.
		Assert.StartsWith("MarkupControl#338574f  (MarkupControl)", text,
			StringComparison.Ordinal);
	}

	[Fact]
	public void ANamedControlIsCalledByItsName()
	{
		// A name is better than an id for the same reason an id is better than
		// a type: it is what the author of the application calls it.
		string text = Output.Pretty("control", JsonNode.Parse("""
		{"ok":true,"control":"ButtonControl","id":"okButton","name":"okButton"}
		""")!);

		Assert.StartsWith("okButton  (ButtonControl)", text, StringComparison.Ordinal);
	}

	[Fact]
	public void TheChainOfContainersIsMadeOfThingsYouCanAskAbout()
	{
		// The point of printing the chain is that you can then ask about one of
		// them. Printing types made it a list of words that go nowhere.
		string text = Output.Pretty("inspect", JsonNode.Parse("""
		{"ok":true,"x":1,"y":1,"region":"client",
		 "window":{"title":"Settings"},
		 "control":{"control":"MarkupControl","id":"MarkupControl#338574f",
		  "parents":[{"control":"(root)"},{"control":"Grid","id":"Grid#21093c0"}]}}
		""")!);

		Assert.Contains("Grid#21093c0", text, StringComparison.Ordinal);
	}

	/// <summary>Every value in a reply, flattened to leaf values.</summary>
	private static IEnumerable<(string Path, string Value)> Leaves(JsonNode? node, string path = "")
	{
		switch (node)
		{
			case JsonObject o:
				foreach (var (name, value) in o)
				{
					if (name == "ok") continue;
					foreach (var leaf in Leaves(value, path.Length == 0 ? name : $"{path}.{name}"))
						yield return leaf;
				}
				break;

			case JsonArray a:
				foreach (var item in a)
					foreach (var leaf in Leaves(item, path)) yield return leaf;
				break;

			case JsonValue v:
				yield return (path, v.ToString());
				break;
		}
	}

	/// <summary>Asserts the rendered text accounts for every value the reply carried.</summary>
	private static void NothingDropped(string verb, string json)
	{
		string text = Render(json, verb);

		foreach (var (path, value) in Leaves(JsonNode.Parse(json)))
		{
			// The ancestor chain is a list of IDENTITIES: each link is named by
			// what another verb accepts, and that is all it claims to say. The
			// layout that arranged each level is a fact about that control, and
			// `control --control <id>` answers it - inlining it would turn a
			// one-line chain into a paragraph for a fact nobody asked this verb.
			if (path.Contains(".parents.", StringComparison.Ordinal)
				&& !path.EndsWith(".control", StringComparison.Ordinal)
				&& !path.EndsWith(".id", StringComparison.Ordinal)
				&& !path.EndsWith(".name", StringComparison.Ordinal))
			{
				continue;
			}

			// A boolean reads as yes/no, and an empty value has nothing to show.
			string shown = value switch { "true" => "yes", "false" => "no", _ => value };
			if (shown.Length == 0) continue;

			Assert.True(text.Contains(shown, StringComparison.Ordinal)
					|| text.Contains(value, StringComparison.Ordinal)
					|| text.Contains(path.Split('.')[^1], StringComparison.Ordinal),
				$"{verb} dropped {path}={value}. Rendered:\n{text}");
		}
	}

	[Fact]
	public void TheControlReplyShowsEveryFieldItWasSent()
	{
		// A formatter that silently drops a field makes the reader run --json
		// to find out whether it was absent or merely uninteresting - which
		// defeats the point of a readable layer. `absolute` was printed only
		// when it differed from `bounds`, so the screen coordinate that `mouse`
		// takes was missing from most replies.
		NothingDropped("control", """
			{"ok":true,"control":"ScrollablePanelControl","id":"ScrollablePanelControl#165f26b",
			 "desired":{"w":30,"h":10},"bounds":{"x":0,"y":3,"w":30,"h":25},
			 "absolute":{"x":0,"y":4,"w":30,"h":25},
			 "props":{"horizontal":"Left","vertical":"Fill"},
			 "state":{"scrollOffset":0,"viewportExtent":25},
			 "layout":"ScrollLayout","visible":true,
			 "parents":[{"control":"(root)","layout":"DockRootLayout"}],
			 "window":"SharpConsoleUI Demo"}
			""");
	}

	[Fact]
	public void TheInspectReplyShowsEveryFieldItWasSent()
	{
		// inspect is the devtools-style verb: point at a cell, be told what is
		// there. It printed the bounds and the ancestor chain and dropped the
		// control's state, props, desired size and screen position - so it said
		// LESS about what it found than `control` did about the same control.
		NothingDropped("inspect", """
			{"ok":true,"x":20,"y":10,"region":"client",
			 "cell":{"ch":"a","fg":"#eeeeee","bg":"#2d2d2d"},
			 "window":{"title":"Demo","active":true},
			 "control":{"control":"MarkupControl","id":"MarkupControl#338574f",
			   "desired":{"w":28,"h":1},"bounds":{"x":0,"y":5,"w":28,"h":1},
			   "absolute":{"x":0,"y":8,"w":28,"h":1},
			   "props":{"horizontal":"Left","sticky":"None"},
			   "state":{"enabled":true,"focusable":false},
			   "visible":true,
			   "parents":[{"control":"(root)","layout":"DockRootLayout"}]}}
			""");
	}

	[Fact]
	public void AFieldTheFormatterHasNeverHeardOfStillReaches()
	{
		// The protocol decides what exists. A reply from an app newer than this
		// build carries fields nothing here anticipates, and they must still be
		// printed rather than quietly discarded.
		string text = Render("""
			{"ok":true,"control":"ButtonControl","id":"ButtonControl#1",
			 "bounds":{"x":1,"y":2,"w":3,"h":4},
			 "somethingNewEntirely":"surprise"}
			""", "control");

		Assert.Contains("surprise", text, StringComparison.Ordinal);
	}

}

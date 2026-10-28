// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using Cxdbg.Format;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// Rendering a tree reply.
/// </summary>
/// <remarks>
/// This had no tests, and it silently dropped most of what the server sent:
/// the whole desktop block, every window flag, every control's state and
/// every layout property. `tree --props` asked for properties and printed
/// none, which made the flag look broken rather than the formatter.
/// </remarks>
public class TreeFormatterTests
{
	private const string Reply = """
	{"ok":true,
	 "desktop":{"width":100,"height":30,"originX":0,"originY":1,
	   "topPanelRows":1,"bottomPanelRows":1,"desktopBottom":28,
	   "theme":"ModernGray","windows":1,"modals":false,"portals":true},
	 "windows":[{"title":"Settings","id":"w-1","root":"DockRootLayout",
	   "bounds":{"x":0,"y":0,"w":90,"h":30},
	   "absolute":{"x":0,"y":1,"w":90,"h":30},
	   "state":{"windowState":"Normal","active":true,"modal":false,
	     "movable":false,"resizable":true,"closable":true},
	   "node":{"control":"(root)","layout":"DockRootLayout","visible":true,
	     "desired":{"w":88,"h":28},"bounds":{"x":0,"y":0,"w":88,"h":28},
	     "state":{"enabled":false,"focusable":true,"text":"Border Styles"},
	     "props":{"width":null,"horizontal":"Stretch"},
	     "childrenElided":3}}]}
	""";

	private static string Render() => TreeFormatter.Render(JsonNode.Parse(Reply)!);

	[Fact]
	public void TheDesktopIsShown()
	{
		// It carries where the desktop starts and how many panel rows frame
		// it - the reason a window's bounds and its screen position differ.
		string text = Render();

		Assert.Contains("desktop", text, StringComparison.Ordinal);
		Assert.Contains("originY 1", text, StringComparison.Ordinal);
		Assert.Contains("topPanelRows 1", text, StringComparison.Ordinal);
	}

	[Fact]
	public void APortalBeingOpenIsSaidOutLoud()
	{
		// A portal eats input. "My clicks do nothing" is answered here.
		Assert.Contains("portal open", Render(), StringComparison.Ordinal);
	}

	[Fact]
	public void EveryWindowFlagIsShown()
	{
		// Dragging a window that is not movable looks exactly like aiming
		// wrong, and only this says which.
		string text = Render();

		Assert.Contains("movable no", text, StringComparison.Ordinal);
		Assert.Contains("resizable yes", text, StringComparison.Ordinal);
		Assert.Contains("closable yes", text, StringComparison.Ordinal);
	}

	[Fact]
	public void AControlsStateIsShown()
	{
		// "Why is my control not responding" is answered by enabled:false,
		// which the server always sent and this never printed.
		string text = Render();

		Assert.Contains("enabled no", text, StringComparison.Ordinal);
		Assert.Contains("Border Styles", text, StringComparison.Ordinal);
	}

	[Fact]
	public void LayoutPropertiesAreShown()
	{
		// `tree --props` returned them and printed nothing.
		Assert.Contains("horizontal Stretch", Render(), StringComparison.Ordinal);
	}

	[Fact]
	public void AnUnsetPropertyIsStillReported()
	{
		// "It has no explicit width" is an answer to why it is the size it
		// is, so a null is shown rather than skipped.
		Assert.Contains("width -", Render(), StringComparison.Ordinal);
	}

	[Fact]
	public void AScreenPositionIsShownWhenItDiffersFromBounds()
	{
		// A window at y:0 under a top panel has its title bar on row 1.
		// Clicking bounds instead of absolute is the commonest way to miss.
		Assert.Contains("screen 0,1", Render(), StringComparison.Ordinal);
	}

	[Fact]
	public void AWalkThatStoppedEarlySaysSo()
	{
		// Without it, a --depth cut looks exactly like a leaf and a reader
		// concludes the control has no children when it has forty.
		Assert.Contains("3 more", Render(), StringComparison.Ordinal);
	}
	[Fact]
	public void TheTreeHasRealGuides()
	{
		// Spectre's tree, so a control's ancestry can be traced down the page
		// by following one line - which indentation alone cannot do once a
		// window is more than a few levels deep.
		string text = Render();

		Assert.Contains("\u2514\u2500\u2500", text, StringComparison.Ordinal);
	}

	[Fact]
	public void ColourIsOffWhenItWouldNotBeSeen()
	{
		// In a file or a pipe colour is noise that breaks grep and diff.
		Assert.DoesNotContain("\u001b[", Render(), StringComparison.Ordinal);
	}

	[Fact]
	public void AZeroExtentIsColouredWhenColourIsOn()
	{
		// The one state that makes a control present in the tree, reachable by
		// focus, and invisible on screen. It is red because that is the bug
		// someone is usually looking for.
		string broken = """
		{"ok":true,"windows":[{"title":"W","node":{"control":"B","id":"B#1",
		 "desired":{"w":20,"h":4},"bounds":{"x":0,"y":0,"w":0,"h":0}}}]}
		""";

		string text = TreeFormatter.Render(JsonNode.Parse(broken)!, colour: true);

		Assert.Contains("\u001b[", text, StringComparison.Ordinal);
		Assert.Contains("zero extent", text, StringComparison.Ordinal);
	}

	[Fact]
	public void FactsBelongToTheirNodeNotBeneathIt()
	{
		// Rendered as children they took a tree connector each and read as
		// further controls inside the one being described.
		string text = Render();

		foreach (string line in text.Split('\n'))
		{
			if (!line.Contains("enabled no", StringComparison.Ordinal)) continue;

			Assert.DoesNotContain("\u251c\u2500\u2500", line, StringComparison.Ordinal);
			Assert.DoesNotContain("\u2514\u2500\u2500", line, StringComparison.Ordinal);
		}
	}

	[Fact]
	public void AnApplicationsOwnMarkupCannotReachTheRenderer()
	{
		// A control's `text` is its SOURCE, markup and all. Those tags are the
		// application's, and this tree uses the same syntax for its own
		// colours - so they must arrive as characters, never as instructions.
		// Stripping them was tried and was wrong: "[[-]]" is the glyph a
		// collapsed node really shows, and no regex can tell it from a style.
		string hostile = """
		{"ok":true,"windows":[{"title":"W","node":{"control":"M","id":"M#1",
		 "state":{"text":"[bold rgb(0,255,255)]  [-] Layout[/]"}}}]}
		""";

		string text = TreeFormatter.Render(JsonNode.Parse(hostile)!, colour: true);

		// Shown as written, including the glyph a strip would have eaten.
		Assert.Contains("[-] Layout", text, StringComparison.Ordinal);
		Assert.Contains("[bold rgb(0,255,255)]", text, StringComparison.Ordinal);
	}

	[Fact]
	public void AnUnclosedTagFromAnApplicationDoesNotThrow()
	{
		// Spectre refuses malformed markup by throwing. An application is free
		// to hold whatever text it likes, and a debugger that crashed on it
		// would be useless exactly when something is already wrong.
		string broken = """
		{"ok":true,"windows":[{"title":"[bold] never closed","node":{"control":"M","id":"M#1",
		 "state":{"text":"[unclosed"}}}]}
		""";

		string text = TreeFormatter.Render(JsonNode.Parse(broken)!, colour: true);

		Assert.Contains("[unclosed", text, StringComparison.Ordinal);
	}

	[Fact]
	public void AMultiLineTextStaysOnOneLine()
	{
		// A control with three lines of text must not take three lines of the
		// tree, and dropping them silently would hide that it is multi-line.
		string multi = """
		{"ok":true,"windows":[{"title":"W","node":{"control":"M","id":"M#1",
		 "state":{"text":"first\nsecond"}}}]}
		""";

		string text = TreeFormatter.Render(JsonNode.Parse(multi)!, colour: false);

		Assert.Contains("first\u00b6second", text, StringComparison.Ordinal);
	}

}

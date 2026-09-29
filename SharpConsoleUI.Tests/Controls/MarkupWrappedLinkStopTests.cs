// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Reflection;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Issue #86: a <c>[link=…]</c> whose text wraps became one keyboard stop per rendered row, so
/// Right/Left stepped through the rows of a single link before reaching the next one, and only the
/// focused row highlighted. <c>LinkCount</c> counts markup tags and so disagreed with the number of
/// stops the moment a link wrapped.
/// </summary>
public class MarkupWrappedLinkStopTests
{
	/// <summary>A link long enough to wrap at 60 columns, followed by a short second link.</summary>
	private const string WrappingMarkup =
		"[markdown]![A rather long description of an image that certainly wraps at sixty columns]" +
		"(https://example.com/a.png) and [second](https://example.com/b)[/]";

	private static (MarkupControl markup, Window window) Render(string content, int width = 60, bool wrap = true)
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(100, 30),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = width,
			Height = 12,
			BorderStyle = BorderStyle.Frameless
		};

		var markup = new MarkupControl(new List<string> { content }) { Wrap = wrap };
		window.AddControl(markup);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.FocusControl(markup);
		window.RenderAndGetVisibleContent();
		return (markup, window);
	}

	private static object? Private(object target, string name)
	{
		var f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
		if (f != null) return f.GetValue(target);
		return target.GetType().GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);
	}

	private static int StopCount(MarkupControl markup)
	{
		var flatten = markup.GetType().GetMethod("FlattenLinks", BindingFlags.NonPublic | BindingFlags.Instance);
		return ((System.Collections.ICollection)flatten!.Invoke(markup, null)!).Count;
	}

	private static int FocusedIndex(MarkupControl markup) => (int)Private(markup, "_focusedLinkIndex")!;

	private static bool Right(MarkupControl markup) =>
		markup.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false));

	private static bool Left(MarkupControl markup) =>
		markup.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false));

	#region The reported bug

	/// <summary>
	/// The heart of #86: the number of keyboard stops must equal the number of links, however the
	/// text happens to wrap.
	/// </summary>
	[Fact]
	public void AWrappedLink_IsOneStop_NotOnePerRow()
	{
		var (markup, _) = Render(WrappingMarkup);

		int linkCount = (int)Private(markup, "LinkCount")!;

		Assert.Equal(2, linkCount);
		Assert.Equal(linkCount, StopCount(markup));
	}

	/// <summary>
	/// One Right from the first link must reach the second link, not the second row of the first.
	/// </summary>
	[Fact]
	public void RightFromAWrappedLink_ReachesTheNextLink()
	{
		var (markup, _) = Render(WrappingMarkup);
		Assert.Equal(0, FocusedIndex(markup));

		Assert.True(Right(markup));

		Assert.Equal(1, FocusedIndex(markup));

		// Only two links exist, so the next Right bubbles rather than finding a third stop.
		Assert.False(Right(markup));
		Assert.Equal(1, FocusedIndex(markup));
	}

	[Fact]
	public void LeftFromTheSecondLink_ReturnsToTheWrappedLink()
	{
		var (markup, _) = Render(WrappingMarkup);
		Right(markup);
		Assert.Equal(1, FocusedIndex(markup));

		Assert.True(Left(markup));

		Assert.Equal(0, FocusedIndex(markup));
		Assert.False(Left(markup));
	}

	#endregion

	#region Guard rails — the merge must not over-merge

	/// <summary>
	/// Two DISTINCT links that happen to share a URL must stay two stops. This is the case a
	/// same-URL heuristic would merge wrongly, and the reason the span carries a link ordinal.
	/// </summary>
	[Fact]
	public void TwoAdjacentLinksWithTheSameUrl_StayTwoStops()
	{
		var (markup, _) = Render("[link=https://example.com/x]one[/] [link=https://example.com/x]two[/]");

		Assert.Equal(2, (int)Private(markup, "LinkCount")!);
		Assert.Equal(2, StopCount(markup));
	}

	[Fact]
	public void LinksOnSeparateLines_StaySeparateStops()
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(100, 30),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 60,
			Height = 12,
			BorderStyle = BorderStyle.Frameless
		};

		var markup = new MarkupControl(new List<string>
		{
			"[link=https://example.com/a]first[/]",
			"[link=https://example.com/b]second[/]",
		})
		{ Wrap = true };

		window.AddControl(markup);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		Assert.Equal(2, StopCount(markup));
	}

	[Fact]
	public void AShortLinkThatDoesNotWrap_IsStillOneStop()
	{
		var (markup, _) = Render("[link=https://example.com/a]short[/] and [link=https://example.com/b]tiny[/]");

		Assert.Equal(2, StopCount(markup));
	}

	#endregion

	#region The parser carries the identity

	/// <summary>
	/// Every span a link produces — however many rows it spans — reports the same ordinal, and
	/// ordinals run in document order from zero.
	/// </summary>
	[Fact]
	public void ParsedSpansCarryTheLinkOrdinal()
	{
		MarkupParser.Parse(
			"[link=https://example.com/a]alpha[/] and [link=https://example.com/b]beta[/]",
			Color.White, Color.Black, out var links);

		Assert.Equal(2, links.Count);
		Assert.Equal(0, links[0].LinkIndex);
		Assert.Equal(1, links[1].LinkIndex);
	}

	#endregion
}

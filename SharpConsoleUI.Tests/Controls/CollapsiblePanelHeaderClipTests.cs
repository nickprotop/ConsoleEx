// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for a borderless <see cref="CollapsiblePanel"/> header painting outside the clip
/// it was given.
///
/// <para>
/// Reported as: GridControl &gt; TabControl (header shown) &gt; ChatTranscriptControl — while
/// scrolling, the chat messages' panel headers overwrote the tab header strip. The bordered header
/// path routes through <c>PanelBorderRenderer</c> (which clips) and the body is painted by the
/// layout engine (which clips), but the BORDERLESS header wrote its background fill, title and
/// separator at the panel's arranged position with no <c>clipRect</c> test at all. A panel scrolled
/// above its host's viewport therefore still drew its header row, landing on whatever chrome owned
/// that row.
/// </para>
///
/// <para>
/// The first test is the "real thing": the reported nesting, driven by real wheel events through the
/// dispatcher, asserting the tab header survives every scroll position (the corruption appeared only
/// at particular offsets, so a single scroll step would have missed it).
/// </para>
/// </summary>
public class CollapsiblePanelHeaderClipTests
{
	/// <summary>Reads one composited-screen row.</summary>
	private static string Row(CharacterBufferSnapshot snap, int y, int width)
	{
		var sb = new StringBuilder(width);
		for (int x = 0; x < width; x++) sb.Append(ChromeGeometry.CharAt(snap, x, y));
		return sb.ToString();
	}

	[Fact]
	public void ScrolledMessageHeaders_DoNotOverwriteTheTabHeader()
	{
		const int W = 60, H = 16;
		var system = ChromeGeometry.CreateSystem(W, H);

		var chat = new ChatTranscriptControl { VerticalAlignment = VerticalAlignment.Fill };
		for (int i = 0; i < 8; i++)
		{
			chat.AddMessage(ChatRole.User,
				$"Message {i}: a long user line that wraps a few times inside the narrow tab content area.");
			chat.AddMessage(ChatRole.Assistant,
				$"Reply {i}: another long wrapping answer adding more rows.");
		}

		var tabs = new SharpConsoleUI.Controls.TabControl { VerticalAlignment = VerticalAlignment.Fill };
		tabs.AddTab("Chat", chat);
		tabs.AddTab("Other", new MarkupControl(new List<string> { "second" }));

		var grid = SharpConsoleUI.Builders.Controls.Grid()
			.Columns(GridLength.Star(1))
			.Rows(GridLength.Star(1))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Place(tabs, 0, 0)
			.Build();

		var window = new WindowBuilder(system).Frameless().Maximized().Build();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);

		ChromeGeometry.Render(system);

		// The header strip as painted before any scrolling.
		string headerAtRest = Row(ChromeGeometry.Render(system), 0, W);
		Assert.Contains("Chat", headerAtRest);

		// Wheel-scroll through many offsets via the REAL input path. The overwrite only surfaced at
		// certain offsets (a message header landing exactly on the header row), so every step is
		// checked rather than a single scroll.
		var driver = (MockConsoleDriver)system.ConsoleDriver;
		for (int step = 1; step <= 12; step++)
		{
			driver.SimulateMouseEvent(
				new List<MouseFlags> { MouseFlags.WheeledUp },
				new System.Drawing.Point(20, 6));
			system.Input.ProcessInput();

			string header = Row(ChromeGeometry.Render(system), 0, W);
			Assert.Equal(headerAtRest, header);
		}

		// The tab content did move — otherwise the assertions above would pass trivially.
		Assert.NotEqual(
			Row(ChromeGeometry.Render(system), 1, W),
			headerAtRest);
	}

	[Fact]
	public void BorderlessHeader_IsClippedToTheTabContentArea()
	{
		const int W = 40, H = 10;
		var system = ChromeGeometry.CreateSystem(W, H);

		// A borderless panel WITH a separator, inside a scrollable panel, inside a tab. Scrolling the
		// panel above the tab's content area exercises all three previously-unclipped writes: the
		// background fill, the title cells and the separator rule.
		var host = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		for (int p = 0; p < 6; p++)
		{
			var panel = new CollapsiblePanel { Title = $"PANEL{p}", ShowHeaderSeparator = true };
			panel.AddControl(new MarkupControl(new List<string> { $"body {p} line 0", $"body {p} line 1" }));
			panel.Expand();
			host.AddControl(panel);
		}

		var tabs = new SharpConsoleUI.Controls.TabControl { VerticalAlignment = VerticalAlignment.Fill };
		tabs.AddTab("TABHDR", host);

		var window = new WindowBuilder(system).Frameless().Maximized().Build();
		window.AddControl(tabs);
		system.WindowStateService.AddWindow(window);

		ChromeGeometry.Render(system);
		string headerAtRest = Row(ChromeGeometry.Render(system), 0, W);
		Assert.Contains("TABHDR", headerAtRest);

		// Every scroll offset must leave the tab header row untouched — no panel title, no separator
		// rule, no header background wiped over it.
		for (int s = 1; s <= 10; s++)
		{
			host.ScrollVerticalBy(1);
			string header = Row(ChromeGeometry.Render(system), 0, W);
			Assert.DoesNotContain("PANEL", header);
			Assert.Equal(headerAtRest, header);
		}
	}
}

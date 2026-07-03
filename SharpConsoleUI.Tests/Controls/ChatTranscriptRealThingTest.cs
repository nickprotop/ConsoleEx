// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// The "real thing" end-to-end test for <see cref="ChatTranscriptControl"/>: multi-in-flight
/// streaming (append to the latest AND to a specific earlier message by id), auto-scroll
/// stickiness (follows while pinned to the bottom, does NOT yank to the bottom once the user has
/// scrolled up), and a real-dispatch click on a collapsed Tool message's header row that expands it.
///
/// <para>
/// This mirrors the actual usage path, NOT isolated component asserts:
/// <list type="bullet">
///   <item>Real container nesting — a <see cref="ChatTranscriptControl"/> (which IS a
///   <see cref="ScrollablePanelControl"/>) filling a NARROW window, hosting ~15 real
///   <see cref="CollapsiblePanel"/> message children (Tool/System start collapsed;
///   long user/assistant markdown wraps).</item>
///   <item>Boundary-stressing size (44 wide, 18 tall) so the transcript overflows and the
///   arranged/painted extents actually differ.</item>
///   <item>Real input path — clicks go through
///   <c>driver.SimulateMouseEvent(...) + system.Input.ProcessInput()</c> (dispatcher → hit-test →
///   parent-chain routing), never a direct <c>ProcessMouseEvent</c> call on the panel.</item>
///   <item>Re-render between action and assert, and every assertion is re-checked after a second
///   <c>UpdateDisplay</c> so the state must SURVIVE a re-layout.</item>
/// </list>
/// </para>
/// </summary>
public class ChatTranscriptRealThingTest
{
	private const int Width = 44;
	private const int Height = 18;

	/// <summary>
	/// Builds the real topology: a Fill <see cref="ChatTranscriptControl"/> in a narrow window with a
	/// spread of mixed-role messages. Returns the system, window, transcript and the ids of the two
	/// messages the streaming assertions target (an EARLY assistant message and the LATEST message).
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, ChatTranscriptControl chat,
		ChatMessageId earlyId, ChatMessageId latestId, ChatMessageId collapsedToolId)
		BuildTranscript()
	{
		Console.SetIn(TextReader.Null);

		var system = TestWindowSystemBuilder.CreateTestSystem(Width, Height);
		var window = new Window(system) { Left = 0, Top = 0, Width = Width, Height = Height };

		var chat = new ChatTranscriptControl
		{
			VerticalAlignment = VerticalAlignment.Fill
		};
		window.AddControl(chat);
		system.AddWindow(window);

		// ~15 mixed-role messages. System/Tool roles start collapsed (per the seeded role styles);
		// user/assistant carry long markdown that wraps in the narrow window.
		string longUser =
			"Please summarise the failing test run and suggest the smallest fix that keeps every " +
			"external consumer compiling unchanged.";
		string longAssistant =
			"The regression is in the scroll clamp: a stray measure pass resolved the viewport against " +
			"the full-content box, so the persisted offset was clamped back to zero on the next relayout.";

		// A collapsed Tool message is placed as the SECOND message so its header is reliably on-screen
		// after ScrollToTop (a long wrapping user message as message 0 would otherwise fill a 16-row
		// content viewport and push a later Tool below the fold). An EARLY assistant message follows,
		// which the multi-in-flight streaming test grows independently.
		ChatMessageId early;
		ChatMessageId collapsedTool;

		chat.AddMessage(ChatRole.System, "session started");                 // 0: collapsed system (short)
		collapsedTool = chat.AddMessage(ChatRole.Tool,                       // 1: collapsed Tool (target)
			"tool output for step 1\nsecond line\nthird line");
		early = chat.AddMessage(ChatRole.Assistant, "initial short reply");  // 2: EARLY streaming target

		// Fill out to ~15 mixed messages, some long-wrapping user/assistant markdown, more collapsed
		// System/Tool verbose roles — the realistic spread the transcript is built for.
		for (int i = 3; i < 15; i++)
		{
			switch (i % 5)
			{
				case 0:
					chat.AddMessage(ChatRole.User, $"{longUser} (turn {i})");
					break;
				case 1:
					chat.AddMessage(ChatRole.Assistant, $"{longAssistant} (turn {i})");
					break;
				case 2:
					chat.AddMessage(ChatRole.System, $"system directive {i}");
					break;
				case 3:
					chat.AddMessage(ChatRole.Tool, $"tool output line for step {i}\nsecond line");
					break;
				default:
					chat.AddMessage(ChatRole.Assistant, $"short reply {i}");
					break;
			}
		}

		// The latest message is a fresh assistant message we will stream tokens into.
		var latest = chat.AddMessage(ChatRole.Assistant, string.Empty);

		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, chat, early, latest, collapsedTool);
	}

	private static void Render(ConsoleWindowSystem system)
	{
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
	}

	/// <summary>
	/// Multi-in-flight streaming: <see cref="ChatTranscriptControl.Append(ChatMessageId, string)"/>
	/// grows a SPECIFIC message. Streaming into the latest and into an earlier message must be
	/// independent — each message reflects only its own tokens, and the growth survives a re-render.
	/// </summary>
	[Fact]
	public void Streaming_ToLatestAndEarlierMessage_GrowIndependently_RealThing()
	{
		var (system, _, chat, earlyId, latestId, _) = BuildTranscript();

		Assert.NotEqual(earlyId, latestId);

		string earlyBefore = chat.BodyTextForTest(earlyId);
		string latestBefore = chat.BodyTextForTest(latestId);

		// Stream distinct tokens into the LATEST message.
		foreach (var tok in new[] { "Hello", ", ", "world", "!" })
		{
			chat.Append(latestId, tok);
			Render(system);
		}

		// Stream distinct tokens into an EARLIER message, interleaved conceptually (multi-in-flight).
		foreach (var tok in new[] { " [appended", "-to-", "early]" })
		{
			chat.Append(earlyId, tok);
			Render(system);
		}

		string earlyAfter = chat.BodyTextForTest(earlyId);
		string latestAfter = chat.BodyTextForTest(latestId);

		Assert.Equal(latestBefore + "Hello, world!", latestAfter);
		Assert.Equal(earlyBefore + " [appended-to-early]", earlyAfter);

		// The latest tokens did NOT bleed into the early message and vice-versa.
		Assert.DoesNotContain("Hello, world!", earlyAfter);
		Assert.DoesNotContain("[appended-to-early]", latestAfter);

		// Survives a second re-render.
		Render(system);
		Assert.Equal(latestBefore + "Hello, world!", chat.BodyTextForTest(latestId));
		Assert.Equal(earlyBefore + " [appended-to-early]", chat.BodyTextForTest(earlyId));
	}

	/// <summary>
	/// Auto-scroll stickiness — the changlv-class scroll-jump nuance.
	/// <para>
	/// Phase 1 (FOLLOW): pinned to the bottom (AutoScroll on), streaming new content keeps the offset
	/// at the maximum — the transcript follows the newest tokens.
	/// </para>
	/// <para>
	/// Phase 2 (DETACH): after the user scrolls UP, streaming more content must NOT yank the viewport
	/// back to the bottom — the offset stays exactly where the user left it.
	/// </para>
	/// </summary>
	[Fact]
	public void AutoScroll_FollowsAtBottom_ButDoesNotYankAfterScrollUp_RealThing()
	{
		var (system, _, chat, _, latestId, _) = BuildTranscript();

		// Precondition: the transcript overflows its viewport, so there is somewhere to scroll.
		Assert.True(chat.TotalContentHeight > chat.ViewportHeight,
			$"precondition: transcript must overflow (content={chat.TotalContentHeight} viewport={chat.ViewportHeight}).");

		// --- Phase 1: pinned to the bottom, streaming should FOLLOW ---
		chat.ScrollToBottom();
		chat.AutoScroll = true; // explicitly pinned to the bottom (ScrollToBottom is one-shot)
		Render(system);

		int maxOffset = Math.Max(0, chat.TotalContentHeight - chat.ViewportHeight);
		Assert.Equal(maxOffset, chat.VerticalScrollOffset);

		for (int i = 0; i < 8; i++)
		{
			chat.Append(latestId, $"streamed line {i}\n");
			Render(system);
		}

		int followMax = Math.Max(0, chat.TotalContentHeight - chat.ViewportHeight);
		Assert.Equal(followMax, chat.VerticalScrollOffset);
		Assert.True(chat.VerticalScrollOffset >= maxOffset,
			$"AutoScroll must FOLLOW newly streamed content while pinned to the bottom " +
			$"(offset={chat.VerticalScrollOffset} pinned-max={followMax}).");

		// --- Phase 2: scroll UP, then stream more — the offset must NOT be yanked back down ---
		chat.ScrollVerticalBy(-4); // user scrolls up: this detaches AutoScroll
		Render(system);

		int parkedOffset = chat.VerticalScrollOffset;
		Assert.True(parkedOffset < followMax,
			$"after scrolling up the offset must be above the bottom (parked={parkedOffset} bottom={followMax}).");
		Assert.False(chat.AutoScroll,
			"scrolling up must detach AutoScroll so streaming does not yank the viewport to the bottom.");

		for (int i = 0; i < 8; i++)
		{
			chat.Append(latestId, $"more streamed line {i}\n");
			Render(system);
		}

		// THE KEY ASSERTION: streaming more content did NOT drag the viewport back to the bottom.
		Assert.Equal(parkedOffset, chat.VerticalScrollOffset);

		// And it survives another re-render.
		Render(system);
		Assert.Equal(parkedOffset, chat.VerticalScrollOffset);
		Assert.False(chat.AutoScroll);
	}

	/// <summary>
	/// Real-click collapse/expand: a collapsed Tool message's header row is clicked through the REAL
	/// window dispatch pipeline (derived coords from its arranged <c>ActualX/ActualY</c> after a first
	/// render). The message expands, and the expanded state survives a re-render.
	/// </summary>
	[Fact]
	public void RealClick_OnCollapsedToolHeader_Expands_SurvivesRelayout_RealThing()
	{
		var (system, window, chat, _, _, toolId) = BuildTranscript();

		var panel = chat.PanelForTest(toolId);

		// The Tool role seeds Collapsible + StartCollapsed, so it must render collapsed initially.
		Assert.True(panel.Collapsible, "test setup: the Tool message panel must be collapsible.");
		Assert.False(panel.IsExpanded, "test setup: the Tool message must start collapsed.");

		// Bring the collapsed Tool message into view from the top so a real click can land on its header.
		// Detach AutoScroll first — otherwise the next repaint (AutoScroll follow) yanks the viewport
		// back to the bottom and the top-of-transcript Tool message would scroll off again.
		chat.AutoScroll = false;
		chat.ScrollToTop();
		Render(system);

		// The panels inside a self-painting ScrollablePanel are orphan layout nodes (ActualX/ActualY are
		// a stale (0,0)); their true on-screen row must come from the transcript's own live stacked
		// layout. HeaderViewportRowForTest returns the panel-top row in the transcript's content-viewport
		// space (or -1 if scrolled off). Translate to absolute screen coords via the window border and
		// the transcript's own arranged position; add Margin.Top to hit the header toggle row.
		int headerRow = chat.HeaderViewportRowForTest(toolId);
		Assert.True(headerRow >= 0,
			$"the collapsed Tool message must be on-screen after ScrollToTop (headerRow={headerRow}).");

		int clickX = window.Left + 1 + chat.ActualX + panel.Margin.Left + 1;
		int clickY = window.Top + 1 + chat.ActualY + headerRow + panel.Margin.Top;

		// Sanity: the derived click row must be within the window's content area.
		Assert.InRange(clickY, window.Top + 1, window.Top + Height - 1);

		// Clear focus so the click alone drives the toggle.
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(clickX, clickY));
		system.Input.ProcessInput();

		Assert.True(panel.IsExpanded,
			$"clicking the collapsed Tool message's header row must expand it " +
			$"(clicked screen=({clickX},{clickY}) headerRow={headerRow} " +
			$"chatActual=({chat.ActualX},{chat.ActualY}) scrollOffset={chat.VerticalScrollOffset}).");

		// Re-lays out without corruption, and the expanded state survives a second re-render.
		Render(system);
		Assert.True(panel.IsExpanded, "the expanded state must survive a re-layout.");

		// The other roles are unaffected — no cross-message corruption.
		foreach (var id in chat.MessageIds)
		{
			if (id == toolId)
				continue;
			var other = chat.PanelForTest(id);
			if (other.Collapsible && chat.GetRole(id) == ChatRole.Tool)
			{
				// other collapsed tool messages must remain collapsed (only the clicked one toggled).
				Assert.False(other.IsExpanded,
					"clicking one Tool header must not expand a different Tool message.");
			}
		}
	}
}

// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.IO;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// The "real thing" end-to-end test for the collapsed fade-peek preview and the footer spacer:
/// a real <see cref="ChatTranscriptControl"/> in a real <see cref="Window"/> at a boundary size,
/// driven through the real render path (double <c>UpdateDisplay</c>).
///
/// <para>
/// Verifies the two features coexist against real layout:
/// <list type="bullet">
///   <item>A collapsible Tool-role message starts collapsed and gets a peek preview row (a real
///   child <see cref="MarkupControl"/>), whose text is the first hidden line — and the peek
///   SURVIVES a re-render.</item>
///   <item>Expanding the message removes the peek row, and it STAYS removed across a re-render.</item>
///   <item>A footer'd message (via <c>SetStatus</c>) carries a 1-line trailing spacer after a real
///   render, so both features are live simultaneously.</item>
/// </list>
/// </para>
/// </summary>
public class ChatMessagePeekRealThingTest
{
	private const int Width = 44;
	private const int Height = 18;

	private static void Render(ConsoleWindowSystem system)
	{
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
	}

	[Fact]
	public void CollapsedPeek_ShowsFirstLine_SurvivesRelayout_RemovedOnExpand_SpacerCoexists_RealThing()
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

		// A collapsible Tool-role message with multi-line content. Tool starts collapsed per the
		// seeded role style, so a peek preview row should be inserted showing the first hidden line.
		var toolId = chat.AddMessage(
			ChatRole.Tool,
			"first hidden line of output\nsecond\nthird");

		Render(system);

		// The Tool message must render collapsed.
		var panel = chat.PanelForTest(toolId);
		Assert.True(panel.Collapsible, "test setup: the Tool message panel must be collapsible.");
		Assert.False(panel.IsExpanded, "test setup: the Tool message must start collapsed.");

		// Collapsed → peek row present, previewing the first hidden line.
		Assert.NotNull(chat.PeekRowForTest(toolId));
		Assert.StartsWith("first hidden line", chat.PeekTextForTest(toolId));

		// The peek SURVIVES a re-render.
		Render(system);
		Assert.NotNull(chat.PeekRowForTest(toolId));

		// Drive expand: the peek must be removed on expand.
		panel.IsExpanded = true;
		Render(system);
		Assert.Null(chat.PeekRowForTest(toolId));

		// And it stays removed across another re-render.
		Render(system);
		Assert.Null(chat.PeekRowForTest(toolId));

		// A footer'd message: SetStatus adds a status row whose bottom margin carries the 1-line
		// spacer. This is a real-render check that the peek and the spacer coexist.
		var footerId = chat.AddMessage(ChatRole.Assistant, "here is a reply");
		chat.SetStatus(footerId, "generated in 1.8 s");
		Render(system);

		Assert.Equal(1, chat.FooterBottomMarginForTest(footerId));

		GC.KeepAlive(system);
	}
}

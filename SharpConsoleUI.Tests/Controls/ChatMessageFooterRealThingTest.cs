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
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// The "real thing" end-to-end test for a chat message's footer (actions row + status row):
/// the footer rows are SIBLINGS of the message's <see cref="CollapsiblePanel"/> — inserted into the
/// transcript right after the panel, never <c>panel.AddControl(footer)</c> — so collapsing the
/// message body must NOT hide the actions or status.
///
/// <para>
/// This mirrors the actual usage path, NOT isolated component asserts:
/// <list type="bullet">
///   <item>Real container nesting — a <see cref="ChatTranscriptControl"/> (which IS a
///   <see cref="ScrollablePanelControl"/>) filling a NARROW/SHORT window, hosting a real
///   collapsible Tool-role <see cref="CollapsiblePanel"/> whose footer carries a Copy button and a
///   Like toggle plus a status line.</item>
///   <item>Boundary-stressing size (40 wide, 16 tall) so the arranged/painted extents differ.</item>
///   <item>Real render path — the host's double <c>system.Render.UpdateDisplay()</c> mirrors the live
///   render loop; the footer/toggle state is re-checked after re-render so it must SURVIVE re-layout.</item>
/// </list>
/// </para>
/// </summary>
public class ChatMessageFooterRealThingTest
{
	private const int Width = 40;
	private const int Height = 16;

	/// <summary>
	/// The core guarantee: a message's footer (actions row + status row) survives collapsing the
	/// message body, and a driven toggle survives a re-render. If the footer were parented to the
	/// panel it would vanish on collapse — this test would then catch that real sibling-hosting bug.
	/// </summary>
	[Fact]
	public void Footer_SurvivesCollapse_AndButtonsStillRender()
	{
		Console.SetIn(TextReader.Null);

		var system = TestWindowSystemBuilder.CreateTestSystem(Width, Height); // narrow/short = boundary
		var window = new Window(system) { Left = 0, Top = 0, Width = Width, Height = Height };

		var chat = new ChatTranscriptControl
		{
			VerticalAlignment = VerticalAlignment.Fill
		};
		window.AddControl(chat);
		system.AddWindow(window);

		// Tool role is collapsible by default; give it a multi-line body so collapse actually changes height.
		var id = chat.AddMessage(ChatRole.Tool, "verbose tool output line one\nline two\nline three");
		chat.SetActions(id, new[]
		{
			new ChatMessageAction { Id = "copy", Label = "Copy" },
			new ChatMessageAction { Id = "like", Label = "Like", Variant = ChatActionVariant.Toggle },
		});
		chat.SetStatus(id, "done", NotificationSeverity.Success);

		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Footer present with both buttons before collapse.
		Assert.True(chat.HasFooterForTest(id), "the message must have a footer (actions + status) after SetActions/SetStatus.");
		Assert.Equal(2, chat.ActionButtonCountForTest(id));
		Assert.NotNull(chat.StatusBarForTest(id));

		// The panel starts collapsed (Tool role seeds StartCollapsed); flip it expanded then collapsed
		// to exercise the transition, ending collapsed.
		var panel = chat.PanelForTest(id);
		panel.IsExpanded = true;
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Collapse the message body.
		panel.IsExpanded = false;
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// THE CORE GUARANTEE: footer + both buttons + status SURVIVE the collapse, read from arranged state.
		Assert.True(chat.HasFooterForTest(id),
			"the footer must survive collapsing the message body (rows are siblings of the panel, not children).");
		Assert.Equal(2, chat.ActionButtonCountForTest(id));
		Assert.NotNull(chat.StatusBarForTest(id));
		Assert.False(panel.IsExpanded, "the message body must be collapsed for this assertion to be meaningful.");

		// Drive the toggle through the real dispatch path and assert it survives a re-render.
		Assert.False(chat.ActionPressedForTest(id, "like"), "the toggle starts unpressed.");
		chat.InvokeActionForTest(id, "like");
		system.Render.UpdateDisplay();

		Assert.True(chat.ActionPressedForTest(id, "like"), "invoking the Like toggle must press it.");

		// Survives a second re-render (state must not be reset by re-layout).
		system.Render.UpdateDisplay();
		Assert.True(chat.ActionPressedForTest(id, "like"), "the pressed toggle state must survive a re-layout.");

		// And the footer is still intact after all of the above.
		Assert.True(chat.HasFooterForTest(id));
		Assert.Equal(2, chat.ActionButtonCountForTest(id));

		GC.KeepAlive(system);
	}
}

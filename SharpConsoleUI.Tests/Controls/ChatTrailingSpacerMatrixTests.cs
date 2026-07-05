// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	// The reported bug: a COLLAPSED message with a peek but NO footer had no trailing blank line below it
	// (panel margin zeroed because a peek follows, but no footer spacer existed). Assert every message —
	// across collapsible/non-collapsible × footer/no-footer — has exactly ONE trailing blank line before the next.
	public class ChatTrailingSpacerMatrixTests
	{
		// Returns the bottom margin of a message's bottommost sibling row (the trailing spacer owner).
		private static int TrailingSpacer(ChatTranscriptControl chat, ChatMessageId id)
		{
			var status = chat.StatusBarForTest(id);
			var actions = chat.ActionsToolbarForTest(id);
			var peek = chat.PeekRowForTest(id);
			if (status != null) return status.Margin.Bottom;
			if (actions != null) return actions.Margin.Bottom;
			if (peek != null) return peek.Margin.Bottom;
			return chat.PanelBottomMarginForTest(id); // no siblings → the panel's own margin provides the gap
		}

		[Fact]
		public void CollapsedTool_NoFooter_HasTrailingBlankLine()
		{
			// THE BUG CASE: a collapsed Tool message (gets a peek), no actions/status.
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Tool, "hidden line one\nhidden line two");
			Assert.NotNull(chat.PeekRowForTest(id));       // collapsed → has a peek
			Assert.Null(chat.StatusBarForTest(id));         // no footer
			Assert.Null(chat.ActionsToolbarForTest(id));
			Assert.Equal(1, TrailingSpacer(chat, id));       // MUST have the trailing blank line
		}

		[Fact]
		public void EveryMatrixCase_HasExactlyOneTrailingBlankLine()
		{
			var chat = new ChatTranscriptControl();

			// 1. expanded (Assistant, borderless, not collapsed), no footer
			var m1 = chat.AddMessage(ChatRole.Assistant, "plain");
			// 2. expanded, footer
			var m2 = chat.AddMessage(ChatRole.Assistant, "with footer");
			chat.SetStatus(m2, "ok");
			// 3. collapsed (Tool), footer
			var m3 = chat.AddMessage(ChatRole.Tool, "tool a\ntool b");
			chat.SetActions(m3, new[] { new ChatMessageAction { Id = "c", Label = "Copy" } });
			chat.SetStatus(m3, "done");
			// 4. collapsed (Tool), no footer — the bug case
			var m4 = chat.AddMessage(ChatRole.Tool, "quiet a\nquiet b");

			foreach (var id in new[] { m1, m2, m3, m4 })
				Assert.Equal(1, TrailingSpacer(chat, id));
		}

		[Fact]
		public void RemovingFooterWhileCollapsed_MovesSpacerToPeek()
		{
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Tool, "a\nb");
			chat.SetStatus(id, "ok");
			Assert.Equal(1, TrailingSpacer(chat, id)); // on status
			chat.ClearStatus(id);                       // footer gone; peek remains, becomes bottommost
			Assert.Equal(1, TrailingSpacer(chat, id)); // spacer moved to peek — still exactly one blank line
		}
	}
}

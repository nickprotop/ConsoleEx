// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class ChatMessageRailGutterTests
	{
		[Fact]
		public void FooterPresent_InsetsChildren_NotPanel()
		{
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Assistant, "hello");
			Assert.Equal(0, chat.BodyLeftMarginForTest(id));   // no footer yet
			chat.SetStatus(id, "done");
			Assert.Equal(chat.MessageRailGutterWidth, chat.BodyLeftMarginForTest(id)); // body inset
			Assert.Equal(0, chat.PanelLeftMarginForTest(id));  // panel/header NEVER inset (flush)
		}

		[Fact]
		public void FooterCleared_RemovesGutter()
		{
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Assistant, "hello");
			chat.SetStatus(id, "done");
			chat.ClearStatus(id);
			Assert.Equal(0, chat.BodyLeftMarginForTest(id));
		}

		[Fact]
		public void RailDisabled_NoGutter()
		{
			var chat = new ChatTranscriptControl { MessageRailEnabled = false };
			var id = chat.AddMessage(ChatRole.Assistant, "hello");
			chat.SetStatus(id, "done");
			Assert.Equal(0, chat.BodyLeftMarginForTest(id));
		}

		[Fact]
		public void RailConfig_Defaults()
		{
			var chat = new ChatTranscriptControl();
			Assert.True(chat.MessageRailEnabled);
			Assert.Equal('│', chat.MessageRailGlyph);
			Assert.Equal(2, chat.MessageRailGutterWidth);
			Assert.Null(chat.MessageRailColor);
		}

		[Fact]
		public void FooterPresent_CollapsesPanelBottomMargin_RestoredWhenCleared()
		{
			// The role style gives the panel a bottom margin (the between-message gap). When the message has a
			// footer, that margin would fall INSIDE the message (between the body and the sibling footer), so
			// it's collapsed to 0 to keep the unit contiguous — and restored when the footer is cleared.
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Assistant, "hello");
			int roleBottom = chat.GetRoleStyle(ChatRole.Assistant).Margin.Bottom;
			Assert.True(roleBottom > 0, "the Assistant role must have a bottom margin for this test to mean anything");
			Assert.Equal(roleBottom, chat.PanelBottomMarginForTest(id)); // no footer → role default

			chat.SetStatus(id, "done");
			Assert.Equal(0, chat.PanelBottomMarginForTest(id)); // footer → collapsed, no internal gap

			chat.ClearStatus(id);
			Assert.Equal(roleBottom, chat.PanelBottomMarginForTest(id)); // footer gone → restored
		}
	}
}

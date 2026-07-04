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
	public class ChatMessagePeekTests
	{
		private static ChatTranscriptControl Build() => new ChatTranscriptControl();

		[Fact]
		public void CollapsedMessage_WithPreview_GetsPeekRow_WithFirstLine()
		{
			var chat = Build();
			// Tool messages start collapsed by default (see SeedDefaultRoleStyles).
			var id = chat.AddMessage(ChatRole.Tool, "first hidden line\nsecond\nthird");

			Assert.False(chat.PanelForTest(id).IsExpanded);
			Assert.NotNull(chat.PeekRowForTest(id));
			Assert.StartsWith("first hidden line", chat.PeekTextForTest(id));
		}

		[Fact]
		public void ExpandingMessage_RemovesPeekRow()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Tool, "first hidden line\nsecond\nthird");

			Assert.NotNull(chat.PeekRowForTest(id));

			chat.PanelForTest(id).IsExpanded = true;

			Assert.Null(chat.PeekRowForTest(id));
		}

		[Fact]
		public void PreviewDisabled_NoPeek()
		{
			var chat = new ChatTranscriptControl { CollapsedPreview = false };
			var id = chat.AddMessage(ChatRole.Tool, "first hidden line\nsecond\nthird");

			Assert.Null(chat.PeekRowForTest(id));
		}

		[Fact]
		public void Config_Defaults()
		{
			var chat = Build();

			Assert.True(chat.CollapsedPreview);
			Assert.Equal(10, chat.CollapsedPreviewFadeWidth);
		}

		[Fact]
		public void CollapsedMessage_StreamedContent_RefreshesPeekText()
		{
			// A message collapsed while its content is still arriving (Tool starts collapsed, then streams)
			// must show an up-to-date preview, not the stale content from when the peek was first built.
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Tool, string.Empty); // collapsed, empty
			chat.Append(id, "streamed first line arrives");

			Assert.NotNull(chat.PeekRowForTest(id));
			Assert.StartsWith("streamed first line", chat.PeekTextForTest(id)!);
		}
	}
}

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
	public class ChatMessageActionSeedTests
	{
		[Fact]
		public void AddMessage_WithActions_SeedsFooter()
		{
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Assistant, "hi", author: null,
				actions: new[] { new ChatMessageAction { Id = "copy", Label = "Copy" } }, status: null);
			Assert.Equal(1, chat.ActionButtonCountForTest(id));
		}

		[Fact]
		public void RoleStyle_DefaultActions_SeedMessagesOfThatRole()
		{
			var chat = new ChatTranscriptControl();
			chat.SetRoleStyle(ChatRole.Assistant, new ChatRoleStyle
			{ DefaultActions = new[] { new ChatMessageAction { Id = "copy", Label = "Copy" } } });
			var id = chat.AddMessage(ChatRole.Assistant, "hi");
			Assert.Equal(1, chat.ActionButtonCountForTest(id));
			// A role without DefaultActions gets no footer:
			var uid = chat.AddMessage(ChatRole.User, "yo");
			Assert.False(chat.HasFooterForTest(uid));
		}
	}
}

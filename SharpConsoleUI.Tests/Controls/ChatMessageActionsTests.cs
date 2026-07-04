// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class ChatMessageActionsTests
	{
		private static ChatTranscriptControl Build() => new ChatTranscriptControl();

		[Fact]
		public void SetActions_BuildsToolbar_WithOneButtonPerAction()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");
			chat.SetActions(id, new[]
			{
				new ChatMessageAction { Id = "copy", Label = "Copy" },
				new ChatMessageAction { Id = "retry", Label = "Retry" },
			});
			Assert.True(chat.HasFooterForTest(id));
			Assert.Equal(2, chat.ActionButtonCountForTest(id));
		}

		[Fact]
		public void ActionClick_RunsOnClick_FiresActionInvoked_AppliesAfterPressHide()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");
			bool clicked = false;
			ChatMessageAction? invoked = null;
			chat.ActionInvoked += (_, e) => invoked = e.Action;
			chat.SetActions(id, new[]
			{
				new ChatMessageAction { Id = "retry", Label = "Retry", AfterPress = ChatActionAfterPress.Hide,
					OnClick = ctx => clicked = true },
			});
			chat.InvokeActionForTest(id, "retry"); // seam that simulates a button click
			Assert.True(clicked);
			Assert.Equal("retry", invoked!.Id);
			Assert.Null(chat.ActionsToolbarForTest(id)); // AfterPress.Hide removed the row
		}

		[Fact]
		public void ClearActions_RemovesRow_AndFooterWhenNoStatus()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");
			chat.SetActions(id, new[] { new ChatMessageAction { Id = "a", Label = "A" } });
			chat.ClearActions(id);
			Assert.Null(chat.ActionsToolbarForTest(id));
			Assert.False(chat.HasFooterForTest(id));
		}

		[Fact]
		public void ActionsRow_SitsAboveStatusRow_RegardlessOfAddOrder()
		{
			// Status added first, then actions.
			var chatA = Build();
			var idA = chatA.AddMessage(ChatRole.Assistant, "hi");
			chatA.SetStatus(idA, "done");
			chatA.SetActions(idA, new[] { new ChatMessageAction { Id = "a", Label = "A" } });
			AssertActionsAboveStatus(chatA, idA);

			// Actions added first, then status.
			var chatB = Build();
			var idB = chatB.AddMessage(ChatRole.Assistant, "hi");
			chatB.SetActions(idB, new[] { new ChatMessageAction { Id = "a", Label = "A" } });
			chatB.SetStatus(idB, "done");
			AssertActionsAboveStatus(chatB, idB);
		}

		private static void AssertActionsAboveStatus(ChatTranscriptControl chat, ChatMessageId id)
		{
			var toolbar = chat.ActionsToolbarForTest(id);
			var status = chat.StatusBarForTest(id);
			Assert.NotNull(toolbar);
			Assert.NotNull(status);

			var children = chat.Children.ToList();
			int toolbarIndex = children.IndexOf(toolbar!);
			int statusIndex = children.IndexOf(status!);
			Assert.True(toolbarIndex >= 0 && statusIndex >= 0);
			Assert.True(toolbarIndex < statusIndex,
				$"actions row (index {toolbarIndex}) must sit above the status row (index {statusIndex})");
		}
	}
}

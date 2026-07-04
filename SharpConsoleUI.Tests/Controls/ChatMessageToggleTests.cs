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
	public class ChatMessageToggleTests
	{
		private static ChatTranscriptControl Build() => new ChatTranscriptControl();

		[Fact]
		public void ToggleAction_Click_FlipsPressed_FiresActionToggled()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");
			bool? toggledTo = null;
			chat.ActionToggled += (_, e) => toggledTo = e.IsPressed;
			chat.SetActions(id, new[] { new ChatMessageAction { Id = "like", Label = "Like", Variant = ChatActionVariant.Toggle } });
			Assert.False(chat.ActionPressedForTest(id, "like"));
			chat.InvokeActionForTest(id, "like");
			Assert.True(chat.ActionPressedForTest(id, "like"));
			Assert.True(toggledTo);
			chat.InvokeActionForTest(id, "like");
			Assert.False(chat.ActionPressedForTest(id, "like"));
		}

		[Fact]
		public void SetActionState_DrivesToggleProgrammatically_WithoutFiringOnClick()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");
			bool onClickRan = false;
			chat.SetActions(id, new[]
			{
				new ChatMessageAction
				{
					Id = "like",
					Label = "Like",
					Variant = ChatActionVariant.Toggle,
					OnClick = _ => onClickRan = true
				}
			});
			chat.SetActionState(id, "like", true);
			Assert.True(chat.ActionPressedForTest(id, "like"));
			Assert.False(onClickRan);
		}
	}
}

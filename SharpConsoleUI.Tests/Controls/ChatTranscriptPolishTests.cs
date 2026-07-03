// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests;

public class ChatTranscriptPolishTests
{
	[Fact]
	public void Thinking_ShowsIndicator_ClearsOnFirstToken()
	{
		var chat = new ChatTranscriptControl();
		var id = chat.AddMessage(ChatRole.Assistant, "", thinking: true);
		Assert.True(chat.IsThinking(id));
		chat.Append(id, "first");
		Assert.False(chat.IsThinking(id));
	}

	[Fact]
	public void AnimateMessages_Toggle_SetsPanelAnimationMode()
	{
		// Non-collapsible roles always get None; use a collapsible role to test the Height path.
		var chat = new ChatTranscriptControl { AnimateMessages = false };
		var id = chat.AddMessage(ChatRole.System, "sys");
		Assert.Equal(CollapsibleAnimationMode.None, chat.AnimationModeForTest(id));

		var chat2 = new ChatTranscriptControl { AnimateMessages = true };
		var id2 = chat2.AddMessage(ChatRole.System, "sys");
		Assert.Equal(CollapsibleAnimationMode.Height, chat2.AnimationModeForTest(id2));
	}

	[Fact]
	public void GradientHeader_WrapsHeaderInGradientMarkup()
	{
		var chat = new ChatTranscriptControl();
		chat.SetRoleStyle(ChatRole.Assistant, new ChatRoleStyle
		{
			HeaderGradient = (new Color(0, 120, 255), new Color(0, 255, 180))
		});
		var id = chat.AddMessage(ChatRole.Assistant, "hi");
		Assert.Contains("[gradient=", chat.HeaderTextForTest(id));
	}

	[Fact]
	public void AlphaBackground_ReachesMessagePanel()
	{
		var chat = new ChatTranscriptControl();
		chat.SetRoleStyle(ChatRole.User, new ChatRoleStyle { Background = new Color(20, 20, 20).WithAlpha(180) });
		var id = chat.AddMessage(ChatRole.User, "hi");
		Assert.Equal((byte)180, chat.BackgroundForTest(id)!.Value.A);
	}
}

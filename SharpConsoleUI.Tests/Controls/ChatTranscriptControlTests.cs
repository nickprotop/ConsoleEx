// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests;

public class ChatTranscriptControlTests
{
	private static ChatTranscriptControl Build() => new ChatTranscriptControl();

	[Fact]
	public void AddMessage_ReturnsId_TracksRole()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.User, "hello");
		Assert.Contains(id, chat.MessageIds);
		Assert.Equal(ChatRole.User, chat.GetRole(id));
	}

	[Fact]
	public void Append_Latest_GrowsMessage()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.Assistant, "");
		chat.Append("Hel");
		chat.Append("lo");
		// Streamed tokens accumulate into the message body.
		Assert.Equal("Hello", chat.BodyTextForTest(id));
		// UpdateMessage replaces the whole body, it does not append.
		chat.UpdateMessage(id, "Replaced");
		Assert.Equal("Replaced", chat.BodyTextForTest(id));
		Assert.Equal(ChatRole.Assistant, chat.GetRole(id));
	}

	[Fact]
	public void Append_ById_TargetsSpecificMessage_MultiInFlight()
	{
		var chat = Build();
		var a = chat.AddMessage(ChatRole.Assistant, "");
		var b = chat.AddMessage(ChatRole.Tool, "");
		chat.Append(a, "answer");     // grow the EARLIER message, not the latest (b)
		chat.Append(b, "toolout");
		Assert.Equal(2, chat.MessageIds.Count);
		// Each message accumulated only its OWN tokens — no cross-contamination.
		Assert.Equal("answer", chat.BodyTextForTest(a));
		Assert.Equal("toolout", chat.BodyTextForTest(b));
		Assert.Equal(ChatRole.Assistant, chat.GetRole(a));
		Assert.Equal(ChatRole.Tool, chat.GetRole(b));
	}

	[Fact]
	public void RemoveAndClear()
	{
		var chat = Build();
		var a = chat.AddMessage(ChatRole.User, "one");
		var b = chat.AddMessage(ChatRole.Assistant, "two");
		chat.RemoveMessage(a);
		Assert.DoesNotContain(a, chat.MessageIds);
		Assert.Contains(b, chat.MessageIds);
		chat.Clear();
		Assert.Empty(chat.MessageIds);
	}

	[Fact]
	public void RoleStyle_DefaultsExist_AndOverridable()
	{
		var chat = Build();
		Assert.NotNull(chat.GetRoleStyle(ChatRole.Assistant));   // themed default present
		var custom = new ChatRoleStyle { ColorRole = ColorRole.Success, Collapsible = true };
		chat.SetRoleStyle(ChatRole.Tool, custom);
		Assert.Equal(ColorRole.Success, chat.GetRoleStyle(ChatRole.Tool).ColorRole);
	}

	[Fact]
	public void ThinkingMessage_IsThinking_ClearedOnFirstToken()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.Assistant, "", thinking: true);
		Assert.True(chat.IsThinking(id));
		chat.Append(id, "first token");
		Assert.False(chat.IsThinking(id));
	}
}

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

	[Fact]
	public void RoleStyle_SelectableDefaultsNull_InheritsMasterTrue_BodyIsSelectable()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.Assistant, "hello");
		// Built-in role styles leave Selectable null → inherit the master (default true).
		Assert.Null(chat.GetRoleStyle(ChatRole.Assistant).Selectable);
		Assert.True(chat.MessagesSelectable);
		Assert.Equal(true, chat.BodySelectionEnabledForTest(id));
	}

	[Fact]
	public void RoleStyle_SelectableFalse_ForcesOff_EvenWhenMasterOn()
	{
		var chat = Build();
		// Opt a single role out of selection via SetRoleStyle; other (null) roles inherit master (on).
		chat.SetRoleStyle(ChatRole.System, new ChatRoleStyle { Selectable = false });
		var sys = chat.AddMessage(ChatRole.System, "notice");
		var asst = chat.AddMessage(ChatRole.Assistant, "answer");
		Assert.Equal(false, chat.BodySelectionEnabledForTest(sys));
		Assert.Equal(true, chat.BodySelectionEnabledForTest(asst));
	}

	[Fact]
	public void MessagesSelectable_DefaultsTrue()
	{
		Assert.True(Build().MessagesSelectable);
	}

	[Fact]
	public void MessagesSelectable_MasterFalse_NullRole_InheritsOff()
	{
		// Master off + role null (inherit) → body not selectable.
		var chat = new ChatTranscriptControl { MessagesSelectable = false };
		var asst = chat.AddMessage(ChatRole.Assistant, "answer");
		Assert.Null(chat.GetRoleStyle(ChatRole.Assistant).Selectable);
		Assert.Equal(false, chat.BodySelectionEnabledForTest(asst));
	}

	[Fact]
	public void MessagesSelectable_MasterFalse_RoleTrue_OptsInRegardlessOfMaster()
	{
		// The key symmetry case: master off, but a role forces Selectable = true → body IS selectable,
		// while a null-role body (inheriting the off master) is not.
		var chat = new ChatTranscriptControl { MessagesSelectable = false };
		chat.SetRoleStyle(ChatRole.Assistant, new ChatRoleStyle { Selectable = true });
		var optedIn = chat.AddMessage(ChatRole.Assistant, "keep me copyable");
		var inherits = chat.AddMessage(ChatRole.User, "echo");
		Assert.Equal(true, chat.BodySelectionEnabledForTest(optedIn));
		Assert.Equal(false, chat.BodySelectionEnabledForTest(inherits));
	}

	[Fact]
	public void MessagesSelectable_ToggleAfterAdd_ReAppliesRespectingRoleOverrides()
	{
		var chat = Build();
		// System forced off; Assistant forced on; User inherits the master.
		chat.SetRoleStyle(ChatRole.System, new ChatRoleStyle { Selectable = false });
		chat.SetRoleStyle(ChatRole.Assistant, new ChatRoleStyle { Selectable = true });
		var sys = chat.AddMessage(ChatRole.System, "notice");
		var asst = chat.AddMessage(ChatRole.Assistant, "answer");
		var usr = chat.AddMessage(ChatRole.User, "echo");
		Assert.Equal(false, chat.BodySelectionEnabledForTest(sys));
		Assert.Equal(true, chat.BodySelectionEnabledForTest(asst));
		Assert.Equal(true, chat.BodySelectionEnabledForTest(usr));

		// Master off → only the inheriting (null) role flips off; the forced roles keep their override.
		chat.MessagesSelectable = false;
		Assert.Equal(false, chat.BodySelectionEnabledForTest(sys));   // forced off
		Assert.Equal(true, chat.BodySelectionEnabledForTest(asst));   // forced on, ignores master
		Assert.Equal(false, chat.BodySelectionEnabledForTest(usr));   // inherits off

		// Master back on → inheriting role returns to on; overrides unchanged.
		chat.MessagesSelectable = true;
		Assert.Equal(false, chat.BodySelectionEnabledForTest(sys));
		Assert.Equal(true, chat.BodySelectionEnabledForTest(asst));
		Assert.Equal(true, chat.BodySelectionEnabledForTest(usr));
	}
}

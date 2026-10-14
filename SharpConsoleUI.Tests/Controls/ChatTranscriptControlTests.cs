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

	#region RestyleMessages — a theme switch must reach messages already on screen

	/* A role style is copied into a message's panel when the message is BUILT, and the body is
	   rendered once from the MarkdownStyle in force at that moment. So after a theme switch the
	   blocks already in the transcript keep the old theme's colours while new ones get the new
	   theme — which is what showed up in cxagent. SetRoleStyle alone cannot fix it, and a host
	   cannot re-add the old messages because the control exposes no way to read their content
	   back. RestyleMessages re-applies the CURRENT role style to every existing entry. */

	[Fact]
	public void RestyleMessages_AppliesTheNewBackgroundToAnExistingMessage()
	{
		var chat = Build();
		var before = new Color(10, 20, 30);
		var after = new Color(200, 100, 50);

		chat.SetRoleStyle(ChatRole.User, new ChatRoleStyle { Background = before });
		var id = chat.AddMessage(ChatRole.User, "hello");
		Assert.Equal(before, chat.BackgroundForTest(id));

		chat.SetRoleStyle(ChatRole.User, new ChatRoleStyle { Background = after });
		chat.RestyleMessages();

		Assert.Equal(after, chat.BackgroundForTest(id));
	}

	[Fact]
	public void RestyleMessages_ClearsABackgroundTheNewStyleDoesNotSet()
	{
		var chat = Build();
		chat.SetRoleStyle(ChatRole.User, new ChatRoleStyle { Background = new Color(10, 20, 30) });
		var id = chat.AddMessage(ChatRole.User, "hello");
		Assert.NotNull(chat.BackgroundForTest(id));

		// A role that drops its background must LOSE it, not keep the old one: leaving the stale
		// colour in place is the same bug seen from the other side.
		chat.SetRoleStyle(ChatRole.User, new ChatRoleStyle());
		chat.RestyleMessages();

		Assert.Null(chat.BackgroundForTest(id));
	}

	[Fact]
	public void RestyleMessages_LeavesTheExpandedStateAlone()
	{
		var chat = Build();
		chat.SetRoleStyle(ChatRole.User, new ChatRoleStyle { Collapsible = true });
		var collapsed = chat.AddMessage(ChatRole.User, "first");
		var expanded = chat.AddMessage(ChatRole.User, "second");

		// What the USER did, which a restyle must not undo.
		chat.PanelForTest(collapsed).IsExpanded = false;
		chat.PanelForTest(expanded).IsExpanded = true;

		// A style that would have started every message collapsed, to prove StartCollapsed is not
		// re-applied over the user's own toggle.
		chat.SetRoleStyle(ChatRole.User, new ChatRoleStyle { Collapsible = true, StartCollapsed = true });
		chat.RestyleMessages();

		Assert.False(chat.PanelForTest(collapsed).IsExpanded);
		Assert.True(chat.PanelForTest(expanded).IsExpanded);
	}

	[Fact]
	public void RestyleMessages_KeepsAPerMessageMarkdownOverride()
	{
		var chat = Build();
		chat.SetRoleStyle(ChatRole.Assistant, new ChatRoleStyle { Markdown = true });
		var id = chat.AddMessage(ChatRole.Assistant, "**bold**");

		// Forced to markup for this message only; the role still says markdown.
		chat.SetMarkdownMode(id, false);
		Assert.False(chat.EffectiveMarkdownForTest(id));

		chat.SetRoleStyle(ChatRole.Assistant, new ChatRoleStyle { Markdown = true });
		chat.RestyleMessages();

		// The override is per-message state, not style state, so a restyle must not revert it.
		Assert.False(chat.MarkdownOverrideForTest(id));
		Assert.False(chat.EffectiveMarkdownForTest(id));
	}

	[Fact]
	public void RestyleMessages_LeavesAThinkingMessageThinking()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.Assistant, "", thinking: true);

		chat.SetRoleStyle(ChatRole.Assistant, new ChatRoleStyle { Background = new Color(1, 2, 3) });
		chat.RestyleMessages();

		// A thinking message has a spinner and no body, so there is nothing to re-render — but the
		// panel still restyles, and the spinner must survive to be cleared by the first token.
		Assert.Equal(new Color(1, 2, 3), chat.BackgroundForTest(id));
		Assert.True(chat.IsThinking(id));

		chat.Append(id, "first token");
		Assert.False(chat.IsThinking(id));
		Assert.Equal("first token", chat.BodyTextForTest(id));
	}

	/* MARKDOWN COLOURS ARE NOT ASSERTABLE FROM HERE, and that is a property of the design rather
	   than a gap in the test. SetMarkdown stores the content as a "[markdown]…[/]" TAG; the tag is
	   resolved against MarkdownStyle.Default by MarkupParser at PAINT time, so the control never
	   holds the colours and RenderedBodyTextForTest returns the same string whatever the style is.
	   Changing the style and re-rendering therefore cannot change anything observable at this level
	   — which is also why a restyle picks up a new MarkdownStyle for free: nothing is baked in.
	   The colour mapping itself is covered at the parser level (MarkdownStyleTests,
	   MarkdownToMarkupTests). What IS checkable here is that the body is genuinely re-rendered from
	   the buffer rather than left alone, which is the part RestyleMessages is responsible for. */

	[Fact]
	public void RestyleMessages_RerendersTheBodyFromTheBuffer()
	{
		var chat = Build();
		chat.SetRoleStyle(ChatRole.Assistant, new ChatRoleStyle { Markdown = true });
		var id = chat.AddMessage(ChatRole.Assistant, "> quoted");

		// Prove the re-render happens by taking the body out of sync with the buffer first: with the
		// message forced to plain markup, the stored body carries no markdown tag.
		chat.SetMarkdownMode(id, false);
		string? asMarkup = chat.RenderedBodyTextForTest(id);
		Assert.NotNull(asMarkup);
		Assert.DoesNotContain("[markdown]", asMarkup!, System.StringComparison.Ordinal);

		// Hand the decision back to the role WITHOUT re-rendering: SetMarkdownMode(id, null) would
		// render on its own, which would let a RestyleMessages that never calls RenderBody still pass.
		// Changing the ROLE's Markdown flag touches no message, so only the restyle can act on it.
		chat.SetMarkdownMode(id, false);
		chat.SetRoleStyle(ChatRole.Assistant, new ChatRoleStyle { Markdown = true });
		chat.SetMarkdownModeWithoutRenderForTest(id, null);
		Assert.DoesNotContain("[markdown]", chat.RenderedBodyTextForTest(id)!, System.StringComparison.Ordinal);

		chat.RestyleMessages();

		string? restyled = chat.RenderedBodyTextForTest(id);
		Assert.NotNull(restyled);
		Assert.Contains("[markdown]", restyled!, System.StringComparison.Ordinal);
		Assert.Contains("quoted", restyled!, System.StringComparison.Ordinal);

		// And the buffer — the source of truth — was not disturbed by any of it.
		Assert.Equal("> quoted", chat.BodyTextForTest(id));
	}

	#endregion
}

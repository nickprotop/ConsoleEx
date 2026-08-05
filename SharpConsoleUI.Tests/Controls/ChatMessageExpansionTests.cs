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
	/// <summary>
	/// Per-MESSAGE collapse control (<see cref="ChatTranscriptControl.SetExpanded"/>), as distinct from
	/// the per-ROLE <see cref="ChatRoleStyle.StartCollapsed"/> default.
	///
	/// <para>The two are different needs. A role like Tool or System is usually worth collapsing —
	/// diagnostics, startup notes, noise — but the occasional message in that role matters and must be
	/// readable without the user opening it: a run reporting that it finished, a warning to act on.
	/// Before this API a consumer could only choose per role, so those lines arrived hidden behind an
	/// "expand…" nobody clicks.</para>
	/// </summary>
	public class ChatMessageExpansionTests
	{
		private static ChatTranscriptControl Build() => new ChatTranscriptControl();

		[Fact]
		public void SetExpanded_OverridesTheRolesStartCollapsedDefault()
		{
			var chat = Build();
			// Tool messages start collapsed by default (see SeedDefaultRoleStyles).
			var id = chat.AddMessage(ChatRole.Tool, "the line the user needs to see");

			Assert.False(chat.IsExpanded(id));

			chat.SetExpanded(id, true);

			Assert.True(chat.IsExpanded(id));
		}

		[Fact]
		public void SetExpanded_CollapsesACollapsibleMessageThatWasExpanded()
		{
			// The inverse direction, on a role that CAN collapse — Tool is collapsible, so a message
			// expanded by the API must be closable again.
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Tool, "detail");
			chat.SetExpanded(id, true);

			Assert.True(chat.IsExpanded(id));

			chat.SetExpanded(id, false);

			Assert.False(chat.IsExpanded(id));
		}

		[Fact]
		public void SetExpanded_CannotCollapseANonCollapsibleRole()
		{
			// Assistant is not Collapsible, and CollapsiblePanel documents that a non-collapsible panel is
			// permanently expanded and ignores attempts to collapse it. Pinned here so this reads as the
			// deliberate behaviour it is, rather than looking like SetExpanded silently failing.
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "chatty detail");

			chat.SetExpanded(id, false);

			Assert.True(chat.IsExpanded(id));
		}

		[Fact]
		public void SetExpanded_AffectsOnlyTheMessageNamed()
		{
			// The point of per-message control: expanding one Tool message must not expand the rest of
			// the role, or this is just StartCollapsed with extra steps.
			var chat = Build();
			var quiet = chat.AddMessage(ChatRole.Tool, "noise");
			var important = chat.AddMessage(ChatRole.Tool, "the result");

			chat.SetExpanded(important, true);

			Assert.True(chat.IsExpanded(important));
			Assert.False(chat.IsExpanded(quiet));
		}

		[Fact]
		public void SetExpanded_UnknownId_Throws()
		{
			// Require(id) is the established lookup for every other per-message accessor; a silent
			// no-op on a bad id would hide a caller bug behind a message that never expands.
			var chat = Build();

			Assert.Throws<KeyNotFoundException>(() => chat.SetExpanded(new ChatMessageId(9999), true));
		}
	}
}

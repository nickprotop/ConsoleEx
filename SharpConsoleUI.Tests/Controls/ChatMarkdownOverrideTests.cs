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
	/// A message may override its role's Markdown chrome. Native markup (per-file colour,
	/// intra-line highlight) must be able to render in a normal Tool row without being forced
	/// into a different role to display correctly.
	/// </summary>
	public class ChatMarkdownOverrideTests
	{
		// A body whose meaning differs between the two paths: as MARKUP this is a red word;
		// as MARKDOWN the tag is literal text.
		private const string Markup = "[red]deleted[/]";

		[Fact]
		public void Override_RendersMarkup_WhileSiblingsInSameRoleStayMarkdown()
		{
			var chat = new ChatTranscriptControl();
			var plain = chat.AddMessage(ChatRole.Assistant, Markup);
			var overridden = chat.AddMessage(ChatRole.Assistant, Markup);

			chat.SetMarkdownMode(overridden, markdown: false);

			// The sibling is untouched: still the role's markdown rendering.
			Assert.True(chat.EffectiveMarkdownForTest(plain));
			Assert.False(chat.EffectiveMarkdownForTest(overridden));
		}

		[Fact]
		public void NoOverride_IsByteIdenticalToToday()
		{
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Assistant, "hello");

			// Default is null → defer to the role, which is Markdown = true.
			Assert.Null(chat.MarkdownOverrideForTest(id));
			Assert.True(chat.EffectiveMarkdownForTest(id));
		}

		[Fact]
		public void Override_SurvivesUpdateMessage()
		{
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Assistant, "start");
			chat.SetMarkdownMode(id, markdown: false);

			chat.UpdateMessage(id, Markup);

			Assert.False(chat.EffectiveMarkdownForTest(id));
			Assert.Equal(Markup, chat.BodyTextForTest(id));
		}

		[Fact]
		public void Override_SurvivesStreamingAppends()
		{
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Tool, "");
			chat.SetMarkdownMode(id, markdown: false);

			chat.Append(id, "[red]a");
			chat.Append(id, "b[/]");

			// RenderBody re-runs from the buffer on every token; the override must not be lost.
			Assert.False(chat.EffectiveMarkdownForTest(id));
			Assert.Equal("[red]ab[/]", chat.BodyTextForTest(id));
		}

		[Fact]
		public void Override_IsReversible_NullRestoresRoleDefault()
		{
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Assistant, "x");

			chat.SetMarkdownMode(id, markdown: false);
			Assert.False(chat.EffectiveMarkdownForTest(id));

			chat.SetMarkdownMode(id, markdown: null);
			Assert.Null(chat.MarkdownOverrideForTest(id));
			Assert.True(chat.EffectiveMarkdownForTest(id)); // back to the role's value
		}

		[Fact]
		public void Override_CanForceMarkdownOnA_NonMarkdownRole()
		{
			var chat = new ChatTranscriptControl();
			// Give a role Markdown = false, then override ONE message back to markdown.
			chat.SetRoleStyle(ChatRole.Tool, new ChatRoleStyle { Markdown = false });

			var plain = chat.AddMessage(ChatRole.Tool, "x");
			var forced = chat.AddMessage(ChatRole.Tool, "x");
			chat.SetMarkdownMode(forced, markdown: true);

			Assert.False(chat.EffectiveMarkdownForTest(plain));
			Assert.True(chat.EffectiveMarkdownForTest(forced));
		}

		[Fact]
		public void AddMessage_CanSeedTheOverride()
		{
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Tool, Markup, author: null,
				actions: null, status: null, markdown: false);

			Assert.False(chat.EffectiveMarkdownForTest(id));
		}

		/// <summary>
		/// The real thing: assert the PAINT path, not a mirrored expression. Markdown rendering wraps
		/// the body in a [markdown] region; markup rendering does not. Two messages in the SAME role,
		/// one overridden, must therefore differ in rendered content — and the difference must survive
		/// a streaming append.
		/// </summary>
		[Fact]
		public void RenderedBody_DiffersBetweenModes_AndSurvivesAppend()
		{
			var chat = new ChatTranscriptControl();
			var plain = chat.AddMessage(ChatRole.Assistant, Markup);
			var overridden = chat.AddMessage(ChatRole.Assistant, Markup);
			chat.SetMarkdownMode(overridden, markdown: false);

			var plainText = chat.RenderedBodyTextForTest(plain);
			var overriddenText = chat.RenderedBodyTextForTest(overridden);

			Assert.Contains("[markdown]", plainText);          // role default: markdown-wrapped
			Assert.DoesNotContain("[markdown]", overriddenText); // override: raw markup
			Assert.Equal(Markup, overriddenText);

			// Streaming must not revert the overridden message to markdown.
			chat.Append(overridden, "[blue]more[/]");
			var after = chat.RenderedBodyTextForTest(overridden);
			Assert.DoesNotContain("[markdown]", after);
			Assert.Equal(Markup + "[blue]more[/]", after);

			// And the untouched sibling is still markdown after its own append.
			chat.Append(plain, "tail");
			Assert.Contains("[markdown]", chat.RenderedBodyTextForTest(plain));
		}

		[Fact]
		public void SetMarkdownMode_OnUnknownId_Throws()
		{
			var chat = new ChatTranscriptControl();
			Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
				() => chat.SetMarkdownMode(new ChatMessageId(), false));
		}
	}
}

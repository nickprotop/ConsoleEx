// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class ChatMessageSpacerTests
	{
		private static ChatTranscriptControl Build() => new ChatTranscriptControl();

		[Fact]
		public void Footer_BottommostRow_HasBottomSpacer()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");

			chat.SetStatus(id, "Copied", NotificationSeverity.Success);

			Assert.Equal(1, chat.FooterBottomMarginForTest(id));
			Assert.Equal(1, chat.StatusBarForTest(id)!.Margin.Bottom);
		}

		[Fact]
		public void Footer_ActionsOnly_ActionsRowHasSpacer()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");

			chat.SetActions(id, new[] { new ChatMessageAction { Id = "copy", Label = "Copy" } });

			Assert.Equal(1, chat.FooterBottomMarginForTest(id));
			Assert.Equal(1, chat.ActionsToolbarForTest(id)!.Margin.Bottom);
		}

		[Fact]
		public void Footer_StatusRemoved_SpacerMovesToActions()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");

			chat.SetActions(id, new[] { new ChatMessageAction { Id = "copy", Label = "Copy" } });
			chat.SetStatus(id, "Copied");

			// While the status row is present it is the bottommost footer row and carries the spacer.
			Assert.Equal(1, chat.StatusBarForTest(id)!.Margin.Bottom);
			Assert.Equal(0, chat.ActionsToolbarForTest(id)!.Margin.Bottom);

			chat.ClearStatus(id);

			// The actions row is now bottommost and picks up the spacer.
			Assert.Equal(1, chat.FooterBottomMarginForTest(id));
			Assert.Equal(1, chat.ActionsToolbarForTest(id)!.Margin.Bottom);
		}

		[Fact]
		public void FooterSeparator_OnBothFooterRows_ThemeDerivedColor()
		{
			// A dim separator line sits above EVERY footer row (actions toolbar AND status row), in a
			// theme-derived color (not a hardcoded literal).
			var chat = Build();

			// Status only → the status row carries the separator.
			var s = chat.AddMessage(ChatRole.Assistant, "a");
			chat.SetStatus(s, "ok");
			Assert.True(chat.StatusBarForTest(s)!.ShowAboveLine);
			Assert.NotNull(chat.StatusBarForTest(s)!.AboveLineColor); // a resolved (theme-derived) color, not null

			// Actions + status → BOTH rows carry the separator.
			var b = chat.AddMessage(ChatRole.Assistant, "b");
			chat.SetActions(b, new[] { new ChatMessageAction { Id = "copy", Label = "Copy" } });
			chat.SetStatus(b, "ok");
			Assert.True(chat.ActionsToolbarForTest(b)!.ShowAboveLine);
			Assert.True(chat.StatusBarForTest(b)!.ShowAboveLine);
			Assert.NotNull(chat.ActionsToolbarForTest(b)!.AboveLineColor);
		}
	}
}

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
	public class ChatMessageFooterTests
	{
		private static ChatTranscriptControl Build() => new ChatTranscriptControl();

		[Fact]
		public void SetStatus_CreatesFooterWithNonStickyTransparentStatusBar()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");
			Assert.False(chat.HasFooterForTest(id));

			chat.SetStatus(id, "Copied", NotificationSeverity.Success);

			Assert.True(chat.HasFooterForTest(id));
			var bar = chat.StatusBarForTest(id);
			Assert.NotNull(bar);
			Assert.Equal(StickyPosition.None, bar!.StickyPosition); // MUST be non-sticky
			Assert.Null(bar.BackgroundColor);                        // transparent
			Assert.False(bar.Outline);
		}

		[Fact]
		public void ClearStatus_RemovesStatusRow_AndFooterWhenEmpty()
		{
			var chat = Build();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");
			chat.SetStatus(id, "Copied");
			chat.ClearStatus(id);
			Assert.Null(chat.StatusBarForTest(id));
			Assert.False(chat.HasFooterForTest(id)); // footer gone (no actions either)
		}

		[Fact]
		public void SetStatus_OnUnknownId_IsNoOp()
		{
			var chat = Build();
			chat.SetStatus(new ChatMessageId(), "x"); // does not throw
		}
	}
}

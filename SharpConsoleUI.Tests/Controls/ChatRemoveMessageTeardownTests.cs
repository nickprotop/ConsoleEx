// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// A message owns more than its panel: an actions toolbar, a status row and a collapsed peek row are
	/// all inserted as SIBLINGS of the panel. RemoveMessage must tear down every one of them, otherwise
	/// the transcript keeps rendering a deleted message's footer chrome.
	/// </summary>
	public class ChatRemoveMessageTeardownTests
	{
		[Fact]
		public void RemoveMessage_RemovesActionsToolbarAndStatusRow()
		{
			var chat = new ChatTranscriptControl();
			var id = chat.AddMessage(ChatRole.Assistant, "hi");
			chat.SetActions(id, new[] { new ChatMessageAction { Id = "copy", Label = "Copy" } });
			chat.SetStatus(id, "Copied", NotificationSeverity.Success);

			var toolbar = chat.ActionsToolbarForTest(id);
			var status = chat.StatusBarForTest(id);
			Assert.NotNull(toolbar);
			Assert.NotNull(status);

			chat.RemoveMessage(id);

			var children = chat.GetChildren();
			Assert.DoesNotContain(toolbar!, children);
			Assert.DoesNotContain(status!, children);
			Assert.Empty(children);
		}

		[Fact]
		public void RemoveMessage_RemovesCollapsedPeekRow()
		{
			var chat = new ChatTranscriptControl { CollapsedPreview = true };
			var id = chat.AddMessage(ChatRole.Tool, "a long body that gets a peek preview row");
			chat.SetExpanded(id, false);

			var peek = chat.PeekRowForTest(id);
			Assert.NotNull(peek);

			chat.RemoveMessage(id);

			Assert.DoesNotContain(peek!, chat.GetChildren());
			Assert.Empty(chat.GetChildren());
		}

		/// <summary>
		/// The real thing: a Fill transcript in a narrow window with several fully-dressed messages
		/// (actions + status + a collapsed peek). Removing the middle message must leave exactly the
		/// children of its neighbours behind, and the state must survive a re-render.
		/// </summary>
		[Fact]
		public void RealThing_RemovingMiddleMessage_LeavesNoOrphanRows()
		{
			const int width = 44, height = 18;
			System.Console.SetIn(System.IO.TextReader.Null);

			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
			var chat = new ChatTranscriptControl
			{
				VerticalAlignment = VerticalAlignment.Fill,
				CollapsedPreview = true
			};
			window.AddControl(chat);
			system.AddWindow(window);

			ChatMessageId Dress(ChatRole role, string text, bool collapse)
			{
				var mid = chat.AddMessage(role, text);
				chat.SetActions(mid, new[] { new ChatMessageAction { Id = "copy", Label = "Copy" }, new ChatMessageAction { Id = "retry", Label = "Retry" } });
				chat.SetStatus(mid, "done", NotificationSeverity.Success);
				if (collapse) chat.SetExpanded(mid, false);
				return mid;
			}

			var first = Dress(ChatRole.User, "please summarise the failing run", false);
			var middle = Dress(ChatRole.Tool, "the regression is in the scroll clamp, a stray measure pass", true);
			var last = Dress(ChatRole.Assistant, "and here is the smallest fix", false);

			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();

			var doomed = new IWindowControl?[]
			{
				chat.PanelForTest(middle),
				chat.ActionsToolbarForTest(middle),
				chat.StatusBarForTest(middle),
				chat.PeekRowForTest(middle)
			}.Where(c => c != null).Select(c => c!).ToList();
			Assert.Equal(4, doomed.Count); // panel + toolbar + status + peek

			int before = chat.GetChildren().Count;

			chat.RemoveMessage(middle);
			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay(); // state must survive a re-render

			var children = chat.GetChildren();
			foreach (var orphan in doomed)
				Assert.DoesNotContain(orphan, children);

			Assert.Equal(before - doomed.Count, children.Count);

			// Neighbours are untouched.
			Assert.Contains((IWindowControl)chat.PanelForTest(first), children);
			Assert.Contains((IWindowControl)chat.PanelForTest(last), children);
			Assert.Contains((IWindowControl)chat.StatusBarForTest(last)!, children);
		}
	}
}
